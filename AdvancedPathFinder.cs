using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SharpDX;
using ExileCore;

namespace Follower
{
    /// <summary>
    /// Node structure for A* pathfinding algorithm
    /// </summary>
    public struct PathNode
    {
        public Vector2 Position { get; set; }
        public float GCost { get; set; }  // Cost from start node
        public float HCost { get; set; }  // Heuristic cost to target
        public float FCost => GCost + HCost;  // Total cost
        public Vector2 Parent { get; set; }
        public bool IsWalkable { get; set; }
    }

    /// <summary>
    /// Advanced pathfinding system with A* algorithm and distance fields for efficient leader following
    /// </summary>
    public class AdvancedPathFinder
    {
        private readonly bool[][] _grid;
        private readonly int _width;
        private readonly int _height;
        private readonly GameController _gameController;
        private readonly FollowerSettings _settings;
        
        // Distance field caching for performance
        private readonly ConcurrentDictionary<Vector2, Dictionary<Vector2, float>> _exactDistanceFields = new();
        private readonly ConcurrentDictionary<Vector2, byte[][]> _directionFields = new();
        
        // Neighbor offsets for 8-directional movement
        private static readonly Vector2[] NeighborOffsets = new Vector2[]
        {
            new Vector2(0, 1),   // North
            new Vector2(1, 1),   // Northeast
            new Vector2(1, 0),   // East
            new Vector2(1, -1),  // Southeast
            new Vector2(0, -1),  // South
            new Vector2(-1, -1), // Southwest
            new Vector2(-1, 0),  // West
            new Vector2(-1, 1),  // Northwest
        };

        public AdvancedPathFinder(byte[,] terrainData, GameController gameController, FollowerSettings settings)
        {
            _gameController = gameController;
            _settings = settings;
            _height = terrainData.GetLength(0);
            _width = terrainData.GetLength(1);
            
            // Convert terrain data to walkability grid
            _grid = new bool[_height][];
            for (int y = 0; y < _height; y++)
            {
                _grid[y] = new bool[_width];
                for (int x = 0; x < _width; x++)
                {
                    // Consider terrain values 1, 2 as walkable (updated for enhanced terrain processing)
                    // Value 1 = highly walkable (from processed terrain values 4,5)
                    // Value 2 = walkable but may require dash (from processed terrain values 1,2,3)
                    // Value 255 = blocked (from processed terrain value 0 or other non-walkable values)
                    _grid[y][x] = terrainData[y, x] is 1 or 2;
                }
            }
        }

        /// <summary>
        /// Checks if a tile is walkable
        /// </summary>
        private bool IsWalkable(Vector2 position)
        {
            int x = (int)position.X;
            int y = (int)position.Y;
            
            if (x < 0 || x >= _width || y < 0 || y >= _height)
                return false;
                
            return _grid[y][x];
        }

        /// <summary>
        /// Gets neighboring positions around a given position
        /// </summary>
        private IEnumerable<Vector2> GetNeighbors(Vector2 position)
        {
            return NeighborOffsets.Select(offset => position + offset);
        }

        /// <summary>
        /// Calculates the distance between two positions
        /// </summary>
        private float CalculateDistance(Vector2 from, Vector2 to)
        {
            var dx = Math.Abs(from.X - to.X);
            var dy = Math.Abs(from.Y - to.Y);
            
            // Use octile distance for 8-directional movement
            return (float)(Math.Sqrt(2) * Math.Min(dx, dy) + Math.Abs(dx - dy));
        }

        /// <summary>
        /// Runs A* pathfinding algorithm to find optimal path
        /// </summary>
        public List<Vector2> FindPath(Vector2 start, Vector2 target)
        {
            // Check if we have a cached direction field for this target
            if (_directionFields.TryGetValue(target, out var directionField))
            {
                return GetPathFromDirectionField(start, target, directionField);
            }

            // Check if we have cached distance field for this target
            if (_exactDistanceFields.TryGetValue(target, out var exactDistanceField))
            {
                return GetPathFromDistanceField(start, target, exactDistanceField);
            }

            // Run full A* pathfinding
            return RunAStarPathfinding(start, target);
        }

        /// <summary>
        /// Runs full A* pathfinding algorithm
        /// </summary>
        private List<Vector2> RunAStarPathfinding(Vector2 start, Vector2 target)
        {
            var openSet = new BinaryHeap<float, Vector2>();
            var closedSet = new HashSet<Vector2>();
            var cameFrom = new Dictionary<Vector2, Vector2>();
            var gScore = new Dictionary<Vector2, float>();
            var fScore = new Dictionary<Vector2, float>();

            // Initialize start node
            gScore[start] = 0;
            fScore[start] = CalculateDistance(start, target);
            openSet.Add(fScore[start], start);

            var stopwatch = Stopwatch.StartNew();
            var maxIterations = _settings.MaxPathfindingIterations.Value;
            var iterations = 0;

            while (!openSet.IsEmpty && iterations < maxIterations)
            {
                iterations++;
                
                // Get node with lowest f score
                if (!openSet.TryRemoveTop(out var current))
                    break;

                var currentPos = current.Value;
                
                // Check if we reached the target
                if (Vector2.Distance(currentPos, target) < 2f)
                {
                    // Reconstruct path
                    var path = ReconstructPath(cameFrom, currentPos);
                    
                    // Cache the distance field for future use
                    CacheDistanceField(target, gScore);
                    
                    return path;
                }

                closedSet.Add(currentPos);

                // Explore neighbors
                foreach (var neighbor in GetNeighbors(currentPos))
                {
                    if (!IsWalkable(neighbor) || closedSet.Contains(neighbor))
                        continue;

                    var tentativeGScore = gScore.GetValueOrDefault(currentPos, float.MaxValue) + 
                                        CalculateDistance(currentPos, neighbor);

                    if (tentativeGScore < gScore.GetValueOrDefault(neighbor, float.MaxValue))
                    {
                        cameFrom[neighbor] = currentPos;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + CalculateDistance(neighbor, target);
                        
                        openSet.Add(fScore[neighbor], neighbor);
                    }
                }

                // Time-based yielding to prevent blocking
                if (stopwatch.ElapsedMilliseconds > 50)
                {
                    break;
                }
            }

            // No path found
            return null;
        }

        /// <summary>
        /// Reconstructs the path from the A* algorithm results
        /// </summary>
        private List<Vector2> ReconstructPath(Dictionary<Vector2, Vector2> cameFrom, Vector2 current)
        {
            var path = new List<Vector2> { current };
            
            while (cameFrom.ContainsKey(current))
            {
                current = cameFrom[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// Caches distance field for future pathfinding queries
        /// </summary>
        private void CacheDistanceField(Vector2 target, Dictionary<Vector2, float> gScore)
        {
            // Cache the distance field for reuse
            _exactDistanceFields[target] = new Dictionary<Vector2, float>(gScore);
            
            // If the distance field is small enough, convert to direction field for memory efficiency
            if (gScore.Count * 8 > _width * _height)  // 8 bytes per entry vs 1 byte per grid cell
            {
                ConvertToDirectionField(target, gScore);
            }
        }

        /// <summary>
        /// Converts distance field to direction field for memory efficiency
        /// </summary>
        private void ConvertToDirectionField(Vector2 target, Dictionary<Vector2, float> distanceField)
        {
            var directionGrid = new byte[_height][];
            
            Parallel.For(0, _height, y =>
            {
                directionGrid[y] = new byte[_width];
                for (int x = 0; x < _width; x++)
                {
                    var pos = new Vector2(x, y);
                    if (!distanceField.ContainsKey(pos))
                    {
                        directionGrid[y][x] = 0; // No direction
                        continue;
                    }

                    var neighbors = GetNeighbors(pos).ToList();
                    var bestNeighbor = neighbors
                        .Where(n => distanceField.ContainsKey(n))
                        .OrderBy(n => distanceField[n])
                        .FirstOrDefault();

                    if (bestNeighbor != default)
                    {
                        var direction = bestNeighbor - pos;
                        var directionIndex = Array.IndexOf(NeighborOffsets, direction);
                        directionGrid[y][x] = (byte)(directionIndex + 1);
                    }
                    else
                    {
                        directionGrid[y][x] = 0;
                    }
                }
            });

            _directionFields[target] = directionGrid;
            _exactDistanceFields.TryRemove(target, out _);
        }

        /// <summary>
        /// Gets path from cached direction field
        /// </summary>
        private List<Vector2> GetPathFromDirectionField(Vector2 start, Vector2 target, byte[][] directionField)
        {
            var path = new List<Vector2>();
            var current = start;
            var maxSteps = _settings.MaxPathfindingIterations.Value;
            var steps = 0;

            while (Vector2.Distance(current, target) > 2f && steps < maxSteps)
            {
                steps++;
                
                int x = (int)current.X;
                int y = (int)current.Y;
                
                if (x < 0 || x >= _width || y < 0 || y >= _height)
                    break;

                var directionIndex = directionField[y][x];
                if (directionIndex == 0)
                    break;

                var direction = NeighborOffsets[directionIndex - 1];
                current = current + direction;
                path.Add(current);
            }

            return path.Count > 0 ? path : null;
        }

        /// <summary>
        /// Gets path from cached distance field
        /// </summary>
        private List<Vector2> GetPathFromDistanceField(Vector2 start, Vector2 target, Dictionary<Vector2, float> distanceField)
        {
            var path = new List<Vector2>();
            var current = start;
            var maxSteps = _settings.MaxPathfindingIterations.Value;
            var steps = 0;

            while (Vector2.Distance(current, target) > 2f && steps < maxSteps)
            {
                steps++;
                
                var neighbors = GetNeighbors(current);
                var bestNeighbor = neighbors
                    .Where(n => distanceField.ContainsKey(n))
                    .OrderBy(n => distanceField[n])
                    .FirstOrDefault();

                if (bestNeighbor == default)
                    break;

                current = bestNeighbor;
                path.Add(current);
            }

            return path.Count > 0 ? path : null;
        }

        /// <summary>
        /// Clears cached pathfinding data
        /// </summary>
        public void ClearCache()
        {
            _exactDistanceFields.Clear();
            _directionFields.Clear();
        }

        /// <summary>
        /// Gets debug information about cached paths
        /// </summary>
        public (int DistanceFields, int DirectionFields) GetCacheInfo()
        {
            return (_exactDistanceFields.Count, _directionFields.Count);
        }
    }
} 