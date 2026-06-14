using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using System.Text;

using UD_Bones_Folder.Mod.Coalescence;

using XRL.World;

using static UD_Bones_Folder.Mod.Utils;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public abstract class Coalescer<T>
        : ICoalescer
        , ICoalescer<T>
        , IComposite
    {
        private static volatile Coalescer<T> _Default;
        public static Coalescer<T> Default
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _Default ??= CreateCoalescer(CoalesceMethod.First);
        }

        private static volatile Coalescer<T> _DefaultSecond;
        public static Coalescer<T> DefaultSecond
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _DefaultSecond ??= CreateCoalescer(CoalesceMethod.Second);
        }

        private static volatile Coalescer<T> _DefaultGreater;
        public static Coalescer<T> DefaultGreater
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _DefaultGreater ??= CreateCoalescer(CoalesceMethod.Greater);
        }

        private static volatile Coalescer<T> _DefaultLesser;
        public static Coalescer<T> DefaultLesser
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _DefaultLesser ??= CreateCoalescer(CoalesceMethod.Lesser);
        }

        public virtual bool WantFieldReflection => false;

        private CoalesceMethod _CoalesceMethod;

        public CoalesceMethod CoalesceMethod => _CoalesceMethod;

        protected Coalescer()
        {
            _CoalesceMethod = CoalesceMethod.First;
        }

        public Coalescer(CoalesceMethod CoalesceMethod)
            : this()
        {
            _CoalesceMethod = CoalesceMethod;
        }

        #region Serialization

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.WriteOptimized((int)CoalesceMethod);
        }

        public virtual void Read(SerializationReader Reader)
        {
            _CoalesceMethod = (CoalesceMethod)Reader.ReadOptimizedInt32();
        }

        #endregion

        private static Coalescer<T> InstantiateCoalescer(
            Type CoalescerType,
            CoalesceMethod CoalesceMethod,
            params Type[] GenericTypeArguments)
            => Activator.CreateInstance(
                type: CoalescerType.MakeGenericType(GenericTypeArguments),
                args: new object[]
                {
                    CoalesceMethod,
                }) as Coalescer<T>;

        [SecuritySafeCritical]
        private static Coalescer<T> CreateCoalescer(CoalesceMethod CoalesceMethod)
        {
            var stackFrames = new StackTrace().GetFrames();
            int count = Math.Min(stackFrames.Length, 5);
            StackFrame[] firstStackFrames = new StackFrame[count];
            Array.Copy(firstStackFrames, stackFrames, count);

            //string textLineBefore = $"{typeof(Coalescer<T>).ToStringWithGenerics()}.{nameof(CreateCoalescer)}({nameof(CoalesceMethod)}: {CoalesceMethod}) Called;";
            //Log($"{new StackTrace().FramesToString(Count: 5, SkipLines: 0, TextLineBefore: textLineBefore)}");
            Type typeT = typeof(T);
            if (typeT == typeof(byte))
                return new ByteCoalescer(CoalesceMethod) as Coalescer<T>;

            if (typeof(ICoalescible<T>).IsAssignableFrom(typeT))
                return InstantiateCoalescer(typeof(GenericCoalescer<>), CoalesceMethod, typeT);

            if (typeT.IsGenericType
                && typeT.GetGenericTypeDefinition() == typeof(Nullable<>)
                && typeT.GetGenericArguments()[0] is Type nullableTypeT
                && typeof(ICoalescible<>).MakeGenericType(nullableTypeT).IsAssignableFrom(nullableTypeT))
                return InstantiateCoalescer(typeof(NullableCoalescer<>), CoalesceMethod, nullableTypeT);

            if (typeT.IsEnum
                && Type.GetTypeCode(Enum.GetUnderlyingType(typeT)) is TypeCode typeCode)
                return InstantiateCoalescer(
                    CoalescerType: typeof(EnumCoalescer<,>),
                    CoalesceMethod: CoalesceMethod,
                    GenericTypeArguments: new Type[]
                    {
                        typeT,
                        typeCode switch
                        {
                            TypeCode.Int16 => typeof(short),
                            TypeCode.SByte => typeof(sbyte),
                            TypeCode.Byte => typeof(byte),
                            TypeCode.UInt16 => typeof(ushort),
                            TypeCode.Int32 => typeof(int),
                            TypeCode.UInt32 => typeof(uint),
                            TypeCode.Int64 => typeof(long),
                            TypeCode.UInt64 => typeof(ulong),
                            _ => throw new InvalidCastException((int)typeCode + " is not a valid " + nameof(TypeCode) + "."),
                        }
                    });

            return new ObjectCoalescer<T>(CoalesceMethod);
        }

        public abstract T CoalesceFirst(T x, T y);

        public abstract T CoalesceSecond(T x, T y);

        public abstract T CoalesceGreater(T x, T y);

        public abstract T CoalesceLesser(T x, T y);

        public abstract T CoalesceCombine(T x, T y);

        public abstract T CoalesceDifference(T x, T y);

        public virtual T CoalesceTypeDefined(T x, T y)
            => x is ICoalescible<T> xCoalescible
            ? xCoalescible.Coalesce(y)
            : throw NotCoalescible_InvalidCastException();

        public virtual T Coalesce(T x, T y)
            => CoalesceMethod switch
            {
                CoalesceMethod.First => CoalesceFirst(x, y),
                CoalesceMethod.Second => CoalesceSecond(x, y),
                CoalesceMethod.Greater => CoalesceGreater(x, y),
                CoalesceMethod.Lesser => CoalesceLesser(x, y),
                CoalesceMethod.Combine => CoalesceCombine(x, y),
                CoalesceMethod.Difference => CoalesceDifference(x, y),
                CoalesceMethod.TypeDefined => CoalesceTypeDefined(x, y),
                _ => throw new InvalidEnumValueException<CoalesceMethod>(CoalesceMethod),
            };

        object ICoalescer.Coalesce(object x, object y)
        {
            if (x is not T xTyped)
                throw NotTypeT_InvalidCastException(nameof(x), x.GetType());
            if (y is not T yTyped)
                throw NotTypeT_InvalidCastException(nameof(y), y.GetType());

            return Coalesce(xTyped, yTyped);
        }

        protected T CoalesceGreaterInternal(T x, T y)
        {
            if (y is IComparable<T> yComparableT)
                return yComparableT.CompareTo(x) > 0
                    ? y
                    : x;

            if (y is IComparable yComparable)
                return yComparable.CompareTo(x) > 0
                    ? y
                    : x;

            throw NotComparable_InvalidCastException();
        }

        protected T CoalesceLesserInternal(T x, T y)
        {
            if (y is IComparable<T> yComparableT)
                return yComparableT.CompareTo(x) < 0
                    ? y
                    : x;

            if (y is IComparable yComparable)
                return yComparable.CompareTo(x) < 0
                    ? y
                    : x;

            throw NotComparable_InvalidCastException();
        }

        public override string ToString()
            => GetType().ToStringWithGenerics() + "(" + CoalesceMethod + ")";

        public override bool Equals(object Other)
            => GetType() == Other?.GetType()
            && Other is Coalescer<T> typedOther
            && CoalesceMethod == typedOther.CoalesceMethod;

        public override int GetHashCode()
            => base.GetHashCode()
            + CoalesceMethod.GetHashCode();

        protected InvalidCastException NotCoalescible_InvalidCastException()
            => new(typeof(T).ToStringWithGenerics() + " cannot be cast to " + nameof(ICoalescible<T>) + ".");

        protected InvalidCastException NotComparable_InvalidCastException()
            => new(typeof(T).ToStringWithGenerics() + " cannot be cast to " + nameof(IComparable) + " or " + nameof(IComparable<T>) + ".");

        protected InvalidCastException NotTypeT_InvalidCastException(string ParamName, Type InvalidType)
            => new(
                message: ParamName + ", of " + nameof(Type) + " " + InvalidType.ToStringWithGenerics() + ", " +
                    "cannot be cast to " + typeof(T).ToStringWithGenerics() + ".");

        protected NotSupportedException Nonsense_NotSupportedException([CallerMemberName] string MethodName = "")
            => new("There is no sensical way to " + MethodName + " for objects of " + nameof(Type) + " " + typeof(T).ToStringWithGenerics() + ".");
    }
}
