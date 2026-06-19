using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

using UD_Bones_Folder.Mod.Coalescence;

using static UD_Bones_Folder.Mod.Utils;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    internal class GenericCoalescer<T> : Coalescer<T> where T : ICoalescible<T>
    {
        public GenericCoalescer()
            : base(CoalesceMethod.TypeDefined)
        { }

        private NotSupportedException UseTypeDefined_NotSupportedException()
            => new (
                message: $"{CallChain(typeof(ICoalescible<T>).ToStringWithGenerics(), nameof(ICoalescible<T>.Coalesce))} " +
                    $"set to {CallChain(nameof(Coalescence.CoalesceMethod), CoalesceMethod.TypeDefined.ToString())} " +
                    $"should handle {nameof(Coalesce)} for this {GetType().ToStringWithGenerics()}.");

        public override T CoalesceTypeDefined(T x, T y)
            => x.Coalesce(y);

        public override T CoalesceFirst(T x, T y)
            => throw UseTypeDefined_NotSupportedException();

        public override T CoalesceSecond(T x, T y)
            => throw UseTypeDefined_NotSupportedException();

        public override T CoalesceGreater(T x, T y)
            => throw UseTypeDefined_NotSupportedException();

        public override T CoalesceLesser(T x, T y)
            => throw UseTypeDefined_NotSupportedException();

        public override T CoalesceCombine(T x, T y)
            => throw UseTypeDefined_NotSupportedException();

        public override T CoalesceDifference(T x, T y)
            => throw UseTypeDefined_NotSupportedException();
    }
}
