using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;

namespace ConnectionThrottlingProblem
{
	public sealed class SftpConnectionPool : IDisposable
	{
		private const int MaxConnectionAttempts = 5;
		private const int MaxDelayBetweenConnectionAttemptsInMs = 180000; // 3 minutes
		
		private readonly SemaphoreSlim _poolLock = new SemaphoreSlim(1, 1);
		private readonly ConcurrentQueue<SftpClient> _pool = new ConcurrentQueue<SftpClient>();
		private readonly ConnectionInfo _connectionInfo;
		
		public int MaxPoolSize { get; }
		public bool IsInitialized { get; private set; }
		public int CurrentPoolSize => _pool.Count;
		public int ClientsReused => _clientsReused;
		private int _clientsReused;
		public int ClientsDisposed => _clientsDisposed;
		private int _clientsDisposed;
		
		public SftpConnectionPool(int maxPoolSize, string host, int port, string username, string password)
		{
			if (maxPoolSize < 0)
				throw new ArgumentException("Argument cannot be less than 0", nameof(maxPoolSize));
			if (port < 0)
				throw new ArgumentException("Argument cannot be less than 0", nameof(port));
			
			ArgumentNullException.ThrowIfNull(host);
			ArgumentNullException.ThrowIfNull(username);
			ArgumentNullException.ThrowIfNull(password);
			
            MaxPoolSize = maxPoolSize;
		
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

		public async Task InitializeAsync(int initialPoolSize)
		{
			if (initialPoolSize < 0)
				throw new ArgumentException("Argument cannot be less than 0", nameof(initialPoolSize));
			if (initialPoolSize > MaxPoolSize)
				throw new ArgumentException("Argument cannot be greater than the pool size", nameof(initialPoolSize));

			await _poolLock.WaitAsync();
			try
			{
				if (IsInitialized)
					return;

				var count = Math.Max(0, initialPoolSize - CurrentPoolSize);
				for (var i = 0; i < count; i++)
				{
					var client = await CreateClientAndConnectAsync();
					_pool.Enqueue(client);
				}
				
				IsInitialized = true;
			}
			catch (Exception ex)
			{
				// ignore
				await Console.Out.WriteLineAsync($"Failed to initialize Sftp connection pool: {ex.Message}");
			}
			finally
			{
				_poolLock.Release();
			}
		}
		
		public async Task<SftpClient> GetClientAsync()
		{
			if (TryGetClientFromPool(out var client)) 
				return client;

			await _poolLock.WaitAsync();
			try
			{
				return TryGetClientFromPool(out client) 
					? client 
					: await CreateClientAndConnectAsync();
			}
			finally
			{
				_poolLock.Release();
			}
		}

		private bool TryGetClientFromPool(out SftpClient client)
		{
			if (_pool.TryDequeue(out client)) 
			{
				if (client.IsConnected)
				{
					Interlocked.Increment(ref _clientsReused);
					return true;
				}
				client.Dispose();
			}
			return false;
		}

		private async Task<SftpClient> CreateClientAndConnectAsync()
		{
			var random = new Random();
			var attemptCount = 0;
			while (true)
			{
				SftpClient client = null;
				try
				{
					client = new SftpClient(_connectionInfo);
					client.KeepAliveInterval = TimeSpan.FromSeconds(60); 
					await client.ConnectAsync(CancellationToken.None);
					return client;
				}
				catch (Exception ex)
				{
					client?.Dispose();
					
					if (attemptCount++ >= MaxConnectionAttempts)
					{
						await Console.Out.WriteLineAsync($"Sftp connection could not be established. Exception: {ex.Message}");
						throw;
					}

					await Console.Out.WriteLineAsync($"Unable to connect to {_connectionInfo.Host}:{_connectionInfo.Port}. Retrying...");
					var millisecondsDelay = Math.Min(attemptCount * random.Next(300, 3000), MaxDelayBetweenConnectionAttemptsInMs);
					await Task.Delay(millisecondsDelay);
				}
			}
		}
		
		public Task ReleaseClientAsync(SftpClient client)
		{
            ArgumentNullException.ThrowIfNull(client);

            if (_pool.Count < MaxPoolSize && client.IsConnected)
			{
				_pool.Enqueue(client);
			}
			else
			{
				Interlocked.Increment(ref _clientsDisposed);
				client.Disconnect();
				client.Dispose();
			}

			return Task.CompletedTask;
		}

		public void Dispose()
		{
			while (_pool.TryDequeue(out var client))
			{
				client.Disconnect();
				client.Dispose();
			}
			
			_poolLock?.Dispose();
		}
	}
}