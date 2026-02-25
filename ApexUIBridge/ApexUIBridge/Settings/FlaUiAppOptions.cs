using System.Drawing;
using System.Windows.Forms;
using ApexUIBridge.Core;

namespace ApexUIBridge.Settings;

public class FlaUiAppOptions {
    public Func<ElementOverlay?> HoverOverlay { get; set; } = () => null;
    public Func<ElementOverlay?> SelectionOverlay { get; set; } = () => null;
    public Func<ElementOverlay?> PickOverlay { get; set; } = () => null;

    public Func<ElementOverlay> DefaultOverlay { get; set; } = () => new ElementOverlay(new ElementOverlayConfiguration(2,
                                                                                                                        new Padding(0),
                                                                                                                        Color.Red,
                                                                                                                        ElementOverlay.BoundRectangleFactory));
}
