using Renci.SshNet;

namespace ConnectionThrottlingProblem
{
    /// <summary>
    /// This class should implement "thread-safe" access to SftpClient.
    /// At the moment it simply implements the methods necessary for testing purposes.
    /// </summary>
    public sealed class SshNetSftpClient : IFtpClient
    {
        private readonly SftpClient _client;

        public SshNetSftpClient(SftpClient client)
        {
            ArgumentNullException.ThrowIfNull(client);
            
            _client = client;
        }

        public bool IsOpen => _client.IsConnected;

        public async Task OpenAsync(CancellationToken cancellationToken = default)
        {
            if (_client.IsConnected)
                return;
                
            await _client.ConnectAsync(cancellationToken);
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            
            if (!_client.IsConnected)
                return Task.CompletedTask;
            
            _client.Disconnect();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _client.Disconnect();
            _client.Dispose();
        }

        public List<FileSystemItem> ListDirectory(string path)
        {
            var sftpFiles = _client.ListDirectory(path);
            return sftpFiles.Select(x => new FileSystemItem(x.Name, x.FullName, x.Length, x.IsDirectory)).ToList();
        }
        
        public async Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken = default)
        {
            await Task.Delay(2000, cancellationToken);
            
            using var output = new MemoryStream();
            await DownloadFileAsync(path, output, cancellationToken);
            output.Position = 0;
            using var reader = new StreamReader(output);
            return await reader.ReadToEndAsync(cancellationToken);
        }
        
        private async Task DownloadFileAsync(string path, Stream output, CancellationToken cancellationToken)
        {
            await using var remoteStream = await _client.OpenAsync(path, FileMode.Open, FileAccess.Read, cancellationToken);
            await remoteStream.CopyToAsync(output, cancellationToken);
        }
        
        public async Task WriteAllTextAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            using var input = new MemoryStream();
            await using var writer = new StreamWriter(input);
            await writer.WriteAsync(content);
            await writer.FlushAsync(cancellationToken);
            input.Position = 0;
            await UploadFileAsync(input, path, FileMode.OpenOrCreate, cancellationToken);
        }
        
        private async Task UploadFileAsync(Stream input, string path, FileMode fileMode, CancellationToken cancellationToken)
        {
            await using var remoteStream = await _client.OpenAsync(path, fileMode, FileAccess.Write, cancellationToken);
            await input.CopyToAsync(remoteStream, cancellationToken);
        }

        public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            await _client.DeleteAsync(path, cancellationToken);
        }
    }
}