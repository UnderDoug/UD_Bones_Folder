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
        UICanvas: "Chargen/Chartype",
        UICanvasHost: 1)]
    public class BonesModeModuleWindow : EmbarkBuilderModuleWindowPrefabBase<BonesModeModule, HorizontalScroller>
    {
        // don't remove this. It's what allows the first call to UpdateControls() to actually update the controls.
        public EmbarkBuilderModuleWindowDescriptor windowDescriptor;

        public override void BeforeShow(EmbarkBuilderModuleWindowDescriptor descriptor)
        {
            windowDescriptor = descriptor;

            prefabComponent.scrollContext.wraps = false;
            prefabComponent.onSelected.RemoveAllListeners();
            prefabComponent.onSelected.AddListener(ChoiceSelected);
            prefabComponent.BeforeShow(windowDescriptor, GetSelections());
            // module.setData(new BonesModeModuleData("InstantDie"));
            base.BeforeShow(windowDescriptor);
        }

        public override void AfterShow(EmbarkBuilderModuleWindowDescriptor descriptor)
        {
            base.AfterShow(descriptor);
            //module?.AdvanceToEnd();
        }

        public void ChoiceSelected(FrameworkDataElement choice)
        {
            module?.SelectType(choice.Id);
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

        public IEnumerable<ChoiceWithColorIcon> GetSelections()
        {
            foreach (BonesModeModule.GameTypeDescriptor value in base.module.GameTypes.Values)
            {
                yield return new ChoiceWithColorIcon
                {
                    Id = value.ID,
                    Title = value.Title,
                    IconPath = value.IconTile,
                    IconDetailColor = ConsoleLib.Console.ColorUtility.ColorMap[value.IconDetail[0]],
                    IconForegroundColor = ConsoleLib.Console.ColorUtility.ColorMap[value.IconForeground[0]],
                    Description = value.Description,
                    Chosen = IsChoiceSelected
                };
            }
        }

        public bool IsChoiceSelected(ChoiceWithColorIcon choice)
            => module?.GetSelectedType() == choice?.Id
            ;
    }
}
