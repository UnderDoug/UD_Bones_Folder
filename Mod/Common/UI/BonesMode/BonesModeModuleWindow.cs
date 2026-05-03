using System;
using System.Collections.Generic;
using System.Text;

using UnityEngine;

using XRL;
using XRL.CharacterBuilds;
using XRL.UI;
using XRL.UI.Framework;

using UnityGameObject = UnityEngine.GameObject;

namespace UD_Bones_Folder.Mod.UI
{
    [UIView(
        ID: "CharacterCreation:UD_Bones_BonesMode",
        NavCategory: "Chargen",
        UICanvas: "Chargen/CustomizeCharacter",
        UICanvasHost: 1)]
    public class BonesModeModuleWindow : EmbarkBuilderModuleWindowPrefabBase<BonesModeModule, FrameworkScroller>
    {
        // don't remove this. It's what allows the first call to UpdateControls() to actually update the controls.
        public EmbarkBuilderModuleWindowDescriptor windowDescriptor;

        public override void BeforeShow(EmbarkBuilderModuleWindowDescriptor descriptor)
        {
            windowDescriptor = descriptor;

            prefabComponent.scrollContext.wraps = false;
            prefabComponent.onSelected.RemoveAllListeners();
            prefabComponent.BeforeShow(windowDescriptor, new List<FrameworkDataElement> { new PrefixMenuOption { Id = "1", Prefix = "", Description = "none"} });
            module.setData(new BonesModeModuleData());
            base.BeforeShow(windowDescriptor);
        }

        public override void AfterShow(EmbarkBuilderModuleWindowDescriptor descriptor)
        {
            base.AfterShow(descriptor);
            module?.AdvanceToEnd();
        }

        /*public override UnityGameObject InstantiatePrefab(UnityGameObject prefab)
        {
            prefab.GetComponentInChildren<CategoryMenusScroller>().allowVerticalLayout = false;
            return base.InstantiatePrefab(prefab);
        }*/

        public override UIBreadcrumb GetBreadcrumb()
        {
            return new UIBreadcrumb
            {
                Id = GetType().FullName,
                Title = "Bones Mode",
                IconPath = "Items/sw_bones_1.bmp,Items/sw_bones_2.bmp,Items/sw_bones_3.bmp,Items/sw_bones_4.bmp,Items/sw_bones_5.bmp,Items/sw_bones_6.bmp,Items/sw_bones_7.bmp,Items/sw_bones_8.bmp".CachedCommaExpansion().GetRandomElementCosmetic(),
                IconDetailColor = The.Color.Black,
                IconForegroundColor = The.Color.Gray,
            };
        }
    }
}
