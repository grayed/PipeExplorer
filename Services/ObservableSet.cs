#region Licensing information
/*
 * Copyright(c) 2020 Vadim Zhukov<zhuk@openbsd.org>
 * 
 * Permission to use, copy, modify, and distribute this software for any
 * purpose with or without fee is hereby granted, provided that the above
 * copyright notice and this permission notice appear in all copies.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS" AND THE AUTHOR DISCLAIMS ALL WARRANTIES
 * WITH REGARD TO THIS SOFTWARE INCLUDING ALL IMPLIED WARRANTIES OF
 * MERCHANTABILITY AND FITNESS.IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR
 * ANY SPECIAL, DIRECT, INDIRECT, OR CONSEQUENTIAL DAMAGES OR ANY DAMAGES
 * WHATSOEVER RESULTING FROM LOSS OF USE, DATA OR PROFITS, WHETHER IN AN
 * ACTION OF CONTRACT, NEGLIGENCE OR OTHER TORTIOUS ACTION, ARISING OUT OF
 * OR IN CONNECTION WITH THE USE OR PERFORMANCE OF THIS SOFTWARE.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using IX.Observable;

namespace PipeExplorer.Services
{
    class ObservableSet<T> : ISet<T>, INotifyCollectionChanged
    {
        private readonly ObservableDictionary<T, Unit> impl;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ObservableSet()
        {
            impl = new ObservableDictionary<T, Unit>();
            impl.CollectionChanged += Impl_CollectionChanged;
        }

        public ObservableSet(int capacity)
        {
            impl = new ObservableDictionary<T, Unit>(capacity);
        }

        private void Impl_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            var oldItems = (e.OldItems as IList<KeyValuePair<T, Unit>>)?.Select(kv => kv.Key).ToList();
            var newItems = (e.NewItems as IList<KeyValuePair<T, Unit>>)?.Select(kv => kv.Key).ToList();
            NotifyCollectionChangedEventArgs newArgs;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    newArgs = new NotifyCollectionChangedEventArgs(e.Action, newItems, e.NewStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Remove:
                    newArgs = new NotifyCollectionChangedEventArgs(e.Action, oldItems, e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Replace:
                    newArgs = new NotifyCollectionChangedEventArgs(e.Action, oldItems, newItems, e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Move:
                    newArgs = new NotifyCollectionChangedEventArgs(e.Action, oldItems, e.NewStartingIndex, e.OldStartingIndex);
                    break;
                case NotifyCollectionChangedAction.Reset:
                    newArgs = new NotifyCollectionChangedEventArgs(e.Action);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(e), e, "unsupported action");
            }
            CollectionChanged?.Invoke(this, newArgs);
        }

        // Trivial

        public void Clear() => impl.Clear();
        public bool Contains(T item) => impl.ContainsKey(item);
        public void CopyTo(T[] array, int arrayIndex) => impl.Keys.CopyTo(array, arrayIndex);
        public int Count => impl.Count;
        public IEnumerator<T> GetEnumerator() => impl.Keys.GetEnumerator();
        public bool IsReadOnly => impl.IsReadOnly;
        public bool Remove(T item) => impl.Remove(item);

        void ICollection<T>.Add(T item) => Add(item);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // Non-trivial

        public bool Add(T item)
        {
            if (!impl.TryGetValue(item, out _))
            {
                impl.Add(item, Unit.Default);
                return true;
            }
            else
            {
                return false;
            }
        }

        public int AddRange(IEnumerable<T> items)
        {
            int cnt = 0;
            foreach (var item in items)
                if (Add(item))
                    cnt++;
            return cnt;
        }

        public void UnionWith(IEnumerable<T> other)
        {
            foreach (var item in other)
                Add(item);
        }

        public void IntersectWith(IEnumerable<T> other)
        {
            foreach (var item in other)
                if (!Contains(item))
                    Remove(item);
        }

        public void ExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public void SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotImplementedException();
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            foreach (var item in this)
                if (!other.Contains(item))
                    return false;
            return true;
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            foreach (var item in other)
                if (!Contains(item))
                    return false;
            return true;
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            if (Count <= other.Count())
                return false;
            return IsSupersetOf(other);
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            if (other.Count() <= Count)
                return false;
            return IsSubsetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            foreach (var item in other)
                if (Contains(item))
                    return true;
            return false;
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            if (Count != other.Count())
                return false;
            foreach (var item in other)
                if (!Contains(item))
                    return false;
            return true;
        }
    }
}
