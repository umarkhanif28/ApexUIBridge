using FlaUI.Core.AutomationElements;

namespace ApexUIBridge.Core;

/// <summary>
/// Result of an element search operation with match scoring.
/// </summary>
public class ElementFinderResult
{
    /// <summary>
    /// The matched automation element.
    /// </summary>
    public AutomationElement Element { get; init; } = null!;

    /// <summary>
    /// Match score (0.0 to 1.0, higher is better).
    /// 1.0 = exact match on AutomationId, Name, or ElementId.
    /// 0.8 = contains match. Lower values = Levenshtein similarity.
    /// </summary>
    public double Score { get; init; }

    /// <summary>
    /// Which field produced the best match (AutomationId, Name, ElementId, or Filter).
    /// </summary>
    public string MatchedField { get; init; } = string.Empty;

    public override string ToString()
    {
        var name = Element.Properties.Name.ValueOrDefault ?? "";
        var automationId = Element.Properties.AutomationId.ValueOrDefault ?? "";
        var controlType = Element.Properties.ControlType.ValueOrDefault;
        return $"[{Score:F2}] Name='{name}' AutomationId='{automationId}' ControlType={controlType} (matched on {MatchedField})";
    }
}
