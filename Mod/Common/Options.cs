using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Platform.IO;

using Qud.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;

using static UD_Bones_Folder.Mod.Const;
using static UD_Bones_Folder.Mod.OsseousAsh;
using static XRL.World.Parts.UD_Bones_MoonKingAnnouncer;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasOptionFlagUpdate(Prefix = MOD_PREFIX)]
    public static class Options
    {
        // Debug Settings
        [OptionFlag] public static bool DebugEnableNoHoarding;
        [OptionFlag] public static bool DebugEnableNoExhuming;
        [OptionFlag] public static bool DebugEnablePickingBones;
        [OptionFlag] public static bool DebugEnableForcePickingBones;
        [OptionFlag] public static bool DebugEnableNoCremation;

        // General Settings
        [OptionFlag] public static bool EnableFlashingLightEffects;

        [OptionFlag] public static bool EnableOsseousAshStartupPopup;

        public static bool DoOsseousAshStartupPopup
        {
            get => EnableOsseousAshStartupPopup;
            set => EnableOsseousAshStartupPopup = value;
        }

        [OptionFlag] public static bool EnableOsseousAshDownloads;
        [OptionFlag] public static bool EnableOsseousAshUploads;
        [OptionFlag] public static string OsseousAshHandle => Config?.Handle ?? DefaultOsseousAshHandle;

        public static async Task SetOsseousAshHandle(string PrependMessage = null)
        {
            EnsureOsseousAshJSON()
                .WriteHandle(
                    Handle: await Popup.AskStringAsync(
                            Message: (!PrependMessage.IsNullOrEmpty() ? $"{PrependMessage}\n\n" : null) +
                                $"Enter a name to associate with your bones files in the {OSSEOUS_ASH}.\n\n" +
                                $"Current handle is:\n{OsseousAshHandle}\n\n" +
                                $"Please note, this name is subject to server-side moderation (to be implemented).",
                            Default: OsseousAshHandle,
                            ReturnNullForEscape: true)
                        ?? OsseousAshHandle);
        }

        public static Guid OsseousAshID => EnsureOsseousAshJSON()?.ID ?? Guid.Empty;

        private static string BaseHandleDisplayText;
        [OptionFlagUpdate]
        public static void UpdateOsseousAshHandleDisplay()
        {
            if (XRL.UI.Options.OptionsByID is Dictionary<string, GameOption> optionsByID
                && optionsByID.TryGetValue("UD_Bones_Folder_OsseousAshHandle", out var handleOption) is true)
            {
                BaseHandleDisplayText ??= handleOption.DisplayText;
                handleOption.DisplayText = $"{BaseHandleDisplayText}: {OsseousAshHandle ?? DefaultOsseousAshHandle}";
            }
        }

        [ModSensitiveCacheInit]
        public static void PerformSetup()
        {
            AskOnStartup().Wait();
            UpdateOsseousAshHandleDisplay();
        }
    }
}
