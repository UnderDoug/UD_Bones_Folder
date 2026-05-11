using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Platform.IO;

using Qud.UI;

using UD_Bones_Folder.Mod.UI;

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

        public static int DefaultPermyriadChance => LastNonCustomChosenPermyriadChance ?? 200;

        private static int? _CustomPermyriadChance;
        [OptionFlag] public static int CustomPermyriadChance => (_CustomPermyriadChance ??= Config?.CustomPermyriadChance) ?? DefaultPermyriadChance;

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
                    if (selected != CustomPermyriadChance)
                    {
                        _CustomPermyriadChance = Math.Clamp(selected.GetValueOrDefault(), 0, 10000);
                        Config?.WriteCustomPermyriadChance(_CustomPermyriadChance.GetValueOrDefault());
                    }
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

        public static async Task ManageOsseousAshHandleAsync()
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

        public static async Task ManageOsseousAshIDAsync()
        {
            PickOptionDataSetAsync<Configuration, UIUtils.CascadableResult> options = new();
            var sB = Event.NewStringBuilder();
            do
            {
                sB.Clear();
                sB.Append("Use the options below to manage your ").Append(OSSEOUS_ASH).Append(" ID.")
                    .AppendLine()
                    .AppendLine().Append("Your current ").Append(OSSEOUS_ASH).Append(" ID is:")
                    .AppendLine().AppendColored("W", Config.ID.ToString())
                    .AppendLine()
                    .AppendLine().Append("If you've been asked for your ").Append(OSSEOUS_ASH).Append(" ID by a server owner, ")
                    .Append("you can use the option below to copy it to your clipboard.")
                    .AppendLine()
                    .AppendLine().Append("Be careful sharing it, because it's one of the ways an ").Append(OSSEOUS_ASH).Append(" remote host ")
                    .Append("is able to restrict access.")
                    .AppendLineEnd();

                options.Clear();
                options.Add(new()
                {
                    Element = Config,
                    Text = "copy to clipboard",
                    Icon = new Renderable(
                            Tile: "Items/sw_unfurled_scroll1.bmp",
                            ColorString: "&w",
                            TileColor: "&w",
                            DetailColor: 'Y'),
                    Hotkey = 'c',
                    Callback = PerformCopyOsseousAshIDToClipboardAsync,
                });
                options.Add(new()
                {
                    Element = Config,
                    Text = "generate new ID",
                    Icon = new Renderable(
                            Tile: "UI/sw_newchar.bmp",
                            ColorString: "&W",
                            TileColor: "&W",
                            DetailColor: 'y'),
                    Hotkey = 'n',
                    Callback = PerformNewOsseousAshIDAsync,
                });
            }
            while ((await UIUtils.PerformPickOptionAsync(
                OptionDataSet: options,
                Title: $"Manage {OSSEOUS_ASH} ID".Colored("yellow"),
                Intro: sB.ToString(),
                IntroIcon: new Renderable(
                    Tile: "Items/sw_credit_wedge.bmp",
                    ColorString: "&y",
                    TileColor: "&y",
                    DetailColor: 'K'),
                DefaultSelected: 0,
                OnBackCallback: () => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                OnEscapeCallback: () => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                FinalSelectedCallback: UIUtils.ShowEscancellepedAsync)).IsContinue());

            Event.ResetTo(sB);
        }

        private static async Task<UIUtils.CascadableResult> PerformCopyOsseousAshIDToClipboardAsync(Configuration Config)
        {
            ClipboardHelper.SetClipboardData(Config.ID.ToString());
            await Popup.NewPopupMessageAsync($"{OSSEOUS_ASH} ID copied to clipboard.");

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformNewOsseousAshIDAsync(Configuration Config)
        {
            var newOAID = Guid.NewGuid();

            switch (await ConfirmOsseousAshID(newOAID))
            {
                case UIUtils.CascadableResult.Continue:
                    Config.WriteID(newOAID);
                    return UIUtils.CascadableResult.Continue;

                case UIUtils.CascadableResult.Back:
                case UIUtils.CascadableResult.BackSilent:
                    return UIUtils.CascadableResult.BackSilent;

                case UIUtils.CascadableResult.Cancel:
                case UIUtils.CascadableResult.CancelSilent:
                default:
                    return UIUtils.CascadableResult.CancelSilent;
            };
        }

        private static async Task<UIUtils.CascadableResult> ConfirmOsseousAshID(Guid OAID)
        {
            if (OAID == Guid.Empty)
            {
                await ShowNoChangeInOsseousAshID();
                return UIUtils.CascadableResult.BackSilent;
            }

            var sB = Event.NewStringBuilder("Your new ").Append(OSSEOUS_ASH).Append(" ID will be:")
                .AppendLine().Append(OAID)
                .AppendLine()
                .AppendLine().AppendColored("r", "Warning").Append(": Changing your ").Append(OSSEOUS_ASH)
                .Append(" ID has a few implications:")
                .AppendBulletLine("r").Append("It will unaffiliate you from any bones you've already had saved/uploaded.")
                .AppendBulletLine("r").Append("It will effectively reset any bones stats, such as the frequency with which you've ")
                .Append("encountered, defeated, or been defeated by a given set of bones.")
                .AppendBulletLine("r").Append("Remote hosts which have your ").Append(OSSEOUS_ASH).Append(" ID whitelisted will no longer ")
                .Append("recognise you unless you pass on your new one to whomever manages a given server.")
                .AppendLine()
                .AppendLine().Append("Keep this new ID?");

            var confirmResult = await Popup.ShowYesNoCancelAsync(Event.FinalizeString(sB));

            switch (confirmResult)
            {
                case DialogResult.Yes:
                    return UIUtils.CascadableResult.Continue;
                case DialogResult.No:
                    return UIUtils.CascadableResult.BackSilent;
                case DialogResult.Cancel:
                default:
                    await ShowNoChangeInOsseousAshID();
                    return UIUtils.CascadableResult.CancelSilent;
            }
        }

        private static async Task ShowNoChangeInOsseousAshID()
        {
            await Popup.ShowAsync(
                    Message: Event.FinalizeString(
                        SB: Event.NewStringBuilder("Your existing ").Append(OSSEOUS_ASH).Append(" ID, ")
                            .AppendColored("W", Config.ID.ToString())
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
            // AskOnStartup().Wait();
            UpdateOsseousAshHandleDisplay();
        }
    }
}
