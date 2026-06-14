using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Diagnostics;

using XRL;
using XRL.Rules;
using XRL.World;
using XRL.Collections;

using static UD_Bones_Folder.Mod.Utils;

using SerializeField = UnityEngine.SerializeField;

namespace UD_Bones_Folder.Mod
{
    [DebuggerDisplay("Count = {Count}")]
    //[Serializable]
    public partial class CoalescibleSet<T>
        : IComposite
    //  , IDisposable
    //  , IEnumerable<T>
    //  , ICollection<T>
    //  , IReadOnlyCollection<T>
    //  , ISet<T>
        , IList
        , IList<T>
        , IReadOnlyList<T>
    {
        public bool IsFixedSize => false;

        public T this[int Index]
        {
            get => Items[Index];
            set
            {
                if (TryGetIndexOf(value, out int valueIndex)
                    && valueIndex != Index)
                    RemoveAt(Index);
                Add(value);
            }
        }

        public static InvalidCastException New_InvalidCastException(string ParamName, object Value)
            => new(message: $"{ParamName}, of {nameof(Type)} {Value.GetType().ToStringWithGenerics()}" +
                $" could not be cast to {nameof(T)} {nameof(Type)} {typeof(T).ToStringWithGenerics()}");

        protected bool CheckValidCastOrThrow(object value, out T TValue)
        {
            TValue = default;
            if (value is null)
                return true;
            else
            if (value is T tValue)
            {
                TValue = tValue;
                return true;
            }
            return false;
        }

        public virtual int IndexOf(T item)
            => Items
                .Select((o, i) => new KeyValuePair<int, T>(i, o)) // convert to IEnumerable of KVP<int, T> where int is Index
                .Aggregate(-1, (a, n) // start with -1 (no item)
                    => EqualityComparer.Equals(n.Value, item) // if (n)ext.Value == Item
                        && a < 0 // but only the first one (should always only be 1)
                    ? n.Key // (a)ccumulator = n.Key (the Index)
                    : a); // otherwise a is unchanged.

        public void RemoveAt(int Index)
        {
            if (Index >= Length)
                throw new ArgumentOutOfRangeException();

            Length--;

            if (Index < Length)
                Array.Copy(Items, Index + 1, Items, Index, Length - Index);

            Items[Length] = default;
            Variant++;
        }

        #region Explicit Implementations

        object IList.this[int Index]
        {
            get => this[Index];
            set
            {
                if (value is null)
                    Remove(this[Index]);
                else
                if (CheckValidCastOrThrow(value, out T tValue))
                    this[Index] = tValue;
            }
        }

        int IList.Add(object value)
        {
            if (!CheckValidCastOrThrow(value, out T tValue))
                throw New_InvalidCastException(nameof(value), value);

            Add(tValue);
            return IndexOf(tValue);
        }

        int IList.IndexOf(object Value)
        {
            if (!CheckValidCastOrThrow(Value, out T tValue))
                throw New_InvalidCastException(nameof(Value), Value);

            return IndexOf(tValue);
        }

        bool IList.Contains(object value)
            => !CheckValidCastOrThrow(value, out T tValue)
            ? throw New_InvalidCastException(nameof(value), value)
            : Contains(tValue);

        /// <summary>
        /// Inserts an item to the <see cref="IList{T}"/> at the specified index.
        /// </summary>
        /// <remarks>
        /// Due to the arbitrary nature of the order of the elements within the set, this method falls back to the set's implementation of <see cref="Add(T)"/>.<br/>
        /// <inheritdoc cref="Add(T)"/>
        /// </remarks>
        /// <param name="Index">Unused; The position in the set at which to add <paramref name="Item"/></param>
        /// <param name="Item"><inheritdoc cref="Add(T)"/></param>
        void IList<T>.Insert(int Index, T Item)
            => Add(Item);

        /// <summary>
        /// Inserts an item to the <see cref="IList"/> at the specified index (provided it can be cast to <typeparamref name="T"/>).
        /// </summary>
        /// <remarks>
        /// Due to the arbitrary nature of the order of the elements within the set, this method falls back to the set's implementation of <see cref="Add(T)"/>.<br/>
        /// <inheritdoc cref="Add(T)"/>
        /// </remarks>
        /// <param name="Index">Unused; The position in the set at which to add <paramref name="Value"/>.</param>
        /// <param name="Value"><inheritdoc cref="Add(T)"/></param>
        void IList.Insert(int Index, object Value)
        {
            if (CheckValidCastOrThrow(Value, out T tValue))
                Add(tValue);
        }

        void IList.Remove(object Value)
        {
            if (CheckValidCastOrThrow(Value, out T tValue))
                Remove(tValue);
        }

        #endregion
    }
}
