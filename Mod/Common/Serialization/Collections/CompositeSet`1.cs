using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UD_Bones_Folder.Mod.Serialization.Delegates;

using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization
{
    [Serializable]
    public class CompositeSet<T> : SerializeableSet<T>
        where T : IComposite, new()
    {
        public override WriteEach<T> WriteEach => (w, e) => w.WriteComposite(e);
        public override ReadEach<T> ReadEach => r => r.ReadComposite<T>();

        #region Constructors
        public CompositeSet()
            : base()
        { }

        public CompositeSet(int Capacity, CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : base(Capacity, EqualityComparer, Coalescer)
        { }

        public CompositeSet(CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : base(EqualityComparer, Coalescer)
        { }

        public CompositeSet(CompositeEqualityComparer<T> EqualityComparer)
            : base(EqualityComparer, Coalescer<T>.Default)
        { }

        public CompositeSet(IEnumerable<T> Source, CompositeEqualityComparer<T> EqualityComparer, Coalescer<T> Coalescer)
            : base(Source, EqualityComparer, Coalescer)
        { }

        public CompositeSet(IEnumerable<T> Source, CompositeEqualityComparer<T> EqualityComparer)
            : this(Source, EqualityComparer, Coalescer<T>.Default)
        { }

        public CompositeSet(Coalescer<T> Coalescer)
            : base((CompositeEqualityComparer<T>)EqualityComparer<T>.Default, Coalescer)
        { }

        #endregion
        #region Serialization

        public override void Write(SerializationWriter Writer)
        {
            base.Write(Writer);
        }
        public override void Read(SerializationReader Reader)
        {
            base.Read(Reader);
        }

        #endregion
    }
}
