using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;

namespace Follower;

public class FollowerSettings : ISettings
{
    // Core Settings
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ToggleNode IsFollowEnabled { get; set; } = new ToggleNode(false);
    [Menu("Toggle Follower")] public HotkeyNode ToggleFollower { get; set; } = Keys.PageUp;
    [Menu("Follow Target Name")] public TextNode LeaderName { get; set; } = new TextNode("");
    
    // Basic Settings
    [Menu("Movement Key")] public HotkeyNode MovementKey { get; set; } = Keys.T;
    [Menu("Follow Close")] public ToggleNode IsCloseFollowEnabled { get; set; } = new ToggleNode(false);
    [Menu("Min Path Distance")] public RangeNode<int> PathfindingNodeDistance { get; set; } = new RangeNode<int>(200, 10, 1000);
    [Menu("Move CMD Frequency")] public RangeNode<int> BotInputFrequency { get; set; } = new RangeNode<int>(50, 10, 250);
    [Menu("Stop Path Distance")] public RangeNode<int> ClearPathDistance { get; set; } = new RangeNode<int>(500, 100, 5000);
    [Menu("Random Click Offset")] public RangeNode<int> RandomClickOffset { get; set; } = new RangeNode<int>(10, 1, 100);
    
    // Dash Settings
    [Menu("Allow Dash")] public ToggleNode IsDashEnabled { get; set; } = new ToggleNode(true);
    [Menu("Dash Key")] public HotkeyNode DashKey { get; set; } = Keys.W;
    [Menu("Aggressive Dash")] public ToggleNode AggressiveDash { get; set; } = new ToggleNode(true);
    [Menu("Dash Distance Threshold")] public RangeNode<int> DashDistanceThreshold { get; set; } = new RangeNode<int>(800, 200, 2000);
    [Menu("Dash Cooldown (ms)")] public RangeNode<int> DashCooldown { get; set; } = new RangeNode<int>(500, 100, 2000);
    
    // Pathfinding Settings
    [Menu("Transition Distance")] public RangeNode<int> TransitionDistance { get; set; } = new RangeNode<int>(200, 50, 1000);
    [Menu("Waypoint Distance")] public RangeNode<int> WaypointDistance { get; set; } = new RangeNode<int>(150, 50, 500);
    [Menu("Max Pathfinding Iterations")] public RangeNode<int> MaxPathfindingIterations { get; set; } = new RangeNode<int>(500, 100, 2000);
    [Menu("Max Task Attempts")] public RangeNode<int> MaxTaskAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    
    // Teleport Detection Settings
    [Menu("Teleport Detection Distance")] public RangeNode<int> TeleportDetectionDistance { get; set; } = new RangeNode<int>(4000, 2000, 10000);
    [Menu("Normal Follow Distance")] public RangeNode<int> NormalFollowDistance { get; set; } = new RangeNode<int>(1500, 500, 3000);
    [Menu("Search Last Position")] public ToggleNode SearchLastPosition { get; set; } = new ToggleNode(true);
    
    // Gem Leveling Settings
    [Menu("Auto Level Gems")] public ToggleNode AutoLevelGems { get; set; } = new ToggleNode(true);
    [Menu("Level Gems When Close")] public ToggleNode LevelGemsWhenClose { get; set; } = new ToggleNode(true);
    [Menu("Level Gems When Stopped")] public ToggleNode LevelGemsWhenStopped { get; set; } = new ToggleNode(true);
    [Menu("Gem Level Check Interval (ms)")] public RangeNode<int> GemLevelCheckInterval { get; set; } = new RangeNode<int>(2000, 500, 10000);
    
    // Plugin Integration Settings
    [Menu("Yield to PickItV2")] public ToggleNode YieldToPickItV2 { get; set; } = new ToggleNode(false);
    [Menu("PickItV2 Yield Timeout (ms)")] public RangeNode<int> PickItV2YieldTimeout { get; set; } = new RangeNode<int>(5000, 1000, 15000);
    
    // Advanced Settings
    [Menu("Post-Transition Grace Period (ms)")] public RangeNode<int> PostTransitionGracePeriod { get; set; } = new RangeNode<int>(10000, 5000, 30000);
    [Menu("Enable Coroutine Monitoring")] public ToggleNode EnableCoroutineMonitoring { get; set; } = new ToggleNode(true);
    [Menu("Terrain Refresh Rate (ms)")] public RangeNode<int> TerrainRefreshRate { get; set; } = new RangeNode<int>(1000, 500, 5000);
    
    // Stuck Detection Settings
    [Menu("Enable Stuck Detection")] public ToggleNode EnableStuckDetection { get; set; } = new ToggleNode(true);
    [Menu("Stuck Detection Time (ms)")] public RangeNode<int> StuckDetectionTime { get; set; } = new RangeNode<int>(3000, 1000, 10000);
    [Menu("Stuck Movement Threshold")] public RangeNode<int> StuckMovementThreshold { get; set; } = new RangeNode<int>(50, 10, 200);
    [Menu("Max Stuck Recovery Attempts")] public RangeNode<int> MaxStuckRecoveryAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    [Menu("Aggressive Stuck Detection")] public ToggleNode AggressiveStuckDetection { get; set; } = new ToggleNode(true);
    [Menu("Unreachable Position Detection")] public ToggleNode UnreachablePositionDetection { get; set; } = new ToggleNode(true);
    [Menu("Max Same Position Attempts")] public RangeNode<int> MaxSamePositionAttempts { get; set; } = new RangeNode<int>(5, 2, 15);
    [Menu("Position Similarity Threshold")] public RangeNode<int> PositionSimilarityThreshold { get; set; } = new RangeNode<int>(100, 50, 300);
    
    // Death & Resurrection Settings
    [Menu("Enable Death Handling")] public ToggleNode EnableDeathHandling { get; set; } = new ToggleNode(true);
    [Menu("Auto Resume After Death")] public ToggleNode AutoResumeAfterDeath { get; set; } = new ToggleNode(true);
    [Menu("Death Detection Check Interval (ms)")] public RangeNode<int> DeathCheckInterval { get; set; } = new RangeNode<int>(1000, 500, 5000);
    [Menu("Resurrection Wait Timeout (ms)")] public RangeNode<int> ResurrectionTimeout { get; set; } = new RangeNode<int>(30000, 10000, 120000);
    
    // Multiple Leader Settings
    [Menu("Enable Multiple Leaders")] public ToggleNode EnableMultipleLeaders { get; set; } = new ToggleNode(false);
    [Menu("Leader Names (comma separated)")] public TextNode LeaderNames { get; set; } = new TextNode("");
    [Menu("Leader Switch Distance")] public RangeNode<int> LeaderSwitchDistance { get; set; } = new RangeNode<int>(2000, 500, 5000);
    [Menu("Prioritize Closest Leader")] public ToggleNode PrioritizeClosestLeader { get; set; } = new ToggleNode(true);
    
    // Safety Features
    [Menu("Enable Safety Features")] public ToggleNode EnableSafetyFeatures { get; set; } = new ToggleNode(true);
    [Menu("Pause on Logout Screen")] public ToggleNode PauseOnLogout { get; set; } = new ToggleNode(true);
    [Menu("Pause on Character Select")] public ToggleNode PauseOnCharacterSelect { get; set; } = new ToggleNode(true);
    [Menu("Auto Resume on Game Return")] public ToggleNode AutoResumeOnGameReturn { get; set; } = new ToggleNode(true);
    
    // Inventory Management Settings
    [Menu("Enable Inventory Management")] public ToggleNode EnableInventoryManagement { get; set; } = new ToggleNode(false);
    [Menu("Auto Portal on Full Inventory")] public ToggleNode AutoPortalOnFullInventory { get; set; } = new ToggleNode(true);
    [Menu("Inventory Full Threshold")] public RangeNode<int> InventoryFullThreshold { get; set; } = new RangeNode<int>(55, 40, 60);
    [Menu("Portal Wait Time (ms)")] public RangeNode<int> PortalWaitTime { get; set; } = new RangeNode<int>(5000, 2000, 15000);
    
    // Debug Settings
    [Menu("Show PathStatus Debug")] public ToggleNode ShowPathStatusDebug { get; set; } = new ToggleNode(false);
    [Menu("Show Terrain Visualization")] public ToggleNode ShowTerrainVisualization { get; set; } = new ToggleNode(false);
    [Menu("Show Task Debug")] public ToggleNode ShowTaskDebug { get; set; } = new ToggleNode(false);
    [Menu("Show Entity Debug")] public ToggleNode ShowEntityDebug { get; set; } = new ToggleNode(false);
    [Menu("Show Raycast Debug")] public ToggleNode ShowRaycastDebug { get; set; } = new ToggleNode(false);
    
    // Mouse Movement Settings
    [Menu("Mouse Movement Area (% from center)")] public RangeNode<int> MouseMovementAreaPercent { get; set; } = new RangeNode<int>(75, 50, 100);
    [Menu("Force Click During Movement")] public ToggleNode ForceClickDuringMovement { get; set; } = new ToggleNode(true);
}