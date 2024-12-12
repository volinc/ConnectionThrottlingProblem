namespace ConnectionThrottlingProblem
{
    public interface IConnection : IDisposable
    {
        bool IsOpen { get; }
        Task OpenAsync(CancellationToken cancellationToken = default);
        Task CloseAsync(CancellationToken cancellationToken = default);
    }
}