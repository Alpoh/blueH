namespace BlueH.Services;

public interface IUiScheduler
{
    void Post(Action action);
}
