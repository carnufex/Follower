using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;

namespace Follower;

public class FollowerSettings : ISettings
{
    // Core Settings (Always visible at top)
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ToggleNode IsFollowEnabled { get; set; } = new ToggleNode(false);
    public HotkeyNode ToggleFollower { get; set; } = Keys.PageUp;
    public TextNode LeaderName { get; set; } = new TextNode("");
    
    // Submenu Settings Groups
    public MovementSettings Movement { get; set; } = new();
    public PathfindingSettings Pathfinding { get; set; } = new();
    public DashSettings Dash { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public UIAvoidanceSettings UIAvoidance { get; set; } = new();
    public LeaderSettings Leader { get; set; } = new();
    public DeathSettings Death { get; set; } = new();
    public InventorySettings Inventory { get; set; } = new();
    public GemSettings Gems { get; set; } = new();
    public PluginSettings Plugins { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
	public LinkSettings Link { get; set; } = new();
	public DebugSettings Debug { get; set; } = new();
}

[Submenu(CollapsedByDefault = true)]
public class MovementSettings
{
    [Menu("Movement Key", "Key used for movement commands")]
    public HotkeyNode MovementKey { get; set; } = Keys.T;
    
    [Menu("Follow Close", "Follow closely behind the leader")]
    public ToggleNode IsCloseFollowEnabled { get; set; } = new ToggleNode(false);
    
    [Menu("Min Path Distance", "Minimum distance to start pathfinding")]
    public RangeNode<int> PathfindingNodeDistance { get; set; } = new RangeNode<int>(200, 10, 1000);
    
    [Menu("Move CMD Frequency", "How often to send movement commands (ms)")]
    public RangeNode<int> BotInputFrequency { get; set; } = new RangeNode<int>(50, 10, 250);
    
    [Menu("Stop Path Distance", "Distance to stop current path")]
    public RangeNode<int> ClearPathDistance { get; set; } = new RangeNode<int>(500, 100, 5000);
    
    [Menu("Transition Distance", "Distance threshold for area transitions")]
    public RangeNode<int> TransitionDistance { get; set; } = new RangeNode<int>(200, 50, 1000);
    
    [Menu("Waypoint Distance", "Distance to claim waypoints")]
    public RangeNode<int> WaypointDistance { get; set; } = new RangeNode<int>(150, 50, 500);
    
    [Menu("Leader Max Distance", "Maximum distance from leader before stopping")]
    public RangeNode<int> LeaderMaxDistance { get; set; } = new RangeNode<int>(1000, 100, 3000);
    
    [Menu("Normal Follow Distance", "Standard follow distance for timing calculations")]
    public RangeNode<int> NormalFollowDistance { get; set; } = new RangeNode<int>(1500, 500, 3000);
    
    [Menu("Min Action Distance", "Minimum distance to perform any action")]
    public RangeNode<int> MinActionDistance { get; set; } = new RangeNode<int>(20, 5, 100);
    
    [Menu("Max Action Distance", "Maximum distance to perform any action")]
    public RangeNode<int> MaxActionDistance { get; set; } = new RangeNode<int>(800, 200, 2000);
}

[Submenu(CollapsedByDefault = true)]
public class PathfindingSettings
{
    [Menu("Enable Advanced Pathfinding", "Use A* pathfinding instead of direct movement")]
    public ToggleNode EnableAdvancedPathfinding { get; set; } = new ToggleNode(true);
    
    [Menu("Enable Predictive Following", "Predict where leader is going and follow ahead")]
    public ToggleNode EnablePredictiveFollowing { get; set; } = new ToggleNode(true);
    
    [Menu("Prediction Distance", "How far ahead to predict leader movement")]
    public RangeNode<int> PredictionDistance { get; set; } = new RangeNode<int>(200, 50, 500);
    
    [Menu("Path Recalculation Threshold", "Grid distance before recalculating path")]
    public RangeNode<int> PathRecalculationThreshold { get; set; } = new RangeNode<int>(5, 1, 20);
    
    [Menu("Max Waypoints Per Update", "Maximum waypoints to add to task queue")]
    public RangeNode<int> MaxWaypointsPerUpdate { get; set; } = new RangeNode<int>(3, 1, 10);
    
    [Menu("Direction Field Cache Duration", "How long to cache direction fields (seconds)")]
    public RangeNode<int> DirectionFieldCacheDuration { get; set; } = new RangeNode<int>(300, 30, 1800);
    
    [Menu("Pathfinding Timeout", "Maximum time to spend on pathfinding (ms)")]
    public RangeNode<int> PathfindingTimeout { get; set; } = new RangeNode<int>(100, 10, 1000);
}

[Submenu(CollapsedByDefault = true)]
public class DashSettings
{
    [Menu("Auto Dash", "Automatically use dash skills")]
    public ToggleNode AutoDash { get; set; } = new ToggleNode(false);
    
    [Menu("Dash Key", "Key for dash skill")]
    public HotkeyNode DashKey { get; set; } = Keys.Q;
    
    [Menu("Dash Distance", "Distance threshold to use dash")]
    public RangeNode<int> DashDistance { get; set; } = new RangeNode<int>(400, 100, 1000);
    
    [Menu("Dash Cooldown", "Cooldown between dash uses (ms)")]
    public RangeNode<int> DashCooldown { get; set; } = new RangeNode<int>(3000, 1000, 10000);
    
    [Menu("Dash in Combat", "Allow dash when enemies are nearby")]
    public ToggleNode DashInCombat { get; set; } = new ToggleNode(false);
}

[Submenu(CollapsedByDefault = true)]
public class SafetySettings
{
    [Menu("Enable Stuck Detection", "Detect when follower is stuck")]
    public ToggleNode EnableStuckDetection { get; set; } = new ToggleNode(true);
    
    [Menu("Stuck Distance Threshold", "Distance threshold for stuck detection")]
    public RangeNode<int> StuckDistanceThreshold { get; set; } = new RangeNode<int>(10, 5, 50);
    
    [Menu("Stuck Time Threshold", "Time threshold for stuck detection (ms)")]
    public RangeNode<int> StuckTimeThreshold { get; set; } = new RangeNode<int>(5000, 1000, 15000);
    
    [Menu("Random Movement Range", "Range for random movement when stuck")]
    public RangeNode<int> RandomMovementRange { get; set; } = new RangeNode<int>(50, 20, 200);
    
    [Menu("Max Stuck Recovery Attempts", "Maximum attempts to recover from stuck")]
    public RangeNode<int> MaxStuckRecoveryAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    
    [Menu("Max Task Attempts", "Maximum attempts per task before giving up")]
    public RangeNode<int> MaxTaskAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    
    [Menu("Pause on Logout Screen", "Pause when logout screen is visible")]
    public ToggleNode PauseOnLogout { get; set; } = new ToggleNode(true);
    
    [Menu("Pause on Character Select", "Pause when character select is visible")]
    public ToggleNode PauseOnCharacterSelect { get; set; } = new ToggleNode(true);
}

[Submenu(CollapsedByDefault = true)]
public class UIAvoidanceSettings
{
    [Menu("Enable Smart UI Avoidance", "Avoid clicking on UI elements")]
    public ToggleNode EnableSmartUIAvoidance { get; set; } = new ToggleNode(true);
    
    [Menu("UI Avoidance Distance", "Distance to avoid UI elements")]
    public RangeNode<int> UIAvoidanceDistance { get; set; } = new RangeNode<int>(100, 20, 200);
    
    [Menu("Mouse Random Offset", "Random offset for mouse clicks")]
    public RangeNode<int> MouseRandomOffset { get; set; } = new RangeNode<int>(8, 2, 20);
    
    [Menu("Show UI Debug Rectangles", "Display UI element rectangles")]
    public ToggleNode ShowUIDebugRectangles { get; set; } = new ToggleNode(false);
    
    [Menu("Exclude Top Edge", "Exclude clicks near top edge")]
    public ToggleNode ExcludeTopEdge { get; set; } = new ToggleNode(true);
    
    [Menu("Top Edge Exclusion Height", "Height of top edge exclusion")]
    public RangeNode<int> TopEdgeExclusionHeight { get; set; } = new RangeNode<int>(50, 20, 150);
    
    [Menu("Exclude Bottom Edge", "Exclude clicks near bottom edge")]
    public ToggleNode ExcludeBottomEdge { get; set; } = new ToggleNode(true);
    
    [Menu("Bottom Edge Exclusion Height", "Height of bottom edge exclusion")]
    public RangeNode<int> BottomEdgeExclusionHeight { get; set; } = new RangeNode<int>(80, 20, 200);
}

[Submenu(CollapsedByDefault = true)]
public class LeaderSettings
{
    [Menu("Enable Multiple Leaders", "Support following multiple leaders")]
    public ToggleNode EnableMultipleLeaders { get; set; } = new ToggleNode(false);
    
    [Menu("Leader Prioritization", "How to prioritize multiple leaders")]
    public ListNode LeaderPrioritization { get; set; } = new ListNode { Values = new System.Collections.Generic.List<string> { "Closest", "First Found", "Alphabetical" }, Value = "Closest" };
    
    [Menu("Leader Switch Threshold", "Distance threshold to switch leaders")]
    public RangeNode<int> LeaderSwitchThreshold { get; set; } = new RangeNode<int>(300, 100, 1000);
    
    [Menu("Leader Timeout", "Time before considering leader lost (ms)")]
    public RangeNode<int> LeaderTimeout { get; set; } = new RangeNode<int>(15000, 5000, 60000);
    
    [Menu("Leader Search Range", "Range to search for leader")]
    public RangeNode<int> LeaderSearchRange { get; set; } = new RangeNode<int>(2000, 500, 5000);
    
    [Menu("Auto Resume Following", "Automatically resume following when leader returns")]
    public ToggleNode AutoResumeFollowing { get; set; } = new ToggleNode(true);
}

[Submenu(CollapsedByDefault = true)]
public class DeathSettings
{
    [Menu("Enable Death Handling", "Handle player death events")]
    public ToggleNode EnableDeathHandling { get; set; } = new ToggleNode(true);
    
    [Menu("Pause on Death", "Pause following when player dies")]
    public ToggleNode PauseOnDeath { get; set; } = new ToggleNode(true);
    
    [Menu("Auto Resume After Resurrect", "Resume following after resurrection")]
    public ToggleNode AutoResumeAfterResurrect { get; set; } = new ToggleNode(true);
    
    [Menu("Death Detection Delay", "Delay before detecting death (ms)")]
    public RangeNode<int> DeathDetectionDelay { get; set; } = new RangeNode<int>(1000, 500, 5000);
}

[Submenu(CollapsedByDefault = true)]
public class InventorySettings
{
    [Menu("Enable Inventory Management", "Manage inventory during following")]
    public ToggleNode EnableInventoryManagement { get; set; } = new ToggleNode(false);
    
    [Menu("Auto Return to Town", "Return to town when inventory is full")]
    public ToggleNode AutoReturnToTown { get; set; } = new ToggleNode(false);
    
    [Menu("Use Portal for Town", "Use portal instead of waypoint")]
    public ToggleNode UsePortalForTown { get; set; } = new ToggleNode(true);
    
    [Menu("Inventory Full Threshold", "Inventory slots threshold for full")]
    public RangeNode<int> InventoryFullThreshold { get; set; } = new RangeNode<int>(55, 40, 60);
    
    [Menu("Portal Wait Time", "Time to wait for portal (ms)")]
    public RangeNode<int> PortalWaitTime { get; set; } = new RangeNode<int>(5000, 2000, 15000);
    
    [Menu("Enable Portal Avoidance", "Avoid standing on portals")]
    public ToggleNode EnablePortalAvoidance { get; set; } = new ToggleNode(true);
    
    [Menu("Portal Avoidance Distance", "Distance to avoid portals")]
    public RangeNode<int> PortalAvoidanceDistance { get; set; } = new RangeNode<int>(300, 150, 800);
}

[Submenu(CollapsedByDefault = false)]
public class GemSettings
{
    [Menu("Auto Level Gems", "Automatically level skill gems")]
    public ToggleNode AutoLevelGems { get; set; } = new ToggleNode(true);
    
    [Menu("Level Gems When Close", "Level gems when close to leader")]
    public ToggleNode LevelGemsWhenClose { get; set; } = new ToggleNode(true);
    
    [Menu("Level Gems When Stopped", "Level gems when not moving")]
    public ToggleNode LevelGemsWhenStopped { get; set; } = new ToggleNode(true);
    
    [Menu("Gem Level Check Interval", "How often to check for gem levels (ms)")]
    public RangeNode<int> GemLevelCheckInterval { get; set; } = new RangeNode<int>(2000, 500, 10000);
}

[Submenu(CollapsedByDefault = true)]
public class PluginSettings
{
    [Menu("Yield to PickItV2", "Pause when PickItV2 is active")]
    public ToggleNode YieldToPickItV2 { get; set; } = new ToggleNode(false);
    
    [Menu("PickItV2 Yield Timeout", "Timeout for PickItV2 yield (ms)")]
    public RangeNode<int> PickItV2YieldTimeout { get; set; } = new RangeNode<int>(5000, 1000, 15000);
    
    [Menu("Yield to ReAgent", "Pause when ReAgent is active")]
    public ToggleNode YieldToReAgent { get; set; } = new ToggleNode(false);
    
    [Menu("ReAgent Yield Timeout", "Timeout for ReAgent yield (ms)")]
    public RangeNode<int> ReAgentYieldTimeout { get; set; } = new RangeNode<int>(2000, 500, 5000);
}

[Submenu(CollapsedByDefault = true)]
public class PerformanceSettings
{
    [Menu("Max Pathfinding Iterations", "Maximum iterations for pathfinding")]
    public RangeNode<int> MaxPathfindingIterations { get; set; } = new RangeNode<int>(500, 100, 2000);
    
    [Menu("Terrain Refresh Rate", "How often to refresh terrain (ms)")]
    public RangeNode<int> TerrainRefreshRate { get; set; } = new RangeNode<int>(1000, 500, 5000);
    
    [Menu("Path Update Frequency", "How often to update path (ms)")]
    public RangeNode<int> PathUpdateFrequency { get; set; } = new RangeNode<int>(250, 100, 1000);
    
    [Menu("Enable Performance Monitoring", "Monitor performance metrics")]
    public ToggleNode EnablePerformanceMonitoring { get; set; } = new ToggleNode(false);
    
    [Menu("Log Level", "Logging level (0=Debug, 1=Info, 2=Warning, 3=Error, 4=Critical)")]
    public RangeNode<int> LogLevel { get; set; } = new RangeNode<int>(1, 0, 4);
}

[Submenu(CollapsedByDefault = true)]
public class LinkSettings
{
	[Menu("Link Key", "Key used to apply Link skill")]
	public HotkeyNode LinkKey { get; set; } = Keys.R;

	[Menu("Enable Link Support", "Enable automatic Link application")]
	public ToggleNode EnableLinkSupport { get; set; } = new ToggleNode(false);
}

[Submenu(CollapsedByDefault = true)]
public class DebugSettings
{
    [Menu("Enable Debug Mode", "Show detailed debug information")]
    public ToggleNode EnableDebugMode { get; set; } = new ToggleNode(false);
    
    [Menu("Enable Action State Logging", "Log leader action state changes")]
    public ToggleNode EnableActionStateLogging { get; set; } = new ToggleNode(false);
    
    [Menu("Debug Display Position", "Y position for debug display")]
    public RangeNode<int> DebugDisplayPosition { get; set; } = new RangeNode<int>(200, 100, 800);
    
    [Menu("Log Performance Metrics", "Log timing and performance data")]
    public ToggleNode LogPerformanceMetrics { get; set; } = new ToggleNode(false);
    
    [Menu("Show Task Details", "Display detailed task information")]
    public ToggleNode ShowTaskDetails { get; set; } = new ToggleNode(false);
}