using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Follower
{
    /// <summary>
    /// Enhanced message protocol V2 with advanced features
    /// </summary>
    public class EnhancedMessageProtocol
    {
        private readonly ConcurrentDictionary<string, ProcessedMessage> _processedMessages = new ConcurrentDictionary<string, ProcessedMessage>();
        private readonly ConcurrentDictionary<string, PendingMessage> _pendingMessages = new ConcurrentDictionary<string, PendingMessage>();
        private readonly ConcurrentQueue<NetworkMessageV2> _incomingMessages = new ConcurrentQueue<NetworkMessageV2>();
        private readonly ConcurrentQueue<NetworkMessageV2> _outgoingMessages = new ConcurrentQueue<NetworkMessageV2>();
        private readonly Timer _cleanupTimer;
        private readonly object _protocolLock = new object();
        private readonly string _nodeId;
        private int _sequenceNumber = 0;
        private int _protocolVersion = 2;
        
        public event EventHandler<MessageReceivedEventArgs> MessageReceived;
        public event EventHandler<MessageSentEventArgs> MessageSent;
        public event EventHandler<MessageErrorEventArgs> MessageError;
        
        public EnhancedMessageProtocol(string nodeId)
        {
            _nodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            
            // Start cleanup timer to remove old processed messages
            _cleanupTimer = new Timer(CleanupProcessedMessages, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }
        
        /// <summary>
        /// Creates a new message with protocol features
        /// </summary>
        public NetworkMessageV2 CreateMessage(string messageType, object payload, MessagePriority priority = MessagePriority.Normal)
        {
            var message = new NetworkMessageV2
            {
                MessageId = GenerateMessageId(),
                SequenceNumber = Interlocked.Increment(ref _sequenceNumber),
                MessageType = messageType,
                ProtocolVersion = _protocolVersion,
                SenderId = _nodeId,
                Timestamp = DateTime.UtcNow,
                Priority = priority,
                Payload = payload,
                RetryCount = 0,
                MaxRetries = GetMaxRetriesForPriority(priority),
                TimeToLive = GetTtlForPriority(priority)
            };
            
            // Add checksum for integrity
            message.Checksum = ComputeChecksum(message);
            
            return message;
        }
        
        /// <summary>
        /// Processes an incoming message
        /// </summary>
        public MessageProcessingResult ProcessIncomingMessage(NetworkMessageV2 message)
        {
            try
            {
                // Basic validation
                if (message == null)
                    return MessageProcessingResult.Invalid("Message is null");
                
                if (string.IsNullOrEmpty(message.MessageId))
                    return MessageProcessingResult.Invalid("Message ID is required");
                
                // Protocol version check
                if (message.ProtocolVersion > _protocolVersion)
                    return MessageProcessingResult.Invalid($"Unsupported protocol version: {message.ProtocolVersion}");
                
                // Integrity check
                var expectedChecksum = ComputeChecksum(message);
                if (message.Checksum != expectedChecksum)
                    return MessageProcessingResult.Invalid("Message integrity check failed");
                
                // TTL check
                if (message.Timestamp.Add(message.TimeToLive) < DateTime.UtcNow)
                    return MessageProcessingResult.Expired("Message has expired");
                
                // Duplicate detection
                if (IsMessageAlreadyProcessed(message.MessageId))
                    return MessageProcessingResult.Duplicate("Message already processed");
                
                // Mark as processed
                MarkMessageAsProcessed(message);
                
                // Add to incoming queue for ordered processing
                _incomingMessages.Enqueue(message);
                
                // Trigger event
                MessageReceived?.Invoke(this, new MessageReceivedEventArgs(message));
                
                return MessageProcessingResult.Success();
            }
            catch (Exception ex)
            {
                var error = MessageProcessingResult.Error($"Error processing message: {ex.Message}");
                MessageError?.Invoke(this, new MessageErrorEventArgs(message, ex));
                return error;
            }
        }
        
        /// <summary>
        /// Queues a message for sending
        /// </summary>
        public void QueueOutgoingMessage(NetworkMessageV2 message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            
            // Add to pending messages for tracking
            _pendingMessages.TryAdd(message.MessageId, new PendingMessage
            {
                Message = message,
                QueuedAt = DateTime.UtcNow,
                AttemptsRemaining = message.MaxRetries
            });
            
            // Add to outgoing queue
            _outgoingMessages.Enqueue(message);
        }
        
        /// <summary>
        /// Gets the next message to send (priority-based)
        /// </summary>
        public NetworkMessageV2 GetNextOutgoingMessage()
        {
            var messages = new List<NetworkMessageV2>();
            
            // Drain the queue
            while (_outgoingMessages.TryDequeue(out var message))
            {
                messages.Add(message);
            }
            
            if (!messages.Any()) return null;
            
            // Sort by priority and then by sequence number
            var sortedMessages = messages
                .OrderBy(m => (int)m.Priority)
                .ThenBy(m => m.SequenceNumber)
                .ToList();
            
            // Put non-priority messages back
            foreach (var message in sortedMessages.Skip(1))
            {
                _outgoingMessages.Enqueue(message);
            }
            
            var nextMessage = sortedMessages.First();
            
            // Update retry tracking
            if (_pendingMessages.TryGetValue(nextMessage.MessageId, out var pendingMessage))
            {
                pendingMessage.LastSentAt = DateTime.UtcNow;
                pendingMessage.AttemptsRemaining--;
            }
            
            return nextMessage;
        }
        
        /// <summary>
        /// Acknowledges receipt of a message
        /// </summary>
        public void AcknowledgeMessage(string messageId)
        {
            if (_pendingMessages.TryRemove(messageId, out var pendingMessage))
            {
                MessageSent?.Invoke(this, new MessageSentEventArgs(pendingMessage.Message, true));
            }
        }
        
        /// <summary>
        /// Reports a message send failure
        /// </summary>
        public void ReportMessageFailure(string messageId, string error)
        {
            if (_pendingMessages.TryGetValue(messageId, out var pendingMessage))
            {
                pendingMessage.LastError = error;
                pendingMessage.FailureCount++;
                
                // Check if we should retry
                if (pendingMessage.AttemptsRemaining <= 0)
                {
                    // Remove from pending and report final failure
                    _pendingMessages.TryRemove(messageId, out _);
                    MessageSent?.Invoke(this, new MessageSentEventArgs(pendingMessage.Message, false));
                    MessageError?.Invoke(this, new MessageErrorEventArgs(pendingMessage.Message, new Exception(error)));
                }
                else
                {
                    // Queue for retry
                    var retryMessage = CloneMessage(pendingMessage.Message);
                    retryMessage.RetryCount++;
                    _outgoingMessages.Enqueue(retryMessage);
                }
            }
        }
        
        /// <summary>
        /// Gets messages that need to be retried
        /// </summary>
        public IEnumerable<NetworkMessageV2> GetMessagesForRetry()
        {
            var now = DateTime.UtcNow;
            var retryMessages = new List<NetworkMessageV2>();
            
            foreach (var kvp in _pendingMessages)
            {
                var pending = kvp.Value;
                
                // Check if message needs retry
                if (pending.AttemptsRemaining > 0 && 
                    now - pending.LastSentAt > GetRetryDelay(pending.Message.RetryCount))
                {
                    var retryMessage = CloneMessage(pending.Message);
                    retryMessage.RetryCount++;
                    retryMessages.Add(retryMessage);
                }
            }
            
            return retryMessages;
        }
        
        /// <summary>
        /// Gets incoming messages in order
        /// </summary>
        public IEnumerable<NetworkMessageV2> GetIncomingMessages()
        {
            var messages = new List<NetworkMessageV2>();
            
            // Drain incoming queue
            while (_incomingMessages.TryDequeue(out var message))
            {
                messages.Add(message);
            }
            
            // Sort by sequence number for ordered processing
            return messages.OrderBy(m => m.SequenceNumber);
        }
        
        /// <summary>
        /// Gets protocol statistics
        /// </summary>
        public ProtocolStatistics GetStatistics()
        {
            return new ProtocolStatistics
            {
                ProcessedMessagesCount = _processedMessages.Count,
                PendingMessagesCount = _pendingMessages.Count,
                IncomingQueueSize = _incomingMessages.Count,
                OutgoingQueueSize = _outgoingMessages.Count,
                CurrentSequenceNumber = _sequenceNumber,
                ProtocolVersion = _protocolVersion,
                NodeId = _nodeId
            };
        }
        
        private bool IsMessageAlreadyProcessed(string messageId)
        {
            return _processedMessages.ContainsKey(messageId);
        }
        
        private void MarkMessageAsProcessed(NetworkMessageV2 message)
        {
            _processedMessages.TryAdd(message.MessageId, new ProcessedMessage
            {
                MessageId = message.MessageId,
                ProcessedAt = DateTime.UtcNow,
                SenderId = message.SenderId,
                MessageType = message.MessageType
            });
        }
        
        private string GenerateMessageId()
        {
            return $"{_nodeId}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        }
        
        private string ComputeChecksum(NetworkMessageV2 message)
        {
            // Create a copy without checksum for hashing
            var messageForHash = new
            {
                message.MessageId,
                message.SequenceNumber,
                message.MessageType,
                message.ProtocolVersion,
                message.SenderId,
                message.Timestamp,
                message.Priority,
                Payload = JsonConvert.SerializeObject(message.Payload),
                message.RetryCount
            };
            
            var json = JsonConvert.SerializeObject(messageForHash);
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
                return Convert.ToBase64String(hash);
            }
        }
        
        private int GetMaxRetriesForPriority(MessagePriority priority)
        {
            return priority switch
            {
                MessagePriority.Critical => 5,
                MessagePriority.High => 3,
                MessagePriority.Normal => 2,
                MessagePriority.Low => 1,
                _ => 2
            };
        }
        
        private TimeSpan GetTtlForPriority(MessagePriority priority)
        {
            return priority switch
            {
                MessagePriority.Critical => TimeSpan.FromMinutes(30),
                MessagePriority.High => TimeSpan.FromMinutes(15),
                MessagePriority.Normal => TimeSpan.FromMinutes(10),
                MessagePriority.Low => TimeSpan.FromMinutes(5),
                _ => TimeSpan.FromMinutes(10)
            };
        }
        
        private TimeSpan GetRetryDelay(int retryCount)
        {
            // Exponential backoff with jitter
            var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
            var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, 1000));
            return baseDelay + jitter;
        }
        
        private NetworkMessageV2 CloneMessage(NetworkMessageV2 original)
        {
            return new NetworkMessageV2
            {
                MessageId = original.MessageId,
                SequenceNumber = original.SequenceNumber,
                MessageType = original.MessageType,
                ProtocolVersion = original.ProtocolVersion,
                SenderId = original.SenderId,
                Timestamp = original.Timestamp,
                Priority = original.Priority,
                Payload = original.Payload,
                RetryCount = original.RetryCount,
                MaxRetries = original.MaxRetries,
                TimeToLive = original.TimeToLive,
                Checksum = original.Checksum
            };
        }
        
        private void CleanupProcessedMessages(object state)
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddMinutes(-30);
                var toRemove = _processedMessages
                    .Where(kvp => kvp.Value.ProcessedAt < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var messageId in toRemove)
                {
                    _processedMessages.TryRemove(messageId, out _);
                }
            }
            catch (Exception ex)
            {
                // Log cleanup errors
                System.Diagnostics.Debug.WriteLine($"Error during message cleanup: {ex.Message}");
            }
        }
        
        public void Dispose()
        {
            _cleanupTimer?.Dispose();
        }
    }
    
    /// <summary>
    /// Enhanced network message V2
    /// </summary>
    public class NetworkMessageV2
    {
        public string MessageId { get; set; }
        public int SequenceNumber { get; set; }
        public string MessageType { get; set; }
        public int ProtocolVersion { get; set; }
        public string SenderId { get; set; }
        public DateTime Timestamp { get; set; }
        public MessagePriority Priority { get; set; }
        public object Payload { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetries { get; set; }
        public TimeSpan TimeToLive { get; set; }
        public string Checksum { get; set; }
    }
    
    /// <summary>
    /// Message priority levels
    /// </summary>
    public enum MessagePriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }
    
    /// <summary>
    /// Message processing result
    /// </summary>
    public class MessageProcessingResult
    {
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public MessageProcessingStatus Status { get; set; }
        
        public static MessageProcessingResult Success() => new MessageProcessingResult { IsSuccess = true, Status = MessageProcessingStatus.Processed };
        public static MessageProcessingResult Invalid(string message) => new MessageProcessingResult { IsSuccess = false, ErrorMessage = message, Status = MessageProcessingStatus.Invalid };
        public static MessageProcessingResult Duplicate(string message) => new MessageProcessingResult { IsSuccess = false, ErrorMessage = message, Status = MessageProcessingStatus.Duplicate };
        public static MessageProcessingResult Expired(string message) => new MessageProcessingResult { IsSuccess = false, ErrorMessage = message, Status = MessageProcessingStatus.Expired };
        public static MessageProcessingResult Error(string message) => new MessageProcessingResult { IsSuccess = false, ErrorMessage = message, Status = MessageProcessingStatus.Error };
    }
    
    /// <summary>
    /// Message processing status
    /// </summary>
    public enum MessageProcessingStatus
    {
        Processed,
        Invalid,
        Duplicate,
        Expired,
        Error
    }
    
    /// <summary>
    /// Processed message tracking
    /// </summary>
    public class ProcessedMessage
    {
        public string MessageId { get; set; }
        public DateTime ProcessedAt { get; set; }
        public string SenderId { get; set; }
        public string MessageType { get; set; }
    }
    
    /// <summary>
    /// Pending message tracking
    /// </summary>
    public class PendingMessage
    {
        public NetworkMessageV2 Message { get; set; }
        public DateTime QueuedAt { get; set; }
        public DateTime LastSentAt { get; set; }
        public int AttemptsRemaining { get; set; }
        public int FailureCount { get; set; }
        public string LastError { get; set; }
    }
    
    /// <summary>
    /// Protocol statistics
    /// </summary>
    public class ProtocolStatistics
    {
        public int ProcessedMessagesCount { get; set; }
        public int PendingMessagesCount { get; set; }
        public int IncomingQueueSize { get; set; }
        public int OutgoingQueueSize { get; set; }
        public int CurrentSequenceNumber { get; set; }
        public int ProtocolVersion { get; set; }
        public string NodeId { get; set; }
    }
    
    /// <summary>
    /// Message received event arguments
    /// </summary>
    public class MessageReceivedEventArgs : EventArgs
    {
        public NetworkMessageV2 Message { get; }
        
        public MessageReceivedEventArgs(NetworkMessageV2 message)
        {
            Message = message;
        }
    }
    
    /// <summary>
    /// Message sent event arguments
    /// </summary>
    public class MessageSentEventArgs : EventArgs
    {
        public NetworkMessageV2 Message { get; }
        public bool Success { get; }
        
        public MessageSentEventArgs(NetworkMessageV2 message, bool success)
        {
            Message = message;
            Success = success;
        }
    }
    
    /// <summary>
    /// Message error event arguments
    /// </summary>
    public class MessageErrorEventArgs : EventArgs
    {
        public NetworkMessageV2 Message { get; }
        public Exception Error { get; }
        
        public MessageErrorEventArgs(NetworkMessageV2 message, Exception error)
        {
            Message = message;
            Error = error;
        }
    }
} 