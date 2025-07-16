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
    [Menu("Core/Toggle Follower")] public HotkeyNode ToggleFollower { get; set; } = Keys.PageUp;
    [Menu("Core/Follow Target Name")] public TextNode LeaderName { get; set; } = new TextNode("");
    
    // Movement & Pathfinding Settings
    [Menu("Movement & Pathfinding/Movement Key")] public HotkeyNode MovementKey { get; set; } = Keys.T;
    [Menu("Movement & Pathfinding/Follow Close")] public ToggleNode IsCloseFollowEnabled { get; set; } = new ToggleNode(false);
    [Menu("Movement & Pathfinding/Min Path Distance")] public RangeNode<int> PathfindingNodeDistance { get; set; } = new RangeNode<int>(200, 10, 1000);
    [Menu("Movement & Pathfinding/Move CMD Frequency")] public RangeNode<int> BotInputFrequency { get; set; } = new RangeNode<int>(50, 10, 250);
    [Menu("Movement & Pathfinding/Stop Path Distance")] public RangeNode<int> ClearPathDistance { get; set; } = new RangeNode<int>(500, 100, 5000);
    [Menu("Movement & Pathfinding/Random Click Offset")] public RangeNode<int> RandomClickOffset { get; set; } = new RangeNode<int>(10, 1, 100);
    [Menu("Movement & Pathfinding/Transition Distance")] public RangeNode<int> TransitionDistance { get; set; } = new RangeNode<int>(200, 50, 1000);
    [Menu("Movement & Pathfinding/Waypoint Distance")] public RangeNode<int> WaypointDistance { get; set; } = new RangeNode<int>(150, 50, 500);
    [Menu("Movement & Pathfinding/Max Pathfinding Iterations")] public RangeNode<int> MaxPathfindingIterations { get; set; } = new RangeNode<int>(500, 100, 2000);
    [Menu("Movement & Pathfinding/Max Task Attempts")] public RangeNode<int> MaxTaskAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    [Menu("Movement & Pathfinding/Normal Follow Distance")] public RangeNode<int> NormalFollowDistance { get; set; } = new RangeNode<int>(1500, 500, 3000);
    [Menu("Movement & Pathfinding/Post-Transition Grace Period (ms)")] public RangeNode<int> PostTransitionGracePeriod { get; set; } = new RangeNode<int>(10000, 5000, 30000);
    [Menu("Movement & Pathfinding/Terrain Refresh Rate (ms)")] public RangeNode<int> TerrainRefreshRate { get; set; } = new RangeNode<int>(1000, 500, 5000);
    
    // Dash & Movement Enhancement
    [Menu("Dash & Movement/Allow Dash")] public ToggleNode IsDashEnabled { get; set; } = new ToggleNode(true);
    [Menu("Dash & Movement/Dash Key")] public HotkeyNode DashKey { get; set; } = Keys.W;
    [Menu("Dash & Movement/Aggressive Dash")] public ToggleNode AggressiveDash { get; set; } = new ToggleNode(true);
    [Menu("Dash & Movement/Dash Distance Threshold")] public RangeNode<int> DashDistanceThreshold { get; set; } = new RangeNode<int>(800, 200, 2000);
    [Menu("Dash & Movement/Dash Cooldown (ms)")] public RangeNode<int> DashCooldown { get; set; } = new RangeNode<int>(500, 100, 2000);
    [Menu("Dash & Movement/Mouse Movement Area (% from center)")] public RangeNode<int> MouseMovementAreaPercent { get; set; } = new RangeNode<int>(65, 30, 90);
    [Menu("Dash & Movement/Force Click During Movement")] public ToggleNode ForceClickDuringMovement { get; set; } = new ToggleNode(true);
    
    // Smart UI Avoidance
    [Menu("Smart UI Avoidance/Enable Smart UI Avoidance")] public ToggleNode EnableSmartUIAvoidance { get; set; } = new ToggleNode(true);
    [Menu("Smart UI Avoidance/UI Avoidance Distance")] public RangeNode<int> UIAvoidanceDistance { get; set; } = new RangeNode<int>(50, 20, 150);
    [Menu("Smart UI Avoidance/Mouse Random Offset")] public RangeNode<int> MouseRandomOffset { get; set; } = new RangeNode<int>(8, 2, 20);
    [Menu("Smart UI Avoidance/Distant Target Threshold")] public RangeNode<int> DistantTargetThreshold { get; set; } = new RangeNode<int>(1000, 500, 3000);
    [Menu("Smart UI Avoidance/Show UI Debug Rectangles")] public ToggleNode ShowUIDebugRectangles { get; set; } = new ToggleNode(false);
    [Menu("Smart UI Avoidance/Exclude Top Edge")] public ToggleNode ExcludeTopEdge { get; set; } = new ToggleNode(true);
    [Menu("Smart UI Avoidance/Top Edge Exclusion Height")] public RangeNode<int> TopEdgeExclusionHeight { get; set; } = new RangeNode<int>(50, 20, 150);
    [Menu("Smart UI Avoidance/Exclude Bottom Edge")] public ToggleNode ExcludeBottomEdge { get; set; } = new ToggleNode(true);
    [Menu("Smart UI Avoidance/Bottom Edge Exclusion Height")] public RangeNode<int> BottomEdgeExclusionHeight { get; set; } = new RangeNode<int>(80, 20, 200);
    [Menu("Smart UI Avoidance/Exclude Left Edge")] public ToggleNode ExcludeLeftEdge { get; set; } = new ToggleNode(false);
    [Menu("Smart UI Avoidance/Left Edge Exclusion Width")] public RangeNode<int> LeftEdgeExclusionWidth { get; set; } = new RangeNode<int>(50, 20, 150);
    [Menu("Smart UI Avoidance/Exclude Right Edge")] public ToggleNode ExcludeRightEdge { get; set; } = new ToggleNode(false);
    [Menu("Smart UI Avoidance/Right Edge Exclusion Width")] public RangeNode<int> RightEdgeExclusionWidth { get; set; } = new RangeNode<int>(50, 20, 150);
    
    // Leader Management
    [Menu("Leader Management/Enable Multiple Leaders")] public ToggleNode EnableMultipleLeaders { get; set; } = new ToggleNode(false);
    [Menu("Leader Management/Leader Names (comma separated)")] public TextNode LeaderNames { get; set; } = new TextNode("");
    [Menu("Leader Management/Leader Switch Distance")] public RangeNode<int> LeaderSwitchDistance { get; set; } = new RangeNode<int>(2000, 500, 5000);
    [Menu("Leader Management/Prioritize Closest Leader")] public ToggleNode PrioritizeClosestLeader { get; set; } = new ToggleNode(true);
    [Menu("Leader Management/Teleport Detection Distance")] public RangeNode<int> TeleportDetectionDistance { get; set; } = new RangeNode<int>(4000, 2000, 10000);
    [Menu("Leader Management/Search Last Position")] public ToggleNode SearchLastPosition { get; set; } = new ToggleNode(true);
    [Menu("Leader Management/Enable Shared Position Fallback")] public ToggleNode EnableSharedPositionFallback { get; set; } = new ToggleNode(true);
    [Menu("Leader Management/Shared Position Max Age (seconds)")] public RangeNode<int> SharedPositionMaxAge { get; set; } = new RangeNode<int>(5, 1, 30);
    [Menu("Leader Management/Shared Position Check Interval (ms)")] public RangeNode<int> SharedPositionCheckInterval { get; set; } = new RangeNode<int>(500, 100, 2000);
    
    // Safety & Detection
    [Menu("Safety & Detection/Enable Safety Features")] public ToggleNode EnableSafetyFeatures { get; set; } = new ToggleNode(true);
    [Menu("Safety & Detection/Pause on Logout Screen")] public ToggleNode PauseOnLogout { get; set; } = new ToggleNode(true);
    [Menu("Safety & Detection/Pause on Character Select")] public ToggleNode PauseOnCharacterSelect { get; set; } = new ToggleNode(true);
    [Menu("Safety & Detection/Auto Resume on Game Return")] public ToggleNode AutoResumeOnGameReturn { get; set; } = new ToggleNode(true);
    [Menu("Safety & Detection/Enable Stuck Detection")] public ToggleNode EnableStuckDetection { get; set; } = new ToggleNode(true);
    [Menu("Safety & Detection/Stuck Detection Time (ms)")] public RangeNode<int> StuckDetectionTime { get; set; } = new RangeNode<int>(3000, 1000, 10000);
    [Menu("Safety & Detection/Stuck Movement Threshold")] public RangeNode<int> StuckMovementThreshold { get; set; } = new RangeNode<int>(50, 10, 200);
    [Menu("Safety & Detection/Max Stuck Recovery Attempts")] public RangeNode<int> MaxStuckRecoveryAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    [Menu("Safety & Detection/Aggressive Stuck Detection")] public ToggleNode AggressiveStuckDetection { get; set; } = new ToggleNode(true);
    [Menu("Safety & Detection/Unreachable Position Detection")] public ToggleNode UnreachablePositionDetection { get; set; } = new ToggleNode(true);
    [Menu("Safety & Detection/Max Same Position Attempts")] public RangeNode<int> MaxSamePositionAttempts { get; set; } = new RangeNode<int>(5, 2, 15);
    [Menu("Safety & Detection/Position Similarity Threshold")] public RangeNode<int> PositionSimilarityThreshold { get; set; } = new RangeNode<int>(100, 50, 300);
    
    // Death & Recovery
    [Menu("Death & Recovery/Enable Death Handling")] public ToggleNode EnableDeathHandling { get; set; } = new ToggleNode(true);
    [Menu("Death & Recovery/Auto Resume After Death")] public ToggleNode AutoResumeAfterDeath { get; set; } = new ToggleNode(true);
    [Menu("Death & Recovery/Death Detection Check Interval (ms)")] public RangeNode<int> DeathCheckInterval { get; set; } = new RangeNode<int>(1000, 500, 5000);
    [Menu("Death & Recovery/Resurrection Wait Timeout (ms)")] public RangeNode<int> ResurrectionTimeout { get; set; } = new RangeNode<int>(30000, 10000, 120000);
    
    // Inventory Management
    [Menu("Inventory Management/Enable Inventory Management")] public ToggleNode EnableInventoryManagement { get; set; } = new ToggleNode(false);
    [Menu("Inventory Management/Auto Portal on Full Inventory")] public ToggleNode AutoPortalOnFullInventory { get; set; } = new ToggleNode(true);
    [Menu("Inventory Management/Inventory Full Threshold")] public RangeNode<int> InventoryFullThreshold { get; set; } = new RangeNode<int>(55, 40, 60);
    [Menu("Inventory Management/Portal Wait Time (ms)")] public RangeNode<int> PortalWaitTime { get; set; } = new RangeNode<int>(5000, 2000, 15000);
    [Menu("Inventory Management/Enable Portal Avoidance")] public ToggleNode EnablePortalAvoidance { get; set; } = new ToggleNode(true);
    [Menu("Inventory Management/Portal Avoidance Distance")] public RangeNode<int> PortalAvoidanceDistance { get; set; } = new RangeNode<int>(300, 150, 800);
    [Menu("Inventory Management/Portal Avoidance Grace Period (ms)")] public RangeNode<int> PortalAvoidanceGracePeriod { get; set; } = new RangeNode<int>(3000, 1000, 10000);
    
    // Gem Management
    [Menu("Gem Management/Auto Level Gems")] public ToggleNode AutoLevelGems { get; set; } = new ToggleNode(true);
    [Menu("Gem Management/Level Gems When Close")] public ToggleNode LevelGemsWhenClose { get; set; } = new ToggleNode(true);
    [Menu("Gem Management/Level Gems When Stopped")] public ToggleNode LevelGemsWhenStopped { get; set; } = new ToggleNode(true);
    [Menu("Gem Management/Gem Level Check Interval (ms)")] public RangeNode<int> GemLevelCheckInterval { get; set; } = new RangeNode<int>(2000, 500, 10000);
    
    // Plugin Integration
    [Menu("Plugin Integration/Yield to PickItV2")] public ToggleNode YieldToPickItV2 { get; set; } = new ToggleNode(false);
    [Menu("Plugin Integration/PickItV2 Yield Timeout (ms)")] public RangeNode<int> PickItV2YieldTimeout { get; set; } = new RangeNode<int>(5000, 1000, 15000);
    [Menu("Plugin Integration/Enable Coroutine Monitoring")] public ToggleNode EnableCoroutineMonitoring { get; set; } = new ToggleNode(true);
    [Menu("Plugin Integration/Yield to ReAgent")] public ToggleNode YieldToReAgent { get; set; } = new ToggleNode(true);
    [Menu("Plugin Integration/ReAgent Yield Timeout (ms)")] public RangeNode<int> ReAgentYieldTimeout { get; set; } = new RangeNode<int>(2000, 500, 5000);
    [Menu("Plugin Integration/ReAgent Coordination Mode")] public ToggleNode ReAgentCoordinationMode { get; set; } = new ToggleNode(true);
    
    // Network Communication
    [Menu("Network Communication/Enable Network Communication")] public ToggleNode EnableNetworkCommunication { get; set; } = new ToggleNode(false);
    [Menu("Network Communication/Enable Auto Discovery")] public ToggleNode EnableAutoDiscovery { get; set; } = new ToggleNode(true);
    [Menu("Network Communication/Leader IP Address")] public TextNode LeaderIpAddress { get; set; } = new TextNode("");
    [Menu("Network Communication/Leader Port")] public RangeNode<int> LeaderPort { get; set; } = new RangeNode<int>(7777, 1024, 65535);
    [Menu("Network Communication/Discovery Port")] public RangeNode<int> DiscoveryPort { get; set; } = new RangeNode<int>(7778, 1024, 65535);
    [Menu("Network Communication/Connection Retry Interval (ms)")] public RangeNode<int> ConnectionRetryInterval { get; set; } = new RangeNode<int>(5000, 1000, 30000);
    [Menu("Network Communication/Heartbeat Interval (ms)")] public RangeNode<int> HeartbeatInterval { get; set; } = new RangeNode<int>(10000, 5000, 60000);
    
    // Commands & Execution
    [Menu("Commands & Execution/Enable Leader Commands")] public ToggleNode EnableLeaderCommands { get; set; } = new ToggleNode(true);
    [Menu("Commands & Execution/Leader Commands Check Interval (ms)")] public RangeNode<int> LeaderCommandsCheckInterval { get; set; } = new RangeNode<int>(1000, 500, 5000);
    [Menu("Commands & Execution/Execute Commands While Following")] public ToggleNode ExecuteCommandsWhileFollowing { get; set; } = new ToggleNode(true);
    [Menu("Commands & Execution/Max Command Execution Time (ms)")] public RangeNode<int> MaxCommandExecutionTime { get; set; } = new RangeNode<int>(60000, 10000, 300000);
    [Menu("Commands & Execution/Enable Stashing Commands")] public ToggleNode EnableStashingCommands { get; set; } = new ToggleNode(true);
    [Menu("Commands & Execution/Stash Command Key")] public HotkeyNode StashCommandKey { get; set; } = Keys.F10;
    [Menu("Commands & Execution/Stash Tab Switch Delay (ms)")] public RangeNode<int> StashTabSwitchDelay { get; set; } = new RangeNode<int>(200, 50, 1000);
    [Menu("Commands & Execution/Item Placement Delay (ms)")] public RangeNode<int> ItemPlacementDelay { get; set; } = new RangeNode<int>(100, 50, 500);
    [Menu("Commands & Execution/Enable Selling Commands")] public ToggleNode EnableSellingCommands { get; set; } = new ToggleNode(true);
    [Menu("Commands & Execution/Sell Command Key")] public HotkeyNode SellCommandKey { get; set; } = Keys.F11;
    [Menu("Commands & Execution/Vendor Interaction Delay (ms)")] public RangeNode<int> VendorInteractionDelay { get; set; } = new RangeNode<int>(300, 100, 1000);
    [Menu("Commands & Execution/Item Sell Delay (ms)")] public RangeNode<int> ItemSellDelay { get; set; } = new RangeNode<int>(150, 50, 500);
    [Menu("Commands & Execution/Enable Trading Commands")] public ToggleNode EnableTradingCommands { get; set; } = new ToggleNode(true);
    [Menu("Commands & Execution/Trade Accept Key")] public HotkeyNode TradeAcceptKey { get; set; } = Keys.F12;
    [Menu("Commands & Execution/Trade Request Timeout (ms)")] public RangeNode<int> TradeRequestTimeout { get; set; } = new RangeNode<int>(15000, 5000, 60000);
    [Menu("Commands & Execution/Trade Window Delay (ms)")] public RangeNode<int> TradeWindowDelay { get; set; } = new RangeNode<int>(500, 100, 2000);
    
    // Security
    [Menu("Security/Enable Security Features")] public ToggleNode EnableSecurityFeatures { get; set; } = new ToggleNode(true);
    [Menu("Security/Enable Message Encryption")] public ToggleNode EnableMessageEncryption { get; set; } = new ToggleNode(true);
    [Menu("Security/Enable Authentication")] public ToggleNode EnableAuthentication { get; set; } = new ToggleNode(true);
    [Menu("Security/API Key")] public TextNode ApiKey { get; set; } = new TextNode("");
    [Menu("Security/Max Message Age (ms)")] public RangeNode<int> MaxMessageAge { get; set; } = new RangeNode<int>(30000, 10000, 300000);
    [Menu("Security/Security Audit Logging")] public ToggleNode SecurityAuditLogging { get; set; } = new ToggleNode(true);
    
    // Monitoring & Performance
    [Menu("Monitoring & Performance/Enable Performance Monitoring")] public ToggleNode EnablePerformanceMonitoring { get; set; } = new ToggleNode(true);
    [Menu("Monitoring & Performance/Enable Health Checks")] public ToggleNode EnableHealthChecks { get; set; } = new ToggleNode(true);
    [Menu("Monitoring & Performance/Health Check Interval (ms)")] public RangeNode<int> HealthCheckInterval { get; set; } = new RangeNode<int>(30000, 10000, 300000);
    [Menu("Monitoring & Performance/Metrics Collection Interval (ms)")] public RangeNode<int> MetricsCollectionInterval { get; set; } = new RangeNode<int>(30000, 10000, 300000);
    [Menu("Monitoring & Performance/Enable Structured Logging")] public ToggleNode EnableStructuredLogging { get; set; } = new ToggleNode(true);
    [Menu("Monitoring & Performance/Log Level")] public RangeNode<int> LogLevel { get; set; } = new RangeNode<int>(1, 0, 4); // 0=Debug, 1=Info, 2=Warning, 3=Error, 4=Critical
    
    // Reliability & Recovery
    [Menu("Reliability & Recovery/Enable Circuit Breakers")] public ToggleNode EnableCircuitBreakers { get; set; } = new ToggleNode(true);
    [Menu("Reliability & Recovery/Circuit Breaker Failure Threshold")] public RangeNode<int> CircuitBreakerFailureThreshold { get; set; } = new RangeNode<int>(5, 2, 20);
    [Menu("Reliability & Recovery/Circuit Breaker Timeout (ms)")] public RangeNode<int> CircuitBreakerTimeout { get; set; } = new RangeNode<int>(60000, 10000, 300000);
    [Menu("Reliability & Recovery/Circuit Breaker Reset Attempts")] public RangeNode<int> CircuitBreakerResetAttempts { get; set; } = new RangeNode<int>(3, 1, 10);
    
    // Protocol & Messaging
    [Menu("Protocol & Messaging/Enable Enhanced Protocol")] public ToggleNode EnableEnhancedProtocol { get; set; } = new ToggleNode(true);
    [Menu("Protocol & Messaging/Protocol Version")] public RangeNode<int> ProtocolVersion { get; set; } = new RangeNode<int>(2, 1, 2);
    [Menu("Protocol & Messaging/Message Deduplication")] public ToggleNode EnableMessageDeduplication { get; set; } = new ToggleNode(true);
    [Menu("Protocol & Messaging/Message Ordering")] public ToggleNode EnableMessageOrdering { get; set; } = new ToggleNode(true);
    [Menu("Protocol & Messaging/Message Prioritization")] public ToggleNode EnableMessagePrioritization { get; set; } = new ToggleNode(true);
    [Menu("Protocol & Messaging/Message Compression")] public ToggleNode EnableMessageCompression { get; set; } = new ToggleNode(false);
    
    // Auto-Configuration
    [Menu("Auto-Configuration/Enable Auto Configuration")] public ToggleNode EnableAutoConfiguration { get; set; } = new ToggleNode(true);
    [Menu("Auto-Configuration/Auto Optimize Performance")] public ToggleNode AutoOptimizePerformance { get; set; } = new ToggleNode(true);
    [Menu("Auto-Configuration/Auto Detect Network")] public ToggleNode AutoDetectNetwork { get; set; } = new ToggleNode(true);
    [Menu("Auto-Configuration/Configuration Validation")] public ToggleNode EnableConfigurationValidation { get; set; } = new ToggleNode(true);
    
    // Debug & Visualization
    [Menu("Debug & Visualization/Show PathStatus Debug")] public ToggleNode ShowPathStatusDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug & Visualization/Show Terrain Visualization")] public ToggleNode ShowTerrainVisualization { get; set; } = new ToggleNode(false);
    [Menu("Debug & Visualization/Show Task Debug")] public ToggleNode ShowTaskDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug & Visualization/Show Entity Debug")] public ToggleNode ShowEntityDebug { get; set; } = new ToggleNode(false);
    [Menu("Debug & Visualization/Show Raycast Debug")] public ToggleNode ShowRaycastDebug { get; set; } = new ToggleNode(false);
}