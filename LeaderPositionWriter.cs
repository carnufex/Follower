using System;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.PoEMemory;
using SharpDX;

namespace Follower
{
    /// <summary>
    /// Example class showing how to integrate SharedPositionManager into a Leader plugin
    /// This should be integrated into your LeaderCommands plugin
    /// </summary>
    public class LeaderPositionWriter
    {
        private readonly GameController _gameController;
        private SharedPositionManager _sharedPositionManager;
        private DateTime _lastPositionWrite = DateTime.MinValue;
        private readonly TimeSpan _writeInterval = TimeSpan.FromMilliseconds(200); // Write every 200ms
        
        public LeaderPositionWriter(GameController gameController)
        {
            _gameController = gameController;
        }
        
        /// <summary>
        /// Initialize the position writer with the leader's character name
        /// Call this in your Leader plugin's Initialise method
        /// </summary>
        public void Initialize(string characterName)
        {
            try
            {
                _sharedPositionManager = new SharedPositionManager(characterName);
                Console.WriteLine($"Leader position writer initialized for: {characterName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize leader position writer: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update the shared position file with current leader position
        /// Call this in your Leader plugin's Tick method or in a coroutine
        /// </summary>
        public void UpdatePosition()
        {
            if (_sharedPositionManager == null || _gameController?.Player == null)
                return;
            
            try
            {
                // Don't write too frequently
                if (DateTime.Now - _lastPositionWrite < _writeInterval)
                    return;
                
                // Only write if player is alive and in game
                if (!_gameController.Player.IsAlive || !_gameController.InGame)
                    return;
                
                var currentPosition = _gameController.Player.Pos;
                var currentArea = _gameController.Area.CurrentArea;
                
                if (currentArea != null && currentPosition != Vector3.Zero)
                {
                    var areaName = currentArea.Name;
                    var instanceId = currentArea.GetHashCode().ToString(); // Use area hash as instance ID
                    
                    var success = _sharedPositionManager.WritePosition(currentPosition, areaName, instanceId);
                    
                    if (success)
                    {
                        _lastPositionWrite = DateTime.Now;
                        // Optional: log position updates (can be removed for performance)
                        // Console.WriteLine($"Leader position updated: {currentPosition} in {areaName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating leader position: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cleanup method - call this in your Leader plugin's Dispose method
        /// </summary>
        public void Dispose()
        {
            try
            {
                _sharedPositionManager?.Cleanup();
                _sharedPositionManager = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing leader position writer: {ex.Message}");
            }
        }
    }
}

/* 
 * INTEGRATION EXAMPLE FOR YOUR LEADER PLUGIN:
 * 
 * In your LeaderCommands plugin class, add:
 * 
 * private LeaderPositionWriter _positionWriter;
 * 
 * In your Initialise() method:
 * _positionWriter = new LeaderPositionWriter(GameController);
 * _positionWriter.Initialize("YourLeaderCharacterName");
 * 
 * In your Tick() method or in a coroutine:
 * _positionWriter?.UpdatePosition();
 * 
 * In your Dispose() method:
 * _positionWriter?.Dispose();
 * 
 * This will write the leader's position to:
 * /Users/chris/Documents/POE/Shared/YourLeaderCharacterName_position.json
 * 
 * The follower will read from this file when entity detection fails.
 */ 