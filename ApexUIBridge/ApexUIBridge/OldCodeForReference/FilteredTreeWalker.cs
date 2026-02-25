using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ApexUIBridge.Core;

/// <summary>
/// A custom ITreeWalker that wraps another walker and filters out any elements
/// if their ProcessName, Name, or LegacyIAccessible.Name matches an exclusion keyword.
/// </summary>
public class FilteredTreeWalker : ITreeWalker
{
    public bool showDefaultDebugOutput { get; set; } = false;

    private readonly ITreeWalker _baseWalker;
    private readonly TreeWalkerFilterLists _filterLists;

    public void ShowDebugOutput(bool show)
    {
        showDefaultDebugOutput = show;
    }

    /// <summary>
    /// Creates an instance of the filtering tree walker.
    /// </summary>
    /// <param name="baseWalker">The base walker to use for traversal (e.g., ControlViewWalker).</param>
    /// <param name="filterLists">The filter configuration containing keywords and control types to include/exclude.</param>
    /// <param name="enableDebugMessages">Enable verbose debug logging (default: false)</param>
    public FilteredTreeWalker(
        ITreeWalker baseWalker,
        TreeWalkerFilterLists filterLists,
        bool enableDebugMessages = false)
    {
        _baseWalker = baseWalker;
        _filterLists = filterLists ?? throw new ArgumentNullException(nameof(filterLists));
        showDefaultDebugOutput = enableDebugMessages;
    }

    /// <summary>
    /// Checks if a given element should be included in the tree by checking multiple properties against the exclusion list.
    /// </summary>
    private bool IsElementAllowed(AutomationElement element)
    {
        if (element == null) return false;

        var elementName = element?.Name ?? "";
        var controlType = element?.Properties.ControlType;

        var legacyName = "";
        var processName = string.Empty;

 
        try
        {
            // --- CHECK: Process Name Inclusion ---
            try
            {
                var processId = element.Properties.ProcessId.ValueOrDefault;
                if (processId > 0)
                {
                    processName = Process.GetProcessById(processId).ProcessName;
                }
            }
            catch
            {
                // Some browser provider elements (e.g., Chrome Document/RootWebArea) can have
                // inaccessible process metadata. Do not exclude them solely for that reason.
                processName = string.Empty;
            }

            if (element.Patterns.LegacyIAccessible.IsSupported)
            {
                legacyName = element.Patterns.LegacyIAccessible.Pattern.Name.ValueOrDefault ?? string.Empty;
            }

            var isAllowed = !_filterLists.InclusionKeywords.Any(included =>
                processName.Contains(included, StringComparison.OrdinalIgnoreCase) ||
                legacyName.Contains(included, StringComparison.OrdinalIgnoreCase) ||
                elementName.Contains(included, StringComparison.OrdinalIgnoreCase));

            if (isAllowed)
            {
                if (_filterLists.InclusionKeywords.Count > 0)
                {
                    if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"**[FTW] EXCLUDING: Name '{processName}' is NOT in the inclusion list.");

                    if (controlType == ControlType.Window ||
                        controlType == ControlType.Window)
                        return false;
                }
            }
            else
            {
                return true;
            }
             
            if (controlType == ControlType.Window || controlType == ControlType.Pane)
            {


                if (_filterLists.ExclusionKeywords.Any(excluded =>
                    elementName.Contains(excluded, StringComparison.OrdinalIgnoreCase)
                    || legacyName.Contains(excluded, StringComparison.OrdinalIgnoreCase)
                    || processName.Contains(excluded, StringComparison.OrdinalIgnoreCase)))
                {
                    if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"[FTW] EXCLUDING: Name '{elementName}' is in the exclusion list.");

                    return false;
                }

                if (_filterLists.ControlsToInclude.Count > 0)
                {
                    if (!_filterLists.ControlsToInclude.Contains(controlType))
                    {
                        if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"[FTW] EXCLUDING: ControlType '{controlType}' is NOT in the inclusion list.");
                        return false;
                    }
                }

                if (_filterLists.ControlsToExclude.Count > 0)
                {
                    if (_filterLists.ControlsToExclude.Contains(controlType))
                    {
                        if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"[FTW] EXCLUDING: ControlType '{controlType}' is in the exclusion list.");
                        return false;
                    }
                }
            }

            //

            //// --- CHECK: LegacyIAccessible Name (Fallback for older apps) ---
            //if (element.Patterns.LegacyIAccessible.IsSupported)
            //{
            //   isExcluded = _filterLists.ExclusionKeywords.Any(excluded => legacyName.Contains(excluded, StringComparison.OrdinalIgnoreCase));

            //    if (!string.IsNullOrEmpty(legacyName) && isExcluded)
            //    {
            //        if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"[FTW] EXCLUDING branch. Reason: LegacyIAccessible.Name '{legacyName}' is in the exclusion list.");
            //        return false;
            //    }
            //}

            // --- CHECK 0.5: Control Type Exclusion ---

            // --- CHECK=: Control Type Inclusion (if specified) ---
            //if (_filterLists.ControlsToInclude.Count > 0)
            //{
            //  //  var controlType = element.Properties.ControlType.ValueOrDefault;
            //    if (!_filterLists.ControlsToInclude.Contains(controlType))
            //    {
            //        if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"[FTW] EXCLUDING element. Reason: ControlType '{controlType}' is NOT in the inclusion list.");
            //        return false;
            //    }
            //}

            //System.Diagnostics.Debug.WriteLine($"**[FTW] DEFAULT");

            // If none of the checks resulted in an exclusion, the element is allowed.
            return true;
        }
        catch
        {
            // Browser web content nodes can throw intermittently when reading provider properties.
            // Keep Document nodes so actions can target the web DOM container instead of the top window.
            return element.Properties.ControlType.ValueOrDefault == ControlType.Document;
        }
    }

    // --- The ITreeWalker implementation ---

    public AutomationElement GetFirstChild(AutomationElement element)
    {
        var child = _baseWalker.GetFirstChild(element);
        while (child != null && !IsElementAllowed(child))
        {
            child = _baseWalker.GetNextSibling(child);
        }
        return child;
    }

    public AutomationElement GetLastChild(AutomationElement element)
    {
        var child = _baseWalker.GetLastChild(element);
        while (child != null && !IsElementAllowed(child))
        {
            child = _baseWalker.GetPreviousSibling(child);
        }
        return child;
    }

    public AutomationElement GetNextSibling(AutomationElement element)
    {
        var sibling = _baseWalker.GetNextSibling(element);
        while (sibling != null && !IsElementAllowed(sibling))
        {
            sibling = _baseWalker.GetNextSibling(sibling);
        }
        return sibling;
    }

    public AutomationElement GetPreviousSibling(AutomationElement element)
    {
        var sibling = _baseWalker.GetPreviousSibling(element);
        while (sibling != null && !IsElementAllowed(sibling))
        {
            sibling = _baseWalker.GetPreviousSibling(sibling);
        }
        return sibling;
    }

    public AutomationElement GetParent(AutomationElement element)
    {
        return _baseWalker.GetParent(element);
    }

    /// <summary>
    /// Finds the first Document element starting from the given element
    /// </summary>
    public AutomationElement FindFirstDocument(AutomationElement startElement)
    {
        if (startElement == null) return null;

        try
        {
            // Check if the start element itself is a Document
            if (startElement.Properties.ControlType.ValueOrDefault == ControlType.Document)
            {
                if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"[FTW] Start element IS Document: {startElement.Name}");
                return startElement;
            }

            // Use the base walker to traverse and find Document
            return FindDocumentRecursive(startElement);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FTW] Error finding Document: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Recursively searches for Document control type
    /// </summary>
    private AutomationElement FindDocumentRecursive(AutomationElement element)
    {
        if (element == null) return null;

        try
        {
            var child = _baseWalker.GetFirstChild(element);
            while (child != null)
            {
                // Check if this child is a Document
                if (child.Properties.ControlType.ValueOrDefault == ControlType.Document)
                {
                    if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"[FTW] Found Document: {child.Name}");
                    return child;
                }

                // Recursively check this child's descendants
                var found = FindDocumentRecursive(child);
                if (found != null)
                {
                    return found;
                }

                child = _baseWalker.GetNextSibling(child);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FTW] Error in recursive search: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gets the first child, optionally starting from Document element
    /// </summary>
    public AutomationElement GetFirstChild(AutomationElement element, bool startFromDocument)
    {
        if (!startFromDocument)
        {
            return GetFirstChild(element);
        }

        var documentElement = FindFirstDocument(element);
        if (documentElement != null)
        {
            if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"[FTW] Using Document as start point: {documentElement.Name}");
            return GetFirstChild(documentElement);
        }

        if (showDefaultDebugOutput) System.Diagnostics.Debug.WriteLine($"[FTW] No Document found, using original element");
        return GetFirstChild(element);
    }
}
