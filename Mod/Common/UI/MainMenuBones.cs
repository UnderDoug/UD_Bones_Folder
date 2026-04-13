using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using HarmonyLib;

using Qud.UI;

using UnityEngine;
using UnityEngine.Events;

using XRL;
using XRL.CharacterBuilds;
using XRL.UI;
using XRL.UI.Framework;
using XRL.Wish;

namespace UD_Bones_Folder.Mod.UI
{
    [HasModSensitiveStaticCache]
    [HasWishCommand]
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

        public static bool DoingBonesManagement;

        public static List<AbstractEmbarkBuilderModule> Modules;
	    public static List<EmbarkBuilderModuleWindowDescriptor> WindowDescriptors;
	    public static List<AbstractBuilderModuleWindowBase> Windows;

        public static EmbarkBuilderModuleWindowDescriptor BonesManagementWindowDescriptor => WindowDescriptors?[0];

        public static SaveBonesInfo ReturnToBones;

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

                MainMenuBonesOptions.Enabled = BonesManager.HasSaveBones();
            }

        }

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.SelectedInfo),
            argumentTypes: new Type[] { typeof(FrameworkDataElement), },
            argumentVariations: new ArgumentType[] { ArgumentType.Normal, })]
        [HarmonyPrefix]
        public static async void SelectedInfo_HandleBones_Prefix(FrameworkDataElement data)
        {
            DoingBonesManagement = false;
            if (((MainMenuOptionData)data)?.Command == "Pick:Bones")
            {
                if (XRL.UI.Options.ModernUI)
                {
                    await NavigationController.instance.SuspendContextWhile(OsseousAsh.PerformAskAsync);

                    if (MainMenuBonesOptions?.Enabled is true)
                    {
                        if (BonesManagement.CheckInit())
                        {
                            try
                            {
                                ReturnToBones = await NavigationController.instance.SuspendContextWhile(BonesManagement.instance.BonesMenu);
                            }
                            finally
                            {
                                UIManager.showWindow("MainMenu");
                                MainMenu.instance.Reshow();
                            }
                        }
                    }
                }
            }
            DoingBonesManagement = false;
        }

        public static void SetBonesMenuOptionEnabled()
        {
            if (MainMenuBonesOptions != null)
                MainMenuBonesOptions.Enabled = BonesManager.HasSaveBones()
                    || OsseousAsh.WantToAsk;
        }

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.Show))]
        [HarmonyPrefix]
        public static void Show_ActiveInactiveBones_Prefix()
            => SetBonesMenuOptionEnabled()
            ;

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.Update))]
        [HarmonyPrefix]
        public static void Update_ActiveInactiveBones_Prefix()
            => SetBonesMenuOptionEnabled()
            ;

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.Hide))]
        [HarmonyPrefix]
        public static void Hide_ActiveInactiveBones_Prefix()
            => SetBonesMenuOptionEnabled()
            ;

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.Reshow))]
        [HarmonyPrefix]
        public static void Reshow_ActiveInactiveBones_Prefix()
            => SetBonesMenuOptionEnabled()
            ;
    }
}
