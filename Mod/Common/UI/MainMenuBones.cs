using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using HarmonyLib;

using Qud.UI;

using UnityEngine.Events;

using XRL;
using XRL.UI;
using XRL.UI.Framework;

namespace UD_Bones_Folder.Mod.UI
{
    [HasModSensitiveStaticCache]
    [HarmonyDebug]
    [HarmonyPatch]
    public static class MainMenuBones
    {
        private static MainMenuOptionData _MainMenuBonesOptions = null;
        public static MainMenuOptionData MainMenuBonesOptions => _MainMenuBonesOptions ??= new MainMenuOptionData
        {
            Text = "Bones",
            Command = "Pick:Bones",
            Shortcut = UnityEngine.KeyCode.B,
        };

        [ModSensitiveCacheInit]
        public static void InsertMainMenuBones()
        {
            if (!MainMenu.LeftOptions.IsNullOrEmpty())
            {
                if (!MainMenu.LeftOptions.Contains(MainMenuBonesOptions))
                {
                    int lastIndex = MainMenu.LeftOptions.Count - 1;
                    int placementIndex = lastIndex;

                    if (MainMenu.LeftOptions.FirstOrDefault(m => m.Text == "Records") is MainMenuOptionData recordsOption)
                        placementIndex = MainMenu.LeftOptions.IndexOf(recordsOption);

                    if (placementIndex == lastIndex)
                        MainMenu.LeftOptions.Add(MainMenuBonesOptions);
                    else
                        MainMenu.LeftOptions.Insert(placementIndex + 1, MainMenuBonesOptions);
                }

                MainMenuBonesOptions.Enabled = BonesManager.HasSavedBones();
            }

        }

        public static void AddListner()
        {
            if (MainMenu.instance is MainMenu mainMenu
                && mainMenu.leftScroller is FrameworkScroller leftScroller
                && leftScroller.onSelected is UnityEvent<FrameworkDataElement> onSelected)
                onSelected.AddListener(SelectedInfo);
        }

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.SelectedInfo),
            argumentTypes: new Type[] { typeof(FrameworkDataElement), },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal, })]
        [HarmonyPrefix]
        public static async void SelectedInfo(FrameworkDataElement data)
        {
            // AddListner();

            if (data is not MainMenuOptionData mainMenuOptionData)
                return;

            if (mainMenuOptionData.Command == "Pick:Bones")
            {
                /*
                try
                {
                    DialogResult dialogResult = await Popup.ShowYesNoAsync("it WORKED!");
                    await Popup.ShowAsync($"{nameof(DialogResult)}: {dialogResult}");
                    return;
                }
                catch (Exception x)
                {
                    Utils.Error("Main Menu Bones", x);
                    return;
                }
                */
                if (Options.ModernUI)
                {
                    try
                    {
                        GameManager.Instance.PushGameView("ModernSaveManagement");
                        MainMenu.instance.DisableNavContext();
                        BonesManagement.instance.BonesMenu().Wait();
                    }
                    finally
                    {
                        GameManager.Instance.PopGameView();
                        MainMenu.instance.Reshow();
                    }
                }
            }
        }
    }
}
