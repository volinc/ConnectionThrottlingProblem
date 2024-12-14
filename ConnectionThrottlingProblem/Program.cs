using ConnectionThrottlingProblem;

var threadSafeSftpClientFactory = new ThreadSafeSftpClientFactory("localhost", 2222, "user", "pass");
var connectionPoolSettings = new ConnectionPoolSettings
{
	MaxPoolSize = 2,
	MaxConcurrentConnections = 1,
};
var connectionPool = new ConnectionPool<ThreadSafeSftpClient>(connectionPoolSettings, threadSafeSftpClientFactory.Create);
await connectionPool.WarmUpAsync(10);

try 
{	
	const int count = 50;
	const string directoryPath = "/uploads";
	var random = new Random();
	
	var tasks = Enumerable.Range(0, count).Select(i =>
	{
		return Task.Run(async () =>
		{
			await Task.Delay(random.Next(100));
			await Test.WorkAsync(connectionPool, directoryPath, i);
		});
	});
	await Task.WhenAll(tasks);
	await Test.DeleteAllFilesAsync(connectionPool, directoryPath);
}
catch (Exception ex)
{ 
	await Console.Out.WriteLineAsync(ex.Message);
}
finally
{
	await Console.Out.WriteLineAsync($"Current pool size [{connectionPool.CurrentPoolSize}]");
	await Console.Out.WriteLineAsync($"Clients reused [{connectionPool.ConnectionsReused}]");
	await Console.Out.WriteLineAsync($"Clients disposed [{connectionPool.ConnectionsDisposed}]");
}