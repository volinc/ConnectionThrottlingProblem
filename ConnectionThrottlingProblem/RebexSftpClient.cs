using Rebex.Net;

namespace ConnectionThrottlingProblem;

public sealed class RebexSftpClient : IFtpClient
{
    private readonly Sftp _sftp;

    public RebexSftpClient(Sftp sftp)
    {
        _sftp = sftp;
    }
    
    public void Dispose()
    {
        _sftp.Dispose();
    }

    public bool IsOpen => _sftp.IsConnected;
    
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (_sftp.IsConnected)
            return;

        //await _sftp.ConnectAsync();
        await Task.Delay(0, cancellationToken);
    }

    public Task CloseAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public List<FileSystemItem> ListDirectory(string path)
    {
        throw new NotImplementedException();
    }

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}