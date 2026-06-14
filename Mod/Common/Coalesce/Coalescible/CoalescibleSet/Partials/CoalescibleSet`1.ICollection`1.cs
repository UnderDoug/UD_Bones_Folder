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
        , ICollection
        , ICollection<T>
        , IReadOnlyCollection<T>
    //  , ISet<T>
    //  , IList
    //  , IList<T>
    //  , IReadOnlyList<T>
    {
        public int Count => Length;

        public virtual bool IsReadOnly => false;

        public virtual void Clear()
        {
            if (Length > 0)
            {
                Array.Clear(Items, 0, Length);
                Length = 0;
            }
            Variant++;
        }

        public virtual bool Contains(T Item)
            => IndexOf(Item) >= 0;

        public void CopyTo(T[] Array, int ArrayIndex)
            => CopyTo(Array as Array, ArrayIndex);

        public void CopyTo(Array Array, int ArrayIndex)
            => System.Array.Copy(Items, 0, Array, ArrayIndex, Length);

        public bool Remove(T Item)
        {
            if (TryGetIndexOf(Item, out int index))
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }

        #region Explicit Implementaitons

        bool ICollection.IsSynchronized => false;
        object ICollection.SyncRoot => this;

        void ICollection<T>.Add(T Item)
            => Add(Item);

        #endregion
    }
}
