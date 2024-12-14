using Renci.SshNet;

namespace ConnectionThrottlingProblem
{
    public sealed class SshNetSftpClientFactory
    {
        private readonly ConnectionInfo _connectionInfo;
        
        public SshNetSftpClientFactory(string host, int port, string username, string password)
        {
            ArgumentNullException.ThrowIfNull(host);
            if (port < 0)
                throw new ArgumentException("Argument cannot be less than 0", nameof(port));
            
            ArgumentNullException.ThrowIfNull(username);
            ArgumentNullException.ThrowIfNull(password);
            
            var keyboardInteractiveAuth = new KeyboardInteractiveAuthenticationMethod(username);
            var passwordAuth = new PasswordAuthenticationMethod(username, password);

            keyboardInteractiveAuth.AuthenticationPrompt += (sender, authPromptEventArgs) =>
            {
                foreach (var prompt in authPromptEventArgs.Prompts)
                {
                    if (prompt.Request.IndexOf("Password:", StringComparison.InvariantCultureIgnoreCase) != -1)
                        prompt.Response = password;
                }
            };

            _connectionInfo = new ConnectionInfo(host, port, username, passwordAuth, keyboardInteractiveAuth);
        }
        
        public IFtpClient Create()
        {
            var client = new SftpClient(_connectionInfo);
            client.KeepAliveInterval = TimeSpan.FromSeconds(60); 
            return new SshNetSftpClient(client);
        }
    }
}