using System.Collections.Concurrent;

namespace ConnectionThrottlingProblem
{
	public sealed class ConnectionPool<T> : IDisposable
		where T : IConnection
	{
		private const int DefaultMaxPoolSize = 4;
		private const int MaxFailedOpenAttempts = 5;
		private const int MaxDelayBetweenOpenAttemptsInMs = 180000; // 3 minutes
		
		private readonly SemaphoreSlim _poolLock = new(1, 1);
		private readonly ConcurrentQueue<T> _pool = new();
		
		private readonly Func<T> _factory;
		
		public int MaxPoolSize { get; }
		
		private bool _isInitialized;
		
		public int CurrentPoolSize => _pool.Count;
		public int ConnectionsReused => _connectionsReused;
		private int _connectionsReused;
		public int ConnectionsDisposed => _connectionsDisposed;
		private int _connectionsDisposed;
		
		public ConnectionPool(Func<T> factory, int maxPoolSize = DefaultMaxPoolSize)
		{
			ArgumentNullException.ThrowIfNull(factory);
			
			if (maxPoolSize < 0)
				throw new ArgumentException("Argument cannot be less than 0", nameof(maxPoolSize));
			
			_factory = factory;
			MaxPoolSize = maxPoolSize;
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
				if (_isInitialized)
					return;

				var count = Math.Max(0, initialPoolSize - CurrentPoolSize);
				for (var i = 0; i < count; i++)
				{
					var connection = await CreateConnectionAndOpenAsync();
					_pool.Enqueue(connection);
				}
				
				_isInitialized = true;
			}
			catch (Exception ex)
			{
				// ignore
				await Console.Out.WriteLineAsync($"Failed to initialize connection pool: {ex.Message}");
			}
			finally
			{
				_poolLock.Release();
			}
		}
		
		public async Task<T> GetAsync()
		{
			if (TryGetConnectionFromPool(out var connection)) 
				return connection;

			await _poolLock.WaitAsync();
			try
			{
				return TryGetConnectionFromPool(out connection) 
					? connection 
					: await CreateConnectionAndOpenAsync();
			}
			finally
			{
				_poolLock.Release();
			}
		}

		private bool TryGetConnectionFromPool(out T connection)
		{
			if (_pool.TryDequeue(out connection)) 
			{
				if (connection.IsOpen)
				{
					Interlocked.Increment(ref _connectionsReused);
					return true;
				}
				(connection as IDisposable)?.Dispose();
			}
			return false;
		}

		private async Task<T> CreateConnectionAndOpenAsync()
		{
			var random = new Random();
			var failedAttempts = 0;
			
			while (true)
			{
				T connection = default;
				try
				{
					connection = _factory();
					await connection.OpenAsync(CancellationToken.None);
					return connection;
				}
				catch (Exception ex)
				{
					(connection as IDisposable)?.Dispose();
					
					if (failedAttempts++ >= MaxFailedOpenAttempts)
					{
						await Console.Out.WriteLineAsync($"Connection could not be established. Exception: {ex.Message}");
						throw;
					}

					//await Console.Out.WriteLineAsync($"Unable to connect to {_connectionInfo.Host}:{_connectionInfo.Port}. Retrying...");
					var millisecondsDelay = Math.Min(failedAttempts * random.Next(300, 3000), MaxDelayBetweenOpenAttemptsInMs);
					await Task.Delay(millisecondsDelay);
				}
			}
		}
		
		public async Task ReleaseAsync(T connection)
		{
            ArgumentNullException.ThrowIfNull(connection);

            if (_pool.Count < MaxPoolSize && connection.IsOpen)
			{
				_pool.Enqueue(connection);
			}
			else
			{
				Interlocked.Increment(ref _connectionsDisposed);
				await connection.CloseAsync();
				(connection as IDisposable)?.Dispose();
			}
		}

		public void Dispose()
		{
			while (_pool.TryDequeue(out var connection))
			{
				connection.CloseAsync().RunSynchronously();
				(connection as IDisposable)?.Dispose();
			}
			
			_poolLock?.Dispose();
		}
	}
}