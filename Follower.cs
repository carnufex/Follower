using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using SharpDX;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using static ExileCore.PoEMemory.MemoryObjects.ServerInventory;
using System.Threading.Tasks;
using System;
using System.Drawing;
using System.Threading;
using System.Collections;
using ExileCore.Shared;

namespace Follower;

public enum PathStatus
{
    Clear,              // The path is fully walkable
    Dashable,           // The path is blocked by a dashable obstacle (terrain value 2)
    Blocked,            // The path is blocked by an impassable wall or terrain (terrain value 255)
    Invalid             // The start or end point is out of bounds
}
public class Follower : BaseSettingsPlugin<FollowerSettings>
{

    private Random random = new Random();
    private Camera Camera => GameController.Game.IngameState.Camera;
    private Dictionary<uint, Entity> _areaTransitions = new Dictionary<uint, Entity>();

    private Vector3 _lastTargetPosition;
    private Vector3 _lastPlayerPosition;
    private Entity _followTarget;

    private bool _hasUsedWP = false;

	private DateTime _lastLinkAttempt = DateTime.MinValue;

	// Teleport detection variables
	private Vector3 _lastKnownGoodPosition = Vector3.Zero;
    private float _lastDistanceToTarget = 0f;
    private bool _isSearchingForTeleport = false;
    private DateTime _teleportSearchStartTime;
    private readonly TimeSpan _teleportSearchTimeout = TimeSpan.FromSeconds(30); // Stop searching after 30 seconds
    
    // Gem leveling integration variables
    private DateTime _lastGemLevelCheck = DateTime.MinValue;
    private bool _isLevelingGems = false;
    private DateTime _gemLevelingStartTime;
    
    // Dash tracking variables
    private DateTime _lastDashTime = DateTime.MinValue;
    
    // PickItV2 coordination variables
    private DateTime _pickItV2YieldStartTime = DateTime.MinValue;
    
    // Coroutine variables
    private Coroutine _followerCoroutine;
    private Coroutine _postTransitionCoroutine;
    private bool _isTransitioning = false;
    private DateTime _areaChangeTime = DateTime.MinValue;


    private List<TaskNode> _tasks = new List<TaskNode>();
    private DateTime _nextBotAction = DateTime.Now;

    private int _numRows, _numCols;
    private byte[,] _tiles;
    private DateTime _lastTerrainRefresh = DateTime.MinValue;
    
    // Stuck detection variables
    private Vector3 _lastStuckCheckPosition = Vector3.Zero;
    private DateTime _lastStuckCheckTime = DateTime.MinValue;
    private DateTime _stuckDetectionStartTime = DateTime.MinValue;
    private bool _isStuckDetectionActive = false;
    private int _stuckRecoveryAttempts = 0;
    
    // Unreachable position detection variables
    private Dictionary<Vector3, int> _positionAttempts = new Dictionary<Vector3, int>();
    private Vector3 _lastAttemptedPosition = Vector3.Zero;
    
    // Death handling variables
    private bool _wasDead = false;
    private DateTime _deathDetectedTime = DateTime.MinValue;
    private DateTime _lastDeathCheckTime = DateTime.MinValue;
    private bool _isWaitingForResurrection = false;
    
    // Multiple leader variables
    private List<string> _leaderNamesList = new List<string>();
    private Entity _previousLeader = null;
    private DateTime _lastLeaderSwitchTime = DateTime.MinValue;
    
    // Safety feature variables
    private bool _wasInGame = true;
    private DateTime _lastSafetyCheckTime = DateTime.MinValue;
    private bool _isPausedForSafety = false;
    
    // Inventory management variables
    private DateTime _lastInventoryCheckTime = DateTime.MinValue;
    private bool _isManagingInventory = false;
    private DateTime _portalUsedTime = DateTime.MinValue;

    public override bool Initialise()
    {
        Name = "Follower";
        Input.RegisterKey(Settings.MovementKey.Value);

        Input.RegisterKey(Settings.ToggleFollower.Value);
        Settings.ToggleFollower.OnValueChanged += () => { Input.RegisterKey(Settings.ToggleFollower.Value); };

        StartFollowerCoroutine();
        return base.Initialise();
    }


    /// <summary>
    /// Clears all pathfinding values. Used on area transitions primarily.
    /// </summary>
    private void ResetPathing()
    {
        _tasks = new List<TaskNode>();
        _followTarget = null;
        _lastTargetPosition = Vector3.Zero;
        _lastPlayerPosition = Vector3.Zero;
        _areaTransitions = new Dictionary<uint, Entity>();
        _hasUsedWP = false;
        
        // Reset teleport detection
        _lastKnownGoodPosition = Vector3.Zero;
        _lastDistanceToTarget = 0f;
        _isSearchingForTeleport = false;
        
        // Reset gem leveling state
        _lastGemLevelCheck = DateTime.MinValue;
        _isLevelingGems = false;
        
        // Reset dash state
        _lastDashTime = DateTime.MinValue;
        
        // Reset PickItV2 coordination state
        _pickItV2YieldStartTime = DateTime.MinValue;
        
        // Reset coroutine state
        _areaChangeTime = DateTime.MinValue;
        
        // Reset terrain refresh timer
        _lastTerrainRefresh = DateTime.MinValue;
        
        // Reset stuck detection
        _lastStuckCheckPosition = Vector3.Zero;
        _lastStuckCheckTime = DateTime.MinValue;
        _stuckDetectionStartTime = DateTime.MinValue;
        _isStuckDetectionActive = false;
        _stuckRecoveryAttempts = 0;
        
        // Reset unreachable position detection
        _positionAttempts.Clear();
        _lastAttemptedPosition = Vector3.Zero;
        
        // Reset death handling
        _wasDead = false;
        _deathDetectedTime = DateTime.MinValue;
        _lastDeathCheckTime = DateTime.MinValue;
        _isWaitingForResurrection = false;
        
        // Reset multiple leader tracking
        _previousLeader = null;
        _lastLeaderSwitchTime = DateTime.MinValue;
        
        // Reset safety features
        _wasInGame = true;
        _lastSafetyCheckTime = DateTime.MinValue;
        _isPausedForSafety = false;
        
        // Reset inventory management
        _lastInventoryCheckTime = DateTime.MinValue;
        _isManagingInventory = false;
        _portalUsedTime = DateTime.MinValue;
    }

	private void MoveMouseToLeader()
	{
		if (_followTarget == null) return;
		var screenPos = WorldToValidScreenPosition(_followTarget.Pos);
		Mouse.SetCursorPos(screenPos);
		System.Threading.Thread.Sleep(50); // Small delay for cursor positioning
	}

	private void HandleLinkBuff()
	{
		if (!Settings.EnableLinkSupport.Value)
			return;

		float linkTime;
		bool hasLink = IsLeaderLinkActive(out linkTime);

		// Apply link if not present or if 5 seconds have passed since last cast
		if (!hasLink || DateTime.Now - _lastLinkAttempt > TimeSpan.FromSeconds(5))
		{
			MoveMouseToLeader(); // Move mouse before pressing key
			Input.KeyDown(Settings.LinkKey);
			Input.KeyUp(Settings.LinkKey);
			_nextBotAction = DateTime.Now.AddMilliseconds(500); // Small delay after reapplying
			_lastLinkAttempt = DateTime.Now;
		}
	}

	private bool IsLeaderLinkActive(out float secondsRemaining)
	{
		secondsRemaining = 0;
		if (_followTarget == null || !_followTarget.IsValid)
			return false;

		var buffs = _followTarget.GetComponent<Buffs>();
		if (buffs?.BuffsList == null)
			return false;

		var linkBuff = buffs.BuffsList.FirstOrDefault(b =>
			b.Name != null && b.Name.Contains("link", StringComparison.OrdinalIgnoreCase));

		if (linkBuff != null && linkBuff.Timer != null)
		{
			secondsRemaining = linkBuff.Timer;
			return true;
		}

		return false;
	}

	public override void AreaChange(AreaInstance area)
    {
        _isTransitioning = true;
        _areaChangeTime = DateTime.Now;
        ResetPathing();

        //Load initial transitions!

        foreach (var transition in GameController.EntityListWrapper.Entities.Where(I => I.Type == ExileCore.Shared.Enums.EntityType.AreaTransition ||
         I.Type == ExileCore.Shared.Enums.EntityType.Portal ||
         I.Type == ExileCore.Shared.Enums.EntityType.TownPortal).ToList())
        {
            if (!_areaTransitions.ContainsKey(transition.Id))
                _areaTransitions.Add(transition.Id, transition);
        }


        var terrain = GameController.IngameState.Data.Terrain;
        var terrainBytes = GameController.Memory.ReadBytes(terrain.LayerMelee.First, terrain.LayerMelee.Size);
        _numCols = (int)(terrain.NumCols - 1) * 23;
        _numRows = (int)(terrain.NumRows - 1) * 23;
        if ((_numCols & 1) > 0)
            _numCols++;

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

        terrainBytes = GameController.Memory.ReadBytes(terrain.LayerRanged.First, terrain.LayerRanged.Size);
        _numCols = (int)(terrain.NumCols - 1) * 23;
        _numRows = (int)(terrain.NumRows - 1) * 23;
        if ((_numCols & 1) > 0)
            _numCols++;
        dataIndex = 0;
        for (int y = 0; y < _numRows; y++)
        {
            for (int x = 0; x < _numCols; x += 2)
            {
                var b = terrainBytes[dataIndex + (x >> 1)];

                var current = _tiles[x, y];
                if (current == 255)
                    _tiles[x, y] = (byte)((b & 0xf) > 3 ? 2 : 255);
                current = _tiles[x + 1, y];
                if (current == 255)
                    _tiles[x + 1, y] = (byte)((b >> 4) > 3 ? 2 : 255);
            }
            dataIndex += terrain.BytesPerRow;
        }

        // Removed PNG generation to prevent accumulating files and improve performance
        // GeneratePNG();
    }

    public void GeneratePNG()
    {
        using (var img = new Bitmap(_numCols, _numRows))
        {
            for (int x = 0; x < _numCols; x++)
                for (int y = 0; y < _numRows; y++)
                {
                    try
                    {
                        var color = System.Drawing.Color.Black;
                        switch (_tiles[x, y])
                        {
                            case 1:
                                color = System.Drawing.Color.White;
                                break;
                            case 2:
                                color = System.Drawing.Color.Gray;
                                break;
                            case 255:
                                color = System.Drawing.Color.Black;
                                break;
                        }
                        img.SetPixel(x, y, color);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
            img.Save("output.png");
        }
    }


    private void HandleToggleFollower()
    {
        if (Settings.ToggleFollower.PressedOnce())
        {
            Settings.IsFollowEnabled.SetValueNoEvent(!Settings.IsFollowEnabled.Value);
            
            // Reset tracking state to re-acquire leader position
            ResetFollowerTracking();
        }
    }

    /// <summary>
    /// Resets follower tracking state without clearing area transitions.
    /// Used when manually toggling follower to ensure fresh tracking.
    /// </summary>
    private void ResetFollowerTracking()
    {
        _tasks = new List<TaskNode>();
        _followTarget = null;
        _lastTargetPosition = Vector3.Zero;
        _lastPlayerPosition = Vector3.Zero;
        _hasUsedWP = false;
        
        // Reset teleport detection
        _lastKnownGoodPosition = Vector3.Zero;
        _lastDistanceToTarget = 0f;
        _isSearchingForTeleport = false;
        
        // Reset gem leveling state
        _lastGemLevelCheck = DateTime.MinValue;
        _isLevelingGems = false;
        
        // Reset dash state
        _lastDashTime = DateTime.MinValue;
        
        // Don't reset _areaTransitions since they're still valid for current area
        // Follower tracking reset - will re-acquire leader position
    }
    
    /// <summary>
    /// Starts the main follower coroutine
    /// </summary>
    private void StartFollowerCoroutine()
    {
        if (_followerCoroutine == null || !_followerCoroutine.Running)
        {
            _followerCoroutine = new Coroutine(FollowerLogic(), this, "FollowerLogic");
            Core.ParallelRunner.Run(_followerCoroutine);
        }
    }
    
    /// <summary>
    /// Starts the post-transition grace period coroutine
    /// </summary>
    private void StartPostTransitionGracePeriod()
    {
        if (_postTransitionCoroutine == null || !_postTransitionCoroutine.Running)
        {
            _postTransitionCoroutine = new Coroutine(PostTransitionGracePeriod(), this, "PostTransitionGracePeriod");
            Core.ParallelRunner.Run(_postTransitionCoroutine);
        }
    }

    /// <summary>
    /// Checks if PickItV2 is currently active and picking up items
    /// </summary>
    /// <returns>True if PickItV2 is active and we should yield control</returns>
    private bool IsPickItV2Active()
    {
        if (!Settings.YieldToPickItV2.Value)
            return false;
            
        try
        {
            // Try to get the PickItV2 plugin status through the plugin bridge
            var pickItIsActiveMethod = GameController.PluginBridge.GetMethod<Func<bool>>("PickIt.IsActive");
            
            if (pickItIsActiveMethod != null)
            {
                bool isActive = pickItIsActiveMethod();
                
                // Track when we started yielding to implement timeout
                if (isActive)
                {
                    if (_pickItV2YieldStartTime == DateTime.MinValue)
                    {
                        _pickItV2YieldStartTime = DateTime.Now;
                    }
                    else
                    {
                        // Check if we've been yielding too long
                        var yieldDuration = DateTime.Now - _pickItV2YieldStartTime;
                        if (yieldDuration.TotalMilliseconds > Settings.PickItV2YieldTimeout.Value)
                        {
                            // Reset the yield timer and continue with follower actions
                            _pickItV2YieldStartTime = DateTime.MinValue;
                            return false;
                        }
                    }
                }
                else
                {
                    // Reset the yield timer when PickItV2 is not active
                    _pickItV2YieldStartTime = DateTime.MinValue;
                }
                
                return isActive;
            }
        }
        catch (Exception ex)
        {
            // If we can't communicate with PickItV2, assume it's not active
            // This prevents the follower from getting stuck if PickItV2 isn't loaded
            _pickItV2YieldStartTime = DateTime.MinValue;
        }
        
        return false;
    }

    /// <summary>
    /// Executes a mouse action only if PickItV2 is not currently active
    /// </summary>
    /// <param name="mouseAction">The mouse action to execute</param>
    /// <returns>True if the action was executed, false if yielded to PickItV2</returns>
    private bool ExecuteMouseActionIfPossible(Action mouseAction)
    {
        if (IsPickItV2Active())
        {
            // PickItV2 is active, yield control and delay our next action
            _nextBotAction = DateTime.Now.AddMilliseconds(Settings.BotInputFrequency.Value);
            return false;
        }
        
        // PickItV2 is not active, execute the mouse action
        mouseAction?.Invoke();
        return true;
    }
    
    /// <summary>
    /// Main follower logic coroutine - replaces the old Tick-based approach
    /// </summary>
    private IEnumerator FollowerLogic()
    {
        while (true)
        {
            var shouldContinue = false;
            var waitTime = Settings.BotInputFrequency.Value;
            
            try
            {
                // Handle toggle follower BEFORE checking if following is enabled
                // This allows the toggle to work both ways
                HandleToggleFollower();
                
                // Check death handling first
                CheckDeathHandling();
                
                // Check safety features
                CheckSafetyFeatures();
                
                // Check if we should be running (includes death and safety checks)
                if (!GameController.Player.IsAlive || !Settings.IsFollowEnabled.Value || _isWaitingForResurrection || _isPausedForSafety)
                {
                    shouldContinue = true;
                    waitTime = 100;
                }
                else
                {
                    // Check if we need to start post-transition grace period
                    if (_isTransitioning && _areaChangeTime != DateTime.MinValue)
                    {
                        StartPostTransitionGracePeriod();
                        _isTransitioning = false;
                    }

                    // Cache the current follow target (using multiple leader support)
                    _followTarget = GetBestAvailableLeader();

					// Check inventory management
					//CheckInventoryManagement();
                    
                    // Refresh terrain data for dynamic obstacle detection
                    RefreshTerrainData();
                    
                    // Check for stuck detection and recovery
                    CheckStuckDetection();
                    
                    // Plan and execute tasks
                    PlanTasks();
                    
                    _lastPlayerPosition = GameController.Player.Pos;
                }
            }
            catch (Exception ex)
            {
                // Handle any errors gracefully
                waitTime = 1000;
            }
            
            // Yield outside of try-catch
            if (shouldContinue)
            {
                yield return new WaitTime(waitTime);
                continue;
            }
            
            // Execute tasks outside of try-catch
            yield return ExecuteTasksCoroutine();
            
            // Yield control for a short time
            yield return new WaitTime(waitTime);
        }
    }
    
    /// <summary>
    /// Post-transition grace period to wait for leader entity to sync
    /// </summary>
    private IEnumerator PostTransitionGracePeriod()
    {
        var timeoutMs = Settings.PostTransitionGracePeriod.Value;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        while (stopwatch.ElapsedMilliseconds < timeoutMs)
        {
            // Check if leader has synced properly
            var leader = GetFollowingTarget();
            if (leader != null && Settings.LeaderName.Value.Length > 0)
            {
                // Leader is present, grace period complete
                break;
            }
            
            yield return new WaitTime(100);
        }
        
        // Grace period complete
        _isTransitioning = false;
    }

    private void MouseoverItem(Entity item)
    {
        var uiLoot = GameController.IngameState.IngameUi.ItemsOnGroundLabels.FirstOrDefault(I => I.IsVisible && I.ItemOnGround.Id == item.Id);
        if (uiLoot != null)
        {
            var clickPos = uiLoot.Label.GetClientRect().Center;
            Mouse.SetCursorPos(new Vector2(
                clickPos.X + random.Next(-15, 15),
                clickPos.Y + random.Next(-10, 10)));
            // Removed Thread.Sleep to prevent UI freezing
        }
    }

    public override Job Tick()
    {
        // Monitor coroutine health and restart if needed
        if (Settings.IsFollowEnabled.Value && GameController.Player.IsAlive)
        {
            if (_followerCoroutine == null || !_followerCoroutine.Running)
            {
                StartFollowerCoroutine();
            }
        }
        
        return null;
    }

    private void PlanTasks()
    {
        if (_followTarget != null)
        {
            var distanceFromFollower = Vector3.Distance(GameController.Player.Pos, _followTarget.Pos);
            
            // Check for teleport detection
            if (Settings.SearchLastPosition.Value)
            {
                CheckForTeleportJump(distanceFromFollower);
            }
            
            // If we're searching for a teleport location, prioritize that
            if (_isSearchingForTeleport)
            {
                HandleTeleportSearch(distanceFromFollower);
            }
            // We are NOT within clear path distance range of leader. Logic can continue
            else if (distanceFromFollower >= Settings.ClearPathDistance.Value)
            {
                HandleDistantLeader();
            }
            else
            {
                HandleNearbyLeader(distanceFromFollower);
            }
            
            _lastTargetPosition = _followTarget.Pos;
            _lastDistanceToTarget = distanceFromFollower;

			// Handle link buff logic
			HandleLinkBuff();

			// Check for gems that need leveling
			CheckAndLevelGems(distanceFromFollower);
        }
        // Leader is null but we have tracked them this map.
        // Try using transition to follow them to their map
        else if (_tasks.Count == 0 && _lastTargetPosition != Vector3.Zero)
        {
            HandleMissingLeader();
        }
        // Check for gems even when leader is not present (when stopped)
        else if (_tasks.Count == 0)
        {
            CheckAndLevelGems(float.MaxValue);
        }
    }

    private void HandleDistantLeader()
    {
        // Clear stuck detection when planning new distant follow tasks
        // This prevents stuck detection from interfering with valid following
        if (_isStuckDetectionActive)
        {
            _isStuckDetectionActive = false;
            _stuckDetectionStartTime = DateTime.MinValue;
            _stuckRecoveryAttempts = 0;
        }
        
        // Check if we should avoid targeting the leader due to portal proximity
        if (IsInPortalAvoidanceGracePeriod() && IsPositionTooCloseToPortal(_followTarget.Pos))
        {
            // Leader is too close to portal during grace period, wait
            return;
        }
        
        // Leader moved VERY far in one frame. Check for transition to use to follow them.
        var distanceMoved = Vector3.Distance(_lastTargetPosition, _followTarget.Pos);
        if (_lastTargetPosition != Vector3.Zero && distanceMoved > Settings.ClearPathDistance.Value)
        {
            // Only attempt transition if leader zone info is reliable
            if (IsLeaderZoneInfoReliable())
            {
                var transition = _areaTransitions.Values.OrderBy(I => Vector3.Distance(_lastTargetPosition, I.Pos)).FirstOrDefault();
                if (transition != null && Vector3.Distance(_lastTargetPosition, transition.Pos) < Settings.ClearPathDistance.Value)
                    _tasks.Add(new TaskNode(transition.Pos, Settings.TransitionDistance, TaskNode.TaskNodeType.Transition));
            }
        }
        // We have no path, set us to go to leader pos.
        else if (_tasks.Count == 0)
            _tasks.Add(new TaskNode(_followTarget.Pos, Settings.PathfindingNodeDistance));
        // We have a path. Check if the last task is far enough away from current one to add a new task node.
        else
        {
            var distanceFromLastTask = Vector3.Distance(_tasks.Last().WorldPosition, _followTarget.Pos);
            if (distanceFromLastTask >= Settings.PathfindingNodeDistance)
                _tasks.Add(new TaskNode(_followTarget.Pos, Settings.PathfindingNodeDistance));
        }
    }

    private void HandleNearbyLeader(float distanceFromFollower)
    {
        // Clear all tasks except for looting/claim portal (as those only get done when we're within range of leader.)
        if (_tasks.Count > 0)
        {
            for (var i = _tasks.Count - 1; i >= 0; i--)
                if (_tasks[i].Type == TaskNode.TaskNodeType.Movement || _tasks[i].Type == TaskNode.TaskNodeType.Transition)
                    _tasks.RemoveAt(i);
        }
        else if (Settings.IsCloseFollowEnabled.Value)
        {
            // Close follow logic. We have no current tasks. Check if we should move towards leader
            if (distanceFromFollower >= Settings.PathfindingNodeDistance.Value)
            {
                // Check if we should avoid targeting the leader due to portal proximity
                if (IsInPortalAvoidanceGracePeriod() && IsPositionTooCloseToPortal(_followTarget.Pos))
                {
                    // Leader is too close to portal during grace period, wait
                    return;
                }
                
                _tasks.Add(new TaskNode(_followTarget.Pos, Settings.PathfindingNodeDistance));
            }
        }

        // Check if we should add quest loot logic. We're close to leader already
        var questLoot = GetLootableQuestItem();
        if (questLoot != null &&
            Vector3.Distance(GameController.Player.Pos, questLoot.Pos) < Settings.ClearPathDistance.Value &&
            _tasks.FirstOrDefault(I => I.Type == TaskNode.TaskNodeType.Loot) == null)
            _tasks.Add(new TaskNode(questLoot.Pos, Settings.ClearPathDistance, TaskNode.TaskNodeType.Loot));

        else if (!_hasUsedWP)
        {
            // Check if there's a waypoint nearby
            var waypoint = GameController.EntityListWrapper.Entities.SingleOrDefault(I => I.Type == ExileCore.Shared.Enums.EntityType.Waypoint &&
                Vector3.Distance(GameController.Player.Pos, I.Pos) < Settings.ClearPathDistance);

            if (waypoint != null)
            {
                _hasUsedWP = true;
                _tasks.Add(new TaskNode(waypoint.Pos, Settings.ClearPathDistance, TaskNode.TaskNodeType.ClaimWaypoint));
            }
        }
    }

    private void HandleMissingLeader()
    {
        // Only attempt transition if we have a valid last known position
        if (_lastTargetPosition != Vector3.Zero)
        {
            var transOptions = _areaTransitions.Values
                .Where(I => Vector3.Distance(_lastTargetPosition, I.Pos) < Settings.ClearPathDistance)
                .OrderBy(I => Vector3.Distance(_lastTargetPosition, I.Pos)).ToArray();
            if (transOptions.Length > 0)
                _tasks.Add(new TaskNode(transOptions[random.Next(transOptions.Length)].Pos, Settings.PathfindingNodeDistance.Value, TaskNode.TaskNodeType.Transition));
        }
    }

    private void CheckForTeleportJump(float currentDistance)
    {
        // Don't check on first tick or if we don't have previous data
        if (_lastDistanceToTarget == 0f || _lastTargetPosition == Vector3.Zero)
            return;

        // Check if the leader was within normal follow distance and suddenly jumped far away
        bool wasNearby = _lastDistanceToTarget <= Settings.NormalFollowDistance.Value;
        bool nowFarAway = currentDistance >= Settings.TeleportDetectionDistance.Value;
        
        if (wasNearby && nowFarAway && !_isSearchingForTeleport)
        {
            // Detected a teleport! Store the last known good position
            _lastKnownGoodPosition = _lastTargetPosition;
            _isSearchingForTeleport = true;
            _teleportSearchStartTime = DateTime.Now;
            
            // Clear current tasks to focus on teleport search
            _tasks.Clear();
            
            // Teleport detected! Searching last known position
        }
    }

    private void HandleTeleportSearch(float currentDistance)
    {
        // Check if we should stop searching (timeout or leader came back to normal range)
        if (DateTime.Now - _teleportSearchStartTime > _teleportSearchTimeout)
        {
            // Teleport search timed out - resuming normal follow
            _isSearchingForTeleport = false;
            return;
        }
        
        // If leader is back within normal range, stop searching
        if (currentDistance <= Settings.NormalFollowDistance.Value)
        {
            // Leader returned to normal range - stopping teleport search
            _isSearchingForTeleport = false;
            return;
        }
        
        // Navigate to the last known good position to find the door/teleport
        if (_lastKnownGoodPosition != Vector3.Zero)
        {
            var distanceToLastPosition = Vector3.Distance(GameController.Player.Pos, _lastKnownGoodPosition);
            
            // If we're not close to the last known position, go there
            if (distanceToLastPosition > Settings.PathfindingNodeDistance.Value)
            {
                // Clear movement tasks and add task to go to last known position
                _tasks.RemoveAll(t => t.Type == TaskNode.TaskNodeType.Movement);
                if (_tasks.FirstOrDefault(t => t.WorldPosition == _lastKnownGoodPosition) == null)
                {
                    _tasks.Add(new TaskNode(_lastKnownGoodPosition, Settings.PathfindingNodeDistance.Value, TaskNode.TaskNodeType.Movement));
                    LogMessage($"Moving to last known position: {_lastKnownGoodPosition.X:F0}, {_lastKnownGoodPosition.Y:F0} (distance: {distanceToLastPosition:F0})", 5);
                }
            }
                 }
     }

     private void CheckAndLevelGems(float distanceToTarget)
     {
         if (!Settings.AutoLevelGems.Value || !GameController.Player.IsAlive)
             return;

         var now = DateTime.Now;
         
         // Check if enough time has passed since last gem level check
         if (now - _lastGemLevelCheck < TimeSpan.FromMilliseconds(Settings.GemLevelCheckInterval.Value))
             return;

         _lastGemLevelCheck = now;

         // Determine if we should level gems based on conditions
         bool shouldLevelGems = false;
         
         if (Settings.LevelGemsWhenClose.Value && _followTarget != null && distanceToTarget <= Settings.NormalFollowDistance.Value)
         {
             // Close to leader, safe to level gems
             shouldLevelGems = true;
         }
         else if (Settings.LevelGemsWhenStopped.Value && _tasks.Count == 0)
         {
             // No tasks, we're stopped, safe to level gems
             shouldLevelGems = true;
         }

         if (shouldLevelGems && HasGemsToLevel())
         {
             TriggerGemLeveling();
         }
     }

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
                     
                     // Clicked gem level button
                 }
             }
         }
         catch (Exception ex)
         {
             // TriggerGemLeveling error occurred
         }
         finally
         {
             // Reset leveling state after a short delay
             Task.Delay(1000).ContinueWith(_ => _isLevelingGems = false);
         }
     }

     private Element GetFirstLevelableGem()
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
             // GetFirstLevelableGem error occurred
             return null;
         }
     }

     /// <summary>
     /// Coroutine version of ExecuteTasks - uses yielding instead of blocking
     /// </summary>
     private IEnumerator ExecuteTasksCoroutine()
     {
         // We have our tasks, now we need to perform in game logic with them.
         // Allow immediate execution if we're stuck to improve responsiveness
         bool canExecuteImmediately = _isStuckDetectionActive && _stuckRecoveryAttempts > 0;
         
         if ((DateTime.Now > _nextBotAction || canExecuteImmediately) && _tasks.Count > 0)
         {
            var currentTask = _tasks.First();
            var taskDistance = Vector3.Distance(GameController.Player.Pos, currentTask.WorldPosition);
            var playerDistanceMoved = Vector3.Distance(GameController.Player.Pos, _lastPlayerPosition);

            //We are using a same map transition and have moved significnatly since last tick. Mark the transition task as done.
            if (currentTask.Type == TaskNode.TaskNodeType.Transition &&
                playerDistanceMoved >= Settings.ClearPathDistance.Value)
            {
                _tasks.RemoveAt(0);
                if (_tasks.Count > 0)
                    currentTask = _tasks.First();
                else
                {
                    _lastPlayerPosition = GameController.Player.Pos;
                    yield break;
                }
            }

            // Check if the current position is unreachable before attempting movement
            if (IsPositionUnreachable(currentTask.WorldPosition))
            {
                // Position is unreachable, skip it
                _tasks.RemoveAt(0);
                yield break;
            }
            
            // Record the attempt to reach this position
            RecordPositionAttempt(currentTask.WorldPosition);
            
            switch (currentTask.Type)
            {
                case TaskNode.TaskNodeType.Movement:
                    _nextBotAction = DateTime.Now.AddMilliseconds(Settings.BotInputFrequency + random.Next(Settings.BotInputFrequency));

                    // Enhanced dashing with pathfinding (but don't block normal movement)
                    if (Settings.IsDashEnabled.Value)
                    {
                        // Try aggressive dash first if enabled
                        if (Settings.AggressiveDash.Value && TryAggressiveDash(currentTask.WorldPosition, taskDistance))
                            yield break;
                        
                        // Use enhanced terrain analysis for dash decision only
                        var pathStatus = GetPathStatus(GameController.Player.GridPos, currentTask.WorldPosition.WorldToGrid());
                        if (pathStatus == PathStatus.Dashable && ShouldDashToPosition(currentTask.WorldPosition.WorldToGrid()))
                        {
                            if (ExecuteDash(currentTask.WorldPosition))
                                yield break;
                        }
                        
                        // Note: We removed the PathStatus.Blocked check that was preventing normal movement
                        // The character should always attempt to move, even if there are obstacles,
                        // because the game's pathfinding can navigate around them
                    }

                    // Always attempt normal movement (this was the original working logic)
                    // Check if PickItV2 is active before performing mouse actions
                    if (!ExecuteMouseActionIfPossible(() =>
                    {
                        Mouse.SetCursorPosHuman2(WorldToValidScreenPosition(currentTask.WorldPosition));
                        
                        // Force a click if the setting is enabled to prevent getting stuck on UI elements
                        if (Settings.ForceClickDuringMovement.Value)
                        {
                            Mouse.LeftClick(1);
                        }
                        
                        Input.KeyDown(Settings.MovementKey);
                        Input.KeyUp(Settings.MovementKey);
                    }))
                    {
                        // PickItV2 is active, yielding control
                        yield break;
                    }

                    // Check if we've reached the target (within bounding range)
                    if (taskDistance <= Settings.PathfindingNodeDistance.Value * 1.5)
                    {
                        _tasks.RemoveAt(0);
                    }
                    else
                    {
                        currentTask.AttemptCount++;
                        if (currentTask.AttemptCount > Settings.MaxTaskAttempts)
                            _tasks.RemoveAt(0);
                    }
                    break;
                case TaskNode.TaskNodeType.Loot:
                    {
                        _nextBotAction = DateTime.Now.AddMilliseconds(Settings.BotInputFrequency + random.Next(Settings.BotInputFrequency));
                        currentTask.AttemptCount++;
                        var questLoot = GetLootableQuestItem();
                        if (questLoot == null
                            || currentTask.AttemptCount > 2
                            || Vector3.Distance(GameController.Player.Pos, questLoot.Pos) >= Settings.ClearPathDistance.Value)
                            _tasks.RemoveAt(0);

                        Input.KeyUp(Settings.MovementKey);
                        // Removed Thread.Sleep to prevent UI freezing
                        //Pause for long enough for movement to hopefully be finished.
                        var targetInfo = questLoot.GetComponent<Targetable>();
                        if (!targetInfo.isTargeted)
                        {
                            // Check if PickItV2 is active before mouse actions
                            if (!ExecuteMouseActionIfPossible(() => MouseoverItem(questLoot)))
                            {
                                // PickItV2 is active, yielding control
                                yield break;
                            }
                        }
                        if (targetInfo.isTargeted)
                        {
                            // Check if PickItV2 is active before clicking
                            if (!ExecuteMouseActionIfPossible(() =>
                            {
                                Mouse.LeftMouseDown();
                                Mouse.LeftMouseUp();
                            }))
                            {
                                // PickItV2 is active, yielding control
                                yield break;
                            }
                            _nextBotAction = DateTime.Now.AddSeconds(1);
                        }

                        break;
                    }
                case TaskNode.TaskNodeType.Transition:
                    {
                        _nextBotAction = DateTime.Now.AddMilliseconds(Settings.BotInputFrequency * 2 + random.Next(Settings.BotInputFrequency));
                        var screenPos = WorldToValidScreenPosition(currentTask.WorldPosition);
                        if (taskDistance <= Settings.ClearPathDistance.Value)
                        {
                            //Click the transition
                            Input.KeyUp(Settings.MovementKey);
                            // Check if PickItV2 is active before clicking transition
                            if (!ExecuteMouseActionIfPossible(() => Mouse.SetCursorPosAndLeftClickHuman(screenPos, 100)))
                            {
                                // PickItV2 is active, yielding control
                                yield break;
                            }
                            _nextBotAction = DateTime.Now.AddSeconds(1);
                        }
                        else
                        {
                            //Walk towards the transition
                            if (Settings.IsDashEnabled.Value)
                            {
                                // Try aggressive dash first if enabled
                                if (Settings.AggressiveDash.Value && TryAggressiveDash(currentTask.WorldPosition, taskDistance))
                                {
                                    yield break;
                                }
                                // Use enhanced terrain analysis for dash decision only
                                var pathStatus = GetPathStatus(GameController.Player.GridPos, currentTask.WorldPosition.WorldToGrid());
                                if (pathStatus == PathStatus.Dashable && ShouldDashToPosition(currentTask.WorldPosition.WorldToGrid()))
                                {
                                    if (ExecuteDash(currentTask.WorldPosition))
                                        yield break;
                                }
                                
                                // Note: Removed PathStatus.Blocked check - always attempt normal movement
                                // Let the game's pathfinding handle obstacles
                            }
                            
                            // Normal movement if no dash executed
                            // Check if PickItV2 is active before mouse movement
                            if (!ExecuteMouseActionIfPossible(() =>
                            {
                                Mouse.SetCursorPosHuman2(screenPos);
                                
                                // Force a click if the setting is enabled to prevent getting stuck on UI elements
                                if (Settings.ForceClickDuringMovement.Value)
                                {
                                    Mouse.LeftClick(1);
                                }
                                
                                Input.KeyDown(Settings.MovementKey);
                                Input.KeyUp(Settings.MovementKey);
                            }))
                            {
                                // PickItV2 is active, yielding control
                                yield break;
                            }
                        }
                        currentTask.AttemptCount++;
                        if (currentTask.AttemptCount > Settings.MaxTaskAttempts)
                            _tasks.RemoveAt(0);
                        break;
                    }

                case TaskNode.TaskNodeType.ClaimWaypoint:
                    {
                        if (Vector3.Distance(GameController.Player.Pos, currentTask.WorldPosition) > Settings.WaypointDistance)
                        {
                            var screenPos = WorldToValidScreenPosition(currentTask.WorldPosition);
                            Input.KeyUp(Settings.MovementKey);
                            // Check if PickItV2 is active before clicking waypoint
                            if (!ExecuteMouseActionIfPossible(() => Mouse.SetCursorPosAndLeftClickHuman(screenPos, 100)))
                            {
                                // PickItV2 is active, yielding control
                                yield break;
                            }
                            _nextBotAction = DateTime.Now.AddSeconds(1);
                        }
                        currentTask.AttemptCount++;
                        if (currentTask.AttemptCount > Settings.MaxTaskAttempts)
                            _tasks.RemoveAt(0);
                        break;
                    }
            }
        }
    }
    
    /// <summary>
    /// Enhanced terrain analysis that returns PathStatus instead of just boolean
    /// </summary>
    /// <param name="start">Starting position in grid coordinates</param>
    /// <param name="end">Ending position in grid coordinates</param>
    /// <returns>PathStatus indicating path quality</returns>
    private PathStatus GetPathStatus(Vector2 start, Vector2 end)
    {
        // Validate terrain data exists
        if (_tiles == null || _numCols <= 0 || _numRows <= 0)
            return PathStatus.Invalid;
            
        // Check bounds
        if (start.X < 0 || start.X >= _numCols || start.Y < 0 || start.Y >= _numRows ||
            end.X < 0 || end.X >= _numCols || end.Y < 0 || end.Y >= _numRows)
            return PathStatus.Invalid;
            
        // Use Bresenham's line algorithm to trace the path
        var dx = Math.Abs(end.X - start.X);
        var dy = Math.Abs(end.Y - start.Y);
        var x = (int)start.X;
        var y = (int)start.Y;
        var n = 1 + dx + dy;
        var x_inc = (end.X > start.X) ? 1 : -1;
        var y_inc = (end.Y > start.Y) ? 1 : -1;
        var error = dx - dy;
        
        dx *= 2;
        dy *= 2;
        
        var hasDashableObstacle = false;
        
        for (; n > 0; --n)
        {
            // Check current position
            if (x >= 0 && x < _numCols && y >= 0 && y < _numRows)
            {
                var terrainValue = _tiles[x, y];
                
                switch (terrainValue)
                {
                    case 255: // Impassable wall
                        return PathStatus.Blocked;
                    case 2: // Dashable terrain
                        hasDashableObstacle = true;
                        break;
                    case 1: // Walkable terrain
                        break;
                    default: // Unknown terrain, assume blocked
                        return PathStatus.Blocked;
                }
            }
            
            // Move to next position
            if (error > 0)
            {
                x += x_inc;
                error -= dy;
            }
            else
            {
                y += y_inc;
                error += dx;
            }
        }
        
        // Return appropriate status
        return hasDashableObstacle ? PathStatus.Dashable : PathStatus.Clear;
    }
    
    /// <summary>
    /// Checks if the leader's zone information is reliable for area transitions
    /// </summary>
    /// <returns>True if zone information is reliable</returns>
    private bool IsLeaderZoneInfoReliable()
    {
        if (_followTarget == null)
            return false;
            
        try
        {
            // Check if leader has valid position data
            if (_followTarget.Pos == Vector3.Zero || _followTarget.Pos == null)
                return false;
                
            // Check if leader is in a valid game state
            var playerComponent = _followTarget.GetComponent<Player>();
            if (playerComponent == null)
                return false;
                
            // Check if we're in the same area
            var currentArea = GameController.Area.CurrentArea;
            if (currentArea == null)
                return false;
                
            // Verify leader is in a reasonable position (not at origin)
            var leaderPos = _followTarget.Pos;
            if (Math.Abs(leaderPos.X) < 1.0f && Math.Abs(leaderPos.Y) < 1.0f)
                return false;
                
            // Check if leader moved recently (indicates they're active)
            var distanceFromLastPosition = Vector3.Distance(_followTarget.Pos, _lastTargetPosition);
            if (_lastTargetPosition != Vector3.Zero && distanceFromLastPosition > 0.1f)
                return true;
                
            // If leader hasn't moved much, check if they're still in a valid state
            return _followTarget.IsValid && _followTarget.IsTargetable;
        }
        catch (Exception ex)
        {
            // If we can't verify leader zone info, assume it's unreliable
            return false;
        }
    }
    
    /// <summary>
    /// Enhanced dash terrain check using PathStatus
    /// </summary>
    /// <param name="targetPosition">Target position in grid coordinates</param>
    /// <returns>True if dash should be executed</returns>
    private bool ShouldDashToPosition(Vector2 targetPosition)
    {
        // Check if dash is available (cooldown)
        if (!IsDashAvailable())
            return false;
            
        var pathStatus = GetPathStatus(GameController.Player.GridPos, targetPosition);
        
        // Only dash if the path is dashable (has obstacles we can dash through)
        return pathStatus == PathStatus.Dashable;
    }
    
    /// <summary>
    /// Checks if the player is stuck and attempts recovery
    /// </summary>
    private void CheckStuckDetection()
    {
        if (!Settings.EnableStuckDetection.Value || !Settings.IsFollowEnabled.Value || _tasks.Count == 0)
        {
            // Reset stuck detection when not needed
            _isStuckDetectionActive = false;
            _stuckDetectionStartTime = DateTime.MinValue;
            _stuckRecoveryAttempts = 0;
            return;
        }
        
        var currentPos = GameController.Player.Pos;
        var now = DateTime.Now;
        
        // Check if leader is nearby - if so, reset stuck detection more aggressively
        if (_followTarget != null)
        {
            var leaderDistance = Vector3.Distance(currentPos, _followTarget.Pos);
            if (leaderDistance <= Settings.NormalFollowDistance.Value && _isStuckDetectionActive)
            {
                // Leader is close, reset stuck detection to give normal following a chance
                _isStuckDetectionActive = false;
                _stuckDetectionStartTime = DateTime.MinValue;
                _stuckRecoveryAttempts = 0;
                _lastStuckCheckPosition = currentPos;
                _lastStuckCheckTime = now;
                return;
            }
        }
        
        // Initialize stuck detection on first check
        if (_lastStuckCheckTime == DateTime.MinValue)
        {
            _lastStuckCheckPosition = currentPos;
            _lastStuckCheckTime = now;
            return;
        }
        
        // Check if enough time has passed for stuck detection
        // Use more aggressive timing when aggressive stuck detection is enabled
        var checkInterval = Settings.AggressiveStuckDetection.Value ? 500 : 1000;
        if (now - _lastStuckCheckTime < TimeSpan.FromMilliseconds(checkInterval))
            return;
            
        // Calculate movement since last check
        var movementDistance = Vector3.Distance(currentPos, _lastStuckCheckPosition);
        
        // Check if we've moved enough to not be considered stuck
        // Use more aggressive threshold when aggressive stuck detection is enabled
        var movementThreshold = Settings.AggressiveStuckDetection.Value ? 
            Settings.StuckMovementThreshold.Value * 0.7f : 
            Settings.StuckMovementThreshold.Value;
            
        if (movementDistance >= movementThreshold)
        {
            // We're moving, reset stuck detection
            _isStuckDetectionActive = false;
            _stuckDetectionStartTime = DateTime.MinValue;
            _stuckRecoveryAttempts = 0;
            
            _lastStuckCheckPosition = currentPos;
            _lastStuckCheckTime = now;
            return;
        }
        
        // We haven't moved enough, check if we should start/continue stuck detection
        if (!_isStuckDetectionActive)
        {
            // Start stuck detection
            _isStuckDetectionActive = true;
            _stuckDetectionStartTime = now;
        }
        else
        {
            // Check if we've been stuck long enough to trigger recovery
            // Use more aggressive timing when aggressive stuck detection is enabled
            var stuckDetectionTime = Settings.AggressiveStuckDetection.Value ? 
                Settings.StuckDetectionTime.Value * 0.6f : 
                Settings.StuckDetectionTime.Value;
                
            var stuckDuration = now - _stuckDetectionStartTime;
            if (stuckDuration.TotalMilliseconds >= stuckDetectionTime)
            {
                // We're stuck! Attempt recovery
                AttemptStuckRecovery();
            }
        }
        
        _lastStuckCheckPosition = currentPos;
        _lastStuckCheckTime = now;
    }
    
    /// <summary>
    /// Attempts to recover from being stuck
    /// </summary>
    private void AttemptStuckRecovery()
    {
        if (_stuckRecoveryAttempts >= Settings.MaxStuckRecoveryAttempts.Value)
        {
            // Max recovery attempts reached, skip current task
            if (_tasks.Count > 0)
            {
                _tasks.RemoveAt(0);
                _stuckRecoveryAttempts = 0;
                _isStuckDetectionActive = false;
                _stuckDetectionStartTime = DateTime.MinValue;
                // Skipped stuck task after max recovery attempts
            }
            return;
        }
        
        _stuckRecoveryAttempts++;
        var currentPos = GameController.Player.Pos;
        
        // Recovery Strategy 1: Try aggressive dash if available
        if (Settings.IsDashEnabled.Value && _tasks.Count > 0)
        {
            var targetPos = _tasks.First().WorldPosition;
            var distance = Vector3.Distance(currentPos, targetPos);
            
            if (distance > Settings.DashDistanceThreshold.Value && IsDashAvailable())
            {
                // Attempting stuck recovery with dash
                ExecuteDash(targetPos);
                
                // Reset stuck detection timer to give dash time to work
                _stuckDetectionStartTime = DateTime.Now;
                return;
            }
        }
        
        // Recovery Strategy 2: Generate random nearby movement
        var randomOffset = new Vector3(
            (float)(random.NextDouble() - 0.5) * 200,
            (float)(random.NextDouble() - 0.5) * 200,
            currentPos.Z
        );
        
        var recoveryPos = currentPos + randomOffset;
        
        // Insert recovery movement at the beginning of task queue
        _tasks.Insert(0, new TaskNode(recoveryPos, Settings.PathfindingNodeDistance.Value, TaskNode.TaskNodeType.Movement));
        
        // IMMEDIATELY move mouse to recovery position and force execution
        // This ensures the mouse adjusts right away when stuck
        if (ExecuteMouseActionIfPossible(() =>
        {
            Mouse.SetCursorPosHuman2(WorldToValidScreenPosition(recoveryPos));
            Input.KeyDown(Settings.MovementKey);
            Input.KeyUp(Settings.MovementKey);
        }))
        {
            // Force immediate execution by resetting the action timer
            _nextBotAction = DateTime.Now;
        }
        
        // Reset stuck detection timer to give recovery time to work
        _stuckDetectionStartTime = DateTime.Now;
        
        // Attempting stuck recovery with random movement
    }
    
    /// <summary>
    /// Checks if a position is likely unreachable based on repeated failed attempts
    /// </summary>
    /// <param name="targetPosition">The position to check</param>
    /// <returns>True if the position should be considered unreachable</returns>
    private bool IsPositionUnreachable(Vector3 targetPosition)
    {
        if (!Settings.UnreachablePositionDetection.Value)
            return false;
            
        // Find similar positions within threshold
        var similarPositions = _positionAttempts.Keys.Where(pos => 
            Vector3.Distance(pos, targetPosition) <= Settings.PositionSimilarityThreshold.Value);
        
        var totalAttempts = similarPositions.Sum(pos => _positionAttempts[pos]);
        
        return totalAttempts >= Settings.MaxSamePositionAttempts.Value;
    }
    
    /// <summary>
    /// Records an attempt to reach a position
    /// </summary>
    /// <param name="position">The position being attempted</param>
    private void RecordPositionAttempt(Vector3 position)
    {
        if (!Settings.UnreachablePositionDetection.Value)
            return;
            
        // Clean up old position attempts (keep last 50 to prevent memory bloat)
        if (_positionAttempts.Count > 50)
        {
            var oldestKey = _positionAttempts.Keys.First();
            _positionAttempts.Remove(oldestKey);
        }
        
        // Find similar position within threshold
        var similarPosition = _positionAttempts.Keys.FirstOrDefault(pos => 
            Vector3.Distance(pos, position) <= Settings.PositionSimilarityThreshold.Value);
        
        if (similarPosition != Vector3.Zero)
        {
            _positionAttempts[similarPosition]++;
        }
        else
        {
            _positionAttempts[position] = 1;
        }
    }
    
    /// <summary>
    /// Checks if we're in the portal avoidance grace period after entering a new area
    /// </summary>
    /// <returns>True if we should avoid targeting positions near portals</returns>
    private bool IsInPortalAvoidanceGracePeriod()
    {
        if (!Settings.EnablePortalAvoidance.Value)
            return false;
            
        if (_areaChangeTime == DateTime.MinValue)
            return false;
            
        var timeSinceAreaChange = DateTime.Now - _areaChangeTime;
        return timeSinceAreaChange.TotalMilliseconds <= Settings.PortalAvoidanceGracePeriod.Value;
    }
    
    /// <summary>
    /// Checks if a position is too close to a portal (should be avoided)
    /// </summary>
    /// <param name="targetPosition">The position to check</param>
    /// <returns>True if the position should be avoided due to portal proximity</returns>
    private bool IsPositionTooCloseToPortal(Vector3 targetPosition)
    {
        if (!Settings.EnablePortalAvoidance.Value)
            return false;
            
        foreach (var transition in _areaTransitions.Values)
        {
            if (transition.Type == ExileCore.Shared.Enums.EntityType.Portal || 
                transition.Type == ExileCore.Shared.Enums.EntityType.TownPortal)
            {
                var distanceToPortal = Vector3.Distance(targetPosition, transition.Pos);
                if (distanceToPortal <= Settings.PortalAvoidanceDistance.Value)
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Handles death detection and resurrection logic
    /// </summary>
    private void CheckDeathHandling()
    {
        if (!Settings.EnableDeathHandling.Value)
            return;
            
        var now = DateTime.Now;
        
        // Check if enough time has passed since last death check
        if (now - _lastDeathCheckTime < TimeSpan.FromMilliseconds(Settings.DeathCheckInterval.Value))
            return;
            
        _lastDeathCheckTime = now;
        
        var isCurrentlyDead = !GameController.Player.IsAlive;
        
        // Death detected
        if (isCurrentlyDead && !_wasDead)
        {
            _wasDead = true;
            _deathDetectedTime = now;
            _isWaitingForResurrection = true;
            
            // Clear all current tasks when dead
            _tasks.Clear();
            
            // Player died - waiting for resurrection
        }
        // Resurrection detected
        else if (!isCurrentlyDead && _wasDead)
        {
            _wasDead = false;
            _isWaitingForResurrection = false;
            
            if (Settings.AutoResumeAfterDeath.Value)
            {
                // Reset tracking state to re-acquire leader
                ResetFollowerTracking();
                
                // Player resurrected - auto-resuming following
            }
        }
        // Check for resurrection timeout
        else if (_isWaitingForResurrection && isCurrentlyDead)
        {
            var waitTime = now - _deathDetectedTime;
            if (waitTime.TotalMilliseconds > Settings.ResurrectionTimeout.Value)
            {
                // Resurrection timeout reached - stopping death handling
                _isWaitingForResurrection = false;
                _wasDead = false;
            }
        }
    }
    
    /// <summary>
    /// Parses leader names and finds the best available leader
    /// </summary>
    private Entity GetBestAvailableLeader()
    {
        // Use single leader mode if multiple leaders is disabled
        if (!Settings.EnableMultipleLeaders.Value)
        {
            return GetFollowingTarget(); // Original single leader method
        }
        
        // Parse leader names from comma-separated string
        var leaderNamesString = Settings.LeaderNames.Value?.Trim();
        if (string.IsNullOrEmpty(leaderNamesString))
        {
            // Fall back to single leader if no multiple leaders specified
            return GetFollowingTarget();
        }
        
        // Update leader names list if it changed
        var newLeaderNames = leaderNamesString.Split(',')
            .Select(name => name.Trim().ToLower())
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
            
        if (!_leaderNamesList.SequenceEqual(newLeaderNames))
        {
            _leaderNamesList = newLeaderNames;
        }
        
        if (_leaderNamesList.Count == 0)
        {
            return GetFollowingTarget(); // Fall back to single leader
        }
        
        try
        {
            // Get all potential leaders
            var potentialLeaders = GameController.Entities
                .Where(x => x.Type == ExileCore.Shared.Enums.EntityType.Player)
                .Where(x => x.GetComponent<Player>()?.PlayerName != null)
                .Where(x => _leaderNamesList.Contains(x.GetComponent<Player>().PlayerName.ToLower()))
                .ToList();
                
            if (potentialLeaders.Count == 0)
                return null;
                
            // If we have a current leader and they're still available, prefer them (unless switching conditions are met)
            if (_followTarget != null && potentialLeaders.Any(p => p.Id == _followTarget.Id))
            {
                var currentLeaderDistance = Vector3.Distance(GameController.Player.Pos, _followTarget.Pos);
                
                // Only switch if current leader is too far and we haven't switched recently
                if (currentLeaderDistance > Settings.LeaderSwitchDistance.Value &&
                    DateTime.Now - _lastLeaderSwitchTime > TimeSpan.FromMilliseconds(5000))
                {
                    // Continue to find better leader
                }
                else
                {
                    return _followTarget; // Keep current leader
                }
            }
            
            // Find the best leader based on settings
            Entity bestLeader = null;
            
            if (Settings.PrioritizeClosestLeader.Value)
            {
                // Find closest leader
                bestLeader = potentialLeaders
                    .OrderBy(leader => Vector3.Distance(GameController.Player.Pos, leader.Pos))
                    .FirstOrDefault();
            }
            else
            {
                // Use priority order (first in list has highest priority)
                foreach (var leaderName in _leaderNamesList)
                {
                    bestLeader = potentialLeaders
                        .FirstOrDefault(x => x.GetComponent<Player>().PlayerName.ToLower() == leaderName);
                    if (bestLeader != null)
                        break;
                }
            }
            
            // Check if we're switching leaders
            if (bestLeader != null && (_followTarget == null || bestLeader.Id != _followTarget.Id))
            {
                _lastLeaderSwitchTime = DateTime.Now;
                var leaderName = bestLeader.GetComponent<Player>().PlayerName;
                // Switched to leader
            }
            
            return bestLeader;
        }
        catch (Exception ex)
        {
            // Error in GetBestAvailableLeader
            return GetFollowingTarget(); // Fall back to single leader
        }
    }
    
    /// <summary>
    /// Checks game state for safety features
    /// </summary>
    private void CheckSafetyFeatures()
    {
        if (!Settings.EnableSafetyFeatures.Value)
            return;
            
        var now = DateTime.Now;
        
        // Check if enough time has passed since last safety check
        if (now - _lastSafetyCheckTime < TimeSpan.FromMilliseconds(1000))
            return;
            
        _lastSafetyCheckTime = now;
        
        try
        {
            // Check if we're in the game
            var isInGame = GameController.Game.IngameState?.ServerData?.IsInGame == true;
            
            // Detect game state changes
            if (_wasInGame && !isInGame)
            {
                // Left the game (logout, character select, etc.)
                if ((Settings.PauseOnLogout.Value || Settings.PauseOnCharacterSelect.Value) && !_isPausedForSafety)
                {
                    _isPausedForSafety = true;
                    _tasks.Clear(); // Clear current tasks
                    
                    // Paused following due to leaving game
                }
            }
            else if (!_wasInGame && isInGame)
            {
                // Returned to the game
                if (Settings.AutoResumeOnGameReturn.Value && _isPausedForSafety)
                {
                    _isPausedForSafety = false;
                    ResetFollowerTracking(); // Reset tracking to re-acquire leader
                    
                    // Resumed following after returning to game
                }
            }
            
            _wasInGame = isInGame;
        }
        catch (Exception ex)
        {
            // Error in CheckSafetyFeatures
        }
    }
    
    /// <summary>
    /// Checks inventory and handles auto-portal when full
    /// </summary>
    private void CheckInventoryManagement()
    {
        if (!Settings.EnableInventoryManagement.Value)
            return;
            
        var now = DateTime.Now;
        
        // Check if enough time has passed since last inventory check
        if (now - _lastInventoryCheckTime < TimeSpan.FromMilliseconds(2000))
            return;
            
        _lastInventoryCheckTime = now;
        
        // Don't manage inventory if we're already managing it or recently used portal
        if (_isManagingInventory || (now - _portalUsedTime).TotalMilliseconds < Settings.PortalWaitTime.Value)
            return;
            
        try
        {
            var inventory = GameController.Game.IngameState?.ServerData?.PlayerInventories?.FirstOrDefault()?.Inventory;
            if (inventory == null)
                return;
                
            // Count occupied inventory slots
            var occupiedSlots = 0;
            var totalSlots = inventory.Rows * inventory.Columns;
            
            foreach (var item in inventory.InventorySlotItems)
            {
                if (item != null)
                    occupiedSlots++;
            }
            
            // Check if inventory is full enough to trigger portal
            var occupancyPercentage = (occupiedSlots * 100) / totalSlots;
            
            if (occupancyPercentage >= Settings.InventoryFullThreshold.Value && Settings.AutoPortalOnFullInventory.Value)
            {
                _isManagingInventory = true;
                
                // Try to use a portal scroll
                var portalScrollUsed = UsePortalScroll();
                if (portalScrollUsed)
                {
                    _portalUsedTime = now;
                    _tasks.Clear(); // Clear current tasks
                    
                    // Used portal scroll due to full inventory
                }
                
                _isManagingInventory = false;
            }
        }
        catch (Exception ex)
        {
            // Error in CheckInventoryManagement
            _isManagingInventory = false;
        }
    }
    
    /// <summary>
    /// Attempts to use a portal scroll
    /// </summary>
    private bool UsePortalScroll()
    {
        try
        {
            var inventory = GameController.Game.IngameState?.ServerData?.PlayerInventories?.FirstOrDefault()?.Inventory;
            if (inventory == null)
                return false;
                
            // Look for portal scroll in inventory
            foreach (var item in inventory.InventorySlotItems)
            {
                if (item?.Item?.RenderName?.Contains("Portal") == true)
                {
                    // Found portal scroll, try to use it
                    var itemPos = item.GetClientRect().Center;
                    Mouse.SetCursorPos(itemPos);
                    System.Threading.Thread.Sleep(100);
                    Mouse.RightClick(50);
                    
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Refreshes terrain data to detect dynamic changes like doors opening/closing
    /// </summary>
    private void RefreshTerrainData()
    {
        try
        {
            // Check if it's time to refresh terrain
            if (DateTime.Now - _lastTerrainRefresh < TimeSpan.FromMilliseconds(Settings.TerrainRefreshRate.Value))
                return;
                
            // Update terrain data from the game using the correct ExileCore API
            var terrain = GameController.IngameState.Data.Terrain;
                
            // Process terrain data similar to AreaChange method
            var terrainBytes = GameController.Memory.ReadBytes(terrain.LayerMelee.First, terrain.LayerMelee.Size);
            _numCols = (int)(terrain.NumCols - 1) * 23;
            _numRows = (int)(terrain.NumRows - 1) * 23;
            if ((_numCols & 1) > 0)
                _numCols++;

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

            // Process ranged layer for dashable terrain
            terrainBytes = GameController.Memory.ReadBytes(terrain.LayerRanged.First, terrain.LayerRanged.Size);
            dataIndex = 0;
            for (int y = 0; y < _numRows; y++)
            {
                for (int x = 0; x < _numCols; x += 2)
                {
                    var b = terrainBytes[dataIndex + (x >> 1)];
                    var current = _tiles[x, y];
                    if (current == 255)
                        _tiles[x, y] = (byte)((b & 0xf) > 3 ? 2 : 255);
                    current = _tiles[x + 1, y];
                    if (current == 255)
                        _tiles[x + 1, y] = (byte)((b >> 4) > 3 ? 2 : 255);
                }
                dataIndex += terrain.BytesPerRow;
            }
            
            _lastTerrainRefresh = DateTime.Now;
        }
        catch (Exception ex)
        {
            // If terrain refresh fails, wait longer before retrying
            _lastTerrainRefresh = DateTime.Now.AddSeconds(5);
        }
    }
    
    /// <summary>
    /// Debug method to visualize terrain values around the player
    /// </summary>
    private void RenderTerrainVisualization()
    {
        if (!Settings.ShowTerrainVisualization.Value || _tiles == null)
            return;
            
        var playerGridPos = GameController.Player.GridPos;
        var radius = 20; // Show terrain in a 40x40 grid around player
        
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                var gridX = (int)playerGridPos.X + x;
                var gridY = (int)playerGridPos.Y + y;
                
                // Check bounds
                if (gridX < 0 || gridX >= _numCols || gridY < 0 || gridY >= _numRows)
                    continue;
                    
                var terrainValue = _tiles[gridX, gridY];
                var worldPos = new Vector3(gridX, gridY, 0);
                var screenPos = WorldToValidScreenPosition(worldPos);
                
                // Skip if off screen
                if (screenPos.X < 0 || screenPos.Y < 0 || screenPos.X > 1920 || screenPos.Y > 1080)
                    continue;
                
                // Color based on terrain value
                var color = terrainValue switch
                {
                    1 => SharpDX.Color.Green,      // Walkable
                    2 => SharpDX.Color.Yellow,     // Dashable
                    255 => SharpDX.Color.Red,      // Blocked
                    _ => SharpDX.Color.Purple      // Unknown
                };
                
                // Draw a small rectangle for each terrain cell
                var rect = new SharpDX.RectangleF(screenPos.X - 1, screenPos.Y - 1, 2, 2);
                Graphics.DrawBox(rect, color);
                
                // Optionally show the terrain value as text for debugging
                if (Settings.ShowRaycastDebug.Value)
                {
                    Graphics.DrawText(terrainValue.ToString(), screenPos, SharpDX.Color.White, 8);
                }
            }
        }
    }
    
    /// <summary>
    /// Debug method to show detailed task information
    /// </summary>
    private void RenderTaskDebug()
    {
        if (!Settings.ShowTaskDebug.Value)
            return;
            
        var yOffset = 340;
        
        Graphics.DrawText($"Tasks: {_tasks.Count}", new Vector2(10, yOffset), SharpDX.Color.White);
        yOffset += 20;
        
        for (int i = 0; i < Math.Min(_tasks.Count, 10); i++)
        {
            var task = _tasks[i];
            var taskText = $"  {i}: {task.Type} at ({task.WorldPosition.X:F0}, {task.WorldPosition.Y:F0}) - Attempts: {task.AttemptCount}";
            var taskColor = i == 0 ? SharpDX.Color.Yellow : SharpDX.Color.Gray;
            
            Graphics.DrawText(taskText, new Vector2(10, yOffset), taskColor);
            yOffset += 15;
        }
    }
    
    /// <summary>
    /// Debug method to show entity information
    /// </summary>
    private void RenderEntityDebug()
    {
        if (!Settings.ShowEntityDebug.Value)
            return;
            
        var yOffset = 400;
        
        // Show leader information
        if (_followTarget != null)
        {
            var leaderText = $"Leader: {Settings.LeaderName.Value} at ({_followTarget.Pos.X:F0}, {_followTarget.Pos.Y:F0})";
            Graphics.DrawText(leaderText, new Vector2(10, yOffset), SharpDX.Color.Green);
            yOffset += 20;
            
            var distanceText = $"Distance: {Vector3.Distance(GameController.Player.Pos, _followTarget.Pos):F1}";
            Graphics.DrawText(distanceText, new Vector2(10, yOffset), SharpDX.Color.White);
            yOffset += 20;
        }
        else
        {
            Graphics.DrawText("Leader: NOT FOUND", new Vector2(10, yOffset), SharpDX.Color.Red);
            yOffset += 20;
        }
        
        // Show area transition information
        Graphics.DrawText($"Area Transitions: {_areaTransitions.Count}", new Vector2(10, yOffset), SharpDX.Color.White);
        yOffset += 20;
        
        var transitionCount = 0;
        foreach (var transition in _areaTransitions.Values.Take(5))
        {
            var transText = $"  {transitionCount}: {transition.RenderName} at ({transition.Pos.X:F0}, {transition.Pos.Y:F0})";
            Graphics.DrawText(transText, new Vector2(10, yOffset), SharpDX.Color.Cyan);
            yOffset += 15;
            transitionCount++;
        }
    }

    private bool CheckDashTerrain(Vector2 targetPosition)
    {
        // Check if dash is available (cooldown)
        if (!IsDashAvailable())
            return false;
            
        // Validate terrain data exists
        if (_tiles == null || _numCols <= 0 || _numRows <= 0)
            return false;

        //Calculate the straight path from us to the target (this would be waypoints normally)
        var distance = Vector2.Distance(GameController.Player.GridPos, targetPosition);
        var dir = targetPosition - GameController.Player.GridPos;
        dir.Normalize();

        var distanceBeforeWall = 0;
        var distanceInWall = 0;

        var shouldDash = false;
        var points = new HashSet<System.Drawing.Point>(); // Use HashSet for O(1) lookups instead of O(n)
        
        for (var i = 0; i < Settings.MaxPathfindingIterations; i++)
        {
            var v2Point = GameController.Player.GridPos + i * dir;
            var point = new System.Drawing.Point((int)(GameController.Player.GridPos.X + i * dir.X),
                (int)(GameController.Player.GridPos.Y + i * dir.Y));

            // Bounds checking to prevent array index out of bounds
            if (point.X < 0 || point.X >= _numCols || point.Y < 0 || point.Y >= _numRows)
                break;

            if (points.Contains(point))
                continue;
            if (Vector2.Distance(v2Point, targetPosition) < 2)
                break;

            points.Add(point);
            var tile = _tiles[point.X, point.Y];


            //Invalid tile: Block dash
            if (tile == 255)
            {
                shouldDash = false;
                break;
            }
            else if (tile == 2)
            {
                if (shouldDash)
                    distanceInWall++;
                shouldDash = true;
            }
            else if (!shouldDash)
            {
                distanceBeforeWall++;
                if (distanceBeforeWall > 10)
                    break;
            }
        }

        if (distanceBeforeWall > 10 || distanceInWall < 5)
            shouldDash = false;

        if (shouldDash)
        {
            return ExecuteDash(targetPosition.GridToWorld(_followTarget == null ? GameController.Player.Pos.Z : _followTarget.Pos.Z));
        }

        return false;
    }

    /// <summary>
    /// Checks if dash is available (off cooldown)
    /// </summary>
    private bool IsDashAvailable()
    {
        var timeSinceLastDash = DateTime.Now - _lastDashTime;
        return timeSinceLastDash.TotalMilliseconds >= Settings.DashCooldown.Value;
    }

    /// <summary>
    /// Attempts to dash towards target for movement acceleration
    /// </summary>
    private bool TryAggressiveDash(Vector3 targetWorldPos, float distanceToTarget)
    {
        if (!Settings.IsDashEnabled.Value)
            return false;

        if (!IsDashAvailable())
            return false;

        // Only dash if target is far enough away
        if (distanceToTarget < Settings.DashDistanceThreshold.Value)
            return false;

        // For aggressive dash, skip terrain checking and just dash
        if (Settings.AggressiveDash.Value)
        {
            return ExecuteDash(targetWorldPos);
        }
        else
        {
            // Use the original terrain-based dash logic for conservative dashing
            return CheckDashTerrain(targetWorldPos.WorldToGrid());
        }
    }

    /// <summary>
    /// Executes a dash towards the target position
    /// </summary>
    private bool ExecuteDash(Vector3 targetWorldPos)
    {
        try
        {
            _lastDashTime = DateTime.Now;
            _nextBotAction = DateTime.Now.AddMilliseconds(Settings.DashCooldown + random.Next(Settings.BotInputFrequency));
            
            Mouse.SetCursorPos(WorldToValidScreenPosition(targetWorldPos));
            Input.KeyDown(Settings.DashKey);
            Input.KeyUp(Settings.DashKey);
            
            // Executed aggressive dash towards target
            return true;
        }
        catch (Exception ex)
        {
            // ExecuteDash error occurred
            return false;
        }
    }

    private Entity GetFollowingTarget()
    {
        var leaderName = Settings.LeaderName.Value?.ToLower();
        if (string.IsNullOrEmpty(leaderName))
            return null;
            
        try
        {
            return GameController.Entities
                .Where(x => x.Type == ExileCore.Shared.Enums.EntityType.Player)
                .FirstOrDefault(x => x.GetComponent<Player>()?.PlayerName?.ToLower() == leaderName);
        }
        catch (InvalidOperationException ex)
        {
            // Collection was modified during enumeration
            return null;
        }
        catch (Exception ex)
        {
            // GetFollowingTarget: Unexpected error occurred
            return null;
        }
    }

    private Entity GetLootableQuestItem()
    {
        try
        {
            return GameController.EntityListWrapper.Entities
                .Where(e => e.Type == ExileCore.Shared.Enums.EntityType.WorldItem)
                .Where(e => e.IsTargetable)
                .Where(e => e.GetComponent<WorldItem>() != null)
                .FirstOrDefault(e =>
                {
                    var worldItem = e.GetComponent<WorldItem>();
                    if (worldItem?.ItemEntity == null) return false;
                    
                    var baseItemType = GameController.Files.BaseItemTypes.Translate(worldItem.ItemEntity.Path);
                    return baseItemType?.ClassName == "QuestItem";
                });
        }
        catch (InvalidOperationException ex)
        {
            // Collection was modified during enumeration
            return null;
        }
        catch (Exception ex)
        {
            // GetLootableQuestItem: Unexpected error occurred
            return null;
        }
    }
    public override void EntityAdded(Entity entity)
    {
        if (!string.IsNullOrEmpty(entity.RenderName))
            switch (entity.Type)
            {
                //TODO: Handle doors and similar obstructions to movement/pathfinding

                //TODO: Handle waypoint (initial claim as well as using to teleport somewhere)

                //Handle clickable teleporters
                case ExileCore.Shared.Enums.EntityType.AreaTransition:
                case ExileCore.Shared.Enums.EntityType.Portal:
                case ExileCore.Shared.Enums.EntityType.TownPortal:
                    if (!_areaTransitions.ContainsKey(entity.Id))
                        _areaTransitions.Add(entity.Id, entity);
                    break;
            }
        base.EntityAdded(entity);
    }

    public override void EntityRemoved(Entity entity)
    {
        switch (entity.Type)
        {
            //TODO: Handle doors and similar obstructions to movement/pathfinding

            //TODO: Handle waypoint (initial claim as well as using to teleport somewhere)

            //Handle clickable teleporters
            case ExileCore.Shared.Enums.EntityType.AreaTransition:
            case ExileCore.Shared.Enums.EntityType.Portal:
            case ExileCore.Shared.Enums.EntityType.TownPortal:
                if (_areaTransitions.ContainsKey(entity.Id))
                    _areaTransitions.Remove(entity.Id);
                break;
        }
        base.EntityRemoved(entity);
    }


    public override void Render()
    {
        if (_tasks != null && _tasks.Count > 1)
            for (var i = 1; i < _tasks.Count; i++)
            {
                var start = WorldToValidScreenPosition(_tasks[i - 1].WorldPosition);
                var end = WorldToValidScreenPosition(_tasks[i].WorldPosition);
                Graphics.DrawLine(start, end, 2, SharpDX.Color.Pink);
            }
        
        // Draw last known good position if we're in teleport search mode
        if (_isSearchingForTeleport && _lastKnownGoodPosition != Vector3.Zero)
        {
            var lastPosScreen = WorldToValidScreenPosition(_lastKnownGoodPosition);
            Graphics.DrawText("LAST POS", lastPosScreen, SharpDX.Color.Red);
        }
        
        var dist = _tasks.Count > 0 ? Vector3.Distance(GameController.Player.Pos, _tasks.First().WorldPosition) : 0;
        var targetDist = _lastTargetPosition == null ? "NA" : Vector3.Distance(GameController.Player.Pos, _lastTargetPosition).ToString();
        Graphics.DrawText($"Follow Enabled: {Settings.IsFollowEnabled.Value}", new Vector2(500, 120));
        Graphics.DrawText($"Task Count: {_tasks.Count} Next WP Distance: {dist} Target Distance: {targetDist}", new Vector2(500, 140));
        
        // Show stuck detection status
        if (Settings.EnableStuckDetection.Value && _isStuckDetectionActive)
        {
            var stuckDuration = DateTime.Now - _stuckDetectionStartTime;
            var stuckColor = _stuckRecoveryAttempts > 0 ? SharpDX.Color.Red : SharpDX.Color.Yellow;
            Graphics.DrawText($"STUCK DETECTED! Duration: {stuckDuration.TotalSeconds:F1}s | Attempts: {_stuckRecoveryAttempts}", 
                new Vector2(500, 160), stuckColor);
        }
        
        // Show death handling status
        if (Settings.EnableDeathHandling.Value && _isWaitingForResurrection)
        {
            var deathDuration = DateTime.Now - _deathDetectedTime;
            Graphics.DrawText($"WAITING FOR RESURRECTION: {deathDuration.TotalSeconds:F1}s", 
                new Vector2(500, 180), SharpDX.Color.Purple);
        }
        
        // Show safety features status
        if (Settings.EnableSafetyFeatures.Value && _isPausedForSafety)
        {
            Graphics.DrawText("PAUSED FOR SAFETY (Outside Game)", 
                new Vector2(500, 200), SharpDX.Color.Orange);
        }
        
        // Show inventory management status
        if (Settings.EnableInventoryManagement.Value && _isManagingInventory)
        {
            Graphics.DrawText("MANAGING INVENTORY", 
                new Vector2(500, 220), SharpDX.Color.Cyan);
        }
        
        // Show multiple leader status
        if (Settings.EnableMultipleLeaders.Value && _followTarget != null)
        {
            var leaderName = _followTarget.GetComponent<Player>()?.PlayerName ?? "Unknown";
            Graphics.DrawText($"Following: {leaderName} (Multi-Leader Mode)", 
                new Vector2(500, 240), SharpDX.Color.LightGreen);
        }
        
        // Show teleport search status
        if (_isSearchingForTeleport)
        {
            var searchTime = DateTime.Now - _teleportSearchStartTime;
            Graphics.DrawText($"TELEPORT SEARCH: {searchTime.TotalSeconds:F1}s", new Vector2(500, 160), SharpDX.Color.Yellow);
        }
        
        // Show portal avoidance status
        if (Settings.EnablePortalAvoidance.Value && IsInPortalAvoidanceGracePeriod())
        {
            var graceTime = DateTime.Now - _areaChangeTime;
            var remainingTime = Settings.PortalAvoidanceGracePeriod.Value - graceTime.TotalMilliseconds;
            var isAvoidingPortal = _followTarget != null && IsPositionTooCloseToPortal(_followTarget.Pos);
            var statusText = isAvoidingPortal ? "AVOIDING PORTAL" : "PORTAL GRACE";
            var statusColor = isAvoidingPortal ? SharpDX.Color.Red : SharpDX.Color.Orange;
            Graphics.DrawText($"{statusText}: {remainingTime / 1000:F1}s", new Vector2(500, 280), statusColor);
        }
        
        // Show gem leveling status
        if (_isLevelingGems)
        {
            Graphics.DrawText("LEVELING GEMS", new Vector2(500, 180), SharpDX.Color.Cyan);
        }
        else if (Settings.AutoLevelGems.Value && HasGemsToLevel())
        {
            Graphics.DrawText("GEMS AVAILABLE", new Vector2(500, 180), SharpDX.Color.Green);
        }
        
        // Show dash status
        if (Settings.IsDashEnabled.Value)
        {
            var timeSinceLastDash = DateTime.Now - _lastDashTime;
            var dashCooldownRemaining = Settings.DashCooldown.Value - timeSinceLastDash.TotalMilliseconds;
            
            if (dashCooldownRemaining > 0)
            {
                Graphics.DrawText($"DASH CD: {dashCooldownRemaining / 1000:F1}s", new Vector2(500, 200), SharpDX.Color.Orange);
            }
            else if (Settings.AggressiveDash.Value)
            {
                Graphics.DrawText("DASH READY (AGG)", new Vector2(500, 200), SharpDX.Color.LightGreen);
            }
            else
            {
                Graphics.DrawText("DASH READY", new Vector2(500, 200), SharpDX.Color.White);
            }
        }
        
        // Show coroutine status if monitoring is enabled
        if (Settings.EnableCoroutineMonitoring.Value)
        {
            var followerStatus = _followerCoroutine?.Running == true ? "RUNNING" : "STOPPED";
            var followerColor = _followerCoroutine?.Running == true ? SharpDX.Color.Green : SharpDX.Color.Red;
            Graphics.DrawText($"Follower Coroutine: {followerStatus}", new Vector2(500, 220), followerColor);
            
            if (_postTransitionCoroutine != null)
            {
                var graceStatus = _postTransitionCoroutine.Running ? "ACTIVE" : "INACTIVE";
                var graceColor = _postTransitionCoroutine.Running ? SharpDX.Color.Yellow : SharpDX.Color.Gray;
                Graphics.DrawText($"Grace Period: {graceStatus}", new Vector2(500, 240), graceColor);
            }
            
            if (_isTransitioning)
            {
                Graphics.DrawText("TRANSITIONING", new Vector2(500, 260), SharpDX.Color.Cyan);
            }
        }
        
        // Show PathStatus debug information
        if (Settings.ShowPathStatusDebug.Value && _tasks.Count > 0)
        {
            var currentTask = _tasks.First();
            var pathStatus = GetPathStatus(GameController.Player.GridPos, currentTask.WorldPosition.WorldToGrid());
            
            var statusText = $"PathStatus: {pathStatus}";
            var statusColor = pathStatus switch
            {
                PathStatus.Clear => SharpDX.Color.Green,
                PathStatus.Dashable => SharpDX.Color.Yellow,
                PathStatus.Blocked => SharpDX.Color.Red,
                PathStatus.Invalid => SharpDX.Color.Purple,
                _ => SharpDX.Color.White
            };
            
            Graphics.DrawText(statusText, new Vector2(500, 280), statusColor);
            
            // Show recommended action
            var actionText = pathStatus switch
            {
                PathStatus.Clear => "Action: Walk",
                PathStatus.Dashable => "Action: Dash",
                PathStatus.Blocked => "Action: Skip/Reroute",
                PathStatus.Invalid => "Action: Invalid Path",
                _ => "Action: Unknown"
            };
            
            Graphics.DrawText(actionText, new Vector2(500, 300), statusColor);
        }
        
        // Show zone reliability information
        if (Settings.ShowPathStatusDebug.Value)
        {
            var zoneReliable = IsLeaderZoneInfoReliable();
            var reliabilityText = $"Zone Reliable: {zoneReliable}";
            var reliabilityColor = zoneReliable ? SharpDX.Color.Green : SharpDX.Color.Red;
            
            Graphics.DrawText(reliabilityText, new Vector2(500, 320), reliabilityColor);
        }
        
        var counter = 0;
        foreach (var transition in _areaTransitions)
        {
            counter++;
            Graphics.DrawText($"{transition.Key} at {transition.Value.Pos.X} {transition.Value.Pos.Y}", new Vector2(100, 120 + counter * 20));
        }
        
        // Render advanced debug visualizations
        RenderTerrainVisualization();
        RenderTaskDebug();
        RenderEntityDebug();
    }


    private Vector2 WorldToValidScreenPosition(Vector3 worldPos)
    {
        var windowRect = GameController.Window.GetWindowRectangle();
        var screenPos = Camera.WorldToScreen(worldPos);
        var result = screenPos + windowRect.Location;

        // Calculate allowed area as percentage from center
        var centerX = windowRect.X + windowRect.Width / 2;
        var centerY = windowRect.Y + windowRect.Height / 2;
        var allowedPercent = Settings.MouseMovementAreaPercent.Value / 100.0f;
        
        var maxOffsetX = (windowRect.Width / 2) * allowedPercent;
        var maxOffsetY = (windowRect.Height / 2) * allowedPercent;
        
        // Constrain to the allowed area
        var constrainedX = Math.Max(centerX - maxOffsetX, Math.Min(centerX + maxOffsetX, result.X));
        var constrainedY = Math.Max(centerY - maxOffsetY, Math.Min(centerY + maxOffsetY, result.Y));
        
        return new Vector2(constrainedX, constrainedY);
    }
    
    public override void OnClose()
    {
        // Stop all coroutines
        _followerCoroutine?.Done();
        _postTransitionCoroutine?.Done();
        
        base.OnClose();
    }
}
