using System;

using Qud.API;
using XRL.UI.Framework;

namespace UD_Bones_Folder.Mod.UI
{
    public class BonesInfoData : PrefixMenuOption
    {
        private SaveBonesInfo _BonesInfo;
        public SaveBonesInfo BonesInfo
        {
            get => _BonesInfo;
            set
            {
                Id = value.ID;
                _BonesInfo = value;
            }
        }
    }
}
