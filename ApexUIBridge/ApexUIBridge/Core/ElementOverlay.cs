using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using FlaUI.Core.Overlay;

namespace ApexUIBridge.Core;

/// <summary>
/// Draws a coloured, semi-transparent visual overlay rectangle (border or fill)
/// over an arbitrary screen region using top-most, transparent WinForms windows
/// positioned via <c>SetWindowPos</c>.
///
/// <para>Two factory modes are supported (configured by
/// <see cref="ElementOverlayConfiguration.RectangleFactory"/>):</para>
/// <list type="bullet">
///   <item><b>Border</b> (<see cref="BoundRectangleFactory"/>) — four thin strips
///       that form a rectangle border around the target bounds.</item>
///   <item><b>Fill</b> (<see cref="FillRectangleFactory"/>) — a single rectangle
///       covering the full target area with configurable opacity.</item>
/// </list>
///
/// <para>Instances are created on demand by the factory delegates stored in
/// <see cref="Settings.FlaUiAppOptions"/> (HoverOverlay, SelectionOverlay,
/// PickOverlay). Callers must dispose the overlay to hide and destroy the
/// underlying windows.</para>
/// </summary>
public class ElementOverlay : IDisposable {

    private OverlayRectangleForm[] _overlayRectangleFormList = [];

    public ElementOverlay(ElementOverlayConfiguration configuration) {
        Configuration = configuration;

    }

    public ElementOverlayConfiguration Configuration { get; }

    public void Dispose() {
        Hide();
    }

    public static Func<ElementOverlayConfiguration, Rectangle, Rectangle[]> GetRectangleFactory(string mode) {
        return mode.ToLower() switch {
            "fill" => FillRectangleFactory,
            "border" => BoundRectangleFactory,
            _ => BoundRectangleFactory
        };
    }

    public static Rectangle[] FillRectangleFactory(ElementOverlayConfiguration config, Rectangle rectangle) {
        return [
            new Rectangle(rectangle.X - (int)config.Margin.Left,
                          rectangle.Y - (int)config.Margin.Top,
                          rectangle.Width + (int)config.Margin.Right,
                          rectangle.Height + (int)config.Margin.Bottom)
        ];
    }

    public static Rectangle[] BoundRectangleFactory(ElementOverlayConfiguration config, Rectangle rectangle) {
        return [
            new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, config.Size, rectangle.Height + (int)config.Margin.Bottom),
            new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, rectangle.Width + (int)config.Margin.Right, config.Size),
            new Rectangle(rectangle.X + rectangle.Width - config.Size + (int)config.Margin.Left, rectangle.Y - (int)config.Margin.Top, config.Size, rectangle.Height + (int)config.Margin.Bottom),
            new Rectangle(rectangle.X - (int)config.Margin.Left, rectangle.Y + rectangle.Height - config.Size + (int)config.Margin.Right, rectangle.Width + (int)config.Margin.Right, config.Size)
        ];
    }

    public void Hide() {
        foreach (OverlayRectangleForm overlayRectangleForm in _overlayRectangleFormList) {
            overlayRectangleForm.Hide();
            overlayRectangleForm.Close();
            overlayRectangleForm.Dispose();
        }
        _overlayRectangleFormList = [];
    }

    public void Show(Rectangle rectangle) {
        Color color1 = Color.FromArgb(255, Configuration.Color.R, Configuration.Color.G, Configuration.Color.B);
        Rectangle[] rectangles = Configuration.RectangleFactory?.Invoke(Configuration, rectangle) ?? BoundRectangleFactory(Configuration, rectangle);

        List<OverlayRectangleForm> rectangleForms = [];

        foreach (Rectangle rectangle1 in rectangles) {
            OverlayRectangleForm overlayRectangleForm1 = new ();
            overlayRectangleForm1.BackColor = color1;
            overlayRectangleForm1.Opacity = Configuration.Color.A / 255d;
            OverlayRectangleForm overlayRectangleForm2 = overlayRectangleForm1;
            rectangleForms.Add(overlayRectangleForm2);
            SetWindowPos(overlayRectangleForm2.Handle, new IntPtr(-1), rectangle1.X, rectangle1.Y, rectangle1.Width, rectangle1.Height, 16 /*0x10*/);
            ShowWindow(overlayRectangleForm2.Handle, 8);
        }

        _overlayRectangleFormList = rectangleForms.ToArray();
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hwndAfter,
        int x,
        int y,
        int width,
        int height,
        int flags);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}

public record ElementOverlayConfiguration(int Size, Padding Margin, Color Color, Func<ElementOverlayConfiguration, Rectangle, Rectangle[]>? RectangleFactory = null);
