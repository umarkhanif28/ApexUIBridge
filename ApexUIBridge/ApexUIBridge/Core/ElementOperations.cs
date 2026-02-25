using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;

using ApexUIBridge.Models;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ComboBox = FlaUI.Core.AutomationElements.ComboBox;

namespace ApexUIBridge.Core
{
    /// <summary>
    /// Provides all low-level UI Automation interactions used by both
    /// <see cref="Bridge"/> (command-driven) and <see cref="ViewModels.ProcessViewModel"/>
    /// (UI toolbar).
    ///
    /// <para>Every public method accepts a live
    /// <see cref="FlaUI.Core.AutomationElements.AutomationElement"/> and returns an
    /// <see cref="OperationResult"/> that carries a success flag, optional error
    /// message, and the element's value where applicable.</para>
    ///
    /// <para><b>Click strategy</b> — <see cref="Click"/> tries UIA patterns in
    /// priority order (InvokePattern → TogglePattern → SelectionItemPattern) before
    /// falling back to a simulated mouse click, ensuring the broadest compatibility
    /// across WinForms, WPF, and Win32 controls.</para>
    ///
    /// <para><b>Value setting</b> — <see cref="SetValue"/> attempts ValuePattern,
    /// then falls back to focus + keyboard simulation for controls that expose no
    /// ValuePattern (e.g. plain Win32 edit boxes).</para>
    ///
    /// <para>Configurable timing delays (<see cref="FocusDelayMs"/>,
    /// <see cref="DragStepDelayMs"/>, <see cref="KeyboardDelayMs"/>,
    /// <see cref="ClickDelayMs"/>) allow callers to tune reliability vs. speed.</para>
    /// </summary>
    public sealed class ElementOperations
    {
 

        public ElementOperations()
        {
 
        }

        #region Timing Configuration

        /// <summary>
        /// Delay after focusing an element before performing keyboard operations.
        /// </summary>
        public int FocusDelayMs { get; set; } = 50;

        /// <summary>
        /// Delay between drag operation steps.
        /// </summary>
        public int DragStepDelayMs { get; set; } = 50;

        /// <summary>
        /// Delay between keyboard operations.
        /// </summary>
        public int KeyboardDelayMs { get; set; } = 50;

        /// <summary>
        /// Delay after click operations.
        /// </summary>
        public int ClickDelayMs { get; set; } = 0;

        #endregion Timing Configuration

        #region Click Operations

        /// <summary>
        /// Clicks an element using the most appropriate method.
        /// Tries: Invoke → Toggle → SelectionItem → Mouse Click
        /// </summary>
        public OperationResult Click(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                // Try invoke pattern first (most reliable for buttons)
                if (element.Patterns.Invoke.TryGetPattern(out var invokePattern))
                {
                    invokePattern.Invoke();
                    return OperationResult.Success(id, element);
                }

                // Try toggle pattern (for checkboxes, toggle buttons)
                if (element.Patterns.Toggle.TryGetPattern(out var togglePattern))
                {
                    togglePattern.Toggle();
                    return OperationResult.Success(id, element);
                }

                // Try selection item pattern (for list items, tree items)
                if (element.Patterns.SelectionItem.TryGetPattern(out var selectionPattern))
                {
                    selectionPattern.Select();
                    return OperationResult.Success(id, element);
                }

                // Fall back to mouse click
                element.Click();
                if (ClickDelayMs > 0) Thread.Sleep(ClickDelayMs);
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Double-clicks an element.
        /// </summary>
        public OperationResult DoubleClick(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                element.DoubleClick();
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Right-clicks an element.
        /// </summary>
        public OperationResult RightClick(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                element.RightClick();
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Middle-clicks an element.
        /// </summary>
        public OperationResult MiddleClick(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                var point = element.GetClickablePoint();
                Mouse.Click(point, MouseButton.Middle);
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Clicks at a specific offset from the element's top-left corner.
        /// </summary>
        public OperationResult ClickAtOffset(AutomationElement element, int id, int offsetX, int offsetY)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                var bounds = element.BoundingRectangle;
                var point = new Point(bounds.Left + offsetX, bounds.Top + offsetY);
                Mouse.Click(point);
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion Click Operations

        #region Invoke Pattern Operations

        /// <summary>
        /// Invokes an element using the Invoke pattern.
        /// </summary>
        public OperationResult Invoke(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Invoke.TryGetPattern(out var pattern))
                {
                    pattern.Invoke();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Invoke pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion Invoke Pattern Operations

        #region Toggle Pattern Operations

        /// <summary>
        /// Toggles an element (checkbox, toggle button).
        /// </summary>
        public OperationResult Toggle(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Toggle.TryGetPattern(out var pattern))
                {
                    pattern.Toggle();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Toggle pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the toggle state of an element.
        /// </summary>
        public OperationResult<ToggleState> GetToggleState(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<ToggleState>.NotFound(id);
            try
            {
                if (element.Patterns.Toggle.TryGetPattern(out var pattern))
                {
                    return OperationResult<ToggleState>.Success(id, pattern.ToggleState);
                }

                return OperationResult<ToggleState>.Failed(id, "Element does not support Toggle pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<ToggleState>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Sets the toggle state to a specific value (On/Off).
        /// </summary>
        public OperationResult SetToggleState(AutomationElement? element, int id, bool isOn)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Toggle.TryGetPattern(out var pattern))
                {
                    var targetState = isOn ? ToggleState.On : ToggleState.Off;

                    // Toggle until we reach the desired state (handles 3-state checkboxes)
                    int maxAttempts = 3;
                    while (pattern.ToggleState != targetState && maxAttempts-- > 0)
                    {
                        pattern.Toggle();
                    }

                    if (pattern.ToggleState == targetState)
                        return OperationResult.Success(id, element);

                    return OperationResult.Failed(id, $"Could not set toggle state to {targetState}");
                }

                return OperationResult.Failed(id, "Element does not support Toggle pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion Toggle Pattern Operations

        #region Value Pattern Operations

        /// <summary>
        /// Sets the value of an element using Value pattern, with fallbacks.
        /// </summary>
        /// <summary>
        /// Sets the value of an element using Value pattern, with fallbacks.
        /// </summary>
        public OperationResult SetValue(AutomationElement? element,int id, string? value)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            if (value == null)
                return OperationResult.Failed(id, "Value cannot be null");

            try
            {
                // For Spinner (NumericUpDown), find the Edit child and set its text
                // This is how FlaUI's Spinner class handles WinForms NumericUpDown
                if (element.ControlType == ControlType.Spinner)
                {
                    var editChild = element.FindFirstChild(cf => cf.ByControlType(ControlType.Edit));
                    if (editChild != null)
                    {
                        if (editChild.Patterns.Value.TryGetPattern(out var editValuePattern))
                        {
                            editValuePattern.SetValue(value);
                            return OperationResult.Success(id, element);
                        }
                    }
                }

                // For numeric values, try RangeValue pattern
                if (double.TryParse(value, out double numericValue) &&
                    element.Patterns.RangeValue.TryGetPattern(out var rangePattern))
                {
                    if (!rangePattern.IsReadOnly)
                    {
                        // Clamp to valid range
                        numericValue = Math.Max(rangePattern.Minimum, Math.Min(rangePattern.Maximum, numericValue));
                        rangePattern.SetValue(numericValue);
                        return OperationResult.Success(id, element);
                    }
                }

                // Try Value pattern
                if (element.Patterns.Value.TryGetPattern(out var valuePattern))
                {
                    if (valuePattern.IsReadOnly)
                        return OperationResult.Failed(id, "Element is read-only");

                    valuePattern.SetValue(value);
                    return OperationResult.Success(id, element);
                }

                // Try LegacyIAccessible pattern
                if (element.Patterns.LegacyIAccessible.TryGetPattern(out var legacyPattern))
                {
                    legacyPattern.SetValue(value);
                    return OperationResult.Success(id, element);
                }

                // Fall back to keyboard input - clear existing text first
                element.Focus();
                Thread.Sleep(FocusDelayMs);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Thread.Sleep(KeyboardDelayMs);
                Keyboard.Type(value);
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the value of an element.
        /// </summary>
        public OperationResult<string> GetValue(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<string>.NotFound(id);

            try
            {
                // Try Value pattern first
                if (element.Patterns.Value.TryGetPattern(out var valuePattern))
                {
                    return OperationResult<string>.Success(id, valuePattern.Value);
                }

                // Try Text pattern
                if (element.Patterns.Text.TryGetPattern(out var textPattern))
                {
                    var text = textPattern.DocumentRange.GetText(-1);
                    return OperationResult<string>.Success(id, text);
                }

                // Try LegacyIAccessible pattern
                if (element.Patterns.LegacyIAccessible.TryGetPattern(out var legacyPattern))
                {
                    return OperationResult<string>.Success(id, legacyPattern.Value ?? "");
                }

                // Fall back to Name property
                var name = element.Properties.Name.ValueOrDefault;
                return OperationResult<string>.Success(id, name ?? "");
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Clears the value of an element.
        /// </summary>
        public OperationResult ClearValue(AutomationElement element,int id)
        {
            return SetValue(element,id, string.Empty);
        }

        /// <summary>
        /// Appends text to an element's current value.
        /// </summary>
        public OperationResult AppendValue(AutomationElement? element, int id, string? value)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                element.Focus();
                Thread.Sleep(FocusDelayMs);
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.END);
                Thread.Sleep(KeyboardDelayMs);
                Keyboard.Type(value);
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion Value Pattern Operations

        #region RangeValue Pattern Operations

        /// <summary>
        /// Sets the range value (slider, progress bar).
        /// </summary>
        public OperationResult SetRangeValue(AutomationElement element, int id, double value)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.RangeValue.TryGetPattern(out var pattern))
                {
                    if (pattern.IsReadOnly)
                        return OperationResult.Failed(id, "Element is read-only");

                    if (value < pattern.Minimum || value > pattern.Maximum)
                        return OperationResult.Failed(id,
                            $"Value {value} is out of range [{pattern.Minimum}, {pattern.Maximum}]");

                    pattern.SetValue(value);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support RangeValue pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the range value of an element.
        /// </summary>
        public OperationResult<double> GetRangeValue(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<double>.NotFound(id);

            try
            {
                if (element.Patterns.RangeValue.TryGetPattern(out var pattern))
                {
                    return OperationResult<double>.Success(id, pattern.Value);
                }

                return OperationResult<double>.Failed(id, "Element does not support RangeValue pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<double>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the range value bounds (minimum, maximum, current).
        /// </summary>
        public OperationResult<RangeValueInfo> GetRangeValueInfo(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<RangeValueInfo>.NotFound(id);
            try
            {
                if (element.Patterns.RangeValue.TryGetPattern(out var pattern))
                {
                    var info = new RangeValueInfo
                    {
                        Value = pattern.Value,
                        Minimum = pattern.Minimum,
                        Maximum = pattern.Maximum,
                        SmallChange = pattern.SmallChange,
                        LargeChange = pattern.LargeChange,
                        IsReadOnly = pattern.IsReadOnly
                    };
                    return OperationResult<RangeValueInfo>.Success(id, info);
                }

                return OperationResult<RangeValueInfo>.Failed(id, "Element does not support RangeValue pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<RangeValueInfo>.Failed(id, ex.Message);
            }
        }

        #endregion RangeValue Pattern Operations

        #region ExpandCollapse Pattern Operations

        /// <summary>
        /// Expands an element (tree node, combo box, menu).
        /// </summary>
        public OperationResult Expand(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.ExpandCollapse.TryGetPattern(out var pattern))
                {
                    if (pattern.ExpandCollapseState == ExpandCollapseState.LeafNode)
                        return OperationResult.Failed(id, "Element is a leaf node and cannot be expanded");

                    pattern.Expand();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support ExpandCollapse pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Collapses an element.
        /// </summary>
        public OperationResult Collapse(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.ExpandCollapse.TryGetPattern(out var pattern))
                {
                    if (pattern.ExpandCollapseState == ExpandCollapseState.LeafNode)
                        return OperationResult.Failed(id, "Element is a leaf node and cannot be collapsed");

                    pattern.Collapse();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support ExpandCollapse pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the expand/collapse state of an element.
        /// </summary>
        public OperationResult<ExpandCollapseState> GetExpandCollapseState(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<ExpandCollapseState>.NotFound(id);
            try
            {
                if (element.Patterns.ExpandCollapse.TryGetPattern(out var pattern))
                {
                    return OperationResult<ExpandCollapseState>.Success(id, pattern.ExpandCollapseState);
                }

                return OperationResult<ExpandCollapseState>.Failed(id, "Element does not support ExpandCollapse pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<ExpandCollapseState>.Failed(id, ex.Message);
            }
        }

        #endregion ExpandCollapse Pattern Operations

        #region SelectionItem Pattern Operations

        /// <summary>
        /// Selects an element (list item, tree item, tab).
        /// </summary>
        public OperationResult Select(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.SelectionItem.TryGetPattern(out var pattern))
                {
                    pattern.Select();
                    return OperationResult.Success(id, element);
                }

                // Fall back to click
                element.Click();
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Adds an element to the selection (multi-select).
        /// </summary>
        public OperationResult AddToSelection(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);
            try
            {
                if (element.Patterns.SelectionItem.TryGetPattern(out var pattern))
                {
                    pattern.AddToSelection();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support SelectionItem pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Removes an element from the selection.
        /// </summary>
        public OperationResult RemoveFromSelection(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.SelectionItem.TryGetPattern(out var pattern))
                {
                    pattern.RemoveFromSelection();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support SelectionItem pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Checks if an element is selected.
        /// </summary>
        public OperationResult<bool> IsSelected(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<bool>.NotFound(id);
            try
            {
                if (element.Patterns.SelectionItem.TryGetPattern(out var pattern))
                {
                    return OperationResult<bool>.Success(id, pattern.IsSelected);
                }

                return OperationResult<bool>.Failed(id, "Element does not support SelectionItem pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(id, ex.Message);
            }
        }

        #endregion SelectionItem Pattern Operations

        #region Selection Pattern Operations (Container)

        /// <summary>
        /// Gets the currently selected elements from a selection container (ListBox, TreeView, TabControl).
        /// </summary>
        public OperationResult<AutomationElement[]> GetSelection(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<AutomationElement[]>.NotFound(id);

            try
            {
                if (element.Patterns.Selection.TryGetPattern(out var pattern))
                {
                    var selected = pattern.Selection;
                    return OperationResult<AutomationElement[]>.Success(id, selected);
                }

                return OperationResult<AutomationElement[]>.Failed(id, "Element does not support Selection pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<AutomationElement[]>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Checks if a selection container supports multiple selection.
        /// </summary>
        public OperationResult<bool> CanSelectMultiple(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<bool>.NotFound(id);

            try
            {
                if (element.Patterns.Selection.TryGetPattern(out var pattern))
                {
                    return OperationResult<bool>.Success(id, pattern.CanSelectMultiple);
                }

                return OperationResult<bool>.Failed(id, "Element does not support Selection pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Checks if a selection container requires at least one selected item.
        /// </summary>
        public OperationResult<bool> IsSelectionRequired(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<bool>.NotFound(id);

            try
            {
                if (element.Patterns.Selection.TryGetPattern(out var pattern))
                {
                    return OperationResult<bool>.Success(id, pattern.IsSelectionRequired);
                }

                return OperationResult<bool>.Failed(id, "Element does not support Selection pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(id, ex.Message);
            }
        }

        #endregion Selection Pattern Operations (Container)

        #region Scroll Pattern Operations

        /// <summary>
        /// Scrolls an element vertically.
        /// </summary>
        public OperationResult Scroll(AutomationElement element, int id, int amount)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Scroll.TryGetPattern(out var pattern))
                {
                    if (!pattern.VerticallyScrollable)
                        return OperationResult.Failed(id, "Element is not vertically scrollable");

                    var direction = amount > 0 ? ScrollAmount.LargeIncrement : ScrollAmount.LargeDecrement;
                    var times = Math.Abs(amount);

                    for (int i = 0; i < times; i++)
                    {
                        pattern.Scroll(ScrollAmount.NoAmount, direction);
                    }

                    return OperationResult.Success(id, element);
                }

                // Fall back to mouse wheel
                element.Focus();
                Thread.Sleep(FocusDelayMs);
                Mouse.Scroll(amount);
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Scrolls an element horizontally.
        /// </summary>
        public OperationResult ScrollHorizontal(AutomationElement element, int id, int amount)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Scroll.TryGetPattern(out var pattern))
                {
                    if (!pattern.HorizontallyScrollable)
                        return OperationResult.Failed(id, "Element is not horizontally scrollable");

                    var direction = amount > 0 ? ScrollAmount.LargeIncrement : ScrollAmount.LargeDecrement;
                    var times = Math.Abs(amount);

                    for (int i = 0; i < times; i++)
                    {
                        pattern.Scroll(direction, ScrollAmount.NoAmount);
                    }

                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support horizontal scrolling");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Scrolls to a specific percentage position.
        /// </summary>
        public OperationResult ScrollToPercent(AutomationElement element, int id, double horizontalPercent, double verticalPercent)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Scroll.TryGetPattern(out var pattern))
                {
                    pattern.SetScrollPercent(horizontalPercent, verticalPercent);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Scroll pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the current scroll position as percentages.
        /// </summary>
        public OperationResult<ScrollInfo> GetScrollInfo(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<ScrollInfo>.NotFound(id);

            try
            {
                if (element.Patterns.Scroll.TryGetPattern(out var pattern))
                {
                    var info = new ScrollInfo
                    {
                        HorizontalScrollPercent = pattern.HorizontalScrollPercent,
                        VerticalScrollPercent = pattern.VerticalScrollPercent,
                        HorizontalViewSize = pattern.HorizontalViewSize,
                        VerticalViewSize = pattern.VerticalViewSize,
                        HorizontallyScrollable = pattern.HorizontallyScrollable,
                        VerticallyScrollable = pattern.VerticallyScrollable
                    };
                    return OperationResult<ScrollInfo>.Success(id, info);
                }

                return OperationResult<ScrollInfo>.Failed(id, "Element does not support Scroll pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<ScrollInfo>.Failed(id, ex.Message);
            }
        }

        #endregion Scroll Pattern Operations

        #region ScrollItem Pattern Operations

        /// <summary>
        /// Scrolls an element into view.
        /// </summary>
        public OperationResult ScrollIntoView(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.ScrollItem.TryGetPattern(out var pattern))
                {
                    pattern.ScrollIntoView();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support ScrollItem pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion ScrollItem Pattern Operations

        #region Window Pattern Operations

        /// <summary>
        /// Closes a window.
        /// </summary>
        public OperationResult CloseWindow(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Window.TryGetPattern(out var pattern))
                {
                    pattern.Close();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Window pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Minimizes a window.
        /// </summary>
        public OperationResult MinimizeWindow(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Window.TryGetPattern(out var pattern))
                {
                    pattern.SetWindowVisualState(WindowVisualState.Minimized);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Window pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Maximizes a window.
        /// </summary>
        public OperationResult MaximizeWindow(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Window.TryGetPattern(out var pattern))
                {
                    pattern.SetWindowVisualState(WindowVisualState.Maximized);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Window pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Restores a window to normal state.
        /// </summary>
        public OperationResult RestoreWindow(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Window.TryGetPattern(out var pattern))
                {
                    pattern.SetWindowVisualState(WindowVisualState.Normal);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Window pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the window visual state.
        /// </summary>
        public OperationResult<WindowVisualState> GetWindowVisualState(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<WindowVisualState>.NotFound(id);
            try
            {
                if (element.Patterns.Window.TryGetPattern(out var pattern))
                {
                    return OperationResult<WindowVisualState>.Success(id, pattern.WindowVisualState);
                }

                return OperationResult<WindowVisualState>.Failed(id, "Element does not support Window pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<WindowVisualState>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Waits for the window to be ready for user input.
        /// </summary>
        public OperationResult WaitForWindowInputIdle(AutomationElement element, int id, int timeoutMs = 10000)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Window.TryGetPattern(out var pattern))
                {
                    var success = pattern.WaitForInputIdle(timeoutMs);
                    if (success)
                        return OperationResult.Success(id, element);
                    return OperationResult.Failed(id, "Window did not become ready within timeout");
                }

                return OperationResult.Failed(id, "Element does not support Window pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion Window Pattern Operations

        #region Transform Pattern Operations

        /// <summary>
        /// Moves an element to a new position.
        /// </summary>
        public OperationResult Move(AutomationElement element, int id, double x, double y)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Transform.TryGetPattern(out var pattern))
                {
                    if (!pattern.CanMove)
                        return OperationResult.Failed(id, "Element cannot be moved");

                    pattern.Move(x, y);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Transform pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Resizes an element.
        /// </summary>
        public OperationResult Resize(AutomationElement element, int id, double width, double height)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Transform.TryGetPattern(out var pattern))
                {
                    if (!pattern.CanResize)
                        return OperationResult.Failed(id, "Element cannot be resized");

                    pattern.Resize(width, height);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Transform pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Rotates an element (Transform2 pattern).
        /// </summary>
        public OperationResult Rotate(AutomationElement element, int id, double degrees)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Transform2.TryGetPattern(out var pattern))
                {
                    if (!pattern.CanRotate)
                        return OperationResult.Failed(id, "Element cannot be rotated");

                    pattern.Rotate(degrees);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Transform2 pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Zooms an element to a specific zoom level (Transform2 pattern).
        /// </summary>
        public OperationResult Zoom(AutomationElement element, int id, double zoomLevel)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Transform2.TryGetPattern(out var pattern))
                {
                    if (!pattern.CanZoom)
                        return OperationResult.Failed(id, "Element cannot be zoomed");

                    pattern.Zoom(zoomLevel);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Transform2 pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Zooms an element by a unit increment (Transform2 pattern).
        /// </summary>
        public OperationResult ZoomByUnit(AutomationElement element, int id, ZoomUnit zoomUnit)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Transform2.TryGetPattern(out var pattern))
                {
                    if (!pattern.CanZoom)
                        return OperationResult.Failed(id, "Element cannot be zoomed");

                    pattern.ZoomByUnit(zoomUnit);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Transform2 pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the current zoom level of an element (Transform2 pattern).
        /// </summary>
        public OperationResult<double> GetZoomLevel(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<double>.NotFound(id);

            try
            {
                if (element.Patterns.Transform2.TryGetPattern(out var pattern))
                {
                    return OperationResult<double>.Success(id, pattern.ZoomLevel);
                }

                return OperationResult<double>.Failed(id, "Element does not support Transform2 pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<double>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Checks if an element supports zooming (Transform2 pattern).
        /// </summary>
        public OperationResult<bool> CanZoom(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<bool>.NotFound(id);

            try
            {
                if (element.Patterns.Transform2.TryGetPattern(out var pattern))
                {
                    return OperationResult<bool>.Success(id, pattern.CanZoom);
                }

                return OperationResult<bool>.Failed(id, "Element does not support Transform2 pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(id, ex.Message);
            }
        }

        #endregion Transform Pattern Operations

        #region Grid Pattern Operations

        /// <summary>
        /// Gets an item from a grid at the specified row and column.
        /// </summary>
        public OperationResult<AutomationElement> GetGridItem(AutomationElement element, int id, int row, int column)
        {
            
            if (element == null)
                return OperationResult<AutomationElement>.NotFound(id);
            try
            {
                if (element.Patterns.Grid.TryGetPattern(out var pattern))
                {
                    if (row < 0 || row >= pattern.RowCount)
                        return OperationResult<AutomationElement>.Failed(id, $"Row {row} is out of range (0-{pattern.RowCount - 1})");
                    if (column < 0 || column >= pattern.ColumnCount)
                        return OperationResult<AutomationElement>.Failed(id, $"Column {column} is out of range (0-{pattern.ColumnCount - 1})");

                    var item = pattern.GetItem(row, column);
                    return OperationResult<AutomationElement>.Success(id, item);
                }

                return OperationResult<AutomationElement>.Failed(id, "Element does not support Grid pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<AutomationElement>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets grid dimensions.
        /// </summary>
        public OperationResult<GridInfo> GetGridInfo(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<GridInfo>.NotFound(id);
            try
            {
                if (element.Patterns.Grid.TryGetPattern(out var pattern))
                {
                    var info = new GridInfo
                    {
                        RowCount = pattern.RowCount,
                        ColumnCount = pattern.ColumnCount
                    };
                    return OperationResult<GridInfo>.Success(id, info);
                }

                return OperationResult<GridInfo>.Failed(id, "Element does not support Grid pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<GridInfo>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the row and column span of a grid item.
        /// </summary>
        public OperationResult<GridItemInfo> GetGridItemInfo(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<GridItemInfo>.NotFound(id);
            try
            {
                if (element.Patterns.GridItem.TryGetPattern(out var pattern))
                {
                    var info = new GridItemInfo
                    {
                        Row = pattern.Row,
                        Column = pattern.Column,
                        RowSpan = pattern.RowSpan,
                        ColumnSpan = pattern.ColumnSpan
                    };
                    return OperationResult<GridItemInfo>.Success(id, info);
                }

                return OperationResult<GridItemInfo>.Failed(id, "Element does not support GridItem pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<GridItemInfo>.Failed(id, ex.Message);
            }
        }

        #endregion Grid Pattern Operations

        #region Table Pattern Operations

        /// <summary>
        /// Gets table header information.
        /// </summary>
        public OperationResult<TableInfo> GetTableInfo(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<TableInfo>.NotFound(id);
            try
            {
                if (element.Patterns.Table.TryGetPattern(out var tablePattern))
                {
                    var info = new TableInfo
                    {
                        RowOrColumnMajor = tablePattern.RowOrColumnMajor.ValueOrDefault,
                        ColumnHeaders = tablePattern.ColumnHeaders.ValueOrDefault ?? Array.Empty<AutomationElement>(),
                        RowHeaders = tablePattern.RowHeaders.ValueOrDefault ?? Array.Empty<AutomationElement>()
                    };

                    // Try to get row/column count from Grid pattern if available
                    if (element.Patterns.Grid.TryGetPattern(out var gridPattern))
                    {
                        info.RowCount = gridPattern.RowCount;
                        info.ColumnCount = gridPattern.ColumnCount;
                    }

                    return OperationResult<TableInfo>.Success(id, info);
                }

                return OperationResult<TableInfo>.Failed(id, "Element does not support Table pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<TableInfo>.Failed(id, ex.Message);
            }
        }

        #endregion Table Pattern Operations

        #region TableItem Pattern Operations

        /// <summary>
        /// Gets the row and column header information for a specific table cell.
        /// </summary>
        public OperationResult<TableItemInfo> GetTableItemInfo(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<TableItemInfo>.NotFound(id);

            try
            {
                if (element.Patterns.TableItem.TryGetPattern(out var pattern))
                {
                    var info = new TableItemInfo
                    {
                        RowHeaderItems = pattern.RowHeaderItems.ValueOrDefault ?? Array.Empty<AutomationElement>(),
                        ColumnHeaderItems = pattern.ColumnHeaderItems.ValueOrDefault ?? Array.Empty<AutomationElement>()
                    };

                    // Also get grid item info if available
                    if (element.Patterns.GridItem.TryGetPattern(out var gridItemPattern))
                    {
                        info.Row = gridItemPattern.Row;
                        info.Column = gridItemPattern.Column;
                        info.RowSpan = gridItemPattern.RowSpan;
                        info.ColumnSpan = gridItemPattern.ColumnSpan;
                    }

                    return OperationResult<TableItemInfo>.Success(id, info);
                }

                return OperationResult<TableItemInfo>.Failed(id, "Element does not support TableItem pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<TableItemInfo>.Failed(id, ex.Message);
            }
        }

        #endregion TableItem Pattern Operations

        #region Dock Pattern Operations

        /// <summary>
        /// Sets the dock position of an element.
        /// </summary>
        public OperationResult SetDockPosition(AutomationElement element, int id, DockPosition position)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.Dock.TryGetPattern(out var pattern))
                {
                    pattern.SetDockPosition(position);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support Dock pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the dock position of an element.
        /// </summary>
        public OperationResult<DockPosition> GetDockPosition(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<DockPosition>.NotFound(id);
            try
            {
                if (element.Patterns.Dock.TryGetPattern(out var pattern))
                {
                    return OperationResult<DockPosition>.Success(id, pattern.DockPosition);
                }

                return OperationResult<DockPosition>.Failed(id, "Element does not support Dock pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<DockPosition>.Failed(id, ex.Message);
            }
        }

        #endregion Dock Pattern Operations

        #region MultipleView Pattern Operations

        /// <summary>
        /// Sets the current view of an element.
        /// </summary>
        public OperationResult SetCurrentView(AutomationElement element, int id, int viewId)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.MultipleView.TryGetPattern(out var pattern))
                {
                    pattern.SetCurrentView(viewId);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support MultipleView pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets available views.
        /// </summary>
        public OperationResult<int[]> GetSupportedViews(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<int[]>.NotFound(id);
            try
            {
                if (element.Patterns.MultipleView.TryGetPattern(out var pattern))
                {
                    return OperationResult<int[]>.Success(id, pattern.SupportedViews);
                }

                return OperationResult<int[]>.Failed(id, "Element does not support MultipleView pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<int[]>.Failed(id, ex.Message);
            }
        }

        #endregion MultipleView Pattern Operations

        #region VirtualizedItem Pattern Operations

        /// <summary>
        /// Realizes a virtualized item (loads it into memory).
        /// </summary>
        public OperationResult Realize(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.VirtualizedItem.TryGetPattern(out var pattern))
                {
                    pattern.Realize();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support VirtualizedItem pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion VirtualizedItem Pattern Operations

        #region Annotation Pattern Operations

        /// <summary>
        /// Gets annotation information from an element.
        /// </summary>
        public OperationResult<AnnotationInfo> GetAnnotationInfo(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<AnnotationInfo>.NotFound(id);

            try
            {
                if (element.Patterns.Annotation.TryGetPattern(out var pattern))
                {
                    var info = new AnnotationInfo
                    {
                        AnnotationTypeId = (int)pattern.AnnotationType.ValueOrDefault,
                        AnnotationTypeName = pattern.AnnotationTypeName.ValueOrDefault ?? "",
                        Author = pattern.Author.ValueOrDefault ?? "",
                        DateTime = pattern.DateTime.ValueOrDefault ?? "",
                        Target = pattern.Target.ValueOrDefault
                    };
                    return OperationResult<AnnotationInfo>.Success(id, info);
                }

                return OperationResult<AnnotationInfo>.Failed(id, "Element does not support Annotation pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<AnnotationInfo>.Failed(id, ex.Message);
            }
        }

        #endregion Annotation Pattern Operations

        #region SynchronizedInput Pattern Operations

        /// <summary>
        /// Starts listening for input on an element. Call Cancel to stop.
        /// </summary>
        public OperationResult StartListeningForInput(AutomationElement element, int id, SynchronizedInputType inputType)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.SynchronizedInput.TryGetPattern(out var pattern))
                {
                    pattern.StartListening(inputType);
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support SynchronizedInput pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Cancels listening for input on an element.
        /// </summary>
        public OperationResult CancelListeningForInput(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                if (element.Patterns.SynchronizedInput.TryGetPattern(out var pattern))
                {
                    pattern.Cancel();
                    return OperationResult.Success(id, element);
                }

                return OperationResult.Failed(id, "Element does not support SynchronizedInput pattern");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion SynchronizedInput Pattern Operations

        #region TextChild Pattern Operations

        /// <summary>
        /// Gets the enclosing text container element for a TextChild element.
        /// </summary>
        public OperationResult<AutomationElement> GetTextContainer(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<AutomationElement>.NotFound(id);

            try
            {
                if (element.Patterns.TextChild.TryGetPattern(out var pattern))
                {
                    var container = pattern.TextContainer;
                    return OperationResult<AutomationElement>.Success(id, container);
                }

                return OperationResult<AutomationElement>.Failed(id, "Element does not support TextChild pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<AutomationElement>.Failed(id, ex.Message);
            }
        }

        #endregion TextChild Pattern Operations

        #region Text Pattern Operations

        /// <summary>
        /// Gets the full text content of an element.
        /// </summary>
        public OperationResult<string> GetText(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<string>.NotFound(id);
            try
            {
                if (element.Patterns.Text.TryGetPattern(out var pattern))
                {
                    var text = pattern.DocumentRange.GetText(-1);
                    return OperationResult<string>.Success(id, text);
                }

                return OperationResult<string>.Failed(id, "Element does not support Text pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the selected text in an element.
        /// </summary>
        public OperationResult<string> GetSelectedText(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<string>.NotFound(id);
            try
            {
                if (element.Patterns.Text.TryGetPattern(out var pattern))
                {
                    var selections = pattern.GetSelection();
                    if (selections.Length > 0)
                    {
                        var selectedText = string.Join("", selections.Select(s => s.GetText(-1)));
                        return OperationResult<string>.Success(id, selectedText);
                    }
                    return OperationResult<string>.Success(id, "");
                }

                return OperationResult<string>.Failed(id, "Element does not support Text pattern");
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Failed(id, ex.Message);
            }
        }

        #endregion Text Pattern Operations

        #region Focus and Basic Operations

        /// <summary>
        /// Sets focus to an element.
        /// </summary>
        public OperationResult Focus(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                element.Focus();
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Checks if an element is enabled.
        /// </summary>
        public OperationResult<bool> IsEnabled(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<bool>.NotFound(id);

            try
            {
                var enabled = element.Properties.IsEnabled.ValueOrDefault;
                return OperationResult<bool>.Success(id, enabled);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Checks if an element is visible (not off-screen).
        /// </summary>
        public OperationResult<bool> IsVisible(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<bool>.NotFound(id);
            try
            {
                var offScreen = element.Properties.IsOffscreen.ValueOrDefault;
                return OperationResult<bool>.Success(id, !offScreen);
            }
            catch (Exception ex)
            {
                return OperationResult<bool>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the bounding rectangle of an element.
        /// </summary>
        public OperationResult<Rectangle> GetBoundingRectangle(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<Rectangle>.NotFound(id);
            try
            {
                var rect = element.BoundingRectangle;
                return OperationResult<Rectangle>.Success(id, rect);
            }
            catch (Exception ex)
            {
                return OperationResult<Rectangle>.Failed(id, ex.Message);
            }
        }

        #endregion Focus and Basic Operations

        #region Mouse Operations

        /// <summary>
        /// Hovers the mouse over an element.
        /// </summary>
        public OperationResult Hover(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                var point = element.GetClickablePoint();
                Mouse.MoveTo(point);
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Drags an element to a target element.
        /// </summary>
        public OperationResult DragTo(AutomationElement? sourceElement, int sourceId, AutomationElement? targetElement, int targetId)
        {
 
            if (sourceElement == null)
                return OperationResult.NotFound(sourceId);

 
            if (targetElement == null)
                return OperationResult.NotFound(targetId);

            try
            {
                var sourcePoint = sourceElement.GetClickablePoint();
                var targetPoint = targetElement.GetClickablePoint();

                Mouse.MoveTo(sourcePoint);
                Thread.Sleep(DragStepDelayMs);
                Mouse.Down(MouseButton.Left);
                Thread.Sleep(DragStepDelayMs);
                Mouse.MoveTo(targetPoint);
                Thread.Sleep(DragStepDelayMs);
                Mouse.Up(MouseButton.Left);

                return OperationResult.Success(sourceId);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(sourceId, ex.Message);
            }
        }

        /// <summary>
        /// Drags an element to a specific screen position.
        /// </summary>
        public OperationResult DragToPosition(AutomationElement element, int id, int targetX, int targetY)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                var sourcePoint = element.GetClickablePoint();
                var targetPoint = new Point(targetX, targetY);

                Mouse.MoveTo(sourcePoint);
                Thread.Sleep(DragStepDelayMs);
                Mouse.Down(MouseButton.Left);
                Thread.Sleep(DragStepDelayMs);
                Mouse.MoveTo(targetPoint);
                Thread.Sleep(DragStepDelayMs);
                Mouse.Up(MouseButton.Left);

                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion Mouse Operations

        #region Keyboard Operations

        /// <summary>
        /// Sends keyboard input to an element.
        /// </summary>
        public OperationResult SendKeys(AutomationElement element, int id, string? keys)
        {

            if (element == null)
                return OperationResult.NotFound(id);

            if (string.IsNullOrEmpty(keys))
                return OperationResult.Failed(id, "Keys cannot be null or empty");

            try
            {
                element.Focus();
                Thread.Sleep(FocusDelayMs);

                // Parse modifier+key combos (e.g. "Ctrl+A", "Alt+F4", "Shift+Tab")
                if (keys.Contains('+'))
                {
                    var keyParts = keys.Split('+', 2);
                    var modifierName = keyParts[0].Trim();
                    var keyName = keyParts[1].Trim();

                    var modifier = ParseVirtualKey(modifierName);
                    var key = ParseVirtualKey(keyName);

                    if (modifier == null)
                        return OperationResult.Failed(id, $"Unknown modifier key: '{modifierName}'");
                    if (key == null)
                        return OperationResult.Failed(id, $"Unknown key: '{keyName}'");

                    Keyboard.TypeSimultaneously(modifier.Value, key.Value);
                }
                else
                {
                    // Single key name (e.g. "Return", "Enter", "Tab", "Escape")
                    var vk = ParseVirtualKey(keys);
                    if (vk != null)
                    {
                        Keyboard.Press(vk.Value);
                        Keyboard.Release(vk.Value);
                    }
                    else
                    {
                        // Not a recognized key name — type as literal text
                        Keyboard.Type(keys);
                    }
                }

                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        private static VirtualKeyShort? ParseVirtualKey(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "enter":
                case "return":
                    return VirtualKeyShort.RETURN;
                case "tab":
                    return VirtualKeyShort.TAB;
                case "escape":
                case "esc":
                    return VirtualKeyShort.ESCAPE;
                case "backspace":
                case "back":
                    return VirtualKeyShort.BACK;
                case "delete":
                case "del":
                    return VirtualKeyShort.DELETE;
                case "space":
                    return VirtualKeyShort.SPACE;
                case "up":
                    return VirtualKeyShort.UP;
                case "down":
                    return VirtualKeyShort.DOWN;
                case "left":
                    return VirtualKeyShort.LEFT;
                case "right":
                    return VirtualKeyShort.RIGHT;
                case "home":
                    return VirtualKeyShort.HOME;
                case "end":
                    return VirtualKeyShort.END;
                case "pageup":
                    return VirtualKeyShort.PRIOR;
                case "pagedown":
                    return VirtualKeyShort.NEXT;
                case "insert":
                    return VirtualKeyShort.INSERT;
                case "ctrl":
                case "control":
                    return VirtualKeyShort.CONTROL;
                case "alt":
                    return VirtualKeyShort.ALT;
                case "shift":
                    return VirtualKeyShort.SHIFT;
                case "f1":
                    return VirtualKeyShort.F1;
                case "f2":
                    return VirtualKeyShort.F2;
                case "f3":
                    return VirtualKeyShort.F3;
                case "f4":
                    return VirtualKeyShort.F4;
                case "f5":
                    return VirtualKeyShort.F5;
                case "f6":
                    return VirtualKeyShort.F6;
                case "f7":
                    return VirtualKeyShort.F7;
                case "f8":
                    return VirtualKeyShort.F8;
                case "f9":
                    return VirtualKeyShort.F9;
                case "f10":
                    return VirtualKeyShort.F10;
                case "f11":
                    return VirtualKeyShort.F11;
                case "f12":
                    return VirtualKeyShort.F12;
                default:
                    // Single letter keys (a-z)
                    if (name.Length == 1 && char.IsLetter(name[0]))
                        return (VirtualKeyShort)char.ToUpper(name[0]);
                    return null;
            }
        }

        /// <summary>
        /// Sends a keyboard shortcut (e.g., Ctrl+C).
        /// </summary>
        public OperationResult SendKeyboardShortcut(AutomationElement element, int id, VirtualKeyShort modifier, VirtualKeyShort key)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                element.Focus();
                Thread.Sleep(FocusDelayMs);
                Keyboard.TypeSimultaneously(modifier, key);
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Presses a single key.
        /// </summary>
        public OperationResult PressKey(AutomationElement element, int id, VirtualKeyShort key)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                element.Focus();
                Thread.Sleep(FocusDelayMs);
                Keyboard.Press(key);
                Keyboard.Release(key);
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        #endregion Keyboard Operations

        #region ComboBox and ListBox Operations

        /// <summary>
        /// Strategy for selecting ComboBox items. Reorder these to change which method is tried first.
        /// </summary>
        public enum ComboBoxSelectionStrategy
        {
            /// <summary>Use standard ComboBox.Items with Click</summary>
            ItemsClick,
            /// <summary>Use standard ComboBox.Items with DoubleClick</summary>
            ItemsDoubleClick,
            /// <summary>Find List child, get ListItems, use Click</summary>
            ListChildClick,
            /// <summary>Find List child, get ListItems, use DoubleClick</summary>
            ListChildDoubleClick,
            /// <summary>Find List child, get ListItems, use SelectionItem pattern</summary>
            ListChildSelect,
            /// <summary>Find List child, get ListItems, use Invoke pattern</summary>
            ListChildInvoke,
            /// <summary>Use keyboard Down arrows + Enter</summary>
            KeyboardNavigation,
            /// <summary>Click combo to open, then Down arrows + Enter</summary>
            ClickThenKeyboard
        }

        /// <summary>
        /// Order of strategies to try for ComboBox selection. Rearrange to change priority.
        /// </summary>
        public ComboBoxSelectionStrategy[] ComboBoxStrategies { get; set; } = new[]
        {
        ComboBoxSelectionStrategy.ListChildSelect,    // Use SelectionItem pattern (most proper)
        ComboBoxSelectionStrategy.ListChildInvoke,    // Use Invoke pattern
        ComboBoxSelectionStrategy.ClickThenKeyboard,  // Click + keyboard navigation
        ComboBoxSelectionStrategy.KeyboardNavigation,
        ComboBoxSelectionStrategy.ListChildDoubleClick,
        ComboBoxSelectionStrategy.ListChildClick,
        ComboBoxSelectionStrategy.ItemsDoubleClick,
        ComboBoxSelectionStrategy.ItemsClick
    };

        /// <summary>
        /// Tries to select an item in a ComboBox using the configured strategies.
        /// </summary>
        private bool TrySelectComboBoxItem(ComboBox comboBox, int targetIndex, string? targetText = null)
        {
            foreach (var strategy in ComboBoxStrategies)
            {
                try
                {
                    bool success = strategy switch
                    {
                        ComboBoxSelectionStrategy.ItemsClick =>
                            TryItemsClick(comboBox, targetIndex, targetText),
                        ComboBoxSelectionStrategy.ItemsDoubleClick =>
                            TryItemsDoubleClick(comboBox, targetIndex, targetText),
                        ComboBoxSelectionStrategy.ListChildClick =>
                            TryListChildClick(comboBox, targetIndex, targetText),
                        ComboBoxSelectionStrategy.ListChildDoubleClick =>
                            TryListChildDoubleClick(comboBox, targetIndex, targetText),
                        ComboBoxSelectionStrategy.ListChildSelect =>
                            TryListChildSelect(comboBox, targetIndex, targetText),
                        ComboBoxSelectionStrategy.ListChildInvoke =>
                            TryListChildInvoke(comboBox, targetIndex, targetText),
                        ComboBoxSelectionStrategy.KeyboardNavigation =>
                            TryKeyboardNavigation(comboBox, targetIndex, targetText),
                        ComboBoxSelectionStrategy.ClickThenKeyboard =>
                            TryClickThenKeyboard(comboBox, targetIndex, targetText),
                        _ => false
                    };

                    if (success) return true;
                }
                catch
                {
                    // Strategy failed, try next one
                }
            }
            return false;
        }

        private bool TryItemsClick(ComboBox comboBox, int targetIndex, string? targetText)
        {
            var items = comboBox.Items;
            if (items.Length == 0) return false;

            if (targetText != null)
            {
                var item = items.FirstOrDefault(i => i.Text == targetText);
                if (item != null)
                {
                    item.Click();
                    return true;
                }
            }
            else if (targetIndex >= 0 && targetIndex < items.Length)
            {
                items[targetIndex].Click();
                return true;
            }
            return false;
        }

        private bool TryItemsDoubleClick(ComboBox comboBox, int targetIndex, string? targetText)
        {
            var items = comboBox.Items;
            if (items.Length == 0) return false;

            if (targetText != null)
            {
                var item = items.FirstOrDefault(i => i.Text == targetText);
                if (item != null)
                {
                    item.DoubleClick();
                    return true;
                }
            }
            else if (targetIndex >= 0 && targetIndex < items.Length)
            {
                items[targetIndex].DoubleClick();
                return true;
            }
            return false;
        }

        private bool TryListChildClick(ComboBox comboBox, int targetIndex, string? targetText)
        {
            var listChild = comboBox.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            if (listChild == null) return false;

            var listItems = listChild.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
            if (listItems.Length == 0) return false;

            if (targetText != null)
            {
                foreach (var listItem in listItems)
                {
                    if (listItem.Name == targetText)
                    {
                        listItem.Click();
                        return true;
                    }
                }
            }
            else if (targetIndex >= 0 && targetIndex < listItems.Length)
            {
                listItems[targetIndex].Click();
                return true;
            }
            return false;
        }

        private bool TryListChildDoubleClick(ComboBox comboBox, int targetIndex, string? targetText)
        {
            var listChild = comboBox.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            if (listChild == null) return false;

            var listItems = listChild.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
            if (listItems.Length == 0) return false;

            if (targetText != null)
            {
                foreach (var listItem in listItems)
                {
                    if (listItem.Name == targetText)
                    {
                        listItem.DoubleClick();
                        return true;
                    }
                }
            }
            else if (targetIndex >= 0 && targetIndex < listItems.Length)
            {
                listItems[targetIndex].DoubleClick();
                return true;
            }
            return false;
        }

        private bool TryListChildSelect(ComboBox comboBox, int targetIndex, string? targetText)
        {
            // Click the combo's Button child to open dropdown (more reliable than Expand)
            var buttonChild = comboBox.FindFirstChild(cf => cf.ByControlType(ControlType.Button));
            if (buttonChild != null)
            {
                buttonChild.Click();
                Thread.Sleep(200);
            }
            else
            {
                comboBox.Click();
                Thread.Sleep(200);
            }

            var listChild = comboBox.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            if (listChild == null) return false;

            var listItems = listChild.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
            if (listItems.Length == 0) return false;

            AutomationElement? targetItem = null;
            if (targetText != null)
            {
                targetItem = listItems.FirstOrDefault(li => li.Name == targetText);
            }
            else if (targetIndex >= 0 && targetIndex < listItems.Length)
            {
                targetItem = listItems[targetIndex];
            }

            if (targetItem == null) return false;

            // Use SelectionItem pattern
            var selectionItemPattern = targetItem.Patterns.SelectionItem;
            if (selectionItemPattern.IsSupported)
            {
                selectionItemPattern.Pattern.Select();
                Thread.Sleep(100);
                // Press Enter to confirm and close dropdown
                Keyboard.Press(VirtualKeyShort.RETURN);
                Keyboard.Release(VirtualKeyShort.RETURN);
                return true;
            }
            return false;
        }

        private bool TryListChildInvoke(ComboBox comboBox, int targetIndex, string? targetText)
        {
            // Click the combo's Button child to open dropdown (more reliable than Expand)
            var buttonChild = comboBox.FindFirstChild(cf => cf.ByControlType(ControlType.Button));
            if (buttonChild != null)
            {
                buttonChild.Click();
                Thread.Sleep(200);
            }
            else
            {
                comboBox.Click();
                Thread.Sleep(200);
            }

            var listChild = comboBox.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
            if (listChild == null) return false;

            var listItems = listChild.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
            if (listItems.Length == 0) return false;

            AutomationElement? targetItem = null;
            if (targetText != null)
            {
                targetItem = listItems.FirstOrDefault(li => li.Name == targetText);
            }
            else if (targetIndex >= 0 && targetIndex < listItems.Length)
            {
                targetItem = listItems[targetIndex];
            }

            if (targetItem == null) return false;

            // Use Invoke pattern
            var invokePattern = targetItem.Patterns.Invoke;
            if (invokePattern.IsSupported)
            {
                invokePattern.Pattern.Invoke();
                return true;
            }
            return false;
        }

        private bool TryKeyboardNavigation(ComboBox comboBox, int targetIndex, string? targetText)
        {
            // For text-based selection, we need to find the index first
            int index = targetIndex;
            if (targetText != null)
            {
                // Try to find index from Items
                var items = comboBox.Items;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].Text == targetText)
                    {
                        index = i;
                        break;
                    }
                }
                // If not found in Items, try ListChild
                if (index < 0)
                {
                    var listChild = comboBox.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
                    if (listChild != null)
                    {
                        var listItems = listChild.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                        for (int i = 0; i < listItems.Length; i++)
                        {
                            if (listItems[i].Name == targetText)
                            {
                                index = i;
                                break;
                            }
                        }
                    }
                }
            }

            if (index < 0) return false;

            // Navigate using Down arrow keys
            for (int i = 0; i <= index; i++)
            {
                Keyboard.Press(VirtualKeyShort.DOWN);
                Keyboard.Release(VirtualKeyShort.DOWN);
                Thread.Sleep(30);
            }
            Thread.Sleep(50);
            Keyboard.Press(VirtualKeyShort.RETURN);
            Keyboard.Release(VirtualKeyShort.RETURN);
            return true;
        }

        private bool TryClickThenKeyboard(ComboBox comboBox, int targetIndex, string? targetText)
        {
            // Get current selection index before clicking (to calculate delta)
            // Default to 0 because when ComboBox opens, first item is highlighted if nothing selected
            int currentIndex = 0;
            var selectedItem = comboBox.SelectedItem;
            if (selectedItem != null)
            {
                // Find the index of the selected item
                var items = comboBox.Items;
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i].Text == selectedItem.Text)
                    {
                        currentIndex = i;
                        break;
                    }
                }
                // If not found in Items, try ListChild
                if (currentIndex == 0 && selectedItem.Text != null)
                {
                    var listChild = comboBox.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
                    if (listChild != null)
                    {
                        var listItems = listChild.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                        for (int i = 0; i < listItems.Length; i++)
                        {
                            if (listItems[i].Name == selectedItem.Text)
                            {
                                currentIndex = i;
                                break;
                            }
                        }
                    }
                }
            }

            // Always click to establish proper keyboard focus
            // UI Automation's Expand() doesn't establish keyboard focus, but Click() does
            comboBox.Click();
            Thread.Sleep(300); // Wait for dropdown to open and focus to establish

            // For text-based selection, we need to find the target index
            int index = targetIndex;
            if (targetText != null)
            {
                // For "Item 2", index would be 1 (0-based)
                // Try to parse from text like "Item X"
                if (targetText.StartsWith("Item ") && int.TryParse(targetText.Substring(5), out int itemNum))
                {
                    index = itemNum - 1; // Convert 1-based to 0-based
                }
                else
                {
                    // Fallback: try to find from Items or ListChild
                    var items = comboBox.Items;
                    for (int i = 0; i < items.Length; i++)
                    {
                        if (items[i].Text == targetText)
                        {
                            index = i;
                            break;
                        }
                    }
                    if (index < 0)
                    {
                        var listChild = comboBox.FindFirstDescendant(cf => cf.ByControlType(ControlType.List));
                        if (listChild != null)
                        {
                            var listItems = listChild.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                            for (int i = 0; i < listItems.Length; i++)
                            {
                                if (listItems[i].Name == targetText)
                                {
                                    index = i;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (index < 0) return false;

            // Calculate how many key presses needed from current position
            int delta = index - currentIndex;

            if (delta > 0)
            {
                // Navigate DOWN
                for (int i = 0; i < delta; i++)
                {
                    Keyboard.Press(VirtualKeyShort.DOWN);
                    Keyboard.Release(VirtualKeyShort.DOWN);
                    Thread.Sleep(50);
                }
            }
            else if (delta < 0)
            {
                // Navigate UP
                for (int i = 0; i < -delta; i++)
                {
                    Keyboard.Press(VirtualKeyShort.UP);
                    Keyboard.Release(VirtualKeyShort.UP);
                    Thread.Sleep(50);
                }
            }
            // If delta == 0, the item is already selected, just press Enter

            Thread.Sleep(100);
            Keyboard.Press(VirtualKeyShort.RETURN);
            Keyboard.Release(VirtualKeyShort.RETURN);
            return true;
        }

        /// <summary>
        /// Selects an item in a combo box or list box by text.
        /// Uses configurable strategies via ComboBoxStrategies property.
        /// </summary>
        public OperationResult SelectByText(AutomationElement element, int id, string? text)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            if (string.IsNullOrEmpty(text))
                return OperationResult.Failed(id, "Text cannot be null or empty");

            try
            {
                // Try ComboBox
                var comboBox = element.AsComboBox();
                if (comboBox != null)
                {
                    // Check if ClickThenKeyboard is the first strategy - if so, let it handle everything
                    // because Expand() uses UI Automation which doesn't establish proper keyboard focus
                    bool useClickThenKeyboardFirst = ComboBoxStrategies.Length > 0 &&
                        ComboBoxStrategies[0] == ComboBoxSelectionStrategy.ClickThenKeyboard;

                    if (!useClickThenKeyboardFirst)
                    {
                        // Focus the combobox first
                        comboBox.Focus();
                        Thread.Sleep(50);

                        // Expand if needed and wait for dropdown to stabilize
                        if (comboBox.ExpandCollapseState == ExpandCollapseState.Collapsed)
                        {
                            comboBox.Expand();
                            Thread.Sleep(300); // Wait for dropdown to fully open and stabilize
                        }
                    }

                    // Try configured strategies
                    if (TrySelectComboBoxItem(comboBox, -1, text))
                    {
                        return OperationResult.Success(id, element);
                    }

                    return OperationResult.Failed(id, $"Item with text '{text}' not found (tried all strategies)");
                }

                // Try ListBox
                var listBox = element.AsListBox();
                if (listBox != null)
                {
                    var item = listBox.Items.FirstOrDefault(i => i.Text == text);
                    if (item != null)
                    {
                        item.Select();
                        return OperationResult.Success(id, element);
                    }
                    return OperationResult.Failed(id, $"Item with text '{text}' not found");
                }

                return OperationResult.Failed(id, "Element is not a combo box or list box");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Selects an item in a combo box or list box by index.
        /// Uses configurable strategies via ComboBoxStrategies property.
        /// </summary>
        public OperationResult SelectByIndex(AutomationElement element, int id, int index)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            if (index < 0)
                return OperationResult.Failed(id, "Index cannot be negative");

            try
            {
                // Try ComboBox
                var comboBox = element.AsComboBox();
                if (comboBox != null)
                {
                    // Check if ClickThenKeyboard is the first strategy - if so, let it handle everything
                    // because Expand() uses UI Automation which doesn't establish proper keyboard focus
                    bool useClickThenKeyboardFirst = ComboBoxStrategies.Length > 0 &&
                        ComboBoxStrategies[0] == ComboBoxSelectionStrategy.ClickThenKeyboard;

                    if (!useClickThenKeyboardFirst)
                    {
                        // Focus the combobox first
                        comboBox.Focus();
                        Thread.Sleep(50);

                        // Expand if needed and wait for dropdown to stabilize
                        if (comboBox.ExpandCollapseState == ExpandCollapseState.Collapsed)
                        {
                            comboBox.Expand();
                            Thread.Sleep(300); // Wait for dropdown to fully open and stabilize
                        }
                    }

                    // Try configured strategies
                    if (TrySelectComboBoxItem(comboBox, index, null))
                    {
                        return OperationResult.Success(id, element);
                    }

                    return OperationResult.Failed(id, $"Index {index} out of range (tried all strategies)");
                }

                // Try ListBox
                var listBox = element.AsListBox();
                if (listBox != null)
                {
                    if (index < listBox.Items.Length)
                    {
                        listBox.Items[index].Select();
                        return OperationResult.Success(id, element);
                    }
                    return OperationResult.Failed(id, $"Index {index} out of range (0-{listBox.Items.Length - 1})");
                }

                return OperationResult.Failed(id, "Element is not a combo box or list box");
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets all items from a combo box or list box.
        /// </summary>
        public OperationResult<string[]> GetItems(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<string[]>.NotFound(id);
            try
            {
                // Try ComboBox
                var comboBox = element.AsComboBox();
                if (comboBox != null)
                {
                    // Expand if needed to load items
                    var wasCollapsed = comboBox.ExpandCollapseState == ExpandCollapseState.Collapsed;
                    if (wasCollapsed) comboBox.Expand();

                    var items = comboBox.Items.Select(i => i.Text).ToArray();

                    if (wasCollapsed) comboBox.Collapse();

                    return OperationResult<string[]>.Success(id, items);
                }

                // Try ListBox
                var listBox = element.AsListBox();
                if (listBox != null)
                {
                    var items = listBox.Items.Select(i => i.Text).ToArray();
                    return OperationResult<string[]>.Success(id, items);
                }

                return OperationResult<string[]>.Failed(id, "Element is not a combo box or list box");
            }
            catch (Exception ex)
            {
                return OperationResult<string[]>.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Gets the selected item text from a combo box or list box.
        /// </summary>
        public OperationResult<string> GetSelectedItem(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult<string>.NotFound(id);
            try
            {
                // Try ComboBox
                var comboBox = element.AsComboBox();
                if (comboBox != null)
                {
                    var selectedItem = comboBox.SelectedItem;
                    return OperationResult<string>.Success(id, selectedItem?.Text ?? "");
                }

                // Try ListBox
                var listBox = element.AsListBox();
                if (listBox != null)
                {
                    var selectedItems = listBox.SelectedItems;
                    var text = selectedItems.Length > 0 ? selectedItems[0].Text : "";
                    return OperationResult<string>.Success(id, text);
                }

                return OperationResult<string>.Failed(id, "Element is not a combo box or list box");
            }
            catch (Exception ex)
            {
                return OperationResult<string>.Failed(id, ex.Message);
            }
        }

        #endregion ComboBox and ListBox Operations






        /// <summary>
        /// Sets focus to an element.
        /// </summary>
        public OperationResult GetElement(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                //  element.Focus();
                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }

        /// <summary>
        /// Hovers the mouse over an element.
        /// </summary>
        public OperationResult Highlight(AutomationElement element,int id)
        {
            
            if (element == null)
                return OperationResult.NotFound(id);

            try
            {
                //var point = element.GetClickablePoint();
                // Mouse.MoveTo(point);

                //   ElementHighlighter.HighlightElement(element, null);

                _ = Task.Run(() =>
                {
                    try
                    {
                        element.DrawHighlight(false, Color.Orange, System.TimeSpan.FromSeconds(1));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ElementOperations.Highlight] DrawHighlight error: {ex.Message}");
                    }
                });

                return OperationResult.Success(id, element);
            }
            catch (Exception ex)
            {
                return OperationResult.Failed(id, ex.Message);
            }
        }
    }

    #region Info Classes

    /// <summary>
    /// Information about a table item (cell-level headers).
    /// </summary>
    public class TableItemInfo
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public int RowSpan { get; set; }
        public int ColumnSpan { get; set; }
        public AutomationElement[] RowHeaderItems { get; set; } = Array.Empty<AutomationElement>();
        public AutomationElement[] ColumnHeaderItems { get; set; } = Array.Empty<AutomationElement>();
    }

    /// <summary>
    /// Information about an annotation.
    /// </summary>
    public class AnnotationInfo
    {
        public int AnnotationTypeId { get; set; }
        public string AnnotationTypeName { get; set; } = "";
        public string Author { get; set; } = "";
        public string DateTime { get; set; } = "";
        public AutomationElement? Target { get; set; }
    }

    /// <summary>
    /// Information about a range value element.
    /// </summary>
    public class RangeValueInfo
    {
        public double Value { get; set; }
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public double SmallChange { get; set; }
        public double LargeChange { get; set; }
        public bool IsReadOnly { get; set; }
    }

    /// <summary>
    /// Information about scroll state.
    /// </summary>
    public class ScrollInfo
    {
        public double HorizontalScrollPercent { get; set; }
        public double VerticalScrollPercent { get; set; }
        public double HorizontalViewSize { get; set; }
        public double VerticalViewSize { get; set; }
        public bool HorizontallyScrollable { get; set; }
        public bool VerticallyScrollable { get; set; }
    }

    /// <summary>
    /// Information about a grid.
    /// </summary>
    public class GridInfo
    {
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
    }

    /// <summary>
    /// Information about a grid item.
    /// </summary>
    public class GridItemInfo
    {
        public int Row { get; set; }
        public int Column { get; set; }
        public int RowSpan { get; set; }
        public int ColumnSpan { get; set; }
    }

    /// <summary>
    /// Information about a table.
    /// </summary>
    public class TableInfo
    {
        public int RowCount { get; set; }
        public int ColumnCount { get; set; }
        public RowOrColumnMajor RowOrColumnMajor { get; set; }
        public AutomationElement[] ColumnHeaders { get; set; } = Array.Empty<AutomationElement>();
        public AutomationElement[] RowHeaders { get; set; } = Array.Empty<AutomationElement>();
    }




    #endregion Info Classes
}
