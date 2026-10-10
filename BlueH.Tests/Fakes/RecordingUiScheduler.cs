using BlueH.Services;

namespace BlueH.Tests.Fakes;

public sealed class RecordingUiScheduler : IUiScheduler
{
    private readonly List<Action> _pendingWork = [];

    public int PendingWorkCount => _pendingWork.Count;

    public void Post(Action action)
    {
        _pendingWork.Add(action);
    }

    public void RunPendingWork()
    {
        List<Action> work = [.. _pendingWork];
        _pendingWork.Clear();

        foreach (Action action in work)
        {
            action();
        }
    }
}
