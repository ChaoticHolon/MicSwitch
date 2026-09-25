using System.Windows;
using MicSwitch.Settings;

namespace MicSwitch.Views;

internal static class WindowPlacement
{
    /// <summary>False when saved bounds are off every monitor (e.g. a display was disconnected).</summary>
    public static bool IsOnScreen(WindowBounds bounds)
    {
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        return bounds.Width > 0 && bounds.Height > 0 && screen.IntersectsWith(new Rect(bounds.Left, bounds.Top, bounds.Width, bounds.Height));
    }
}
