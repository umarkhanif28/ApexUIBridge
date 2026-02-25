using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Globalization;
using ApexUIBridge.Core;
using ApexUIBridge.Core.Logger;
using ApexUIBridge.Settings;

namespace ApexUIBridge;

/// <summary>
/// Static application container that holds the DI service provider, the global
/// internal logger, and the runtime overlay option delegates used throughout the
/// application. <see cref="ApplyAppOption"/> converts JSON-backed
/// <see cref="Settings.FlaUiAppSettings"/> into <see cref="ElementOverlay"/>
/// factory functions stored in <see cref="FlaUiAppOptions"/>.
/// </summary>
public static class App {
    /// <summary>The application-wide dependency injection service provider.</summary>
    public static IServiceProvider Services { get; set; } = null!;

    /// <summary>
    /// Runtime overlay option factories (hover, selection, pick). Populated by
    /// <see cref="ApplyAppOption"/> on startup.
    /// </summary>
    public static FlaUiAppOptions FlaUiAppOptions { get; } = new();

    /// <summary>Application-wide logger instance.</summary>
    public static InternalLogger Logger { get; } = new();

    /// <summary>
    /// Converts serialised <see cref="Settings.FlaUiAppSettings"/> into live
    /// <see cref="ElementOverlay"/> factory delegates and stores them in
    /// <see cref="FlaUiAppOptions"/>. Falls back to a default overlay when a
    /// section is null or missing.
    /// </summary>
    public static void ApplyAppOption(FlaUiAppSettings settings) {
        FlaUiAppSettings clone = settings.Clone() as FlaUiAppSettings ?? settings;

        FlaUiAppOptions.HoverOverlay = BuildOverlay(clone.HoverOverlay) ?? FlaUiAppOptions.DefaultOverlay;
        FlaUiAppOptions.SelectionOverlay = BuildOverlay(clone.SelectionOverlay) ?? FlaUiAppOptions.DefaultOverlay;
        FlaUiAppOptions.PickOverlay = BuildOverlay(clone.PickOverlay) ?? FlaUiAppOptions.DefaultOverlay;
    }

    private static Func<ElementOverlay>? BuildOverlay(OverlaySettings? settings) {
        if (settings == null) {
            return null;
        }

        Padding margin = ParseMargin(settings.Margin);
        return () => new ElementOverlay(new ElementOverlayConfiguration(settings.Size,
                                                                         margin,
                                                                         ColorTranslator.FromHtml(settings.OverlayColor),
                                                                         ElementOverlay.GetRectangleFactory(settings.OverlayMode)));
    }

    private static Padding ParseMargin(string margin) {
        string[] values = margin.Split(',').Select(v => v.Trim()).ToArray();

        if (values.Length == 1 && int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int all)) {
            return new Padding(all);
        }

        if (values.Length == 4
            && int.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int left)
            && int.TryParse(values[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int top)
            && int.TryParse(values[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int right)
            && int.TryParse(values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int bottom)) {
            return new Padding(left, top, right, bottom);
        }

        return new Padding(0);
    }
}
