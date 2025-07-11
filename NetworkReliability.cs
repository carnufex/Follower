using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Follower
{
    /// <summary>
    /// Advanced error recovery and network reliability system
    /// </summary>
    public class NetworkReliability
    {
        private readonly Dictionary<string, CircuitBreaker> _circuitBreakers = new Dictionary<string, CircuitBreaker>();
        private readonly Dictionary<string, RetryPolicy> _retryPolicies = new Dictionary<string, RetryPolicy>();
        private readonly object _reliabilityLock = new object();
        
        /// <summary>
        /// Executes an operation with comprehensive error recovery
        /// </summary>
        public async Task<T> ExecuteWithRetry<T>(
            Func<Task<T>> operation,
            string operationName,
            RetryPolicy retryPolicy = null,
            CancellationToken cancellationToken = default)
        {
            retryPolicy ??= GetDefaultRetryPolicy();
            var circuitBreaker = GetCircuitBreaker(operationName);
            
            // Check if circuit breaker is open
            if (circuitBreaker.State == CircuitBreakerState.Open)
            {
                if (circuitBreaker.CanAttemptReset())
                {
                    circuitBreaker.State = CircuitBreakerState.HalfOpen;
                }
                else
                {
                    throw new CircuitBreakerOpenException($"Circuit breaker is open for operation: {operationName}");
                }
            }
            
            Exception lastException = null;
            
            for (int attempt = 0; attempt < retryPolicy.MaxAttempts; attempt++)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    var result = await operation();
                    
                    // Success - reset circuit breaker
                    circuitBreaker.RecordSuccess();
                    return result;
                }
                catch (Exception ex) when (IsRetryableException(ex))
                {
                    lastException = ex;
                    
                    // Record failure in circuit breaker
                    circuitBreaker.RecordFailure();
                    
                    // Check if circuit breaker should open
                    if (circuitBreaker.ShouldOpen())
                    {
                        circuitBreaker.State = CircuitBreakerState.Open;
                        throw new CircuitBreakerOpenException($"Circuit breaker opened due to failures for operation: {operationName}", ex);
                    }
                    
                    // If this is the last attempt, don't wait
                    if (attempt == retryPolicy.MaxAttempts - 1)
                        break;
                    
                    // Calculate delay with exponential backoff and jitter
                    var delay = CalculateDelay(attempt, retryPolicy);
                    await Task.Delay(delay, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Non-retryable exception - fail immediately
                    circuitBreaker.RecordFailure();
                    throw;
                }
            }
            
            // All retries exhausted
            throw new MaxRetriesExceededException($"Operation '{operationName}' failed after {retryPolicy.MaxAttempts} attempts", lastException);
        }
        
        /// <summary>
        /// Executes an operation with fire-and-forget error recovery
        /// </summary>
        public async Task ExecuteWithRetry(
            Func<Task> operation,
            string operationName,
            RetryPolicy retryPolicy = null,
            CancellationToken cancellationToken = default)
        {
            await ExecuteWithRetry(async () =>
            {
                await operation();
                return true;
            }, operationName, retryPolicy, cancellationToken);
        }
        
        /// <summary>
        /// Executes multiple operations with intelligent batching and error recovery
        /// </summary>
        public async Task<IEnumerable<T>> ExecuteBatch<T>(
            IEnumerable<Func<Task<T>>> operations,
            string operationName,
            int maxConcurrency = 3,
            RetryPolicy retryPolicy = null,
            CancellationToken cancellationToken = default)
        {
            retryPolicy ??= GetDefaultRetryPolicy();
            var results = new List<T>();
            var semaphore = new SemaphoreSlim(maxConcurrency);
            
            var tasks = operations.Select(async operation =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    return await ExecuteWithRetry(operation, operationName, retryPolicy, cancellationToken);
                }
                finally
                {
                    semaphore.Release();
                }
            });
            
            var completedTasks = await Task.WhenAll(tasks);
            return completedTasks;
        }
        
        /// <summary>
        /// Monitors and reports on network health metrics
        /// </summary>
        public NetworkHealthReport GetHealthReport()
        {
            lock (_reliabilityLock)
            {
                var report = new NetworkHealthReport
                {
                    Timestamp = DateTime.UtcNow,
                    CircuitBreakers = _circuitBreakers.Select(kvp => new CircuitBreakerStatus
                    {
                        OperationName = kvp.Key,
                        State = kvp.Value.State,
                        FailureCount = kvp.Value.FailureCount,
                        LastFailureTime = kvp.Value.LastFailureTime,
                        SuccessCount = kvp.Value.SuccessCount
                    }).ToList(),
                    TotalOperations = _circuitBreakers.Values.Sum(cb => cb.TotalOperations),
                    TotalFailures = _circuitBreakers.Values.Sum(cb => cb.FailureCount),
                    OverallHealthScore = CalculateOverallHealthScore()
                };
                
                return report;
            }
        }
        
        /// <summary>
        /// Resets all circuit breakers (use with caution)
        /// </summary>
        public void ResetAllCircuitBreakers()
        {
            lock (_reliabilityLock)
            {
                foreach (var circuitBreaker in _circuitBreakers.Values)
                {
                    circuitBreaker.Reset();
                }
            }
        }
        
        /// <summary>
        /// Adds a custom retry policy for specific operations
        /// </summary>
        public void AddRetryPolicy(string operationName, RetryPolicy policy)
        {
            lock (_reliabilityLock)
            {
                _retryPolicies[operationName] = policy;
            }
        }
        
        private CircuitBreaker GetCircuitBreaker(string operationName)
        {
            lock (_reliabilityLock)
            {
                if (!_circuitBreakers.TryGetValue(operationName, out var circuitBreaker))
                {
                    circuitBreaker = new CircuitBreaker(operationName);
                    _circuitBreakers[operationName] = circuitBreaker;
                }
                return circuitBreaker;
            }
        }
        
        private RetryPolicy GetDefaultRetryPolicy()
        {
            return new RetryPolicy
            {
                MaxAttempts = 3,
                BaseDelay = TimeSpan.FromSeconds(1),
                MaxDelay = TimeSpan.FromSeconds(30),
                BackoffMultiplier = 2.0,
                JitterEnabled = true
            };
        }
        
        private TimeSpan CalculateDelay(int attempt, RetryPolicy policy)
        {
            var delay = policy.BaseDelay.TotalMilliseconds * Math.Pow(policy.BackoffMultiplier, attempt);
            delay = Math.Min(delay, policy.MaxDelay.TotalMilliseconds);
            
            if (policy.JitterEnabled)
            {
                var random = new Random();
                var jitter = delay * 0.1 * (random.NextDouble() - 0.5);
                delay += jitter;
            }
            
            return TimeSpan.FromMilliseconds(Math.Max(0, delay));
        }
        
        private bool IsRetryableException(Exception ex)
        {
            return ex is TimeoutException ||
                   ex is TaskCanceledException ||
                   ex is System.Net.Sockets.SocketException ||
                   ex is System.Net.WebException ||
                   (ex is AggregateException aggEx && aggEx.InnerExceptions.All(IsRetryableException));
        }
        
        private double CalculateOverallHealthScore()
        {
            if (!_circuitBreakers.Any()) return 1.0;
            
            var totalOperations = _circuitBreakers.Values.Sum(cb => cb.TotalOperations);
            if (totalOperations == 0) return 1.0;
            
            var totalFailures = _circuitBreakers.Values.Sum(cb => cb.FailureCount);
            var successRate = (double)(totalOperations - totalFailures) / totalOperations;
            
            // Factor in circuit breaker states
            var openBreakers = _circuitBreakers.Values.Count(cb => cb.State == CircuitBreakerState.Open);
            var breakerPenalty = openBreakers * 0.1;
            
            return Math.Max(0, successRate - breakerPenalty);
        }
    }
    
    /// <summary>
    /// Circuit breaker implementation for preventing cascading failures
    /// </summary>
    public class CircuitBreaker
    {
        public string OperationName { get; }
        public CircuitBreakerState State { get; set; } = CircuitBreakerState.Closed;
        public int FailureCount { get; private set; }
        public int SuccessCount { get; private set; }
        public DateTime LastFailureTime { get; private set; }
        public int TotalOperations => FailureCount + SuccessCount;
        
        private readonly int _failureThreshold;
        private readonly TimeSpan _timeout;
        private readonly object _lock = new object();
        
        public CircuitBreaker(string operationName, int failureThreshold = 5, TimeSpan? timeout = null)
        {
            OperationName = operationName;
            _failureThreshold = failureThreshold;
            _timeout = timeout ?? TimeSpan.FromMinutes(1);
        }
        
        public void RecordSuccess()
        {
            lock (_lock)
            {
                SuccessCount++;
                
                if (State == CircuitBreakerState.HalfOpen)
                {
                    Reset();
                }
            }
        }
        
        public void RecordFailure()
        {
            lock (_lock)
            {
                FailureCount++;
                LastFailureTime = DateTime.UtcNow;
            }
        }
        
        public bool ShouldOpen()
        {
            lock (_lock)
            {
                return State == CircuitBreakerState.Closed && 
                       FailureCount >= _failureThreshold;
            }
        }
        
        public bool CanAttemptReset()
        {
            lock (_lock)
            {
                return State == CircuitBreakerState.Open && 
                       DateTime.UtcNow - LastFailureTime > _timeout;
            }
        }
        
        public void Reset()
        {
            lock (_lock)
            {
                State = CircuitBreakerState.Closed;
                FailureCount = 0;
                SuccessCount = 0;
                LastFailureTime = DateTime.MinValue;
            }
        }
    }
    
    /// <summary>
    /// Circuit breaker states
    /// </summary>
    public enum CircuitBreakerState
    {
        Closed,   // Normal operation
        Open,     // Failing fast
        HalfOpen  // Testing if service has recovered
    }
    
    /// <summary>
    /// Retry policy configuration
    /// </summary>
    public class RetryPolicy
    {
        public int MaxAttempts { get; set; } = 3;
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
        public double BackoffMultiplier { get; set; } = 2.0;
        public bool JitterEnabled { get; set; } = true;
        
        public static RetryPolicy Conservative => new RetryPolicy
        {
            MaxAttempts = 2,
            BaseDelay = TimeSpan.FromSeconds(2),
            MaxDelay = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 1.5,
            JitterEnabled = false
        };
        
        public static RetryPolicy Aggressive => new RetryPolicy
        {
            MaxAttempts = 5,
            BaseDelay = TimeSpan.FromMilliseconds(500),
            MaxDelay = TimeSpan.FromSeconds(60),
            BackoffMultiplier = 2.5,
            JitterEnabled = true
        };
    }
    
    /// <summary>
    /// Network health report
    /// </summary>
    public class NetworkHealthReport
    {
        public DateTime Timestamp { get; set; }
        public List<CircuitBreakerStatus> CircuitBreakers { get; set; } = new List<CircuitBreakerStatus>();
        public int TotalOperations { get; set; }
        public int TotalFailures { get; set; }
        public double OverallHealthScore { get; set; }
        
        public bool IsHealthy => OverallHealthScore >= 0.95;
        public bool IsUnhealthy => OverallHealthScore < 0.7;
    }
    
    /// <summary>
    /// Circuit breaker status information
    /// </summary>
    public class CircuitBreakerStatus
    {
        public string OperationName { get; set; }
        public CircuitBreakerState State { get; set; }
        public int FailureCount { get; set; }
        public int SuccessCount { get; set; }
        public DateTime LastFailureTime { get; set; }
    }
    
    /// <summary>
    /// Exception thrown when circuit breaker is open
    /// </summary>
    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
        public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    /// <summary>
    /// Exception thrown when maximum retries are exceeded
    /// </summary>
    public class MaxRetriesExceededException : Exception
    {
        public MaxRetriesExceededException(string message) : base(message) { }
        public MaxRetriesExceededException(string message, Exception innerException) : base(message, innerException) { }
    }
} 