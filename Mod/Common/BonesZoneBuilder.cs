using System;

using Qud.API;

using XRL.World.Effects;
using XRL.World.Parts;

using UD_Bones_Folder.Mod;
using static UD_Bones_Folder.Mod.BonesManager;
using XRL.UI;
using Qud.UI;
using Platform.IO;
using Kobold;

namespace XRL.World.ZoneBuilders
{
    public class BonesZoneBuilder
    {
        protected bool Alerted;

        public string BonesID;

        public string ZoneID;

        private SaveBonesInfo _SaveBonesInfo;
        public SaveBonesInfo SaveBonesInfo => _SaveBonesInfo ??= GetSavedBonesByID(BonesID);

        public bool Blocked;

        public BonesZoneBuilder()
        { }

        public bool BuildZone(Zone Z)
        {
            if (Blocked)
                return true;

            Utils.Log($"{GetType().Name} for {Z?.ZoneID}");

            return true;
        }
    }
}
