﻿using System.Collections.Generic;
using System.Diagnostics;

namespace ChessBot.Search
{
    // todo: it's theoretically possible for 2 states to hash to the same value. should we take care of that?
    /// <summary>
    /// Maps <see cref="State"/> objects to values of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public class TranspositionTable<T>
    {
        private const int DefaultCapacity = 4096;
        private const int EvictionPeriod = 8;

        private readonly Dictionary<ulong, TtNode<T>> _dict;
        private readonly TtLinkedList<T> _nodes;
        private readonly int _capacity;
        private int _numAdds;

        public TranspositionTable() : this(DefaultCapacity) { }

        public TranspositionTable(int capacity)
        {
            _dict = new Dictionary<ulong, TtNode<T>>(capacity);
            _nodes = new TtLinkedList<T>();
            _capacity = capacity;
            _numAdds = 0;
        }

        public bool TryAdd(State state, T value)
        {
            _numAdds++;
            if (_dict.Count == _capacity)
            {
                if ((_numAdds % EvictionPeriod) != 0)
                {
                    return false;
                }
                Evict();
            }

            var node = new TtNode<T>(state.Hash, value);
            _dict.Add(state.Hash, node);
            _nodes.AddToTop(node);
            return true;
        }

        public bool TryGetValue(State state, out T value)
        {
            if (_dict.TryGetValue(state.Hash, out var node))
            {
                // Since we accessed the node, move it to the top
                _nodes.Remove(node);
                _nodes.AddToTop(node);

                value = node.Value;
                return true;
            }

            value = default;
            return false;
        }

        // For now, we're using an LRU cache scheme to decide who gets evicted.
        // In the future, we could take other factors into account such as number of hits, relative depth, etc.
        private void Evict()
        {
            var node = _nodes.RemoveLru();
            bool removed = _dict.Remove(node.Key);
            Debug.Assert(removed);
        }
    }
}