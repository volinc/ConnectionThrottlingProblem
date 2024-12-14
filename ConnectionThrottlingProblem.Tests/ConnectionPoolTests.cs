using Moq;

namespace ConnectionThrottlingProblem.Tests
{
    [TestFixture]
    public class ConnectionPoolTests
    {
        [Test]
        public void ConnectionsReused_InitiallyZero()
        {
            // Arrange
            var settings = new ConnectionPoolSettings();
            var factoryMock = new Mock<Func<IConnection>>();
            var connectionPool = new ConnectionPool<IConnection>(settings, factoryMock.Object);

            // Act
            var reusedCount = connectionPool.ConnectionsReused;

            // Assert
            Assert.That(reusedCount, Is.EqualTo(0));
        }
        
        [Test]
        public void ConnectionsDisposed_InitiallyZero()
        {
            // Arrange
            var settings = new ConnectionPoolSettings();
            var factoryMock = new Mock<Func<IConnection>>();
            var connectionPool = new ConnectionPool<IConnection>(settings, factoryMock.Object);

            // Act
            var disposedCount = connectionPool.ConnectionsDisposed;

            // Assert
            Assert.That(disposedCount, Is.EqualTo(0));
        }

        [Test]
        public async Task ConnectionsReused_IncrementsWhenConnectionReused()
        {
            // Arrange
            var settings = new ConnectionPoolSettings();
            var connectionMock = new Mock<IConnection>();
            connectionMock.Setup(c => c.IsOpen).Returns(true);
            var factoryMock = new Mock<Func<IConnection>>();
            factoryMock.Setup(f => f()).Returns(connectionMock.Object);
            var connectionPool = new ConnectionPool<IConnection>(settings, factoryMock.Object);

            // Act
            await connectionPool.WarmUpAsync(1);
            await connectionPool.GetAsync();

            // Assert
            Assert.That(connectionPool.ConnectionsReused, Is.EqualTo(1));
        }

        [Test]
        public async Task ConnectionsReused_DoesNotIncrementWhenConnectionNotReused()
        {
            // Arrange
            var settings = new ConnectionPoolSettings();
            var connectionMock = new Mock<IConnection>();
            connectionMock.Setup(c => c.IsOpen).Returns(false);
            var factoryMock = new Mock<Func<IConnection>>();
            factoryMock.Setup(f => f()).Returns(connectionMock.Object);
            var connectionPool = new ConnectionPool<IConnection>(settings, factoryMock.Object);

            // Act
            await connectionPool.WarmUpAsync(1);
            await connectionPool.GetAsync();

            // Assert
            Assert.That(connectionPool.ConnectionsReused, Is.EqualTo(0));
        }

        [Test]
        public async Task ConnectionsReused_IncrementsMultipleTimesWhenReusedMultipleTimes()
        {
            // Arrange
            var settings = new ConnectionPoolSettings();
            var connectionMock = new Mock<IConnection>();
            connectionMock.Setup(c => c.IsOpen).Returns(true);
            var factoryMock = new Mock<Func<IConnection>>();
            factoryMock.Setup(f => f()).Returns(connectionMock.Object);
            var connectionPool = new ConnectionPool<IConnection>(settings, factoryMock.Object);

            // Act
            await connectionPool.WarmUpAsync(1); 
            await connectionPool.GetAsync(); // Get the connection from the pool (reused once)
            await connectionPool.ReleaseAsync(connectionMock.Object); // Release the connection back to the pool
            await connectionPool.GetAsync(); // Get the connection again (reused twice)
            await connectionPool.ReleaseAsync(connectionMock.Object); 
            await connectionPool.GetAsync(); // Get the connection again (reused thrice)

            // Assert
            Assert.That(connectionPool.ConnectionsReused, Is.EqualTo(3));
        }
        
        [Test]
        public async Task CreateConnectionAndOpenAsync_ReturnsConnection_OnSuccessfulAttempt()
        {
            // Arrange
            var settings = new ConnectionPoolSettings();
            var connectionMock = new Mock<IConnection>();
            var factoryMock = new Mock<Func<IConnection>>();
            factoryMock.Setup(f => f()).Returns(connectionMock.Object);
            var connectionPool = new ConnectionPool<IConnection>(settings, factoryMock.Object);

            // Act
            var connection = await connectionPool.GetAsync();

            // Assert
            Assert.That(connection, Is.EqualTo(connectionMock.Object));
        }

        [Test]
        public async Task CreateConnectionAndOpenAsync_ReturnsConnection_AfterRetries()
        {
            // Arrange
            var settings = new ConnectionPoolSettings
            {
                MaxFailedConnectionAttempts = 3,
                MaxConnectionRetryDelayMs = 1000
            };

            var connectionMock = new Mock<IConnection>();
            var factoryMock = new Mock<Func<IConnection>>();
            factoryMock.SetupSequence(f => f())
                .Throws(new Exception("Simulated connection failure"))
                .Throws(new Exception("Simulated connection failure"))
                .Returns(connectionMock.Object); // Succeed on the third attempt

            var connectionPool = new ConnectionPool<IConnection>(settings, factoryMock.Object);

            // Act
            var connection = await connectionPool.GetAsync();

            // Assert
            Assert.That(connection, Is.EqualTo(connectionMock.Object));
        }

        [Test]
        public void CreateConnectionAndOpenAsync_ThrowsInvalidOperationException_WhenAllRetriesFail()
        {
            // Arrange
            var settings = new ConnectionPoolSettings
            {
                MaxFailedConnectionAttempts = 3,
                MaxConnectionRetryDelayMs = 1000
            };

            var connectionMock = new Mock<IConnection>();
            connectionMock.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>()))
                          .ThrowsAsync(new Exception("Simulated connection failure"));

            var factoryMock = new Mock<Func<IConnection>>();
            factoryMock.Setup(f => f()).Returns(connectionMock.Object);

            var connectionPool = new ConnectionPool<IConnection>(settings, factoryMock.Object);

            // Act & Assert
            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await connectionPool.GetAsync()
            );
        }
    }
}