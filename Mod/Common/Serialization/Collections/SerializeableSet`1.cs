using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UD_Bones_Folder.Mod.Serialization.Delegates;

using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization
{
    [Serializable]
    public class SerializeableSet<T> : CoalescibleSet<T>
    {
        private SerializeEach<T> _SerializeEach;
        public SerializeEach<T> SerializeEach
        {
            get => _SerializeEach ??= new();
            protected set
            {
                _SerializeEach = value;
                Variant++;
            }
        }

        public virtual WriteEach<T> WriteEach => SerializeEach.WriteEach;
        public virtual ReadEach<T> ReadEach => SerializeEach.ReadEach;

        #region Constructors

        public SerializeableSet()
            : base()
        {
            _SerializeEach = null;
        }

        public SerializeableSet(SerializeEach<T> SerializeEach)
            : this()
        {
            _SerializeEach = SerializeEach;
        }

        public SerializeableSet(int Capacity, CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer, SerializeEach<T> SerializeEach)
            : base(Capacity, EqualityComparer, Coalescer)
        {
            _SerializeEach = SerializeEach;
        }

        public SerializeableSet(int Capacity, CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : this(Capacity, EqualityComparer, Coalescer, null)
        { }

        public SerializeableSet(CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer, SerializeEach<T> SerializeEach)
            : base(EqualityComparer, Coalescer)
        {
            _SerializeEach = SerializeEach;
        }

        public SerializeableSet(CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : this(EqualityComparer, Coalescer, null)
        { }

        public SerializeableSet(IEnumerable<T> Source, CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer, SerializeEach<T> SerializeEach)
            : base(Source, EqualityComparer, Coalescer)
        {
            _SerializeEach = SerializeEach;
        }

        public SerializeableSet(IEnumerable<T> Source, CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : this(Source, EqualityComparer, Coalescer, null)
        { }

        public SerializeableSet(IEnumerable<T> Source)
            : this(Source, null, null)
        { }

        #endregion
        #region Serialization

        public override void WriteElements(SerializationWriter Writer)
        {
            Writer.WriteComposite(SerializeEach);

            if (SerializeEach != null
                || WriteEach != null)
            {
                foreach (var item in this.IteratorSafe())
                    WriteEach.Invoke(Writer, item);
            }
            else
            {
                string serializeEachGenericsName = typeof(SerializeEach<T>).ToStringWithGenerics();
                string writeEachGenericsName = typeof(WriteEach<T>).ToStringWithGenerics();
                Utils.Warn(
                    Context: $"{NameWithGenerics} is never assigned a {serializeEachGenericsName} or {writeEachGenericsName}. Calling base.{nameof(WriteElements)}",
                    X: new Exception());
                base.WriteElements(Writer);
            }
        }

        public override void Write(SerializationWriter Writer)
        {
            base.Write(Writer);
        }

        public override void ReadElements(SerializationReader Reader, int Count)
        {
            SerializeEach = Reader.ReadComposite<SerializeEach<T>>();

            if (SerializeEach != null
                || ReadEach != null)
            {
                for (int i = 0; i < Count; i++)
                    Add(ReadEach.Invoke(Reader));
            }
            else
            {
                string serializeEachGenericsName = typeof(SerializeEach<T>).ToStringWithGenerics();
                string readEachGenericsName = typeof(ReadEach<T>).ToStringWithGenerics();
                Utils.Warn(
                    Context: $"{NameWithGenerics} is never assigned a {serializeEachGenericsName} or {readEachGenericsName}. Calling base.{nameof(ReadElements)}",
                    X: new Exception());
                base.ReadElements(Reader, Count);
            }

        }

        public override void Read(SerializationReader Reader)
        {
            base.Read(Reader);
        }

        #endregion
    }
}
