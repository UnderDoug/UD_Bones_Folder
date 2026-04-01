using System;

using Qud.API;
using XRL.UI.Framework;

namespace UD_Bones_Folder.Mod.UI
{
    public class BonesInfoData : SaveInfoData
    {
        private SaveBonesInfo _BonesInfo;
        public SaveBonesInfo BonesInfo
        {
            get => _BonesInfo;
            set
            {
                Id = value.ID;
                SaveGame = value;
                _BonesInfo = value;
            }
        }
    }
}
