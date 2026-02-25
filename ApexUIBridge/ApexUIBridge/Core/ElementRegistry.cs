using System.Collections.Concurrent;
using FlaUI.Core.AutomationElements;
using ApexUIBridge.ViewModels;

namespace ApexUIBridge.Core;

/// <summary>
/// Global registry that maps ElementId to ElementViewModel.
/// Allows looking up any element by its deterministic ID.
/// </summary>
public static class ElementRegistry {
    private static readonly ConcurrentDictionary<int, WeakReference<ElementViewModel>> _elements = new();

    /// <summary>
    /// Registers an element in the registry. If an element with the same ID
    /// already exists, it is replaced with the newer instance.
    /// </summary>
    public static void Register(ElementViewModel element) {
        if (element.ElementId != 0) {
            _elements[element.ElementId] = new WeakReference<ElementViewModel>(element);
        }
    }

    /// <summary>
    /// Looks up an ElementViewModel by its deterministic ElementId.
    /// Returns null if the element is not found or has been garbage collected.
    /// </summary>
    public static ElementViewModel? FindById(int elementId) {
        if (_elements.TryGetValue(elementId, out var weakRef) && weakRef.TryGetTarget(out var vm)) {
            return vm;
        }
        _elements.TryRemove(elementId, out _);
        return null;
    }

    /// <summary>
    /// Looks up an AutomationElement by its deterministic ElementId.
    /// Returns null if the element is not found or has been garbage collected.
    /// </summary>
    public static AutomationElement? FindAutomationElementById(int elementId) {
        return FindById(elementId)?.AutomationElement;
    }

    /// <summary>
    /// Removes an element from the registry.
    /// </summary>
    public static void Unregister(int elementId) {
        _elements.TryRemove(elementId, out _);
    }

    /// <summary>
    /// Clears all entries from the registry.
    /// </summary>
    public static void Clear() {
        _elements.Clear();
    }

    /// <summary>
    /// Gets the number of registered elements.
    /// </summary>
    public static int Count => _elements.Count;
}
