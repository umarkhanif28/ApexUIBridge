using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;

namespace ApexUIBridge.Core;

/// <summary>
/// Service for finding windows and elements using FlaUI ConditionFactory conditions,
/// LINQ filtering, and fuzzy text matching (Levenshtein distance).
///
/// ControlType/Name inclusion filters are pushed into FlaUI Or-condition chains.
/// ControlType/Name exclusion filters are applied via LINQ after the search.
/// Fuzzy matching is used for "closest matching name" lookups.
/// </summary>
public sealed class ElementFinderService
{
    private readonly AutomationBase _automation;
    private readonly ConditionFactory _cf;

    public ElementFinderService(AutomationBase automation)
    {
        _automation = automation;
        _cf = new ConditionFactory(automation.PropertyLibrary);
    }

    #region 1. Find Window

    /// <summary>
    /// Finds a window on the desktop by AutomationId, Name, or closest matching name.
    /// Searches direct children of the desktop (top-level windows).
    /// If no IncludeControlTypes is specified in the filter, defaults to ControlType.Window.
    /// </summary>
    public ElementFinderResult? FindWindow(string searchText, ElementSearchFilter? filter = null)
    {
        filter ??= new ElementSearchFilter();
        var desktop = _automation.GetDesktop();

        // Build FlaUI condition — defaults to Window type when no ControlType filter specified
        var condition = BuildIncludeCondition(filter, defaultControlType: ControlType.Window);

        var windows = condition != null
            ? desktop.FindAllChildren(condition)
            : desktop.FindAllChildren();

        // Apply exclusion filters via LINQ
        var candidates = ApplyExclusionFilters(windows, filter);

        return FindBestMatch(candidates, searchText, filter);
    }

    #endregion

    #region 2. Find Element

    /// <summary>
    /// Finds a single element within a window by AutomationId, Name, or closest matching name.
    /// Searches all descendants of the window.
    /// </summary>
    public ElementFinderResult? FindElement(AutomationElement window, string searchText, ElementSearchFilter? filter = null)
    {
        filter ??= new ElementSearchFilter();

        // Build FlaUI condition from filter
        var condition = BuildIncludeCondition(filter);

        // Try exact AutomationId match first — combine with filter condition
        var idCondition = (ConditionBase)_cf.ByAutomationId(searchText);
        var combinedIdCondition = condition != null ? idCondition.And(condition) : idCondition;
        var byId = window.FindFirstDescendant(combinedIdCondition);
        if (byId != null && PassesExclusionFilters(byId, filter))
            return new ElementFinderResult { Element = byId, Score = 1.0, MatchedField = "AutomationId" };

        // Try exact Name match — combine with filter condition
        var nameCondition = (ConditionBase)_cf.ByName(searchText);
        var combinedNameCondition = condition != null ? nameCondition.And(condition) : nameCondition;
        var byName = window.FindFirstDescendant(combinedNameCondition);
        if (byName != null && PassesExclusionFilters(byName, filter))
            return new ElementFinderResult { Element = byName, Score = 1.0, MatchedField = "Name" };

        // Try ElementRegistry lookup if search text is numeric (deterministic ElementId)
        if (int.TryParse(searchText, out var elementId))
        {
            var registryElement = ElementRegistry.FindAutomationElementById(elementId);
            if (registryElement != null && PassesExclusionFilters(registryElement, filter))
                return new ElementFinderResult { Element = registryElement, Score = 1.0, MatchedField = "ElementId" };
        }

        // Fuzzy match: get all descendants with include condition, then score
        var allElements = condition != null
            ? window.FindAllDescendants(condition)
            : window.FindAllDescendants();

        var candidates = ApplyExclusionFilters(allElements, filter);
        return ScoreAndRank(candidates, searchText, filter.MinFuzzyScore)
            .FirstOrDefault();
    }

    /// <summary>
    /// Finds a single element within a window identified by search text.
    /// First finds the window, then searches for the element within it.
    /// </summary>
    public ElementFinderResult? FindElement(string windowSearch, string elementSearch,
        ElementSearchFilter? windowFilter = null, ElementSearchFilter? elementFilter = null)
    {
        var windowResult = FindWindow(windowSearch, windowFilter);
        return windowResult != null ? FindElement(windowResult.Element, elementSearch, elementFilter) : null;
    }

    #endregion

    #region 3. Find All Elements

    /// <summary>
    /// Finds all elements within a window, filtered by the search filter.
    /// Uses FlaUI conditions for ControlType/Name inclusion, LINQ for exclusions.
    /// </summary>
    public List<ElementFinderResult> FindAllElements(AutomationElement window, ElementSearchFilter? filter = null)
    {
        filter ??= new ElementSearchFilter();

        var condition = BuildIncludeCondition(filter);
        var elements = condition != null
            ? window.FindAllDescendants(condition)
            : window.FindAllDescendants();

        var results = ApplyExclusionFilters(elements, filter)
            .Select(e => new ElementFinderResult
            {
                Element = e,
                Score = 1.0,
                MatchedField = "Filter"
            })
            .ToList();

        if (filter.MaxResults > 0)
            results = results.Take(filter.MaxResults).ToList();

        return results;
    }

    /// <summary>
    /// Finds all elements within a window identified by search text.
    /// First finds the window, then returns all matching elements within it.
    /// </summary>
    public List<ElementFinderResult> FindAllElements(string windowSearch,
        ElementSearchFilter? windowFilter = null, ElementSearchFilter? elementFilter = null)
    {
        var windowResult = FindWindow(windowSearch, windowFilter);
        return windowResult != null ? FindAllElements(windowResult.Element, elementFilter) : [];
    }

    #endregion

    #region Condition Building (FlaUI ConditionFactory And/Or chains)

    /// <summary>
    /// Builds a FlaUI condition from the include filters using ConditionFactory Or/And chains.
    /// IncludeControlTypes → ByControlType(A).Or(ByControlType(B)).Or(...)
    /// IncludeNames → ByName(A).Or(ByName(B)).Or(...)
    /// Combined with .And() if both are present.
    /// Returns null if no include conditions apply.
    /// </summary>
    private ConditionBase? BuildIncludeCondition(ElementSearchFilter filter, ControlType? defaultControlType = null)
    {
        var conditions = new List<ConditionBase>();

        // ControlType inclusion — Or-chain
        if (filter.IncludeControlTypes is { Count: > 0 })
        {
            conditions.Add(
                filter.IncludeControlTypes
                    .Select(ct => (ConditionBase)_cf.ByControlType(ct))
                    .Aggregate((current, next) => current.Or(next)));
        }
        else if (defaultControlType.HasValue)
        {
            conditions.Add(_cf.ByControlType(defaultControlType.Value));
        }

        // Name inclusion — Or-chain
        if (filter.IncludeNames is { Count: > 0 })
        {
            conditions.Add(
                filter.IncludeNames
                    .Select(name => (ConditionBase)_cf.ByName(name))
                    .Aggregate((current, next) => current.Or(next)));
        }

        return conditions.Count switch
        {
            0 => null,
            1 => conditions[0],
            _ => conditions.Aggregate((current, next) => current.And(next))
        };
    }

    #endregion

    #region LINQ Exclusion Filters

    /// <summary>
    /// Applies exclusion filters via LINQ (for ControlTypes and Names to exclude).
    /// </summary>
    private static IEnumerable<AutomationElement> ApplyExclusionFilters(
        AutomationElement[] elements, ElementSearchFilter filter)
    {
        IEnumerable<AutomationElement> result = elements;

        if (filter.ExcludeControlTypes is { Count: > 0 })
        {
            var excluded = filter.ExcludeControlTypes;
            result = result.Where(e => !excluded.Contains(e.Properties.ControlType.ValueOrDefault));
        }

        if (filter.ExcludeNames is { Count: > 0 })
        {
            var excluded = filter.ExcludeNames;
            result = result.Where(e =>
            {
                var name = e.Properties.Name.ValueOrDefault ?? "";
                return !excluded.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));
            });
        }

        return result;
    }

    /// <summary>
    /// Checks if a single element passes the exclusion filters.
    /// </summary>
    private static bool PassesExclusionFilters(AutomationElement element, ElementSearchFilter filter)
    {
        if (filter.ExcludeControlTypes is { Count: > 0 } &&
            filter.ExcludeControlTypes.Contains(element.Properties.ControlType.ValueOrDefault))
            return false;

        if (filter.ExcludeNames is { Count: > 0 })
        {
            var name = element.Properties.Name.ValueOrDefault ?? "";
            if (filter.ExcludeNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    #endregion

    #region Matching Logic

    /// <summary>
    /// Finds the best match among candidates: exact AutomationId → exact Name → ElementRegistry → fuzzy.
    /// </summary>
    private ElementFinderResult? FindBestMatch(
        IEnumerable<AutomationElement> candidates, string searchText, ElementSearchFilter filter)
    {
        var candidateList = candidates.ToList();

        // Exact AutomationId match
        var exactId = candidateList.FirstOrDefault(e =>
            string.Equals(e.Properties.AutomationId.ValueOrDefault, searchText, StringComparison.OrdinalIgnoreCase));
        if (exactId != null)
            return new ElementFinderResult { Element = exactId, Score = 1.0, MatchedField = "AutomationId" };

        // Exact Name match
        var exactName = candidateList.FirstOrDefault(e =>
            string.Equals(e.Properties.Name.ValueOrDefault, searchText, StringComparison.OrdinalIgnoreCase));
        if (exactName != null)
            return new ElementFinderResult { Element = exactName, Score = 1.0, MatchedField = "Name" };

        // ElementRegistry lookup if search text is numeric (deterministic ElementId)
        if (int.TryParse(searchText, out var elementId))
        {
            var registryElement = ElementRegistry.FindAutomationElementById(elementId);
            if (registryElement != null)
                return new ElementFinderResult { Element = registryElement, Score = 1.0, MatchedField = "ElementId" };
        }

        // Fuzzy match
        return ScoreAndRank(candidateList, searchText, filter.MinFuzzyScore)
            .FirstOrDefault();
    }

    /// <summary>
    /// Scores elements by fuzzy similarity to searchText using LINQ, sorted best-first.
    /// </summary>
    private static IEnumerable<ElementFinderResult> ScoreAndRank(
        IEnumerable<AutomationElement> elements, string searchText, double minScore)
    {
        return elements
            .Select(e =>
            {
                var nameScore = CalculateSimilarity(e.Properties.Name.ValueOrDefault ?? "", searchText);
                var idScore = CalculateSimilarity(e.Properties.AutomationId.ValueOrDefault ?? "", searchText);
                var bestScore = Math.Max(nameScore, idScore);
                var matchedField = idScore > nameScore ? "AutomationId" : "Name";

                return new ElementFinderResult
                {
                    Element = e,
                    Score = bestScore,
                    MatchedField = matchedField
                };
            })
            .Where(r => r.Score >= minScore)
            .OrderByDescending(r => r.Score);
    }

    #endregion

    #region Fuzzy Matching (Levenshtein Distance)

    /// <summary>
    /// Calculates similarity between two strings using Levenshtein distance.
    /// Returns a score between 0.0 (no match) and 1.0 (perfect match).
    /// </summary>
    private static double CalculateSimilarity(string str1, string str2)
    {
        if (string.IsNullOrEmpty(str1) || string.IsNullOrEmpty(str2))
            return 0.0;

        str1 = str1.ToLowerInvariant();
        str2 = str2.ToLowerInvariant();

        if (str1 == str2)
            return 1.0;

        if (str1.Contains(str2) || str2.Contains(str1))
            return 0.8;

        var distance = LevenshteinDistance(str1, str2);
        var maxLength = Math.Max(str1.Length, str2.Length);
        return 1.0 - ((double)distance / maxLength);
    }

    /// <summary>
    /// Calculates the Levenshtein distance between two strings.
    /// </summary>
    private static int LevenshteinDistance(string str1, string str2)
    {
        var len1 = str1.Length;
        var len2 = str2.Length;
        var matrix = new int[len1 + 1, len2 + 1];

        if (len1 == 0) return len2;
        if (len2 == 0) return len1;

        for (var i = 0; i <= len1; i++)
            matrix[i, 0] = i;

        for (var j = 0; j <= len2; j++)
            matrix[0, j] = j;

        for (var i = 1; i <= len1; i++)
        {
            for (var j = 1; j <= len2; j++)
            {
                var cost = str1[i - 1] == str2[j - 1] ? 0 : 1;

                matrix[i, j] = Math.Min(
                    Math.Min(
                        matrix[i - 1, j] + 1,      // deletion
                        matrix[i, j - 1] + 1),     // insertion
                    matrix[i - 1, j - 1] + cost);  // substitution
            }
        }

        return matrix[len1, len2];
    }

    #endregion
}
