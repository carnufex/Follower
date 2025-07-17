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
    
    // Gem Management Section
    [Menu("Gem Management", "Auto Level Gems")] public ToggleNode AutoLevelGems { get; set; } = new ToggleNode(true);
    [Menu("Gem Management", "Level Gems When Close")] public ToggleNode LevelGemsWhenClose { get; set; } = new ToggleNode(true);
    [Menu("Gem Management", "Level Gems When Stopped")] public ToggleNode LevelGemsWhenStopped { get; set; } = new ToggleNode(true);
    [Menu("Gem Management", "Gem Level Check Interval (ms)")] public RangeNode<int> GemLevelCheckInterval { get; set; } = new RangeNode<int>(2000, 500, 10000);
    
    // Debug & Visualization Section
    [Menu("Debug & Visualization", "Show Path Debug")] public ToggleNode ShowPathStatusDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug & Visualization", "Show Task Debug")] public ToggleNode ShowTaskDebug { get; set; } = new ToggleNode(false);
}