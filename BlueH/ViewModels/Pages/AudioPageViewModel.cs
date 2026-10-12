using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels.Pages;

public sealed class AudioPageViewModel : PageViewModel
{
    public AudioPageViewModel(EndpointsViewModel outputs)
    {
        Outputs = outputs;
    }

    public EndpointsViewModel Outputs { get; }

    public override string Title => AppResources.EndpointsHeader;
}
