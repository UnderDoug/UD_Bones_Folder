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
        , ISet<T>
    //  , IList
    //  , IList<T>
    //  , IReadOnlyList<T>
    {
        /// <summary>
        /// Adds a unique element to the underlying <see cref="T[]"/> after first ensuring that it has the capacity for a new entry, then updates the <see cref="Version"/> of the current set to ensure the integrity of any enumeration.
        /// </summary>
        /// <remarks>
        /// This method assumes the necessary steps have already been taken to ensure that the current set does not already contain a copy of <paramref name="Item"/>.
        /// </remarks>
        /// <param name="Item">The unique item to add to the current set.</param>
        /// <exception cref="InvalidOperationException">
        ///     <paramref name="Item"/> already exists in the current set.</exception>
        protected void InternalAdd(T Item)
        {
            if (TryGetIndexOf(Item, out int index)
                && Items[index] is T itemAtIndex)
                throw new InvalidOperationException(
                    message: $"A {typeof(CoalescibleSet<T>).ToStringWithGenerics()} (" +
                        $"{GetType().ToStringWithGenerics()}) cannot contain duplicate entries. " +
                        $"{nameof(Item)} ({Item?.ToString() ?? "null"}) already present ({itemAtIndex?.ToString() ?? "null"}).");

            EnsureCapacity(Length + 1);
            Items[Length++] = Item;
            Variant++;
        }

        /// <summary>
        /// Adds or coalesces an element into the current set and returns a value to indicate if the element was successfully added.
        /// </summary>
        /// <remarks>
        /// If <paramref name="Item"/> already exists in the set, instead of simply not being added, the existing element is replaced with the result of <see cref="Coalescer{T}.Coalesce(T,T)"/> being called on the existing element and <paramref name="Item" />.
        /// </remarks>
        /// <param name="Item">The element to add or coalesc into the set.</param>
        /// <returns>
        ///   <see langword="true" /> if the element is added to the set;<br/>
        ///   <see langword="false" /> if the element is already in the set and the existing element is replaced with the result of <see cref="Coalescer{T}.Coalesce(T,T)"/> being called on the existing element and <paramref name="Item" />.</returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Item" /> is <see langword="null" />.</exception>
        public virtual bool Add(T Item)
        {
            if (Item == null)
                throw new ArgumentNullException(nameof(Item));

            bool isNew = true;
            T itemToAdd = Item;
            if (TryGetIndexOf(Item, out int index)
                && Items[index] is T itemAtIndex)
            {
                RemoveAt(index);
                itemToAdd = Coalescer.Coalesce(itemAtIndex, itemToAdd);
                isNew = false;
            }
            InternalAdd(itemToAdd);
            return isNew;
        }

        /// <summary>
        /// Modifies the current set so that it contains all elements that are present in the current set, in the specified collection, or in both.
        /// </summary>
        /// <remarks>
        /// Where an element exists in both collections, <see cref="Coalescer{T}.Coalesce(T,T)"/> will be called on the existing element the matching one in <paramref name="Other"/>.
        /// </remarks>
        /// <param name="Other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual void UnionWith(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));
            
            AddRange(Other);
        }

        /// <summary>
        /// Modifies the current set so that it contains only elements that are also in a specified collection.
        /// </summary>
        /// <param name="Other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual void IntersectWith(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));

            UnionWith(Other);
            IEnumerable<T> notInOther = this.Where(e => !Other.Contains(e));
            foreach (T element in notInOther)
                Remove(element);
        }

        /// <summary>
        /// Removes all elements in the specified collection from the current set.
        /// </summary>
        /// <param name="Other">The collection of items to remove from the set.</param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual void ExceptWith(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));

            IEnumerable<T> inOther = this.Where(e => Other.Contains(e));
            foreach (T element in inOther)
                Remove(element);
        }

        /// <summary>
        /// Modifies the current set so that it contains only elements that are present either in the current set or in the specified collection, but not both.
        /// </summary>
        /// <param name="Other">The collection to compare to the current set.</param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual void SymmetricExceptWith(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));

            if (Count == 0)
                UnionWith(Other);
            else
            if (Other == this)
                Clear();
            else
            {
                IEnumerable<T> inBoth = this.Where(e => Other.Contains(e));
                IEnumerable<T> onlyInThis = this.Where(e => !inBoth.Contains(e));
                ExceptWith(inBoth);
                if (Other.Where(e => !inBoth.Contains(e)) is IEnumerable<T> onlyInOther
                    && !onlyInOther.IsNullOrEmpty())
                    AddRange(onlyInOther);  
            }
        }

        /// <summary>
        /// Determines whether a set is a subset of a specified collection.
        /// </summary>
        /// <param name="Other">The collection to compare to the current set.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual bool IsSubsetOf(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));

            return this.All(e => Other.Contains(e));
        }

        /// <summary>
        /// Determines whether the current set is a superset of a specified collection.
        /// </summary>
        /// <param name="Other">The collection to compare to the current set.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual bool IsSupersetOf(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));

            return Other.All(e => Contains(e));
        }

        /// <summary>
        /// Determines whether the current set is a proper (strict) superset of a specified collection.
        /// </summary>
        /// <param name="Other">The collection to compare to the current set.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual bool IsProperSupersetOf(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));

            return IsSupersetOf(Other)
                && !this.All(e => Other.Contains(e));
        }

        /// <summary>
        /// Determines whether the current set is a proper (strict) subset of a specified collection.
        /// </summary>
        /// <param name="Other">The collection to compare to the current set.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual bool IsProperSubsetOf(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));

            return IsSubsetOf(Other)
                && !Other.All(e => Contains(e));
        }

        /// <summary>
        /// Determines whether the current set overlaps with the specified collection.
        /// </summary>
        /// <param name="Other">The collection to compare to the current set.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual bool Overlaps(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));

            return Other.Any(e => Contains(e));
        }

        /// <summary>
        /// Determines whether the current set and the specified collection contain the same elements.
        /// </summary>
        /// <param name="Other">The collection to compare to the current set.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="Other" /> is <see langword="null" />.</exception>
        public virtual bool SetEquals(IEnumerable<T> Other)
        {
            if (Other == null)
                throw new ArgumentNullException(nameof(Other));

            return Other.All(e => Contains(e))
                && this.All(e => Other.Contains(e));
        }
    }
}
