# Enterprise Follower Plugin for ExileAPI

## Overview
A **production-ready, enterprise-grade** follower plugin for Path of Exile that provides advanced automated character following with comprehensive network communication, security, reliability, and monitoring features. Built for multi-PC gaming environments with industrial-strength architecture.

## 🚀 **Key Features**

### **Core Following System**
- **Intelligent Following**: Advanced pathfinding with obstacle detection and stuck recovery
- **Teleport Detection**: Automatically search for doors/transitions when leader teleports
- **Quest Item Pickup**: Automatically collect quest items and claim waypoints
- **Multiple Leader Support**: Follow different leaders with priority system
- **Safety Features**: Death handling, portal avoidance, and inventory management

### **🔐 Enterprise Security**
- **AES-256 Encryption**: All network communications encrypted
- **JWT Authentication**: Secure token-based authentication system
- **Message Integrity**: SHA-256 checksums prevent tampering
- **Replay Protection**: Duplicate message detection and prevention
- **Trusted Leader Whitelist**: Only accept commands from verified leaders

### **🛡️ Network Reliability**
- **Circuit Breakers**: Prevent cascading failures with automatic recovery
- **Exponential Backoff**: Intelligent retry logic with jitter
- **Connection Pooling**: Efficient connection management
- **Heartbeat Monitoring**: Real-time connection health tracking
- **Automatic Reconnection**: Seamless recovery from network issues

### **📊 Comprehensive Monitoring**
- **Structured Logging**: JSON-formatted logs with full context
- **Performance Metrics**: Real-time performance tracking and analysis
- **Health Checks**: Continuous system health monitoring
- **Error Tracking**: Detailed error reporting and categorization
- **Protocol Statistics**: Message processing and queue monitoring

### **⚡ Advanced Command Execution**
- **Intelligent Stashing**: Automated item organization with filtering
- **Smart Selling**: Vendor interaction with item valuation
- **Secure Trading**: Whitelist-based trade acceptance
- **Async Processing**: Non-blocking command execution
- **Error Recovery**: Robust error handling and retry logic

### **🔧 Enhanced Message Protocol V2**
- **Message Deduplication**: Prevents duplicate command execution
- **Priority Queue**: Critical messages processed first
- **Message Ordering**: Guaranteed sequential processing
- **TTL Management**: Automatic message expiration
- **Checksums**: Message integrity verification

## 📦 **Installation & Setup**

### **Quick Start**
1. Download the plugin and place in `ExileAPI/Plugins/Source/Follower/`
2. Start ExileCore and enable the Follower plugin
3. Configure leader name and network settings
4. Enable advanced features as needed

### **Network Configuration**
1. **Enable Network Communication** in settings (F12 → Follower → Network Communication)
2. **Configure Security**: Set API key and enable encryption
3. **Set Leader Details**: Use auto-discovery or manual IP configuration
4. **Firewall Setup**: Allow ExileCore through Windows Firewall on ports 7777-7778

## ⚙️ **Configuration**

### **Basic Settings**
- **Leader Name**: Character name to follow
- **Movement Settings**: Input frequency and pathfinding parameters
- **Following Behavior**: Close follow, distance thresholds, dash settings

### **Security Settings**
- **Enable Security Features**: Master security toggle
- **Message Encryption**: AES-256 encryption for all communications
- **Authentication**: JWT token-based authentication
- **API Key**: Secure authentication key
- **Security Audit Logging**: Comprehensive security event logging

### **Network Settings**
- **Leader IP Address**: Manual IP configuration
- **Leader Port**: TCP communication port (default: 7777)
- **Discovery Port**: UDP auto-discovery port (default: 7778)
- **Connection Retry**: Automatic reconnection settings
- **Heartbeat Interval**: Connection health monitoring

### **Advanced Features**
- **Circuit Breakers**: Failure threshold and timeout settings
- **Message Protocol**: Enhanced protocol V2 with deduplication
- **Performance Monitoring**: Metrics collection and health checks
- **Auto-Configuration**: Automatic optimal settings detection

## 🌐 **Network Communication**

### **Architecture**
- **TCP**: Reliable command transmission
- **UDP**: Fast auto-discovery
- **JSON**: Structured message format
- **Encryption**: End-to-end security
- **Compression**: Optional message compression

### **Message Types**
- **COMMAND**: Execute follower actions
- **HEARTBEAT**: Connection health monitoring
- **DISCOVERY**: Auto-discovery of leaders
- **AUTHENTICATION**: Secure connection establishment
- **ACKNOWLEDGMENT**: Message delivery confirmation

### **Security Model**
```
Leader PC                    Follower PC
┌─────────────────┐         ┌─────────────────┐
│ LeaderCommands  │         │ Follower Plugin │
│ Plugin          │         │                 │
│                 │         │                 │
│ 1. Generate     │         │ 4. Decrypt      │
│    JWT Token    │         │    Message      │
│                 │         │                 │
│ 2. Encrypt      │◄────────┤ 5. Validate     │
│    Message      │  TCP    │    Token        │
│                 │         │                 │
│ 3. Send         │────────►│ 6. Execute      │
│    Command      │         │    Command      │
└─────────────────┘         └─────────────────┘
```

## 🔨 **Command System**

### **Available Commands**
- **STASH_ITEMS**: Automated item stashing with filtering
- **SELL_ITEMS**: Intelligent vendor interaction
- **ACCEPT_TRADE**: Secure trade acceptance
- **EMERGENCY_STOP**: Immediate halt all operations

### **Command Structure**
```json
{
  "Type": "COMMAND",
  "Command": "STASH_ITEMS",
  "Data": {
    "stashTabs": ["Currency", "Gems"],
    "itemFilters": {
      "minRarity": "Rare",
      "itemTypes": ["Currency", "Gem"]
    }
  },
  "Priority": "High",
  "Timestamp": "2024-01-01T12:00:00Z"
}
```

## 🏗️ **Architecture Overview**

### **Core Components**
- **NetworkSecurity**: Encryption, authentication, and message integrity
- **NetworkReliability**: Circuit breakers, retry logic, and error recovery
- **NetworkLogger**: Structured logging, metrics, and monitoring
- **EnhancedMessageProtocol**: Advanced message handling and processing
- **CommandExecutor**: Intelligent command execution with game interaction
- **ConfigurationValidator**: Comprehensive settings validation and optimization

### **Data Flow**
```
Network Input → Security Layer → Protocol Layer → Command Executor → Game Actions
     ↓              ↓              ↓              ↓              ↓
  Encrypted      Decrypted     Validated     Processed     Executed
  Message        Message       Message       Command       Action
```

## 🔍 **Monitoring & Observability**

### **Health Metrics**
- **Connection Status**: Real-time connection health
- **Message Processing**: Queue sizes and processing rates
- **Error Rates**: Failure rates and error categorization
- **Performance Metrics**: Latency, throughput, and resource usage

### **Logging Features**
- **Structured JSON**: Machine-readable log format
- **Performance Tracking**: Operation timing and success rates
- **Error Context**: Full error details with stack traces
- **Security Auditing**: Authentication and authorization events

### **Alerting**
- **Health Degradation**: Automatic detection of system issues
- **Security Events**: Unauthorized access attempts
- **Performance Issues**: Latency or throughput problems
- **Connection Failures**: Network communication problems

## 🧪 **Testing & Validation**

### **Comprehensive Test Suite**
- **Unit Tests**: Component-level testing
- **Integration Tests**: End-to-end workflow testing
- **Performance Tests**: Load and stress testing
- **Security Tests**: Authentication and encryption validation

### **Configuration Validation**
- **Network Connectivity**: Automatic network testing
- **Port Availability**: Port conflict detection
- **Security Settings**: Encryption and authentication validation
- **Performance Optimization**: Automatic optimal settings detection

## 🚀 **Performance Optimizations**

### **Network Optimizations**
- **Connection Pooling**: Efficient connection reuse
- **Message Batching**: Reduced network overhead
- **Compression**: Optional message compression
- **Keep-Alive**: Persistent connection management

### **Processing Optimizations**
- **Async Operations**: Non-blocking command execution
- **Thread Pool**: Efficient thread management
- **Memory Management**: Automatic cleanup and optimization
- **Caching**: Intelligent data caching

## 🔧 **Troubleshooting**

### **Common Issues**
- **Connection Refused**: Check firewall and port settings
- **Authentication Failed**: Verify API keys and certificates
- **High Latency**: Check network configuration and optimization
- **Memory Usage**: Monitor and optimize resource usage

### **Debug Features**
- **Detailed Logging**: Comprehensive debug information
- **Network Diagnostics**: Built-in network testing tools
- **Performance Profiling**: Resource usage monitoring
- **Configuration Validation**: Automatic settings verification

## 📚 **API Reference**

### **Core Classes**
- `NetworkSecurity`: Encryption and authentication
- `NetworkReliability`: Error recovery and circuit breakers
- `NetworkLogger`: Logging and monitoring
- `EnhancedMessageProtocol`: Advanced message handling
- `CommandExecutor`: Game interaction and command processing

### **Configuration**
- `FollowerSettings`: Comprehensive plugin settings
- `ConfigurationValidator`: Settings validation and optimization
- `SecurityConfiguration`: Security-related settings
- `NetworkConfiguration`: Network communication settings

## 🤝 **Related Plugins**
- **LeaderCommands**: Companion plugin for sending commands to followers

## 📄 **License**
This plugin is provided as-is for educational and personal use. Not affiliated with Grinding Gear Games.

## 🙏 **Acknowledgments**
Built with ExileCore framework and modern C# best practices for production-ready performance. 
