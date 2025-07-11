using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace Follower
{
    /// <summary>
    /// Comprehensive configuration validation and auto-configuration system
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly FollowerSettings _settings;
        private readonly List<ValidationResult> _validationResults = new List<ValidationResult>();
        
        public ConfigurationValidator(FollowerSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }
        
        /// <summary>
        /// Validates all configuration settings
        /// </summary>
        public async Task<ConfigurationValidationReport> ValidateAllSettings()
        {
            var report = new ConfigurationValidationReport();
            _validationResults.Clear();
            
            try
            {
                // Validate basic settings
                ValidateBasicSettings();
                
                // Validate network settings
                await ValidateNetworkSettings();
                
                // Validate plugin integration settings
                ValidatePluginIntegrationSettings();
                
                // Validate advanced settings
                ValidateAdvancedSettings();
                
                // Validate security settings
                ValidateSecuritySettings();
                
                // Validate performance settings
                ValidatePerformanceSettings();
                
                // Generate recommendations
                GenerateRecommendations();
                
                // Compile report
                report.IsValid = _validationResults.All(r => r.IsValid);
                report.ValidationResults = _validationResults.ToList();
                report.ErrorCount = _validationResults.Count(r => !r.IsValid && r.Severity == ValidationSeverity.Error);
                report.WarningCount = _validationResults.Count(r => !r.IsValid && r.Severity == ValidationSeverity.Warning);
                report.InfoCount = _validationResults.Count(r => r.IsValid && r.Severity == ValidationSeverity.Info);
                
                return report;
            }
            catch (Exception ex)
            {
                report.IsValid = false;
                report.ValidationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Error,
                    Category = "System",
                    Message = $"Configuration validation failed: {ex.Message}",
                    Details = ex.ToString()
                });
                
                return report;
            }
        }
        
        /// <summary>
        /// Validates basic follower settings
        /// </summary>
        private void ValidateBasicSettings()
        {
            // Validate leader name
            if (string.IsNullOrWhiteSpace(_settings.LeaderName.Value))
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Warning,
                    Category = "Basic",
                    Message = "Leader name is not configured",
                    Recommendation = "Set a leader name to enable following functionality"
                });
            }
            else
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = true,
                    Severity = ValidationSeverity.Info,
                    Category = "Basic",
                    Message = $"Leader name configured: {_settings.LeaderName.Value}"
                });
            }
            
            // Validate movement settings
            if (_settings.BotInputFrequency.Value < 10 || _settings.BotInputFrequency.Value > 250)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Warning,
                    Category = "Basic",
                    Message = "Bot input frequency may cause performance issues",
                    Recommendation = "Set bot input frequency between 10-250ms for optimal performance"
                });
            }
            
            // Validate distance settings
            if (_settings.PathfindingNodeDistance.Value > _settings.ClearPathDistance.Value)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Error,
                    Category = "Basic",
                    Message = "Pathfinding node distance exceeds clear path distance",
                    Recommendation = "Ensure pathfinding node distance is less than clear path distance"
                });
            }
        }
        
        /// <summary>
        /// Validates network communication settings
        /// </summary>
        private async Task ValidateNetworkSettings()
        {
            if (!_settings.EnableNetworkCommunication.Value)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = true,
                    Severity = ValidationSeverity.Info,
                    Category = "Network",
                    Message = "Network communication is disabled"
                });
                return;
            }
            
            // Validate IP address format
            if (!string.IsNullOrEmpty(_settings.LeaderIpAddress.Value))
            {
                if (!IPAddress.TryParse(_settings.LeaderIpAddress.Value, out var ipAddress))
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = false,
                        Severity = ValidationSeverity.Error,
                        Category = "Network",
                        Message = "Invalid IP address format",
                        Recommendation = "Enter a valid IP address (e.g., 192.168.1.100)"
                    });
                }
                else
                {
                    // Test connectivity
                    var connectivityResult = await TestNetworkConnectivity(ipAddress.ToString(), _settings.LeaderPort.Value);
                    _validationResults.Add(connectivityResult);
                }
            }
            
            // Validate port ranges
            if (_settings.LeaderPort.Value < 1024 || _settings.LeaderPort.Value > 65535)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Error,
                    Category = "Network",
                    Message = "Leader port is out of valid range",
                    Recommendation = "Use ports between 1024-65535"
                });
            }
            
            if (_settings.DiscoveryPort.Value < 1024 || _settings.DiscoveryPort.Value > 65535)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Error,
                    Category = "Network",
                    Message = "Discovery port is out of valid range",
                    Recommendation = "Use ports between 1024-65535"
                });
            }
            
            // Check for port conflicts
            if (_settings.LeaderPort.Value == _settings.DiscoveryPort.Value)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Error,
                    Category = "Network",
                    Message = "Leader port and discovery port cannot be the same",
                    Recommendation = "Use different ports for leader and discovery communication"
                });
            }
            
            // Validate network interfaces
            await ValidateNetworkInterfaces();
        }
        
        /// <summary>
        /// Tests network connectivity to a specific host and port
        /// </summary>
        private async Task<ValidationResult> TestNetworkConnectivity(string host, int port)
        {
            try
            {
                using var client = new TcpClient();
                var connectTask = client.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(5000);
                
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                
                if (completedTask == connectTask && client.Connected)
                {
                    return new ValidationResult
                    {
                        IsValid = true,
                        Severity = ValidationSeverity.Info,
                        Category = "Network",
                        Message = $"Network connectivity to {host}:{port} confirmed"
                    };
                }
                else
                {
                    return new ValidationResult
                    {
                        IsValid = false,
                        Severity = ValidationSeverity.Warning,
                        Category = "Network",
                        Message = $"Cannot connect to {host}:{port}",
                        Recommendation = "Check that the leader is running and network is accessible"
                    };
                }
            }
            catch (Exception ex)
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Warning,
                    Category = "Network",
                    Message = $"Network connectivity test failed: {ex.Message}",
                    Recommendation = "Verify network configuration and firewall settings"
                };
            }
        }
        
        /// <summary>
        /// Validates available network interfaces
        /// </summary>
        private async Task ValidateNetworkInterfaces()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                    .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .ToList();
                
                if (!interfaces.Any())
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = false,
                        Severity = ValidationSeverity.Error,
                        Category = "Network",
                        Message = "No active network interfaces found",
                        Recommendation = "Check network connection and adapter settings"
                    });
                    return;
                }
                
                var hasEthernet = interfaces.Any(i => i.NetworkInterfaceType == NetworkInterfaceType.Ethernet);
                var hasWifi = interfaces.Any(i => i.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);
                
                if (!hasEthernet && !hasWifi)
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = false,
                        Severity = ValidationSeverity.Warning,
                        Category = "Network",
                        Message = "No standard network interfaces (Ethernet/WiFi) detected",
                        Recommendation = "Ensure proper network adapter is connected and configured"
                    });
                }
                else
                {
                    var interfaceTypes = interfaces.Select(i => i.NetworkInterfaceType.ToString()).Distinct();
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = true,
                        Severity = ValidationSeverity.Info,
                        Category = "Network",
                        Message = $"Network interfaces available: {string.Join(", ", interfaceTypes)}"
                    });
                }
            }
            catch (Exception ex)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Warning,
                    Category = "Network",
                    Message = $"Network interface validation failed: {ex.Message}"
                });
            }
        }
        
        /// <summary>
        /// Validates plugin integration settings
        /// </summary>
        private void ValidatePluginIntegrationSettings()
        {
            // Validate PickItV2 integration
            if (_settings.YieldToPickItV2.Value)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = true,
                    Severity = ValidationSeverity.Info,
                    Category = "Integration",
                    Message = "PickItV2 integration enabled",
                    Recommendation = "Ensure PickItV2 plugin is installed and configured"
                });
            }
            
            // Validate multiple leaders configuration
            if (_settings.EnableMultipleLeaders.Value)
            {
                var leaderNames = _settings.LeaderNames.Value.Split(',')
                    .Select(name => name.Trim())
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToList();
                
                if (!leaderNames.Any())
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = false,
                        Severity = ValidationSeverity.Error,
                        Category = "Integration",
                        Message = "Multiple leaders enabled but no leader names configured",
                        Recommendation = "Configure leader names or disable multiple leaders"
                    });
                }
                else
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = true,
                        Severity = ValidationSeverity.Info,
                        Category = "Integration",
                        Message = $"Multiple leaders configured: {string.Join(", ", leaderNames)}"
                    });
                }
            }
        }
        
        /// <summary>
        /// Validates advanced feature settings
        /// </summary>
        private void ValidateAdvancedSettings()
        {
            // Validate stuck detection settings
            if (_settings.EnableStuckDetection.Value)
            {
                if (_settings.StuckDetectionTime.Value < 1000)
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = false,
                        Severity = ValidationSeverity.Warning,
                        Category = "Advanced",
                        Message = "Stuck detection time may be too sensitive",
                        Recommendation = "Consider using at least 1000ms for stuck detection"
                    });
                }
                
                if (_settings.MaxStuckRecoveryAttempts.Value < 1)
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = false,
                        Severity = ValidationSeverity.Error,
                        Category = "Advanced",
                        Message = "Invalid stuck recovery attempts configuration",
                        Recommendation = "Set at least 1 stuck recovery attempt"
                    });
                }
            }
            
            // Validate inventory management
            if (_settings.EnableInventoryManagement.Value)
            {
                if (_settings.InventoryFullThreshold.Value < 20 || _settings.InventoryFullThreshold.Value > 60)
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = false,
                        Severity = ValidationSeverity.Warning,
                        Category = "Advanced",
                        Message = "Inventory threshold may cause issues",
                        Recommendation = "Set inventory threshold between 20-60 for best results"
                    });
                }
            }
        }
        
        /// <summary>
        /// Validates security-related settings
        /// </summary>
        private void ValidateSecuritySettings()
        {
            // Validate command execution security
            if (_settings.EnableLeaderCommands.Value)
            {
                if (_settings.MaxCommandExecutionTime.Value < 10000)
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = false,
                        Severity = ValidationSeverity.Warning,
                        Category = "Security",
                        Message = "Command execution timeout may be too low",
                        Recommendation = "Allow at least 10 seconds for command execution"
                    });
                }
                
                if (_settings.EnableStashingCommands.Value || _settings.EnableSellingCommands.Value || _settings.EnableTradingCommands.Value)
                {
                    _validationResults.Add(new ValidationResult
                    {
                        IsValid = true,
                        Severity = ValidationSeverity.Info,
                        Category = "Security",
                        Message = "Automated commands enabled - ensure leader is trusted",
                        Recommendation = "Only enable automated commands with trusted leaders"
                    });
                }
            }
        }
        
        /// <summary>
        /// Validates performance-related settings
        /// </summary>
        private void ValidatePerformanceSettings()
        {
            // Check for performance-impacting combinations
            if (_settings.BotInputFrequency.Value < 25 && _settings.EnableStuckDetection.Value && _settings.AggressiveStuckDetection.Value)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Warning,
                    Category = "Performance",
                    Message = "High frequency updates with aggressive stuck detection may impact performance",
                    Recommendation = "Consider increasing bot input frequency or disabling aggressive stuck detection"
                });
            }
            
            if (_settings.TerrainRefreshRate.Value < 500)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Warning,
                    Category = "Performance",
                    Message = "Terrain refresh rate may be too aggressive",
                    Recommendation = "Consider setting terrain refresh rate to at least 500ms"
                });
            }
        }
        
        /// <summary>
        /// Generates configuration recommendations
        /// </summary>
        private void GenerateRecommendations()
        {
            // Performance optimization recommendations
            if (_settings.EnableNetworkCommunication.Value && _settings.BotInputFrequency.Value < 50)
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = true,
                    Severity = ValidationSeverity.Info,
                    Category = "Optimization",
                    Message = "Consider increasing bot input frequency when using network communication",
                    Recommendation = "Set bot input frequency to 50-100ms for optimal network performance"
                });
            }
            
            // Security recommendations
            if (_settings.EnableNetworkCommunication.Value && !_settings.EnableAutoDiscovery.Value && string.IsNullOrEmpty(_settings.LeaderIpAddress.Value))
            {
                _validationResults.Add(new ValidationResult
                {
                    IsValid = false,
                    Severity = ValidationSeverity.Warning,
                    Category = "Configuration",
                    Message = "Network communication enabled but no connection method configured",
                    Recommendation = "Enable auto-discovery or configure leader IP address"
                });
            }
        }
        
        /// <summary>
        /// Attempts to auto-configure optimal settings
        /// </summary>
        public async Task<AutoConfigurationResult> AutoConfigureOptimalSettings()
        {
            var result = new AutoConfigurationResult();
            
            try
            {
                // Detect network configuration
                var networkConfig = await DetectNetworkConfiguration();
                result.NetworkConfiguration = networkConfig;
                
                // Optimize performance settings
                var performanceConfig = OptimizePerformanceSettings();
                result.PerformanceConfiguration = performanceConfig;
                
                // Apply security defaults
                var securityConfig = ApplySecurityDefaults();
                result.SecurityConfiguration = securityConfig;
                
                result.IsSuccessful = true;
                result.Message = "Auto-configuration completed successfully";
                
                return result;
            }
            catch (Exception ex)
            {
                result.IsSuccessful = false;
                result.Message = $"Auto-configuration failed: {ex.Message}";
                return result;
            }
        }
        
        private async Task<NetworkConfiguration> DetectNetworkConfiguration()
        {
            var config = new NetworkConfiguration();
            
            // Detect available network interfaces
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();
            
            config.AvailableInterfaces = interfaces.Select(i => i.Name).ToList();
            
            // Detect optimal ports
            config.OptimalLeaderPort = await FindAvailablePort(7777);
            config.OptimalDiscoveryPort = await FindAvailablePort(7778);
            
            return config;
        }
        
        private async Task<int> FindAvailablePort(int startPort)
        {
            for (int port = startPort; port <= startPort + 100; port++)
            {
                try
                {
                    using var listener = new TcpListener(IPAddress.Any, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch
                {
                    // Port is in use, try next one
                }
            }
            
            return startPort; // Fallback to original port
        }
        
        private PerformanceConfiguration OptimizePerformanceSettings()
        {
            return new PerformanceConfiguration
            {
                OptimalBotInputFrequency = 50,
                OptimalTerrainRefreshRate = 1000,
                OptimalStuckDetectionTime = 3000,
                OptimalMaxPathfindingIterations = 500
            };
        }
        
        private SecurityConfiguration ApplySecurityDefaults()
        {
            return new SecurityConfiguration
            {
                RecommendedMaxCommandExecutionTime = 60000,
                RecommendedConnectionRetryInterval = 5000,
                RecommendedHeartbeatInterval = 10000
            };
        }
        
        // Configuration classes
        public class ConfigurationValidationReport
        {
            public bool IsValid { get; set; }
            public List<ValidationResult> ValidationResults { get; set; } = new List<ValidationResult>();
            public int ErrorCount { get; set; }
            public int WarningCount { get; set; }
            public int InfoCount { get; set; }
            public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
        }
        
        public class ValidationResult
        {
            public bool IsValid { get; set; }
            public ValidationSeverity Severity { get; set; }
            public string Category { get; set; }
            public string Message { get; set; }
            public string Recommendation { get; set; }
            public string Details { get; set; }
        }
        
        public enum ValidationSeverity
        {
            Info,
            Warning,
            Error
        }
        
        public class AutoConfigurationResult
        {
            public bool IsSuccessful { get; set; }
            public string Message { get; set; }
            public NetworkConfiguration NetworkConfiguration { get; set; }
            public PerformanceConfiguration PerformanceConfiguration { get; set; }
            public SecurityConfiguration SecurityConfiguration { get; set; }
        }
        
        public class NetworkConfiguration
        {
            public List<string> AvailableInterfaces { get; set; } = new List<string>();
            public int OptimalLeaderPort { get; set; }
            public int OptimalDiscoveryPort { get; set; }
        }
        
        public class PerformanceConfiguration
        {
            public int OptimalBotInputFrequency { get; set; }
            public int OptimalTerrainRefreshRate { get; set; }
            public int OptimalStuckDetectionTime { get; set; }
            public int OptimalMaxPathfindingIterations { get; set; }
        }
        
        public class SecurityConfiguration
        {
            public int RecommendedMaxCommandExecutionTime { get; set; }
            public int RecommendedConnectionRetryInterval { get; set; }
            public int RecommendedHeartbeatInterval { get; set; }
        }
    }
} 