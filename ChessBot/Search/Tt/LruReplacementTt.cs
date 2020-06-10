﻿using ChessBot.Helpers;
using System.Collections.Generic;
using System.Diagnostics;

namespace ChessBot.Search.Tt
{
    /// <summary>
    /// Maps <see cref="IState"/> objects to values of type <typeparamref name="TValue"/>. Uses an LRU eviction policy.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class LruReplacementTt<TValue>
    {
        private const int DefaultCapacity = 4096;

        private readonly Dictionary<ulong, LruNode<TValue>> _dict;
        private readonly int _capacity;
        private readonly LruLinkedList<TValue> _nodes;

        public LruReplacementTt() : this(DefaultCapacity) { }

        public LruReplacementTt(int capacity)
        {
            _dict = new Dictionary<ulong, LruNode<TValue>>(capacity);
            _capacity = capacity;
            _nodes = new LruLinkedList<TValue>();
        }

        public bool Add<TState>(TState state, TValue value) where TState : IState
        {
            if (_dict.Count == _capacity)
            {
                Evict();
            }

            var node = new LruNode<TValue>(state.Hash, value);
            if (!_dict.TryAdd(state.Hash, node))
            {
                // although rare, this could happen if a state is not in the table during the initial lookup, but is
                // populated during a recursive call as it searches its children. afterwards, there will be a conflict
                // when it tries to call Add() with an existing key. our behavior is to favor the newer entry since it
                // probably contains information about a greater depth.
                //
                // this could also theoretically happen in the case of a hash collision, although that's very unlikely.
                var existingNode = _dict[state.Hash];
                Log.Debug("Evicting node {0} in favor of {1}", existingNode, node);
                Evict(existingNode);
                _dict.Add(state.Hash, node);
            }
            _nodes.AddToTop(node);
            return true;
        }

        public void Touch(LruNode<TValue> node)
        {
            Debug.Assert(_dict.ContainsKey(node.Key));
            Debug.Assert(_dict[node.Key] == node);

            Log.Debug("Node {0} was hit, moving to top of cache", node);
            node.Remove();
            _nodes.AddToTop(node);
        }

        public bool TryGetNode<TState>(TState state, out LruNode<TValue> node) where TState : IState
        {
            return _dict.TryGetValue(state.Hash, out node);
        }

        // For now, we're using an LRU cache scheme to decide who gets evicted.
        // In the future, we could take other factors into account such as number of hits, relative depth, etc.
        private void Evict()
        {
            var lru = _nodes.Lru;
            Log.Debug("Evicting lru node {0}", lru);
            Evict(lru);
        }

        private void Evict(LruNode<TValue> node)
        {
            node.Remove();
            bool removed = _dict.Remove(node.Key);
            Debug.Assert(removed);
        }
    }
}