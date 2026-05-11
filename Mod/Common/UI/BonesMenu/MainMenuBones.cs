using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Cysharp.Threading.Tasks;

using HarmonyLib;

using Qud.UI;

using UnityEngine;
using UnityEngine.Events;

using XRL;
using XRL.CharacterBuilds;
using XRL.UI;
using XRL.UI.Framework;
using XRL.Wish;

using static XRL.World.Parts.UD_Bones_MoonKingAnnouncer;

using KeyCode = UnityEngine.KeyCode;

namespace UD_Bones_Folder.Mod.UI
{
    [HasModSensitiveStaticCache]
    [HasWishCommand]
    [HarmonyDebug]
    [HarmonyPatch]
    public static class MainMenuBones
    {
        public static bool HasSeenMainMenu = false;

        public static bool AllowPromptAboutLinux = false;

        [ModSensitiveStaticCache]
        public static bool PromptedAboutLinux = false;

        private static MainMenuOptionData _MainMenuBonesOptions = null;
        public static MainMenuOptionData MainMenuBonesOptions => _MainMenuBonesOptions ??= new MainMenuOptionData
        {
            Text = "Bones",
            Command = "Pick:Bones",
            Shortcut = KeyCode.B,
        };

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
            if (data is MainMenuOptionData menuOptionData
                && menuOptionData.Command == "Pick:Bones"
                && XRL.UI.Options.ModernUI)
            {
                // await NavigationController.instance.SuspendContextWhile(OsseousAsh.PerformAskAsync);

                if (MainMenuBonesOptions?.Enabled is true
                    && BonesManagement.CheckInit())
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
        public static bool Show_ActiveInactiveBones_Prefix()
        {
            SetBonesMenuOptionEnabled();
            return true;
        }
            

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.Show))]
        [HarmonyPostfix]
        public static void Show_PerformMainMenuStuff_Postfix()
        {
            //Utils.Log($"{nameof(Show_PerformMainMenuStuff_Postfix)}({nameof(HasSeenMainMenu)}: {HasSeenMainMenu})");
            if (!HasSeenMainMenu)
            {
                HasSeenMainMenu = true;

                // NavigationController.instance.SuspendContextWhile(PerforMainMenuFirstShowAsync).Wait();
                PerforMainMenuFirstShowAsync();

                // UIManager.showWindow("MainMenu");
                // MainMenu.instance.Reshow();
            }
        }

        private static async void PerforMainMenuFirstShowAsync()
        {
            try
            {
                await The.UiContext;

                AllowPromptAboutLinux = true;
                await NavigationController.instance.SuspendContextWhile(OsseousAsh.PerformAskAsync);

                if (!GameManager.AwakeComplete)
                {
                    HasSeenMainMenu = false;
                    return;
                }

                if (Options.EnableOsseousAshDownloads
                    || Options.EnableOsseousAshUploads)
                {
                    try
                    {
                        if (OsseousAsh.Hosts.FirstHostMatching(h => h.Enabled) is OsseousAsh.Host firstEnabledHost)
                            firstEnabledHost.GetServerStatus(RethrowForLinuxPrompt: true);
                    }
                    catch (Exception x)
                    {
                        if (!PromptedAboutLinux
                            && (x is NotSupportedException
                                || x?.InnerException is NotSupportedException)
                            && (x.Message?.Contains("The URI prefix is not recognized") is true))
                        {
                            PromptedAboutLinux = true;
                            var sB = XRL.World.Event.NewStringBuilder()
                                .Append("It appears that there's a compatibility issue creating web requests on this system.")
                                .AppendLine()
                                .AppendLine().Append("There's a known bug with this sort of request when they originate from linux.")
                                .AppendLine()
                                .AppendLine().Append("If you're running Caves of Qud from a linux distribution, ")
                                    .Append("it's recommended that you switch to the '").AppendColored("C", "linuxtest")
                                    .Append("' beta, on which this issue has been resolved.")
                                .AppendLine()
                                .AppendLine().Append("If you're not, please contact ").Append(Utils.AuthorOnPlatforms)
                                    .Append(", since this is a pretty core error and the mod would benefit from it being investigated.");

                            Popup.WaitNewPopupMessage(
                                message: XRL.World.Event.FinalizeString(sB),
                                title: $"Potential Compatibility Conflict",
                                afterRender: new FlippableRender(
                                    Source: new Renderable(
                                        Tile: "Abilities/tile_supressive_fire.png",
                                        ColorString: "&R",
                                        TileColor: "&R",
                                        DetailColor: 'K'),
                                    HFlip: false,
                                    VFlip: true));
                        }
                    }
                }
            }
            finally
            {
                AllowPromptAboutLinux = false;
                UIManager.showWindow("MainMenu");
                MainMenu.instance.Reshow();
            }

        }

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.Update))]
        [HarmonyPrefix]
        public static bool Update_ActiveInactiveBones_Prefix()
        {
            SetBonesMenuOptionEnabled();
            return true;
        }

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.Hide))]
        [HarmonyPrefix]
        public static bool Hide_ActiveInactiveBones_Prefix()
        {
            SetBonesMenuOptionEnabled();
            return true;
        }

        [HarmonyPatch(
            declaringType: typeof(MainMenu),
            methodName: nameof(MainMenu.Reshow))]
        [HarmonyPrefix]
        public static bool Reshow_ActiveInactiveBones_Prefix()
        {
            SetBonesMenuOptionEnabled();
            return true;
        }
    }
}
