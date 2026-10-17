using System.Windows.Input;
using AppResources = BlueH.Properties.Resources;

namespace BlueH.ViewModels;

public sealed class DisplayModeViewModel : ViewModelBase
{
    private bool _isWidgetMode;

    public DisplayModeViewModel()
    {
        SwitchToWidgetCommand = new RelayCommand(_ => IsWidgetMode = true, _ => !_isWidgetMode);
        SwitchToWindowCommand = new RelayCommand(_ => IsWidgetMode = false, _ => _isWidgetMode);
        ExitCommand = new RelayCommand(_ => ExitRequested?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? ExitRequested;

    public bool IsWidgetMode
    {
        get => _isWidgetMode;
        private set => SetField(ref _isWidgetMode, value);
    }

    public ICommand SwitchToWidgetCommand { get; }

    public ICommand SwitchToWindowCommand { get; }

    public ICommand ExitCommand { get; }

    public string SwitchToWidgetLabel => AppResources.SwitchToWidgetLabel;

    public string SwitchToWindowLabel => AppResources.SwitchToWindowLabel;

    public string ExitLabel => AppResources.ExitLabel;
}
