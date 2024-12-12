namespace ConnectionThrottlingProblem;

public static class Test
{
    public static async Task WorkAsync(ConnectionPool<ThreadSafeSftpClient> connectionPool, string directoryPath, int i)
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
    
    public static async Task DeleteAllFilesAsync(ConnectionPool<ThreadSafeSftpClient> connectionPool, string directoryPath)
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