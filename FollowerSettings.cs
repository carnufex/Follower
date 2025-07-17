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
    
    // Core Settings Section
    [Menu("Core Settings", "Toggle Follower")] public HotkeyNode ToggleFollower { get; set; } = Keys.PageUp;
    [Menu("Core Settings", "Follow Target Name")] public TextNode LeaderName { get; set; } = new TextNode("");
    
    // Movement & Pathfinding Section
    [Menu("Movement & Pathfinding", "Movement Key")] public HotkeyNode MovementKey { get; set; } = Keys.T;
    [Menu("Movement & Pathfinding", "Follow Close")] public ToggleNode IsCloseFollowEnabled { get; set; } = new ToggleNode(false);
    [Menu("Movement & Pathfinding", "Min Path Distance")] public RangeNode<int> PathfindingNodeDistance { get; set; } = new RangeNode<int>(200, 10, 1000);
    [Menu("Movement & Pathfinding", "Move CMD Frequency")] public RangeNode<int> BotInputFrequency { get; set; } = new RangeNode<int>(50, 10, 250);
    [Menu("Movement & Pathfinding", "Stop Path Distance")] public RangeNode<int> ClearPathDistance { get; set; } = new RangeNode<int>(500, 100, 5000);
    [Menu("Movement & Pathfinding", "Transition Distance")] public RangeNode<int> TransitionDistance { get; set; } = new RangeNode<int>(200, 50, 1000);
    [Menu("Movement & Pathfinding", "Waypoint Distance")] public RangeNode<int> WaypointDistance { get; set; } = new RangeNode<int>(150, 50, 500);
    [Menu("Movement & Pathfinding", "Max Task Attempts")] public RangeNode<int> MaxTaskAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    [Menu("Movement & Pathfinding", "Normal Follow Distance")] public RangeNode<int> NormalFollowDistance { get; set; } = new RangeNode<int>(1500, 500, 3000);
    [Menu("Movement & Pathfinding", "Random Click Offset")] public RangeNode<int> RandomClickOffset { get; set; } = new RangeNode<int>(10, 1, 100);
    
    // Dash & Movement Enhancement Section
    [Menu("Dash & Movement", "Allow Dash")] public ToggleNode IsDashEnabled { get; set; } = new ToggleNode(false);
    [Menu("Dash & Movement", "Dash Key")] public HotkeyNode DashKey { get; set; } = Keys.W;
    [Menu("Dash & Movement", "Dash Distance Threshold")] public RangeNode<int> DashDistanceThreshold { get; set; } = new RangeNode<int>(800, 200, 2000);
    [Menu("Dash & Movement", "Dash Cooldown (ms)")] public RangeNode<int> DashCooldown { get; set; } = new RangeNode<int>(500, 100, 2000);
    [Menu("Dash & Movement", "Mouse Movement Area (% from center)")] public RangeNode<int> MouseMovementAreaPercent { get; set; } = new RangeNode<int>(65, 30, 90);
    
    // Safety & Detection Section
    [Menu("Safety & Detection", "Enable Stuck Detection")] public ToggleNode EnableStuckDetection { get; set; } = new ToggleNode(false);
    [Menu("Safety & Detection", "Stuck Detection Time (ms)")] public RangeNode<int> StuckDetectionTime { get; set; } = new RangeNode<int>(3000, 1000, 10000);
    [Menu("Safety & Detection", "Stuck Movement Threshold")] public RangeNode<int> StuckMovementThreshold { get; set; } = new RangeNode<int>(50, 10, 200);
    [Menu("Safety & Detection", "Max Stuck Recovery Attempts")] public RangeNode<int> MaxStuckRecoveryAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    [Menu("Safety & Detection", "Pause on Logout Screen")] public ToggleNode PauseOnLogout { get; set; } = new ToggleNode(true);
    [Menu("Safety & Detection", "Pause on Character Select")] public ToggleNode PauseOnCharacterSelect { get; set; } = new ToggleNode(true);
    
    // UI Avoidance Section
    [Menu("UI Avoidance", "Enable Smart UI Avoidance")] public ToggleNode EnableSmartUIAvoidance { get; set; } = new ToggleNode(true);
    [Menu("UI Avoidance", "UI Avoidance Distance")] public RangeNode<int> UIAvoidanceDistance { get; set; } = new RangeNode<int>(100, 20, 200);
    [Menu("UI Avoidance", "Mouse Random Offset")] public RangeNode<int> MouseRandomOffset { get; set; } = new RangeNode<int>(8, 2, 20);
    [Menu("UI Avoidance", "Show UI Debug Rectangles")] public ToggleNode ShowUIDebugRectangles { get; set; } = new ToggleNode(false);
    [Menu("UI Avoidance", "Exclude Top Edge")] public ToggleNode ExcludeTopEdge { get; set; } = new ToggleNode(true);
    [Menu("UI Avoidance", "Top Edge Exclusion Height")] public RangeNode<int> TopEdgeExclusionHeight { get; set; } = new RangeNode<int>(50, 20, 150);
    [Menu("UI Avoidance", "Exclude Bottom Edge")] public ToggleNode ExcludeBottomEdge { get; set; } = new ToggleNode(true);
    [Menu("UI Avoidance", "Bottom Edge Exclusion Height")] public RangeNode<int> BottomEdgeExclusionHeight { get; set; } = new RangeNode<int>(80, 20, 200);
    
    // Leader Management Section
    [Menu("Leader Management", "Enable Multiple Leaders")] public ToggleNode EnableMultipleLeaders { get; set; } = new ToggleNode(false);
    [Menu("Leader Management", "Leader Names (comma separated)")] public TextNode LeaderNames { get; set; } = new TextNode("");
    [Menu("Leader Management", "Leader Switch Distance")] public RangeNode<int> LeaderSwitchDistance { get; set; } = new RangeNode<int>(2000, 500, 5000);
    [Menu("Leader Management", "Prioritize Closest Leader")] public ToggleNode PrioritizeClosestLeader { get; set; } = new ToggleNode(true);
    [Menu("Leader Management", "Teleport Detection Distance")] public RangeNode<int> TeleportDetectionDistance { get; set; } = new RangeNode<int>(4000, 2000, 10000);
    [Menu("Leader Management", "Search Last Position")] public ToggleNode SearchLastPosition { get; set; } = new ToggleNode(true);
    
    // Death & Recovery Section
    [Menu("Death & Recovery", "Enable Death Handling")] public ToggleNode EnableDeathHandling { get; set; } = new ToggleNode(false);
    [Menu("Death & Recovery", "Auto Resume After Death")] public ToggleNode AutoResumeAfterDeath { get; set; } = new ToggleNode(true);
    [Menu("Death & Recovery", "Death Detection Check Interval (ms)")] public RangeNode<int> DeathCheckInterval { get; set; } = new RangeNode<int>(1000, 500, 5000);
    [Menu("Death & Recovery", "Resurrection Wait Timeout (ms)")] public RangeNode<int> ResurrectionTimeout { get; set; } = new RangeNode<int>(30000, 10000, 120000);
    
    // Inventory Management Section
    [Menu("Inventory Management", "Enable Inventory Management")] public ToggleNode EnableInventoryManagement { get; set; } = new ToggleNode(false);
    [Menu("Inventory Management", "Auto Portal on Full Inventory")] public ToggleNode AutoPortalOnFullInventory { get; set; } = new ToggleNode(true);
    [Menu("Inventory Management", "Inventory Full Threshold")] public RangeNode<int> InventoryFullThreshold { get; set; } = new RangeNode<int>(55, 40, 60);
    [Menu("Inventory Management", "Portal Wait Time (ms)")] public RangeNode<int> PortalWaitTime { get; set; } = new RangeNode<int>(5000, 2000, 15000);
    [Menu("Inventory Management", "Enable Portal Avoidance")] public ToggleNode EnablePortalAvoidance { get; set; } = new ToggleNode(true);
    [Menu("Inventory Management", "Portal Avoidance Distance")] public RangeNode<int> PortalAvoidanceDistance { get; set; } = new RangeNode<int>(300, 150, 800);
    
    // Gem Management Section
    [Menu("Gem Management", "Auto Level Gems")] public ToggleNode AutoLevelGems { get; set; } = new ToggleNode(true);
    [Menu("Gem Management", "Level Gems When Close")] public ToggleNode LevelGemsWhenClose { get; set; } = new ToggleNode(true);
    [Menu("Gem Management", "Level Gems When Stopped")] public ToggleNode LevelGemsWhenStopped { get; set; } = new ToggleNode(true);
    [Menu("Gem Management", "Gem Level Check Interval (ms)")] public RangeNode<int> GemLevelCheckInterval { get; set; } = new RangeNode<int>(2000, 500, 10000);
    
    // Plugin Integration Section
    [Menu("Plugin Integration", "Yield to PickItV2")] public ToggleNode YieldToPickItV2 { get; set; } = new ToggleNode(false);
    [Menu("Plugin Integration", "PickItV2 Yield Timeout (ms)")] public RangeNode<int> PickItV2YieldTimeout { get; set; } = new RangeNode<int>(5000, 1000, 15000);
    [Menu("Plugin Integration", "Yield to ReAgent")] public ToggleNode YieldToReAgent { get; set; } = new ToggleNode(false);
    [Menu("Plugin Integration", "ReAgent Yield Timeout (ms)")] public RangeNode<int> ReAgentYieldTimeout { get; set; } = new RangeNode<int>(2000, 500, 5000);
    
    // Performance & Timing Section
    [Menu("Performance & Timing", "Max Pathfinding Iterations")] public RangeNode<int> MaxPathfindingIterations { get; set; } = new RangeNode<int>(500, 100, 2000);
    [Menu("Performance & Timing", "Terrain Refresh Rate (ms)")] public RangeNode<int> TerrainRefreshRate { get; set; } = new RangeNode<int>(1000, 500, 5000);
    [Menu("Performance & Timing", "Path Update Frequency (ms)")] public RangeNode<int> PathUpdateFrequency { get; set; } = new RangeNode<int>(250, 100, 1000);
    [Menu("Performance & Timing", "Enable Performance Monitoring")] public ToggleNode EnablePerformanceMonitoring { get; set; } = new ToggleNode(false);
    [Menu("Performance & Timing", "Log Level")] public RangeNode<int> LogLevel { get; set; } = new RangeNode<int>(1, 0, 4); // 0=Debug, 1=Info, 2=Warning, 3=Error, 4=Critical
    
    // Debug & Visualization Section
    [Menu("Debug & Visualization", "Show Path Debug")] public ToggleNode ShowPathStatusDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug & Visualization", "Show Task Debug")] public ToggleNode ShowTaskDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug & Visualization", "Show Terrain Visualization")] public ToggleNode ShowTerrainVisualization { get; set; } = new ToggleNode(false);
    [Menu("Debug & Visualization", "Show Entity Debug")] public ToggleNode ShowEntityDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug & Visualization", "Show Debug Paths")] public ToggleNode ShowDebugPaths { get; set; } = new ToggleNode(false);
}