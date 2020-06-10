﻿using ChessBot.Helpers;

namespace ChessBot.Search.Tt
{
    /// <summary>
    /// An entry in the LRU transposition table.
    /// </summary>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    public class LruNode<TValue>
    {
        // dummy initializer for head and tail
        internal LruNode()
        {
        }

        internal LruNode(ulong key, TValue value)
        {
            Key = key;
            Value = value;
        }

        public ulong Key { get; }
        public TValue Value { get; set; }
        public LruNode<TValue> Previous { get; internal set; }
        public LruNode<TValue> Next { get; internal set; }

        internal bool WasRemoved => Previous == null && Next == null;

        public void Remove()
        {
            Previous.Next = Next;
            Next.Previous = Previous;
            Previous = null;
            Next = null;
        }

        public override string ToString()
        {
            var sb = StringBuilderCache.Acquire();
            sb.Append(nameof(Key));
            sb.Append(" = ");
            sb.Append(Key);
            sb.Append(", ");
            sb.Append(nameof(Value));
            sb.Append(" = ");
            sb.Append(Value.ToString());
            return StringBuilderCache.GetStringAndRelease(sb);
        }
    }
}