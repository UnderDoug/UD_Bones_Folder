using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Genkit;

using UD_Bones_Folder.Mod.Serialization.Delegates;

using XRL.World;
using XRL.World.WorldBuilders;

namespace UD_Bones_Folder.Mod.Serialization
{
    [Serializable]
    public class LocationSet : SerializeableSet<Location2D>
    {
        public override WriteEach<Location2D> WriteEach => (w, e) => w.Write(e);
        public override ReadEach<Location2D> ReadEach => r => r.ReadLocation2D();

        #region Constructors

        public LocationSet()
            : base()
        { }

        public LocationSet(IEnumerable<Location2D> Source)
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

        public void InvertLocations()
        {
            var newItems = UD_Bones_WorldBuilder.YieldAllLocations(l => !Items.Contains(l));
            Clear();
            EnsureCapacity(newItems.Count());
            foreach (var item in newItems)
                Add(item);
        }
    }
}
