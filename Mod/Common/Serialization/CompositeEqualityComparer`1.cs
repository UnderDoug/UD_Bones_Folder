using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

using SerializeField = UnityEngine.SerializeField;

namespace UD_Bones_Folder.Mod.Serialization
{
    public class CompositeEqualityComparer<T> : EqualityComparer<T>, IComposite
    {
        public virtual bool WantFieldReflection => true;

        [SerializeField]
        private readonly bool UseBaseDefault;

        public CompositeEqualityComparer()
            : base()
        {
        }

        public CompositeEqualityComparer(bool UseBaseDefault)
            : this()
        {
            this.UseBaseDefault = UseBaseDefault;
        }

        public virtual void Write(SerializationWriter Writer)
        { }

        public virtual void Read(SerializationReader Reader)
        { }

        public override bool Equals(T x, T y)
        {
            if (UseBaseDefault)
                return Default.Equals(x, y);
            else
                throw new NotSupportedException("Derived classes must provide an implementation.");
        }

        public override int GetHashCode(T obj)
        {
            if (UseBaseDefault)
                return Default.GetHashCode(obj);
            else
                throw new NotSupportedException("Derived classes must provide an implementation.");
        }
    }
}
