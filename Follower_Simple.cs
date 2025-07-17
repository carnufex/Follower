using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared.Enums;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Follower
{
    /// <summary>
    /// Simple, focused follower plugin - back to basics
    /// </summary>
    public class SimpleFollower : BaseSettingsPlugin<FollowerSettings>
    {
        // Core state - minimal and focused
        private Entity _followTarget;
        private Vector3 _lastTargetPosition = Vector3.Zero;
        private List<TaskNode> _tasks = new List<TaskNode>();
        private DateTime _nextBotAction = DateTime.Now;
        private Random _random = new Random();
        
        // Terrain data
        private byte[,] _tiles;
        private int _numCols, _numRows;
        
        // Area transitions
        private Dictionary<uint, Entity> _areaTransitions = new Dictionary<uint, Entity>();
        
        public override bool Initialise()
        {
            Name = "Simple Follower";
            Input.RegisterKey(Settings.MovementKey.Value);
            Input.RegisterKey(Settings.ToggleFollower.Value);
            
            Settings.ToggleFollower.OnValueChanged += () => { 
                Input.RegisterKey(Settings.ToggleFollower.Value); 
            };
            
            return base.Initialise();
        }
        
        public override Job Tick()
        {
            // Handle toggle first
            if (Settings.ToggleFollower.PressedOnce())
            {
                Settings.IsFollowEnabled.SetValueNoEvent(!Settings.IsFollowEnabled.Value);
                _tasks.Clear(); // Clear tasks when toggling
            }
            
            // Only run if enabled and player is alive
            if (!Settings.IsFollowEnabled.Value || !GameController.Player.IsAlive)
                return null;
            
            // Don't run too frequently
            if (DateTime.Now < _nextBotAction)
                return null;
            
            try
            {
                // Get follow target
                _followTarget = GetFollowTarget();
                
                // Plan what to do
                PlanTasks();
                
                // Execute tasks
                ExecuteTasks();
            }
            catch (Exception ex)
            {
                LogMessage($"Error in follower: {ex.Message}", 1);
                _nextBotAction = DateTime.Now.AddSeconds(1); // Wait longer on error
            }
            
            return null;
        }
        
        private Entity GetFollowTarget()
        {
            var leaderName = Settings.LeaderName.Value?.ToLower();
            if (string.IsNullOrEmpty(leaderName))
                return null;
            
            try
            {
                // First try to find in visible entities (most common case)
                var visibleTarget = GameController.Entities
                    .Where(x => x.Type == EntityType.Player)
                    .FirstOrDefault(x => x.GetComponent<Player>()?.PlayerName?.ToLower() == leaderName);
                
                if (visibleTarget != null && visibleTarget.IsValid)
                    return visibleTarget;
                
                // If not visible, try EntityListWrapper which includes off-screen entities
                var offscreenTarget = GameController.EntityListWrapper.Entities
                    .Where(x => x.Type == EntityType.Player)
                    .FirstOrDefault(x => x.IsValid && x.GetComponent<Player>()?.PlayerName?.ToLower() == leaderName);
                
                if (offscreenTarget != null && offscreenTarget.IsValid)
                    return offscreenTarget;
                
                // Third attempt: try all entities in the area (more expensive but comprehensive)
                var allEntities = GameController.EntityListWrapper.Entities
                    .Where(x => x.Type == EntityType.Player && x.IsValid)
                    .ToList();
                
                foreach (var entity in allEntities)
                {
                    try
                    {
                        var playerComponent = entity.GetComponent<Player>();
                        if (playerComponent?.PlayerName?.ToLower() == leaderName)
                        {
                            return entity;
                        }
                    }
                    catch
                    {
                        // Skip invalid entities
                        continue;
                    }
                }
                
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        private void PlanTasks()
        {
            // Clear old tasks if we have too many
            if (_tasks.Count > 5)
                _tasks.Clear();
            
            if (_followTarget == null)
            {
                // Leader not found, try to use last known position for area transition
                if (_lastTargetPosition != Vector3.Zero && _tasks.Count == 0)
                {
                    var nearbyTransition = _areaTransitions.Values
                        .Where(t => Vector3.Distance(_lastTargetPosition, t.Pos) < Settings.ClearPathDistance.Value)
                        .OrderBy(t => Vector3.Distance(_lastTargetPosition, t.Pos))
                        .FirstOrDefault();
                    
                    if (nearbyTransition != null)
                    {
                        _tasks.Add(new TaskNode(nearbyTransition.Pos, Settings.TransitionDistance.Value, TaskNode.TaskNodeType.Transition));
                    }
                }
                return;
            }
            
            var distance = Vector3.Distance(GameController.Player.Pos, _followTarget.Pos);
            
            // If leader is close, don't add tasks unless close follow is enabled
            if (distance < Settings.ClearPathDistance.Value)
            {
                if (Settings.IsCloseFollowEnabled.Value && distance > Settings.PathfindingNodeDistance.Value)
                {
                    _tasks.Add(new TaskNode(_followTarget.Pos, Settings.PathfindingNodeDistance.Value));
                }
                return;
            }
            
            // Leader is far, add movement task
            if (_tasks.Count == 0)
            {
                _tasks.Add(new TaskNode(_followTarget.Pos, Settings.PathfindingNodeDistance.Value));
            }
            else
            {
                // Update last task if leader moved significantly
                var lastTask = _tasks.Last();
                if (Vector3.Distance(lastTask.WorldPosition, _followTarget.Pos) > Settings.PathfindingNodeDistance.Value)
                {
                    _tasks.Add(new TaskNode(_followTarget.Pos, Settings.PathfindingNodeDistance.Value));
                }
            }
            
            _lastTargetPosition = _followTarget.Pos;
        }
        
        private void ExecuteTasks()
        {
            if (_tasks.Count == 0)
                return;
            
            var currentTask = _tasks.First();
            var taskDistance = Vector3.Distance(GameController.Player.Pos, currentTask.WorldPosition);
            
            // Adaptive timing based on leader visibility and distance
            var baseDelay = Settings.BotInputFrequency.Value;
            var randomDelay = _random.Next(0, Settings.BotInputFrequency.Value);
            
            // If leader is not visible (out of range), use much slower timing to prevent kicks
            if (_followTarget == null)
            {
                baseDelay = Math.Max(baseDelay * 3, 500); // At least 3x slower when leader not visible
                randomDelay = _random.Next(100, 300); // Additional random delay
            }
            else
            {
                // If leader is visible but far away, use moderately slower timing
                var leaderDistance = Vector3.Distance(GameController.Player.Pos, _followTarget.Pos);
                if (leaderDistance > Settings.NormalFollowDistance.Value * 2)
                {
                    baseDelay = Math.Max(baseDelay * 2, 300); // 2x slower when leader is far
                    randomDelay = _random.Next(50, 150);
                }
            }
            
            _nextBotAction = DateTime.Now.AddMilliseconds(baseDelay + randomDelay);
            
            switch (currentTask.Type)
            {
                case TaskNode.TaskNodeType.Movement:
                    ExecuteMovementTask(currentTask, taskDistance);
                    break;
                    
                case TaskNode.TaskNodeType.Transition:
                    ExecuteTransitionTask(currentTask, taskDistance);
                    break;
            }
            
            // Remove completed tasks
            if (taskDistance <= currentTask.Bounds)
            {
                _tasks.RemoveAt(0);
            }
            else
            {
                // Track attempts and remove if too many
                currentTask.AttemptCount++;
                if (currentTask.AttemptCount > Settings.MaxTaskAttempts.Value)
                {
                    _tasks.RemoveAt(0);
                }
            }
        }
        }
        
        private void ExecuteMovementTask(TaskNode task, float distance)
        {
            var screenPos = WorldToScreenPosition(task.WorldPosition);
            Mouse.SetCursorPos(screenPos);
            
            Input.KeyDown(Settings.MovementKey);
            Input.KeyUp(Settings.MovementKey);
        }
        
        private void ExecuteTransitionTask(TaskNode task, float distance)
        {
            var screenPos = WorldToScreenPosition(task.WorldPosition);
            
            if (distance <= Settings.ClearPathDistance.Value)
            {
                // Close enough to click
                Mouse.SetCursorPosAndLeftClick(screenPos, 100);
                _nextBotAction = DateTime.Now.AddSeconds(1); // Wait longer after transition
            }
            else
            {
                // Move towards transition
                Mouse.SetCursorPos(screenPos);
                Input.KeyDown(Settings.MovementKey);
                Input.KeyUp(Settings.MovementKey);
            }
        }
        
        private Vector2 WorldToScreenPosition(Vector3 worldPos)
        {
            var camera = GameController.Game.IngameState.Camera;
            var windowRect = GameController.Window.GetWindowRectangle();
            var screenPos = camera.WorldToScreen(worldPos);
            
            // More conservative UI avoidance - larger margins
            var safeMargin = 100; // Increased from 50
            var result = new Vector2(
                Math.Max(safeMargin, Math.Min(windowRect.Width - safeMargin, screenPos.X + windowRect.X)),
                Math.Max(safeMargin, Math.Min(windowRect.Height - safeMargin, screenPos.Y + windowRect.Y))
            );
            
            // Additional UI panel avoidance
            if (IsUIBlocking(result))
            {
                // If UI is blocking, try to find a safe position
                result = FindSafeScreenPosition(result, windowRect);
            }
            
            return result;
        }
        
        private bool IsUIBlocking(Vector2 screenPos)
        {
            try
            {
                var ingameUI = GameController.IngameState.IngameUi;
                if (ingameUI == null) return false;
                
                var screenRect = new RectangleF(screenPos.X - 50, screenPos.Y - 50, 100, 100);
                
                // Check common UI panels that might block movement
                if (ingameUI.InventoryPanel?.IsVisible == true && 
                    ingameUI.InventoryPanel.GetClientRect().Intersects(screenRect))
                    return true;
                    
                if (ingameUI.StashElement?.IsVisible == true && 
                    ingameUI.StashElement.GetClientRect().Intersects(screenRect))
                    return true;
                    
                if (ingameUI.TreePanel?.IsVisible == true && 
                    ingameUI.TreePanel.GetClientRect().Intersects(screenRect))
                    return true;
                    
                if (ingameUI.Atlas?.IsVisible == true && 
                    ingameUI.Atlas.GetClientRect().Intersects(screenRect))
                    return true;
                    
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        private Vector2 FindSafeScreenPosition(Vector2 originalPos, RectangleF windowRect)
        {
            // Try positions in a spiral pattern around the original position
            var attempts = new[]
            {
                new Vector2(originalPos.X - 100, originalPos.Y),
                new Vector2(originalPos.X + 100, originalPos.Y),
                new Vector2(originalPos.X, originalPos.Y - 100),
                new Vector2(originalPos.X, originalPos.Y + 100),
                new Vector2(originalPos.X - 150, originalPos.Y - 150),
                new Vector2(originalPos.X + 150, originalPos.Y + 150),
            };
            
            foreach (var attempt in attempts)
            {
                if (attempt.X >= 100 && attempt.X <= windowRect.Width - 100 &&
                    attempt.Y >= 100 && attempt.Y <= windowRect.Height - 100 &&
                    !IsUIBlocking(attempt))
                {
                    return attempt;
                }
            }
            
            // Fallback to center of screen
            return new Vector2(windowRect.Width / 2, windowRect.Height / 2);
        }
        
        public override void AreaChange(AreaInstance area)
        {
            // Reset state for new area
            _tasks.Clear();
            _followTarget = null;
            _lastTargetPosition = Vector3.Zero;
            _areaTransitions.Clear();
            
            // Load area transitions
            foreach (var entity in GameController.EntityListWrapper.Entities)
            {
                if (entity.Type == EntityType.AreaTransition || 
                    entity.Type == EntityType.Portal || 
                    entity.Type == EntityType.TownPortal)
                {
                    if (!_areaTransitions.ContainsKey(entity.Id))
                        _areaTransitions.Add(entity.Id, entity);
                }
            }
            
            // Initialize terrain data (simplified)
            InitializeTerrain();
        }
        
        private void InitializeTerrain()
        {
            try
            {
                var terrain = GameController.IngameState.Data.Terrain;
                var terrainBytes = GameController.Memory.ReadBytes(terrain.LayerMelee.First, terrain.LayerMelee.Size);
                
                _numCols = (int)(terrain.NumCols - 1) * 23;
                _numRows = (int)(terrain.NumRows - 1) * 23;
                if ((_numCols & 1) > 0) _numCols++;
                
                _tiles = new byte[_numCols, _numRows];
                
                int dataIndex = 0;
                for (int y = 0; y < _numRows; y++)
                {
                    for (int x = 0; x < _numCols; x += 2)
                    {
                        var b = terrainBytes[dataIndex + (x >> 1)];
                        _tiles[x, y] = (byte)((b & 0xf) > 0 ? 1 : 255);
                        _tiles[x + 1, y] = (byte)((b >> 4) > 0 ? 1 : 255);
                    }
                    dataIndex += terrain.BytesPerRow;
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to initialize terrain: {ex.Message}", 2);
            }
        }
        
        public override void EntityAdded(Entity entity)
        {
            // Track area transitions
            if (entity.Type == EntityType.AreaTransition || 
                entity.Type == EntityType.Portal || 
                entity.Type == EntityType.TownPortal)
            {
                if (!_areaTransitions.ContainsKey(entity.Id))
                    _areaTransitions.Add(entity.Id, entity);
            }
        }
        
        public override void EntityRemoved(Entity entity)
        {
            // Remove area transitions
            if (_areaTransitions.ContainsKey(entity.Id))
                _areaTransitions.Remove(entity.Id);
        }
        
        public override void Render()
        {
            // Simple status display
            var yPos = 100;
            Graphics.DrawText($"Follow Enabled: {Settings.IsFollowEnabled.Value}", new Vector2(10, yPos), SharpDX.Color.White);
            yPos += 20;
            
            if (_followTarget != null)
            {
                var distance = Vector3.Distance(GameController.Player.Pos, _followTarget.Pos);
                Graphics.DrawText($"Following: {Settings.LeaderName.Value} (Distance: {distance:F1})", new Vector2(10, yPos), SharpDX.Color.Green);
            }
            else
            {
                Graphics.DrawText($"Leader not found: {Settings.LeaderName.Value}", new Vector2(10, yPos), SharpDX.Color.Red);
            }
            yPos += 20;
            
            Graphics.DrawText($"Tasks: {_tasks.Count}", new Vector2(10, yPos), SharpDX.Color.White);
            
            // Draw task path
            if (_tasks.Count > 1)
            {
                for (int i = 1; i < _tasks.Count; i++)
                {
                    var start = WorldToScreenPosition(_tasks[i - 1].WorldPosition);
                    var end = WorldToScreenPosition(_tasks[i].WorldPosition);
                    Graphics.DrawLine(start, end, 2, SharpDX.Color.Pink);
                }
            }
        }
    }
} 