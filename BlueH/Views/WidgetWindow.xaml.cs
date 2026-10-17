using System.Windows;
using System.Windows.Input;
using BlueH.Models;
using BlueH.ViewModels;

namespace BlueH.Views;

public partial class WidgetWindow : Window
{
    private const int DoubleClick = 2;

    public WidgetWindow()
    {
        InitializeComponent();
    }

    private void OnDragAreaMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not WidgetViewModel widget)
        {
            return;
        }

        if (e.ClickCount == DoubleClick)
        {
            widget.DisplayMode.SwitchToWindowCommand.Execute(null);

            return;
        }

        DragMove();
        widget.RememberPosition(new WidgetPosition(Left, Top));
    }
}
