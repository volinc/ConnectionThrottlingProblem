using ConnectionThrottlingProblem;

const string host = "localhost";
const int port = 2222;
const string username = "user";
const string password = "pass";

var ftpClientFactory = new SshNetSftpClientFactory(host, port, username, password);
//var ftpClientFactory = new RebexSftpClientFactory(host, port, username, password);

var connectionPoolSettings = new ConnectionPoolSettings
{
	MaxPoolSize = 2,
	MaxConcurrentConnections = 1,
};
var connectionPool = new ConnectionPool<IFtpClient>(connectionPoolSettings, ftpClientFactory.Create);

await Test.RunAsync(connectionPool, warmUpPoolSize: 10, tasksCount: 50, directoryPath: "/uploads");

