using System.Collections.Generic;

namespace Follower
{
    /// <summary>
    /// Binary heap implementation for efficient A* pathfinding priority queue operations
    /// </summary>
    public class BinaryHeap<TKey, TValue>
    {
        private readonly List<KeyValuePair<TKey, TValue>> _storage = new List<KeyValuePair<TKey, TValue>>();

        /// <summary>
        /// Moves an element up the heap to maintain heap property
        /// </summary>
        private void SieveUp(int startIndex)
        {
            var index = startIndex;
            var nextIndex = (index - 1) / 2;
            while (index != nextIndex)
            {
                if (Compare(index, nextIndex) < 0)
                {
                    Swap(index, nextIndex);
                }
                else
                {
                    return;
                }

                index = nextIndex;
                nextIndex = (index - 1) / 2;
            }
        }

        /// <summary>
        /// Moves an element down the heap to maintain heap property
        /// </summary>
        private void SieveDown(int startIndex)
        {
            var index = startIndex;
            while (index * 2 + 1 < _storage.Count)
            {
                var child1 = index * 2 + 1;
                var child2 = index * 2 + 2;
                int nextIndex;
                if (child2 < _storage.Count)
                {
                    nextIndex = Compare(index, child1) > 0
                                    ? Compare(index, child2) > 0
                                          ? Compare(child1, child2) > 0
                                                ? child2
                                                : child1
                                          : child1
                                    : Compare(index, child2) > 0
                                        ? child2
                                        : index;
                }
                else
                {
                    nextIndex = Compare(index, child1) > 0
                                    ? child1
                                    : index;
                }

                if (nextIndex == index)
                {
                    return;
                }

                Swap(index, nextIndex);
                index = nextIndex;
            }
        }

        /// <summary>
        /// Compares two elements in the heap
        /// </summary>
        private int Compare(int i1, int i2)
        {
            return Comparer<TKey>.Default.Compare(_storage[i1].Key, _storage[i2].Key);
        }

        /// <summary>
        /// Swaps two elements in the heap
        /// </summary>
        private void Swap(int i1, int i2)
        {
            (_storage[i1], _storage[i2]) = (_storage[i2], _storage[i1]);
        }

        /// <summary>
        /// Adds an element to the heap
        /// </summary>
        public void Add(TKey key, TValue value)
        {
            _storage.Add(new KeyValuePair<TKey, TValue>(key, value));
            SieveUp(_storage.Count - 1);
        }

        /// <summary>
        /// Removes and returns the top element from the heap
        /// </summary>
        public bool TryRemoveTop(out KeyValuePair<TKey, TValue> value)
        {
            if (_storage.Count == 0)
            {
                value = default;
                return false;
            }

            value = _storage[0];
            _storage[0] = _storage[^1];
            _storage.RemoveAt(_storage.Count - 1);
            if (_storage.Count > 0)
            {
                SieveDown(0);
            }
            return true;
        }

        /// <summary>
        /// Gets the number of elements in the heap
        /// </summary>
        public int Count => _storage.Count;

        /// <summary>
        /// Checks if the heap is empty
        /// </summary>
        public bool IsEmpty => _storage.Count == 0;

        /// <summary>
        /// Clears all elements from the heap
        /// </summary>
        public void Clear()
        {
            _storage.Clear();
        }
    }
} 