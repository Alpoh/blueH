using System.Windows;
using System.Windows.Controls;
using BlueH.ViewModels;

namespace BlueH.Views;

public partial class EndpointsView : UserControl
{
    public EndpointsView()
    {
        InitializeComponent();
    }

    private void VolumeInputLostFocus(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement { DataContext: AudioEndpointViewModel endpoint })
        {
            endpoint.CancelVolumeEditCommand.Execute(null);
        }
    }
}
