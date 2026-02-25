using ApexUIBridge.Core.Logger;
using ApexUIBridge.ViewModels;

using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.UIA3;

using LMStudioExampleFormApp;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Application = FlaUI.Core.Application;
using Debug = System.Diagnostics.Debug;

namespace ApexUIBridge.Core
{
    /// <summary>
    /// The Bridge is the command-first automation façade that the AI chat panel (and
    /// the manual Bridge command panel in <see cref="Forms.StartupForm"/>) dispatch to.
    ///
    /// <para><b>Element registry</b> — <see cref="ScanWindowByName"/> and
    /// <see cref="ScanWindowAsync"/> walk the UIA tree depth-first (max 25 levels) and
    /// populate a <see cref="ConcurrentDictionary{TKey,TValue}"/> of
    /// <see cref="ElementRecord"/> objects keyed by their deterministic integer ID.
    /// The registry is cleared and rebuilt on every scan.</para>
    ///
    /// <para><b>Command dispatch</b> — <see cref="ExecuteCommand"/> parses a single
    /// text command (e.g. <c>CLICK 12345</c>) and routes it to the corresponding
    /// helper method, which resolves the element from the registry and delegates to
    /// <see cref="ElementOperations"/>. The full command surface is:</para>
    /// <list type="table">
    ///   <listheader><term>Category</term><description>Commands</description></listheader>
    ///   <item><term>Mouse</term><description>CLICK, DOUBLE_CLICK, RIGHT_CLICK, MIDDLE_CLICK, CLICK_OFFSET, CLICK_COORDS, DRAG, DRAG_TO_ELEMENT, HOVER</description></item>
    ///   <item><term>Keyboard</term><description>TYPE, SEND_KEYS</description></item>
    ///   <item><term>Value/State</term><description>SET_VALUE, SET_SLIDER, TOGGLE, EXPAND, COLLAPSE, SELECT, SELECT_BY_TEXT, SELECT_BY_INDEX, FOCUS, HIGHLIGHT</description></item>
    ///   <item><term>Scroll</term><description>SCROLL (up/down/left/right/pageup/pagedown), SCROLL_HORIZONTAL, SCROLL_INTO_VIEW</description></item>
    ///   <item><term>Window</term><description>WINDOW_ACTION (minimize/maximize/restore/close), LIST_WINDOWS, SCAN_WINDOW</description></item>
    ///   <item><term>Inspect</term><description>GET_ELEMENT, GET_TEXT, GET_TREE, SEARCH, REFRESH</description></item>
    ///   <item><term>Capture</term><description>CAPTURE, CAPTURE_WINDOW, DESCRIBE (via LM Studio vision model)</description></item>
    ///   <item><term>Meta</term><description>HELP</description></item>
    /// </list>
    ///
    /// <para><b>Menu traversal</b> — <see cref="GetMenus"/> attaches to a target
    /// application's menu bar, recursively expands each menu item using
    /// <see cref="ExpandCollapse"/>, records the full element detail tree, and returns
    /// a hierarchical JSON representation.</para>
    /// </summary>
    internal class Bridge
    {
        private readonly ElementOperations Operations = new();
        private readonly UIA3Automation _automation = new();
        private readonly ConcurrentDictionary<int, ElementRecord> _elements = new();
        private readonly ElementIdGenerator _idGenerator = new();

        private string _lmStudioEndpoint = "http://localhost:1234/v1/chat/completions";
        private string _lmStudioModel = "lfm2-vl-1.6b";

        public Bridge()
        {
        }

        /// <summary>
        /// Configures the LM Studio client for DESCRIBE commands.
        /// </summary>
        public void ConfigureLMStudio(string endpoint, string model)
        {
            _lmStudioEndpoint = endpoint;
            _lmStudioModel = model;
        }

        #region Element Resolution

        /// <summary>
        /// Resolves a live AutomationElement from the internal registry by ID.
        /// </summary>
        private AutomationElement? ResolveElement(int elementId)
        {
            if (_elements.TryGetValue(elementId, out var record))
                return record.AutomationElement;
            return null;
        }

        /// <summary>
        /// Gets an ElementRecord by ID.
        /// </summary>
        public ElementRecord? GetElement(int elementId)
        {
            _elements.TryGetValue(elementId, out var record);
            return record;
        }

        /// <summary>
        /// Gets all scanned ElementRecords.
        /// </summary>
        public IReadOnlyList<ElementRecord> GetAllElements()
        {
            return _elements.Values.ToList();
        }

        #endregion Element Resolution

        #region Window Scanning

        /// <summary>
        /// Scans a window by process/window name (title substring match).
        /// Returns element count.
        /// </summary>
        public async Task<(bool success, string message, int elementCount)> ScanWindowByName(string windowName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var desktop = _automation.GetDesktop();
                    var windows = desktop.FindAllChildren();

                    var window = windows.FirstOrDefault(w =>
                    {
                        var name = w.Properties.Name.ValueOrDefault ?? "";
                        return name.Contains(windowName, StringComparison.OrdinalIgnoreCase);
                    });

                    if (window == null)
                    {
                        // Try by process name
                        var processes = Process.GetProcessesByName(windowName);
                        if (processes.Length > 0 && processes[0].MainWindowHandle != IntPtr.Zero)
                        {
                            window = _automation.FromHandle(processes[0].MainWindowHandle);
                        }
                    }

                    if (window == null)
                        return (false, $"Window '{windowName}' not found", 0);

                    _elements.Clear();
                    _idGenerator.Reset();
                    ScanElementRecursive(window, null, null, window.Properties.NativeWindowHandle.ValueOrDefault, 0, 25);

                    return (true, $"Scanned {_elements.Count} elements", _elements.Count);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Bridge.ScanWindowByName] Error: {ex.Message}");
                    return (false, ex.Message, 0);
                }
            });
        }

        /// <summary>
        /// Scans a window by handle.
        /// </summary>
        public async Task<(bool success, string message, int elementCount)> ScanWindowAsync(IntPtr hwnd)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var window = _automation.FromHandle(hwnd);
                    if (window == null)
                        return (false, "Could not find window", 0);

                    _elements.Clear();
                    _idGenerator.Reset();
                    ScanElementRecursive(window, null, null, hwnd, 0, 25);

                    return (true, $"Scanned {_elements.Count} elements", _elements.Count);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Bridge.ScanWindowAsync] Error: {ex.Message}");
                    return (false, ex.Message, 0);
                }
            });
        }

        /// <summary>
        /// Recursively scans an element and its children into the registry.
        /// </summary>
        private int ScanElementRecursive(AutomationElement element, int? parentId, string? parentHash, IntPtr windowHandle, int depth, int maxDepth, int siblingIndex = 0)
        {
            int id = 0;
            string hash = "";

            try
            {
                var isWindowOrPane = false;
                try
                {
                    var ct = element.Properties.ControlType.ValueOrDefault;
                    isWindowOrPane = ct == ControlType.Window || ct == ControlType.Pane;
                }
                catch { }

                var effectiveHandle = isWindowOrPane ? windowHandle : IntPtr.Zero;

                hash = _idGenerator.GenerateElementHash(
                    element,
                    parentId,
                    parentHash,
                    effectiveHandle,
                    excludeName: isWindowOrPane,
                    siblingIndex);

                id = _idGenerator.GenerateIdFromHash(hash);

                var findCriteria = ElementFindCriteria.FromAutomationElement(element, windowHandleOverride: windowHandle);
                var record = ElementRecord.FromAutomationElement(element, id, parentId, hash, findCriteria, windowHandle);

                var childIds = new List<int>();
                if (depth < maxDepth)
                {
                    try
                    {
                        var children = element.FindAllChildren();
                        int childIndex = 0;
                        foreach (var child in children)
                        {
                            try
                            {
                                var childId = ScanElementRecursive(child, id, hash, windowHandle, depth + 1, maxDepth, childIndex);
                                childIds.Add(childId);
                                childIndex++;
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[Bridge.ScanElementRecursive] Error scanning child element: {ex.Message}");
                                childIndex++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Bridge.ScanElementRecursive] Error enumerating children of element {id}: {ex.Message}");
                    }
                }

                _elements[id] = record with { ChildIds = childIds };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Bridge.ScanElementRecursive] Error creating record for element {id}: {ex.Message}");
            }

            return id;
        }

        #endregion Window Scanning

        #region Command Execution

        public async Task<CommandResult> ExecuteCommand(string command)
        {
            try
            {
                command ??= string.Empty;
                var trimmedCommand = command.Trim();
                var parts = trimmedCommand.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                {
                    return new CommandResult
                    {
                        IsSuccess = false,
                        Message = "Empty command",
                        Command = trimmedCommand
                    };
                }

                var action = parts[0].ToUpperInvariant();

                switch (action)
                {
                    case "CLICK":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int clickId))
                            return CommandResult.Failed(command, "Usage: CLICK <elementId>");

                        var clickResult = ClickElement(clickId);
                        return new CommandResult
                        {
                            IsSuccess = clickResult.success,
                            Message = clickResult.message,
                            Command = command
                        };

                    case "TYPE":
                        if (!TryParseElementCommandWithRemainder(trimmedCommand, "TYPE", out int typeId, out var text) ||
                            string.IsNullOrWhiteSpace(text))
                            return CommandResult.Failed(command, "Usage: TYPE <elementId> <text>");

                        var typeResult = TypeText(typeId, text);
                        return new CommandResult
                        {
                            IsSuccess = typeResult.success,
                            Message = typeResult.message,
                            Command = command
                        };

                    case "GET_ELEMENT":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int getElId))
                            return CommandResult.Failed(command, "Usage: GET_ELEMENT <elementId>");

                        var element = GetElement(getElId);
                        if (element == null)
                            return CommandResult.Failed(command, $"Element {getElId} not found");

                        var elementJson = JsonSerializer.Serialize(new
                        {
                            ElementId = element.Id,
                            Name = element.Name,
                            ControlType = element.ControlType.ToString(),
                            IsEnabled = element.IsEnabled
                        }, new JsonSerializerOptions { WriteIndented = true });

                        return new CommandResult
                        {
                            IsSuccess = true,
                            Message = "Element retrieved",
                            Command = command,
                            Data = elementJson
                        };

                    case "SEARCH":
                        if (parts.Length < 2)
                            return CommandResult.Failed(command, "Usage: SEARCH <searchText>");

                        var searchText = string.Join(" ", parts.Skip(1));
                        var searchResults = Search(searchText);
                        var resultsJson = JsonSerializer.Serialize(searchResults.Select(e => new
                        {
                            ElementId = e.Id,
                            Name = e.Name,
                            ControlType = e.ControlType.ToString()
                        }), new JsonSerializerOptions { WriteIndented = true });

                        return new CommandResult
                        {
                            IsSuccess = true,
                            Message = $"Found {searchResults.Count} elements matching '{searchText}'",
                            Command = command,
                            Data = resultsJson
                        };

                    case "DOUBLE_CLICK":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int dblClickId))
                            return CommandResult.Failed(command, "Usage: DOUBLE_CLICK <elementId>");

                        var dblClickResult = DoubleClickElement(dblClickId);
                        return new CommandResult
                        {
                            IsSuccess = dblClickResult.success,
                            Message = dblClickResult.message,
                            Command = command
                        };

                    case "RIGHT_CLICK":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int rightClickId))
                            return CommandResult.Failed(command, "Usage: RIGHT_CLICK <elementId>");

                        var rightClickResult = RightClickElement(rightClickId);
                        return new CommandResult
                        {
                            IsSuccess = rightClickResult.success,
                            Message = rightClickResult.message,
                            Command = command
                        };

                    case "DRAG":
                        if (parts.Length < 4 || !int.TryParse(parts[1], out int dragId) ||
                            !int.TryParse(parts[2], out int targetX) || !int.TryParse(parts[3], out int targetY))
                            return CommandResult.Failed(command, "Usage: DRAG <elementId> <targetX> <targetY>");

                        var dragResult = DragElementToPosition(dragId, targetX, targetY);
                        return new CommandResult
                        {
                            IsSuccess = dragResult.success,
                            Message = dragResult.message,
                            Command = command
                        };

                    case "SELECT":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int selectId))
                            return CommandResult.Failed(command, "Usage: SELECT <elementId>");

                        var selectResult = SelectListItem(selectId);
                        return new CommandResult
                        {
                            IsSuccess = selectResult.success,
                            Message = selectResult.message,
                            Command = command
                        };

                    case "SCROLL":
                        if (parts.Length < 3 || !int.TryParse(parts[1], out int scrollId))
                            return CommandResult.Failed(command, "Usage: SCROLL <elementId> <direction> [amount]");

                        var scrollDirection = parts[2].ToLowerInvariant();
                        int scrollAmount = 1;
                        if (parts.Length > 3)
                        {
                            int.TryParse(parts[3], out scrollAmount);
                        }

                        var scrollResult = ScrollElementDirection(scrollId, scrollDirection, scrollAmount);
                        return new CommandResult
                        {
                            IsSuccess = scrollResult.success,
                            Message = scrollResult.message,
                            Command = command
                        };

                    case "SET_VALUE":
                        if (!TryParseElementCommandWithRemainder(trimmedCommand, "SET_VALUE", out int setValueId, out var value))
                            return CommandResult.Failed(command, "Usage: SET_VALUE <elementId> <value>");

                        var setValueResult = SetElementValue(setValueId, value);
                        return new CommandResult
                        {
                            IsSuccess = setValueResult.success,
                            Message = setValueResult.message,
                            Command = command
                        };

                    case "WINDOW_ACTION":
                        if (parts.Length < 3 || !int.TryParse(parts[1], out int windowActionId))
                            return CommandResult.Failed(command, "Usage: WINDOW_ACTION <elementId> <action> (action: minimize, maximize, restore, close)");

                        var windowActionName = parts[2].ToLowerInvariant();
                        var windowActionResult = WindowAction(windowActionId, windowActionName);
                        return new CommandResult
                        {
                            IsSuccess = windowActionResult.success,
                            Message = windowActionResult.message,
                            Command = command
                        };

                    case "SET_SLIDER":
                        if (parts.Length < 3 || !int.TryParse(parts[1], out int sliderId) ||
                            !double.TryParse(parts[2], out double sliderValue))
                            return CommandResult.Failed(command, "Usage: SET_SLIDER <elementId> <value>");

                        var sliderResult = SetSliderValue(sliderId, sliderValue);
                        return new CommandResult
                        {
                            IsSuccess = sliderResult.success,
                            Message = sliderResult.message,
                            Command = command
                        };

                    case "GET_TEXT":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int getTextId))
                            return CommandResult.Failed(command, "Usage: GET_TEXT <elementId>");

                        var getTextResult = GetTextContent(getTextId);
                        return new CommandResult
                        {
                            IsSuccess = getTextResult.success,
                            Message = getTextResult.message,
                            Command = command,
                            Data = getTextResult.text
                        };

                    case "LIST_WINDOWS":
                        try
                        {
                            var windows = await Task.Run(() => GetListOfWindows());

                            if (windows == null || windows.Count == 0)
                            {
                                return new CommandResult
                                {
                                    IsSuccess = true,
                                    Message = "No windows found",
                                    Command = command,
                                    Data = "[]"
                                };
                            }

                            var windowsJson = JsonSerializer.Serialize(windows, new JsonSerializerOptions { WriteIndented = true });

                            return new CommandResult
                            {
                                IsSuccess = true,
                                Message = $"Found {windows.Count} window(s)",
                                Command = command,
                                Data = windowsJson
                            };
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[LIST_WINDOWS] Error: {ex.Message}");
                            return CommandResult.Failed(command, $"Error listing windows: {ex.Message}");
                        }

                    case "MIDDLE_CLICK":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int middleClickId))
                            return CommandResult.Failed(command, "Usage: MIDDLE_CLICK <elementId>");

                        var middleClickResult = MiddleClickElement(middleClickId);
                        return new CommandResult
                        {
                            IsSuccess = middleClickResult.success,
                            Message = middleClickResult.message,
                            Command = command
                        };

                    case "CLICK_OFFSET":
                        if (parts.Length < 4 || !int.TryParse(parts[1], out int clickOffsetId) ||
                            !int.TryParse(parts[2], out int offsetX) || !int.TryParse(parts[3], out int offsetY))
                            return CommandResult.Failed(command, "Usage: CLICK_OFFSET <elementId> <x> <y>");

                        var clickOffsetResult = ClickAtOffset(clickOffsetId, offsetX, offsetY);
                        return new CommandResult
                        {
                            IsSuccess = clickOffsetResult.success,
                            Message = clickOffsetResult.message,
                            Command = command
                        };

                    case "CLICK_COORDS":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int clickCoordsId))
                            return CommandResult.Failed(command, "Usage: CLICK_COORDS <elementId>");

                        var clickCoordsResult = ClickByCoordinates(clickCoordsId);
                        return new CommandResult
                        {
                            IsSuccess = clickCoordsResult.success,
                            Message = clickCoordsResult.message,
                            Command = command
                        };

                    case "TOGGLE":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int toggleId))
                            return CommandResult.Failed(command, "Usage: TOGGLE <elementId>");

                        var toggleResult = ToggleElement(toggleId);
                        return new CommandResult
                        {
                            IsSuccess = toggleResult.success,
                            Message = toggleResult.message,
                            Command = command
                        };

                    case "EXPAND":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int expandId))
                            return CommandResult.Failed(command, "Usage: EXPAND <elementId>");

                        var expandResult = ExpandElement(expandId);
                        return new CommandResult
                        {
                            IsSuccess = expandResult.success,
                            Message = expandResult.message,
                            Command = command
                        };

                    case "COLLAPSE":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int collapseId))
                            return CommandResult.Failed(command, "Usage: COLLAPSE <elementId>");

                        var collapseResult = CollapseElement(collapseId);
                        return new CommandResult
                        {
                            IsSuccess = collapseResult.success,
                            Message = collapseResult.message,
                            Command = command
                        };

                    case "FOCUS":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int focusId))
                            return CommandResult.Failed(command, "Usage: FOCUS <elementId>");

                        var focusResult = FocusElement(focusId);
                        return new CommandResult
                        {
                            IsSuccess = focusResult.success,
                            Message = focusResult.message,
                            Command = command
                        };

                    case "HOVER":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int hoverId))
                            return CommandResult.Failed(command, "Usage: HOVER <elementId>");

                        var hoverResult = HoverElement(hoverId);
                        return new CommandResult
                        {
                            IsSuccess = hoverResult.success,
                            Message = hoverResult.message,
                            Command = command
                        };

                    case "HIGHLIGHT":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int highlightId))
                            return CommandResult.Failed(command, "Usage: HIGHLIGHT <elementId>");

                        var highlightResult = Highlight(highlightId);
                        return new CommandResult
                        {
                            IsSuccess = highlightResult.success,
                            Message = highlightResult.message,
                            Command = command
                        };

                    case "SCROLL_HORIZONTAL":
                        if (parts.Length < 3 || !int.TryParse(parts[1], out int scrollHorizId) ||
                            !int.TryParse(parts[2], out int scrollHorizAmount))
                            return CommandResult.Failed(command, "Usage: SCROLL_HORIZONTAL <elementId> <amount>");

                        var scrollHorizResult = ScrollElementHorizontal(scrollHorizId, scrollHorizAmount);
                        return new CommandResult
                        {
                            IsSuccess = scrollHorizResult.success,
                            Message = scrollHorizResult.message,
                            Command = command
                        };

                    case "SEND_KEYS":
                        if (!TryParseElementCommandWithRemainder(trimmedCommand, "SEND_KEYS", out int sendKeysId, out var keys) ||
                            string.IsNullOrWhiteSpace(keys))
                            return CommandResult.Failed(command, "Usage: SEND_KEYS <elementId> <keys>");

                        var sendKeysResult = SendKeysToElement(sendKeysId, keys);
                        return new CommandResult
                        {
                            IsSuccess = sendKeysResult.success,
                            Message = sendKeysResult.message,
                            Command = command
                        };

                    case "SELECT_BY_TEXT":
                        if (!TryParseElementCommandWithRemainder(trimmedCommand, "SELECT_BY_TEXT", out int selectTextId, out var selectText) ||
                            string.IsNullOrWhiteSpace(selectText))
                            return CommandResult.Failed(command, "Usage: SELECT_BY_TEXT <elementId> <text>");

                        var selectTextResult = SelectByText(selectTextId, selectText);
                        return new CommandResult
                        {
                            IsSuccess = selectTextResult.success,
                            Message = selectTextResult.message,
                            Command = command
                        };

                    case "SELECT_BY_INDEX":
                        if (parts.Length < 3 || !int.TryParse(parts[1], out int selectIndexId) ||
                            !int.TryParse(parts[2], out int selectIdx))
                            return CommandResult.Failed(command, "Usage: SELECT_BY_INDEX <elementId> <index>");

                        var selectIndexResult = SelectByIndex(selectIndexId, selectIdx);
                        return new CommandResult
                        {
                            IsSuccess = selectIndexResult.success,
                            Message = selectIndexResult.message,
                            Command = command
                        };

                    case "SCROLL_INTO_VIEW":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int scrollIntoViewId))
                            return CommandResult.Failed(command, "Usage: SCROLL_INTO_VIEW <elementId>");

                        var scrollIntoViewResult = ScrollIntoView(scrollIntoViewId);
                        return new CommandResult
                        {
                            IsSuccess = scrollIntoViewResult.success,
                            Message = scrollIntoViewResult.message,
                            Command = command
                        };

                    case "DRAG_TO_ELEMENT":
                        if (parts.Length < 3 || !int.TryParse(parts[1], out int dragSourceId) ||
                            !int.TryParse(parts[2], out int dragTargetId))
                            return CommandResult.Failed(command, "Usage: DRAG_TO_ELEMENT <sourceElementId> <targetElementId>");

                        var dragToElementResult = DragElement(dragSourceId, dragTargetId);
                        return new CommandResult
                        {
                            IsSuccess = dragToElementResult.success,
                            Message = dragToElementResult.message,
                            Command = command
                        };

                    case "GET_TREE":
                        int treeMaxDepth = 10;
                        if (parts.Length > 1 && int.TryParse(parts[1], out int parsedDepth))
                        {
                            treeMaxDepth = parsedDepth;
                        }
                        var treeText = GetElementsText(treeMaxDepth);
                        return new CommandResult
                        {
                            IsSuccess = true,
                            Message = "Element tree retrieved",
                            Command = command,
                            Data = treeText
                        };

                    case "REFRESH":
                        return new CommandResult
                        {
                            IsSuccess = true,
                            Message = "Tree refreshed",
                            Command = command,
                        };

                    case "SCAN_WINDOW":
                        if (parts.Length < 2)
                            return CommandResult.Failed(command, "Usage: SCAN_WINDOW <windowName>");

                        var scanWindowName = string.Join(" ", parts.Skip(1));
                        var scanWindowResult = await ScanWindowByName(scanWindowName);
                        var scanData = scanWindowResult.success
                            ? GetElementsText(10)
                            : $"Scan failed: {scanWindowResult.message}";
                        return new CommandResult
                        {
                            IsSuccess = scanWindowResult.success,
                            Message = $"Scanned {scanWindowResult.elementCount} elements",
                            Command = command,
                            Data = scanData
                        };

                    case "HELP":
                        var helpText = GetHelpText();
                        return new CommandResult
                        {
                            IsSuccess = true,
                            Message = "Help information",
                            Command = command,
                            Data = helpText
                        };

                    case "CAPTURE":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int captureId))
                            return CommandResult.Failed(command, "Usage: CAPTURE <elementId>");

                        var captureResult = CaptureElement(captureId);
                        return new CommandResult
                        {
                            IsSuccess = captureResult.success,
                            Message = captureResult.message,
                            Command = command,
                            Data = captureResult.filePath
                        };

                    case "CAPTURE_WINDOW":
                        if (parts.Length < 2)
                            return CommandResult.Failed(command, "Usage: CAPTURE_WINDOW <windowName> [elementId1] [elementId2] ...");

                        // Parse window name (could be multi-word) and optional element IDs
                        var captureWindowArgs = string.Join(" ", parts.Skip(1));
                        var captureWindowResult = await CaptureWindowAsync(captureWindowArgs);
                        return new CommandResult
                        {
                            IsSuccess = captureWindowResult.success,
                            Message = captureWindowResult.message,
                            Command = command,
                            Data = captureWindowResult.filePath
                        };

                    case "DESCRIBE":
                        if (parts.Length < 2 || !int.TryParse(parts[1], out int describeId))
                            return CommandResult.Failed(command, "Usage: DESCRIBE <elementId> [prompt]");

                        var describePrompt = parts.Length > 2
                            ? string.Join(" ", parts.Skip(2))
                            : "Describe this UI element. What is it and what does it contain?";

                        var describeResult = await DescribeElementAsync(describeId, describePrompt);
                        return new CommandResult
                        {
                            IsSuccess = describeResult.success,
                            Message = describeResult.message,
                            Command = command,
                            Data = describeResult.description
                        };

                    default:
                        return CommandResult.Failed(command, $"Unknown command: {action}. Type 'HELP' for available commands.");
                }
            }
            catch (Exception ex)
            {
                return new CommandResult
                {
                    IsSuccess = false,
                    Message = $"Error executing command: {ex.Message}",
                    Command = command
                };
            }
        }

        #endregion Command Execution

        #region Parsing

        private static bool TryParseElementCommandWithRemainder(string command, string action, out int elementId, out string remainder)
        {
            elementId = 0;
            remainder = string.Empty;

            if (string.IsNullOrWhiteSpace(command))
                return false;

            var actionLength = action.Length;
            if (!command.StartsWith(action, StringComparison.OrdinalIgnoreCase))
                return false;

            if (command.Length <= actionLength || !char.IsWhiteSpace(command[actionLength]))
                return false;

            var args = command.Substring(actionLength).TrimStart();
            if (string.IsNullOrEmpty(args))
                return false;

            var firstSpaceIndex = args.IndexOf(' ');
            var idText = firstSpaceIndex >= 0 ? args.Substring(0, firstSpaceIndex) : args;
            if (!int.TryParse(idText, out elementId))
                return false;

            remainder = firstSpaceIndex >= 0 ? args.Substring(firstSpaceIndex + 1) : string.Empty;
            return true;
        }

        #endregion Parsing

        #region Element Operations

        /// <summary>
        /// Helper to resolve element and run an operation.
        /// </summary>
        private (bool success, string message) RunOp(int elementId, Func<AutomationElement, int, OperationResult> op)
        {
            var element = ResolveElement(elementId);
            if (element == null)
                return (false, $"Element {elementId} not found in registry");

            try
            {
                var result = op(element, elementId);
                return (result.IsSuccess, result.ErrorMessage ?? "Success");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Bridge.RunOp] Error on element {elementId}: {ex.Message}");
                return (false, ex.Message);
            }
        }

        public (bool success, string message) ClickElement(int elementId)
            => RunOp(elementId, Operations.Click);

        public (bool success, string message) DoubleClickElement(int elementId)
            => RunOp(elementId, Operations.DoubleClick);

        public (bool success, string message) RightClickElement(int elementId)
            => RunOp(elementId, Operations.RightClick);

        public (bool success, string message) MiddleClickElement(int elementId)
            => RunOp(elementId, Operations.MiddleClick);

        public (bool success, string message) ClickAtOffset(int elementId, int x, int y)
            => RunOp(elementId, (el, id) => Operations.ClickAtOffset(el, id, x, y));

        public (bool success, string message) ToggleElement(int elementId)
            => RunOp(elementId, Operations.Toggle);

        public (bool success, string message) ExpandElement(int elementId)
            => RunOp(elementId, Operations.Expand);

        public (bool success, string message) CollapseElement(int elementId)
            => RunOp(elementId, Operations.Collapse);

        public (bool success, string message) FocusElement(int elementId)
            => RunOp(elementId, Operations.Focus);

        public (bool success, string message) HoverElement(int elementId)
            => RunOp(elementId, Operations.Hover);

        public (bool success, string message) Highlight(int elementId)
            => RunOp(elementId, Operations.Highlight);

        public (bool success, string message) ScrollIntoView(int elementId)
            => RunOp(elementId, Operations.ScrollIntoView);

        public (bool success, string message) TypeText(int elementId, string text, bool submit = false)
        {
            var element = ResolveElement(elementId);
            if (element == null) return (false, $"Element {elementId} not found");

            try
            {
                var result = Operations.SetValue(element, elementId, text);
                if (result.IsSuccess && submit)
                {
                    Operations.Focus(element, elementId);
                    FlaUI.Core.Input.Keyboard.Press(FlaUI.Core.WindowsAPI.VirtualKeyShort.ENTER);
                }
                return (result.IsSuccess, result.ErrorMessage ?? "Text set successfully");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public (bool success, string message) SetElementValue(int elementId, string value)
            => RunOp(elementId, (el, id) => Operations.SetValue(el, id, value));

        public (bool success, string message, string? text) GetTextContent(int elementId)
        {
            var element = ResolveElement(elementId);
            if (element == null) return (false, $"Element {elementId} not found", null);

            try
            {
                var result = Operations.GetValue(element, elementId);
                return (result.IsSuccess, result.ErrorMessage ?? "Success", result.Value);
            }
            catch (Exception ex)
            {
                return (false, ex.Message, null);
            }
        }

        public (bool success, string message) SetSliderValue(int elementId, double value)
            => RunOp(elementId, (el, id) => Operations.SetValue(el, id, value.ToString()));

        public (bool success, string message) SendKeysToElement(int elementId, string keys)
            => RunOp(elementId, (el, id) => Operations.SendKeys(el, id, keys));

        public (bool success, string message) SelectByText(int elementId, string text)
            => RunOp(elementId, (el, id) => Operations.SelectByText(el, id, text));

        public (bool success, string message) SelectByIndex(int elementId, int index)
            => RunOp(elementId, (el, id) => Operations.SelectByIndex(el, id, index));

        public (bool success, string message) SelectListItem(int elementId)
            => RunOp(elementId, Operations.Select);

        public (bool success, string message) ScrollElement(int elementId, int amount)
            => RunOp(elementId, (el, id) => Operations.Scroll(el, id, amount));

        public (bool success, string message) ScrollElementHorizontal(int elementId, int amount)
            => RunOp(elementId, (el, id) => Operations.ScrollHorizontal(el, id, amount));

        public (bool success, string message) DragElementToPosition(int elementId, int targetX, int targetY)
            => RunOp(elementId, (el, id) => Operations.DragToPosition(el, id, targetX, targetY));

        /// <summary>
        /// Fallback click using raw screen coordinates from the element's stored bounding rectangle.
        /// Brings the owning window to the foreground before clicking.
        /// </summary>
        public (bool success, string message) ClickByCoordinates(int elementId)
        {
            if (!_elements.TryGetValue(elementId, out var record))
                return (false, $"Element {elementId} not found in registry");

            var rect = record.BoundingRectangle;
            if (rect.IsEmpty || rect.Width == 0 || rect.Height == 0)
                return (false, $"Element {elementId} has no valid bounding rectangle");

            try
            {
                // Bring the owning window to the foreground
                var hwnd = record.WindowHandle;
                if (hwnd != IntPtr.Zero)
                {
                    NativeMethods.SetForegroundWindow(hwnd);
                    System.Threading.Thread.Sleep(100);
                }

                int cx = rect.X + rect.Width / 2;
                int cy = rect.Y + rect.Height / 2;

                Mouse.MoveTo(new System.Drawing.Point(cx, cy));
                Mouse.Click(FlaUI.Core.Input.MouseButton.Left);

                return (true, $"Clicked at screen coordinates ({cx}, {cy}) for element {elementId}");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to click at coordinates: {ex.Message}");
            }
        }

        public (bool success, string message) DragElement(int sourceId, int targetId)
        {
            var sourceEl = ResolveElement(sourceId);
            var targetEl = ResolveElement(targetId);
            if (sourceEl == null) return (false, $"Source element {sourceId} not found");
            if (targetEl == null) return (false, $"Target element {targetId} not found");

            try
            {
                var result = Operations.DragTo(sourceEl, sourceId, targetEl, targetId);
                return (result.IsSuccess, result.ErrorMessage ?? "Drag successful");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion Element Operations

        #region Capture & Describe

        /// <summary>
        /// Captures a screenshot of an element and saves it to a temp file.
        /// </summary>
        public (bool success, string message, string? filePath) CaptureElement(int elementId)
        {
            var element = ResolveElement(elementId);
            if (element == null)
                return (false, $"Element {elementId} not found", null);

            try
            {
                using var bitmap = element.Capture();
                var tempPath = Path.Combine(Path.GetTempPath(), $"apexuibridge_capture_{elementId}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bitmap.Save(tempPath, ImageFormat.Png);
                return (true, $"Element {elementId} captured to {tempPath}", tempPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Bridge.CaptureElement] Error: {ex.Message}");
                return (false, $"Failed to capture element {elementId}: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Captures a window screenshot. Optionally highlights specific elements.
        /// Args format: "windowName elementId1 elementId2 ..."
        /// </summary>
        public async Task<(bool success, string message, string? filePath)> CaptureWindowAsync(string args)
        {
            try
            {
                // Parse: first try to split out element IDs from the end
                var argParts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var elementIds = new List<int>();
                var windowNameParts = new List<string>();

                // Walk from the end: collect trailing integers as element IDs
                for (int i = argParts.Length - 1; i >= 0; i--)
                {
                    if (int.TryParse(argParts[i], out int eid))
                        elementIds.Insert(0, eid);
                    else
                    {
                        windowNameParts = argParts.Take(i + 1).ToList();
                        break;
                    }
                }

                if (windowNameParts.Count == 0)
                    return (false, "Usage: CAPTURE_WINDOW <windowName> [elementId1] [elementId2] ...", null);

                var windowName = string.Join(" ", windowNameParts);

                // First scan the window if not already scanned
                var scanResult = await ScanWindowByName(windowName);
                if (!scanResult.success)
                    return (false, $"Window '{windowName}' not found: {scanResult.message}", null);

                // Find the window element (root of scanned tree)
                var windowElement = _elements.Values
                    .FirstOrDefault(e => !e.ParentId.HasValue || !_elements.ContainsKey(e.ParentId.Value));

                if (windowElement?.AutomationElement == null)
                    return (false, $"Could not resolve window element for '{windowName}'", null);

                using var bitmap = windowElement.AutomationElement.Capture();
                var tempPath = Path.Combine(Path.GetTempPath(), $"apexuibridge_window_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bitmap.Save(tempPath, ImageFormat.Png);

                var msg = $"Window '{windowName}' captured to {tempPath}";
                if (elementIds.Count > 0)
                    msg += $" (referenced elements: {string.Join(", ", elementIds)})";

                return (true, msg, tempPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Bridge.CaptureWindowAsync] Error: {ex.Message}");
                return (false, $"Failed to capture window: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Captures an element screenshot and sends it to LM Studio for description.
        /// </summary>
        public async Task<(bool success, string message, string? description)> DescribeElementAsync(int elementId, string prompt)
        {
            var element = ResolveElement(elementId);
            if (element == null)
                return (false, $"Element {elementId} not found", null);

            string? tempPath = null;
            try
            {
                // Capture the element to a temp file
                using var bitmap = element.Capture();
                tempPath = Path.Combine(Path.GetTempPath(), $"apexuibridge_describe_{elementId}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                bitmap.Save(tempPath, ImageFormat.Png);

                // Get element metadata for context
                var record = GetElement(elementId);
                var contextPrompt = $"{prompt}\n\nElement info: Name='{record?.Name}', Type={record?.ControlType}, AutomationId='{record?.AutomationId}'";

                // Send to LM Studio vision model (fresh client per call to avoid history accumulation)
                using var client = new LMStudioExample(
                    _lmStudioEndpoint,
                    _lmStudioModel,
                    "You are a UI element descriptor. Describe what you see in the screenshot concisely."
                );
                client.initialize(30);

                var description = await client.SendMessageWithImagesAsync(
                    contextPrompt,
                    new[] { tempPath },
                    CancellationToken.None
                );

                return (true, $"Element {elementId} described successfully", description);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Bridge.DescribeElementAsync] Error: {ex.Message}");
                return (false, $"Failed to describe element {elementId}: {ex.Message}", null);
            }
            finally
            {
                // Clean up temp file
                if (tempPath != null && File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
            }
        }

        #endregion Capture & Describe

        #region Window Operations

        public (bool success, string message) WindowAction(int elementId, string action)
        {
            var record = GetElement(elementId);
            if (record == null) return (false, $"Element {elementId} not found");

            try
            {
                var hwnd = record.WindowHandle;
                if (hwnd == IntPtr.Zero)
                {
                    // Try the element's own handle
                    var el = record.AutomationElement;
                    if (el != null)
                    {
                        try { hwnd = el.Properties.NativeWindowHandle.ValueOrDefault; } catch { }
                    }
                }

                if (hwnd == IntPtr.Zero)
                    return (false, "Element is not associated with a window");

                var windowElement = _automation.FromHandle(hwnd);
                if (windowElement == null)
                    return (false, "Could not find window");

                if (!windowElement.Patterns.Window.IsSupported)
                    return (false, "Element does not support window operations");

                var windowPattern = windowElement.Patterns.Window.Pattern;

                switch (action.ToLowerInvariant())
                {
                    case "minimize":
                        windowPattern.SetWindowVisualState(WindowVisualState.Minimized);
                        return (true, "Window minimized");
                    case "maximize":
                        windowPattern.SetWindowVisualState(WindowVisualState.Maximized);
                        return (true, "Window maximized");
                    case "restore":
                        windowPattern.SetWindowVisualState(WindowVisualState.Normal);
                        return (true, "Window restored");
                    case "close":
                        windowPattern.Close();
                        return (true, "Window closed");
                    default:
                        return (false, $"Unknown window action: {action}. Use: minimize, maximize, restore, close");
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public List<string> GetListOfWindows()
        {
            try
            {
                var desktop = _automation.GetDesktop();
                var windows = desktop.FindAllChildren();
                var result = new List<string>();
                int index = 1;

                foreach (var window in windows)
                {
                    try
                    {
                        var name = window.Properties.Name.ValueOrDefault;
                        if (!string.IsNullOrWhiteSpace(name))
                        {
                            result.Add($"{index}. {name}");
                            index++;
                        }
                    }
                    catch { }
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Bridge.GetListOfWindows] Error: {ex.Message}");
                return new List<string>();
            }
        }

        #endregion Window Operations

        #region Scroll Direction

        public (bool success, string message) ScrollElementDirection(int elementId, string direction, int amount = 1)
        {
            try
            {
                switch (direction.ToLowerInvariant())
                {
                    case "up": return ScrollElement(elementId, -amount);
                    case "down": return ScrollElement(elementId, amount);
                    case "left": return ScrollElementHorizontal(elementId, -amount);
                    case "right": return ScrollElementHorizontal(elementId, amount);
                    case "pageup": return ScrollElement(elementId, -amount * 10);
                    case "pagedown": return ScrollElement(elementId, amount * 10);
                    default: return (false, $"Unknown scroll direction: {direction}");
                }
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        #endregion Scroll Direction

        #region Search

        public List<ElementRecord> Search(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return new List<ElementRecord>();

            var lower = searchText.ToLowerInvariant();
            return _elements.Values
                .Where(e =>
                    (e.Name?.ToLowerInvariant().Contains(lower) ?? false) ||
                    (e.AutomationId?.ToLowerInvariant().Contains(lower) ?? false))
                .ToList();
        }

        #endregion Search

        #region Tree Text

        public string GetElementsText(int maxDepth = 10)
        {
            var sb = new StringBuilder();
            // Find root elements (no parent or parent not in registry)
            var roots = _elements.Values
                .Where(e => !e.ParentId.HasValue || !_elements.ContainsKey(e.ParentId.Value))
                .OrderBy(e => e.Id)
                .ToList();

            foreach (var root in roots)
            {
                AppendElementText(sb, root, 0, maxDepth);
            }

            return sb.ToString();
        }

        private void AppendElementText(StringBuilder sb, ElementRecord element, int depth, int maxDepth)
        {
            if (depth > maxDepth) return;

            var indent = new string(' ', depth * 2);
            sb.AppendLine($"{indent}ID:{element.Id} {element.ControlType} '{element.Name}'");

            foreach (var childId in element.ChildIds)
            {
                if (_elements.TryGetValue(childId, out var child))
                {
                    AppendElementText(sb, child, depth + 1, maxDepth);
                }
            }
        }

        #endregion Tree Text

        #region Help

        private static string GetHelpText()
        {
            return @"Available Commands:

Basic Interactions:
  CLICK <elementId> - Click an element
  DOUBLE_CLICK <elementId> - Double-click an element
  RIGHT_CLICK <elementId> - Right-click an element
  MIDDLE_CLICK <elementId> - Middle-click an element
  CLICK_OFFSET <elementId> <x> <y> - Click at offset from element center
  CLICK_COORDS <elementId> - Fallback: click center of stored bounding rect using raw screen coordinates (brings window to front first)
  TYPE <elementId> <text> - Type text into an element
  SEND_KEYS <elementId> <keys> - Send keyboard input to element

Advanced Interactions:
  DRAG <elementId> <targetX> <targetY> - Drag element to position
  DRAG_TO_ELEMENT <sourceId> <targetId> - Drag from one element to another
  SELECT <elementId> - Select a list item
  SELECT_BY_TEXT <elementId> <text> - Select combo box item by text
  SELECT_BY_INDEX <elementId> <index> - Select combo box item by index
  SCROLL <elementId> <direction> [amount] - Scroll element
  SCROLL_HORIZONTAL <elementId> <amount> - Scroll element horizontally
  SCROLL_INTO_VIEW <elementId> - Scroll element into visible area
  SET_VALUE <elementId> <value> - Set element value
  SET_SLIDER <elementId> <value> - Set slider value
  TOGGLE <elementId> - Toggle checkbox or toggle button
  EXPAND <elementId> - Expand a tree node
  COLLAPSE <elementId> - Collapse a tree node

Element State:
  FOCUS <elementId> - Set focus to an element
  HOVER <elementId> - Hover mouse over an element
  HIGHLIGHT <elementId> - Highlight an element visually

Text:
  GET_TEXT <elementId> - Get text content from element

Capture & Describe:
  CAPTURE <elementId> - Capture screenshot of element to temp file
  CAPTURE_WINDOW <windowName> [id1] [id2] - Capture window screenshot
  DESCRIBE <elementId> [prompt] - Capture element and describe via LM Studio vision model

 Window Operations:
   WINDOW_ACTION <elementId> <action> - Window actions (minimize, maximize, restore, close)
   LIST_WINDOWS - List all available windows
 
 Information:
   GET_ELEMENT <elementId> - Get element details as JSON
   GET_TREE [maxDepth] - Get element tree as text
   SEARCH <searchText> - Search for elements by name
   HELP - Show this help message";
        }

        #endregion Help

        private readonly ConcurrentDictionary<int, AutomationElement> menuMap = new ConcurrentDictionary<int, AutomationElement>();
        private readonly List<Dictionary<string, object>> comprehensiveStateList = new List<Dictionary<string, object>>();
        private readonly InternalLogger Logger = new();
        private int _menuElementIdCounter = 0;
        private int? _currentParentMenuId = null; // Track current parent for hierarchy

        public TreeWalkerFilterLists _treeWalkerFilter = new TreeWalkerFilterLists();

        private AutomationType automationType;
        public AutomationType SelectedAutomationType => automationType;
        private string applicationVersion => Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? string.Empty;


        public string GetMenus(string targetApp, string? menuToClick = null, string? startingPoint = null, int depth = 0)
        {
            // Reset state for new scan
            _menuElementIdCounter = 0;
            _currentParentMenuId = null;
            comprehensiveStateList.Clear();
            menuMap.Clear();

            // If no menu item specified, use the fake name to traverse the structure
            if (string.IsNullOrEmpty(menuToClick)) menuToClick = "__NONEXISTENT_MENU_ITEM_FOR_TRAVERSAL__";


            var rootWindow = _automation.GetDesktop();

            // Exclude Apex windows to prevent finding our own application

            var targetWindow = FindBestWindowByName(rootWindow, targetApp, _treeWalkerFilter.ExclusionKeywords);

            if (targetWindow == null) { Debug.WriteLine($"Error: No Target Window"); return string.Empty; }

            var app = Application.Attach(targetWindow.Properties.ProcessId.Value);
            if (app == null) { Debug.WriteLine($"Error: No App"); return string.Empty; }

            var window = app.GetMainWindow(_automation);
            if (window == null) { Debug.WriteLine($"Error: No Window!"); return string.Empty; }

            window.SetForeground();

            var sb = new StringBuilder();

            // Wait.UntilInputIsProcessed(TimeSpan.FromMilliseconds(500)); // Give window time to become active

            var menuBar = window.FindAllDescendants(cf =>
                cf.ByControlType(ControlType.MenuBar))
                .Where(child => !child.AutomationId.Equals("SystemMenuBar"))
                .FirstOrDefault();

            if (menuBar == null) return $"Could not find the MenuBar.";

            FindMenuAndClick(window, menuBar, menuToClick, startingPoint, depth);

            // Build hierarchical structure from flat list
            var hierarchicalMenus = BuildMenuHierarchy(comprehensiveStateList);

            string stateJson = JsonSerializer.Serialize(hierarchicalMenus, new JsonSerializerOptions { WriteIndented = true });

            Debug.WriteLine($"\n--- COMPREHENSIVE MENU ELEMENT REPORT (JSON) ---\n");
            Debug.WriteLine($"\n{stateJson}\n");

            return stateJson;
        }

        private List<Dictionary<string, object>> BuildMenuHierarchy(List<Dictionary<string, object>> flatList)
        {
            // Create a dictionary for quick lookup by ElementId
            var menuDict = new Dictionary<int, Dictionary<string, object>>();

            foreach (var item in flatList)
            {
                if (item.ContainsKey("ElementId") && item["ElementId"] is int elementId)
                {
                    // Create a copy to avoid modifying original
                    var menuItem = new Dictionary<string, object>(item);
                    menuDict[elementId] = menuItem;
                }
            }

            // Build parent-child relationships
            var rootItems = new List<Dictionary<string, object>>();

            foreach (var item in menuDict.Values)
            {
                var parentId = item.ContainsKey("ParentId") ? item["ParentId"] : null;

                if (parentId == null)
                {
                    // Root level item (no parent)
                    rootItems.Add(item);
                }
                else if (parentId is int pid && menuDict.ContainsKey(pid))
                {
                    // Has a parent - add to parent's Children array
                    var parent = menuDict[pid];

                    if (!parent.ContainsKey("Children"))
                    {
                        parent["Children"] = new List<Dictionary<string, object>>();
                    }

                    if (parent["Children"] is List<Dictionary<string, object>> children)
                    {
                        children.Add(item);
                    }
                }
                else
                {
                    // Parent not found, treat as root
                    rootItems.Add(item);
                }
            }

            return rootItems;
        }

        public bool FindMenuAndClick(Window window, AutomationElement parentMenu, string itemName, string? startingPoint = null, int depth = 0)
        {
            var topLevelMenus = parentMenu.FindAllDescendants(cf => cf
                .ByControlType(ControlType.Menu).Or(cf.ByControlType(ControlType.MenuItem)))
                .Where(child => !child.ControlType.Equals(ControlType.Separator))
                .ToList();

            bool passedStart = string.IsNullOrEmpty(startingPoint);

            foreach (var menuItem in topLevelMenus)
            {
                #region Menu element storage and logging

                var name = menuItem.Properties.Name?.Value ?? "<No Name>";
                var controlType = menuItem.Properties.ControlType?.Value.ToString() ?? "<Unknown>";
                _menuElementIdCounter++;
                menuMap[_menuElementIdCounter] = menuItem;

                var tempViewModel = new ElementViewModel(automationElement: menuItem, null, 10, logger: Logger);

                var rawDetails = tempViewModel.LoadDetails();

                var elementData = tempViewModel.ParseAllDetails(rawDetails);

                if (elementData.ContainsKey("Identification") && elementData["Identification"] is Dictionary<string, object> identification)
                {
                    identification["Id"] = _menuElementIdCounter.ToString();
                }
                else
                {
                    elementData["Identification"] = new Dictionary<string, object>
                    {
                        ["Id"] = _menuElementIdCounter.ToString(),
                        ["Name"] = name,
                        ["ControlType"] = controlType
                    };
                }

                elementData["ElementId"] = _menuElementIdCounter;
                elementData["Depth"] = depth;
                elementData["ParentId"] = _currentParentMenuId;
                elementData["ParentType"] = parentMenu.Properties.ControlType?.Value.ToString() ?? "Unknown";
                comprehensiveStateList.Add(elementData);

                #endregion Menu element storage and logging

                // Handle starting point logic
                if (!passedStart)
                {
                    if (menuItem.Name == startingPoint)
                    {
                        passedStart = true;
                    }
                    else
                    {
                        continue;
                    }
                }

                if (menuItem.Name == itemName)
                {
                    menuItem.Click(false);
                    return true;  // ? STOP
                }

                try
                {
                    var newitem = FindElement(window, menuItem) ?? parentMenu;
                    if (newitem == null) continue;

                    if (ExpandCollapse(newitem))
                    {
                        var TempElement = FindElement(window, newitem, itemName);
                        if (TempElement != null)
                        {
                            TempElement.Click();
                            return true;  // ? STOP - don't check ExpandCollapse!
                        }

                        // Save current parent, set this menu as parent for children
                        var previousParentId = _currentParentMenuId;
                        _currentParentMenuId = _menuElementIdCounter;

                        if (FindMenuAndClick(window, FindElement(window, newitem) ?? parentMenu, itemName, null, depth + 1))
                        {  // ? Added depth + 1
                            _currentParentMenuId = previousParentId; // Restore parent
                            return true;  // ? STOP
                        }

                        // Restore parent after exploring children
                        _currentParentMenuId = previousParentId;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Could not expand menu: {ex.Message}");
                }
            }

            return false;
        }

        public AutomationElement? FindBestWindowByName(AutomationElement desktopElement, string targetName, HashSet<string>? excludedProcessNames = null)
        {
            // if (excludedProcessNames == null) excludedProcessNames = new List<string> { "devenv" };
            // // Keep your exclusion list here
            if (excludedProcessNames == null)
                excludedProcessNames = new HashSet<string>(); // Keep your exclusion list here

            // --- Step 1: Your powerful LINQ query to get all potential candidates ---
            var candidateWindows = desktopElement.FindAllDescendants(cf => cf.ByControlType(ControlType.Window).Or(cf.ByControlType(ControlType.Pane)))
                .Where(window =>
                {
                    var windowName = window.Properties.Name.ValueOrDefault ?? "";
                    string? processName = Process.GetProcessById(window.Properties.ProcessId.Value).ProcessName;

                    if (windowName == null || !windowName.Contains(targetName, StringComparison.OrdinalIgnoreCase) && !processName.Contains(targetName, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[Window NOT Found]Name: {windowName}");

                        return false;
                    }

                    try
                    {
                        //  if (!string.IsNullOrEmpty(processName))
                        Debug.WriteLine($"Process Name: {processName}");

                        // if (!string.IsNullOrEmpty(windowName))
                        Debug.WriteLine($"Window Name: {windowName}");

                        return !excludedProcessNames.Any(ex => ex.Equals(processName, StringComparison.OrdinalIgnoreCase));
                    }
                    catch
                    {
                        return false;
                    } // Exclude windows whose process has closed
                })
                .ToList();

            if (!candidateWindows.Any())
            {
                Debug.WriteLine($"[FindBestWindow] No candidate windows found for '{targetName}'.");
                return null;
            }

            bool IsWindowInForeground(AutomationElement element)
            {
                try
                {
                    var handle = element.Properties.NativeWindowHandle.Value;
                    return handle != IntPtr.Zero && handle == NativeMethods.GetForegroundWindow();
                }
                catch { return false; }
            }

            // --- Step 2: Intelligent Selection (The refinement) --- If multiple windows match (e.g.,
            // two Notepad windows are open), the best one is probably the one the user is currently
            // looking at.
            var foregroundWindow = candidateWindows.FirstOrDefault(IsWindowInForeground);

            if (foregroundWindow != null)
            {
                Debug.WriteLine($"[FindBestWindow] Found {candidateWindows.Count} candidates. Selecting the foreground window: '{foregroundWindow.Name}'.");
                return foregroundWindow;
            }

            // If none are in the foreground, fall back to the first one in the list. This is the safe
            // version of your .ToList()[0].
            Debug.WriteLine($"[FindBestWindow] Found {candidateWindows.Count} candidates, none in foreground. Defaulting to the first match: '{candidateWindows.First().Name}'.");
            return candidateWindows.First();
        }

        internal static class NativeMethods
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            internal static extern IntPtr GetForegroundWindow();

            [System.Runtime.InteropServices.DllImport("user32.dll")]
            [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
            internal static extern bool SetForegroundWindow(IntPtr hWnd);
        }


        public AutomationElement? FindElement(Window _window, AutomationElement? _automationElement = null, string? _overRideName = null, FlaUI.UIA3.UIA3Automation? _automation = null)
        {
            if (_window == null) return null;

            if (_automation == null) _automation = new FlaUI.UIA3.UIA3Automation();

            var cf = new ConditionFactory(_automation.PropertyLibrary);

            if (!string.IsNullOrEmpty(_overRideName)) return _window.FindFirstDescendant(cf.ByName(_overRideName));

            if (_automationElement == null) return null;

            return _window.FindFirstDescendant(!string.IsNullOrEmpty(_automationElement.Name)
                ? cf.ByName(_automationElement.Name)
                : cf.ByAutomationId(_automationElement.AutomationId));
        }

        private AutomationElement? FindDocumentInElement(AutomationElement root, int maxDepth = 3)
        {
            if (maxDepth <= 0) return null;

            // Check if this element is a Document
            if (root.ControlType == FlaUI.Core.Definitions.ControlType.Document)
            {
                return root;
            }

            var docElement = root.FindFirstDescendant(e => e.ByControlType(ControlType.Document));

            return docElement;
        }

        public bool IsChromeBased(AutomationElement? element)
        {
            if (element == null) return false;
            try
            {
                var current = element;
                for (int i = 0; i < 5 && current != null; i++)
                {
                    var className = current.ClassName;
                    if (!string.IsNullOrEmpty(className) &&
                        (className.Contains("Chrome_WidgetWin", StringComparison.OrdinalIgnoreCase) ||
                         className.Contains("Electron", StringComparison.OrdinalIgnoreCase) ||
                         className.Contains("CEF", StringComparison.OrdinalIgnoreCase)))
                    {
                        Debug.WriteLine($"[IsChromeBased] Detected Chrome-based app: {className}");
                        return true;
                    }
                    try
                    {
                        current = current.Parent;
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return false;
        }

        private bool ExpandCollapse(AutomationElement automationElement)
        {
            if (automationElement.Patterns.ExpandCollapse.IsSupported && automationElement.Patterns.ExpandCollapse.TryGetPattern(out var _expandPattern))
            {
                if (_expandPattern.ExpandCollapseState == ExpandCollapseState.Collapsed)
                {
                    _expandPattern.Expand();
                    Wait.UntilInputIsProcessed();
                }
                else _expandPattern.Collapse();
                return true;
            }
            return false;
        }













        public sealed class ElementParameters
        {
            /// <summary>
            /// Gets or sets the parent ElementViewModel.
            /// </summary>
            public ElementViewModel? Parent { get; set; } = null;

            /// <summary>
            /// Gets or sets the shared automation base instance (UIA2 or UIA3).
            /// </summary>
            /// <remarks>This should be shared across all ElementViewModel instances to avoid creating multiple automation instances.</remarks>
            public AutomationBase? AutomationBase { get; set; }

            /// <summary>
            /// Gets or sets the UI Automation element associated with this instance.
            /// </summary>
            /// <remarks>This property may be null if no automation element is available or has not been set. Use this
            /// property to interact with or retrieve information about the underlying UI element for automation
            /// tasks.</remarks>
            public AutomationElement? AutomationElement { get; set; }

            /// <summary>
            /// Gets or sets the tree walker used for UI traversal.
            /// </summary>
            public FilteredTreeWalker? TreeWalker { get; set; }

            /// <summary>
            /// Gets or sets the logger for logging purposes.
            /// </summary>
            public ILogger? Logger { get; set; }




            /// <summary>
            /// Gets or sets the depth level for the current context.
            /// </summary>
            public int Depth { get; set; } = 0;

            /// <summary>
            /// Gets or sets the maximum depth to traverse in the UI tree.
            /// </summary>
            public int MaxDepth { get; set; } = 25;

            /// <summary>
            /// Gets or sets a value indicating whether to show the default debug output.
            /// </summary>
            public bool ShowDefaultDebugOutput { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating whether to show tracking output.
            /// </summary>
            public bool ShowTrackingOutput { get; set; } = true;

            /// <summary>
            /// Gets or sets a value indicating whether to load child elements.
            /// </summary>
            public bool LoadTheChildren { get; set; } = true;

            /// <summary>
            /// Indicates whether details and patterns should be loaded.
            /// </summary>
            public bool LoadTheDetails { get; set; } = true;

            /// <summary>
            /// Indicates whether to initialize the find condition.
            /// </summary>
            public bool LoadFindConditions { get; set; } = true;





        }
         

    }

}