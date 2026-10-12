using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels.Pages;

public sealed class DiagnosticsPageViewModel : PageViewModel
{
    public DiagnosticsPageViewModel(DiagnosticsViewModel diagnostics)
    {
        Diagnostics = diagnostics;
    }

    public DiagnosticsViewModel Diagnostics { get; }

    public override string Title => AppResources.DiagnosticsHeader;
}
