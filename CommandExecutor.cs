using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.PoEMemory.Elements;
using ExileCore.Shared.Enums;
using SharpDX;
using System.Windows.Forms;
using ExileCore.Shared;

namespace Follower
{
    /// <summary>
    /// Production-ready command execution system for automated game operations
    /// </summary>
    public class CommandExecutor
    {
        private readonly GameController _gameController;
        private readonly FollowerSettings _settings;
        private readonly NetworkLogger _logger;
        private readonly NetworkReliability _reliability;
        private readonly Random _random = new Random();
        private readonly object _executionLock = new object();
        private bool _isExecuting = false;
        
        public CommandExecutor(GameController gameController, FollowerSettings settings, NetworkLogger logger, NetworkReliability reliability)
        {
            _gameController = gameController ?? throw new ArgumentNullException(nameof(gameController));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _reliability = reliability ?? throw new ArgumentNullException(nameof(reliability));
        }
        
        /// <summary>
        /// Executes a stash command with comprehensive inventory management
        /// </summary>
        public async Task<CommandResult> ExecuteStashCommand(Dictionary<string, object> commandData)
        {
            if (!_settings.EnableStashingCommands.Value)
                return CommandResult.Disabled("Stashing commands are disabled");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                return await _reliability.ExecuteWithRetry(async () =>
                {
                    lock (_executionLock)
                    {
                        if (_isExecuting)
                            throw new InvalidOperationException("Another command is currently executing");
                        _isExecuting = true;
                    }
                    
                    try
                    {
                        _logger.LogCommandEvent("STASH_ITEMS", "STARTED", null, commandData);
                        
                        // Parse command data
                        var stashConfig = ParseStashConfiguration(commandData);
                        
                        // Find and validate stash
                        var stash = await FindNearbyStash();
                        if (stash == null)
                            return CommandResult.Failed("No stash found nearby");
                        
                        // Open stash
                        var stashOpened = await OpenStash(stash);
                        if (!stashOpened)
                            return CommandResult.Failed("Failed to open stash");
                        
                        // Execute stashing operations
                        var stashingResult = await ExecuteStashingOperations(stashConfig);
                        
                        // Close stash
                        await CloseStash();
                        
                        var result = stashingResult.IsSuccess ? 
                            CommandResult.Success($"Stashed {stashingResult.ItemsProcessed} items") :
                            CommandResult.Failed(stashingResult.ErrorMessage);
                        
                        _logger.LogCommandEvent("STASH_ITEMS", result.IsSuccess ? "SUCCESS" : "FAILED", 
                            stopwatch.Elapsed, stashingResult, result.ErrorMessage);
                        
                        return result;
                    }
                    finally
                    {
                        _isExecuting = false;
                    }
                }, "stash_command");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing stash command: {ex.Message}", ex, "stash_command");
                return CommandResult.Failed($"Command execution failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Executes a sell command by simulating the sell key press
        /// </summary>
        public async Task<CommandResult> ExecuteSellCommand(Dictionary<string, object> commandData)
        {
            if (!_settings.EnableSellingCommands.Value)
                return CommandResult.Disabled("Selling commands are disabled");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                return await _reliability.ExecuteWithRetry(async () =>
                {
                    lock (_executionLock)
                    {
                        if (_isExecuting)
                            throw new InvalidOperationException("Another command is currently executing");
                        _isExecuting = true;
                    }
                    
                    try
                    {
                        _logger.LogCommandEvent("SELL_ITEMS", "STARTED", null, commandData);
                        
                        // Execute the sell command by simulating the key press  
                        var sellResult = await ExecuteSellKeyPress();
                        
                        var result = sellResult ? 
                            CommandResult.Success("Sell command executed successfully") :
                            CommandResult.Failed("Failed to execute sell command");
                        
                        _logger.LogCommandEvent("SELL_ITEMS", result.IsSuccess ? "SUCCESS" : "FAILED", 
                            stopwatch.Elapsed, new { KeyPressed = "SellKey" }, result.ErrorMessage);
                        
                        return result;
                    }
                    finally
                    {
                        _isExecuting = false;
                    }
                }, "sell_command");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing sell command: {ex.Message}", ex, "sell_command");
                return CommandResult.Failed($"Command execution failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Executes a trade command by simulating the trade accept key press
        /// </summary>
        public async Task<CommandResult> ExecuteTradeCommand(Dictionary<string, object> commandData)
        {
            if (!_settings.EnableTradingCommands.Value)
                return CommandResult.Disabled("Trading commands are disabled");
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                return await _reliability.ExecuteWithRetry(async () =>
                {
                    lock (_executionLock)
                    {
                        if (_isExecuting)
                            throw new InvalidOperationException("Another command is currently executing");
                        _isExecuting = true;
                    }
                    
                    try
                    {
                        _logger.LogCommandEvent("ACCEPT_TRADE", "STARTED", null, commandData);
                        
                        // Execute the trade accept command by simulating the key press
                        var tradeResult = await ExecuteTradeAcceptKeyPress();
                        
                        var result = tradeResult ? 
                            CommandResult.Success("Trade accept command executed successfully") :
                            CommandResult.Failed("Failed to execute trade accept command");
                        
                        _logger.LogCommandEvent("ACCEPT_TRADE", result.IsSuccess ? "SUCCESS" : "FAILED", 
                            stopwatch.Elapsed, new { KeyPressed = "TradeAcceptKey" }, result.ErrorMessage);
                        
                        return result;
                    }
                    finally
                    {
                        _isExecuting = false;
                    }
                }, "trade_command");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing trade command: {ex.Message}", ex, "trade_command");
                return CommandResult.Failed($"Command execution failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Executes the stash key press (F10 or whatever key is configured)
        /// </summary>
        private async Task<bool> ExecuteStashKeyPress()
        {
            try
            {
                var stashKey = _settings.StashCommandKey.Value;
                
                _logger.LogCommandEvent("STASH_KEY_PRESS", "STARTING", null, new { Key = stashKey.ToString() });
                
                await Task.Run(() =>
                {
                    // Press the configured stash key
                    Input.KeyDown(stashKey);
                    Thread.Sleep(50);
                    Input.KeyUp(stashKey);
                });
                
                // Wait a bit for the command to be processed
                await Task.Delay(200);
                
                _logger.LogCommandEvent("STASH_KEY_PRESS", "EXECUTED", null, new { Key = stashKey.ToString() });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing stash key press: {ex.Message}", ex, "stash_key_press");
                return false;
            }
        }
        
        /// <summary>
        /// Executes the sell key press (or whatever key is configured)
        /// </summary>
        private async Task<bool> ExecuteSellKeyPress()
        {
            try
            {
                var sellKey = _settings.SellCommandKey.Value;
                
                _logger.LogCommandEvent("SELL_KEY_PRESS", "STARTING", null, new { Key = sellKey.ToString() });
                
                await Task.Run(() =>
                {
                    // Press the configured sell key
                    Input.KeyDown(sellKey);
                    Thread.Sleep(50);
                    Input.KeyUp(sellKey);
                });
                
                // Wait a bit for the command to be processed
                await Task.Delay(200);
                
                _logger.LogCommandEvent("SELL_KEY_PRESS", "EXECUTED", null, new { Key = sellKey.ToString() });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing sell key press: {ex.Message}", ex, "sell_key_press");
                return false;
            }
        }
        
        /// <summary>
        /// Executes the trade accept key press (or whatever key is configured)
        /// </summary>
        private async Task<bool> ExecuteTradeAcceptKeyPress()
        {
            try
            {
                var tradeKey = _settings.TradeAcceptKey.Value;
                
                _logger.LogCommandEvent("TRADE_ACCEPT_KEY_PRESS", "STARTING", null, new { Key = tradeKey.ToString() });
                
                await Task.Run(() =>
                {
                    // Press the configured trade accept key
                    Input.KeyDown(tradeKey);
                    Thread.Sleep(50);
                    Input.KeyUp(tradeKey);
                });
                
                // Wait a bit for the command to be processed
                await Task.Delay(200);
                
                _logger.LogCommandEvent("TRADE_ACCEPT_KEY_PRESS", "EXECUTED", null, new { Key = tradeKey.ToString() });
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error executing trade accept key press: {ex.Message}", ex, "trade_accept_key_press");
                return false;
            }
        }
        
        private StashConfiguration ParseStashConfiguration(Dictionary<string, object> commandData)
        {
            var config = new StashConfiguration();
            
            if (commandData.TryGetValue("stashTabs", out var stashTabsObj) && stashTabsObj is List<object> stashTabs)
            {
                config.TargetStashTabs = stashTabs.Cast<string>().ToList();
            }
            
            if (commandData.TryGetValue("itemFilters", out var itemFiltersObj) && itemFiltersObj is Dictionary<string, object> itemFilters)
            {
                config.ItemFilters = itemFilters;
            }
            
            if (commandData.TryGetValue("preserveSpace", out var preserveSpaceObj) && preserveSpaceObj is bool preserveSpace)
            {
                config.PreserveSpace = preserveSpace;
            }
            
            if (commandData.TryGetValue("maxItemsPerTab", out var maxItemsObj) && maxItemsObj is int maxItems)
            {
                config.MaxItemsPerTab = maxItems;
            }
            
            return config;
        }
        
        private async Task<Entity> FindNearbyStash()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var playerPos = _gameController.Player.Pos;
                    _logger.LogCommandEvent("FIND_STASH", "SEARCHING", null, new { PlayerPos = playerPos });
                    
                    // Look for stash entities within 300 units (increased from 200)
                    var stashEntities = _gameController.EntityListWrapper.Entities
                        .Where(e => e.Type == EntityType.Stash)
                        .Where(e => e.IsValid && e.IsTargetable)
                        .Where(e => Vector3.Distance(playerPos, e.Pos) < 300)
                        .OrderBy(e => Vector3.Distance(playerPos, e.Pos))
                        .ToList();
                    
                    // Also check for entities that might be named "Stash" or similar
                    var namedStashEntities = _gameController.EntityListWrapper.Entities
                        .Where(e => e.RenderName != null && e.RenderName.Contains("Stash"))
                        .Where(e => e.IsValid && e.IsTargetable)
                        .Where(e => Vector3.Distance(playerPos, e.Pos) < 300)
                        .OrderBy(e => Vector3.Distance(playerPos, e.Pos))
                        .ToList();
                    
                    // Combine and get the closest
                    var allStashEntities = stashEntities.Concat(namedStashEntities).Distinct().ToList();
                    
                    _logger.LogCommandEvent("FIND_STASH", "RESULTS", null, new { 
                        TypedStashes = stashEntities.Count,
                        NamedStashes = namedStashEntities.Count,
                        TotalFound = allStashEntities.Count
                    });
                    
                    var nearestStash = allStashEntities.FirstOrDefault();
                    
                    if (nearestStash != null)
                    {
                        var distance = Vector3.Distance(playerPos, nearestStash.Pos);
                        _logger.LogCommandEvent("FIND_STASH", "FOUND", null, new { 
                            EntityType = nearestStash.Type,
                            RenderName = nearestStash.RenderName,
                            Distance = distance,
                            Position = nearestStash.Pos
                        });
                    }
                    else
                    {
                        _logger.LogCommandEvent("FIND_STASH", "NOT_FOUND", null, new { SearchRadius = 300 });
                    }
                    
                    return nearestStash;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error finding stash: {ex.Message}", ex, "find_stash");
                    return null;
                }
            });
        }
        
        private async Task<bool> OpenStash(Entity stash)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    _logger.LogCommandEvent("OPEN_STASH", "STARTING", null, new { 
                        StashType = stash.Type,
                        StashName = stash.RenderName,
                        StashPos = stash.Pos
                    });
                    
                    // Check if stash is already open
                    if (IsStashOpen())
                    {
                        _logger.LogCommandEvent("OPEN_STASH", "ALREADY_OPEN", null, null);
                        return true;
                    }
                    
                    // Move closer to stash if needed
                    var playerPos = _gameController.Player.Pos;
                    var stashDistance = Vector3.Distance(playerPos, stash.Pos);
                    
                    if (stashDistance > 150)
                    {
                        _logger.LogCommandEvent("OPEN_STASH", "TOO_FAR", null, new { Distance = stashDistance });
                        return false; // Too far to interact
                    }
                    
                    // Click on stash
                    var stashPos = _gameController.Game.IngameState.Camera.WorldToScreen(stash.Pos);
                    var screenPos = new Vector2(stashPos.X, stashPos.Y);
                    
                    // Add random offset for natural mouse movement
                    screenPos.X += _random.Next(-15, 15);
                    screenPos.Y += _random.Next(-15, 15);
                    
                    _logger.LogCommandEvent("OPEN_STASH", "CLICKING", null, new { 
                        WorldPos = stash.Pos,
                        ScreenPos = screenPos
                    });
                    
                    Mouse.SetCursorPos(screenPos);
                    await Task.Delay(_random.Next(100, 200));
                    
                    // Right-click to open
                    Mouse.RightClick(100);
                    await Task.Delay(_random.Next(300, 500));
                    
                    // Wait for stash to open with multiple checks
                    var timeout = DateTime.Now.AddSeconds(8);
                    var checkCount = 0;
                    
                    while (DateTime.Now < timeout)
                    {
                        checkCount++;
                        
                        if (IsStashOpen())
                        {
                            _logger.LogCommandEvent("OPEN_STASH", "SUCCESS", null, new { 
                                ChecksRequired = checkCount,
                                TimeElapsed = (DateTime.Now - timeout.AddSeconds(-8)).TotalMilliseconds
                            });
                            return true;
                        }
                        
                        await Task.Delay(200);
                    }
                    
                    _logger.LogCommandEvent("OPEN_STASH", "TIMEOUT", null, new { 
                        ChecksPerformed = checkCount,
                        TimeoutSeconds = 8
                    });
                    
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error opening stash: {ex.Message}", ex, "open_stash");
                    return false;
                }
            });
        }
        
        private bool IsStashOpen()
        {
            try
            {
                var stashPanel = _gameController.Game.IngameState.IngameUi.StashElement;
                return stashPanel != null && stashPanel.IsVisible;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task<StashingResult> ExecuteStashingOperations(StashConfiguration config)
        {
            var result = new StashingResult();
            
            try
            {
                _logger.LogCommandEvent("STASHING_OPERATIONS", "STARTING", null, config);
                
                var inventory = _gameController.Game.IngameState.IngameUi.InventoryPanel;
                if (inventory == null || !inventory.IsVisible)
                {
                    result.ErrorMessage = "Inventory not accessible";
                    _logger.LogCommandEvent("STASHING_OPERATIONS", "INVENTORY_NOT_ACCESSIBLE", null, new { 
                        InventoryNull = inventory == null,
                        InventoryVisible = inventory?.IsVisible ?? false
                    });
                    return result;
                }
                
                var inventoryItems = GetInventoryItems();
                var itemsToStash = FilterItemsForStashing(inventoryItems, config);
                
                _logger.LogCommandEvent("STASHING_OPERATIONS", "FILTERED_ITEMS", null, new { 
                    TotalItems = inventoryItems.Count,
                    ItemsToStash = itemsToStash.Count,
                    ItemsToStashNames = itemsToStash.Take(5).Select(i => i.GetComponent<Base>()?.Name ?? "Unknown").ToList()
                });
                
                foreach (var item in itemsToStash)
                {
                    var stashResult = await StashSingleItem(item, config);
                    if (stashResult)
                    {
                        result.ItemsProcessed++;
                        await Task.Delay(_settings.ItemPlacementDelay.Value);
                    }
                    else
                    {
                        result.FailedItems.Add(item);
                    }
                }
                
                result.IsSuccess = result.ItemsProcessed > 0;
                
                _logger.LogCommandEvent("STASHING_OPERATIONS", "COMPLETED", null, new { 
                    ItemsProcessed = result.ItemsProcessed,
                    FailedItems = result.FailedItems.Count,
                    IsSuccess = result.IsSuccess
                });
                
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                _logger.LogError($"Error in stashing operations: {ex.Message}", ex, "stashing_operations");
                return result;
            }
        }
        
        private List<Entity> GetInventoryItems()
        {
            try
            {
                _logger.LogCommandEvent("GET_INVENTORY_ITEMS", "STARTING", null, null);
                
                var inventory = _gameController.Game.IngameState?.Data?.ServerData?.PlayerInventories?.FirstOrDefault()?.Inventory;
                if (inventory == null)
                {
                    _logger.LogCommandEvent("GET_INVENTORY_ITEMS", "NO_INVENTORY", null, null);
                    return new List<Entity>();
                }
                
                var inventoryItems = inventory.InventorySlotItems?.Select(item => item.Item).ToList() ?? new List<Entity>();
                
                _logger.LogCommandEvent("GET_INVENTORY_ITEMS", "FOUND_ITEMS", null, new { 
                    ItemCount = inventoryItems.Count,
                    ItemNames = inventoryItems.Take(5).Select(i => i.GetComponent<Base>()?.Name ?? "Unknown").ToList()
                });
                
                return inventoryItems;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error getting inventory items: {ex.Message}", ex, "get_inventory_items");
                return new List<Entity>();
            }
        }
        
        private List<Entity> FilterItemsForStashing(List<Entity> items, StashConfiguration config)
        {
            var filteredItems = new List<Entity>();
            
            foreach (var item in items)
            {
                try
                {
                    var itemComponent = item.GetComponent<Mods>();
                    if (itemComponent == null) continue;
                    
                    var baseComponent = item.GetComponent<Base>();
                    if (baseComponent == null) continue;
                    
                    var itemName = baseComponent.Name;
                    var itemRarity = itemComponent.ItemRarity;
                    
                    // Apply filters
                    if (ShouldStashItem(itemName, itemRarity, config))
                    {
                        filteredItems.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error filtering item: {ex.Message}");
                }
            }
            
            return filteredItems;
        }
        
        private bool ShouldStashItem(string itemName, ItemRarity rarity, StashConfiguration config)
        {
            // Default stashing logic
            if (config.ItemFilters != null && config.ItemFilters.Any())
            {
                // Apply custom filters
                return ApplyItemFilters(itemName, rarity, config.ItemFilters);
            }
            
            // Default rules: stash currency, gems, maps, and rare+ items
            return itemName.Contains("Orb") || 
                   itemName.Contains("Shard") || 
                   itemName.Contains("Gem") || 
                   itemName.Contains("Map") || 
                   rarity >= ItemRarity.Rare;
        }
        
        private bool ApplyItemFilters(string itemName, ItemRarity rarity, Dictionary<string, object> filters)
        {
            // Implement custom filtering logic based on filters
            // This would be expanded based on specific requirements
            return true;
        }
        
        private async Task<bool> StashSingleItem(Entity item, StashConfiguration config)
        {
            try
            {
                _logger.LogCommandEvent("STASH_SINGLE_ITEM", "STARTING", null, new { 
                    ItemId = item.Id,
                    ItemName = item.GetComponent<Base>()?.Name ?? "Unknown"
                });
                
                // Get item position in inventory
                var inventory = _gameController.Game.IngameState?.Data?.ServerData?.PlayerInventories?.FirstOrDefault()?.Inventory;
                if (inventory == null)
                {
                    _logger.LogCommandEvent("STASH_SINGLE_ITEM", "NO_INVENTORY", null, null);
                    return false;
                }
                
                var inventoryItem = inventory.InventorySlotItems?.FirstOrDefault(i => i.Item.Id == item.Id);
                if (inventoryItem == null)
                {
                    _logger.LogCommandEvent("STASH_SINGLE_ITEM", "ITEM_NOT_FOUND_IN_INVENTORY", null, new { 
                        ItemId = item.Id,
                        InventoryItemsCount = inventory.InventorySlotItems?.Count ?? 0
                    });
                    return false;
                }
                
                // Get item screen position from inventory slot
                var itemRect = inventoryItem.GetClientRect();
                var itemPos = itemRect.Center;
                
                _logger.LogCommandEvent("STASH_SINGLE_ITEM", "CLICKING_ITEM", null, new { 
                    ItemPos = itemPos,
                    ItemRect = itemRect
                });
                
                // Move mouse to item
                Mouse.SetCursorPos(itemPos);
                await Task.Delay(_random.Next(50, 150));
                
                // Hold Ctrl and click to move to stash
                Input.KeyDown(Keys.LControlKey);
                await Task.Delay(50);
                Mouse.LeftClick();
                await Task.Delay(100);
                Input.KeyUp(Keys.LControlKey);
                
                // Wait for item to be stashed
                await Task.Delay(_settings.ItemPlacementDelay.Value);
                
                _logger.LogCommandEvent("STASH_SINGLE_ITEM", "SUCCESS", null, new { 
                    ItemId = item.Id,
                    PlacementDelay = _settings.ItemPlacementDelay.Value
                });
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error stashing item: {ex.Message}", ex, "stash_single_item");
                return false;
            }
        }
        
        private async Task CloseStash()
        {
            try
            {
                // Press Escape to close stash
                Input.KeyDown(Keys.Escape);
                await Task.Delay(50);
                Input.KeyUp(Keys.Escape);
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error closing stash: {ex.Message}", ex);
            }
        }
        
        private SellConfiguration ParseSellConfiguration(Dictionary<string, object> commandData)
        {
            var config = new SellConfiguration();
            
            if (commandData.TryGetValue("sellWhiteItems", out var sellWhiteObj) && sellWhiteObj is bool sellWhite)
            {
                config.SellWhiteItems = sellWhite;
            }
            
            if (commandData.TryGetValue("sellBlueItems", out var sellBlueObj) && sellBlueObj is bool sellBlue)
            {
                config.SellBlueItems = sellBlue;
            }
            
            if (commandData.TryGetValue("sellGems", out var sellGemsObj) && sellGemsObj is bool sellGems)
            {
                config.SellGems = sellGems;
            }
            
            if (commandData.TryGetValue("keepValuableItems", out var keepValuableObj) && keepValuableObj is bool keepValuable)
            {
                config.KeepValuableItems = keepValuable;
            }
            
            return config;
        }
        
        private async Task<Entity> FindNearbyVendor()
        {
            return await Task.Run(() =>
            {
                var playerPos = _gameController.Player.Pos;
                return _gameController.EntityListWrapper.Entities
                    .Where(e => e.Type == EntityType.Npc)
                    .Where(e => e.GetComponent<NPC>() != null)
                    .Where(e => Vector3.Distance(playerPos, e.Pos) < 300)
                    .OrderBy(e => Vector3.Distance(playerPos, e.Pos))
                    .FirstOrDefault();
            });
        }
        
        private async Task<bool> OpenVendor(Entity vendor)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    // Click on vendor
                    var vendorPos = _gameController.Game.IngameState.Camera.WorldToScreen(vendor.Pos);
                    var screenPos = new Vector2(vendorPos.X, vendorPos.Y);
                    
                    // Add random offset
                    screenPos.X += _random.Next(-20, 20);
                    screenPos.Y += _random.Next(-20, 20);
                    
                    Mouse.SetCursorPos(screenPos);
                    await Task.Delay(_random.Next(100, 200));
                    
                    // Left-click to interact
                    Mouse.LeftClick();
                    await Task.Delay(_random.Next(300, 500));
                    
                    // Wait for vendor window to open
                    var timeout = DateTime.Now.AddSeconds(5);
                    while (DateTime.Now < timeout)
                    {
                        if (IsVendorOpen())
                            return true;
                        await Task.Delay(100);
                    }
                    
                    return false;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error opening vendor: {ex.Message}", ex);
                    return false;
                }
            });
        }
        
        private bool IsVendorOpen()
        {
            try
            {
                var vendorPanel = _gameController.Game.IngameState.IngameUi.PurchaseWindow;
                return vendorPanel != null && vendorPanel.IsVisible;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task<SellingResult> ExecuteSellingOperations(SellConfiguration config)
        {
            var result = new SellingResult();
            
            try
            {
                var inventoryItems = GetInventoryItems();
                var itemsToSell = FilterItemsForSelling(inventoryItems, config);
                
                foreach (var item in itemsToSell)
                {
                    var sellResult = await SellSingleItem(item);
                    if (sellResult)
                    {
                        result.ItemsProcessed++;
                        await Task.Delay(_settings.ItemSellDelay.Value);
                    }
                    else
                    {
                        result.FailedItems.Add(item);
                    }
                }
                
                result.IsSuccess = result.ItemsProcessed > 0;
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
        
        private List<Entity> FilterItemsForSelling(List<Entity> items, SellConfiguration config)
        {
            var filteredItems = new List<Entity>();
            
            foreach (var item in items)
            {
                try
                {
                    var itemComponent = item.GetComponent<Mods>();
                    if (itemComponent == null) continue;
                    
                    var baseComponent = item.GetComponent<Base>();
                    if (baseComponent == null) continue;
                    
                    var itemName = baseComponent.Name;
                    var itemRarity = itemComponent.ItemRarity;
                    
                    if (ShouldSellItem(itemName, itemRarity, config))
                    {
                        filteredItems.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Error filtering item for selling: {ex.Message}");
                }
            }
            
            return filteredItems;
        }
        
        private bool ShouldSellItem(string itemName, ItemRarity rarity, SellConfiguration config)
        {
            // Don't sell valuable items if configured
            if (config.KeepValuableItems && IsValuableItem(itemName, rarity))
                return false;
            
            // Sell based on rarity settings
            return (config.SellWhiteItems && rarity == ItemRarity.Normal) ||
                   (config.SellBlueItems && rarity == ItemRarity.Magic) ||
                   (config.SellGems && itemName.Contains("Gem"));
        }
        
        private bool IsValuableItem(string itemName, ItemRarity rarity)
        {
            // Define valuable items (currency, high-value uniques, etc.)
            var valuableKeywords = new[] { "Exalted", "Divine", "Mirror", "Eternal", "Ancient" };
            return valuableKeywords.Any(keyword => itemName.Contains(keyword)) || 
                   rarity == ItemRarity.Unique;
        }
        
        private async Task<bool> SellSingleItem(Entity item)
        {
            try
            {
                var inventory = _gameController.Game.IngameState?.Data?.ServerData?.PlayerInventories?.FirstOrDefault()?.Inventory;
                var inventoryItem = inventory?.InventorySlotItems?.FirstOrDefault(i => i.Item.Id == item.Id);
                
                if (inventoryItem == null) return false;
                
                var itemPos = inventoryItem.GetClientRect().Center;
                
                // Move mouse to item
                Mouse.SetCursorPos(itemPos);
                await Task.Delay(_random.Next(50, 150));
                
                // Hold Ctrl and click to sell
                Input.KeyDown(Keys.LControlKey);
                await Task.Delay(50);
                Mouse.LeftClick();
                await Task.Delay(100);
                Input.KeyUp(Keys.LControlKey);
                
                await Task.Delay(_settings.ItemSellDelay.Value);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error selling item: {ex.Message}", ex);
                return false;
            }
        }
        
        private async Task CloseVendor()
        {
            try
            {
                Input.KeyDown(Keys.Escape);
                await Task.Delay(50);
                Input.KeyUp(Keys.Escape);
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error closing vendor: {ex.Message}", ex);
            }
        }
        
        private TradeConfiguration ParseTradeConfiguration(Dictionary<string, object> commandData)
        {
            var config = new TradeConfiguration();
            
            if (commandData.TryGetValue("tradeWhitelist", out var whitelistObj) && whitelistObj is List<object> whitelist)
            {
                config.TrustedTraders = whitelist.Cast<string>().ToList();
            }
            
            if (commandData.TryGetValue("requireLeaderNearby", out var requireLeaderObj) && requireLeaderObj is bool requireLeader)
            {
                config.RequireLeaderNearby = requireLeader;
            }
            
            if (commandData.TryGetValue("maxTradeValue", out var maxValueObj) && maxValueObj is double maxValue)
            {
                config.MaxTradeValue = maxValue;
            }
            
            return config;
        }
        
        private TradeRequest GetActiveTradeRequest()
        {
            try
            {
                var tradePanel = _gameController.Game.IngameState.IngameUi.TradeWindow;
                if (tradePanel == null || !tradePanel.IsVisible)
                    return null;
                
                // Extract trade information from UI
                return new TradeRequest
                {
                    TraderName = "ExtractedTraderName", // Would extract from UI
                    IsActive = true,
                    RequestTime = DateTime.Now
                };
            }
            catch
            {
                return null;
            }
        }
        
        private async Task<TradeValidationResult> ValidateTradeRequest(TradeRequest request, TradeConfiguration config)
        {
            // Validate trader is in whitelist
            if (!config.TrustedTraders.Contains(request.TraderName))
            {
                return new TradeValidationResult { IsValid = false, Reason = "Trader not in whitelist" };
            }
            
            // Validate leader is nearby if required
            if (config.RequireLeaderNearby)
            {
                var leader = GetNearbyLeader();
                if (leader == null)
                {
                    return new TradeValidationResult { IsValid = false, Reason = "Leader not nearby" };
                }
            }
            
            return new TradeValidationResult { IsValid = true };
        }
        
        private Entity GetNearbyLeader()
        {
            var playerPos = _gameController.Player.Pos;
            var leaderNames = _settings.LeaderNames.Value.Split(',').Select(n => n.Trim());
            
            return _gameController.EntityListWrapper.Entities
                .Where(e => e.Type == EntityType.Player)
                .Where(e => e.GetComponent<Player>() != null)
                .Where(e => leaderNames.Contains(e.GetComponent<Player>().PlayerName))
                .Where(e => Vector3.Distance(playerPos, e.Pos) < 1000)
                .FirstOrDefault();
        }
        
        private async Task<TradeResult> ExecuteTradeOperations(TradeRequest request, TradeConfiguration config)
        {
            var result = new TradeResult();
            
            try
            {
                // Accept trade request
                var acceptResult = await AcceptTradeRequest();
                if (!acceptResult)
                {
                    result.ErrorMessage = "Failed to accept trade request";
                    return result;
                }
                
                // Wait for trade window
                var tradeWindowOpened = await WaitForTradeWindow();
                if (!tradeWindowOpened)
                {
                    result.ErrorMessage = "Trade window did not open";
                    return result;
                }
                
                // Validate trade contents
                var validationResult = await ValidateTradeContents(config);
                if (!validationResult.IsValid)
                {
                    result.ErrorMessage = $"Trade validation failed: {validationResult.Reason}";
                    return result;
                }
                
                // Complete trade
                var tradeCompleted = await CompleteTrade();
                result.IsSuccess = tradeCompleted;
                
                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
                return result;
            }
        }
        
        private async Task<bool> AcceptTradeRequest()
        {
            try
            {
                // Find and click accept button
                var tradePanel = _gameController.Game.IngameState.IngameUi.TradeWindow;
                if (tradePanel == null || !tradePanel.IsVisible)
                    return false;
                
                // Implementation would click the accept button
                // This is a simplified version
                Input.KeyDown(Keys.F5); // Assuming F5 accepts trade
                await Task.Delay(50);
                Input.KeyUp(Keys.F5);
                await Task.Delay(500);
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task<bool> WaitForTradeWindow()
        {
            var timeout = DateTime.Now.AddSeconds(_settings.TradeWindowDelay.Value / 1000.0);
            while (DateTime.Now < timeout)
            {
                if (IsTradeWindowOpen())
                    return true;
                await Task.Delay(100);
            }
            return false;
        }
        
        private bool IsTradeWindowOpen()
        {
            try
            {
                var tradeWindow = _gameController.Game.IngameState.IngameUi.TradeWindow;
                return tradeWindow != null && tradeWindow.IsVisible;
            }
            catch
            {
                return false;
            }
        }
        
        private async Task<TradeValidationResult> ValidateTradeContents(TradeConfiguration config)
        {
            // Implement trade content validation
            // This would check items, currency amounts, etc.
            return new TradeValidationResult { IsValid = true };
        }
        
        private async Task<bool> CompleteTrade()
        {
            try
            {
                // Click accept in trade window
                Input.KeyDown(Keys.Space); // Assuming Space accepts trade
                await Task.Delay(50);
                Input.KeyUp(Keys.Space);
                await Task.Delay(500);
                
                return true;
            }
            catch
            {
                return false;
            }
        }
        
        // Helper classes
        public class StashConfiguration
        {
            public List<string> TargetStashTabs { get; set; } = new List<string>();
            public Dictionary<string, object> ItemFilters { get; set; } = new Dictionary<string, object>();
            public bool PreserveSpace { get; set; } = true;
            public int MaxItemsPerTab { get; set; } = 50;
        }
        
        public class SellConfiguration
        {
            public bool SellWhiteItems { get; set; } = true;
            public bool SellBlueItems { get; set; } = false;
            public bool SellGems { get; set; } = false;
            public bool KeepValuableItems { get; set; } = true;
        }
        
        public class TradeConfiguration
        {
            public List<string> TrustedTraders { get; set; } = new List<string>();
            public bool RequireLeaderNearby { get; set; } = true;
            public double MaxTradeValue { get; set; } = 1000;
        }
        
        public class StashingResult
        {
            public bool IsSuccess { get; set; }
            public int ItemsProcessed { get; set; }
            public List<Entity> FailedItems { get; set; } = new List<Entity>();
            public string ErrorMessage { get; set; }
        }
        
        public class SellingResult
        {
            public bool IsSuccess { get; set; }
            public int ItemsProcessed { get; set; }
            public double CurrencyEarned { get; set; }
            public List<Entity> FailedItems { get; set; } = new List<Entity>();
            public string ErrorMessage { get; set; }
        }
        
        public class TradeResult
        {
            public bool IsSuccess { get; set; }
            public string ErrorMessage { get; set; }
        }
        
        public class TradeRequest
        {
            public string TraderName { get; set; }
            public bool IsActive { get; set; }
            public DateTime RequestTime { get; set; }
        }
        
        public class TradeValidationResult
        {
            public bool IsValid { get; set; }
            public string Reason { get; set; }
        }
        
        public class CommandResult
        {
            public bool IsSuccess { get; set; }
            public string Message { get; set; }
            public string ErrorMessage { get; set; }
            
            public static CommandResult Success(string message) => new CommandResult { IsSuccess = true, Message = message };
            public static CommandResult Failed(string error) => new CommandResult { IsSuccess = false, ErrorMessage = error };
            public static CommandResult Disabled(string message) => new CommandResult { IsSuccess = false, Message = message };
        }
    }
} 