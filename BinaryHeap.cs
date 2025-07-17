using System;
using System.Collections.Generic;

namespace Follower
{
    public class BinaryHeap<TKey, TValue> where TKey : IComparable<TKey>
    {
        private readonly List<(TKey Key, TValue Value)> _heap = new List<(TKey, TValue)>();

        public int Count => _heap.Count;

        public void Add(TKey key, TValue value)
        {
            _heap.Add((key, value));
            HeapifyUp(_heap.Count - 1);
        }

        public bool TryRemoveTop(out (TKey Key, TValue Value) result)
        {
            if (_heap.Count == 0)
            {
                result = default;
                return false;
            }

            result = _heap[0];
            
            if (_heap.Count == 1)
            {
                _heap.Clear();
                return true;
            }

            _heap[0] = _heap[_heap.Count - 1];
            _heap.RemoveAt(_heap.Count - 1);
            HeapifyDown(0);
            
            return true;
        }

        private void HeapifyUp(int index)
        {
            while (index > 0)
            {
                int parentIndex = (index - 1) / 2;
                if (_heap[index].Key.CompareTo(_heap[parentIndex].Key) >= 0)
                    break;

                Swap(index, parentIndex);
                index = parentIndex;
            }
        }

        private void HeapifyDown(int index)
        {
            int leftChild = 2 * index + 1;
            int rightChild = 2 * index + 2;
            int smallest = index;

            if (leftChild < _heap.Count && _heap[leftChild].Key.CompareTo(_heap[smallest].Key) < 0)
                smallest = leftChild;

            if (rightChild < _heap.Count && _heap[rightChild].Key.CompareTo(_heap[smallest].Key) < 0)
                smallest = rightChild;

            if (smallest != index)
            {
                Swap(index, smallest);
                HeapifyDown(smallest);
            }
        }

        private void Swap(int i, int j)
        {
            var temp = _heap[i];
            _heap[i] = _heap[j];
            _heap[j] = temp;
        }

        public void Clear()
        {
            _heap.Clear();
        }
    }
} 