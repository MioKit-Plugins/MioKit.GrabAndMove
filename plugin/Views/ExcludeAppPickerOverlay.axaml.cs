using Avalonia;
using Avalonia.Controls;

namespace GrabAndMove.Views;

public partial class ExcludeAppPickerOverlay : Window
{
    public ExcludeAppPickerOverlay()
    {
        InitializeComponent();
    }

    public void MoveTo(int x, int y, int width, int height, string? title, string? processName)
    {
        Position = new PixelPoint(x, y);
        var scale = Math.Max(RenderScaling, 1);
        Width = Math.Max(width / scale, 1);
        Height = Math.Max(height / scale, 1);

        var label = !string.IsNullOrWhiteSpace(processName)
            ? processName
            : string.IsNullOrWhiteSpace(title) ? "未命名窗口" : title;
        CaptionTitle.Text = label!;
        CaptionHost.IsVisible = Width >= 120 && Height >= 48;

        if (!IsVisible)
            Show();
    }
}
