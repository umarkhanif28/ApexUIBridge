using FlaUI.Core.Definitions;

namespace ApexUIBridge.Core;

/// <summary>
/// Filter options for element searching.
/// IncludeControlTypes and IncludeNames are built into FlaUI ConditionFactory Or-chains.
/// ExcludeControlTypes and ExcludeNames are applied via LINQ after the FlaUI search.
/// </summary>
public class ElementSearchFilter
{
    /// <summary>
    /// Only include elements with these ControlTypes.
    /// Built into a FlaUI Or-condition chain: ByControlType(A).Or(ByControlType(B)).Or(...)
    /// Null or empty means no ControlType filtering (all types included).
    /// </summary>
    public HashSet<ControlType>? IncludeControlTypes { get; set; }

    /// <summary>
    /// Exclude elements with these ControlTypes.
    /// Applied via LINQ .Where() after the FlaUI condition search.
    /// </summary>
    public HashSet<ControlType>? ExcludeControlTypes { get; set; }

    /// <summary>
    /// Only include elements whose Name exactly matches one of these values.
    /// Built into a FlaUI Or-condition chain: ByName(A).Or(ByName(B)).Or(...)
    /// Null or empty means no name inclusion filtering.
    /// </summary>
    public HashSet<string>? IncludeNames { get; set; }

    /// <summary>
    /// Exclude elements whose Name matches one of these values (case-insensitive).
    /// Applied via LINQ .Where() after the FlaUI condition search.
    /// </summary>
    public HashSet<string>? ExcludeNames { get; set; }

    /// <summary>
    /// Minimum fuzzy match score (0.0 to 1.0) for "closest matching name" searches.
    /// Default is 0.5.
    /// </summary>
    public double MinFuzzyScore { get; set; } = 0.5;

    /// <summary>
    /// Maximum number of results to return. 0 means unlimited. Default is 10.
    /// </summary>
    public int MaxResults { get; set; } = 10;
}
