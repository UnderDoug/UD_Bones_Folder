using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

using UD_Bones_Folder.Mod.Coalescence;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    internal class ObjectCoalescer<T> : Coalescer<T>
    {
        public ObjectCoalescer()
            : base()
        { }
        public ObjectCoalescer(CoalesceMethod CoalesceMethod)
            : base(CoalesceMethod)
        { }

        public override T CoalesceFirst(T x, T y)
            => x;

        public override T CoalesceSecond(T x, T y)
            => y;

        public override T CoalesceGreater(T x, T y)
            => CoalesceGreaterInternal(x, y);

        public override T CoalesceLesser(T x, T y)
            => CoalesceLesserInternal(x, y);

        public override T CoalesceCombine(T x, T y)
            => throw Nonsense_NotSupportedException();

        public override T CoalesceDifference(T x, T y)
            => throw Nonsense_NotSupportedException();
    }
}
