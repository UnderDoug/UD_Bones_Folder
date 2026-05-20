using System;
using System.Collections.Generic;
using System.Text;

using XRL.CharacterBuilds;

namespace UD_Bones_Folder.Mod.UI
{
    public class BonesModeModuleData : AbstractEmbarkBuilderModuleData
    {
        public string type;

        public BonesModeModuleData()
        {
        }

        public BonesModeModuleData(string type)
        {
            this.type = type;
        }
    }
}
