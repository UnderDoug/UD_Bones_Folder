using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Genkit;

using UD_Bones_Folder.Mod.Serialization;

using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization.Delegates
{
    [Serializable]
    public class SerializeEachLocation : SerializeEach<Location2D>
    {
        public static SerializeEachLocation Default => new();

        #region Constructors

        public SerializeEachLocation()
            : base()
        { }

        #endregion
        #region Serialization

        public override void WriteEach(SerializationWriter Writer, Location2D Element)
        {
            Writer.Write(Element);
        }

        public override Location2D ReadEach(SerializationReader Reader)
        {
            return Reader.ReadLocation2D();
        }

        #endregion
    }
}
