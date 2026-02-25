using System.Windows.Forms;
using FlaUI.Core;
using ApexUIBridge.Core.Logger;
using AutomationElement = FlaUI.Core.AutomationElements.AutomationElement;
using Mouse = FlaUI.Core.Input.Mouse;
using Point = System.Drawing.Point;

namespace ApexUIBridge.Core;

/// <summary>
/// Singleton static manager that polls the cursor position every 300 ms and,
/// when the <c>Ctrl</c> key is held, resolves the
/// <see cref="FlaUI.Core.AutomationElements.AutomationElement"/> under the cursor
/// via <see cref="FlaUI.Core.AutomationBase.FromPoint"/>.
///
/// <para>Multiple <see cref="ProcessViewModel"/> instances register listener
/// callbacks via <see cref="AddListener"/>. When the hovered element changes,
/// all callbacks with an entry in <see cref="EnabledListeners"/> are notified,
/// and an <see cref="ElementOverlay"/> is drawn around the element's bounding
/// rectangle.</para>
///
/// <para>Hover mode is enabled/disabled per-window-handle to avoid conflicts when
/// multiple processes are inspected simultaneously.</para>
/// </summary>
public static class HoverManager {
    private static Func<ElementOverlay?>? _elementOverlayFunc;
    private static AutomationBase? _automationBase;
    private static AutomationElement? _hoveredElement;
    private static ElementOverlay? _elementOverlay;

    private static readonly List<KeyValuePair<IntPtr, Action<AutomationElement?>>> Listeners = [];
    private static readonly HashSet<IntPtr> EnabledListeners = [];
    private static readonly object LockObject = new();

    static HoverManager() {
        System.Windows.Forms.Timer timer = new() { Interval = 300 };
        timer.Tick += (_, _) => Refresh();
        timer.Start();
    }

    private static void Refresh() {
        if (EnabledListeners.Count == 0) {
            _elementOverlay?.Dispose();
            _hoveredElement = null;
            return;
        }

        if ((Control.ModifierKeys & Keys.Control) == Keys.Control) {
            Point screenPos = Mouse.Position;
            try {
                AutomationElement? automationElement = _automationBase?.FromPoint(screenPos);
                if (automationElement == null || automationElement.Properties.ProcessId == Environment.ProcessId) {
                    _elementOverlay?.Dispose();
                    _hoveredElement = null;
                    return;
                }

                if (_hoveredElement == null || !automationElement.Equals(_hoveredElement)) {
                    _elementOverlay?.Dispose();
                    _hoveredElement = automationElement;

                    foreach (KeyValuePair<IntPtr, Action<AutomationElement?>> listener in Listeners) {
                        listener.Value?.Invoke(automationElement);
                    }

                    if (_elementOverlayFunc != null && EnabledListeners.Count > 0) {
                        _elementOverlay = _elementOverlayFunc();
                        _elementOverlay?.Show(automationElement.Properties.BoundingRectangle.Value);
                    }
                }
            } catch {
            }
        }
    }

    public static void AddListener(IntPtr id, Action<AutomationElement?> onElementHovered) {
        lock (LockObject) {
            Listeners.Add(new KeyValuePair<IntPtr, Action<AutomationElement?>>(id, onElementHovered));
        }
    }

    public static void RemoveListener(IntPtr id) {
        lock (LockObject) {
            int idx = Listeners.FindIndex(x => x.Key == id);
            if (idx >= 0) {
                Listeners.RemoveAt(idx);
            }
        }
    }

    public static void Enable(IntPtr intPtr) {
        lock (LockObject) {
            EnabledListeners.Add(intPtr);
        }
    }

    public static void Disable(IntPtr intPtr) {
        lock (LockObject) {
            EnabledListeners.Remove(intPtr);
        }
    }

    public static void Initialize(AutomationBase? automation, Func<ElementOverlay?> elementOverlayFunc, ILogger? logger) {
        _automationBase = automation;
        _elementOverlayFunc = elementOverlayFunc;
    }
}
