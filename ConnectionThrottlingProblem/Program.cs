using Renci.SshNet;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System;
using System.Collections.Concurrent;

namespace ConnectionThrottlingProblem
{
	internal static class Program
	{
		private const int BufferSize = 81920;
		private static readonly ConcurrentQueue<string> FilePaths = new ConcurrentQueue<string>();

		public static async Task Main()
		{
			const int count = 150;
			var random = new Random();
			
			var connectionPool = new SftpConnectionPool(2, "localhost", 2222, "user", "pass");
			await connectionPool.InitializeAsync(connectionPool.MaxPoolSize / 2);
			
			try 
			{	
				var tasks = Enumerable.Range(0, count).Select(i =>
				{
					return Task.Run(async () =>
					{
						await Task.Delay(i * random.Next(100, 1000));
						await WorkAsync(connectionPool, i);
					});
				});
				await Task.WhenAll(tasks);

				await DeleteAllFilesAsync(connectionPool);
			}
			catch (Exception ex)
			{ 
				await Console.Out.WriteLineAsync(ex.Message);
			}
			finally
			{
				await Console.Out.WriteLineAsync($"Current pool size [{connectionPool.CurrentPoolSize}]");
				await Console.Out.WriteLineAsync($"Clients reused [{connectionPool.ClientsReused}]");
				await Console.Out.WriteLineAsync($"Clients disposed [{connectionPool.ClientsDisposed}]");
			}
		}

		private static async Task DeleteAllFilesAsync(SftpConnectionPool connectionPool)
		{
			var client = await connectionPool.GetClientAsync();
			try
			{
				while (FilePaths.TryDequeue(out var fileName))
					await client.DeleteAsync(fileName);
			}
			finally
			{
				await connectionPool.ReleaseClientAsync(client);
			}
		}

		private static async Task WorkAsync(SftpConnectionPool connectionPool, int i)
		{
			const string directoryPath = "/uploads";
		
			var client = await connectionPool.GetClientAsync();
			try
			{
				var sftpFiles = client.ListDirectory(directoryPath).Where(x => !x.IsDirectory)
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

				if (sftpFiles.Count > 0) 
				{
					var sftpFileContent = await ReadAllTextAsync(client, sftpFiles[0].FullName);
					await Console.Out.WriteLineAsync($"Content [{sftpFiles[0].Name}] [{sftpFileContent}]");
				}

				var fileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{i}.txt";
				var filePath = Path.Combine(directoryPath, fileName);
				FilePaths.Enqueue(filePath);
				var fileContent = GenerateRandomText(100);
				await WriteAllTextAsync(client, filePath, fileContent);
			}
			finally
			{
				await connectionPool.ReleaseClientAsync(client);
			}
		}

		private static async Task<string> ReadAllTextAsync(SftpClient client, string path)
		{
			using (var output = new MemoryStream()) 
			{
				await DownloadFileAsync(client, path, output, CancellationToken.None);
				output.Position = 0;
				using (var reader = new StreamReader(output))
					return await reader.ReadToEndAsync();
			}
		}

		private static async Task DownloadFileAsync(SftpClient client, string path, Stream output, CancellationToken cancellationToken)
		{ 
			using (var remoteStream = await client.OpenAsync(path, FileMode.Open, FileAccess.Read, cancellationToken))
			{ 
				await remoteStream.CopyToAsync(output, BufferSize, cancellationToken);
			}
		}
	
		private static async Task WriteAllTextAsync(SftpClient client, string path, string content)
		{
			using (var input = new MemoryStream()) 
			{
				using (var writer = new StreamWriter(input))
				{
					await writer.WriteAsync(content);
					await writer.FlushAsync();
					input.Position = 0;
					await UploadFileAsync(client, input, path, FileMode.OpenOrCreate, CancellationToken.None);
				}
			}
		}

		private static async Task UploadFileAsync(this SftpClient client, Stream input, string path, FileMode fileMode, CancellationToken cancellationToken)
		{
			using (var remoteStream = await client.OpenAsync(path, fileMode, FileAccess.Write, cancellationToken))
			{
				await input.CopyToAsync(remoteStream, BufferSize, cancellationToken);
			}
		}
	
		private static string GenerateRandomText(int length)
		{
			const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
			var random = new Random();
			return new string(Enumerable.Range(0, length).Select(_ => chars[random.Next(chars.Length)]).ToArray());
		}
	}
}
