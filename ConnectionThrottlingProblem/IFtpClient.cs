namespace ConnectionThrottlingProblem;

public interface IFtpClient : IConnection
{
    List<FileSystemItem> ListDirectory(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default);
    Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default);
    Task DeleteAsync(string path, CancellationToken cancellationToken = default);
}