using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using SharpDX;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Follower;

public class Follower : BaseSettingsPlugin<FollowerSettings>
{
    // Core following state - simplified
    private Entity _followTarget;
    private Vector3 _lastTargetPosition = Vector3.Zero;
    private List<TaskNode> _tasks = new List<TaskNode>();
    private DateTime _nextBotAction = DateTime.Now;
    private Random _random = new Random();
    
    // Action tracking to prevent kicks
    private Queue<DateTime> _recentActions = new Queue<DateTime>();
    private const int MAX_ACTIONS_PER_SECOND = 2; // Very conservative limit
    
    // Terrain data
    private byte[,] _tiles;
    private int _numCols, _numRows;
    
    // Area transitions
    private Dictionary<uint, Entity> _areaTransitions = new Dictionary<uint, Entity>();
    private bool _hasUsedWP = false;
    
    // Gem leveling integration (maintained for plugin compatibility)
    private DateTime _lastGemLevelCheck = DateTime.MinValue;
    private bool _isLevelingGems = false;
    private DateTime _gemLevelingStartTime;

    public override bool Initialise()
    {
        Name = "Follower";
        Input.RegisterKey(Settings.Movement.MovementKey.Value);
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
            
            // Check for gem leveling
            CheckAndLevelGems();
            
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
    
    /// <summary>
    /// Checks for gems that need leveling and triggers the leveling process
    /// </summary>
    private void CheckAndLevelGems()
    {
        if (!Settings.Gems.AutoLevelGems.Value || !GameController.Player.IsAlive)
            return;

        var now = DateTime.Now;
        
        // Check if enough time has passed since last gem level check
        if (now - _lastGemLevelCheck < TimeSpan.FromMilliseconds(Settings.Gems.GemLevelCheckInterval.Value))
            return;

        _lastGemLevelCheck = now;

        // Determine if we should level gems based on conditions
        bool shouldLevelGems = false;
        
        if (Settings.Gems.LevelGemsWhenClose.Value && _followTarget != null)
        {
            var distance = Vector3.Distance(GameController.Player.Pos, _followTarget.Pos);
            if (distance <= Settings.Movement.NormalFollowDistance.Value)
            {
                shouldLevelGems = true;
            }
        }
        else if (Settings.Gems.LevelGemsWhenStopped.Value && _tasks.Count == 0)
        {
            shouldLevelGems = true;
        }

        if (shouldLevelGems && HasGemsToLevel())
        {
            TriggerGemLeveling();
        }
    }

    /// <summary>
    /// Checks if there are gems available to level
    /// </summary>
    private bool HasGemsToLevel()
    {
        try
        {
            var gemLevelUpPanel = GameController.IngameState.IngameUi?.GemLvlUpPanel;
            if (gemLevelUpPanel == null || !gemLevelUpPanel.IsVisible)
                return false;

            var gemsToLvlUp = gemLevelUpPanel.GemsToLvlUp;
            if (gemsToLvlUp == null || !gemsToLvlUp.Any())
                return false;

            foreach (var gemGroup in gemsToLvlUp)
            {
                if (gemGroup.Children.Any(elem => elem.Text != null && elem.Text.Contains("Click to level")))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            LogMessage($"HasGemsToLevel error: {ex.Message}", 1);
            return false;
        }
    }

    /// <summary>
    /// Triggers the gem leveling process
    /// </summary>
    private void TriggerGemLeveling()
    {
        if (_isLevelingGems)
            return;

        try
        {
            _isLevelingGems = true;
            _gemLevelingStartTime = DateTime.Now;
            
            // Get the first gem that needs leveling
            var gemToLevel = GetFirstLevelableGem();
            if (gemToLevel != null)
            {
                LogMessage("Auto-leveling gem detected by follower", 5);
                
                // Use the same approach as SkillGems plugin
                var gemLevelButton = gemToLevel.GetChildAtIndex(1);
                if (gemLevelButton != null)
                {
                    // Set cursor position and click
                    var windowRect = GameController.Window.GetWindowRectangle();
                    var buttonCenter = gemLevelButton.GetClientRect().Center;
                    var screenPos = windowRect.TopLeft + buttonCenter;
                    
                    Mouse.SetCursorPos(screenPos);
                    System.Threading.Thread.Sleep(50); // Small delay for cursor positioning
                    Mouse.LeftClick(25);
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"TriggerGemLeveling error: {ex.Message}", 1);
        }
        finally
        {
            // Reset leveling state after a short delay
            Task.Delay(1000).ContinueWith(_ => _isLevelingGems = false);
        }
    }

    /// <summary>
    /// Gets the first gem that can be leveled
    /// </summary>
    private GemLevelUpElement GetFirstLevelableGem()
    {
        try
        {
            var gemLevelUpPanel = GameController.IngameState.IngameUi?.GemLvlUpPanel;
            if (gemLevelUpPanel == null || !gemLevelUpPanel.IsVisible)
                return null;

            var gemsToLvlUp = gemLevelUpPanel.GemsToLvlUp;
            if (gemsToLvlUp == null || !gemsToLvlUp.Any())
                return null;

            foreach (var gemGroup in gemsToLvlUp)
            {
                if (gemGroup.Children.Any(elem => elem.Text != null && elem.Text.Contains("Click to level")))
                {
                    return gemGroup;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            LogMessage($"GetFirstLevelableGem error: {ex.Message}", 1);
            return null;
        }
    }


    /// <summary>
    /// Plans tasks based on leader position and current state
    /// </summary>
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
                .Where(t => Vector3.Distance(_lastTargetPosition, t.Pos) < Settings.Movement.ClearPathDistance.Value)
                .OrderBy(t => Vector3.Distance(_lastTargetPosition, t.Pos))
                .FirstOrDefault();
            
            if (nearbyTransition != null)
            {
                _tasks.Add(new TaskNode(nearbyTransition.Pos, Settings.Movement.TransitionDistance.Value, TaskNode.TaskNodeType.Transition));
            }
            }
            return;
        }
        
        var distance = Vector3.Distance(GameController.Player.Pos, _followTarget.Pos);
        
        // If leader is close, don't add tasks unless close follow is enabled
        if (distance < Settings.Movement.ClearPathDistance.Value)
        {
            if (Settings.Movement.IsCloseFollowEnabled.Value && distance > Settings.Movement.PathfindingNodeDistance.Value)
            {
                _tasks.Add(new TaskNode(_followTarget.Pos, Settings.Movement.PathfindingNodeDistance.Value));
            }
            
            // Check for waypoint claiming
            if (!_hasUsedWP)
            {
                var waypoint = GameController.EntityListWrapper.Entities.FirstOrDefault(e => 
                    e.Type == EntityType.Waypoint && 
                    Vector3.Distance(GameController.Player.Pos, e.Pos) < Settings.Movement.ClearPathDistance.Value);
                    
                if (waypoint != null)
                {
                    _hasUsedWP = true;
                    _tasks.Add(new TaskNode(waypoint.Pos, Settings.Movement.ClearPathDistance.Value, TaskNode.TaskNodeType.ClaimWaypoint));
                }
            }
            
            return;
        }
        
        // Leader is far, add movement task
        if (_tasks.Count == 0)
        {
            _tasks.Add(new TaskNode(_followTarget.Pos, Settings.Movement.PathfindingNodeDistance.Value));
        }
        else
        {
            // Update last task if leader moved significantly
            var lastTask = _tasks.Last();
            if (Vector3.Distance(lastTask.WorldPosition, _followTarget.Pos) > Settings.Movement.PathfindingNodeDistance.Value)
            {
                _tasks.Add(new TaskNode(_followTarget.Pos, Settings.Movement.PathfindingNodeDistance.Value));
            }
        }
        
        _lastTargetPosition = _followTarget.Pos;
    }

    /// <summary>
    /// Check if we're performing too many actions too quickly
    /// </summary>
    private bool IsActionRateLimited()
    {
        var now = DateTime.Now;
        var oneSecondAgo = now.AddSeconds(-1);
        
        // Remove actions older than 1 second
        while (_recentActions.Count > 0 && _recentActions.Peek() < oneSecondAgo)
        {
            _recentActions.Dequeue();
        }
        
        // Check if we've exceeded the rate limit
        if (_recentActions.Count >= MAX_ACTIONS_PER_SECOND)
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Record that we performed an action
    /// </summary>
    private void RecordAction()
    {
        _recentActions.Enqueue(DateTime.Now);
    }
    
    /// <summary>
    /// Executes the current task queue
    /// </summary>
    private void ExecuteTasks()
    {
        if (_tasks.Count == 0)
            return;
            
        // Check if we're rate limited
        if (IsActionRateLimited())
        {
            _nextBotAction = DateTime.Now.AddMilliseconds(1000 + _random.Next(200, 500)); // Wait at least 1 second
            return;
        }
        
        var currentTask = _tasks.First();
        var taskDistance = Vector3.Distance(GameController.Player.Pos, currentTask.WorldPosition);
        
        // Much more conservative timing to prevent kicks
        var baseDelay = Math.Max(Settings.Movement.BotInputFrequency.Value, 250); // Minimum 250ms
        var randomDelay = _random.Next(100, 300); // Always add random delay
        
        // If leader is not visible (out of range), use very slow timing
        if (_followTarget == null)
        {
            baseDelay = Math.Max(baseDelay * 5, 1000); // At least 5x slower, minimum 1 second
            randomDelay = _random.Next(200, 500); // Large random delay
        }
        else
        {
            // If leader is visible but far away, use slow timing
            var leaderDistance = Vector3.Distance(GameController.Player.Pos, _followTarget.Pos);
            if (leaderDistance > Settings.Movement.NormalFollowDistance.Value * 2)
            {
                baseDelay = Math.Max(baseDelay * 3, 750); // 3x slower, minimum 750ms
                randomDelay = _random.Next(150, 350);
            }
            else if (leaderDistance > Settings.Movement.NormalFollowDistance.Value)
            {
                baseDelay = Math.Max(baseDelay * 2, 500); // 2x slower, minimum 500ms
                randomDelay = _random.Next(100, 250);
            }
        }
        
        _nextBotAction = DateTime.Now.AddMilliseconds(baseDelay + randomDelay);
        
        switch (currentTask.Type)
        {
            case TaskNode.TaskNodeType.Movement:
                ExecuteMovementTask(currentTask, taskDistance);
                RecordAction();
                break;
                
            case TaskNode.TaskNodeType.Transition:
                ExecuteTransitionTask(currentTask, taskDistance);
                RecordAction();
                break;
                
            case TaskNode.TaskNodeType.ClaimWaypoint:
                ExecuteWaypointTask(currentTask, taskDistance);
                RecordAction();
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
            if (currentTask.AttemptCount > Settings.Safety.MaxTaskAttempts.Value)
            {
                _tasks.RemoveAt(0);
            }
        }
    }

    private void ExecuteMovementTask(TaskNode task, float distance)
    {
        var screenPos = WorldToScreenPosition(task.WorldPosition);
        Mouse.SetCursorPos(screenPos);
        
        Input.KeyDown(Settings.Movement.MovementKey);
        Input.KeyUp(Settings.Movement.MovementKey);
    }
    
    private void ExecuteTransitionTask(TaskNode task, float distance)
    {
        var screenPos = WorldToScreenPosition(task.WorldPosition);
        
        if (distance <= Settings.Movement.ClearPathDistance.Value)
        {
            // Close enough to click
            Mouse.SetCursorPosAndLeftClick(screenPos, 100);
            _nextBotAction = DateTime.Now.AddSeconds(1); // Wait longer after transition
        }
        else
        {
            // Move towards transition
            Mouse.SetCursorPos(screenPos);
            Input.KeyDown(Settings.Movement.MovementKey);
            Input.KeyUp(Settings.Movement.MovementKey);
        }
    }
    
    private void ExecuteWaypointTask(TaskNode task, float distance)
    {
        var screenPos = WorldToScreenPosition(task.WorldPosition);
        
        if (distance <= Settings.Movement.WaypointDistance.Value)
        {
            // Close enough to click waypoint
            Mouse.SetCursorPosAndLeftClick(screenPos, 100);
            _nextBotAction = DateTime.Now.AddSeconds(1); // Wait longer after waypoint
        }
        else
        {
            // Move towards waypoint
            Mouse.SetCursorPos(screenPos);
            Input.KeyDown(Settings.Movement.MovementKey);
            Input.KeyUp(Settings.Movement.MovementKey);
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
        _hasUsedWP = false;
        
        // Reset gem leveling state
        _lastGemLevelCheck = DateTime.MinValue;
        _isLevelingGems = false;
        
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
            var color = distance > 100 ? SharpDX.Color.Yellow : SharpDX.Color.Green;
            Graphics.DrawText($"Following: {Settings.LeaderName.Value} (Distance: {distance:F1})", new Vector2(10, yPos), color);
            
            // Show timing mode
            yPos += 20;
            var timingMode = distance > Settings.Movement.NormalFollowDistance.Value * 2 ? "SLOW (Far)" : "NORMAL";
            Graphics.DrawText($"Timing Mode: {timingMode}", new Vector2(10, yPos), SharpDX.Color.Cyan);
        }
        else
        {
            Graphics.DrawText($"Leader not found: {Settings.LeaderName.Value}", new Vector2(10, yPos), SharpDX.Color.Red);
            yPos += 20;
            Graphics.DrawText("Timing Mode: VERY SLOW (No Leader)", new Vector2(10, yPos), SharpDX.Color.Orange);
        }
        yPos += 20;
        
        Graphics.DrawText($"Tasks: {_tasks.Count}", new Vector2(10, yPos), SharpDX.Color.White);
        yPos += 20;
        
        // Show next action time
        var nextActionDelay = (_nextBotAction - DateTime.Now).TotalMilliseconds;
        if (nextActionDelay > 0)
        {
            Graphics.DrawText($"Next Action: {nextActionDelay:F0}ms", new Vector2(10, yPos), SharpDX.Color.Gray);
            yPos += 20;
        }
        
        // Show rate limiting status
        if (IsActionRateLimited())
        {
            Graphics.DrawText("RATE LIMITED - Slowing down", new Vector2(10, yPos), SharpDX.Color.Red);
            yPos += 20;
        }
        
        // Show recent actions count
        Graphics.DrawText($"Actions/sec: {_recentActions.Count}/{MAX_ACTIONS_PER_SECOND}", new Vector2(10, yPos), SharpDX.Color.Gray);
        yPos += 20;
        
        // Show gem leveling status
        if (_isLevelingGems)
        {
            Graphics.DrawText("LEVELING GEMS", new Vector2(10, yPos), SharpDX.Color.Cyan);
        }
        else if (Settings.Gems.AutoLevelGems.Value && HasGemsToLevel())
        {
            Graphics.DrawText("GEMS AVAILABLE", new Vector2(10, yPos), SharpDX.Color.Green);
        }
        
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
