using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

using Event = XRL.World.Event;

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

        private static int _ChosenPermyriadChance;
        [OptionFlag] public static int ChosenPermyriadChance
        {
            get => _ChosenPermyriadChance;
            set
            {
                _ChosenPermyriadChance = value;
                if (value != -1)
                    LastNonCustomChosenPermyriadChance = value;
            }
        }
        private static int? LastNonCustomChosenPermyriadChance;

        private static int DefaultPermyriadChance => LastNonCustomChosenPermyriadChance ?? 200;
        private static int? _CustomPermyriadChance;
        [OptionFlag] public static int CustomPermyriadChance
        {
            get => _CustomPermyriadChance ?? DefaultPermyriadChance;
            set
            {
                if (value >= 0)
                    _CustomPermyriadChance = value;
                else
                    _CustomPermyriadChance = null;
            }
        }

        public static async Task ManageCustomPermyriadChance()
        {
            while (true)
            {
                int? selected = await AskCustomPermyriadChanceForBones();
                bool? confirmed = await ConfirmCustomPermyriadChanceForBones(selected);

                if (!confirmed.HasValue)
                    break;

                if (confirmed.GetValueOrDefault())
                {
                    CustomPermyriadChance = selected ?? CustomPermyriadChance;
                    break;
                }
            }
        }

        private static async Task<int?> AskCustomPermyriadChanceForBones()
        {
            var sB = Event.NewStringBuilder();
            sB.AppendColored("W", "Manage Bones Encounter Chance")
                .AppendLine().AppendLine();
            sB.Append("Enter an amount between ").AppendColored("C", "1").Append(" and ").AppendColored("C", "10,000")
                .Append(" to represent the chance in 10,000 per newly visited zone that an eligible bones encounter ")
                .Append("will be searched for and, if found, loaded into the zone.")
                .AppendLine().AppendLine();
            sB.Append("The amount you enter, when divided by 100, will be the percent chance.")
                .AppendLine();
            sB.Append("The currently stored value is ").AppendColored("W", $"{CustomPermyriadChance}")
                .Append(" which is equivalent to ").AppendColored("C", $"{(CustomPermyriadChance) / 100.0:0.0#}%").Append(".");

            return await Popup.AskNumberAsync(Event.FinalizeString(sB), CustomPermyriadChance, 1, 10000);
        }

        private static async Task<bool?> ConfirmCustomPermyriadChanceForBones(int? Selected)
        {
            if (Selected == null)
            {
                await ShowNoChangeInPermyriadChanceForBones();
                return true;
            }

            var confirmResult = await Popup.ShowYesNoCancelAsync(
                    Message: Event.FinalizeString(
                        SB: Event.NewStringBuilder("You've entered ")
                            .AppendColored("W", $"{Selected ?? CustomPermyriadChance}").Append(", which is the equivalent of ")
                            .AppendColored("C", $"{(Selected ?? CustomPermyriadChance) / 100.0:0.0#}%")
                            .Append(".").AppendLine().AppendLine()
                            .Append("Keep this value?"))
                    );
            
            switch (confirmResult)
            {
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    return false;
                case DialogResult.Cancel:
                default:
                    await ShowNoChangeInPermyriadChanceForBones();
                    return null;
            }
        }

        private static async Task ShowNoChangeInPermyriadChanceForBones()
        {
            await Popup.ShowAsync(
                    Message: Event.FinalizeString(
                        SB: Event.NewStringBuilder("Your custom permyriad chance (")
                            .AppendColored("W", $"{CustomPermyriadChance}").Append(", ")
                            .AppendColored("C", $"{(CustomPermyriadChance) / 100.0:0.0#}%")
                            .Append(") remains unchanged."))
                    );
        }

        public static int GetPermyriadChanceForBones()
        {
            int chance = ChosenPermyriadChance != -1
                ? ChosenPermyriadChance
                : CustomPermyriadChance
                ;
            return Math.Clamp(chance, 1, 10000);
        }

        private static HashSet<string> LockedMembers = new();
        private static bool? _EnableOsseousAshStartupPopup;
        [OptionFlag] public static bool EnableOsseousAshStartupPopup
        {
            get => (_EnableOsseousAshStartupPopup ??= Config?.AskAtStartup) ?? false;
            set
            {
                LockedMembers ??= new();
                if (!LockedMembers.Contains(nameof(EnableOsseousAshStartupPopup)))
                {
                    LockedMembers.Add(nameof(EnableOsseousAshStartupPopup));
                    try
                    {
                        if (Config != null
                            && Config.AskAtStartup != value)
                        {
                            Config.WriteAskAtStartup(value);
                        }
                    }
                    finally
                    {
                        LockedMembers.Remove(nameof(EnableOsseousAshStartupPopup));
                    }
                }
                if (_EnableOsseousAshStartupPopup != value)
                    _EnableOsseousAshStartupPopup = value;
            }
        }

        [OptionFlag] public static bool EnableOsseousAshDownloads;
        [OptionFlag] public static bool EnableOsseousAshUploads;
        [OptionFlag] public static string OsseousAshHandle => Config?.Handle ?? DefaultOsseousAshHandle;

        public static async Task ManageOsseousAshHandle()
        {
            while (true)
            {
                string entered = await AskOsseousAshHandle(null);
                bool? confirmed = await ConfirmOsseousAshHandle(entered);

                if (!confirmed.HasValue)
                    break;

                if (confirmed.GetValueOrDefault())
                {
                    Config.WriteHandle(entered ?? OsseousAshHandle);
                    break;
                }
            }
        }

        private static async Task<string> AskOsseousAshHandle(string PrependMessage)
        {
            var sB = Event.NewStringBuilder();
            sB.AppendColored("W", $"Manage {OSSEOUS_ASH} Handle")
                .AppendLine().AppendLine();
            if (!PrependMessage.IsNullOrEmpty())
                sB.Append(PrependMessage)
                    .AppendLine().AppendLine();

            sB.Append($"Enter a name to associate with your bones files in the ").Append(OSSEOUS_ASH).Append(".")
                .AppendLine().AppendLine();
            sB.Append("Current handle is:")
                .AppendLine().Append(OsseousAshHandle)
                .AppendLine().AppendLine();
            sB.Append("Please note, this name is subject to server-side moderation (to be implemented).");

            return await Popup.AskStringAsync(Event.FinalizeString(sB), Default: OsseousAshHandle, ReturnNullForEscape: true);
        }

        private static async Task<bool?> ConfirmOsseousAshHandle(string Entered)
        {
            if (Entered == null)
            {
                await ShowNoChangeInOsseousAshHandle();
                return true;
            }

            var confirmResult = await Popup.ShowYesNoCancelAsync(
                    Message: Event.FinalizeString(
                        SB: Event.NewStringBuilder("The handle you've entered will appear like this:")
                            .AppendLine().Append(Entered)
                            .AppendLine().AppendLine()
                            .Append("Keep this handle?"))
                    );

            switch (confirmResult)
            {
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    return false;
                case DialogResult.Cancel:
                default:
                    await ShowNoChangeInOsseousAshHandle();
                    return null;
            }
        }

        private static async Task ShowNoChangeInOsseousAshHandle()
        {
            await Popup.ShowAsync(
                    Message: Event.FinalizeString(
                        SB: Event.NewStringBuilder("Your existing handle, ")
                            .Append(OsseousAshHandle)
                            .Append(", remains unchanged."))
                    );
        }

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
