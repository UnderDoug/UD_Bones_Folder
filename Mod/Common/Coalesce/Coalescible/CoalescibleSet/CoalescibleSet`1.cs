using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using XRL;
using XRL.Rules;
using XRL.World;
using XRL.Collections;

using static UD_Bones_Folder.Mod.Utils;

using SerializeField = UnityEngine.SerializeField;
using UD_Bones_Folder.Mod.Serialization;
using Genkit;

namespace UD_Bones_Folder.Mod
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [DebuggerDisplay("Count = {Count}")]
    [Serializable]
    public partial class CoalescibleSet<T>
        : IComposite
        , IDisposable
    //  , IEnumerable<T>
    //  , ICollection
    //  , ICollection<T>
    //  , IReadOnlyCollection<T>
    //  , ISet<T>
    //  , IList
    //  , IList<T>
    //  , IReadOnlyList<T>
    {
        #region Debug
        /*
        [UD_DebugRegistry]
        public static void CoalescibleSet_DoDebugRegistry(DebugMethodRegistry Registry)
        {
            Registry.RegisterEach(
                Type: typeof(UD_Bones_Folder.Mod.CoalescibleSet<T>),
                MethodNameValues: new Dictionary<string, bool>()
                {
                    { nameof(Add), false },
                });
        }
        */
        #endregion
        #region Static Fields & Props

        public static XRL.Version AddedIn => new(0, 0, 3, 8);

        #endregion
        #region Instance Fields & Properties

        public string NameWithGenerics => GetType().ToStringWithGenerics();

        protected T[] Items = Array.Empty<T>();

        protected int Length;
        protected int Size;
        protected int Variant;

        private CompositeEqualityComparer<T> _EqualityComparer;
        public CompositeEqualityComparer<T> EqualityComparer
        {
            get => _EqualityComparer ??= new CompositeEqualityComparer<T>(UseBaseDefault: true);
            protected set
            {
                _EqualityComparer = value;
                Variant++;
            }
        }

        private Coalescer<T> _Coalescer;
        public Coalescer<T> Coalescer
        {
            get => _Coalescer ??= Coalescer<T>.Default;
            protected set
            {
                _Coalescer = value;
                Variant++;
            }
        }

        public int Capacity => Size;
        public virtual int DefaultCapacity => 4;
        public int Version => Variant;

        public bool WantFieldReflection => false;

        #endregion
        #region Constructors

        public CoalescibleSet()
        {
            Length = 0;
            Size = 0;
            Variant = 0;

            _EqualityComparer = null;
            _Coalescer = null;

            EnsureCapacity(DefaultCapacity);
        }

        public CoalescibleSet(int Capacity, CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : this()
        {
            EnsureCapacity(Capacity);
            _EqualityComparer = EqualityComparer;
            _Coalescer = Coalescer;
        }

        public CoalescibleSet(int Capacity)
            : this(Capacity, null, null)
        { }

        public CoalescibleSet(CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : this(0, EqualityComparer, Coalescer)
        { }

        public CoalescibleSet(CompositeEqualityComparer<T> EqualityComparer)
            : this(EqualityComparer, null)
        { }

        public CoalescibleSet(Coalescer<T> Coalescer)
            : this((CompositeEqualityComparer<T>)null, Coalescer)
        { }

        public CoalescibleSet(IEnumerable<T> Enumerable, CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : this(Enumerable?.Count() ?? 0, EqualityComparer, Coalescer)
        {
            AddRange(Enumerable);
        }

        public CoalescibleSet(IEnumerable<T> Enumerable)
            : this(Enumerable, null, null)
        { }

        public CoalescibleSet(IEnumerable<T> Enumerable, CompositeEqualityComparer<T> EqualityComparer)
            : this(Enumerable, EqualityComparer, null)
        { }

        public CoalescibleSet(IEnumerable<T> Enumerable, Coalescer<T> Coalescer)
            : this(Enumerable, null, Coalescer)
        { }

        public CoalescibleSet(IReadOnlyList<T> List, CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : this(List.IteratorSafe(), EqualityComparer, Coalescer)
        { }

        public CoalescibleSet(IReadOnlyList<T> List)
            : this(List, null, null)
        { }

        #endregion
        #region Serialization

        public virtual void WriteElements(SerializationWriter Writer)
        {
            bool warn = false;
            for (int i = 0; i < Count; i++)
            {
                if (typeof(T) == typeof(Location2D))
                    Writer.Write(Items[i] as Location2D);
                else
                if (typeof(T) == typeof(List<Location2D>))
                    Writer.Write(Items[i] as List<Location2D>);
                else
                if (typeof(IComposite).IsAssignableFrom(typeof(T)))
                    Writer.Write(Items[i] as IComposite);
                else
                if (typeof(T) == typeof(GameObject))
                    Writer.WriteGameObject(Items[i] as GameObject);
                else
                {
                    warn = true;
                    Writer.WriteObject(Items[i]);
                }
            }

            if (warn
                && GetType() == typeof(CoalescibleSet<T>))
            {
                string serializeableSetNameWithGenerics = typeof(SerializeableSet<T>).ToStringWithGenerics();
                Warn(
                    Context: $"{NameWithGenerics} has an incomplete implementation of {nameof(Write)}, which may not serialize the elements in the collection efficiently. Use a {serializeableSetNameWithGenerics} instead, or make a derived collection that overrides the {nameof(WriteElements)} method.",
                    X: new Exception());
            }
        }

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.Write(EqualityComparer);
            Writer.Write(Coalescer);

            Writer.WriteOptimized(Count);
            WriteElements(Writer);
        }

        public virtual void ReadElements(SerializationReader Reader, int Count)
        {
            bool warn = false;
            for (int i = 0; i < Count; i++)
            {
                if (typeof(T) == typeof(Location2D))
                    Items[i] = (T)(object)Reader.ReadLocation2D();
                else
                if (typeof(T) == typeof(List<Location2D>))
                    Items[i] = (T)(object)Reader.ReadLocation2DList();
                else
                if (typeof(IComposite).IsAssignableFrom(typeof(T)))
                    Items[i] = (T)(object)Reader.ReadComposite();
                else
                if (typeof(T) == typeof(GameObject))
                    Items[i] = (T)(object)Reader.ReadGameObject();
                else
                {
                    warn = true;
                    Items[i] = (T)Reader.ReadObject();
                }
            }

            if (warn
                && GetType() == typeof(CoalescibleSet<T>))
            {
                string serializeableSetNameWithGenerics = typeof(SerializeableSet<T>).ToStringWithGenerics();
                Warn(
                    Context: $"{NameWithGenerics} has an incomplete implementation of {nameof(Write)}, which may not deserialize the elements in the collection efficiently. Use a {serializeableSetNameWithGenerics} instead, or make a derived collection that overrides the {nameof(ReadElements)} method.",
                    X: new Exception());
            }
        }

        public virtual void Read(SerializationReader Reader)
        {
            _EqualityComparer = Reader.ReadComposite() as CompositeEqualityComparer<T>;
            _Coalescer = Reader.ReadComposite() as Coalescer<T>;

            int count = Reader.ReadOptimizedInt32();
            EnsureCapacity(count);
            ReadElements(Reader, count);
        }

        #endregion
        #region Private Member References

        protected CompositeEqualityComparer<T> GetEqualityComparer()
            => _EqualityComparer
            ;

        protected Coalescer<T> GetCoalescer()
            => _Coalescer
            ;

        #endregion
        #region Collection Helpers

        protected void Resize(int Capacity)
        {
            if (Capacity == 0)
                Capacity = DefaultCapacity;

            T[] array = new T[Capacity];
            Array.Copy(Items, 0, array, 0, Length);
            Items = array;
            Size = Capacity;
        }

        public void EnsureCapacity(int Capacity)
        {
            if (Size < Capacity)
                Resize(Capacity);
        }

        public void AddRange(IEnumerable<T> Items)
        {
            int count = Items.Count();
            EnsureCapacity(Length + count);
            Items.ForEach(e => Add(e));
        }

        public void AddRange(IReadOnlyCollection<T> Items)
            => AddRange(Items as IEnumerable<T>)
            ;

        public void AddRange(IReadOnlyList<T> Items)
            => AddRange(Items as IReadOnlyCollection<T>)
            ;

        public void AddSpan(ReadOnlySpan<T> Items)
        {
            if (Items.GetEnumerator() is ReadOnlySpan<T>.Enumerator enumerator)
            {
                EnsureCapacity(Length + Items.Length);
                while (enumerator.MoveNext())
                    Add(enumerator.Current);
            }
        }

        public bool TryGetIndexOf(T Value, out int Index)
            => (Index = IndexOf(Value)) >= 0
            ;

        public Span<T> FillSpan(int Length)
        {
            EnsureCapacity(this.Length + Length);
            Span<T> result = new(Items, this.Length, Length);
            this.Length += Length;
            Variant++;
            return result;
        }

        public virtual T[] ToArray()
        {
            T[] array = new T[Length];
            Array.Copy(Items, 0, array, 0, Length);
            return array;
        }

        public virtual int RemoveWhere(Predicate<T> Match)
        {
            if (Match == null)
                throw new ArgumentNullException(nameof(Match));

            int removed = 0;
            for (int i = Count - 1; i >= 0; i--)
            {
                if (Items[i] is T item
                    && Match.Invoke(item)
                    && Remove(item))
                {
                    removed++;
                }
            }
            return removed;
        }

        #endregion
        #region Coalesce

        /// <summary>
        /// Gets the stored <typeparamref name="T"/> element matching <paramref name="Value"/>, if it exists, and returns the result of calling <see cref="Coalescer{T}.Coalesce(T,T)"/> on the two objects, or returns <paramref name="Value"/> if it doesn't.
        /// </summary>
        /// <remarks>
        /// This method will pass the above two objects with the existing element as the first parameter and <paramref name="Value"/> as the second; the inverse of <see cref="GetCoalescedWith(T)"/>.
        /// </remarks>
        /// <param name="Value">The object to <see cref="Coalescer{T}.Coalesce(T,T)"/> with an equal entry in the current set, if one exists.</param>
        /// <returns>The result of calling <see cref="Coalescer{T}.Coalesce(T,T)"/> on a stored, matching <typeparamref name="T"/> element, if it exists and <paramref name="Value"/>;<br/><paramref name="Value"/>, otherwise.</returns>
        public T GetCoalesceWith(T Value)
            => TryGetIndexOf(Value, out int index)
                && this[index] is T itemAtIndex
            ? Coalescer.Coalesce(itemAtIndex, Value)
            : Value
            ;

        /// <summary>
        /// Gets the stored <typeparamref name="T"/> element matching <paramref name="Value"/>, if it exists, and returns the result of calling <see cref="Coalescer{T}.Coalesce(T,T)"/> on the two objects, or returns <paramref name="Value"/> if it doesn't.
        /// </summary>
        /// <remarks>
        /// This method will pass the above two objects with <paramref name="Value"/> as the first parameter and the existing element as the second; the inverse of <see cref="GetCoalesceWith(T)"/>.
        /// </remarks>
        /// <param name="Value">The object to have <see cref="Coalescer{T}.Coalesce(T,T)"/> an equal entry in this <see cref="CoalescibleSet{T}"/> if one exists.</param>
        /// <returns>The result of calling <see cref="Coalescer{T}.Coalesce(T,T)"/> on <paramref name="Value"/> and a stored, matching <typeparamref name="T"/> element, if it exists;<br/><paramref name="Value"/>, otherwise.</returns>
        public T GetCoalescedWith(T Value)
            => TryGetIndexOf(Value, out int index)
                && this[index] is T itemAtIndex
            ? Coalescer.Coalesce(itemAtIndex, Value)
            : Value
            ;

        public bool TryGetCoalesceWith(T Value, out T CoalescedValue)
            => !(CoalescedValue = GetCoalesceWith(Value)).Equals(Value)
            ;

        public bool TryGetCoalescedWith(T Value, out T CoalescedValue)
            => !(CoalescedValue = GetCoalescedWith(Value)).Equals(Value)
            ;

        #endregion
        #region Sorting

        public virtual CoalescibleSet<T> SortInPlace(Comparison<T> Comparison)
        {
            if (!Items.IsNullOrEmpty())
                Array.Sort(Items, Comparison);
            return this;
        }

        #endregion
        #region ReadOnlySpan

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ReadOnlySpan<T> AsSpan(int Start, int Length)
            => (uint)(Start + Length) > (uint)this.Length
            ? throw new ArgumentOutOfRangeException("Length")
            : new(Items, Start, Length)
            ;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ReadOnlySpan<T> AsSpan(int Start)
            => (uint)Start > (uint)Length
            ? throw new ArgumentOutOfRangeException("Start")
            : AsSpan(Start, Length - Start)
            ;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public virtual ReadOnlySpan<T> AsSpan()
            => AsSpan(0, Length)
            ;

        #endregion
        #region Disposable

        public virtual void Dispose()
        {
            Items = null;
            Length = 0;
            Size = 0;
            Variant = 0;
            EqualityComparer = null;
            Coalescer = null;
        }

        #endregion
        #region User-defined Conversions

        public static implicit operator ReadOnlySpan<T>(CoalescibleSet<T> CoalescibleSet)
            => CoalescibleSet.AsSpan()
            ;

        public static explicit operator HashSet<T>(CoalescibleSet<T> CoalescibleSet)
            => CoalescibleSet != null
            ? new HashSet<T>(CoalescibleSet, CoalescibleSet.EqualityComparer)
            : null
            ;

        public static explicit operator CoalescibleSet<T>(HashSet<T> HashSet)
            => HashSet != null
            ? new CoalescibleSet<T>(HashSet, (CompositeEqualityComparer<T>)HashSet.Comparer, Coalescer<T>.Default)
            : null
            ;

        #endregion
    }
}
