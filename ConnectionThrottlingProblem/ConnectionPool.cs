using System.Collections.Concurrent;

namespace ConnectionThrottlingProblem
{
	public sealed class ConnectionPool<T> : IDisposable
		where T : IConnection
	{
		private readonly ConnectionPoolSettings _settings;
		private readonly Func<T> _factory;
		private readonly SemaphoreSlim _connectionLock;
		private readonly ConcurrentQueue<T> _pool = new();
		private readonly Random _random = new();
		
		public int CurrentPoolSize => _pool.Count;
		public int ConnectionsReused => _connectionsReused;
		private int _connectionsReused;
		public int ConnectionsDisposed => _connectionsDisposed;
		private int _connectionsDisposed;
		
		public ConnectionPool(ConnectionPoolSettings settings, Func<T> factory)
		{
			settings.Validate();
			ArgumentNullException.ThrowIfNull(factory);
			
			_settings = settings;
			_factory = factory;

			_connectionLock = new SemaphoreSlim(1, _settings.MaxConcurrentConnections);
		}

		public async Task WarmUpAsync(int poolSize = 1, CancellationToken cancellationToken = default)
		{
			if (poolSize < 1)
				throw new ArgumentException("Pool size must be greater than 0", nameof(poolSize));
			
			if (poolSize < _pool.Count)
				return;
			
			await _connectionLock.WaitAsync(cancellationToken);
			try
			{
				if (poolSize < _pool.Count)
					return;
				
				var count = poolSize - _pool.Count;
				count = Math.Min(count, _settings.MaxPoolSize);
				
				for (var i = 0; i < count; i++)
				{
					var connection = await CreateConnectionAndOpenAsync(cancellationToken);
					_pool.Enqueue(connection);
				}
			}
			catch (Exception ex)
			{
				// ignore
				await Console.Out.WriteLineAsync($"Failed to initialize connection pool: {ex.Message}");
			}
			finally
			{
				_connectionLock.Release();
			}
		}
		
		public async Task<T> GetAsync(CancellationToken cancellationToken = default)
		{
			if (TryGetConnectionFromPool(out var connection)) 
				return connection;

			await _connectionLock.WaitAsync(cancellationToken);
			try
			{
				return TryGetConnectionFromPool(out connection) 
					? connection 
					: await CreateConnectionAndOpenAsync(cancellationToken);
			}
			finally
			{
				_connectionLock.Release();
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
				connection?.Dispose();
			}
			return false;
		}

		private async Task<T> CreateConnectionAndOpenAsync(CancellationToken cancellationToken)
		{
			var failedConnectionAttempts = 0;
			do
			{
				T connection = default;
				try
				{
					connection = _factory();
					await connection.OpenAsync(cancellationToken);
					return connection;
				}
				catch (Exception ex)
				{
					failedConnectionAttempts++;
					await Console.Out.WriteLineAsync($"Unable to create connection. Retrying...; {ex.Message}");
					connection?.Dispose();

					if (failedConnectionAttempts >= _settings.MaxFailedConnectionAttempts)
						throw new InvalidOperationException($"Connection could not be established after {failedConnectionAttempts} attempts.", ex);

					var millisecondsDelay = Math.Min(failedConnectionAttempts * _random.Next(300, 3000), _settings.MaxConnectionRetryDelayMs);
					await Task.Delay(millisecondsDelay, cancellationToken);
				}
			} while (failedConnectionAttempts < _settings.MaxFailedConnectionAttempts);
			
			throw new InvalidOperationException($"This code should not be reached.");
		}

		public async Task ReleaseAsync(T connection)
		{
            ArgumentNullException.ThrowIfNull(connection);

            if (_pool.Count < _settings.MaxPoolSize && connection.IsOpen)
			{
				_pool.Enqueue(connection);
			}
			else
			{
				Interlocked.Increment(ref _connectionsDisposed);
				await connection.CloseAsync();
				connection.Dispose();
			}
		}

		public void Dispose()
		{
			while (_pool.TryDequeue(out var connection))
			{
				connection.CloseAsync().GetAwaiter().GetResult();
				connection.Dispose();
			}
			
			_connectionLock?.Dispose();
		}
	}
}