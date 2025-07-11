using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;

namespace Follower
{
    /// <summary>
    /// Enterprise-grade security layer for network communication
    /// </summary>
    public class NetworkSecurity : IDisposable
    {
        private readonly AesCryptoServiceProvider _aesProvider;
        private readonly RSACryptoServiceProvider _rsaProvider;
        private readonly Dictionary<string, DateTime> _processedMessages = new Dictionary<string, DateTime>();
        private readonly object _securityLock = new object();
        private readonly string _apiKey;
        private readonly HashSet<string> _trustedLeaders = new HashSet<string>();
        
        public NetworkSecurity(string apiKey)
        {
            _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
            _aesProvider = new AesCryptoServiceProvider();
            _rsaProvider = new RSACryptoServiceProvider(2048);
            
            // Generate secure AES key
            _aesProvider.GenerateKey();
            _aesProvider.GenerateIV();
        }
        
        /// <summary>
        /// Encrypts a message with AES-256 encryption
        /// </summary>
        public string EncryptMessage(string message, string recipientId)
        {
            if (string.IsNullOrEmpty(message)) return string.Empty;
            
            try
            {
                var messageBytes = Encoding.UTF8.GetBytes(message);
                
                using (var encryptor = _aesProvider.CreateEncryptor())
                {
                    var encryptedBytes = encryptor.TransformFinalBlock(messageBytes, 0, messageBytes.Length);
                    var result = Convert.ToBase64String(encryptedBytes);
                    
                    // Add integrity hash
                    var integrity = ComputeIntegrityHash(result + recipientId);
                    return $"{result}|{integrity}";
                }
            }
            catch (Exception ex)
            {
                throw new SecurityException($"Failed to encrypt message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Decrypts a message with AES-256 encryption
        /// </summary>
        public string DecryptMessage(string encryptedMessage, string senderId)
        {
            if (string.IsNullOrEmpty(encryptedMessage)) return string.Empty;
            
            try
            {
                var parts = encryptedMessage.Split('|');
                if (parts.Length != 2) throw new SecurityException("Invalid message format");
                
                var encryptedData = parts[0];
                var receivedHash = parts[1];
                
                // Verify integrity
                var expectedHash = ComputeIntegrityHash(encryptedData + senderId);
                if (receivedHash != expectedHash)
                {
                    throw new SecurityException("Message integrity verification failed");
                }
                
                var encryptedBytes = Convert.FromBase64String(encryptedData);
                
                using (var decryptor = _aesProvider.CreateDecryptor())
                {
                    var decryptedBytes = decryptor.TransformFinalBlock(encryptedBytes, 0, encryptedBytes.Length);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
            }
            catch (Exception ex)
            {
                throw new SecurityException($"Failed to decrypt message: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Validates an authentication token
        /// </summary>
        public bool ValidateAuthToken(string token, string leaderId)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(leaderId))
                return false;
            
            try
            {
                // Decode and validate JWT-style token
                var parts = token.Split('.');
                if (parts.Length != 3) return false;
                
                var header = JsonConvert.DeserializeObject<AuthHeader>(DecodeBase64(parts[0]));
                var payload = JsonConvert.DeserializeObject<AuthPayload>(DecodeBase64(parts[1]));
                var signature = parts[2];
                
                // Verify signature
                var expectedSignature = ComputeSignature($"{parts[0]}.{parts[1]}", _apiKey);
                if (signature != expectedSignature) return false;
                
                // Verify expiration
                if (payload.ExpirationTime < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false;
                
                // Verify leader ID
                if (payload.LeaderId != leaderId) return false;
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Generates an authentication token for a leader
        /// </summary>
        public string GenerateAuthToken(string leaderId, TimeSpan validFor)
        {
            var header = new AuthHeader
            {
                Algorithm = "HS256",
                Type = "JWT"
            };
            
            var payload = new AuthPayload
            {
                LeaderId = leaderId,
                IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ExpirationTime = DateTimeOffset.UtcNow.Add(validFor).ToUnixTimeSeconds(),
                Issuer = "FollowerPlugin"
            };
            
            var headerJson = JsonConvert.SerializeObject(header);
            var payloadJson = JsonConvert.SerializeObject(payload);
            
            var headerBase64 = EncodeBase64(headerJson);
            var payloadBase64 = EncodeBase64(payloadJson);
            
            var signature = ComputeSignature($"{headerBase64}.{payloadBase64}", _apiKey);
            
            return $"{headerBase64}.{payloadBase64}.{signature}";
        }
        
        /// <summary>
        /// Checks if a message has already been processed (prevents replay attacks)
        /// </summary>
        public bool IsMessageAlreadyProcessed(string messageId)
        {
            lock (_securityLock)
            {
                if (_processedMessages.ContainsKey(messageId))
                    return true;
                
                // Clean up old entries (older than 5 minutes)
                var cutoff = DateTime.UtcNow.AddMinutes(-5);
                var toRemove = _processedMessages
                    .Where(kvp => kvp.Value < cutoff)
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var key in toRemove)
                {
                    _processedMessages.Remove(key);
                }
                
                return false;
            }
        }
        
        /// <summary>
        /// Marks a message as processed
        /// </summary>
        public void MarkMessageAsProcessed(string messageId)
        {
            lock (_securityLock)
            {
                _processedMessages[messageId] = DateTime.UtcNow;
            }
        }
        
        /// <summary>
        /// Adds a trusted leader to the whitelist
        /// </summary>
        public void AddTrustedLeader(string leaderId)
        {
            lock (_securityLock)
            {
                _trustedLeaders.Add(leaderId);
            }
        }
        
        /// <summary>
        /// Checks if a leader is trusted
        /// </summary>
        public bool IsLeaderTrusted(string leaderId)
        {
            lock (_securityLock)
            {
                return _trustedLeaders.Contains(leaderId);
            }
        }
        
        /// <summary>
        /// Gets the public key for secure key exchange
        /// </summary>
        public string GetPublicKey()
        {
            return Convert.ToBase64String(_rsaProvider.ExportRSAPublicKey());
        }
        
        /// <summary>
        /// Securely exchanges AES keys with a leader
        /// </summary>
        public async Task<bool> PerformKeyExchange(string leaderPublicKey, Func<string, Task<string>> sendMessageFunc)
        {
            try
            {
                // Import leader's public key
                var leaderRsa = new RSACryptoServiceProvider();
                leaderRsa.ImportRSAPublicKey(Convert.FromBase64String(leaderPublicKey));
                
                // Encrypt our AES key with leader's public key
                var keyData = new
                {
                    AesKey = Convert.ToBase64String(_aesProvider.Key),
                    AesIV = Convert.ToBase64String(_aesProvider.IV),
                    Timestamp = DateTime.UtcNow
                };
                
                var keyJson = JsonConvert.SerializeObject(keyData);
                var keyBytes = Encoding.UTF8.GetBytes(keyJson);
                var encryptedKey = leaderRsa.Encrypt(keyBytes, false);
                
                // Send encrypted key to leader
                var keyExchangeMessage = Convert.ToBase64String(encryptedKey);
                var response = await sendMessageFunc(keyExchangeMessage);
                
                // Verify leader received the key successfully
                return response == "KEY_EXCHANGE_SUCCESS";
            }
            catch (Exception ex)
            {
                throw new SecurityException($"Key exchange failed: {ex.Message}");
            }
        }
        
        private string ComputeIntegrityHash(string input)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_apiKey)))
            {
                var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes);
            }
        }
        
        private string ComputeSignature(string input, string secret)
        {
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret)))
            {
                var hashBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
                return Convert.ToBase64String(hashBytes);
            }
        }
        
        private string EncodeBase64(string input)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        }
        
        private string DecodeBase64(string input)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(input));
        }
        
        public void Dispose()
        {
            _aesProvider?.Dispose();
            _rsaProvider?.Dispose();
        }
        
        private class AuthHeader
        {
            public string Algorithm { get; set; }
            public string Type { get; set; }
        }
        
        private class AuthPayload
        {
            public string LeaderId { get; set; }
            public long IssuedAt { get; set; }
            public long ExpirationTime { get; set; }
            public string Issuer { get; set; }
        }
    }
    
    /// <summary>
    /// Custom security exception for network security issues
    /// </summary>
    public class SecurityException : Exception
    {
        public SecurityException(string message) : base(message) { }
        public SecurityException(string message, Exception innerException) : base(message, innerException) { }
    }
} 