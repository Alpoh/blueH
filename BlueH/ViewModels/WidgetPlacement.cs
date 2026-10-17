using System.Windows;
using BlueH.Models;

namespace BlueH.ViewModels;

public static class WidgetPlacement
{
    public const double VisibleGrip = 48;

    public static WidgetPosition Place(WidgetPosition? remembered, WidgetPosition fallback, Rect screen)
    {
        if (remembered is not { } position || !double.IsFinite(position.Left) || !double.IsFinite(position.Top))
        {
            return fallback;
        }

        return new WidgetPosition(
            Math.Clamp(position.Left, screen.Left, screen.Right - VisibleGrip),
            Math.Clamp(position.Top, screen.Top, screen.Bottom - VisibleGrip));
    }
}
