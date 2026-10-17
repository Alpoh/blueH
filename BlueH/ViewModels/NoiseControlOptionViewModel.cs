using System.Windows.Input;
using BlueH.Models;

namespace BlueH.ViewModels;

public sealed class NoiseControlOptionViewModel : ViewModelBase
{
    private readonly AsyncRelayCommand _selectCommand;

    private bool _isSelected;

    public NoiseControlOptionViewModel(
        NoiseControlMode mode,
        string label,
        Func<NoiseControlMode, Task> select,
        Func<bool> canSelect)
    {
        Mode = mode;
        Label = label;
        _selectCommand = new AsyncRelayCommand(_ => select(mode), _ => canSelect());
    }

    public NoiseControlMode Mode { get; }

    public string Label { get; }

    public bool IsSelected => _isSelected;

    public AsyncRelayCommand SelectCommand => _selectCommand;

    public void ShowSelection(bool isSelected)
    {
        _isSelected = isSelected;
        OnPropertyChanged(nameof(IsSelected));
    }

    public void RaiseCanExecuteChanged()
    {
        _selectCommand.RaiseCanExecuteChanged();
    }
}
