using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using SharpDX;

namespace Follower
{
    public class PathFinder
    {
        private readonly bool[][] _grid;
        private readonly ConcurrentDictionary<Vector2i, Dictionary<Vector2i, float>> ExactDistanceField = new();
        private readonly ConcurrentDictionary<Vector2i, byte[][]> DirectionField = new();
        private readonly int _dimension2;
        private readonly int _dimension1;
        private readonly int[] _pathableValues;

        // Constants for POE terrain
        private const int TILE_SIZE = 23;
        private const float DIAGONAL_COST = 1.414f;
        private const float STRAIGHT_COST = 1.0f;

        public PathFinder(int[][] grid, int[] pathableValues = null)
        {
            _pathableValues = pathableValues ?? new[] { 1, 2, 3, 4, 5 };
            var pv = _pathableValues.ToHashSet();
            _grid = grid.Select(x => x.Select(y => pv.Contains(y)).ToArray()).ToArray();
            _dimension1 = _grid.Length;
            _dimension2 = _grid[0].Length;
        }

        public PathFinder(byte[,] terrainData, int[] pathableValues = null)
        {
            _pathableValues = pathableValues ?? new[] { 1, 2, 3, 4, 5 };
            var pv = _pathableValues.ToHashSet();
            
            _dimension1 = terrainData.GetLength(0);
            _dimension2 = terrainData.GetLength(1);
            
            _grid = new bool[_dimension1][];
            for (int y = 0; y < _dimension1; y++)
            {
                _grid[y] = new bool[_dimension2];
                for (int x = 0; x < _dimension2; x++)
                {
                    _grid[y][x] = pv.Contains(terrainData[y, x]);
                }
            }
        }

        private bool IsTilePathable(Vector2i tile)
        {
            if (tile.X < 0 || tile.X >= _dimension2)
                return false;
            
            if (tile.Y < 0 || tile.Y >= _dimension1)
                return false;
            
            return _grid[tile.Y][tile.X];
        }

        private static readonly List<Vector2i> NeighborOffsets = new List<Vector2i>
        {
            new Vector2i(0, 1),   // North
            new Vector2i(1, 1),   // Northeast
            new Vector2i(1, 0),   // East
            new Vector2i(1, -1),  // Southeast
            new Vector2i(0, -1),  // South
            new Vector2i(-1, -1), // Southwest
            new Vector2i(-1, 0),  // West
            new Vector2i(-1, 1),  // Northwest
        };

        private static IEnumerable<Vector2i> GetNeighbors(Vector2i tile)
        {
            return NeighborOffsets.Select(offset => tile + offset);
        }

        private static float GetExactDistance(Vector2i tile, Dictionary<Vector2i, float> dict)
        {
            return dict.GetValueOrDefault(tile, float.PositiveInfinity);
        }

        private static float GetMovementCost(Vector2i from, Vector2i to)
        {
            var dx = Math.Abs(to.X - from.X);
            var dy = Math.Abs(to.Y - from.Y);
            
            if (dx == 1 && dy == 1)
                return DIAGONAL_COST;
            
            return STRAIGHT_COST;
        }

        public async Task<List<Vector2i>> FindPathAsync(Vector2i start, Vector2i target, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() => FindPath(start, target), cancellationToken);
        }

        public List<Vector2i> FindPath(Vector2i start, Vector2i target)
        {
            if (!IsTilePathable(start) || !IsTilePathable(target))
                return null;

            if (start == target)
                return new List<Vector2i> { target };

            // Try to use cached direction field first
            if (DirectionField.TryGetValue(target, out var directionField))
            {
                return FindPathUsingDirectionField(start, target, directionField);
            }

            // Use A* algorithm
            return FindPathAStar(start, target);
        }

        private List<Vector2i> FindPathUsingDirectionField(Vector2i start, Vector2i target, byte[][] directionField)
        {
            if (directionField[start.Y][start.X] == 0)
                return null;

            var path = new List<Vector2i>();
            var current = start;
            var maxIterations = _dimension1 * _dimension2; // Prevent infinite loops
            var iterations = 0;

            while (current != target && iterations < maxIterations)
            {
                var directionIndex = directionField[current.Y][current.X];
                if (directionIndex == 0)
                    return null;

                var next = NeighborOffsets[directionIndex - 1] + current;
                if (!IsTilePathable(next))
                    return null;

                path.Add(next);
                current = next;
                iterations++;
            }

            return current == target ? path : null;
        }

        private List<Vector2i> FindPathAStar(Vector2i start, Vector2i target)
        {
            var openSet = new BinaryHeap<float, Vector2i>();
            var closedSet = new HashSet<Vector2i>();
            var gScore = new Dictionary<Vector2i, float>();
            var fScore = new Dictionary<Vector2i, float>();
            var cameFrom = new Dictionary<Vector2i, Vector2i>();

            gScore[start] = 0;
            fScore[start] = Heuristic(start, target);
            openSet.Add(fScore[start], start);

            while (openSet.TryRemoveTop(out var currentNode))
            {
                var current = currentNode.Value;

                if (current == target)
                {
                    return ReconstructPath(cameFrom, current);
                }

                closedSet.Add(current);

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (!IsTilePathable(neighbor) || closedSet.Contains(neighbor))
                        continue;

                    var tentativeGScore = gScore[current] + GetMovementCost(current, neighbor);

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = tentativeGScore + Heuristic(neighbor, target);
                        
                        openSet.Add(fScore[neighbor], neighbor);
                    }
                }
            }

            return null; // No path found
        }

        private List<Vector2i> ReconstructPath(Dictionary<Vector2i, Vector2i> cameFrom, Vector2i current)
        {
            var path = new List<Vector2i>();
            
            while (cameFrom.ContainsKey(current))
            {
                path.Add(current);
                current = cameFrom[current];
            }
            
            path.Reverse();
            return path;
        }

        private static float Heuristic(Vector2i a, Vector2i b)
        {
            // Manhattan distance with diagonal movement consideration
            var dx = Math.Abs(a.X - b.X);
            var dy = Math.Abs(a.Y - b.Y);
            return Math.Max(dx, dy) + (DIAGONAL_COST - 1) * Math.Min(dx, dy);
        }

        // Pre-calculate direction fields for commonly used targets
        public async Task PreCalculateDirectionFieldAsync(Vector2i target, CancellationToken cancellationToken = default)
        {
            if (DirectionField.ContainsKey(target))
                return;

            await Task.Run(() => RunDirectionFieldCalculation(target), cancellationToken);
        }

        private void RunDirectionFieldCalculation(Vector2i target)
        {
            if (DirectionField.ContainsKey(target))
                return;

            if (!ExactDistanceField.TryAdd(target, new Dictionary<Vector2i, float>()))
                return;

            var exactDistanceField = ExactDistanceField[target];
            exactDistanceField[target] = 0;
            var localBacktrackDictionary = new Dictionary<Vector2i, Vector2i>();
            var queue = new BinaryHeap<float, Vector2i>();
            queue.Add(0, target);

            void TryEnqueueTile(Vector2i coord, Vector2i previous, float previousScore)
            {
                if (!IsTilePathable(coord) || localBacktrackDictionary.ContainsKey(coord))
                    return;

                localBacktrackDictionary.Add(coord, previous);
                var exactDistance = previousScore + GetMovementCost(previous, coord);
                exactDistanceField.TryAdd(coord, exactDistance);
                queue.Add(exactDistance, coord);
            }

            localBacktrackDictionary.Add(target, target);

            while (queue.TryRemoveTop(out var top))
            {
                var current = top.Value;
                var currentDistance = top.Key;

                foreach (var neighbor in GetNeighbors(current))
                {
                    TryEnqueueTile(neighbor, current, currentDistance);
                }
            }

            // Generate direction grid for fast lookups
            var directionGrid = new byte[_dimension1][];
            for (int y = 0; y < _dimension1; y++)
            {
                directionGrid[y] = new byte[_dimension2];
                for (int x = 0; x < _dimension2; x++)
                {
                    var coordVec = new Vector2i(x, y);
                    if (float.IsPositiveInfinity(GetExactDistance(coordVec, exactDistanceField)))
                    {
                        directionGrid[y][x] = 0;
                        continue;
                    }

                    var neighbors = GetNeighbors(coordVec);
                    var (closestNeighbor, closestDistance) = neighbors
                        .Select(n => (n, distance: GetExactDistance(n, exactDistanceField)))
                        .MinBy(p => p.distance);

                    if (float.IsPositiveInfinity(closestDistance))
                    {
                        directionGrid[y][x] = 0;
                        continue;
                    }

                    var bestDirection = closestNeighbor - coordVec;
                    directionGrid[y][x] = (byte)(1 + NeighborOffsets.IndexOf(bestDirection));
                }
            }

            DirectionField[target] = directionGrid;
            ExactDistanceField.TryRemove(target, out _);
        }

        public void ClearCache()
        {
            ExactDistanceField.Clear();
            DirectionField.Clear();
        }

        public bool HasDirectionField(Vector2i target)
        {
            return DirectionField.ContainsKey(target);
        }

        // Convert world coordinates to grid coordinates
        public static Vector2i WorldToGrid(Vector3 worldPos)
        {
            return new Vector2i((int)(worldPos.X / TILE_SIZE), (int)(worldPos.Y / TILE_SIZE));
        }

        // Convert grid coordinates to world coordinates
        public static Vector3 GridToWorld(Vector2i gridPos)
        {
            return new Vector3(gridPos.X * TILE_SIZE, gridPos.Y * TILE_SIZE, 0);
        }
    }
} 