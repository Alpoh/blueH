using System.Windows.Threading;

namespace BlueH.Services;

public sealed class DispatcherUiScheduler : IUiScheduler
{
    private readonly Dispatcher _dispatcher;

    public DispatcherUiScheduler(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public void Post(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return;
        }

        _ = _dispatcher.InvokeAsync(action);
    }
}
