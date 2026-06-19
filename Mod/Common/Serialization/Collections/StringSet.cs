using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UD_Bones_Folder.Mod.Serialization.Delegates;

using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization
{
    [Serializable]
    public class StringSet : SerializeableSet<string>
    {
        public override WriteEach<string> WriteEach => (w, e) => w.WriteOptimized(e);
        public override ReadEach<string> ReadEach => r => r.ReadOptimizedString();

        #region Constructors

        public StringSet()
            : base()
        { }

        public StringSet(IEnumerable<string> Source)
            : base(Source)
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
