using Rebex.Net;

namespace ConnectionThrottlingProblem;

public sealed class RebexSftpClientFactory
{
    public RebexSftpClientFactory(string host, int port, string username, string password)
    {
        
    }
    
    public IFtpClient Create()
    {
        var sftp = new Sftp();
        return new RebexSftpClient(sftp);
    }
}