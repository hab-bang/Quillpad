// ---------------------------------------------------------------------------------------------
//  Copyright (c) 2026 hab-bang. All rights reserved.
//  See LICENSE file in the project root for license information.
// ---------------------------------------------------------------------------------------------

namespace Notepads.Utilities
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The files of recently closed tabs, newest first, for Ctrl+Shift+T. A file closed twice is kept once.
    /// </summary>
    public sealed class ClosedTabHistory<T>
    {
        private readonly LinkedList<T> _items = new LinkedList<T>();
        private readonly Func<T, string> _keySelector;
        private readonly int _capacity;

        /// <param name="keySelector">Tells which items are the same file, ignoring case like Windows paths.</param>
        public ClosedTabHistory(int capacity, Func<T, string> keySelector)
        {
            _capacity = Math.Max(1, capacity);
            _keySelector = keySelector;
        }

        public int Count => _items.Count;

        public void Push(T item)
        {
            var key = _keySelector(item);

            for (var node = _items.First; node != null; node = node.Next)
            {
                if (string.Equals(_keySelector(node.Value), key, StringComparison.OrdinalIgnoreCase))
                {
                    _items.Remove(node);
                    break;
                }
            }

            _items.AddFirst(item);
            if (_items.Count > _capacity) _items.RemoveLast();
        }

        public bool TryPop(out T item)
        {
            if (_items.Count == 0)
            {
                item = default;
                return false;
            }

            item = _items.First.Value;
            _items.RemoveFirst();
            return true;
        }
    }
}
