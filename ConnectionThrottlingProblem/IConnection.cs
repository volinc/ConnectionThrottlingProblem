namespace ConnectionThrottlingProblem
{
    public interface IConnection
    {
        bool IsOpen { get; }
        Task OpenAsync(CancellationToken cancellationToken = default);
        Task CloseAsync(CancellationToken cancellationToken = default);
    }
}