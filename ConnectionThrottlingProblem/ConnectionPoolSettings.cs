namespace ConnectionThrottlingProblem;

public sealed record ConnectionPoolSettings
{
    private const int DefaultMaxPoolSize = 4;
    private const int DefaultMaxConcurrentConnections = 1;
    private const int DefaultMaxFailedOpenAttempts = 5;
    private const int DefaultMaxConnectionRetryDelayMs = 180000;
    
    public int MaxPoolSize { get; init; } = DefaultMaxPoolSize;
    public int MaxConcurrentConnections { get; init; } = DefaultMaxConcurrentConnections;
    public int MaxConnectionRetryDelayMs { get; init; } = DefaultMaxConnectionRetryDelayMs;
    public int MaxFailedConnectionAttempts { get; init; } = DefaultMaxFailedOpenAttempts;

    public void Validate()
    {
        if (MaxPoolSize < 1)
            throw new ArgumentException("MaxPoolSize must be greater than 0", nameof(MaxPoolSize));
        if (MaxConcurrentConnections < 1)
            throw new ArgumentException("MaxConcurrentConnections must be greater than 0", nameof(MaxConcurrentConnections));
        if (MaxConnectionRetryDelayMs < 0)
            throw new ArgumentException("MaxConnectionRetryDelayMs must be greater than 0", nameof(MaxConnectionRetryDelayMs));
        if (MaxFailedConnectionAttempts < 1)
            throw new ArgumentException("MaxFailedConnectionAttempts must be greater than 0", nameof(MaxFailedConnectionAttempts));
    }
}