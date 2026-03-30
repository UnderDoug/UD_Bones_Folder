using System;
using System.Collections.Generic;
using System.Text;

using XRL.CharacterBuilds;

namespace UD_Bones_Folder.Mod.UI
{
    public class BonesManagementModule : QudEmbarkBuilderModule<BonesManagementModuleData>
    {
        private AbstractEmbarkBuilderModuleData _DefaultData;
        public override AbstractEmbarkBuilderModuleData DefaultData => _DefaultData ??= new BonesManagementModuleData();

        public override bool shouldBeEnabled()
            => MainMenuBones.DoingBonesManagement;

        public override bool IncludeInBuildCodes()
            => false;

        public override bool shouldBeEditable()
            => MainMenuBones.DoingBonesManagement;

    }
}
