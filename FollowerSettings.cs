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
    
    // Portal Avoidance Settings
    [Menu("Enable Portal Avoidance")] public ToggleNode EnablePortalAvoidance { get; set; } = new ToggleNode(true);
    [Menu("Portal Avoidance Distance")] public RangeNode<int> PortalAvoidanceDistance { get; set; } = new RangeNode<int>(300, 150, 800);
    [Menu("Portal Avoidance Grace Period (ms)")] public RangeNode<int> PortalAvoidanceGracePeriod { get; set; } = new RangeNode<int>(3000, 1000, 10000);
    
    // Leader Commands Integration Settings
    [Menu("Enable Leader Commands")] public ToggleNode EnableLeaderCommands { get; set; } = new ToggleNode(false);
    [Menu("Leader Commands Check Interval (ms)")] public RangeNode<int> LeaderCommandsCheckInterval { get; set; } = new RangeNode<int>(1000, 500, 5000);
    [Menu("Execute Commands While Following")] public ToggleNode ExecuteCommandsWhileFollowing { get; set; } = new ToggleNode(true);
    [Menu("Max Command Execution Time (ms)")] public RangeNode<int> MaxCommandExecutionTime { get; set; } = new RangeNode<int>(60000, 10000, 300000);
    
    // Network Communication Settings
    [Menu("Enable Network Communication")] public ToggleNode EnableNetworkCommunication { get; set; } = new ToggleNode(false);
    [Menu("Enable Auto Discovery")] public ToggleNode EnableAutoDiscovery { get; set; } = new ToggleNode(true);
    [Menu("Leader IP Address")] public TextNode LeaderIpAddress { get; set; } = new TextNode("");
    [Menu("Leader Port")] public RangeNode<int> LeaderPort { get; set; } = new RangeNode<int>(7777, 1024, 65535);
    [Menu("Discovery Port")] public RangeNode<int> DiscoveryPort { get; set; } = new RangeNode<int>(7778, 1024, 65535);
    [Menu("Connection Retry Interval (ms)")] public RangeNode<int> ConnectionRetryInterval { get; set; } = new RangeNode<int>(5000, 1000, 30000);
    [Menu("Heartbeat Interval (ms)")] public RangeNode<int> HeartbeatInterval { get; set; } = new RangeNode<int>(10000, 5000, 60000);
    
    // Stashing Settings
    [Menu("Enable Stashing Commands")] public ToggleNode EnableStashingCommands { get; set; } = new ToggleNode(true);
    [Menu("Stash Tab Switch Delay (ms)")] public RangeNode<int> StashTabSwitchDelay { get; set; } = new RangeNode<int>(200, 50, 1000);
    [Menu("Item Placement Delay (ms)")] public RangeNode<int> ItemPlacementDelay { get; set; } = new RangeNode<int>(100, 50, 500);
    
    // Selling Settings  
    [Menu("Enable Selling Commands")] public ToggleNode EnableSellingCommands { get; set; } = new ToggleNode(true);
    [Menu("Vendor Interaction Delay (ms)")] public RangeNode<int> VendorInteractionDelay { get; set; } = new RangeNode<int>(300, 100, 1000);
    [Menu("Item Sell Delay (ms)")] public RangeNode<int> ItemSellDelay { get; set; } = new RangeNode<int>(150, 50, 500);
    
    // Trading Settings
    [Menu("Enable Trading Commands")] public ToggleNode EnableTradingCommands { get; set; } = new ToggleNode(true);
    [Menu("Trade Request Timeout (ms)")] public RangeNode<int> TradeRequestTimeout { get; set; } = new RangeNode<int>(15000, 5000, 60000);
    [Menu("Trade Window Delay (ms)")] public RangeNode<int> TradeWindowDelay { get; set; } = new RangeNode<int>(500, 100, 2000);
    
    // Security Settings
    [Menu("Enable Security Features")] public ToggleNode EnableSecurityFeatures { get; set; } = new ToggleNode(true);
    [Menu("Enable Message Encryption")] public ToggleNode EnableMessageEncryption { get; set; } = new ToggleNode(true);
    [Menu("Enable Authentication")] public ToggleNode EnableAuthentication { get; set; } = new ToggleNode(true);
    [Menu("API Key")] public TextNode ApiKey { get; set; } = new TextNode("");
    [Menu("Max Message Age (ms)")] public RangeNode<int> MaxMessageAge { get; set; } = new RangeNode<int>(30000, 10000, 300000);
    [Menu("Security Audit Logging")] public ToggleNode SecurityAuditLogging { get; set; } = new ToggleNode(true);
    
    // Advanced Monitoring Settings
    [Menu("Enable Performance Monitoring")] public ToggleNode EnablePerformanceMonitoring { get; set; } = new ToggleNode(true);
    [Menu("Enable Health Checks")] public ToggleNode EnableHealthChecks { get; set; } = new ToggleNode(true);
    [Menu("Health Check Interval (ms)")] public RangeNode<int> HealthCheckInterval { get; set; } = new RangeNode<int>(30000, 10000, 300000);
    [Menu("Metrics Collection Interval (ms)")] public RangeNode<int> MetricsCollectionInterval { get; set; } = new RangeNode<int>(30000, 10000, 300000);
    [Menu("Enable Structured Logging")] public ToggleNode EnableStructuredLogging { get; set; } = new ToggleNode(true);
    [Menu("Log Level")] public RangeNode<int> LogLevel { get; set; } = new RangeNode<int>(1, 0, 4); // 0=Debug, 1=Info, 2=Warning, 3=Error, 4=Critical
    
    // Circuit Breaker Settings
    [Menu("Enable Circuit Breakers")] public ToggleNode EnableCircuitBreakers { get; set; } = new ToggleNode(true);
    [Menu("Circuit Breaker Failure Threshold")] public RangeNode<int> CircuitBreakerFailureThreshold { get; set; } = new RangeNode<int>(5, 2, 20);
    [Menu("Circuit Breaker Timeout (ms)")] public RangeNode<int> CircuitBreakerTimeout { get; set; } = new RangeNode<int>(60000, 10000, 300000);
    [Menu("Circuit Breaker Reset Attempts")] public RangeNode<int> CircuitBreakerResetAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    
    // Message Protocol Settings
    [Menu("Enable Enhanced Protocol")] public ToggleNode EnableEnhancedProtocol { get; set; } = new ToggleNode(true);
    [Menu("Protocol Version")] public RangeNode<int> ProtocolVersion { get; set; } = new RangeNode<int>(2, 1, 2);
    [Menu("Message Deduplication")] public ToggleNode EnableMessageDeduplication { get; set; } = new ToggleNode(true);
    [Menu("Message Ordering")] public ToggleNode EnableMessageOrdering { get; set; } = new ToggleNode(true);
    [Menu("Message Prioritization")] public ToggleNode EnableMessagePrioritization { get; set; } = new ToggleNode(true);
    [Menu("Message Compression")] public ToggleNode EnableMessageCompression { get; set; } = new ToggleNode(false);
    
    // Auto-Configuration Settings
    [Menu("Enable Auto Configuration")] public ToggleNode EnableAutoConfiguration { get; set; } = new ToggleNode(true);
    [Menu("Auto Optimize Performance")] public ToggleNode AutoOptimizePerformance { get; set; } = new ToggleNode(true);
    [Menu("Auto Detect Network")] public ToggleNode AutoDetectNetwork { get; set; } = new ToggleNode(true);
    [Menu("Configuration Validation")] public ToggleNode EnableConfigurationValidation { get; set; } = new ToggleNode(true);
}