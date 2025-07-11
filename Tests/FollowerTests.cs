using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Follower;

namespace Follower.Tests
{
    [TestClass]
    public class NetworkSecurityTests
    {
        private NetworkSecurity _networkSecurity;
        private const string TestApiKey = "test-api-key-12345";
        
        [TestInitialize]
        public void Setup()
        {
            _networkSecurity = new NetworkSecurity(TestApiKey);
        }
        
        [TestMethod]
        public void EncryptMessage_ValidMessage_ReturnsEncryptedString()
        {
            // Arrange
            var message = "Test message";
            var recipientId = "test-recipient";
            
            // Act
            var encrypted = _networkSecurity.EncryptMessage(message, recipientId);
            
            // Assert
            Assert.IsNotNull(encrypted);
            Assert.AreNotEqual(message, encrypted);
            Assert.IsTrue(encrypted.Contains("|")); // Should contain integrity hash
        }
        
        [TestMethod]
        public void DecryptMessage_ValidEncryptedMessage_ReturnsOriginalMessage()
        {
            // Arrange
            var originalMessage = "Test message";
            var recipientId = "test-recipient";
            var encrypted = _networkSecurity.EncryptMessage(originalMessage, recipientId);
            
            // Act
            var decrypted = _networkSecurity.DecryptMessage(encrypted, recipientId);
            
            // Assert
            Assert.AreEqual(originalMessage, decrypted);
        }
        
        [TestMethod]
        [ExpectedException(typeof(SecurityException))]
        public void DecryptMessage_TamperedMessage_ThrowsSecurityException()
        {
            // Arrange
            var message = "Test message";
            var recipientId = "test-recipient";
            var encrypted = _networkSecurity.EncryptMessage(message, recipientId);
            var tamperedMessage = encrypted.Replace(encrypted[0], 'X'); // Tamper with message
            
            // Act
            _networkSecurity.DecryptMessage(tamperedMessage, recipientId);
        }
        
        [TestMethod]
        public void GenerateAuthToken_ValidParameters_ReturnsValidToken()
        {
            // Arrange
            var leaderId = "test-leader";
            var validFor = TimeSpan.FromHours(1);
            
            // Act
            var token = _networkSecurity.GenerateAuthToken(leaderId, validFor);
            
            // Assert
            Assert.IsNotNull(token);
            Assert.IsTrue(token.Split('.').Length == 3); // JWT format
        }
        
        [TestMethod]
        public void ValidateAuthToken_ValidToken_ReturnsTrue()
        {
            // Arrange
            var leaderId = "test-leader";
            var validFor = TimeSpan.FromHours(1);
            var token = _networkSecurity.GenerateAuthToken(leaderId, validFor);
            
            // Act
            var isValid = _networkSecurity.ValidateAuthToken(token, leaderId);
            
            // Assert
            Assert.IsTrue(isValid);
        }
        
        [TestMethod]
        public void ValidateAuthToken_ExpiredToken_ReturnsFalse()
        {
            // Arrange
            var leaderId = "test-leader";
            var validFor = TimeSpan.FromMilliseconds(1);
            var token = _networkSecurity.GenerateAuthToken(leaderId, validFor);
            
            // Act
            Task.Delay(10).Wait(); // Wait for token to expire
            var isValid = _networkSecurity.ValidateAuthToken(token, leaderId);
            
            // Assert
            Assert.IsFalse(isValid);
        }
        
        [TestMethod]
        public void IsMessageAlreadyProcessed_NewMessage_ReturnsFalse()
        {
            // Arrange
            var messageId = "test-message-1";
            
            // Act
            var isProcessed = _networkSecurity.IsMessageAlreadyProcessed(messageId);
            
            // Assert
            Assert.IsFalse(isProcessed);
        }
        
        [TestMethod]
        public void IsMessageAlreadyProcessed_ProcessedMessage_ReturnsTrue()
        {
            // Arrange
            var messageId = "test-message-1";
            _networkSecurity.MarkMessageAsProcessed(messageId);
            
            // Act
            var isProcessed = _networkSecurity.IsMessageAlreadyProcessed(messageId);
            
            // Assert
            Assert.IsTrue(isProcessed);
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _networkSecurity?.Dispose();
        }
    }
    
    [TestClass]
    public class NetworkReliabilityTests
    {
        private NetworkReliability _networkReliability;
        
        [TestInitialize]
        public void Setup()
        {
            _networkReliability = new NetworkReliability();
        }
        
        [TestMethod]
        public async Task ExecuteWithRetry_SuccessfulOperation_ReturnsResult()
        {
            // Arrange
            var expectedResult = "success";
            Func<Task<string>> operation = () => Task.FromResult(expectedResult);
            
            // Act
            var result = await _networkReliability.ExecuteWithRetry(operation, "test-operation");
            
            // Assert
            Assert.AreEqual(expectedResult, result);
        }
        
        [TestMethod]
        public async Task ExecuteWithRetry_FailingOperation_RetriesAndSucceeds()
        {
            // Arrange
            var attemptCount = 0;
            var expectedResult = "success";
            Func<Task<string>> operation = () => 
            {
                attemptCount++;
                if (attemptCount < 3)
                    throw new InvalidOperationException("Test failure");
                return Task.FromResult(expectedResult);
            };
            
            // Act
            var result = await _networkReliability.ExecuteWithRetry(operation, "test-operation");
            
            // Assert
            Assert.AreEqual(expectedResult, result);
            Assert.AreEqual(3, attemptCount);
        }
        
        [TestMethod]
        [ExpectedException(typeof(MaxRetriesExceededException))]
        public async Task ExecuteWithRetry_AlwaysFailingOperation_ThrowsMaxRetriesExceeded()
        {
            // Arrange
            Func<Task<string>> operation = () => throw new InvalidOperationException("Always fails");
            
            // Act
            await _networkReliability.ExecuteWithRetry(operation, "test-operation");
        }
        
        [TestMethod]
        public async Task ExecuteBatch_MultipleOperations_ExecutesAllSuccessfully()
        {
            // Arrange
            var operations = new List<Func<Task<string>>>
            {
                () => Task.FromResult("result1"),
                () => Task.FromResult("result2"),
                () => Task.FromResult("result3")
            };
            
            // Act
            var results = await _networkReliability.ExecuteBatch(operations, "test-batch");
            
            // Assert
            Assert.AreEqual(3, results.Count());
            Assert.IsTrue(results.Contains("result1"));
            Assert.IsTrue(results.Contains("result2"));
            Assert.IsTrue(results.Contains("result3"));
        }
        
        [TestMethod]
        public void GetHealthReport_NewInstance_ReturnsHealthyReport()
        {
            // Act
            var report = _networkReliability.GetHealthReport();
            
            // Assert
            Assert.IsNotNull(report);
            Assert.IsTrue(report.IsHealthy);
            Assert.AreEqual(0, report.TotalFailures);
        }
        
        [TestMethod]
        public void ResetAllCircuitBreakers_AfterFailures_ResetsSuccessfully()
        {
            // Arrange
            // Simulate some failures first
            var operation = new Func<Task<string>>(() => throw new InvalidOperationException("Test"));
            try
            {
                _networkReliability.ExecuteWithRetry(operation, "test-operation").Wait();
            }
            catch
            {
                // Expected to fail
            }
            
            // Act
            _networkReliability.ResetAllCircuitBreakers();
            var report = _networkReliability.GetHealthReport();
            
            // Assert
            Assert.IsTrue(report.IsHealthy);
        }
    }
    
    [TestClass]
    public class NetworkLoggerTests
    {
        private NetworkLogger _networkLogger;
        private const string TestConnectionId = "test-connection-123";
        
        [TestInitialize]
        public void Setup()
        {
            _networkLogger = new NetworkLogger(TestConnectionId);
        }
        
        [TestMethod]
        public void LogNetworkEvent_ValidEvent_LogsSuccessfully()
        {
            // Arrange
            var eventType = "TEST_EVENT";
            var data = new { Message = "Test message" };
            
            // Act
            _networkLogger.LogNetworkEvent(eventType, data);
            
            // Assert
            var metrics = _networkLogger.GetMetrics();
            Assert.IsTrue(metrics.ContainsKey("log.info"));
            Assert.IsTrue(metrics["log.info"].Count > 0);
        }
        
        [TestMethod]
        public void LogError_WithException_RecordsErrorMetrics()
        {
            // Arrange
            var message = "Test error";
            var exception = new Exception("Test exception");
            
            // Act
            _networkLogger.LogError(message, exception);
            
            // Assert
            var metrics = _networkLogger.GetMetrics();
            Assert.IsTrue(metrics.ContainsKey("errors.total"));
            Assert.IsTrue(metrics["errors.total"].Count > 0);
        }
        
        [TestMethod]
        public void LogPerformanceEvent_ValidOperation_RecordsPerformanceMetrics()
        {
            // Arrange
            var operationName = "test-operation";
            var duration = TimeSpan.FromMilliseconds(100);
            
            // Act
            _networkLogger.LogPerformanceEvent(operationName, duration, true);
            
            // Assert
            var metrics = _networkLogger.GetMetrics();
            Assert.IsTrue(metrics.ContainsKey($"performance.{operationName}.duration"));
            Assert.IsTrue(metrics.ContainsKey($"performance.{operationName}.success"));
        }
        
        [TestMethod]
        public void GetHealthStatus_NewInstance_ReturnsHealthyStatus()
        {
            // Act
            var status = _networkLogger.GetHealthStatus();
            
            // Assert
            Assert.IsNotNull(status);
            Assert.IsTrue(status.IsHealthy);
            Assert.AreEqual(0, status.ErrorsLast5Minutes);
        }
        
        [TestMethod]
        public void GetPerformanceSummary_WithOperations_ReturnsValidSummary()
        {
            // Arrange
            var operationName = "test-operation";
            _networkLogger.LogPerformanceEvent(operationName, TimeSpan.FromMilliseconds(100), true);
            _networkLogger.LogPerformanceEvent(operationName, TimeSpan.FromMilliseconds(200), true);
            _networkLogger.LogPerformanceEvent(operationName, TimeSpan.FromMilliseconds(150), false);
            
            // Act
            var summary = _networkLogger.GetPerformanceSummary(operationName);
            
            // Assert
            Assert.IsNotNull(summary);
            Assert.AreEqual(operationName, summary.OperationName);
            Assert.AreEqual(3, summary.TotalExecutions);
            Assert.AreEqual(2, summary.SuccessCount);
            Assert.AreEqual(1, summary.FailureCount);
            Assert.IsTrue(summary.SuccessRate > 0);
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _networkLogger?.Dispose();
        }
    }
    
    [TestClass]
    public class EnhancedMessageProtocolTests
    {
        private EnhancedMessageProtocol _messageProtocol;
        private const string TestNodeId = "test-node-123";
        
        [TestInitialize]
        public void Setup()
        {
            _messageProtocol = new EnhancedMessageProtocol(TestNodeId);
        }
        
        [TestMethod]
        public void CreateMessage_ValidParameters_ReturnsValidMessage()
        {
            // Arrange
            var messageType = "TEST_MESSAGE";
            var payload = new { Data = "test data" };
            
            // Act
            var message = _messageProtocol.CreateMessage(messageType, payload);
            
            // Assert
            Assert.IsNotNull(message);
            Assert.AreEqual(messageType, message.MessageType);
            Assert.AreEqual(TestNodeId, message.SenderId);
            Assert.IsTrue(message.SequenceNumber > 0);
            Assert.IsNotNull(message.Checksum);
        }
        
        [TestMethod]
        public void ProcessIncomingMessage_ValidMessage_ReturnsSuccess()
        {
            // Arrange
            var message = _messageProtocol.CreateMessage("TEST", new { Data = "test" });
            
            // Act
            var result = _messageProtocol.ProcessIncomingMessage(message);
            
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(MessageProcessingStatus.Processed, result.Status);
        }
        
        [TestMethod]
        public void ProcessIncomingMessage_DuplicateMessage_ReturnsDuplicate()
        {
            // Arrange
            var message = _messageProtocol.CreateMessage("TEST", new { Data = "test" });
            _messageProtocol.ProcessIncomingMessage(message); // Process once
            
            // Act
            var result = _messageProtocol.ProcessIncomingMessage(message); // Process again
            
            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(MessageProcessingStatus.Duplicate, result.Status);
        }
        
        [TestMethod]
        public void ProcessIncomingMessage_ExpiredMessage_ReturnsExpired()
        {
            // Arrange
            var message = _messageProtocol.CreateMessage("TEST", new { Data = "test" });
            message.Timestamp = DateTime.UtcNow.AddDays(-1); // Set to past
            message.TimeToLive = TimeSpan.FromMinutes(1); // Short TTL
            
            // Act
            var result = _messageProtocol.ProcessIncomingMessage(message);
            
            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(MessageProcessingStatus.Expired, result.Status);
        }
        
        [TestMethod]
        public void QueueOutgoingMessage_ValidMessage_QueuesSuccessfully()
        {
            // Arrange
            var message = _messageProtocol.CreateMessage("TEST", new { Data = "test" });
            
            // Act
            _messageProtocol.QueueOutgoingMessage(message);
            var nextMessage = _messageProtocol.GetNextOutgoingMessage();
            
            // Assert
            Assert.IsNotNull(nextMessage);
            Assert.AreEqual(message.MessageId, nextMessage.MessageId);
        }
        
        [TestMethod]
        public void AcknowledgeMessage_ValidMessageId_RemovesFromPending()
        {
            // Arrange
            var message = _messageProtocol.CreateMessage("TEST", new { Data = "test" });
            _messageProtocol.QueueOutgoingMessage(message);
            _messageProtocol.GetNextOutgoingMessage(); // Dequeue it
            
            // Act
            _messageProtocol.AcknowledgeMessage(message.MessageId);
            
            // Assert
            var stats = _messageProtocol.GetStatistics();
            Assert.AreEqual(0, stats.PendingMessagesCount);
        }
        
        [TestMethod]
        public void GetStatistics_AfterOperations_ReturnsValidStats()
        {
            // Arrange
            var message = _messageProtocol.CreateMessage("TEST", new { Data = "test" });
            _messageProtocol.ProcessIncomingMessage(message);
            _messageProtocol.QueueOutgoingMessage(message);
            
            // Act
            var stats = _messageProtocol.GetStatistics();
            
            // Assert
            Assert.IsNotNull(stats);
            Assert.AreEqual(TestNodeId, stats.NodeId);
            Assert.AreEqual(2, stats.ProtocolVersion);
            Assert.IsTrue(stats.ProcessedMessagesCount > 0);
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _messageProtocol?.Dispose();
        }
    }
    
    [TestClass]
    public class ConfigurationValidatorTests
    {
        private ConfigurationValidator _validator;
        private FollowerSettings _settings;
        
        [TestInitialize]
        public void Setup()
        {
            _settings = new FollowerSettings();
            _validator = new ConfigurationValidator(_settings);
        }
        
        [TestMethod]
        public async Task ValidateAllSettings_DefaultSettings_ReturnsValidationReport()
        {
            // Act
            var report = await _validator.ValidateAllSettings();
            
            // Assert
            Assert.IsNotNull(report);
            Assert.IsTrue(report.ValidationResults.Count > 0);
        }
        
        [TestMethod]
        public async Task ValidateAllSettings_InvalidPortRange_ReturnsError()
        {
            // Arrange
            _settings.EnableNetworkCommunication.Value = true;
            _settings.LeaderPort.Value = 99999; // Invalid port
            
            // Act
            var report = await _validator.ValidateAllSettings();
            
            // Assert
            Assert.IsTrue(report.ValidationResults.Any(r => r.Category == "Network" && !r.IsValid));
        }
        
        [TestMethod]
        public async Task ValidateAllSettings_ValidConfiguration_ReturnsSuccess()
        {
            // Arrange
            _settings.LeaderName.Value = "TestLeader";
            _settings.EnableNetworkCommunication.Value = false; // Disable to avoid connectivity tests
            _settings.BotInputFrequency.Value = 50;
            _settings.PathfindingNodeDistance.Value = 100;
            _settings.ClearPathDistance.Value = 200;
            
            // Act
            var report = await _validator.ValidateAllSettings();
            
            // Assert
            Assert.IsTrue(report.ValidationResults.Any(r => r.IsValid));
        }
        
        [TestMethod]
        public async Task AutoConfigureOptimalSettings_ValidEnvironment_ReturnsConfiguration()
        {
            // Act
            var result = await _validator.AutoConfigureOptimalSettings();
            
            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.NetworkConfiguration);
            Assert.IsNotNull(result.PerformanceConfiguration);
            Assert.IsNotNull(result.SecurityConfiguration);
        }
    }
    
    [TestClass]
    public class IntegrationTests
    {
        [TestMethod]
        public async Task FullSystemIntegration_CompleteWorkflow_ExecutesSuccessfully()
        {
            // Arrange
            var settings = new FollowerSettings();
            var connectionId = "integration-test-connection";
            
            // Initialize all components
            using var networkLogger = new NetworkLogger(connectionId);
            using var networkSecurity = new NetworkSecurity("integration-test-key");
            var networkReliability = new NetworkReliability();
            using var messageProtocol = new EnhancedMessageProtocol(connectionId);
            var validator = new ConfigurationValidator(settings);
            
            // Act & Assert - Test component initialization
            Assert.IsNotNull(networkLogger);
            Assert.IsNotNull(networkSecurity);
            Assert.IsNotNull(networkReliability);
            Assert.IsNotNull(messageProtocol);
            Assert.IsNotNull(validator);
            
            // Test configuration validation
            var validationReport = await validator.ValidateAllSettings();
            Assert.IsNotNull(validationReport);
            
            // Test message creation and processing
            var message = messageProtocol.CreateMessage("INTEGRATION_TEST", new { TestData = "success" });
            var processingResult = messageProtocol.ProcessIncomingMessage(message);
            Assert.IsTrue(processingResult.IsSuccess);
            
            // Test logging
            networkLogger.LogNetworkEvent("INTEGRATION_TEST", new { Status = "Success" });
            var healthStatus = networkLogger.GetHealthStatus();
            Assert.IsTrue(healthStatus.IsHealthy);
            
            // Test encryption/decryption
            var testMessage = "Integration test message";
            var encrypted = networkSecurity.EncryptMessage(testMessage, "test-recipient");
            var decrypted = networkSecurity.DecryptMessage(encrypted, "test-recipient");
            Assert.AreEqual(testMessage, decrypted);
            
            // Test reliability system
            var reliabilityResult = await networkReliability.ExecuteWithRetry(
                () => Task.FromResult("reliability-test-success"),
                "integration-test"
            );
            Assert.AreEqual("reliability-test-success", reliabilityResult);
            
            // Test protocol statistics
            var stats = messageProtocol.GetStatistics();
            Assert.IsTrue(stats.ProcessedMessagesCount > 0);
            
            // Test auto-configuration
            var autoConfigResult = await validator.AutoConfigureOptimalSettings();
            Assert.IsTrue(autoConfigResult.IsSuccessful);
        }
        
        [TestMethod]
        public async Task NetworkCommunicationFlow_EndToEnd_WorksCorrectly()
        {
            // Arrange
            using var senderProtocol = new EnhancedMessageProtocol("sender-node");
            using var receiverProtocol = new EnhancedMessageProtocol("receiver-node");
            
            // Act
            // Create and queue message
            var testMessage = senderProtocol.CreateMessage("TEST_COMMAND", new { Command = "STASH_ITEMS" });
            senderProtocol.QueueOutgoingMessage(testMessage);
            
            // Get message for sending
            var messageToSend = senderProtocol.GetNextOutgoingMessage();
            Assert.IsNotNull(messageToSend);
            
            // Process message on receiver
            var processingResult = receiverProtocol.ProcessIncomingMessage(messageToSend);
            Assert.IsTrue(processingResult.IsSuccess);
            
            // Get processed messages
            var incomingMessages = receiverProtocol.GetIncomingMessages();
            Assert.IsTrue(incomingMessages.Any());
            
            // Acknowledge message
            senderProtocol.AcknowledgeMessage(messageToSend.MessageId);
            
            // Verify statistics
            var senderStats = senderProtocol.GetStatistics();
            var receiverStats = receiverProtocol.GetStatistics();
            
            Assert.AreEqual(0, senderStats.PendingMessagesCount);
            Assert.IsTrue(receiverStats.ProcessedMessagesCount > 0);
        }
        
        [TestMethod]
        public async Task SecurityWorkflow_AuthenticationAndEncryption_WorksCorrectly()
        {
            // Arrange
            using var security = new NetworkSecurity("security-test-key");
            var leaderId = "test-leader";
            
            // Act
            // Generate authentication token
            var authToken = security.GenerateAuthToken(leaderId, TimeSpan.FromHours(1));
            Assert.IsNotNull(authToken);
            
            // Validate authentication token
            var isValidToken = security.ValidateAuthToken(authToken, leaderId);
            Assert.IsTrue(isValidToken);
            
            // Test message encryption/decryption
            var sensitiveData = "This is sensitive command data";
            var encrypted = security.EncryptMessage(sensitiveData, leaderId);
            var decrypted = security.DecryptMessage(encrypted, leaderId);
            
            Assert.AreEqual(sensitiveData, decrypted);
            
            // Test replay protection
            var messageId = "test-message-123";
            Assert.IsFalse(security.IsMessageAlreadyProcessed(messageId));
            
            security.MarkMessageAsProcessed(messageId);
            Assert.IsTrue(security.IsMessageAlreadyProcessed(messageId));
        }
        
        [TestMethod]
        public async Task ErrorRecoveryWorkflow_CircuitBreakerAndRetry_WorksCorrectly()
        {
            // Arrange
            var reliability = new NetworkReliability();
            var attemptCount = 0;
            
            // Act
            // Test successful retry after failures
            var result = await reliability.ExecuteWithRetry(async () =>
            {
                attemptCount++;
                if (attemptCount < 3)
                    throw new InvalidOperationException("Simulated failure");
                return "success-after-retries";
            }, "test-operation");
            
            Assert.AreEqual("success-after-retries", result);
            Assert.AreEqual(3, attemptCount);
            
            // Test health reporting
            var healthReport = reliability.GetHealthReport();
            Assert.IsNotNull(healthReport);
            
            // Test circuit breaker reset
            reliability.ResetAllCircuitBreakers();
            var healthAfterReset = reliability.GetHealthReport();
            Assert.IsTrue(healthAfterReset.IsHealthy);
        }
    }
    
    [TestClass]
    public class PerformanceTests
    {
        [TestMethod]
        public async Task MessageProtocol_HighVolumeMessages_MaintainsPerformance()
        {
            // Arrange
            using var protocol = new EnhancedMessageProtocol("performance-test-node");
            var messageCount = 1000;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Act
            for (int i = 0; i < messageCount; i++)
            {
                var message = protocol.CreateMessage("PERFORMANCE_TEST", new { Index = i });
                protocol.ProcessIncomingMessage(message);
            }
            
            stopwatch.Stop();
            
            // Assert
            var stats = protocol.GetStatistics();
            Assert.AreEqual(messageCount, stats.ProcessedMessagesCount);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000); // Should complete in under 5 seconds
        }
        
        [TestMethod]
        public async Task NetworkLogger_HighVolumeLogging_MaintainsPerformance()
        {
            // Arrange
            using var logger = new NetworkLogger("performance-test-logger");
            var logCount = 1000;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Act
            for (int i = 0; i < logCount; i++)
            {
                logger.LogNetworkEvent("PERFORMANCE_TEST", new { Index = i });
            }
            
            stopwatch.Stop();
            
            // Assert
            var metrics = logger.GetMetrics();
            Assert.IsTrue(metrics.ContainsKey("log.info"));
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2000); // Should complete in under 2 seconds
        }
        
        [TestMethod]
        public async Task Encryption_HighVolumeOperations_MaintainsPerformance()
        {
            // Arrange
            using var security = new NetworkSecurity("performance-test-key");
            var operationCount = 100;
            var testMessage = "Performance test message that is somewhat longer to test encryption performance";
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Act
            for (int i = 0; i < operationCount; i++)
            {
                var encrypted = security.EncryptMessage(testMessage, $"recipient-{i}");
                var decrypted = security.DecryptMessage(encrypted, $"recipient-{i}");
                Assert.AreEqual(testMessage, decrypted);
            }
            
            stopwatch.Stop();
            
            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000); // Should complete in under 5 seconds
        }
    }
} 