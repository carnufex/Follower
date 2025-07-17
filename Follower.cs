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
using System.Threading;

namespace Follower;

public class Follower : BaseSettingsPlugin<FollowerSettings>
{
    // Core following state - simplified
    private Entity _followTarget;
    private Vector3 _lastTargetPosition = Vector3.Zero;
    private List<TaskNode> _tasks = new List<TaskNode>();
    private DateTime _nextBotAction = DateTime.Now;
    private Random _random = new Random();
    
    // Advanced pathfinding system
    private PathFinder _pathFinder;
    private bool _pathFinderInitialized = false;
    private Dictionary<Vector2i, DateTime> _directionFieldCache = new Dictionary<Vector2i, DateTime>();
    private CancellationTokenSource _pathfindingCancellation = new CancellationTokenSource();
    private Vector2i _lastLeaderGridPosition = new Vector2i(-1, -1);
    private DateTime _lastPathfindingUpdate = DateTime.MinValue;
    private List<Vector2i> _currentPath = new List<Vector2i>();
    private int _currentPathIndex = 0;
    
    // Action tracking to prevent kicks
    private Queue<DateTime> _recentActions = new Queue<DateTime>();
    private const int MAX_ACTIONS_PER_SECOND = 2; // Very conservative limit
    
    // Leader action state tracking (Phase 1)
    private ActionFlags _leaderActionState = ActionFlags.None;
    private DateTime _lastActionStateUpdate = DateTime.MinValue;
    private bool _combatModeActive = false;
    private DateTime _lastCombatStateChange = DateTime.MinValue;
    
    // Improved stuck detection and course correction
    private Vector3 _lastPlayerPosition = Vector3.Zero;
    private DateTime _lastPositionUpdate = DateTime.MinValue;
    private Queue<Vector3> _recentPositions = new Queue<Vector3>();
    private const int MAX_POSITION_HISTORY = 10;
    private bool _isStuck = false;
    private DateTime _stuckDetectionTime = DateTime.MinValue;
    private Vector3 _stuckPosition = Vector3.Zero;
    private int _stuckRecoveryAttempts = 0;
    private List<Vector3> _alternativeWaypoints = new List<Vector3>();
    private bool _usingAlternativeRoute = false;
    
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

    public override void AreaChange(AreaInstance area)
    {
        // Cancel any ongoing pathfinding
        _pathfindingCancellation.Cancel();
        _pathfindingCancellation = new CancellationTokenSource();
        
        // Reset pathfinding state
        _pathFinderInitialized = false;
        _pathFinder = null;
        _directionFieldCache.Clear();
        _currentPath.Clear();
        _currentPathIndex = 0;
        _lastLeaderGridPosition = new Vector2i(-1, -1);
        
        // Reset state for new area
        _tasks.Clear();
        _followTarget = null;
        _lastTargetPosition = Vector3.Zero;
        _areaTransitions.Clear();
        _hasUsedWP = false;
        
        // Reset gem leveling state
        _lastGemLevelCheck = DateTime.MinValue;
        _isLevelingGems = false;
        
        // Reset stuck detection state
        _lastPlayerPosition = Vector3.Zero;
        _lastPositionUpdate = DateTime.MinValue;
        _recentPositions.Clear();
        _isStuck = false;
        _stuckDetectionTime = DateTime.MinValue;
        _stuckPosition = Vector3.Zero;
        _stuckRecoveryAttempts = 0;
        _alternativeWaypoints.Clear();
        _usingAlternativeRoute = false;
        
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
        
        // Initialize pathfinder for new area
        InitializePathFinder();
        
        base.AreaChange(area);
    }

    private void InitializePathFinder()
    {
        try
        {
            // Get terrain data from game
            _tiles = GameController.IngameState.Data.RawTerrainData;
            
            if (_tiles != null)
            {
                _numRows = _tiles.GetLength(0);
                _numCols = _tiles.GetLength(1);
                
                // Initialize pathfinder with terrain data
                _pathFinder = new PathFinder(_tiles, new[] { 1, 2, 3, 4, 5 });
                _pathFinderInitialized = true;
                
                if (Settings.Debug.EnableActionStateLogging.Value)
                {
                    LogMessage($"PathFinder initialized for area: {_numRows}x{_numCols}", 3);
                }
            }
            else
            {
                LogMessage("Failed to get terrain data for pathfinding", 1);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error initializing pathfinder: {ex.Message}", 1);
            _pathFinderInitialized = false;
        }
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
            
            // Update leader action state (Phase 1)
            UpdateLeaderActionState();
            
            // Update stuck detection and course correction
            UpdateStuckDetection();
            
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
    /// Updates the leader's action state for smarter following behavior
    /// </summary>
    private void UpdateLeaderActionState()
    {
        if (_followTarget == null)
        {
            _leaderActionState = ActionFlags.None;
            _combatModeActive = false;
            return;
        }

        try
        {
            // Get the leader's current action state
            if (_followTarget.TryGetComponent<Actor>(out var actorComp))
            {
                var currentActionState = actorComp.Action;
                var previousActionState = _leaderActionState;
                _leaderActionState = currentActionState;
                
                var now = DateTime.Now;
                
                // Determine if leader is in combat based on action flags
                var isInCombat = IsLeaderInCombat(currentActionState);
                
                // Update combat mode with state change tracking
                if (isInCombat != _combatModeActive)
                {
                    _combatModeActive = isInCombat;
                    _lastCombatStateChange = now;
                    
                    // Log combat state changes for debugging
                    if (Settings.Debug.EnableActionStateLogging.Value)
                    {
                        LogMessage($"Leader combat state changed: {(isInCombat ? "COMBAT" : "PEACEFUL")}", 3);
                    }
                }
                
                _lastActionStateUpdate = now;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"UpdateLeaderActionState error: {ex.Message}", 1);
            // Fallback to safe defaults
            _leaderActionState = ActionFlags.None;
            _combatModeActive = false;
        }
    }
    
    /// <summary>
    /// Determines if the leader is in combat based on action flags
    /// </summary>
    private bool IsLeaderInCombat(ActionFlags actionFlags)
    {
        // Check for combat-related action flags
        return (actionFlags & ActionFlags.UsingAbility) != 0 ||
               (actionFlags & ActionFlags.HasMines) != 0 ||
               (actionFlags & ActionFlags.Dead) != 0 ||
               actionFlags.ToString().Contains("Attack") ||
               actionFlags.ToString().Contains("Cast") ||
               actionFlags.ToString().Contains("Skill");
    }
    
    /// <summary>
    /// Updates stuck detection system with position tracking and course correction
    /// </summary>
    private void UpdateStuckDetection()
    {
        var currentPosition = GameController.Player.Pos;
        var now = DateTime.Now;
        
        // Update position history
        _recentPositions.Enqueue(currentPosition);
        if (_recentPositions.Count > MAX_POSITION_HISTORY)
        {
            _recentPositions.Dequeue();
        }
        
        // Check if we've moved significantly since last update
        if (Vector3.Distance(currentPosition, _lastPlayerPosition) > Settings.Safety.StuckDistanceThreshold.Value)
        {
            _lastPlayerPosition = currentPosition;
            _lastPositionUpdate = now;
            
            // Reset stuck state if we're moving
            if (_isStuck)
            {
                _isStuck = false;
                _stuckRecoveryAttempts = 0;
                _usingAlternativeRoute = false;
                _alternativeWaypoints.Clear();
                
                if (Settings.Debug.EnableActionStateLogging.Value)
                {
                    LogMessage("Unstuck - movement detected", 3);
                }
            }
        }
        
        // Check for stuck condition
        if (!_isStuck && _recentPositions.Count >= 5)
        {
            var timeSinceMovement = now - _lastPositionUpdate;
            
            // Check if we've been stationary for too long
            if (timeSinceMovement > TimeSpan.FromMilliseconds(Settings.Safety.StuckTimeThreshold.Value))
            {
                var positionVariance = CalculatePositionVariance();
                
                // If position variance is low, we're probably stuck
                if (positionVariance < Settings.Safety.StuckDistanceThreshold.Value)
                {
                    DetectStuckCondition(currentPosition, now);
                }
            }
        }
        
        // Handle stuck recovery
        if (_isStuck)
        {
            HandleStuckRecovery(currentPosition, now);
        }
    }
    
    /// <summary>
    /// Calculates position variance to detect stuck conditions
    /// </summary>
    private float CalculatePositionVariance()
    {
        if (_recentPositions.Count < 2)
            return float.MaxValue;
        
        var positions = _recentPositions.ToArray();
        var avgPosition = Vector3.Zero;
        
        // Calculate average position
        foreach (var pos in positions)
        {
            avgPosition += pos;
        }
        avgPosition /= positions.Length;
        
        // Calculate variance
        var totalVariance = 0f;
        foreach (var pos in positions)
        {
            totalVariance += Vector3.Distance(pos, avgPosition);
        }
        
        return totalVariance / positions.Length;
    }
    
    /// <summary>
    /// Detects and handles stuck conditions
    /// </summary>
    private void DetectStuckCondition(Vector3 currentPosition, DateTime now)
    {
        _isStuck = true;
        _stuckDetectionTime = now;
        _stuckPosition = currentPosition;
        _stuckRecoveryAttempts = 0;
        
        if (Settings.Debug.EnableActionStateLogging.Value)
        {
            LogMessage($"Stuck detected at position: {currentPosition}", 2);
        }
        
        // Generate alternative waypoints for course correction
        GenerateAlternativeWaypoints(currentPosition);
    }
    
    /// <summary>
    /// Handles stuck recovery logic
    /// </summary>
    private void HandleStuckRecovery(Vector3 currentPosition, DateTime now)
    {
        var stuckDuration = now - _stuckDetectionTime;
        
        // If we've been stuck for too long, try more aggressive recovery
        if (stuckDuration > TimeSpan.FromSeconds(10))
        {
            _stuckRecoveryAttempts++;
            
            if (_stuckRecoveryAttempts <= Settings.Safety.MaxStuckRecoveryAttempts.Value)
            {
                // Clear current tasks and use alternative route
                _tasks.Clear();
                _usingAlternativeRoute = true;
                
                if (Settings.Debug.EnableActionStateLogging.Value)
                {
                    LogMessage($"Stuck recovery attempt #{_stuckRecoveryAttempts}", 2);
                }
            }
            else
            {
                // Too many recovery attempts, pause briefly
                _nextBotAction = DateTime.Now.AddSeconds(5);
                _stuckRecoveryAttempts = 0;
                
                if (Settings.Debug.EnableActionStateLogging.Value)
                {
                    LogMessage("Max stuck recovery attempts reached, pausing", 1);
                }
            }
        }
    }
    
    /// <summary>
    /// Generates alternative waypoints for course correction
    /// </summary>
    private void GenerateAlternativeWaypoints(Vector3 stuckPosition)
    {
        _alternativeWaypoints.Clear();
        
        if (_followTarget == null)
            return;
        
        var leaderPos = _followTarget.Pos;
        var directionToLeader = leaderPos - stuckPosition;
        var distance = directionToLeader.Length();
        
        // Base distance for alternative waypoints
        var baseDistance = Settings.Safety.RandomMovementRange.Value;
        
        if (distance > 0)
        {
            directionToLeader = directionToLeader / distance;
            
            // Generate waypoints in a semi-circle around the stuck position
            var angles = new float[] { -45f, -22.5f, 0f, 22.5f, 45f };
            
            foreach (var angle in angles)
            {
                var radians = angle * (float)Math.PI / 180f;
                var rotatedDirection = RotateVector(directionToLeader, radians);
                var alternativePos = stuckPosition + rotatedDirection * baseDistance;
                
                // Check if this position is likely to be pathable
                if (IsPositionLikelyPathable(alternativePos))
                {
                    _alternativeWaypoints.Add(alternativePos);
                }
            }
        }
        
        // Add random movement points if no good alternatives found
        if (_alternativeWaypoints.Count == 0)
        {
            for (int i = 0; i < 3; i++)
            {
                var randomDirection = new Vector3(
                    _random.Next(-100, 100),
                    _random.Next(-100, 100),
                    0
                );
                randomDirection = randomDirection / randomDirection.Length();
                var randomPos = stuckPosition + randomDirection * baseDistance;
                _alternativeWaypoints.Add(randomPos);
            }
        }
    }
    
    /// <summary>
    /// Rotates a vector by the given angle in radians
    /// </summary>
    private Vector3 RotateVector(Vector3 vector, float angleRadians)
    {
        var cos = (float)Math.Cos(angleRadians);
        var sin = (float)Math.Sin(angleRadians);
        
        return new Vector3(
            vector.X * cos - vector.Y * sin,
            vector.X * sin + vector.Y * cos,
            vector.Z
        );
    }
    
    /// <summary>
    /// Advanced check if a position is pathable using the pathfinder
    /// </summary>
    private bool IsPositionLikelyPathable(Vector3 position)
    {
        // Use pathfinder if available
        if (_pathFinderInitialized && _pathFinder != null)
        {
            var gridPos = PathFinder.WorldToGrid(position);
            
            // Check bounds
            if (gridPos.X < 0 || gridPos.X >= _numCols || gridPos.Y < 0 || gridPos.Y >= _numRows)
                return false;
            
            // Use pathfinder's terrain evaluation
            return _tiles[gridPos.Y, gridPos.X] > 0 && _tiles[gridPos.Y, gridPos.X] < 100;
        }
        
        // Fall back to basic checking
        if (position.X < 0 || position.Y < 0)
            return false;
        
        // Check against terrain data if available
        if (_tiles != null && _numRows > 0 && _numCols > 0)
        {
            var tileX = (int)(position.X / 23f); // Convert to tile coordinates
            var tileY = (int)(position.Y / 23f);
            
            if (tileX >= 0 && tileX < _numCols && tileY >= 0 && tileY < _numRows)
            {
                var terrainValue = _tiles[tileY, tileX];
                // Values 1-5 are generally pathable, 255 is blocked
                return terrainValue > 0 && terrainValue < 100;
            }
        }
        
        return true; // Default to pathable if no terrain data
    }
    
    /// <summary>
    /// Gets the optimal follow distance based on combat state
    /// </summary>
    private int GetSmartFollowDistance(float currentDistance)
    {
        if (_combatModeActive)
        {
            // In combat, maintain a larger distance for safety
            return Math.Max(Settings.Movement.PathfindingNodeDistance.Value * 2, 400);
        }
        
        // Normal following distance
        return Settings.Movement.PathfindingNodeDistance.Value;
    }
    
    /// <summary>
    /// Determines if the follower should follow in the current state
    /// </summary>
    private bool ShouldFollowInCurrentState(float currentDistance)
    {
        // Always follow if leader is very far away
        if (currentDistance > Settings.Movement.LeaderMaxDistance.Value)
            return true;
        
        // In combat mode, be more conservative about following
        if (_combatModeActive)
        {
            // Only follow if leader is quite far away or has been in combat for a while
            var combatDuration = DateTime.Now - _lastCombatStateChange;
            return currentDistance > Settings.Movement.NormalFollowDistance.Value || 
                   combatDuration > TimeSpan.FromSeconds(5);
        }
        
        // Normal following behavior
        return currentDistance > Settings.Movement.PathfindingNodeDistance.Value;
    }
    
    /// <summary>
    /// Gets the smart follow position based on leader state with predictive pathfinding
    /// </summary>
    private Vector3 GetSmartFollowPosition()
    {
        if (_followTarget == null)
            return Vector3.Zero;
        
        // Phase 4: Implement predictive pathfinding
        var currentLeaderPos = _followTarget.Pos;
        
        // If predictive following is enabled and we have a path history
        if (Settings.Pathfinding.EnablePredictiveFollowing.Value && _pathFinderInitialized && _lastTargetPosition != Vector3.Zero)
        {
            var leaderMovement = currentLeaderPos - _lastTargetPosition;
            var movementDistance = leaderMovement.Length();
            
            // If leader is moving significantly, predict next position
            if (movementDistance > 50f) // Minimum movement threshold
            {
                var predictedDistance = Math.Min(movementDistance * 2f, Settings.Pathfinding.PredictionDistance.Value); // Predict ahead but cap it
                var predictedPosition = currentLeaderPos + Vector3.Normalize(leaderMovement) * predictedDistance;
                
                // Validate predicted position is pathable
                if (_pathFinder != null)
                {
                    var predictedGrid = PathFinder.WorldToGrid(predictedPosition);
                    var currentGrid = PathFinder.WorldToGrid(currentLeaderPos);
                    
                    // Check if predicted position is within reasonable bounds and pathable
                    if (predictedGrid.X >= 0 && predictedGrid.X < _numCols && 
                        predictedGrid.Y >= 0 && predictedGrid.Y < _numRows)
                    {
                        // Test if we can path to the predicted position
                        var testPath = _pathFinder.FindPath(currentGrid, predictedGrid);
                        if (testPath != null && testPath.Count > 0)
                        {
                            if (Settings.Debug.EnableActionStateLogging.Value)
                            {
                                LogMessage($"Predictive pathfinding: Using predicted position {predictedDistance:F1} units ahead", 3);
                            }
                            return predictedPosition;
                        }
                    }
                }
            }
        }
        
        // Fall back to current position if prediction fails
        return currentLeaderPos;
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
    /// Plans movement tasks based on current state using advanced pathfinding
    /// </summary>
    private void PlanTasks()
    {
        // Clear old tasks if we have too many
        if (_tasks.Count > 5)
            _tasks.Clear();
        
        // If we're stuck and using alternative route, prioritize alternative waypoints
        if (_isStuck && _usingAlternativeRoute && _alternativeWaypoints.Count > 0)
        {
            PlanAlternativeRoute();
            return;
        }
        
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
        
        // Combat mode awareness: Adjust following behavior based on leader's state
        var followDistance = GetSmartFollowDistance(distance);
        var shouldFollow = ShouldFollowInCurrentState(distance);
        
        // If leader is close, don't add tasks unless close follow is enabled
        if (distance < Settings.Movement.ClearPathDistance.Value)
        {
            if (Settings.Movement.IsCloseFollowEnabled.Value && distance > followDistance && shouldFollow)
            {
                var targetPosition = GetSmartFollowPosition();
                PlanPathfindingTasks(targetPosition, followDistance);
            }
            
            // Check for waypoint claiming (only when not in combat)
            if (!_hasUsedWP && !_combatModeActive)
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
        
        // Leader is far - decide whether to follow based on current state
        if (shouldFollow)
        {
            var targetPosition = GetSmartFollowPosition();
            
            // Check if we need to recalculate path
            if (ShouldRecalculatePath(targetPosition))
            {
                PlanPathfindingTasks(targetPosition, followDistance);
            }
            else if (_tasks.Count == 0)
            {
                // No current path, create one
                PlanPathfindingTasks(targetPosition, followDistance);
            }
        }
        
        _lastTargetPosition = _followTarget.Pos;
    }

    /// <summary>
    /// Plans tasks using advanced pathfinding instead of simple movement
    /// </summary>
    private void PlanPathfindingTasks(Vector3 targetPosition, float followDistance)
    {
        // Check if advanced pathfinding is enabled
        if (!Settings.Pathfinding.EnableAdvancedPathfinding.Value || !_pathFinderInitialized)
        {
            // Fall back to simple movement if pathfinder disabled or not ready
            _tasks.Add(new TaskNode(targetPosition, (int)followDistance));
            return;
        }

        try
        {
            var playerGridPos = PathFinder.WorldToGrid(GameController.Player.Pos);
            var targetGridPos = PathFinder.WorldToGrid(targetPosition);
            
            // Skip pathfinding if target is too close
            if (playerGridPos.Distance(targetGridPos) < 2)
            {
                _tasks.Add(new TaskNode(targetPosition, (int)followDistance));
                return;
            }
            
            // Use existing path if we have one and it's still valid
            if (_currentPath.Count > 0 && _currentPathIndex < _currentPath.Count)
            {
                var remainingPath = _currentPath.Skip(_currentPathIndex).ToList();
                AddPathToTasks(remainingPath, followDistance);
                return;
            }
            
            // Calculate new path with timeout
            var pathfindingTask = Task.Run(() => _pathFinder.FindPath(playerGridPos, targetGridPos));
            var timeoutTask = Task.Delay(Settings.Pathfinding.PathfindingTimeout.Value);
            
            var completedTask = Task.WaitAny(pathfindingTask, timeoutTask);
            
            List<Vector2i> path = null;
            if (completedTask == 0 && pathfindingTask.IsCompletedSuccessfully)
            {
                path = pathfindingTask.Result;
            }
            
            if (path != null && path.Count > 0)
            {
                _currentPath = path;
                _currentPathIndex = 0;
                AddPathToTasks(path, followDistance);
                
                // Pre-calculate direction field for leader position (Phase 3)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _pathFinder.PreCalculateDirectionFieldAsync(targetGridPos, _pathfindingCancellation.Token);
                        _directionFieldCache[targetGridPos] = DateTime.Now;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when area changes
                    }
                    catch (Exception ex)
                    {
                        if (Settings.Debug.EnableActionStateLogging.Value)
                        {
                            LogMessage($"Error pre-calculating direction field: {ex.Message}", 2);
                        }
                    }
                });
                
                if (Settings.Debug.EnableActionStateLogging.Value)
                {
                    LogMessage($"Pathfinding: Found path with {path.Count} waypoints", 3);
                }
            }
            else
            {
                // No path found or timeout, fall back to simple movement
                _tasks.Add(new TaskNode(targetPosition, (int)followDistance));
                
                if (Settings.Debug.EnableActionStateLogging.Value)
                {
                    LogMessage($"Pathfinding: No path found or timeout, using direct movement", 2);
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error in pathfinding: {ex.Message}", 1);
            // Fall back to simple movement
            _tasks.Add(new TaskNode(targetPosition, (int)followDistance));
        }
    }

    /// <summary>
    /// Adds pathfinding waypoints to the task list
    /// </summary>
    private void AddPathToTasks(List<Vector2i> path, float followDistance)
    {
        // Add waypoints as tasks, but don't add too many at once
        var waypointsToAdd = Math.Min(path.Count, Settings.Pathfinding.MaxWaypointsPerUpdate.Value);
        
        for (int i = 0; i < waypointsToAdd; i++)
        {
            var worldPos = PathFinder.GridToWorld(path[i]);
            var nodeDistance = i == waypointsToAdd - 1 ? (int)followDistance : Settings.Movement.PathfindingNodeDistance.Value;
            _tasks.Add(new TaskNode(worldPos, nodeDistance));
        }
    }

    /// <summary>
    /// Determines if we should recalculate the path
    /// </summary>
    private bool ShouldRecalculatePath(Vector3 targetPosition)
    {
        if (!_pathFinderInitialized || _currentPath.Count == 0)
            return true;
        
        var currentTargetGrid = PathFinder.WorldToGrid(targetPosition);
        
        // Recalculate if leader moved significantly
        if (currentTargetGrid.Distance(_lastLeaderGridPosition) > Settings.Pathfinding.PathRecalculationThreshold.Value)
        {
            _lastLeaderGridPosition = currentTargetGrid;
            return true;
        }
        
        // Recalculate if we've been following the same path for too long
        if (DateTime.Now - _lastPathfindingUpdate > TimeSpan.FromSeconds(5))
        {
            _lastPathfindingUpdate = DateTime.Now;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Plans alternative route when stuck
    /// </summary>
    private void PlanAlternativeRoute()
    {
        if (_alternativeWaypoints.Count == 0)
            return;
        
        var playerPos = GameController.Player.Pos;
        
        // Find the closest alternative waypoint that we haven't tried yet
        var bestWaypoint = _alternativeWaypoints
            .OrderBy(w => Vector3.Distance(playerPos, w))
            .FirstOrDefault();
        
        if (bestWaypoint != Vector3.Zero)
        {
            _tasks.Add(new TaskNode(bestWaypoint, Settings.Movement.PathfindingNodeDistance.Value));
            
            // Remove the waypoint so we don't keep trying it
            _alternativeWaypoints.Remove(bestWaypoint);
            
            if (Settings.Debug.EnableActionStateLogging.Value)
            {
                LogMessage($"Using alternative waypoint: {bestWaypoint}", 3);
            }
        }
    }
    
    /// <summary>
    /// Gets the best alternative waypoint for course correction
    /// </summary>
    private Vector3 GetBestAlternativeWaypoint(Vector3 targetPosition)
    {
        if (_alternativeWaypoints.Count == 0)
            return Vector3.Zero;
        
        var playerPos = GameController.Player.Pos;
        
        // Find waypoint that's closest to the target but not too close to current position
        var bestWaypoint = _alternativeWaypoints
            .Where(w => Vector3.Distance(playerPos, w) > Settings.Movement.PathfindingNodeDistance.Value / 2)
            .OrderBy(w => Vector3.Distance(w, targetPosition))
            .FirstOrDefault();
        
        return bestWaypoint != Vector3.Zero ? bestWaypoint : _alternativeWaypoints.First();
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
        
        // Combat mode awareness: Use extra conservative timing during combat
        if (_combatModeActive)
        {
            baseDelay = Math.Max(baseDelay * 2, 500); // Double delay during combat, minimum 500ms
            randomDelay = _random.Next(200, 400); // Larger random delay during combat
        }
        
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
            
            // Update pathfinding index if we're following a calculated path
            if (_currentPath.Count > 0 && _currentPathIndex < _currentPath.Count)
            {
                _currentPathIndex++;
                
                if (Settings.Debug.EnableActionStateLogging.Value)
                {
                    LogMessage($"Pathfinding: Completed waypoint {_currentPathIndex}/{_currentPath.Count}", 3);
                }
            }
        }
        else
        {
            // Track attempts and remove if too many
            currentTask.AttemptCount++;
            if (currentTask.AttemptCount > Settings.Safety.MaxTaskAttempts.Value)
            {
                _tasks.RemoveAt(0);
                
                // Also increment pathfinding index to skip problematic waypoint
                if (_currentPath.Count > 0 && _currentPathIndex < _currentPath.Count)
                {
                    _currentPathIndex++;
                    
                    if (Settings.Debug.EnableActionStateLogging.Value)
                    {
                        LogMessage($"Pathfinding: Skipping problematic waypoint {_currentPathIndex}/{_currentPath.Count}", 2);
                    }
                }
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

    /// <summary>
    /// Draws debug rectangles around UI elements for debugging UI avoidance
    /// </summary>
    private void DrawUIDebugRectangles()
    {
        try
        {
            var ingameUI = GameController.IngameState.IngameUi;
            if (ingameUI == null) return;

            var windowRect = GameController.Window.GetWindowRectangle();
            
            // Draw rectangles for common UI panels
            DrawUIElementRectangle(ingameUI.InventoryPanel, "Inventory", SharpDX.Color.Red);
            DrawUIElementRectangle(ingameUI.StashElement, "Stash", SharpDX.Color.Orange);
            DrawUIElementRectangle(ingameUI.TreePanel, "Tree", SharpDX.Color.Yellow);
            DrawUIElementRectangle(ingameUI.Atlas, "Atlas", SharpDX.Color.Green);
            DrawUIElementRectangle(ingameUI.WorldMap, "World Map", SharpDX.Color.Blue);
            DrawUIElementRectangle(ingameUI.OpenLeftPanel, "Left Panel", SharpDX.Color.Purple);
            DrawUIElementRectangle(ingameUI.OpenRightPanel, "Right Panel", SharpDX.Color.Cyan);
            DrawUIElementRectangle(ingameUI.GuildPanel, "Guild", SharpDX.Color.Pink);
            DrawUIElementRectangle(ingameUI.ChallengePanel, "Challenge", SharpDX.Color.Brown);
            
            // Draw edge exclusion zones
            if (Settings.UIAvoidance.ExcludeTopEdge.Value)
            {
                var topRect = new RectangleF(0, 0, windowRect.Width, Settings.UIAvoidance.TopEdgeExclusionHeight.Value);
                Graphics.DrawFrame(topRect, SharpDX.Color.Red, 2);
                Graphics.DrawText("TOP EXCLUSION ZONE", new Vector2(10, 10), SharpDX.Color.Red);
            }
            
            if (Settings.UIAvoidance.ExcludeBottomEdge.Value)
            {
                var bottomRect = new RectangleF(0, windowRect.Height - Settings.UIAvoidance.BottomEdgeExclusionHeight.Value, 
                    windowRect.Width, Settings.UIAvoidance.BottomEdgeExclusionHeight.Value);
                Graphics.DrawFrame(bottomRect, SharpDX.Color.Red, 2);
                Graphics.DrawText("BOTTOM EXCLUSION ZONE", new Vector2(10, windowRect.Height - 30), SharpDX.Color.Red);
            }
            
            // Draw safe margins
            var safeMargin = 100; // Same as used in WorldToScreenPosition
            var safeRect = new RectangleF(safeMargin, safeMargin, 
                windowRect.Width - (safeMargin * 2), windowRect.Height - (safeMargin * 2));
            Graphics.DrawFrame(safeRect, SharpDX.Color.Green, 1);
            Graphics.DrawText("SAFE AREA", new Vector2(safeMargin + 10, safeMargin + 10), SharpDX.Color.Green);
        }
        catch (Exception ex)
        {
            LogMessage($"DrawUIDebugRectangles error: {ex.Message}", 1);
        }
    }
    
    /// <summary>
    /// Draws a debug rectangle for a specific UI element
    /// </summary>
    private void DrawUIElementRectangle(Element element, string name, SharpDX.Color color)
    {
        try
        {
            if (element?.IsVisible == true)
            {
                var rect = element.GetClientRect();
                Graphics.DrawFrame(rect, color, 2);
                Graphics.DrawText($"{name} UI", new Vector2(rect.X + 5, rect.Y + 5), color);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"DrawUIElementRectangle error for {name}: {ex.Message}", 1);
        }
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
            
            // Show combat mode status
            yPos += 20;
            var combatColor = _combatModeActive ? SharpDX.Color.Red : SharpDX.Color.Green;
            Graphics.DrawText($"Combat Mode: {(_combatModeActive ? "ACTIVE" : "INACTIVE")}", new Vector2(10, yPos), combatColor);
            
            // Show action state details
            yPos += 20;
            Graphics.DrawText($"Action State: {_leaderActionState}", new Vector2(10, yPos), SharpDX.Color.Cyan);
            
            // Show timing mode
            yPos += 20;
            var timingMode = _combatModeActive ? "COMBAT (Extra Slow)" : 
                           distance > Settings.Movement.NormalFollowDistance.Value * 2 ? "SLOW (Far)" : "NORMAL";
            Graphics.DrawText($"Timing Mode: {timingMode}", new Vector2(10, yPos), SharpDX.Color.Cyan);
        }
        else
        {
            Graphics.DrawText($"Leader not found: {Settings.LeaderName.Value}", new Vector2(10, yPos), SharpDX.Color.Red);
            yPos += 20;
            Graphics.DrawText("Combat Mode: N/A", new Vector2(10, yPos), SharpDX.Color.Gray);
            yPos += 20;
            Graphics.DrawText("Timing Mode: VERY SLOW (No Leader)", new Vector2(10, yPos), SharpDX.Color.Orange);
        }
        yPos += 20;
        
        Graphics.DrawText($"Tasks: {_tasks.Count}", new Vector2(10, yPos), SharpDX.Color.White);
        yPos += 20;
        
        // Show stuck detection status
        var stuckColor = _isStuck ? SharpDX.Color.Red : SharpDX.Color.Green;
        Graphics.DrawText($"Stuck Detection: {(_isStuck ? "STUCK" : "NORMAL")}", new Vector2(10, yPos), stuckColor);
        yPos += 20;
        
        if (_isStuck)
        {
            Graphics.DrawText($"Stuck Recovery Attempts: {_stuckRecoveryAttempts}", new Vector2(10, yPos), SharpDX.Color.Orange);
            yPos += 20;
            Graphics.DrawText($"Alternative Waypoints: {_alternativeWaypoints.Count}", new Vector2(10, yPos), SharpDX.Color.Cyan);
            yPos += 20;
        }
        
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
        
        // Draw UI debug rectangles if enabled
        if (Settings.UIAvoidance.ShowUIDebugRectangles.Value)
        {
            DrawUIDebugRectangles();
        }
    }
}
