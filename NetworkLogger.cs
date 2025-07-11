using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Text;

namespace Follower
{
    /// <summary>
    /// Enterprise-grade structured logging and metrics collection system
    /// </summary>
    public class NetworkLogger : IDisposable
    {
        private readonly ConcurrentQueue<LogEntry> _logQueue = new ConcurrentQueue<LogEntry>();
        private readonly ConcurrentDictionary<string, MetricTracker> _metrics = new ConcurrentDictionary<string, MetricTracker>();
        private readonly Timer _metricsTimer;
        private readonly Timer _logFlushTimer;
        private readonly string _logDirectory;
        private readonly string _connectionId;
        private readonly object _fileLock = new object();
        private readonly CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private readonly Task _logProcessingTask;
        private bool _disposed = false;
        
        public event EventHandler<LogEventArgs> LogEvent;
        public event EventHandler<MetricsEventArgs> MetricsReported;
        
        public NetworkLogger(string connectionId, string logDirectory = null)
        {
            _connectionId = connectionId;
            _logDirectory = logDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FollowerPlugin", "Logs");
            
            // Ensure log directory exists
            Directory.CreateDirectory(_logDirectory);
            
            // Start timers
            _metricsTimer = new Timer(ReportMetrics, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            _logFlushTimer = new Timer(FlushLogs, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            
            // Start log processing task
            _logProcessingTask = Task.Run(ProcessLogQueue);
        }
        
        /// <summary>
        /// Logs a network event with structured data
        /// </summary>
        public void LogNetworkEvent(string eventType, object data, LogLevel level = LogLevel.Info, string operationName = null)
        {
            var logEntry = new LogEntry
            {
                Timestamp = DateTime.UtcNow,
                Level = level,
                EventType = eventType,
                Data = data,
                ConnectionId = _connectionId,
                OperationName = operationName,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
                ProcessId = Process.GetCurrentProcess().Id
            };
            
            _logQueue.Enqueue(logEntry);
            
            // Trigger event for real-time monitoring
            LogEvent?.Invoke(this, new LogEventArgs(logEntry));
            
            // Record metrics
            RecordMetric($"log.{level.ToString().ToLower()}", 1);
            if (!string.IsNullOrEmpty(operationName))
            {
                RecordMetric($"operation.{operationName}", 1);
            }
        }
        
        /// <summary>
        /// Logs a performance event with timing information
        /// </summary>
        public void LogPerformanceEvent(string operationName, TimeSpan duration, bool success = true, string details = null)
        {
            var performanceData = new
            {
                Operation = operationName,
                Duration = duration.TotalMilliseconds,
                Success = success,
                Details = details
            };
            
            LogNetworkEvent("PERFORMANCE", performanceData, LogLevel.Debug, operationName);
            
            // Record performance metrics
            RecordMetric($"performance.{operationName}.duration", duration.TotalMilliseconds);
            RecordMetric($"performance.{operationName}.{(success ? "success" : "failure")}", 1);
        }
        
        /// <summary>
        /// Logs an error with full context
        /// </summary>
        public void LogError(string message, Exception exception = null, string operationName = null, object context = null)
        {
            var errorData = new
            {
                Message = message,
                Exception = exception?.ToString(),
                OperationName = operationName,
                Context = context,
                StackTrace = exception?.StackTrace
            };
            
            LogNetworkEvent("ERROR", errorData, LogLevel.Error, operationName);
            
            // Record error metrics
            RecordMetric("errors.total", 1);
            if (!string.IsNullOrEmpty(operationName))
            {
                RecordMetric($"errors.{operationName}", 1);
            }
        }
        
        /// <summary>
        /// Logs a warning message
        /// </summary>
        public void LogWarning(string message, object context = null, string operationName = null)
        {
            var warningData = new
            {
                Message = message,
                Context = context,
                OperationName = operationName
            };
            
            LogNetworkEvent("WARNING", warningData, LogLevel.Warning, operationName);
            
            // Record warning metrics
            RecordMetric("warnings.total", 1);
        }
        
        /// <summary>
        /// Logs detailed connection information
        /// </summary>
        public void LogConnectionEvent(string eventType, string leaderName, string ipAddress, int port, string details = null)
        {
            var connectionData = new
            {
                EventType = eventType,
                LeaderName = leaderName,
                IpAddress = ipAddress,
                Port = port,
                Details = details
            };
            
            LogNetworkEvent("CONNECTION", connectionData, LogLevel.Info, "connection");
            
            // Record connection metrics
            RecordMetric($"connection.{eventType.ToLower()}", 1);
        }
        
        /// <summary>
        /// Logs command execution details
        /// </summary>
        public void LogCommandEvent(string command, string status, TimeSpan? duration = null, object result = null, string error = null)
        {
            var commandData = new
            {
                Command = command,
                Status = status,
                Duration = duration?.TotalMilliseconds,
                Result = result,
                Error = error
            };
            
            var level = status == "SUCCESS" ? LogLevel.Info : 
                       status == "ERROR" ? LogLevel.Error : LogLevel.Debug;
            
            LogNetworkEvent("COMMAND", commandData, level, "command");
            
            // Record command metrics
            RecordMetric($"command.{command.ToLower()}.{status.ToLower()}", 1);
            if (duration.HasValue)
            {
                RecordMetric($"command.{command.ToLower()}.duration", duration.Value.TotalMilliseconds);
            }
        }
        
        /// <summary>
        /// Records a custom metric
        /// </summary>
        public void RecordMetric(string name, double value)
        {
            var tracker = _metrics.GetOrAdd(name, _ => new MetricTracker(name));
            tracker.RecordValue(value);
        }
        
        /// <summary>
        /// Gets current metrics snapshot
        /// </summary>
        public Dictionary<string, MetricSnapshot> GetMetrics()
        {
            return _metrics.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.GetSnapshot()
            );
        }
        
        /// <summary>
        /// Gets performance summary for an operation
        /// </summary>
        public PerformanceSummary GetPerformanceSummary(string operationName)
        {
            var durationKey = $"performance.{operationName}.duration";
            var successKey = $"performance.{operationName}.success";
            var failureKey = $"performance.{operationName}.failure";
            
            var duration = _metrics.TryGetValue(durationKey, out var durationTracker) ? durationTracker.GetSnapshot() : null;
            var success = _metrics.TryGetValue(successKey, out var successTracker) ? successTracker.GetSnapshot() : null;
            var failure = _metrics.TryGetValue(failureKey, out var failureTracker) ? failureTracker.GetSnapshot() : null;
            
            return new PerformanceSummary
            {
                OperationName = operationName,
                AverageDuration = duration?.Average ?? 0,
                MinDuration = duration?.Min ?? 0,
                MaxDuration = duration?.Max ?? 0,
                TotalExecutions = (success?.Total ?? 0) + (failure?.Total ?? 0),
                SuccessCount = success?.Total ?? 0,
                FailureCount = failure?.Total ?? 0,
                SuccessRate = (success?.Total ?? 0) / Math.Max(1, (success?.Total ?? 0) + (failure?.Total ?? 0))
            };
        }
        
        /// <summary>
        /// Exports logs to JSON file
        /// </summary>
        public async Task ExportLogsToFile(string fileName = null, LogLevel minLevel = LogLevel.Debug)
        {
            fileName ??= $"network-logs-{DateTime.UtcNow:yyyy-MM-dd-HH-mm-ss}.json";
            var filePath = Path.Combine(_logDirectory, fileName);
            
            var logs = new List<LogEntry>();
            
            // Drain the queue
            while (_logQueue.TryDequeue(out var logEntry))
            {
                if (logEntry.Level >= minLevel)
                {
                    logs.Add(logEntry);
                }
            }
            
            var json = JsonConvert.SerializeObject(logs, Formatting.Indented);
            await File.WriteAllTextAsync(filePath, json);
        }
        
        /// <summary>
        /// Gets real-time health status
        /// </summary>
        public HealthStatus GetHealthStatus()
        {
            var now = DateTime.UtcNow;
            var recentErrors = _metrics.Where(kvp => kvp.Key.StartsWith("errors."))
                .Sum(kvp => kvp.Value.GetRecentCount(TimeSpan.FromMinutes(5)));
            
            var recentWarnings = _metrics.Where(kvp => kvp.Key.StartsWith("warnings."))
                .Sum(kvp => kvp.Value.GetRecentCount(TimeSpan.FromMinutes(5)));
            
            var connectionStatus = _metrics.ContainsKey("connection.connected") ? "Connected" : "Disconnected";
            
            return new HealthStatus
            {
                Timestamp = now,
                ConnectionStatus = connectionStatus,
                ErrorsLast5Minutes = recentErrors,
                WarningsLast5Minutes = recentWarnings,
                IsHealthy = recentErrors == 0 && connectionStatus == "Connected",
                Uptime = now - Process.GetCurrentProcess().StartTime
            };
        }
        
        private async Task ProcessLogQueue()
        {
            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    var logsToProcess = new List<LogEntry>();
                    
                    // Batch process logs
                    while (_logQueue.TryDequeue(out var logEntry) && logsToProcess.Count < 100)
                    {
                        logsToProcess.Add(logEntry);
                    }
                    
                    if (logsToProcess.Count > 0)
                    {
                        await WriteLogsToFile(logsToProcess);
                    }
                    
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Log processing error - use basic logging to avoid recursion
                    Debug.WriteLine($"Error processing logs: {ex.Message}");
                }
            }
        }
        
        private async Task WriteLogsToFile(IEnumerable<LogEntry> logs)
        {
            var fileName = $"network-{DateTime.UtcNow:yyyy-MM-dd}.log";
            var filePath = Path.Combine(_logDirectory, fileName);
            
            var logLines = logs.Select(log => JsonConvert.SerializeObject(log, Formatting.None));
            var content = string.Join(Environment.NewLine, logLines) + Environment.NewLine;
            
            lock (_fileLock)
            {
                File.AppendAllText(filePath, content);
            }
        }
        
        private void ReportMetrics(object state)
        {
            try
            {
                var metrics = GetMetrics();
                MetricsReported?.Invoke(this, new MetricsEventArgs(metrics));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reporting metrics: {ex.Message}");
            }
        }
        
        private void FlushLogs(object state)
        {
            try
            {
                // Force flush any pending logs
                var pendingLogs = new List<LogEntry>();
                while (_logQueue.TryDequeue(out var logEntry))
                {
                    pendingLogs.Add(logEntry);
                }
                
                if (pendingLogs.Count > 0)
                {
                    WriteLogsToFile(pendingLogs).Wait(5000);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error flushing logs: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            _cancellationTokenSource?.Cancel();
            _metricsTimer?.Dispose();
            _logFlushTimer?.Dispose();
            
            try
            {
                _logProcessingTask?.Wait(5000);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during logger disposal: {ex.Message}");
            }
            
            _cancellationTokenSource?.Dispose();
        }
    }
    
    /// <summary>
    /// Tracks metrics for a specific measurement
    /// </summary>
    public class MetricTracker
    {
        private readonly string _name;
        private readonly object _lock = new object();
        private readonly List<MetricValue> _values = new List<MetricValue>();
        private double _sum = 0;
        private double _min = double.MaxValue;
        private double _max = double.MinValue;
        private int _count = 0;
        
        public MetricTracker(string name)
        {
            _name = name;
        }
        
        public void RecordValue(double value)
        {
            lock (_lock)
            {
                _values.Add(new MetricValue { Value = value, Timestamp = DateTime.UtcNow });
                _sum += value;
                _count++;
                
                if (value < _min) _min = value;
                if (value > _max) _max = value;
                
                // Keep only recent values (last 1000 or last hour)
                var cutoff = DateTime.UtcNow.AddHours(-1);
                _values.RemoveAll(v => v.Timestamp < cutoff && _values.Count > 1000);
            }
        }
        
        public MetricSnapshot GetSnapshot()
        {
            lock (_lock)
            {
                return new MetricSnapshot
                {
                    Name = _name,
                    Total = _sum,
                    Count = _count,
                    Average = _count > 0 ? _sum / _count : 0,
                    Min = _min == double.MaxValue ? 0 : _min,
                    Max = _max == double.MinValue ? 0 : _max,
                    LastUpdated = _values.LastOrDefault()?.Timestamp ?? DateTime.MinValue
                };
            }
        }
        
        public double GetRecentCount(TimeSpan window)
        {
            lock (_lock)
            {
                var cutoff = DateTime.UtcNow - window;
                return _values.Where(v => v.Timestamp >= cutoff).Sum(v => v.Value);
            }
        }
    }
    
    /// <summary>
    /// Log entry structure
    /// </summary>
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string EventType { get; set; }
        public object Data { get; set; }
        public string ConnectionId { get; set; }
        public string OperationName { get; set; }
        public int ThreadId { get; set; }
        public int ProcessId { get; set; }
    }
    
    /// <summary>
    /// Log levels
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3,
        Critical = 4
    }
    
    /// <summary>
    /// Metric value with timestamp
    /// </summary>
    public class MetricValue
    {
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// Metric snapshot
    /// </summary>
    public class MetricSnapshot
    {
        public string Name { get; set; }
        public double Total { get; set; }
        public int Count { get; set; }
        public double Average { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public DateTime LastUpdated { get; set; }
    }
    
    /// <summary>
    /// Performance summary for an operation
    /// </summary>
    public class PerformanceSummary
    {
        public string OperationName { get; set; }
        public double AverageDuration { get; set; }
        public double MinDuration { get; set; }
        public double MaxDuration { get; set; }
        public double TotalExecutions { get; set; }
        public double SuccessCount { get; set; }
        public double FailureCount { get; set; }
        public double SuccessRate { get; set; }
    }
    
    /// <summary>
    /// Health status information
    /// </summary>
    public class HealthStatus
    {
        public DateTime Timestamp { get; set; }
        public string ConnectionStatus { get; set; }
        public double ErrorsLast5Minutes { get; set; }
        public double WarningsLast5Minutes { get; set; }
        public bool IsHealthy { get; set; }
        public TimeSpan Uptime { get; set; }
    }
    
    /// <summary>
    /// Log event arguments
    /// </summary>
    public class LogEventArgs : EventArgs
    {
        public LogEntry LogEntry { get; }
        
        public LogEventArgs(LogEntry logEntry)
        {
            LogEntry = logEntry;
        }
    }
    
    /// <summary>
    /// Metrics event arguments
    /// </summary>
    public class MetricsEventArgs : EventArgs
    {
        public Dictionary<string, MetricSnapshot> Metrics { get; }
        
        public MetricsEventArgs(Dictionary<string, MetricSnapshot> metrics)
        {
            Metrics = metrics;
        }
    }
} 