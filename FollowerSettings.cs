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
    
    // Movement Settings
    [Menu("Movement", "Movement Key")] public HotkeyNode MovementKey { get; set; } = Keys.T;
    [Menu("Movement", "Follow Close")] public ToggleNode IsCloseFollowEnabled { get; set; } = new ToggleNode(false);
    [Menu("Movement", "Min Path Distance")] public RangeNode<int> PathfindingNodeDistance { get; set; } = new RangeNode<int>(200, 10, 1000);
    [Menu("Movement", "Move CMD Frequency")] public RangeNode<int> BotInputFrequency { get; set; } = new RangeNode<int>(50, 10, 250);
    [Menu("Movement", "Stop Path Distance")] public RangeNode<int> ClearPathDistance { get; set; } = new RangeNode<int>(500, 100, 5000);
    [Menu("Movement", "Random Click Offset")] public RangeNode<int> RandomClickOffset { get; set; } = new RangeNode<int>(10, 1, 100);
    
    // Dash Settings
    [Menu("Dash", "Allow Dash")] public ToggleNode IsDashEnabled { get; set; } = new ToggleNode(true);
    [Menu("Dash", "Dash Key")] public HotkeyNode DashKey { get; set; } = Keys.W;
    [Menu("Dash", "Aggressive Dash")] public ToggleNode AggressiveDash { get; set; } = new ToggleNode(true);
    [Menu("Dash", "Dash Distance Threshold")] public RangeNode<int> DashDistanceThreshold { get; set; } = new RangeNode<int>(800, 200, 2000);
    
    [Menu("Dash", "Dash Cooldown (ms)")] public RangeNode<int> DashCooldown { get; set; } = new RangeNode<int>(500, 100, 2000);
    
    // Pathfinding Settings
    [Menu("Pathfinding", "Transition Distance")] public RangeNode<int> TransitionDistance { get; set; } = new RangeNode<int>(200, 50, 1000);
    [Menu("Pathfinding", "Waypoint Distance")] public RangeNode<int> WaypointDistance { get; set; } = new RangeNode<int>(150, 50, 500);
    [Menu("Pathfinding", "Max Pathfinding Iterations")] public RangeNode<int> MaxPathfindingIterations { get; set; } = new RangeNode<int>(500, 100, 2000);
    [Menu("Pathfinding", "Max Task Attempts")] public RangeNode<int> MaxTaskAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    
    // Teleport Detection Settings
    [Menu("Teleport Detection", "Teleport Detection Distance")] public RangeNode<int> TeleportDetectionDistance { get; set; } = new RangeNode<int>(4000, 2000, 10000);
    [Menu("Teleport Detection", "Normal Follow Distance")] public RangeNode<int> NormalFollowDistance { get; set; } = new RangeNode<int>(1500, 500, 3000);
    [Menu("Teleport Detection", "Search Last Position")] public ToggleNode SearchLastPosition { get; set; } = new ToggleNode(true);
    
    // Gem Leveling Settings
    [Menu("Gem Leveling", "Auto Level Gems")] public ToggleNode AutoLevelGems { get; set; } = new ToggleNode(true);
    [Menu("Gem Leveling", "Level Gems When Close")] public ToggleNode LevelGemsWhenClose { get; set; } = new ToggleNode(true);
    [Menu("Gem Leveling", "Level Gems When Stopped")] public ToggleNode LevelGemsWhenStopped { get; set; } = new ToggleNode(true);
    [Menu("Gem Leveling", "Gem Level Check Interval (ms)")] public RangeNode<int> GemLevelCheckInterval { get; set; } = new RangeNode<int>(2000, 500, 10000);
    
    // Plugin Integration Settings
    [Menu("Plugin Integration", "Yield to PickItV2")] public ToggleNode YieldToPickItV2 { get; set; } = new ToggleNode(false);
    [Menu("Plugin Integration", "PickItV2 Yield Timeout (ms)")] public RangeNode<int> PickItV2YieldTimeout { get; set; } = new RangeNode<int>(5000, 1000, 15000);
    
    // Advanced Settings
    [Menu("Advanced", "Post-Transition Grace Period (ms)")] public RangeNode<int> PostTransitionGracePeriod { get; set; } = new RangeNode<int>(10000, 5000, 30000);
    [Menu("Advanced", "Enable Coroutine Monitoring")] public ToggleNode EnableCoroutineMonitoring { get; set; } = new ToggleNode(true);
    
    // Debug Settings
    [Menu("Debug", "Show PathStatus Debug")] public ToggleNode ShowPathStatusDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Show Terrain Visualization")] public ToggleNode ShowTerrainVisualization { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Show Task Debug")] public ToggleNode ShowTaskDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Show Entity Debug")] public ToggleNode ShowEntityDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Show Raycast Debug")] public ToggleNode ShowRaycastDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug", "Debug Terrain Refresh Rate (ms)")] public RangeNode<int> DebugTerrainRefreshRate { get; set; } = new RangeNode<int>(1000, 500, 5000);
}