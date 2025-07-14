using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SharpDX;

namespace Follower
{
    /// <summary>
    /// Manages shared position data between Leader and Follower using file system
    /// </summary>
    public class SharedPositionManager
    {
        private readonly string _sharedDirectory;
        private readonly string _positionFilePath;
        private readonly string _characterName;
        private readonly object _fileLock = new object();
        private DateTime _lastWriteTime = DateTime.MinValue;
        private DateTime _lastReadTime = DateTime.MinValue;
        private SharedPositionData _lastKnownPosition;
        
        public SharedPositionManager(string characterName)
        {
            _characterName = characterName;
            _sharedDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "POE", "Shared");
            _positionFilePath = Path.Combine(_sharedDirectory, $"{characterName}_position.json");
            
            // Ensure directory exists
            Directory.CreateDirectory(_sharedDirectory);
        }
        
        /// <summary>
        /// Writes current position to shared file (used by Leader)
        /// </summary>
        public bool WritePosition(Vector3 position, string areaName, string instanceId = null)
        {
            try
            {
                // Don't write too frequently to avoid file system overhead
                if (DateTime.Now - _lastWriteTime < TimeSpan.FromMilliseconds(200))
                    return false;
                
                var positionData = new SharedPositionData
                {
                    Position = position,
                    AreaName = areaName,
                    InstanceId = instanceId ?? "unknown",
                    Timestamp = DateTime.UtcNow,
                    CharacterName = _characterName
                };
                
                lock (_fileLock)
                {
                    var json = JsonConvert.SerializeObject(positionData, Formatting.Indented);
                    File.WriteAllText(_positionFilePath, json);
                    _lastWriteTime = DateTime.Now;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                // Log error but don't throw to prevent plugin crashes
                Console.WriteLine($"SharedPositionManager: Error writing position - {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Reads position from shared file (used by Follower)
        /// </summary>
        public SharedPositionData ReadPosition()
        {
            try
            {
                // Don't read too frequently to avoid file system overhead
                if (DateTime.Now - _lastReadTime < TimeSpan.FromMilliseconds(100))
                    return _lastKnownPosition;
                
                _lastReadTime = DateTime.Now;
                
                if (!File.Exists(_positionFilePath))
                    return null;
                
                lock (_fileLock)
                {
                    var json = File.ReadAllText(_positionFilePath);
                    var positionData = JsonConvert.DeserializeObject<SharedPositionData>(json);
                    
                    // Check if data is reasonably fresh (within 10 seconds)
                    if (positionData != null && DateTime.UtcNow - positionData.Timestamp < TimeSpan.FromSeconds(10))
                    {
                        _lastKnownPosition = positionData;
                        return positionData;
                    }
                }
                
                return null;
            }
            catch (Exception ex)
            {
                // Log error but don't throw to prevent plugin crashes
                Console.WriteLine($"SharedPositionManager: Error reading position - {ex.Message}");
                return _lastKnownPosition;
            }
        }
        
        /// <summary>
        /// Checks if shared position data is available and fresh
        /// </summary>
        public bool IsPositionDataAvailable()
        {
            var data = ReadPosition();
            return data != null && DateTime.UtcNow - data.Timestamp < TimeSpan.FromSeconds(5);
        }
        
        /// <summary>
        /// Gets the age of the last position update
        /// </summary>
        public TimeSpan GetPositionAge()
        {
            var data = ReadPosition();
            return data != null ? DateTime.UtcNow - data.Timestamp : TimeSpan.MaxValue;
        }
        
        /// <summary>
        /// Cleans up old position files
        /// </summary>
        public void Cleanup()
        {
            try
            {
                if (File.Exists(_positionFilePath))
                {
                    File.Delete(_positionFilePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SharedPositionManager: Error cleaning up - {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Shared position data structure
    /// </summary>
    public class SharedPositionData
    {
        public Vector3 Position { get; set; }
        public string AreaName { get; set; }
        public string InstanceId { get; set; }
        public DateTime Timestamp { get; set; }
        public string CharacterName { get; set; }
        
        /// <summary>
        /// Checks if this position data is from the same area/instance
        /// </summary>
        public bool IsSameArea(string currentAreaName, string currentInstanceId = null)
        {
            return string.Equals(AreaName, currentAreaName, StringComparison.OrdinalIgnoreCase) &&
                   (string.IsNullOrEmpty(currentInstanceId) || string.Equals(InstanceId, currentInstanceId, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Checks if this position data is still fresh
        /// </summary>
        public bool IsFresh(TimeSpan maxAge)
        {
            return DateTime.UtcNow - Timestamp <= maxAge;
        }
    }
} 