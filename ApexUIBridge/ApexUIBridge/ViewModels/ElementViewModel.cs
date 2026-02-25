using ApexUIBridge.Core;
using ApexUIBridge.Core.Extensions;
using ApexUIBridge.Core.Logger;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Identifiers;
using FlaUI.Core.Patterns;
using FlaUI.Core.Tools;
using FlaUI.UIA3.Identifiers;

namespace ApexUIBridge.ViewModels;

/// <summary>
/// Wraps a single <see cref="FlaUI.Core.AutomationElements.AutomationElement"/> for
/// display in the UI element tree.
///
/// <para>On construction the view model:</para>
/// <list type="number">
///   <item>Reads the most-used properties (Name, AutomationId, ControlType) eagerly.</item>
///   <item>Generates a deterministic <see cref="ElementHash"/> and integer
///       <see cref="ElementId"/> via <see cref="Core.ElementIdGenerator"/> — the hash
///       encodes parent hash, control type, class name, automation ID, framework ID,
///       process name, element name, and sibling index.</item>
///   <item>Registers itself in the global <see cref="Core.ElementRegistry"/> so that
///       other subsystems (e.g. the direct-click toolbar) can look elements up by ID
///       without walking the tree again.</item>
/// </list>
///
/// <para>Children are loaded lazily by <see cref="LoadChildren"/>, which is called
/// by <see cref="ProcessViewModel.ExpandElement"/> when the user expands a tree node.</para>
///
/// <para><see cref="LoadDetails"/> performs a cached UIA property fetch and enumerates
/// all supported patterns (Toggle, Value, Window, Selection, etc.), returning
/// <see cref="DetailGroupViewModel"/> objects suitable for a property grid display.
/// <see cref="ParseAllDetails"/> converts those into a plain
/// <c>Dictionary&lt;string, object&gt;</c> used for JSON serialisation.</para>
/// </summary>
public class ElementViewModel : ObservableObject {
    private static readonly ElementIdGenerator _idGenerator = new();

    private readonly string _guidId;

    private readonly object _lockObject = new ();
    private readonly ILogger? _logger;

    public ElementViewModel(AutomationElement? automationElement, ElementViewModel? parent, int level, ILogger? logger, int siblingIndex = 0) {
        Level = level;
        _logger = logger;
        AutomationElement = automationElement;
        Parent = parent;

        _guidId = Guid.NewGuid().ToString() + (AutomationElement?.Properties.Name.ValueOrDefault ?? string.Empty).NormalizeString();
        Name = (AutomationElement?.Properties.Name.ValueOrDefault ?? string.Empty).NormalizeString();
        AutomationId = (AutomationElement?.Properties.AutomationId.ValueOrDefault ?? string.Empty).NormalizeString();
        ControlType = AutomationElement != null && AutomationElement.Properties.ControlType.TryGetValue(out ControlType value) ? value : ControlType.Unknown;

        // Generate deterministic element ID
        if (AutomationElement != null) {
            var isWindowOrPane = ControlType == ControlType.Window || ControlType == ControlType.Pane;
            var effectiveWindowHandle = isWindowOrPane
                ? AutomationElement.Properties.NativeWindowHandle.ValueOrDefault
                : IntPtr.Zero;

            ElementHash = _idGenerator.GenerateElementHash(
                AutomationElement,
                parent?.ElementId,
                parent?.ElementHash,
                effectiveWindowHandle,
                excludeName: isWindowOrPane,
                siblingIndex);

            ElementId = _idGenerator.GenerateIdFromHash(ElementHash);

            // Register in global registry for lookup by ID
            ElementRegistry.Register(this);
        }
    }

    public ExtendedObservableCollection<ElementViewModel?> Children
    {
        get => GetProperty<ExtendedObservableCollection<ElementViewModel?>>() ?? new ExtendedObservableCollection<ElementViewModel?>();
        set => SetProperty(value);
    }

    public ExtendedObservableCollection<DetailGroupViewModel> ItemDetails
    {
        get => GetProperty<ExtendedObservableCollection<DetailGroupViewModel>>() ?? new ExtendedObservableCollection<DetailGroupViewModel>();
        set => SetProperty(value);
    }


    public AutomationElement? AutomationElement { get; }
    public ElementViewModel? Parent { get; }

    public bool IsExpanded {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public bool IsSelected {
        get => GetProperty<bool>();
        set => SetProperty(value);
    }

    public int Level { get; }

    public int MaxDepth { get; } = 25;

    public string Name { get; }

    public string AutomationId { get; }

    public ControlType ControlType { get; }

    public string? ElementHash { get; }
    public int ElementId { get; }

    public string XPath => AutomationElement == null ? string.Empty : Debug.GetXPathToElement(AutomationElement);

    public override string ToString() {
        return $"{Name} [{ControlType}] : {AutomationId} (#{ElementId})";
    }

    public List<ElementViewModel> LoadChildren() 
        {


            try {
                if (AutomationElement != null && Level < MaxDepth) {
                    using (CacheRequest.ForceNoCache()) {
                        AutomationElement[] elements = AutomationElement.FindAllChildren();

                        return elements.Select((element, index) => new ElementViewModel(element, this, Level + 1, _logger, index)).ToList();
                    }
                }
            } catch (Exception ex) {
                _logger?.LogError($"Exception: {ex.Message}");
            }

            return [];
        }






            /// <summary>
            /// Helper method to find the first Document element in the subtree
            /// </summary>
    private AutomationElement? FindFirstDocumentInSubtree(AutomationElement? element)
    {
        if (element == null) return null;

        try
        {
            // Check if current element is Document
            if (element.Properties.ControlType.ValueOrDefault == ControlType.Document)
            {
                return element;
            }

            // Search descendants for Document
            var descendants = element.FindAllDescendants();
            foreach (var descendant in descendants)
            {
                try
                {
                    if (descendant.Properties.ControlType.ValueOrDefault == ControlType.Document)
                    {
                        return descendant;
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            //Debug.WriteLine($"Error finding Document element: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Get URL from web elements (hyperlinks)
    /// </summary>
    public string GetUrlFromWebElement(AutomationElement automationElement)
    {
        if (automationElement == null) return null;

        // Strategy 1: ValuePattern
        if (automationElement.Patterns.Value.TryGetPattern(out var valuePattern))
        {
            string potentialUrl = valuePattern.Value;
            if (!string.IsNullOrEmpty(potentialUrl) && Uri.IsWellFormedUriString(potentialUrl, UriKind.Absolute))
                return potentialUrl;
        }

        // Strategy 2: LegacyIAccessible Pattern
        if (automationElement.Patterns.LegacyIAccessible.TryGetPattern(out var legacyPattern))
        {
            string potentialUrl = legacyPattern.Value;
            if (!string.IsNullOrEmpty(potentialUrl) && Uri.IsWellFormedUriString(potentialUrl, UriKind.Absolute))
                return potentialUrl;
        }

        return null;
    }

    private ElementViewModel? FindElement(ElementViewModel parent, AutomationElement element)
    {
        return parent.Children.FirstOrDefault(child =>
        {
            if (child?.AutomationElement == null)
            {
                return false;
            }

            try
            {
                return child.AutomationElement.Equals(element);
            }
            catch (Exception e)
            {
                 
            }

            return false;
        });
    }





    public List<DetailGroupViewModel> LoadDetails()
    {
        var detailGroups = new List<DetailGroupViewModel>();
        var cacheRequest = new CacheRequest();
        cacheRequest.TreeScope = TreeScope.Element;
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.AutomationId);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.Name);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.ClassName);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.ControlType);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.LocalizedControlType);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.FrameworkId);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.ProcessId);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.IsEnabled);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.IsOffscreen);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.BoundingRectangle);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.HelpText);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.IsPassword);
        cacheRequest.Add(AutomationElement.Automation.PropertyLibrary.Element.NativeWindowHandle);

        using (cacheRequest.Activate())
        {
            var elementCached = AutomationElement.FindFirst(TreeScope.Element, TrueCondition.Default);
            if (elementCached != null)
            {
                // Element identification
                var identification = new List<IDetailViewModel>
                {
                        DetailViewModel.FromAutomationProperty("AutomationId", elementCached.Properties.AutomationId),
                        DetailViewModel.FromAutomationProperty("Name", elementCached.Properties.Name),
                        DetailViewModel.FromAutomationProperty("ClassName", elementCached.Properties.ClassName),
                        DetailViewModel.FromAutomationProperty("ControlType", elementCached.Properties.ControlType),
                        DetailViewModel.FromAutomationProperty("LocalizedControlType", elementCached.Properties.LocalizedControlType),
                        new DetailViewModel("FrameworkType", elementCached.FrameworkType.ToString()),
                        DetailViewModel.FromAutomationProperty("FrameworkId", elementCached.Properties.FrameworkId),
                        DetailViewModel.FromAutomationProperty("ProcessId", elementCached.Properties.ProcessId),
                    };
                detailGroups.Add(new DetailGroupViewModel("Identification", identification));

                // Element details
                var details = new List<DetailViewModel>
                {
                        DetailViewModel.FromAutomationProperty("IsEnabled", elementCached.Properties.IsEnabled),
                        DetailViewModel.FromAutomationProperty("IsOffscreen", elementCached.Properties.IsOffscreen),
                        DetailViewModel.FromAutomationProperty("BoundingRectangle", elementCached.Properties.BoundingRectangle),
                        DetailViewModel.FromAutomationProperty("HelpText", elementCached.Properties.HelpText),
                        DetailViewModel.FromAutomationProperty("IsPassword", elementCached.Properties.IsPassword)
                    };

                // Special handling for NativeWindowHandle
                var nativeWindowHandle = elementCached.Properties.NativeWindowHandle.ValueOrDefault;
                var nativeWindowHandleString = "Not Supported";
                if (nativeWindowHandle != default(IntPtr))
                {
                    nativeWindowHandleString = String.Format("{0} ({0:X8})", nativeWindowHandle.ToInt32());
                }
                details.Add(new DetailViewModel("NativeWindowHandle", nativeWindowHandleString));
                detailGroups.Add(new DetailGroupViewModel("Details", details));
            }
        }

        //return detailGroups;

        // Pattern details
        var allSupportedPatterns = AutomationElement.GetSupportedPatterns();
        var allPatterns = AutomationElement.Automation.PatternLibrary.AllForCurrentFramework;
        var patterns = new List<DetailViewModel>();

        foreach (var pattern in allPatterns)
        {
            var hasPattern = allSupportedPatterns.Contains(pattern);
            /*if(hasPattern) */
            patterns.Add(new DetailViewModel(pattern.Name + "Pattern", hasPattern ? "Yes" : "No") { Important = hasPattern });
        }

        detailGroups.Add(new DetailGroupViewModel("Pattern Support", patterns));

        // GridItemPattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.GridItemPattern))
        {
            var pattern = AutomationElement.Patterns.GridItem.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("Column", pattern.Column),
                    DetailViewModel.FromAutomationProperty("ColumnSpan", pattern.ColumnSpan),
                    DetailViewModel.FromAutomationProperty("Row", pattern.Row),
                    DetailViewModel.FromAutomationProperty("RowSpan", pattern.RowSpan),
                    DetailViewModel.FromAutomationProperty("ContainingGrid", pattern.ContainingGrid)
                };
            detailGroups.Add(new DetailGroupViewModel("GridItem Pattern", patternDetails));
        }
        // GridPattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.GridPattern))
        {
            var pattern = AutomationElement.Patterns.Grid.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("ColumnCount", pattern.ColumnCount),
                    DetailViewModel.FromAutomationProperty("RowCount", pattern.RowCount)
                };
            detailGroups.Add(new DetailGroupViewModel("Grid Pattern", patternDetails));
        }

        // LegacyIAccessiblePattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.LegacyIAccessiblePattern))
        {
            var pattern = AutomationElement.Patterns.LegacyIAccessible.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                   DetailViewModel.FromAutomationProperty("Name", pattern.Name),
                   new DetailViewModel("State", AccessibilityTextResolver.GetStateText(pattern.State.ValueOrDefault)),
                   new DetailViewModel("Role", AccessibilityTextResolver.GetRoleText(pattern.Role.ValueOrDefault)),
                   DetailViewModel.FromAutomationProperty("Value", pattern.Value),
                   DetailViewModel.FromAutomationProperty("ChildId", pattern.ChildId),
                   DetailViewModel.FromAutomationProperty("DefaultAction", pattern.DefaultAction),
                   DetailViewModel.FromAutomationProperty("Description", pattern.Description),
                   DetailViewModel.FromAutomationProperty("Help", pattern.Help),
                   DetailViewModel.FromAutomationProperty("KeyboardShortcut", pattern.KeyboardShortcut),
                   DetailViewModel.FromAutomationProperty("Selection", pattern.Selection)
                };
            detailGroups.Add(new DetailGroupViewModel("LegacyIAccessible Pattern", patternDetails));
        }

        // RangeValuePattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.RangeValuePattern))
        {
            var pattern = AutomationElement.Patterns.RangeValue.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                   DetailViewModel.FromAutomationProperty("IsReadOnly", pattern.IsReadOnly),
                   DetailViewModel.FromAutomationProperty("SmallChange", pattern.SmallChange),
                   DetailViewModel.FromAutomationProperty("LargeChange", pattern.LargeChange),
                   DetailViewModel.FromAutomationProperty("Minimum", pattern.Minimum),
                   DetailViewModel.FromAutomationProperty("Maximum", pattern.Maximum),
                   DetailViewModel.FromAutomationProperty("Value", pattern.Value)
                };
            detailGroups.Add(new DetailGroupViewModel("RangeValue Pattern", patternDetails));
        }
        // ScrollPattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.ScrollPattern))
        {
            var pattern = AutomationElement.Patterns.Scroll.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("HorizontalScrollPercent", pattern.HorizontalScrollPercent),
                    DetailViewModel.FromAutomationProperty("HorizontalViewSize", pattern.HorizontalViewSize),
                    DetailViewModel.FromAutomationProperty("HorizontallyScrollable", pattern.HorizontallyScrollable),
                    DetailViewModel.FromAutomationProperty("VerticalScrollPercent", pattern.VerticalScrollPercent),
                    DetailViewModel.FromAutomationProperty("VerticalViewSize", pattern.VerticalViewSize),
                    DetailViewModel.FromAutomationProperty("VerticallyScrollable", pattern.VerticallyScrollable)
                };
            detailGroups.Add(new DetailGroupViewModel("Scroll Pattern", patternDetails));
        }
        // SelectionItemPattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.SelectionItemPattern))
        {
            var pattern = AutomationElement.Patterns.SelectionItem.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("IsSelected", pattern.IsSelected),
                    DetailViewModel.FromAutomationProperty("SelectionContainer", pattern.SelectionContainer)
                };
            detailGroups.Add(new DetailGroupViewModel("SelectionItem Pattern", patternDetails));
        }
        // SelectionPattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.SelectionPattern))
        {
            var pattern = AutomationElement.Patterns.Selection.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("Selection", pattern.Selection),
                    DetailViewModel.FromAutomationProperty("CanSelectMultiple", pattern.CanSelectMultiple),
                    DetailViewModel.FromAutomationProperty("IsSelectionRequired", pattern.IsSelectionRequired)
                };
            detailGroups.Add(new DetailGroupViewModel("Selection Pattern", patternDetails));
        }
        // TableItemPattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.TableItemPattern))
        {
            var pattern = AutomationElement.Patterns.TableItem.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("ColumnHeaderItems", pattern.ColumnHeaderItems),
                    DetailViewModel.FromAutomationProperty("RowHeaderItems", pattern.RowHeaderItems)
                };
            detailGroups.Add(new DetailGroupViewModel("TableItem Pattern", patternDetails));
        }
        // TablePattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.TablePattern))
        {
            var pattern = AutomationElement.Patterns.Table.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("ColumnHeaderItems", pattern.ColumnHeaders),
                    DetailViewModel.FromAutomationProperty("RowHeaderItems", pattern.RowHeaders),
                    DetailViewModel.FromAutomationProperty("RowOrColumnMajor", pattern.RowOrColumnMajor)
                };
            detailGroups.Add(new DetailGroupViewModel("Table Pattern", patternDetails));
        }
        // TextPattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.TextPattern))
        {
            var pattern = AutomationElement.Patterns.Text.Pattern;

            // TODO: This can in the future be replaced with automation.MixedAttributeValue
            object mixedValue = AutomationElement.AutomationType == AutomationType.UIA2
                ? System.Windows.Automation.TextPattern.MixedAttributeValue
                : ((FlaUI.UIA3.UIA3Automation)AutomationElement.Automation).NativeAutomation.ReservedMixedAttributeValue;

            var foreColor = GetTextAttributeNew<int>(pattern, TextAttributes.ForegroundColor, mixedValue, (x) =>
            {
                return $"{System.Drawing.Color.FromArgb(x)} ({x})";
            }, AutomationElement);

            var backColor = GetTextAttributeNew<int>(pattern, TextAttributes.BackgroundColor, mixedValue, (x) =>
            {
                return $"{System.Drawing.Color.FromArgb(x)} ({x})";
            }, AutomationElement);

            var fontName = GetTextAttributeNew<string>(pattern, TextAttributes.FontName, mixedValue, (x) =>
            {
                return $"{x}";
            }, AutomationElement);

            var fontSize = GetTextAttributeNew<double>(pattern, TextAttributes.FontSize, mixedValue, (x) =>
            {
                return $"{x}";
            }, AutomationElement);

            var fontWeight = GetTextAttributeNew<int>(pattern, TextAttributes.FontWeight, mixedValue, (x) =>
            {
                return $"{x}";
            }, AutomationElement);

            var patternDetails = new List<DetailViewModel>
            {
                    new DetailViewModel("ForeColor", foreColor),
                    new DetailViewModel("BackgroundColor", backColor),
                    new DetailViewModel("FontName", fontName),
                    new DetailViewModel("FontSize", fontSize),
                    new DetailViewModel("FontWeight", fontWeight),
                };
            detailGroups.Add(new DetailGroupViewModel("Text Pattern", patternDetails));
        }
        // TogglePattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.TogglePattern))
        {
            var pattern = AutomationElement.Patterns.Toggle.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("ToggleState", pattern.ToggleState)
                };
            detailGroups.Add(new DetailGroupViewModel("Toggle Pattern", patternDetails));
        }

        // ValuePattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.ValuePattern))
        {
            var pattern = AutomationElement.Patterns.Value.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("IsReadOnly", pattern.IsReadOnly),
                    DetailViewModel.FromAutomationProperty("Value", pattern.Value)
                };
            detailGroups.Add(new DetailGroupViewModel("Value Pattern", patternDetails));
        }

        // WindowPattern
        if (allSupportedPatterns.Contains(AutomationElement.Automation.PatternLibrary.WindowPattern))
        {
            var pattern = AutomationElement.Patterns.Window.Pattern;
            var patternDetails = new List<DetailViewModel>
            {
                    DetailViewModel.FromAutomationProperty("IsModal", pattern.IsModal),
                    DetailViewModel.FromAutomationProperty("IsTopmost", pattern.IsTopmost),
                    DetailViewModel.FromAutomationProperty("CanMinimize", pattern.CanMinimize),
                    DetailViewModel.FromAutomationProperty("CanMaximize", pattern.CanMaximize),
                    DetailViewModel.FromAutomationProperty("WindowVisualState", pattern.WindowVisualState),
                    DetailViewModel.FromAutomationProperty("WindowInteractionState", pattern.WindowInteractionState)
                };
            detailGroups.Add(new DetailGroupViewModel("Window Pattern", patternDetails));
        }

        return detailGroups;
    }

    private string GetTextAttributeNew<T>(ITextPattern pattern, TextAttributeId textAttribute, object mixedValue, Func<T, string> func, AutomationElement _automationElement)
    {
        var value = pattern.DocumentRange.GetAttributeValue(textAttribute);

        if (value == mixedValue)
        {
            return "Mixed";
        }
        else if (value == _automationElement.Automation.NotSupportedValue)
        {
            return "Not supported";
        }
        else
        {
            try
            {
                var converted = (T)value;
                return func(converted);
            }
            catch
            {
                return $"Conversion to ${typeof(T)} failed";
            }
        }
    }

    public Dictionary<string, object> ParseAllDetails(List<DetailGroupViewModel> detailGroups)
    {
        var comprehensiveReport = new Dictionary<string, object>();
        if (detailGroups == null || !detailGroups.Any()) return comprehensiveReport;

        foreach (var group in detailGroups)
        {
            // For most groups, we create a dictionary of the key-value pairs.
            var groupDetails = group.Details
                .GroupBy(d => d.Key) // Group by key to handle potential duplicates
                .ToDictionary(g => g.Key, g => g.First().Value as object); // Take the first value for a given key

            // Special handling for the "Pattern Support" group to make it a simple list of
            // supported patterns.
            if (group.Name.Equals("Pattern Support", StringComparison.OrdinalIgnoreCase))
            {
                var supportedPatterns = group.Details
                    .Where(d => d.Value.Equals("Yes", StringComparison.OrdinalIgnoreCase))
                    .Select(d => d.Key.Replace("Pattern", "")) // Clean up the name
                    .ToList();
                comprehensiveReport[group.Name] = supportedPatterns;
            }
            else
            {
                comprehensiveReport[group.Name] = groupDetails;
            }
        }

        return comprehensiveReport;
    }



































}