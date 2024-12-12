using ConnectionThrottlingProblem;

var threadSafeSftpClientFactory = new ThreadSafeSftpClientFactory("localhost", 2222, "user", "pass");
var connectionPool = new ConnectionPool<ThreadSafeSftpClient>(threadSafeSftpClientFactory.Create);
await connectionPool.InitializeAsync(connectionPool.MaxPoolSize / 2);
			
try 
{	
	const int count = 10;
	const string directoryPath = "/uploads";
	var random = new Random();
	
	var tasks = Enumerable.Range(0, count).Select(i =>
	{
		return Task.Run(async () =>
		{
			await Task.Delay(i * random.Next(100, 1000));
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