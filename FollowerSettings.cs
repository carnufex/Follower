using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;

namespace Follower;

public class FollowerSettings : ISettings
{
    // Core Settings (these don't have Menu attributes, so they show as property names)
    public ToggleNode Enable { get; set; } = new ToggleNode(false);
    public ToggleNode IsFollowEnabled { get; set; } = new ToggleNode(false);
    [Menu("Toggle Follower")] public HotkeyNode ToggleFollower { get; set; } = Keys.PageUp;
    [Menu("Follow Target Name")] public TextNode LeaderName { get; set; } = new TextNode("");
    
    // Movement Settings - using submenu structure
    [Menu("Movement", "Movement Key")] public HotkeyNode MovementKey_ { get; set; } = Keys.T;
    [Menu("Movement", "Follow Close")] public ToggleNode IsCloseFollowEnabled_ { get; set; } = new ToggleNode(false);
    [Menu("Movement", "Min Path Distance")] public RangeNode<int> PathfindingNodeDistance_ { get; set; } = new RangeNode<int>(200, 10, 1000);
    [Menu("Movement", "Move CMD Frequency")] public RangeNode<int> BotInputFrequency_ { get; set; } = new RangeNode<int>(50, 10, 250);
    [Menu("Movement", "Stop Path Distance")] public RangeNode<int> ClearPathDistance_ { get; set; } = new RangeNode<int>(500, 100, 5000);
    [Menu("Movement", "Random Click Offset")] public RangeNode<int> RandomClickOffset_ { get; set; } = new RangeNode<int>(10, 1, 100);
    
    // Dash Settings - using submenu structure
    [Menu("Dash", "Allow Dash")] public ToggleNode IsDashEnabled_ { get; set; } = new ToggleNode(true);
    [Menu("Dash", "Dash Key")] public HotkeyNode DashKey_ { get; set; } = Keys.W;
    [Menu("Dash", "Aggressive Dash")] public ToggleNode AggressiveDash_ { get; set; } = new ToggleNode(true);
    [Menu("Dash", "Dash Distance Threshold")] public RangeNode<int> DashDistanceThreshold_ { get; set; } = new RangeNode<int>(800, 200, 2000);
    [Menu("Dash", "Dash Cooldown (ms)")] public RangeNode<int> DashCooldown_ { get; set; } = new RangeNode<int>(500, 100, 2000);
    
    // Pathfinding Settings - using submenu structure
    [Menu("Pathfinding", "Transition Distance")] public RangeNode<int> TransitionDistance_ { get; set; } = new RangeNode<int>(200, 50, 1000);
    [Menu("Pathfinding", "Waypoint Distance")] public RangeNode<int> WaypointDistance_ { get; set; } = new RangeNode<int>(150, 50, 500);
    [Menu("Pathfinding", "Max Pathfinding Iterations")] public RangeNode<int> MaxPathfindingIterations_ { get; set; } = new RangeNode<int>(500, 100, 2000);
    [Menu("Pathfinding", "Max Task Attempts")] public RangeNode<int> MaxTaskAttempts_ { get; set; } = new RangeNode<int>(3, 1, 10);
    
    // Teleport Detection Settings - using submenu structure
    [Menu("Teleport Detection", "Teleport Detection Distance")] public RangeNode<int> TeleportDetectionDistance_ { get; set; } = new RangeNode<int>(4000, 2000, 10000);
    [Menu("Teleport Detection", "Normal Follow Distance")] public RangeNode<int> NormalFollowDistance_ { get; set; } = new RangeNode<int>(1500, 500, 3000);
    [Menu("Teleport Detection", "Search Last Position")] public ToggleNode SearchLastPosition_ { get; set; } = new ToggleNode(true);
    
    // Gem Leveling Settings - using submenu structure
    [Menu("Gem Leveling", "Auto Level Gems")] public ToggleNode AutoLevelGems_ { get; set; } = new ToggleNode(true);
    [Menu("Gem Leveling", "Level Gems When Close")] public ToggleNode LevelGemsWhenClose_ { get; set; } = new ToggleNode(true);
    [Menu("Gem Leveling", "Level Gems When Stopped")] public ToggleNode LevelGemsWhenStopped_ { get; set; } = new ToggleNode(true);
    [Menu("Gem Leveling", "Gem Level Check Interval (ms)")] public RangeNode<int> GemLevelCheckInterval_ { get; set; } = new RangeNode<int>(2000, 500, 10000);
    
    // Plugin Integration Settings - using submenu structure
    [Menu("Plugin Integration", "Yield to PickItV2")] public ToggleNode YieldToPickItV2_ { get; set; } = new ToggleNode(false);
    [Menu("Plugin Integration", "PickItV2 Yield Timeout (ms)")] public RangeNode<int> PickItV2YieldTimeout_ { get; set; } = new RangeNode<int>(5000, 1000, 15000);
    
    // Advanced Settings - using submenu structure
    [Menu("Advanced", "Post-Transition Grace Period (ms)")] public RangeNode<int> PostTransitionGracePeriod_ { get; set; } = new RangeNode<int>(10000, 5000, 30000);
    [Menu("Advanced", "Enable Coroutine Monitoring")] public ToggleNode EnableCoroutineMonitoring_ { get; set; } = new ToggleNode(true);
    
    // Debug Settings - using submenu structure
    [Menu("Debug", "Show PathStatus Debug")] public ToggleNode ShowPathStatusDebug_ { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Show Terrain Visualization")] public ToggleNode ShowTerrainVisualization_ { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Show Task Debug")] public ToggleNode ShowTaskDebug_ { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Show Entity Debug")] public ToggleNode ShowEntityDebug_ { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Show Raycast Debug")] public ToggleNode ShowRaycastDebug_ { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Debug Terrain Refresh Rate (ms)")] public RangeNode<int> DebugTerrainRefreshRate_ { get; set; } = new RangeNode<int>(1000, 500, 5000);

    // Compatibility properties for existing code (these will be hidden duplicates but keep the code working)
    public HotkeyNode MovementKey => MovementKey_;
    public ToggleNode IsCloseFollowEnabled => IsCloseFollowEnabled_;
    public RangeNode<int> PathfindingNodeDistance => PathfindingNodeDistance_;
    public RangeNode<int> BotInputFrequency => BotInputFrequency_;
    public RangeNode<int> ClearPathDistance => ClearPathDistance_;
    public RangeNode<int> RandomClickOffset => RandomClickOffset_;
    public ToggleNode IsDashEnabled => IsDashEnabled_;
    public HotkeyNode DashKey => DashKey_;
    public ToggleNode AggressiveDash => AggressiveDash_;
    public RangeNode<int> DashDistanceThreshold => DashDistanceThreshold_;
    public RangeNode<int> DashCooldown => DashCooldown_;
    public RangeNode<int> TransitionDistance => TransitionDistance_;
    public RangeNode<int> WaypointDistance => WaypointDistance_;
    public RangeNode<int> MaxPathfindingIterations => MaxPathfindingIterations_;
    public RangeNode<int> MaxTaskAttempts => MaxTaskAttempts_;
    public RangeNode<int> TeleportDetectionDistance => TeleportDetectionDistance_;
    public RangeNode<int> NormalFollowDistance => NormalFollowDistance_;
    public ToggleNode SearchLastPosition => SearchLastPosition_;
    public ToggleNode AutoLevelGems => AutoLevelGems_;
    public ToggleNode LevelGemsWhenClose => LevelGemsWhenClose_;
    public ToggleNode LevelGemsWhenStopped => LevelGemsWhenStopped_;
    public RangeNode<int> GemLevelCheckInterval => GemLevelCheckInterval_;
    public ToggleNode YieldToPickItV2 => YieldToPickItV2_;
    public RangeNode<int> PickItV2YieldTimeout => PickItV2YieldTimeout_;
    public RangeNode<int> PostTransitionGracePeriod => PostTransitionGracePeriod_;
    public ToggleNode EnableCoroutineMonitoring => EnableCoroutineMonitoring_;
    public ToggleNode ShowPathStatusDebug => ShowPathStatusDebug_;
    public ToggleNode ShowTerrainVisualization => ShowTerrainVisualization_;
    public ToggleNode ShowTaskDebug => ShowTaskDebug_;
    public ToggleNode ShowEntityDebug => ShowEntityDebug_;
    public ToggleNode ShowRaycastDebug => ShowRaycastDebug_;
    public RangeNode<int> DebugTerrainRefreshRate => DebugTerrainRefreshRate_;
}