namespace ConnectionThrottlingProblem;

public static class Test
{
    private static readonly Random Random = new();

    public static async Task RunAsync(ConnectionPool<IFtpClient> connectionPool, int warmUpPoolSize = 10,
        int tasksCount = 50,
        string directoryPath = "/uploads")
    {
        try
        {
            await connectionPool.WarmUpAsync(warmUpPoolSize);

            var tasks = Enumerable.Range(0, tasksCount).Select(i =>
            {
                return Task.Run(async () =>
                {
                    await Task.Delay(Random.Next(100));
                    await WorkAsync(connectionPool, directoryPath, i);
                });
            });
            
            await Task.WhenAll(tasks);
            await DeleteAllFilesAsync(connectionPool, directoryPath);
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
    }

    private static async Task WorkAsync(ConnectionPool<IFtpClient> connectionPool, string directoryPath, int i)
    {
        var client = await connectionPool.GetAsync();
        try
        {
            var fileInfos = client.ListDirectory(directoryPath).Where(x => !x.IsDirectory)
                .OrderByDescending(x => x.Name)
                .ToList();
				
            //await Console.Out.WriteLineAsync($"Files count [{sftpFiles.Count}]");
            // foreach (var sftpFile in sftpFiles)
            // {
            // 	//await Console.Out.WriteLineAsync($"Reading file [{sftpFile.Name}]");
            // 	var sftpFileContent = await ReadAllTextAsync(client, sftpFile.FullName);
            // 	//await Console.Out.WriteLineAsync($"Content length [{sftpFile.Name}] [{sftpFileContent.Length}]");
            // 	await Console.Out.WriteLineAsync($"Content [{sftpFile.Name}] [{sftpFileContent}]");
            // }

            if (fileInfos.Count > 0) 
            {
                var readContent = await client.ReadAllTextAsync(fileInfos[0].FullName);
                await Console.Out.WriteLineAsync($"Content [{fileInfos[0].Name}] [{readContent}]");
            }

            var fileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{i}.txt";
            var filePath = Path.Combine(directoryPath, fileName);
            var fileContent = GenerateRandomText(100);
            await client.WriteAllTextAsync(filePath, fileContent);
        }
        finally
        {
            await connectionPool.ReleaseAsync(client);
        }
    }

    private static string GenerateRandomText(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        return new string(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
    }

    private static async Task DeleteAllFilesAsync(ConnectionPool<IFtpClient> connectionPool, string directoryPath)
    {
        var client = await connectionPool.GetAsync();
        try
        {
            var sftpFiles = client.ListDirectory(directoryPath).Where(x => !x.IsDirectory)
                .OrderByDescending(x => x.Name)
                .ToList();

            foreach (var sftpFile in sftpFiles)
                await client.DeleteAsync(sftpFile.FullName);
        }
        finally
        {
            await connectionPool.ReleaseAsync(client);
        }
    }
}