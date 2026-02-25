using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.EventHandlers;

namespace ApexUIBridge.Core;

/// <summary>
/// Subscribes to the UIA focus-changed event (<c>RegisterFocusChangedEvent</c>)
/// and invokes a callback whenever focus moves to an element that belongs to
/// a different process than ApexUIBridge itself.
///
/// <para>Started and stopped by <see cref="ViewModels.ProcessViewModel.SetMode"/>
/// when the user toggles "Focus Tracking" mode in the UI. The callback walks
/// the tree path back to the root to expand and select the newly focused element
/// in the UI tree.</para>
/// </summary>
public class FocusTrackingMode(AutomationBase? automation, Action<AutomationElement> onFocusChangedAction) {
    private AutomationElement? _currentFocusedElement;
    private FocusChangedEventHandlerBase? _eventHandler;

    public void Start() {
        Task.Factory.StartNew(() => _eventHandler = automation?.RegisterFocusChangedEvent(OnFocusChanged));
    }

    public void Stop() {
        if (_eventHandler != null) {
            automation?.UnregisterFocusChangedEvent(_eventHandler);
        }
    }

    private void OnFocusChanged(AutomationElement? automationElement) {
        if (automationElement?.Properties.ProcessId == Environment.ProcessId) {
            return;
        }

        if (!Equals(_currentFocusedElement, automationElement) && automationElement != null) {
            _currentFocusedElement = automationElement;
            onFocusChangedAction(automationElement);
        }
    }
}
