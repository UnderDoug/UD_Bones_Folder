using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Genkit;

using UD_Bones_Folder.Mod.Serialization.Delegates;

using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization
{
    [Serializable]
    public class MutableLocationsSet : SerializeableSet<Location2D>
    {
        public override WriteEach<Location2D> WriteEach => (w, e) => w.Write(e);
        public override ReadEach<Location2D> ReadEach => r => r.ReadLocation2D();

        #region Constructors

        public MutableLocationsSet()
            : base()
        { }

        public MutableLocationsSet(IEnumerable<Location2D> Source)
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
