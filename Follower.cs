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
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using ExileCore.Shared;
using Newtonsoft.Json;

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
    
    // ReAgent coordination
    private DateTime _reAgentYieldStartTime = DateTime.MinValue;
    private DateTime _lastReAgentActionTime = DateTime.MinValue;
    
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
    
    // Leader commands variables
    private DateTime _lastCommandCheckTime = DateTime.MinValue;
    private bool _isExecutingCommand = false;
    private string _currentCommand = null;
    private DateTime _commandStartTime = DateTime.MinValue;
    private List<string> _processedCommands = new List<string>();
    
    // Network communication variables
    private TcpClient _tcpClient;
    private UdpClient _discoveryListener;
    private NetworkStream _networkStream;
    private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
    private Task _networkTask;
    private Task _discoveryTask;
    private bool _isConnectedToLeader = false;
    private string _connectedLeaderName = "";
    private string _leaderIpAddress = "";
    private int _leaderPort = 7777;
    private DateTime _lastConnectionAttempt = DateTime.MinValue;
    private DateTime _lastHeartbeat = DateTime.MinValue;
    private readonly object _networkLock = new object();
    private Queue<CommandMessage> _pendingCommands = new Queue<CommandMessage>();
    
    // Advanced infrastructure components
    private NetworkSecurity _networkSecurity;
    private NetworkReliability _networkReliability;
    private NetworkLogger _networkLogger;
    private EnhancedMessageProtocol _messageProtocol;
    private CommandExecutor _commandExecutor;
    
    // Shared position system
    private SharedPositionManager _sharedPositionManager;
    private DateTime _lastSharedPositionCheck = DateTime.MinValue;
    private Vector3 _sharedPositionFallback = Vector3.Zero;
    
    // Smart UI Avoidance system
    private SmartUIAvoidance _smartUIAvoidance;
    
    // Advanced Pathfinding system
    private AdvancedPathFinder _advancedPathFinder;
    private List<Vector2> _currentPath = new List<Vector2>();
    private DateTime _lastPathUpdate = DateTime.MinValue;

    public override bool Initialise()
    {
        Name = "Follower";
        Input.RegisterKey(Settings.MovementKey.Value);

        Input.RegisterKey(Settings.ToggleFollower.Value);
        Settings.ToggleFollower.OnValueChanged += () => { Input.RegisterKey(Settings.ToggleFollower.Value); };

        // Initialize advanced infrastructure components
        InitializeAdvancedInfrastructure();

        // Initialize PluginBridge for shared utilities
        InitializeSharedUtilities();
        
        // Initialize shared position manager
        InitializeSharedPositionManager();
        
        // Initialize Smart UI Avoidance system
        InitializeSmartUIAvoidance();
        
        // Initialize Advanced Pathfinding system
        InitializeAdvancedPathfinding();

        // Start network services if enabled
        if (Settings.EnableNetworkCommunication.Value)
        {
            StartNetworkServices();
        }

        StartFollowerCoroutine();
        return base.Initialise();
    }
    
    /// <summary>
    /// Initializes the advanced infrastructure components for production-ready operation
    /// </summary>
    private void InitializeAdvancedInfrastructure()
    {
        try
        {
            // Generate unique connection ID
            var connectionId = $"follower-{Environment.MachineName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
            
            // Initialize network logger
            _networkLogger = new NetworkLogger(connectionId);
            _networkLogger.LogNetworkEvent("INITIALIZATION", new { Component = "NetworkLogger", Status = "Started" });
            
            // Initialize network security with API key
            var apiKey = GenerateSecureApiKey();
            _networkSecurity = new NetworkSecurity(apiKey);
            _networkLogger.LogNetworkEvent("INITIALIZATION", new { Component = "NetworkSecurity", Status = "Started" });
            
            // Initialize network reliability
            _networkReliability = new NetworkReliability();
            _networkLogger.LogNetworkEvent("INITIALIZATION", new { Component = "NetworkReliability", Status = "Started" });
            
            // Initialize enhanced message protocol
            _messageProtocol = new EnhancedMessageProtocol(connectionId);
            _networkLogger.LogNetworkEvent("INITIALIZATION", new { Component = "EnhancedMessageProtocol", Status = "Started" });
            
            // Initialize command executor
            _commandExecutor = new CommandExecutor(GameController, Settings, _networkLogger, _networkReliability);
            _networkLogger.LogNetworkEvent("INITIALIZATION", new { Component = "CommandExecutor", Status = "Started" });
            
            // Set up event handlers
            SetupEventHandlers();
            
            _networkLogger.LogNetworkEvent("INITIALIZATION", new { Status = "All components initialized successfully" });
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to initialize advanced infrastructure: {ex.Message}", 1);
            throw;
        }
    }
    
    /// <summary>
    /// Sets up event handlers for advanced components
    /// </summary>
    private void SetupEventHandlers()
    {
        // Network logger events
        _networkLogger.LogEvent += (sender, args) => {
            if (args.LogEntry.Level >= LogLevel.Error)
            {
                LogMessage($"Network Error: {args.LogEntry.EventType} - {args.LogEntry.Data}", 1);
            }
        };
        
        _networkLogger.MetricsReported += (sender, args) => {
            var healthStatus = _networkLogger.GetHealthStatus();
            if (!healthStatus.IsHealthy)
            {
                LogMessage($"Network health degraded: {healthStatus.ErrorsLast5Minutes} errors in last 5 minutes", 2);
            }
        };
        
        // Message protocol events
        _messageProtocol.MessageReceived += (sender, args) => {
            _networkLogger.LogNetworkEvent("MESSAGE_RECEIVED", new { 
                MessageId = args.Message.MessageId,
                MessageType = args.Message.MessageType,
                SenderId = args.Message.SenderId
            });
        };
        
        _messageProtocol.MessageError += (sender, args) => {
            _networkLogger.LogError($"Message protocol error: {args.Error.Message}", args.Error, "message_protocol");
        };
    }
    
    /// <summary>
    /// Generates a secure API key for network communication
    /// </summary>
    private string GenerateSecureApiKey()
    {
        // Generate a secure API key based on machine and user info
        var machineInfo = $"{Environment.MachineName}-{Environment.UserName}";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var random = Guid.NewGuid().ToString("N");
        
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{machineInfo}-{timestamp}-{random}"));
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

    private void StartNetworkServices()
    {
        try
        {
            // Start auto-discovery listener if enabled
            if (Settings.EnableAutoDiscovery.Value)
            {
                _discoveryTask = Task.Run(() => StartDiscoveryListener(_cancellationTokenSource.Token));
            }
            
            // Start TCP client connection
            _networkTask = Task.Run(() => ConnectToLeader(_cancellationTokenSource.Token));
            
            LogMessage("Network services started successfully", 4);
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to start network services: {ex.Message}", 1);
        }
    }

    private async Task StartDiscoveryListener(CancellationToken cancellationToken)
    {
        try
        {
            _discoveryListener = new UdpClient(Settings.DiscoveryPort.Value);
            
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var result = await _discoveryListener.ReceiveAsync();
                    var message = Encoding.UTF8.GetString(result.Buffer);
                    
                    var discoveryMessage = JsonConvert.DeserializeObject<DiscoveryMessage>(message);
                    
                    if (discoveryMessage?.Type == "LEADER_DISCOVERY")
                    {
                        // Check if this is a leader we want to follow
                        if (ShouldFollowLeader(discoveryMessage.LeaderName))
                        {
                            lock (_networkLock)
                            {
                                _leaderIpAddress = discoveryMessage.IpAddress;
                                _leaderPort = discoveryMessage.Port;
                            }
                            
                            LogMessage($"Discovered leader '{discoveryMessage.LeaderName}' at {discoveryMessage.IpAddress}:{discoveryMessage.Port}", 4);
                            
                            // Try to connect if not already connected
                            if (!_isConnectedToLeader)
                            {
                                _ = Task.Run(() => ConnectToLeader(_cancellationTokenSource.Token));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogMessage($"Error in discovery listener: {ex.Message}", 1);
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Discovery listener error: {ex.Message}", 1);
        }
    }

    private bool ShouldFollowLeader(string leaderName)
    {
        if (string.IsNullOrEmpty(leaderName))
            return false;
            
        // Check if auto-discovery is enabled
        if (!Settings.EnableAutoDiscovery.Value)
            return false;
            
        // Check if the leader name matches any of our target leaders
        var leaderNames = Settings.LeaderNames.Value.Split(',')
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
            
        return leaderNames.Any(name => string.Equals(name, leaderName, StringComparison.OrdinalIgnoreCase));
    }

    private async Task ConnectToLeader(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_isConnectedToLeader)
                {
                    await Task.Delay(5000, cancellationToken); // Check connection every 5 seconds
                    continue;
                }
                
                // Don't spam connection attempts
                if (DateTime.Now - _lastConnectionAttempt < TimeSpan.FromSeconds(5))
                {
                    await Task.Delay(1000, cancellationToken);
                    continue;
                }
                
                _lastConnectionAttempt = DateTime.Now;
                
                string ipAddress;
                int port;
                
                lock (_networkLock)
                {
                    ipAddress = !string.IsNullOrEmpty(_leaderIpAddress) ? _leaderIpAddress : Settings.LeaderIpAddress.Value;
                    port = _leaderPort > 0 ? _leaderPort : Settings.LeaderPort.Value;
                }
                
                if (string.IsNullOrEmpty(ipAddress) || port <= 0)
                {
                    await Task.Delay(5000, cancellationToken);
                    continue;
                }
                
                LogMessage($"Attempting to connect to leader at {ipAddress}:{port}", 4);
                
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(ipAddress, port);
                
                _networkStream = _tcpClient.GetStream();
                _isConnectedToLeader = true;
                _connectedLeaderName = GetConnectedLeaderName();
                
                LogMessage($"Connected to leader at {ipAddress}:{port}", 4);
                
                // Send initial connection message
                await SendMessageToLeader(new FollowerMessage
                {
                    Type = "CONNECTED",
                    Data = GameController?.Game?.IngameState?.Data?.LocalPlayer?.GetComponent<Player>()?.PlayerName ?? "Unknown",
                    Timestamp = DateTime.Now
                });
                
                // Start listening for messages
                _ = Task.Run(() => ListenForMessages(cancellationToken));
                
                // Start heartbeat
                _ = Task.Run(() => SendHeartbeat(cancellationToken));
            }
            catch (Exception ex)
            {
                LogMessage($"Failed to connect to leader: {ex.Message}", 1);
                _isConnectedToLeader = false;
                _tcpClient?.Close();
                await Task.Delay(5000, cancellationToken);
            }
        }
    }

    private async Task ListenForMessages(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        
        try
        {
            while (!cancellationToken.IsCancellationRequested && _isConnectedToLeader && _networkStream != null)
            {
                var bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                
                if (bytesRead == 0)
                {
                    // Connection closed
                    break;
                }
                
                var message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                await ProcessLeaderMessage(message);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error listening for messages: {ex.Message}", 1);
        }
        finally
        {
            _isConnectedToLeader = false;
            _tcpClient?.Close();
        }
    }

    private async Task ProcessLeaderMessage(string message)
    {
        try
        {
            var commandMessage = JsonConvert.DeserializeObject<CommandMessage>(message);
            
            if (commandMessage?.Type == "COMMAND")
            {
                lock (_networkLock)
                {
                    _pendingCommands.Enqueue(commandMessage);
                }
                
                LogMessage($"Received command: {commandMessage.Command}", 4);
                
                // Send acknowledgment
                await SendMessageToLeader(new FollowerMessage
                {
                    Type = "COMMAND_RECEIVED",
                    Data = commandMessage.Command,
                    Timestamp = DateTime.Now
                });
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error processing leader message: {ex.Message}", 1);
        }
    }

    private async Task SendMessageToLeader(FollowerMessage message)
    {
        try
        {
            if (_networkStream == null || !_isConnectedToLeader)
                return;
                
            var jsonMessage = JsonConvert.SerializeObject(message);
            var data = Encoding.UTF8.GetBytes(jsonMessage);
            
            await _networkStream.WriteAsync(data, 0, data.Length);
            await _networkStream.FlushAsync();
        }
        catch (Exception ex)
        {
            LogMessage($"Error sending message to leader: {ex.Message}", 1);
            _isConnectedToLeader = false;
        }
    }

    private async Task SendHeartbeat(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _isConnectedToLeader)
            {
                await Task.Delay(10000, cancellationToken); // Send heartbeat every 10 seconds
                
                await SendMessageToLeader(new FollowerMessage
                {
                    Type = "HEARTBEAT",
                    Data = "alive",
                    Timestamp = DateTime.Now
                });
                
                _lastHeartbeat = DateTime.Now;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Heartbeat error: {ex.Message}", 1);
        }
    }

    private string GetConnectedLeaderName()
    {
        // Try to get the leader name from the connected leaders
        var leaderNames = Settings.LeaderNames.Value.Split(',')
            .Select(name => name.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();
            
        return leaderNames.FirstOrDefault() ?? "Unknown";
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
    /// Checks if PickItV2 is active and we should yield control to it
    /// </summary>
    private bool IsPickItV2Active()
    {
        if (!Settings.YieldToPickItV2.Value)
            return false;

        try
        {
            // Check if PickItV2 plugin is loaded
            // Use the existing plugin bridge approach instead of PluginManager
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
            
            return false;
        }
        catch (Exception ex)
        {
            // If we can't check PickItV2 state, assume it's not active
            _pickItV2YieldStartTime = DateTime.MinValue;
            return false;
        }
    }

    /// <summary>
    /// Checks if ReAgent is active and we should yield control to it
    /// </summary>
    private bool IsReAgentActive()
    {
        if (!Settings.YieldToReAgent.Value)
            return false;

        try
        {
            // First try to use the plugin bridge method for more reliable communication
            var reAgentIsActiveMethod = GameController.PluginBridge.GetMethod<Func<bool>>("ReAgent.IsActive");
            if (reAgentIsActiveMethod != null)
            {
                bool isActive = reAgentIsActiveMethod();
                
                if (isActive)
                {
                    // ReAgent is actively processing, yield control
                    if (_reAgentYieldStartTime == DateTime.MinValue)
                        _reAgentYieldStartTime = DateTime.Now;
                    
                    // Check timeout
                    if (DateTime.Now - _reAgentYieldStartTime > TimeSpan.FromMilliseconds(Settings.ReAgentYieldTimeout.Value))
                    {
                        _reAgentYieldStartTime = DateTime.MinValue;
                        return false; // Timeout reached, resume follower
                    }
                    
                    return true;
                }
                else
                {
                    _reAgentYieldStartTime = DateTime.MinValue;
                    return false;
                }
            }
            
            // Return false if plugin bridge is not available
            return false;
        }
        catch (Exception ex)
        {
            // If we can't check ReAgent state, assume it's not active
            _reAgentYieldStartTime = DateTime.MinValue;
            return false;
        }
    }

    /// <summary>
    /// Checks if either plugin is active and we should yield control
    /// </summary>
    private bool ShouldYieldToOtherPlugins()
    {
        return IsPickItV2Active() || IsReAgentActive();
    }

    /// <summary>
    /// Executes a mouse action only if PickItV2 is not currently active
    /// </summary>
    /// <param name="mouseAction">The mouse action to execute</param>
    /// <returns>True if the action was executed, false if yielded to PickItV2</returns>
    private bool ExecuteMouseActionIfPossible(Action mouseAction)
    {
        if (ShouldYieldToOtherPlugins())
        {
            // PickItV2 or ReAgent is active, yield control and delay our next action
            _nextBotAction = DateTime.Now.AddMilliseconds(Settings.BotInputFrequency.Value);
            return false;
        }
        
        // PickItV2 or ReAgent is not active, execute the mouse action
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
                    CheckInventoryManagement();
                    
                    // Check and execute leader commands
                    CheckLeaderCommands();
                    
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
        
        // Monitor network health and advanced infrastructure
        MonitorAdvancedInfrastructure();
        
        return null;
    }
    
    /// <summary>
    /// Monitors advanced infrastructure health and performance
    /// </summary>
    private void MonitorAdvancedInfrastructure()
    {
        try
        {
            // Check network health every 30 seconds
            var now = DateTime.UtcNow;
            if (_networkLogger != null && (now - _lastHeartbeat).TotalSeconds > 30)
            {
                _lastHeartbeat = now;
                
                // Get health status
                var healthStatus = _networkLogger.GetHealthStatus();
                
                // Log health metrics
                _networkLogger.RecordMetric("health.check", 1);
                _networkLogger.RecordMetric("health.score", healthStatus.IsHealthy ? 1 : 0);
                
                // Check for network reliability issues
                if (_networkReliability != null)
                {
                    var reliabilityReport = _networkReliability.GetHealthReport();
                    if (reliabilityReport.IsUnhealthy)
                    {
                        _networkLogger.LogWarning("Network reliability degraded", new { 
                            HealthScore = reliabilityReport.OverallHealthScore,
                            TotalFailures = reliabilityReport.TotalFailures,
                            TotalOperations = reliabilityReport.TotalOperations
                        });
                    }
                }
                
                // Check message protocol statistics
                if (_messageProtocol != null)
                {
                    var protocolStats = _messageProtocol.GetStatistics();
                    _networkLogger.RecordMetric("protocol.processed_messages", protocolStats.ProcessedMessagesCount);
                    _networkLogger.RecordMetric("protocol.pending_messages", protocolStats.PendingMessagesCount);
                    _networkLogger.RecordMetric("protocol.incoming_queue", protocolStats.IncomingQueueSize);
                    _networkLogger.RecordMetric("protocol.outgoing_queue", protocolStats.OutgoingQueueSize);
                }
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error monitoring advanced infrastructure: {ex.Message}", 1);
        }
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
            
            // Check for gems that need leveling
            CheckAndLevelGems(distanceFromFollower);
        }
        // Leader is null but we have tracked them this map.
        // Try using transition to follow them to their map
        else if (_tasks.Count == 0 && _lastTargetPosition != Vector3.Zero)
        {
            HandleMissingLeader();
        }
        // Leader entity not found, try shared position fallback
        else if (_tasks.Count == 0)
        {
            var sharedPosition = GetSharedPositionFallback();
            if (sharedPosition != Vector3.Zero)
            {
                // Use shared position as fallback leader position
                var distanceToSharedPos = Vector3.Distance(GameController.Player.Pos, sharedPosition);
                
                // Only use if it's a reasonable distance (not too far)
                if (distanceToSharedPos < Settings.ClearPathDistance.Value * 2)
                {
                    _tasks.Add(new TaskNode(sharedPosition, Settings.PathfindingNodeDistance));
                    LogMessage($"Using shared position fallback: distance {distanceToSharedPos:F0}", 4);
                }
            }
            else
            {
                // Check for gems even when leader is not present (when stopped)
                CheckAndLevelGems(float.MaxValue);
            }
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
        
        // Use advanced pathfinding if enabled
        if (Settings.EnableAdvancedPathfinding.Value && _advancedPathFinder != null)
        {
            UpdateAdvancedPath();
        }
        else
        {
            // Fallback to basic task-based pathfinding
            HandleDistantLeaderBasic();
        }
    }
    
    /// <summary>
    /// Updates the advanced pathfinding path to the leader
    /// </summary>
    private void UpdateAdvancedPath()
    {
        var now = DateTime.Now;
        var playerPos = GameController.Player.Pos;
        var leaderPos = _followTarget.Pos;
        
        // Check if we need to recalculate the path
        bool shouldRecalculatePath = false;
        
        // Time-based recalculation
        if (now - _lastPathUpdate > TimeSpan.FromMilliseconds(Settings.PathUpdateFrequency.Value))
        {
            shouldRecalculatePath = true;
        }
        
        // Distance-based recalculation
        if (_currentPath.Count > 0)
        {
            var distanceToPath = Vector3.Distance(playerPos, new Vector3(_currentPath[0].X, _currentPath[0].Y, playerPos.Z));
            if (distanceToPath > Settings.RecalculatePathDistance.Value)
            {
                shouldRecalculatePath = true;
            }
        }
        
        // Empty path recalculation
        if (_currentPath.Count == 0)
        {
            shouldRecalculatePath = true;
        }
        
        // Recalculate path if needed
        if (shouldRecalculatePath)
        {
            try
            {
                var playerGridPos = playerPos.WorldToGrid();
                var leaderGridPos = leaderPos.WorldToGrid();
                
                var path = _advancedPathFinder.FindPath(
                    new Vector2(playerGridPos.X, playerGridPos.Y),
                    new Vector2(leaderGridPos.X, leaderGridPos.Y)
                );
                
                if (path != null && path.Count > 0)
                {
                    _currentPath = path;
                    _lastPathUpdate = now;
                    
                    // Clear existing tasks and add path nodes
                    _tasks.Clear();
                    
                    // Add path nodes as tasks, but limit to prevent overwhelming the system
                    var maxPathNodes = Math.Min(path.Count, 10);
                    for (int i = 0; i < maxPathNodes; i++)
                    {
                        var pathNode = path[i];
                        var worldPos = new Vector3(pathNode.X, pathNode.Y, playerPos.Z).GridToWorld();
                        _tasks.Add(new TaskNode(worldPos, Settings.PathfindingNodeDistance));
                    }
                }
                else
                {
                    // No path found, fallback to basic approach
                    HandleDistantLeaderBasic();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Advanced pathfinding error: {ex.Message}", 2);
                // Fallback to basic approach on error
                HandleDistantLeaderBasic();
            }
        }
    }
    
    /// <summary>
    /// Basic fallback pathfinding for distant leader
    /// </summary>
    private void HandleDistantLeaderBasic()
    {
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
        // More aggressive task clearing when leader is nearby - clear ALL movement-related tasks
        // This prevents the follower from continuing old paths when the leader is already close
        if (_tasks.Count > 0)
        {
            var tasksToRemove = new List<int>();
            for (var i = _tasks.Count - 1; i >= 0; i--)
            {
                var task = _tasks[i];
                if (task.Type == TaskNode.TaskNodeType.Movement || task.Type == TaskNode.TaskNodeType.Transition)
                {
                    // Always remove old movement tasks when leader is nearby
                    tasksToRemove.Add(i);
                }
                else if (task.Type == TaskNode.TaskNodeType.Loot)
                {
                    // Remove loot tasks that are far from current position (stale loot tasks)
                    var taskDistance = Vector3.Distance(GameController.Player.Pos, task.WorldPosition);
                    if (taskDistance > Settings.ClearPathDistance.Value)
                    {
                        tasksToRemove.Add(i);
                    }
                }
            }
            
            // Remove the identified tasks
            foreach (var index in tasksToRemove)
            {
                _tasks.RemoveAt(index);
            }
        }
        
        // Close follow logic - only add new movement task if we need to get closer
        if (Settings.IsCloseFollowEnabled.Value && distanceFromFollower >= Settings.PathfindingNodeDistance.Value)
        {
            // Check if we should avoid targeting the leader due to portal proximity
            if (IsInPortalAvoidanceGracePeriod() && IsPositionTooCloseToPortal(_followTarget.Pos))
            {
                // Leader is too close to portal during grace period, wait
                return;
            }
            
            // Only add a new movement task if we don't already have one targeting the leader's current position
            var hasRecentLeaderTask = _tasks.Any(t => t.Type == TaskNode.TaskNodeType.Movement && 
                Vector3.Distance(t.WorldPosition, _followTarget.Pos) < Settings.PathfindingNodeDistance.Value);
                
            if (!hasRecentLeaderTask)
            {
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
            
            // PRIORITY CHECK: If leader is now closer than current task, abandon current task
            if (_followTarget != null && currentTask.Type == TaskNode.TaskNodeType.Movement)
            {
                var leaderDistance = Vector3.Distance(GameController.Player.Pos, _followTarget.Pos);
                
                // If leader is significantly closer than the current task, abandon the task
                if (leaderDistance < taskDistance * 0.7f && leaderDistance < Settings.ClearPathDistance.Value)
                {
                    // Clear all movement tasks since leader is now closer
                    _tasks.RemoveAll(t => t.Type == TaskNode.TaskNodeType.Movement);
                    
                    // Add new task to go directly to leader
                    if (leaderDistance >= Settings.PathfindingNodeDistance.Value)
                    {
                        _tasks.Insert(0, new TaskNode(_followTarget.Pos, Settings.PathfindingNodeDistance));
                    }
                    
                    yield break; // Exit and restart with new task
                }
            }

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
                    // Check if PickItV2 or ReAgent is active before performing mouse actions
                    if (!ExecuteMouseActionIfPossible(() =>
                    {
                        Mouse.SetCursorPosHuman2(WorldToValidScreenPosition(currentTask.WorldPosition));
                        
                        // Add small delay to ensure mouse positioning is complete
                        System.Threading.Thread.Sleep(10);
                        
                        // Force a click if the setting is enabled to prevent getting stuck on UI elements
                        if (Settings.ForceClickDuringMovement.Value)
                        {
                            Mouse.LeftClick(1);
                        }
                        
                        Input.KeyDown(Settings.MovementKey);
                        Input.KeyUp(Settings.MovementKey);
                    }))
                    {
                        // PickItV2 or ReAgent is active, yielding control
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
                            // Check if PickItV2 or ReAgent is active before mouse actions
                            if (!ExecuteMouseActionIfPossible(() => MouseoverItem(questLoot)))
                            {
                                // PickItV2 or ReAgent is active, yielding control
                                yield break;
                            }
                        }
                        if (targetInfo.isTargeted)
                        {
                            // Check if PickItV2 or ReAgent is active before clicking
                            if (!ExecuteMouseActionIfPossible(() =>
                            {
                                Mouse.LeftMouseDown();
                                Mouse.LeftMouseUp();
                            }))
                            {
                                // PickItV2 or ReAgent is active, yielding control
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
                            // Check if PickItV2 or ReAgent is active before clicking transition
                            if (!ExecuteMouseActionIfPossible(() => Mouse.SetCursorPosAndLeftClickHuman(screenPos, 100)))
                            {
                                // PickItV2 or ReAgent is active, yielding control
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
                            // Check if PickItV2 or ReAgent is active before mouse movement
                            if (!ExecuteMouseActionIfPossible(() =>
                            {
                                Mouse.SetCursorPosHuman2(screenPos);
                                
                                // Add small delay to ensure mouse positioning is complete
                                System.Threading.Thread.Sleep(10);
                                
                                // Force a click if the setting is enabled to prevent getting stuck on UI elements
                                if (Settings.ForceClickDuringMovement.Value)
                                {
                                    Mouse.LeftClick(1);
                                }
                                
                                Input.KeyDown(Settings.MovementKey);
                                Input.KeyUp(Settings.MovementKey);
                            }))
                            {
                                // PickItV2 or ReAgent is active, yielding control
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
                            // Check if PickItV2 or ReAgent is active before clicking waypoint
                            if (!ExecuteMouseActionIfPossible(() => Mouse.SetCursorPosAndLeftClickHuman(screenPos, 100)))
                            {
                                // PickItV2 or ReAgent is active, yielding control
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
        
        // Recovery Strategy 1: Check if we're clicking on items and clear them
        if (IsLikelyClickingOnItems())
        {
            // Force a simple left click to clear any item interaction
            if (ExecuteMouseActionIfPossible(() =>
            {
                Mouse.LeftClick(25);
            }))
            {
                // Reset stuck detection timer to give item clearing time to work
                _stuckDetectionStartTime = DateTime.Now;
                return;
            }
        }
        
        // Recovery Strategy 2: Try aggressive dash if available
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
        
        // Recovery Strategy 3: Generate smart recovery position using UI avoidance
        var recoveryPos = GenerateSmartRecoveryPosition(currentPos);
        
        // Insert recovery movement at the beginning of task queue
        _tasks.Insert(0, new TaskNode(recoveryPos, Settings.PathfindingNodeDistance.Value, TaskNode.TaskNodeType.Movement));
        
        // IMMEDIATELY move mouse to recovery position and force execution
        // This ensures the mouse adjusts right away when stuck
        if (ExecuteMouseActionIfPossible(() =>
        {
            Mouse.SetCursorPosHuman2(WorldToValidScreenPosition(recoveryPos));
            
            // Add small delay and random offset to ensure reliable movement
            System.Threading.Thread.Sleep(15);
            
            Input.KeyDown(Settings.MovementKey);
            Input.KeyUp(Settings.MovementKey);
        }))
        {
            // Force immediate execution by resetting the action timer
            _nextBotAction = DateTime.Now;
        }
        
        // Reset stuck detection timer to give recovery time to work
        _stuckDetectionStartTime = DateTime.Now;
        
        // Attempting stuck recovery with item-avoiding movement
    }
    
    /// <summary>
    /// Detects if the player is likely clicking on items instead of moving
    /// </summary>
    private bool IsLikelyClickingOnItems()
    {
        try
        {
            // Check if there are many items on ground near the player
            var nearbyItems = GameController.EntityListWrapper.Entities
                .Where(e => e.Type == ExileCore.Shared.Enums.EntityType.WorldItem)
                .Where(e => e.IsTargetable && e.IsValid)
                .Count(e => Vector3.Distance(e.Pos, GameController.Player.Pos) < 150);
            
            // If there are many items nearby and we're stuck, likely clicking on items
            return nearbyItems > 5;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Generates a recovery position that avoids items on the ground
    /// </summary>
    private Vector3 GenerateItemAvoidancePosition(Vector3 currentPos)
    {
        try
        {
            // Get nearby items to avoid
            var nearbyItems = GameController.EntityListWrapper.Entities
                .Where(e => e.Type == ExileCore.Shared.Enums.EntityType.WorldItem)
                .Where(e => e.IsTargetable && e.IsValid)
                .Where(e => Vector3.Distance(e.Pos, currentPos) < 200)
                .ToList();
            
            // Try multiple random positions and pick the one farthest from items
            var bestPosition = currentPos;
            var bestDistance = 0f;
            
            for (int i = 0; i < 10; i++)
            {
                var randomOffset = new Vector3(
                    (float)(random.NextDouble() - 0.5) * 300,
                    (float)(random.NextDouble() - 0.5) * 300,
                    currentPos.Z
                );
                
                var candidatePos = currentPos + randomOffset;
                
                // Calculate minimum distance to any item
                var minItemDistance = nearbyItems.Count > 0 ? 
                    nearbyItems.Min(item => Vector3.Distance(item.Pos, candidatePos)) : 
                    float.MaxValue;
                
                if (minItemDistance > bestDistance)
                {
                    bestDistance = minItemDistance;
                    bestPosition = candidatePos;
                }
            }
            
            return bestPosition;
        }
        catch (Exception ex)
        {
            // Fallback to simple random position
            var randomOffset = new Vector3(
                (float)(random.NextDouble() - 0.5) * 200,
                (float)(random.NextDouble() - 0.5) * 200,
                currentPos.Z
            );
            return currentPos + randomOffset;
        }
    }
    
    /// <summary>
    /// Generates a smart recovery position using both item avoidance and UI avoidance
    /// </summary>
    private Vector3 GenerateSmartRecoveryPosition(Vector3 currentPos)
    {
        try
        {
            // First try Smart UI Avoidance if enabled
            if (Settings.EnableSmartUIAvoidance.Value && _smartUIAvoidance != null)
            {
                // Generate multiple candidate positions and pick the best one
                var bestPosition = currentPos;
                var bestScore = float.MinValue;
                
                for (int i = 0; i < 15; i++)
                {
                    var randomOffset = new Vector3(
                        (float)(random.NextDouble() - 0.5) * 400,
                        (float)(random.NextDouble() - 0.5) * 400,
                        currentPos.Z
                    );
                    
                    var candidatePos = currentPos + randomOffset;
                    var candidateScreenPos = WorldToValidScreenPosition(candidatePos);
                    
                    // Check if the screen position is safe (not on UI elements)
                    if (_smartUIAvoidance.IsPositionSafe(candidateScreenPos))
                    {
                        // Calculate score based on distance from items and other factors
                        var score = ScoreRecoveryPosition(candidatePos, currentPos);
                        
                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestPosition = candidatePos;
                        }
                    }
                }
                
                // If we found a good position, use it
                if (bestScore > float.MinValue)
                {
                    return bestPosition;
                }
            }
            
            // Fallback to basic item avoidance
            return GenerateItemAvoidancePosition(currentPos);
        }
        catch (Exception ex)
        {
            // Ultimate fallback
            return GenerateItemAvoidancePosition(currentPos);
        }
    }
    
    /// <summary>
    /// Scores a recovery position based on various factors
    /// </summary>
    private float ScoreRecoveryPosition(Vector3 candidatePos, Vector3 currentPos)
    {
        float score = 0f;
        
        try
        {
            // Factor 1: Distance from items (higher score for farther from items)
            var nearbyItems = GameController.EntityListWrapper.Entities
                .Where(e => e.Type == ExileCore.Shared.Enums.EntityType.WorldItem)
                .Where(e => e.IsTargetable && e.IsValid)
                .Where(e => Vector3.Distance(e.Pos, candidatePos) < 200)
                .ToList();
            
            if (nearbyItems.Count > 0)
            {
                var minItemDistance = nearbyItems.Min(item => Vector3.Distance(item.Pos, candidatePos));
                score += minItemDistance * 0.1f; // Convert to score
            }
            else
            {
                score += 100f; // Bonus for no nearby items
            }
            
            // Factor 2: Reasonable distance from current position (not too far, not too close)
            var distanceFromCurrent = Vector3.Distance(candidatePos, currentPos);
            if (distanceFromCurrent > 50 && distanceFromCurrent < 300)
            {
                score += 50f;
            }
            
            // Factor 3: Avoid positions that are too close to walls/obstacles
            // This could be expanded with terrain checking
            
            return score;
        }
        catch (Exception ex)
        {
            return 0f;
        }
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
    /// Gets shared position fallback when entity detection fails
    /// </summary>
    private Vector3 GetSharedPositionFallback()
    {
        if (!Settings.EnableSharedPositionFallback.Value || _sharedPositionManager == null)
            return Vector3.Zero;
        
        var now = DateTime.Now;
        
        // Check at configured interval
        if (now - _lastSharedPositionCheck < TimeSpan.FromMilliseconds(Settings.SharedPositionCheckInterval.Value))
            return _sharedPositionFallback;
        
        _lastSharedPositionCheck = now;
        
        try
        {
            var positionData = _sharedPositionManager.ReadPosition();
            if (positionData != null)
            {
                // Check if position is fresh
                var maxAge = TimeSpan.FromSeconds(Settings.SharedPositionMaxAge.Value);
                if (positionData.IsFresh(maxAge))
                {
                    // Check if it's from the same area
                    var currentAreaName = GameController.Area.CurrentArea?.Name;
                    if (!string.IsNullOrEmpty(currentAreaName) && positionData.IsSameArea(currentAreaName))
                    {
                        _sharedPositionFallback = positionData.Position;
                        return _sharedPositionFallback;
                    }
                    else
                    {
                        // Different area, don't use this position
                        _sharedPositionFallback = Vector3.Zero;
                    }
                }
                else
                {
                    // Stale data, reset fallback
                    _sharedPositionFallback = Vector3.Zero;
                }
            }
            else
            {
                _sharedPositionFallback = Vector3.Zero;
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error reading shared position: {ex.Message}", 2);
            _sharedPositionFallback = Vector3.Zero;
        }
        
        return _sharedPositionFallback;
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
            var totalSlots = 60; // Standard inventory size (12x5)
            
            // Count non-empty slots
            for (int i = 0; i < inventory.Items.Count; i++)
            {
                if (inventory.Items[i] != null)
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
            
            // Reinitialize advanced pathfinding with updated terrain data
            if (Settings.EnableAdvancedPathfinding.Value)
            {
                try
                {
                    _advancedPathFinder = new AdvancedPathFinder(_tiles, GameController, Settings);
                    _currentPath.Clear();
                    _lastPathUpdate = DateTime.MinValue;
                    LogMessage("Advanced pathfinding reinitialized after terrain refresh", 5);
                }
                catch (Exception pathEx)
                {
                    LogMessage($"Failed to reinitialize advanced pathfinding: {pathEx.Message}", 2);
                }
            }
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
        
        // Show shared position fallback status
        if (Settings.EnableSharedPositionFallback.Value && _sharedPositionFallback != Vector3.Zero)
        {
            var sharedPosDistance = Vector3.Distance(GameController.Player.Pos, _sharedPositionFallback);
            var positionAge = _sharedPositionManager?.GetPositionAge() ?? TimeSpan.Zero;
            Graphics.DrawText($"SHARED POSITION: {sharedPosDistance:F0} units | Age: {positionAge.TotalSeconds:F1}s", 
                new Vector2(500, 300), SharpDX.Color.Cyan);
                
            // Draw shared position on screen
            var sharedPosScreen = WorldToValidScreenPosition(_sharedPositionFallback);
            Graphics.DrawText("SHARED POS", sharedPosScreen, SharpDX.Color.Cyan);
        }
        
        // Show Smart UI Avoidance status and debug rectangles
        if (Settings.EnableSmartUIAvoidance.Value && _smartUIAvoidance != null)
        {
            // Check if we're using relaxed constraints for distant targets
            var isUsingRelaxedConstraints = false;
            if (_followTarget != null)
            {
                var playerPos = GameController.Player.Pos;
                var distanceToLeader = Vector3.Distance(playerPos, _followTarget.Pos);
                isUsingRelaxedConstraints = distanceToLeader > Settings.DistantTargetThreshold.Value;
            }
            
            var statusText = isUsingRelaxedConstraints ? "SMART UI AVOIDANCE: RELAXED" : "SMART UI AVOIDANCE: ACTIVE";
            var statusColor = isUsingRelaxedConstraints ? SharpDX.Color.Orange : SharpDX.Color.LightGreen;
            Graphics.DrawText(statusText, new Vector2(500, 320), statusColor);
            
            // Draw UI debug rectangles if enabled
            if (Settings.ShowUIDebugRectangles.Value)
            {
                var uiElements = _smartUIAvoidance.GetUIElementsForDebug();
                foreach (var uiRect in uiElements)
                {
                    Graphics.DrawBox(uiRect, SharpDX.Color.Red, 2);
                }
                
                // Draw safe zone circle
                var windowRect = GameController.Window.GetWindowRectangle();
                var safeZoneCenter = new Vector2(windowRect.X + windowRect.Width / 2, windowRect.Y + windowRect.Height / 2);
                var smallestDimension = Math.Min(windowRect.Width, windowRect.Height);
                var safeZoneRadius = (smallestDimension / 2) * (Settings.MouseMovementAreaPercent.Value / 100.0f);
                
                // Draw safe zone outline (approximate with box)
                var safeZoneRect = new SharpDX.RectangleF(
                    safeZoneCenter.X - safeZoneRadius,
                    safeZoneCenter.Y - safeZoneRadius,
                    safeZoneRadius * 2,
                    safeZoneRadius * 2
                );
                Graphics.DrawBox(safeZoneRect, SharpDX.Color.Green, 2);
                
                Graphics.DrawText($"UI ELEMENTS: {uiElements.Count}", new Vector2(500, 340), SharpDX.Color.Yellow);
                
                if (isUsingRelaxedConstraints)
                {
                    Graphics.DrawText("USING RELAXED CONSTRAINTS", new Vector2(500, 360), SharpDX.Color.Orange);
                }
            }
        }
        else
        {
            Graphics.DrawText("SMART UI AVOIDANCE: DISABLED", new Vector2(500, 320), SharpDX.Color.Orange);
        }
        
        // Show Advanced Pathfinding status and debug paths
        if (Settings.EnableAdvancedPathfinding.Value && _advancedPathFinder != null)
        {
            var cacheInfo = _advancedPathFinder.GetCacheInfo();
            Graphics.DrawText($"ADVANCED PATHFINDING: ACTIVE | Cache: {cacheInfo.DistanceFields}D/{cacheInfo.DirectionFields}Dir", 
                new Vector2(500, 380), SharpDX.Color.Cyan);
            
            // Draw current path if debug is enabled
            if (Settings.ShowDebugPaths.Value && _currentPath != null && _currentPath.Count > 0)
            {
                var playerPos = GameController.Player.Pos;
                Vector2 lastPoint = default;
                
                foreach (var pathNode in _currentPath)
                {
                    var worldPos = new Vector3(pathNode.X, pathNode.Y, playerPos.Z).GridToWorld();
                    var screenPos = Camera.WorldToScreen(worldPos);
                    
                    // Draw path node
                    Graphics.DrawText("●", screenPos, SharpDX.Color.Yellow);
                    
                    // Draw line between path nodes
                    if (lastPoint != default)
                    {
                        // Simple line approximation using multiple points
                        var direction = screenPos - lastPoint;
                        var steps = (int)Math.Max(1, Vector2.Distance(screenPos, lastPoint) / 10);
                        
                        for (int i = 0; i <= steps; i++)
                        {
                            var t = i / (float)steps;
                            var linePoint = lastPoint + direction * t;
                            Graphics.DrawText(".", linePoint, SharpDX.Color.Yellow);
                        }
                    }
                    
                    lastPoint = screenPos;
                }
                
                Graphics.DrawText($"PATH NODES: {_currentPath.Count}", new Vector2(500, 400), SharpDX.Color.Yellow);
            }
        }
        else
        {
            Graphics.DrawText("ADVANCED PATHFINDING: DISABLED", new Vector2(500, 380), SharpDX.Color.Orange);
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
        
        // Show leader commands status
        if (Settings.EnableLeaderCommands.Value)
        {
            var commandStatus = _isExecutingCommand ? $"EXECUTING: {_currentCommand}" : "WAITING FOR COMMANDS";
            var commandColor = _isExecutingCommand ? SharpDX.Color.Orange : SharpDX.Color.LightBlue;
            Graphics.DrawText($"Leader Commands: {commandStatus}", new Vector2(500, 220), commandColor);
            
            if (_isExecutingCommand)
            {
                var executionTime = DateTime.Now - _commandStartTime;
                Graphics.DrawText($"Execution Time: {executionTime.TotalSeconds:F1}s", new Vector2(500, 240), SharpDX.Color.Yellow);
            }
        }
        
        // Show coroutine status if monitoring is enabled
        if (Settings.EnableCoroutineMonitoring.Value)
        {
            var yPos = Settings.EnableLeaderCommands.Value ? 260 : 220;
            
            var followerStatus = _followerCoroutine?.Running == true ? "RUNNING" : "STOPPED";
            var followerColor = _followerCoroutine?.Running == true ? SharpDX.Color.Green : SharpDX.Color.Red;
            Graphics.DrawText($"Follower Coroutine: {followerStatus}", new Vector2(500, yPos), followerColor);
            
            if (_postTransitionCoroutine != null)
            {
                var graceStatus = _postTransitionCoroutine.Running ? "ACTIVE" : "INACTIVE";
                var graceColor = _postTransitionCoroutine.Running ? SharpDX.Color.Yellow : SharpDX.Color.Gray;
                Graphics.DrawText($"Grace Period: {graceStatus}", new Vector2(500, yPos + 20), graceColor);
            }
            
            if (_isTransitioning)
            {
                Graphics.DrawText("TRANSITIONING", new Vector2(500, yPos + 40), SharpDX.Color.Cyan);
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
        // Use Smart UI Avoidance if enabled, otherwise fallback to basic method
        if (Settings.EnableSmartUIAvoidance.Value && _smartUIAvoidance != null)
        {
            // Check if we're following a distant leader
            var playerPos = GameController.Player.Pos;
            var distanceToTarget = Vector3.Distance(playerPos, worldPos);
            var isDistantTarget = distanceToTarget > Settings.DistantTargetThreshold.Value;
            
            // Also check if we're actively following the leader and they're far away
            var isFollowingDistantLeader = _followTarget != null && 
                Vector3.Distance(playerPos, _followTarget.Pos) > Settings.DistantTargetThreshold.Value &&
                Vector3.Distance(worldPos, _followTarget.Pos) < Settings.PathfindingNodeDistance.Value;
            
            return _smartUIAvoidance.GetSafeScreenPosition(worldPos, isDistantTarget || isFollowingDistantLeader);
        }
        else
        {
            // Fallback to basic method
            return GetBasicScreenPosition(worldPos);
        }
    }
    
    /// <summary>
    /// Basic screen position calculation (fallback when Smart UI Avoidance is disabled)
    /// </summary>
    private Vector2 GetBasicScreenPosition(Vector3 worldPos)
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
        
        var finalPos = new Vector2(constrainedX, constrainedY);
        
        // Check if the final position would click on an item and adjust if needed
        finalPos = AvoidClickingOnItems(finalPos, worldPos, windowRect);
        
        // Add small random offset to prevent clicking on exact same pixel repeatedly
        finalPos = AddRandomOffset(finalPos, windowRect);
        
        return finalPos;
    }
    
    /// <summary>
    /// Adds a small random offset to mouse position to prevent pixel-perfect clicking issues
    /// </summary>
    private Vector2 AddRandomOffset(Vector2 basePos, SharpDX.RectangleF windowRect)
    {
        try
        {
            // Add small random offset (±3 pixels) to prevent exact pixel clicking
            var offsetX = (float)(random.NextDouble() - 0.5) * 6;
            var offsetY = (float)(random.NextDouble() - 0.5) * 6;
            
            var adjustedPos = new Vector2(basePos.X + offsetX, basePos.Y + offsetY);
            
            // Ensure the adjusted position is still within bounds
            adjustedPos.X = Math.Max(windowRect.Left + 10, Math.Min(windowRect.Right - 10, adjustedPos.X));
            adjustedPos.Y = Math.Max(windowRect.Top + 10, Math.Min(windowRect.Bottom - 10, adjustedPos.Y));
            
            return adjustedPos;
        }
        catch (Exception ex)
        {
            return basePos;
        }
    }
    
    /// <summary>
    /// Adjusts mouse position to avoid clicking on items on the ground
    /// </summary>
    private Vector2 AvoidClickingOnItems(Vector2 screenPos, Vector3 targetWorldPos, SharpDX.RectangleF windowRect)
    {
        try
        {
            // Check for items on ground that might interfere with movement
            var nearbyItems = GameController.EntityListWrapper.Entities
                .Where(e => e.Type == ExileCore.Shared.Enums.EntityType.WorldItem)
                .Where(e => e.IsTargetable && e.IsValid)
                .Where(e => Vector3.Distance(e.Pos, targetWorldPos) < 100) // Only check nearby items
                .ToList();
            
            if (nearbyItems.Count == 0)
                return screenPos;
            
            // Get item labels that might be on screen
            var itemLabels = GameController.IngameState.IngameUi.ItemsOnGroundLabels
                .Where(label => label.IsVisible && label.ItemOnGround != null)
                .ToList();
            
            foreach (var label in itemLabels)
            {
                var labelRect = label.Label.GetClientRect();
                var labelCenter = labelRect.Center;
                var labelScreenPos = new Vector2(labelCenter.X, labelCenter.Y);
                
                // Check if our intended click position is too close to an item label
                var distance = Vector2.Distance(screenPos, labelScreenPos);
                if (distance < 30) // 30 pixel threshold
                {
                    // Adjust position to avoid the item
                    var direction = screenPos - labelScreenPos;
                    var length = (float)Math.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
                    var avoidanceDirection = length > 0 ? new Vector2(direction.X / length, direction.Y / length) : new Vector2(1, 0);
                    var adjustedPos = labelScreenPos + avoidanceDirection * 40; // Move 40 pixels away
                    
                    // Make sure adjusted position is still within game window
                    adjustedPos.X = Math.Max(windowRect.Left + 50, Math.Min(windowRect.Right - 50, adjustedPos.X));
                    adjustedPos.Y = Math.Max(windowRect.Top + 50, Math.Min(windowRect.Bottom - 50, adjustedPos.Y));
                    
                    return adjustedPos;
                }
            }
            
            return screenPos;
        }
        catch (Exception ex)
        {
            // If there's an error in item avoidance, return the original position
            return screenPos;
        }
    }
    
            /// <summary>
        /// Checks for and executes leader commands
        /// </summary>
        private void CheckLeaderCommands()
        {
            if (!Settings.EnableLeaderCommands.Value)
            {
                // Log once that commands are disabled (to help with troubleshooting)
                if (DateTime.Now - _lastCommandCheckTime > TimeSpan.FromMinutes(1))
                {
                    LogMessage("Leader commands are disabled in settings", 4);
                    _lastCommandCheckTime = DateTime.Now;
                }
                return;
            }
                
            var now = DateTime.Now;
            
            // Check if enough time has passed since last command check
            if (now - _lastCommandCheckTime < TimeSpan.FromMilliseconds(Settings.LeaderCommandsCheckInterval.Value))
                return;
                
            _lastCommandCheckTime = now;
            
            // If we're currently executing a command, check timeout
            if (_isExecutingCommand)
            {
                var executionTime = now - _commandStartTime;
                if (executionTime.TotalMilliseconds > Settings.MaxCommandExecutionTime.Value)
                {
                    // Command timeout - stop execution
                    _isExecutingCommand = false;
                    _currentCommand = null;
                    LogMessage($"Command execution timed out after {executionTime.TotalSeconds:F1}s", 2);
                }
                return; // Don't check for new commands while executing
            }
            
            try
            {
                // Check if we're connected to a leader
                if (!_isConnectedToLeader)
                    return;
                
                CommandMessage commandMessage = null;
                
                // Get pending commands from network queue
                lock (_networkLock)
                {
                    if (_pendingCommands.Count > 0)
                    {
                        commandMessage = _pendingCommands.Dequeue();
                    }
                }
                
                if (commandMessage == null)
                    return;
                    
                // Check if we should execute commands while following
                if (!Settings.ExecuteCommandsWhileFollowing.Value && _tasks.Count > 0)
                    return;
                    
                // Execute the command
                LogMessage($"Received leader command: {commandMessage.Command} - attempting to execute", 4);
                
                if (ExecuteLeaderCommand(commandMessage))
                {
                    _currentCommand = commandMessage.Command;
                    _isExecutingCommand = true;
                    _commandStartTime = now;
                    _processedCommands.Add(commandMessage.Command);
                    
                    LogMessage($"Successfully started executing leader command: {commandMessage.Command}", 4);
                    
                    // Send completion acknowledgment
                    _ = Task.Run(async () =>
                    {
                        await SendMessageToLeader(new FollowerMessage
                        {
                            Type = "COMMAND_STARTED",
                            Data = commandMessage.Command,
                            Timestamp = DateTime.Now
                        });
                    });
                }
                
                // Clean up processed commands list to prevent memory bloat
                if (_processedCommands.Count > 50)
                {
                    _processedCommands.RemoveRange(0, 25);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error checking leader commands: {ex.Message}", 1);
            }
        }
        
        /// <summary>
        /// Executes a leader command
        /// </summary>
        private bool ExecuteLeaderCommand(CommandMessage commandMessage)
        {
            try
            {
                return commandMessage.Command switch
                {
                    "STASH_ITEMS" => ExecuteStashCommand(commandMessage.Data),
                    "SELL_ITEMS" => ExecuteSellCommand(commandMessage.Data),
                    "ACCEPT_TRADE" => ExecuteTradeCommand(commandMessage.Data),
                    "EMERGENCY_STOP" => ExecuteEmergencyStopCommand(),
                    _ => false
                };
            }
            catch (Exception ex)
            {
                LogMessage($"Error executing command {commandMessage.Command}: {ex.Message}", 1);
                return false;
            }
        }
        
        /// <summary>
        /// Executes the stash items command using advanced command executor
        /// </summary>
        private bool ExecuteStashCommand(Dictionary<string, object> commandData)
        {
            if (!Settings.EnableStashingCommands.Value)
                return false;
                
            try
            {
                // Execute using advanced command executor
                var task = _commandExecutor.ExecuteStashCommand(commandData);
                
                // Handle async execution
                task.ContinueWith(t => {
                    if (t.IsCompletedSuccessfully)
                    {
                        var result = t.Result;
                        if (result.IsSuccess)
                        {
                            LogMessage($"Stash command completed successfully: {result.Message}", 4);
                        }
                        else
                        {
                            LogMessage($"Stash command failed: {result.ErrorMessage}", 2);
                        }
                    }
                    else
                    {
                        LogMessage($"Stash command error: {t.Exception?.GetBaseException().Message}", 1);
                    }
                }, TaskScheduler.Default);
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in stash command: {ex.Message}", 1);
                _networkLogger?.LogError($"Stash command error: {ex.Message}", ex, "stash_command");
                return false;
            }
        }
        
        /// <summary>
        /// Executes the sell items command using advanced command executor
        /// </summary>
        private bool ExecuteSellCommand(Dictionary<string, object> commandData)
        {
            if (!Settings.EnableSellingCommands.Value)
                return false;
                
            try
            {
                // Execute using advanced command executor
                var task = _commandExecutor.ExecuteSellCommand(commandData);
                
                // Handle async execution
                task.ContinueWith(t => {
                    if (t.IsCompletedSuccessfully)
                    {
                        var result = t.Result;
                        if (result.IsSuccess)
                        {
                            LogMessage($"Sell command completed successfully: {result.Message}", 4);
                        }
                        else
                        {
                            LogMessage($"Sell command failed: {result.ErrorMessage}", 2);
                        }
                    }
                    else
                    {
                        LogMessage($"Sell command error: {t.Exception?.GetBaseException().Message}", 1);
                    }
                }, TaskScheduler.Default);
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in sell command: {ex.Message}", 1);
                _networkLogger?.LogError($"Sell command error: {ex.Message}", ex, "sell_command");
                return false;
            }
        }
        
        /// <summary>
        /// Executes the accept trade command using advanced command executor
        /// </summary>
        private bool ExecuteTradeCommand(Dictionary<string, object> commandData)
        {
            if (!Settings.EnableTradingCommands.Value)
                return false;
                
            try
            {
                // Execute using advanced command executor
                var task = _commandExecutor.ExecuteTradeCommand(commandData);
                
                // Handle async execution
                task.ContinueWith(t => {
                    if (t.IsCompletedSuccessfully)
                    {
                        var result = t.Result;
                        if (result.IsSuccess)
                        {
                            LogMessage($"Trade command completed successfully: {result.Message}", 4);
                        }
                        else
                        {
                            LogMessage($"Trade command failed: {result.ErrorMessage}", 2);
                        }
                    }
                    else
                    {
                        LogMessage($"Trade command error: {t.Exception?.GetBaseException().Message}", 1);
                    }
                }, TaskScheduler.Default);
                
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in trade command: {ex.Message}", 1);
                _networkLogger?.LogError($"Trade command error: {ex.Message}", ex, "trade_command");
                return false;
            }
        }
        
        /// <summary>
        /// Executes the emergency stop command
        /// </summary>
        private bool ExecuteEmergencyStopCommand()
        {
            try
            {
                // Stop all current activities
                _tasks.Clear();
                _isExecutingCommand = false;
                _currentCommand = null;
                _processedCommands.Clear();
                
                // Stop following temporarily
                Settings.IsFollowEnabled.SetValueNoEvent(false);
                
                LogMessage("Emergency stop executed - all activities halted", 1);
                return true;
            }
            catch (Exception ex)
            {
                LogMessage($"Error in emergency stop: {ex.Message}", 1);
                return false;
            }
        }
        
        public override void OnClose()
        {
            // Stop all coroutines
            _followerCoroutine?.Done();
            _postTransitionCoroutine?.Done();
            
            base.OnClose();
        }
        
        public override void Dispose()
        {
            try
            {
                _cancellationTokenSource?.Cancel();
                
                _tcpClient?.Close();
                _discoveryListener?.Close();
                _networkStream?.Close();
                
                _networkTask?.Wait(1000);
                _discoveryTask?.Wait(1000);
                
                _cancellationTokenSource?.Dispose();
                
                // Dispose advanced infrastructure components
                DisposeAdvancedInfrastructure();
                
                // Cleanup shared position manager
                _sharedPositionManager?.Cleanup();
            }
            catch (Exception ex)
            {
                LogMessage($"Error disposing network services: {ex.Message}", 1);
            }
            
            base.Dispose();
        }
        
        /// <summary>
        /// Disposes all advanced infrastructure components
        /// </summary>
        private void DisposeAdvancedInfrastructure()
        {
            try
            {
                _networkLogger?.LogNetworkEvent("SHUTDOWN", new { Status = "Disposing advanced infrastructure" });
                
                // Dispose components in reverse order of initialization
                _commandExecutor = null;
                _messageProtocol?.Dispose();
                _networkSecurity?.Dispose();
                _networkLogger?.Dispose();
                
                LogMessage("Advanced infrastructure disposed successfully", 4);
            }
            catch (Exception ex)
            {
                LogMessage($"Error disposing advanced infrastructure: {ex.Message}", 1);
            }
        }

    /// <summary>
    /// Initializes access to shared utilities via PluginBridge.
    /// </summary>
    private void InitializeSharedUtilities()
    {
        try
        {
            // Check for InputCoordinator
            var requestControlMethod = GameController.PluginBridge.GetMethod<Func<string, int, bool>>("InputCoordinator.RequestControl");
            if (requestControlMethod != null)
            {
                LogMessage("InputCoordinator detected via PluginBridge", 5);
            }

            // Check for PluginLogger
            var logErrorMethod = GameController.PluginBridge.GetMethod<Action<string, string>>("PluginLogger.LogError");
            if (logErrorMethod != null)
            {
                LogMessage("PluginLogger detected via PluginBridge", 5);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to initialize shared utilities: {ex.Message}", 5);
        }
    }
    
    /// <summary>
    /// Initializes the shared position manager for file-based leader position fallback
    /// </summary>
    private void InitializeSharedPositionManager()
    {
        try
        {
            if (Settings.EnableSharedPositionFallback.Value)
            {
                // Use leader name as the character name for the shared position file
                var leaderName = Settings.LeaderName.Value?.Trim();
                if (!string.IsNullOrEmpty(leaderName))
                {
                    _sharedPositionManager = new SharedPositionManager(leaderName);
                    LogMessage($"Shared position manager initialized for leader: {leaderName}", 4);
                }
                else
                {
                    LogMessage("Shared position manager not initialized - no leader name configured", 3);
                }
            }
            else
            {
                LogMessage("Shared position fallback is disabled", 4);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to initialize shared position manager: {ex.Message}", 1);
        }
    }
    
    /// <summary>
    /// Initializes the Smart UI Avoidance system
    /// </summary>
    private void InitializeSmartUIAvoidance()
    {
        try
        {
            if (Settings.EnableSmartUIAvoidance.Value)
            {
                _smartUIAvoidance = new SmartUIAvoidance(GameController, Settings);
                LogMessage("Smart UI Avoidance system initialized", 4);
            }
            else
            {
                LogMessage("Smart UI Avoidance is disabled", 4);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to initialize Smart UI Avoidance: {ex.Message}", 1);
        }
    }
    
    /// <summary>
    /// Initializes the Advanced Pathfinding system
    /// </summary>
    private void InitializeAdvancedPathfinding()
    {
        try
        {
            if (Settings.EnableAdvancedPathfinding.Value && _tiles != null)
            {
                _advancedPathFinder = new AdvancedPathFinder(_tiles, GameController, Settings);
                LogMessage("Advanced Pathfinding system initialized", 4);
            }
            else
            {
                LogMessage("Advanced Pathfinding is disabled or terrain data unavailable", 4);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Failed to initialize Advanced Pathfinding: {ex.Message}", 1);
        }
    }

    private bool RequestInputControl(string pluginName, int durationMs)
    {
        try
        {
            var method = GameController.PluginBridge.GetMethod<Func<string, int, bool>>("InputCoordinator.RequestControl");
            return method?.Invoke(pluginName, durationMs) ?? true; // Fallback to true if not available
        }
        catch
        {
            return true; // Proceed if bridge fails
        }
    }

    private void ReleaseInputControl(string pluginName)
    {
        try
        {
            var method = GameController.PluginBridge.GetMethod<Action<string>>("InputCoordinator.ReleaseControl");
            method?.Invoke(pluginName);
        }
        catch { }
    }

    private void LogSharedError(string pluginName, string message)
    {
        try
        {
            var method = GameController.PluginBridge.GetMethod<Action<string, string>>("PluginLogger.LogError");
            method?.Invoke(pluginName, message);
        }
        catch { }
    }
}
    
    // Message classes for network communication
    public class CommandMessage
    {
        public string Type { get; set; }
        public string Command { get; set; }
        public Dictionary<string, object> Data { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class FollowerMessage
    {
        public string Type { get; set; }
        public string Data { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class DiscoveryMessage
    {
        public string Type { get; set; }
        public string LeaderName { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public DateTime Timestamp { get; set; }
    }
