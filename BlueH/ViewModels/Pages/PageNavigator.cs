using System.Windows.Input;

namespace BlueH.ViewModels.Pages;

public sealed class PageNavigator : ViewModelBase
{
    private readonly Stack<PageViewModel> _history = new();
    private readonly AsyncRelayCommand _goBackCommand;

    private PageViewModel _currentPage;

    public PageNavigator(PageViewModel startPage)
    {
        _currentPage = startPage;
        _goBackCommand = new AsyncRelayCommand(_ => GoBackAsync(), _ => CanGoBack);
    }

    public PageViewModel CurrentPage
    {
        get => _currentPage;
        private set
        {
            if (SetField(ref _currentPage, value))
            {
                OnPropertyChanged(nameof(CanGoBack));
                _goBackCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanGoBack => _history.Count > 0;

    public ICommand GoBackCommand => _goBackCommand;

    public void Open(PageViewModel page)
    {
        if (ReferenceEquals(page, _currentPage))
        {
            return;
        }

        _history.Push(_currentPage);
        CurrentPage = page;
    }

    public void ShowSection(PageViewModel sectionPage)
    {
        _history.Clear();

        if (ReferenceEquals(sectionPage, _currentPage))
        {
            OnPropertyChanged(nameof(CanGoBack));
            _goBackCommand.RaiseCanExecuteChanged();

            return;
        }

        CurrentPage = sectionPage;
    }

    public void GoBack()
    {
        if (_history.Count > 0)
        {
            CurrentPage = _history.Pop();
        }
    }

    private Task GoBackAsync()
    {
        GoBack();

        return Task.CompletedTask;
    }
}
