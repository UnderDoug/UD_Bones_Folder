using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using UD_Bones_Folder.Mod.Coalescence;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    internal class EnumCoalescer<T, U> : Coalescer<T>
        where T : struct, Enum
        where U : IComparable
    {
        public EnumCoalescer()
            : base()
        { }
        public EnumCoalescer(CoalesceMethod Method)
            : base(Method)
        { }

        public override T CoalesceFirst(T x, T y)
            => x;

        public override T CoalesceSecond(T x, T y)
            => y;

        public override T CoalesceCombine(T x, T y)
        {
            if (!typeof(T).GetCustomAttributes(false).Contains(typeof(FlagsAttribute)))
                throw Nonsense_NotSupportedException();

            if (!x.TryConvertType(out U xUnderlying)
                || !y.TryConvertType(out U yUnderlying))
                throw UnderlyingType_InvalidCastException();

            Type uType = typeof(U);
            if (uType.GetMethod("Or", new[] { uType, uType, }) is not MethodInfo orMethod
                || orMethod.Invoke(null, new object[] { xUnderlying, yUnderlying }) is not T combined)
                throw UnderlyingType_InvalidCastException();

            return combined;
        }

        public override T CoalesceDifference(T x, T y)
        {
            if (!typeof(T).GetCustomAttributes(false).Contains(typeof(FlagsAttribute)))
                throw Nonsense_NotSupportedException();

            if (!x.TryConvertType(out U xUnderlying)
                || !y.TryConvertType(out U yUnderlying))
                throw UnderlyingType_InvalidCastException();

            Type uType = typeof(U);
            if (uType.GetMethod("OnesComplement", Type.EmptyTypes) is not MethodInfo notMethod
                || notMethod.Invoke(null, new object[] { yUnderlying }) is not U notU
                || uType.GetMethod("And", new[] { uType, uType, }) is not MethodInfo andMethod
                || andMethod.Invoke(null, new object[] { xUnderlying, notU }) is not T difference)
                throw UnderlyingType_InvalidCastException();

            return difference;
        }

        public override T CoalesceGreater(T X, T Y)
        {
            if (Y is U yComparable)
                return yComparable.CompareTo(X) > 0
                    ? Y
                    : X;

            throw UnderlyingType_InvalidCastException();
        }

        public override T CoalesceLesser(T X, T Y)
        {
            if (Y is U yComparable)
                return yComparable.CompareTo(X) < 0
                    ? Y
                    : X;

            throw UnderlyingType_InvalidCastException();
        }

        private InvalidCastException UnderlyingType_InvalidCastException()
            => new(typeof(T).ToStringWithGenerics() + " is not of specified underlying " +
                nameof(Type) + " " + typeof(U).ToStringWithGenerics() + ".");
    }
}
