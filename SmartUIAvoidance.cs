using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore;
using ExileCore.PoEMemory.Elements;
using SharpDX;

namespace Follower
{
    /// <summary>
    /// Smart UI Avoidance System that prevents mouse clicks on UI elements
    /// </summary>
    public class SmartUIAvoidance
    {
        private readonly GameController _gameController;
        private readonly FollowerSettings _settings;
        private readonly Random _random = new Random();
        private DateTime _lastUICheck = DateTime.MinValue;
        private List<SharpDX.RectangleF> _cachedUIElements = new List<SharpDX.RectangleF>();
        private Vector2 _safeZoneCenter;
        private float _safeZoneRadius;
        
        public SmartUIAvoidance(GameController gameController, FollowerSettings settings)
        {
            _gameController = gameController;
            _settings = settings;
            UpdateSafeZone();
        }
        
        /// <summary>
        /// Updates the safe zone based on current window size and settings
        /// </summary>
        private void UpdateSafeZone()
        {
            var windowRect = _gameController.Window.GetWindowRectangle();
            _safeZoneCenter = new Vector2(windowRect.X + windowRect.Width / 2, windowRect.Y + windowRect.Height / 2);
            
            // Calculate safe zone radius based on settings
            var smallestDimension = Math.Min(windowRect.Width, windowRect.Height);
            _safeZoneRadius = (smallestDimension / 2) * (_settings.MouseMovementAreaPercent.Value / 100.0f);
        }
        
        /// <summary>
        /// Gets a safe screen position that avoids UI elements
        /// </summary>
        public Vector2 GetSafeScreenPosition(Vector3 worldPosition)
        {
            UpdateSafeZone();
            
            // First convert world position to screen position
            var windowRect = _gameController.Window.GetWindowRectangle();
            var screenPos = _gameController.Game.IngameState.Camera.WorldToScreen(worldPosition);
            var absoluteScreenPos = screenPos + windowRect.Location;
            
            // Step 1: Constrain to safe zone
            var constrainedPos = ConstrainToSafeZone(absoluteScreenPos);
            
            // Step 2: Avoid UI elements
            var uiAvoidedPos = AvoidUIElements(constrainedPos);
            
            // Step 3: Final validation and fallback
            var finalPos = ValidateAndFallback(uiAvoidedPos, worldPosition);
            
            return finalPos;
        }
        
        /// <summary>
        /// Constrains position to the safe zone around screen center
        /// </summary>
        private Vector2 ConstrainToSafeZone(Vector2 position)
        {
            var distanceFromCenter = Vector2.Distance(position, _safeZoneCenter);
            
            if (distanceFromCenter <= _safeZoneRadius)
                return position;
            
            // Position is outside safe zone, constrain it
            var direction = position - _safeZoneCenter;
            var normalizedDirection = Vector2.Normalize(direction);
            var constrainedPos = _safeZoneCenter + normalizedDirection * _safeZoneRadius;
            
            return constrainedPos;
        }
        
        /// <summary>
        /// Avoids UI elements by detecting them and moving away
        /// </summary>
        private Vector2 AvoidUIElements(Vector2 position)
        {
            RefreshUIElements();
            
            // Check if position intersects with any UI element
            var intersectingUI = GetIntersectingUIElement(position);
            if (intersectingUI == null)
                return position;
            
            // Find a safe position away from UI elements
            var safePosition = FindSafePositionAwayFromUI(position, intersectingUI.Value);
            return safePosition;
        }
        
        /// <summary>
        /// Refreshes the cached UI elements list
        /// </summary>
        private void RefreshUIElements()
        {
            // Don't refresh too frequently
            if (DateTime.Now - _lastUICheck < TimeSpan.FromMilliseconds(200))
                return;
            
            _lastUICheck = DateTime.Now;
            _cachedUIElements.Clear();
            
            try
            {
                var ingameUI = _gameController.Game.IngameState.IngameUi;
                if (ingameUI == null) return;
                
                // Add inventory panel
                if (ingameUI.InventoryPanel?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.InventoryPanel.GetClientRect());
                }
                
                // Add stash panel
                if (ingameUI.StashElement?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.StashElement.GetClientRect());
                }
                
                // Add atlas panel
                if (ingameUI.AtlasPanel?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.AtlasPanel.GetClientRect());
                }
                
                // Add skill tree panel
                if (ingameUI.TreePanel?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.TreePanel.GetClientRect());
                }
                
                // Add vendor window
                if (ingameUI.PurchaseWindow?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.PurchaseWindow.GetClientRect());
                }
                
                // Add sell window
                if (ingameUI.SellWindow?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.SellWindow.GetClientRect());
                }
                
                // Add trade window
                if (ingameUI.TradeWindow?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.TradeWindow.GetClientRect());
                }
                
                // Add chat panel
                if (ingameUI.ChatPanel?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.ChatPanel.GetClientRect());
                }
                
                // Flask panel not available in this API version
                // Removed: FlaskPanel check
                
                // Add minimap
                if (ingameUI.Map?.SmallMiniMap?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.Map.SmallMiniMap.GetClientRect());
                }
                
                // Add large map
                if (ingameUI.Map?.LargeMap?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.Map.LargeMap.GetClientRect());
                }
                
                // Add quest tracker
                if (ingameUI.QuestTracker?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.QuestTracker.GetClientRect());
                }
                
                // Add gem level up panel
                if (ingameUI.GemLvlUpPanel?.IsVisible == true)
                {
                    _cachedUIElements.Add(ingameUI.GemLvlUpPanel.GetClientRect());
                }
                
                // Add items on ground labels with padding
                if (ingameUI.ItemsOnGroundLabels != null)
                {
                    foreach (var label in ingameUI.ItemsOnGroundLabels.Where(l => l.IsVisible))
                    {
                        var labelRect = label.Label.GetClientRect();
                        // Add padding around item labels
                        var paddedRect = new SharpDX.RectangleF(
                            labelRect.X - 10, 
                            labelRect.Y - 10, 
                            labelRect.Width + 20, 
                            labelRect.Height + 20
                        );
                        _cachedUIElements.Add(paddedRect);
                    }
                }
                
                // Add additional UI elements as needed
                AddCustomUIExclusionZones();
            }
            catch (Exception ex)
            {
                // Log error but don't throw to prevent plugin crashes
                Console.WriteLine($"SmartUIAvoidance: Error refreshing UI elements - {ex.Message}");
            }
        }
        
        /// <summary>
        /// Adds custom exclusion zones based on settings
        /// </summary>
        private void AddCustomUIExclusionZones()
        {
            var windowRect = _gameController.Window.GetWindowRectangle();
            
            // Add top edge exclusion (for window title bar)
            if (_settings.ExcludeTopEdge.Value)
            {
                _cachedUIElements.Add(new SharpDX.RectangleF(
                    windowRect.X, 
                    windowRect.Y, 
                    windowRect.Width, 
                    _settings.TopEdgeExclusionHeight.Value
                ));
            }
            
            // Add bottom edge exclusion (for taskbar-like elements)
            if (_settings.ExcludeBottomEdge.Value)
            {
                _cachedUIElements.Add(new SharpDX.RectangleF(
                    windowRect.X, 
                    windowRect.Y + windowRect.Height - _settings.BottomEdgeExclusionHeight.Value, 
                    windowRect.Width, 
                    _settings.BottomEdgeExclusionHeight.Value
                ));
            }
            
            // Add left edge exclusion
            if (_settings.ExcludeLeftEdge.Value)
            {
                _cachedUIElements.Add(new SharpDX.RectangleF(
                    windowRect.X, 
                    windowRect.Y, 
                    _settings.LeftEdgeExclusionWidth.Value, 
                    windowRect.Height
                ));
            }
            
            // Add right edge exclusion
            if (_settings.ExcludeRightEdge.Value)
            {
                _cachedUIElements.Add(new SharpDX.RectangleF(
                    windowRect.X + windowRect.Width - _settings.RightEdgeExclusionWidth.Value, 
                    windowRect.Y, 
                    _settings.RightEdgeExclusionWidth.Value, 
                    windowRect.Height
                ));
            }
        }
        
        /// <summary>
        /// Checks if a position intersects with any UI element
        /// </summary>
        private SharpDX.RectangleF? GetIntersectingUIElement(Vector2 position)
        {
            var point = new Vector2(position.X, position.Y);
            
            foreach (var uiRect in _cachedUIElements)
            {
                if (uiRect.Contains(point))
                    return uiRect;
            }
            
            return null;
        }
        
        /// <summary>
        /// Finds a safe position away from UI elements
        /// </summary>
        private Vector2 FindSafePositionAwayFromUI(Vector2 originalPosition, SharpDX.RectangleF intersectingUI)
        {
            var attempts = 0;
            var maxAttempts = 20;
            
            while (attempts < maxAttempts)
            {
                attempts++;
                
                // Generate candidate position
                Vector2 candidatePos;
                
                if (attempts <= 10)
                {
                    // First 10 attempts: try to move away from the intersecting UI element
                    candidatePos = GetPositionAwayFromRect(originalPosition, intersectingUI);
                }
                else
                {
                    // Last 10 attempts: try random positions within safe zone
                    candidatePos = GetRandomPositionInSafeZone();
                }
                
                // Check if this position is safe
                if (GetIntersectingUIElement(candidatePos) == null)
                {
                    return candidatePos;
                }
            }
            
            // If we can't find a safe position, return the center of the safe zone
            return _safeZoneCenter;
        }
        
        /// <summary>
        /// Gets a position away from a specific UI rectangle
        /// </summary>
        private Vector2 GetPositionAwayFromRect(Vector2 originalPosition, SharpDX.RectangleF rect)
        {
            var rectCenter = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            var direction = originalPosition - rectCenter;
            
            // If we're at the center, pick a random direction
            if (direction.Length() < 1)
            {
                var angle = _random.NextDouble() * Math.PI * 2;
                direction = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            }
            else
            {
                direction = Vector2.Normalize(direction);
            }
            
            // Move away from the rectangle
            var distanceFromRect = Math.Max(rect.Width, rect.Height) / 2 + _settings.UIAvoidanceDistance.Value;
            var newPosition = rectCenter + direction * distanceFromRect;
            
            // Constrain to safe zone
            return ConstrainToSafeZone(newPosition);
        }
        
        /// <summary>
        /// Gets a random position within the safe zone
        /// </summary>
        private Vector2 GetRandomPositionInSafeZone()
        {
            var angle = _random.NextDouble() * Math.PI * 2;
            var distance = _random.NextDouble() * _safeZoneRadius * 0.8f; // Use 80% of safe zone radius
            
            var offset = new Vector2(
                (float)(Math.Cos(angle) * distance),
                (float)(Math.Sin(angle) * distance)
            );
            
            return _safeZoneCenter + offset;
        }
        
        /// <summary>
        /// Final validation and fallback for the position
        /// </summary>
        private Vector2 ValidateAndFallback(Vector2 position, Vector3 worldPosition)
        {
            // Add small random offset to prevent clicking on exact same pixel
            var randomOffset = new Vector2(
                (float)(_random.NextDouble() - 0.5) * _settings.MouseRandomOffset.Value,
                (float)(_random.NextDouble() - 0.5) * _settings.MouseRandomOffset.Value
            );
            
            var finalPosition = position + randomOffset;
            
            // Final constraint to safe zone
            finalPosition = ConstrainToSafeZone(finalPosition);
            
            // Ensure we're not on a UI element
            if (GetIntersectingUIElement(finalPosition) != null)
            {
                // Last resort: use safe zone center
                finalPosition = _safeZoneCenter;
            }
            
            return finalPosition;
        }
        
        /// <summary>
        /// Gets debug information about UI elements
        /// </summary>
        public List<SharpDX.RectangleF> GetUIElementsForDebug()
        {
            RefreshUIElements();
            return new List<SharpDX.RectangleF>(_cachedUIElements);
        }
        
        /// <summary>
        /// Checks if a position is in a safe zone
        /// </summary>
        public bool IsPositionSafe(Vector2 position)
        {
            RefreshUIElements();
            return GetIntersectingUIElement(position) == null;
        }
    }
} 