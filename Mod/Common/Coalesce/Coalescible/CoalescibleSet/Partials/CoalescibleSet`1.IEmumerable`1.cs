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
        , IEnumerable<T>
    //  , ICollection<T>
    //  , IReadOnlyCollection<T>
    //  , ISet<T>
    //  , IList
    //  , IList<T>
    //  , IReadOnlyList<T>
    {
        [Serializable]
        public struct Enumerator : IEnumerator<T>, IEnumerator, IDisposable
        {
            public static InvalidOperationException CollectionModifiedException => new("Collection was modified. Enmumation can't continue.");

            private CoalescibleSet<T> CoalescibleSet;
            private T[] Items;
            private int Index;
            private int Variant;

            public readonly T Current => Items[Index];

            readonly object IEnumerator.Current => Current;

            public Enumerator(CoalescibleSet<T> CoalescibleSet)
            {
                this.CoalescibleSet = CoalescibleSet;

                Items = new T[CoalescibleSet.Count];
                for (int i = 0; i < CoalescibleSet.Count; i++)
                    Items[i] = CoalescibleSet.Items[i];

                Index = -1;
                Variant = CoalescibleSet.Variant;
            }

            public bool MoveNext()
            {
                if (Variant != CoalescibleSet.Variant)
                    throw CollectionModifiedException;

                return ++Index < Items.Length
                    && Current is not null
                    ;
            }

            public void Reset()
            {
                if (Variant != CoalescibleSet.Variant)
                    throw CollectionModifiedException;

                Index = -1;
            }

            public void Dispose()
            {
                CoalescibleSet = null;
                Items = null;
                Index = default;
                Variant = default;
            }
        }

        public IEnumerator<T> GetEnumerator()
            => new Enumerator(this);

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
