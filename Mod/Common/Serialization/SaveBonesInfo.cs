using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;

using Platform.IO;

using Qud.API;
using Qud.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;

using ColorUtility = ConsoleLib.Console.ColorUtility;
using Event = XRL.World.Event;

using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.OsseousAsh;
using UD_Bones_Folder.Mod.UI;
using XRL.World.Text;
using UD_Bones_Folder.Mod.Serialization;
using UD_Bones_Folder.Mod.BonesSystem.BonesSource;
using UD_Bones_Folder.Mod.Logging;

using Debug = UD_Bones_Folder.Mod.Logging.Debug;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class SaveBonesInfo
        : SaveGameInfo
        , IBonesFileElement
        , IComparable<SaveBonesInfo>
        , IDisposable
        , IVomitable<SaveBonesInfo>
    {
        #region Debug Registration
        [RegistersMethodsForDebug]
        public static void doDebugRegistry(DebugMethodRegistry Registry)
            => Registry.RegisterEach(
                Type: typeof(SaveBonesInfo),
                MethodNameValues: new()
                {
                    { nameof(GetModsDifferInfo), false },
                });


        [RegistersMethodsForDebug]
        public static void doDebugRegistrySpecial(DebugMethodRegistry Registry)
        {
            Registry.RegisterEach(
                Type: typeof(SaveBonesInfo),
                // true
                new MethodRegistryEntry(DebugMethodRegistry.GetAsyncMethod(typeof(SaveBonesInfo), nameof(RestoreModsLoadedAsync)), false)
            );
        }
        #endregion

        public class SaveBonesInfoComparer : IComparer<SaveBonesInfo>
        {
            public bool Ascending;

            public SaveBonesInfoComparer()
            {
                Ascending = false;
            }

            public SaveBonesInfoComparer(bool Ascending)
                : base()
            {
                this.Ascending = Ascending;
            }

            public int Compare(SaveBonesInfo x, SaveBonesInfo y)
            {
                int modifier = Ascending ? -1 : 1;

                if (y == null)
                {
                    if (x != null)
                        return -1 * modifier;
                    else
                        return 0;
                }
                else
                if (x == null)
                    return 1 * modifier;

                return x.CompareTo(y) * modifier;
            }
        }

        public static Configuration Config => Configuration.Instance;

        public static string Sav => $"{BonesManager.BonesFileName}.sav";
        public static string SavGz => $"{Sav}.gz";
        public static string SavGzBak => $"{SavGz}.bak";
        public static string Json => SaveBonesJSON.FileName;

        public static SaveBonesInfoComparer SaveBonesInfoComparerDescending = new SaveBonesInfoComparer(Ascending: true);

        public static IRenderable NonePleaseIcon = new BonesRender(
                Blueprint: GameObjectFactory.Factory.GetBlueprintIfExists("UD The Lunar Face"),
                HFlip: false)
            .SetTileColor("&K")
            .SetDetailColor('K');

        public static IRenderable RollItIcon = new BonesRender(
            Tile: "Abilities/sw_skill_pointed_circle.png",
            ColorString: "&y",
            TileColor: "&y",
            DetailColor: 'W');

        public static IRenderable ModConfigIcon = new Renderable(
            Tile: "Abilities/sw_skill_tinkering.png",
            ColorString: "&y",
            TileColor: "&y",
            DetailColor: 'c');

        public static IRenderable UnavailableModsIcon = new Renderable(
            Tile: "Abilities/abil_berate.bmp",
            ColorString: $"&K",
            TileColor: $"&K",
            DetailColor: 'R');

        public static IRenderable ManageConfigIcon = new Renderable(
            Tile: "Abilities/sw_skill_tinkering.png",
            ColorString: "&y",
            TileColor: "&y",
            DetailColor: 'c');

        public static IRenderable DebugInternalsIcon = GameObjectFactory
            .Factory
            ?.GetBlueprintIfExists("Mirror Bug")
            ?.GetRenderable();

        public static IRenderable YesAvailableModsIcon = new Renderable(UnavailableModsIcon).setDetailColor('y');

        public bool IsYou
        {
            get
            {
                if (JSON is not SaveBonesJSON bonesJSON)
                    return true;

                if (bonesJSON.IsYou is bool isYou)
                    return isYou;

                return true;
            }
        }

        public SaveBonesJSON JSON
        {
            get => json as SaveBonesJSON;
            set => json = value;
        }

        public string UserHandle => JSON?.Handle;

        public DateTime SaveTimeValue;

        public string ModVersion => JSON?.ModVersion;

        public string DeathReason => JSON?.DeathReason ?? "They died mysteriously...";

        public string GenotypeName => JSON?.GenotypeName ?? "Human";
        public string SubtypeName => JSON?.SubtypeName ?? "Adventurer";

        public BonesSpec BonesSpec
        {
            get
            {
                if (JSON == null)
                    return null;

                return JSON.BonesSpec ??= new();
            }
            protected set
            {
                if (JSON != null)
                    JSON.BonesSpec = value;
            }
        }

        public BonesStats Stats
        {
            get
            {
                if (JSON == null)
                    return null;

                return JSON.BonesStats ??= new();
            }
            protected set
            {
                if (JSON != null)
                    JSON.BonesStats = value;
            }
        }

        private bool? _IsMad;
        public bool IsMad => _IsMad ??= (JSON?.IsCharIconSwapped() is true)
            || !GameObjectFactory.Factory.HasBlueprint(JSON?.Blueprint ?? "MISSING_BLUEPRINT")
            ;

        public ModRecordSet EnabledMods
        {
            get
            {
                if (JSON == null)
                    return null;

                return JSON.EnabledMods ??= new();
            }
            protected set
            {
                if (JSON != null)
                    JSON.EnabledMods = value;
            }
        }

        private ModsDifferInfo _ModsDiffer;
        public ModsDifferInfo ModsDiffer => _ModsDiffer ??= GetModsDifferInfo();

        public bool WasCremated;

        public bool? _IsEligible;
        public bool IsEligible => _IsEligible ??= IsEligibleForCurrentSave(Strict: true);

        public bool? _IsLooselyEligible;
        public bool IsLooselyEligible => _IsLooselyEligible ??= IsEligibleForCurrentSave();

        private BonesRender _Render;
        public BonesRender Render => _Render ??= new(JSON);
        public BonesRender FlippedRender => new(Render, HFlip: !Render.GetHFlip());

        public IRenderable StatsIcon => new Renderable(
            Tile: "Items/sw_bookshelf1.bmp,Items/sw_bookshelf2.bmp,Items/sw_bookshelf3.bmp".CachedCommaExpansion().GetRandomElementCosmetic(),
            ColorString: "&w",
            TileColor: "&w",
            DetailColor: Render.GetDetailColor());

        public string BonesBakDisplay
        {
            get {
                if (BonesSources.FirstOrDefault(bs => bs is BonesFolder bf && bf.IsPreferredLocation) is BonesFolder bonesFolder)
                    return BonesFolder.SanitizePathForDisplay(bonesFolder.CombinePath(SavGzBak));
                return SavGzBak;
            }
        }

        public string TaggedDisplayDirectory => BonesSources.GetBestSource(BonesID).DisplayName;

        public bool IsDummy;

        private BonesSources _BonesSources;
        public BonesSources BonesSources => _BonesSources ??= BonesSources.GetFromPool();

        public bool IsCrematable
            => !IsDummy
            && BonesSources.Any(IsSourceCrematable)
            ;

        public bool IsDownloaded
            => /*!IsYou
            && */JSON is SaveBonesJSON bonesJSON
            && bonesJSON.Downloaded.GetValueOrDefault()
            ;

        protected bool? _IsBlocked;
        public bool IsBlocked => _IsBlocked ??= ServerUtils.IsBonesBlocked(ID);

        public bool IsGenerated => JSON?.GameMode == BonesModeModule.BONES_MODE;

        [JsonIgnore]
        public string BonesID
        {
            get => JSON?.BonesID;
            set
            {
                if (JSON is SaveBonesJSON bonesJSON)
                    bonesJSON.BonesID = value;
            }
        }

        public SaveBonesInfo()
            : base()
        { }

        public static SaveBonesInfo DummyBonesInfo(
            BonesManagement.VisibilityModes VisibilityMode,
            IBonesSource BonesSource
            )
            => SaveBonesJSON.CreateInfoFrom(
                SaveBonesJSON: SaveBonesJSON.DummyBonesJSON(VisibilityMode),
                Directory: BonesSource.SourceID,
                SaveSize: 0L,
                IsDummy: true)
            ;

        public string GetModsDifferString()
            => ModsDiffer?.ToString()
            ?? "{{y|mods:}} {{K|N/A}}"
            ;

        public string GetSourceTypesColorString(bool IncludeAdditionalPips = true)
        {
            string output = null;
            if (!BonesSources.IsNullOrEmpty())
                output = BonesSources.GetSourceTypesColorString(IncludeAdditionalPips);

            if (!output.IsNullOrEmpty())
                return output;

            return IBonesSource.SourceType.None.Tag(false);
        }

        public string GetVersionWarning()
        {
            string versionString = null;
            if (JSON is SaveBonesJSON saveBonesJSON)
            {
                string versionColor = null;
                if (saveBonesJSON.SaveVersion < Const.MIN_SAVE_VERSION)
                {
                    versionString = $"Incompatible Version".Colored("R");
                    versionColor = "R";
                }
                else
                if (saveBonesJSON.SaveVersion < XRLGame.SaveVersion)
                {
                    versionString = $"Older Version".Colored("R");
                    versionColor = "R";
                }
                else
                if (saveBonesJSON.SaveVersion > XRLGame.SaveVersion)
                {
                    versionString = $"Newer Version".Colored("r");
                    versionColor = "r";
                }

                if (!versionString.IsNullOrEmpty())
                    versionString = $"{versionString} ({saveBonesJSON.GameVersion})".Colored(versionColor);
            }
            return versionString;
        }

        public string GetName()
            => $"{(IsMad ? "Mad " : null)}{Name}"
                .StartReplace()
                .ToString()
            ;

        public static XRL.Version GetModVersion(string ModVersion)
            => new(ModVersion)
            ;

        public XRL.Version GetModVersion()
            => GetModVersion(ModVersion)
            ;

        public SaveBonesInfo AddSource(IBonesSource BonesSource)
        {
            BonesSources.AddDistinct(BonesSource);
            return this;
        }

        public SaveBonesInfo RemoveSource(IBonesSource BonesSource)
        {
            BonesSources.Remove(BonesSource);
            return this;
        }

        public SaveBonesInfo RemoveSourceByType(IBonesSource.SourceType Type)
        {
            BonesSources.RemoveAll(bs => bs.Type == Type);
            return this;
        }

        public IEnumerable<string> GetBonesMenuDataRowStrings(bool IncludeVersionWarning = false)
        {
            yield return $"{GetName()} :: {Description}".Colored("W");
            yield return ColorUtility.CapitalizeExceptFormatting(Info);
            yield return $"{DeathReason} on {GetSaveTimeString()}";
            yield return $"{Size} {"{" + ID + "} "}".Colored("K");

            if (IncludeVersionWarning)
                yield return GetVersionWarning();
        }

        public string GetBonesMenuDataRowString(int N, bool IncludeVersionWarning)
        {
            var rowStrings = GetBonesMenuDataRowStrings(IncludeVersionWarning);
            var rowStringsCount = rowStrings.Count();
            if (N < rowStringsCount
                && N >= 0)
            {
                int i = 0;
                foreach (var rowString in rowStrings)
                    if (N == i++)
                        return rowString;
            }
            throw new ArgumentOutOfRangeException(nameof(N), $"Must be between 0 and {rowStringsCount - 1} inclusive.");
        }

        public string GetBonesMenuDataRowString(int N)
            => GetBonesMenuDataRowString(N, IncludeVersionWarning: false)
            ;

        private void IncrementStat(Func<bool> IncrementStatFunc, string StatName = null)
        {
            // Utils.Log($"{nameof(IncrementStat)} {StatName ?? "NO_STAT_NAME"} via {nameof(FileLocationData)}: {FileLocationData?.SanitiseForDisplay(Json) ?? "NO_DATA"}");
            if (JSON is not SaveBonesJSON bonesJSON)
            {
                Utils.Warn($"Attempted to {nameof(IncrementStat)} {StatName ?? "NO_STAT_NAME"} for null {nameof(SaveBonesJSON)}.");
                return;
            }
            if (!IncrementStatFunc.Invoke())
            {
                Utils.Log($"Failed to {nameof(IncrementStat)} {StatName ?? "NO_STAT_NAME"}, {nameof(IncrementStatFunc)} returned {false}");
                return;
            }

            foreach (var bonesSource in BonesSources)
            {
                try
                {
                    bonesSource.IncrementStat(BonesID, StatName);
                }
                catch (Exception x)
                {
                    Utils.Warn($"{bonesSource.SourceID} failed to {nameof(IncrementStat)} {StatName ?? "NO_STAT_NAME"}", x);
                }
            }
        }

        public void IncrementEncountered()
            => IncrementStat(
                IncrementStatFunc: () => Stats?.IncrementEncountered() is true,
                StatName: Utils.CallChain(nameof(SaveBonesInfo), nameof(Stats), nameof(Stats.Encountered)))
            ;

        public void IncrementDefeated()
            => IncrementStat(
                IncrementStatFunc: () => Stats?.IncrementDefeated() is true,
                StatName: Utils.CallChain(nameof(SaveBonesInfo), nameof(Stats), nameof(Stats.Defeated)))
            ;

        public void IncrementReclaimed()
            => IncrementStat(
                IncrementStatFunc: () => Stats?.IncrementReclaimed() is true,
                StatName: Utils.CallChain(nameof(SaveBonesInfo), nameof(Stats), nameof(Stats.Reclaimed)))
            ;

        public void IncrementBroken()
            => IncrementStat(
                IncrementStatFunc: () => Stats?.IncrementBroken() is true,
                StatName: Utils.CallChain(nameof(SaveBonesInfo), nameof(Stats), nameof(Stats.Broken)))
            ;

        public async Task<BonesSavGz> LoadBonesSavGzAsync()
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(indent[1]);

            using var bonesSources = BonesManager.GetBonesSourcesFromPool().Intersect(BonesSources);
            // using var bonesSources = BonesManager.GetBonesSourcesFromPool(bs => BonesSources.Any(tbs => BonesSources.DefaultEQC.Equals(bs, tbs)));

            _ = Debug.YehNah(nameof(bonesSources), bonesSources.GetNullString(), Good: bonesSources.Count > 0, Indent: indent[2])[1];
            return await bonesSources.GetBonesSavGzAsync(ID);
        }

        public async Task<BonesSavGz> GetBonesSavGzStreamAsync()
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(indent[1]);

            if (await LoadBonesSavGzAsync() is not BonesSavGz bonesSavGz)
                return null;

            if (!bonesSavGz.TryEnsureLoadable())
            {
                Debug.CheckNah($"{nameof(bonesSavGz)}.{nameof(bonesSavGz.TryEnsureLoadable)}", Indent: indent[1]);
                bonesSavGz.Dispose();
                return null;
            }

            Debug.CheckYeh($"{nameof(bonesSavGz)}.{nameof(bonesSavGz.TryEnsureLoadable)}", Indent: indent[1]);
            return bonesSavGz;
        }

        public async Task<bool> TryGetBonesSavGzStreamAsync(Action<BonesSavGz> SuccessCallback)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(indent[1]);

            if (SuccessCallback == null)
            {
                Debug.CheckNah(nameof(SuccessCallback), "null", Indent: indent[1]);
                return false;
            }

            if (await GetBonesSavGzStreamAsync() is not BonesSavGz bonesSavGz)
            {
                Debug.CheckNah(nameof(bonesSavGz), "null", Indent: indent[1]);
                return false;
            }

            SuccessCallback.Invoke(bonesSavGz);
            Debug.CheckYeh(nameof(bonesSavGz), nameof(SuccessCallback), Indent: indent[1]);
            return true;
        }

        public IEnumerable<string> GetDebugLines()
        {
            yield return $"{nameof(ID)}: {ID}";
            yield return $"{nameof(Name)}: {Name}";

            yield return $"{nameof(WasCremated)}: {WasCremated}";

            yield return $"{nameof(IsMad)}: {IsMad}";

            yield return $"{nameof(IsEligible)}: {IsEligible}";
            yield return $"{nameof(IsLooselyEligible)}: {IsLooselyEligible}";

            var bonesJSON = JSON;

            yield return $"{nameof(bonesJSON.DeathReason)}: {bonesJSON.DeathReason}";
            yield return $"{nameof(bonesJSON.Location)}: {bonesJSON.Location}";

            yield return $"{nameof(bonesJSON.GameVersion)}: {bonesJSON?.GameVersion ?? "missing"}";
            yield return $"{nameof(bonesJSON.SaveVersion)}: {(bonesJSON?.SaveVersion)?.ToString() ?? "missing"}";
            yield return $"{nameof(ModVersion)}: {ModVersion ?? "missing"}";

        }

        public static DateTime GetSaveTime(SaveBonesJSON BonesJSON)
        {
            try
            {
                return new DateTime(BonesJSON.SaveTimeValue).ToLocalTime();
            }
            catch
            {
                return new DateTime(2026, 04, 01, 11, 59, 59, DateTimeKind.Local);
            }
        }

        public DateTime GetSaveTime()
            => GetSaveTime(JSON)
            ;

        public static string GetSaveTimeString(DateTime SaveTime)
            => $"{SaveTime.ToLongDateString()} at {SaveTime.ToLongTimeString()}"
            ;

        public static string GetSaveTimeString(SaveBonesJSON BonesJSON)
            => GetSaveTimeString(GetSaveTime(BonesJSON))
            ;

        public string GetSaveTimeString()
            => GetSaveTimeString(GetSaveTime())
            ;

        public string GetBlurbString()
        {
            var tB = TextBuilder.Get();

            tB.Append("These bones ");
            if (IsYou)
                tB.Append("are yours");
            else
                tB.Append("come from ").Append(UserHandle);
            tB.Append(".");

            var preferredBonesSource = BonesSources.FirstOrDefault() ?? BonesPile.DummyFolder;

            tB.AppendLine()
                .AppendLine().Append("They ");

            if (preferredBonesSource.Type != IBonesSource.SourceType.None)
                tB.Append("were ");

            tB.Append(preferredBonesSource.BlurbSavedVerb).Append(" ");

            tB.Append(preferredBonesSource.BlurbDisplayName)
                .Append(" on ").Append(GetSaveTimeString()).Append(", ")
                .AppendRule(SaveTimeValue.TimeAgo("ago")).Append(".")
                .AppendLine();

            if ((BonesSources?.Count ?? 0) > 1)
            {
                tB.AppendLine().Append("There is also a copy ");

                if (BonesSources.Count == 2)
                {
                    var otherLocation = BonesSources[1];
                    tB.Append(otherLocation.BlurbSavedVerb).Append(" ").Append(otherLocation.BlurbDisplayName);
                }
                else
                {
                    tB.Append("in the following additional locations:");
                    for (int i = 1; i < BonesSources.Count; i++)
                        tB.AppendLine()
                            .Append(BonesSources[i].BlurbSavedVerb);
                }
                tB.AppendLine();
            }

            tB.AppendLine()
                .Append("They are from version ").AppendRule(GetModVersion()).Append(" of the ").AppendColored("Y", Utils.ModTitle).Append(" mod");
            if (GetModVersion() == Utils.ModVersion)
                tB.Append(", which is the current one");
            tB.Append(".")
                .AppendLine()
                .AppendLine().AppendBonesStatsBlurb(Stats, Name)
                .AppendLine();

            bool showModConfigLine = !ModsDiffer.IsDummy
                && !ModsDiffer.IsError;
            if (GetModVersion() is XRL.Version infoVersion
                && Utils.ModVersion is XRL.Version modVersion)
            {
                bool saveIsLaterVersion = modVersion < infoVersion;
                bool buildVersionDiffers = modVersion.Build != infoVersion.Build;
                int versionDiff = modVersion.Revision - infoVersion.Revision;
                if (saveIsLaterVersion
                    || buildVersionDiffers)
                {
                    showModConfigLine = false;
                    tB.AppendLine().Append("Based on the ").AppendRule("version difference").Append(" between the ").AppendColored("Y", Utils.ModTitle)
                        .Append(" mod (").AppendRule(modVersion).Append("), ")
                        .Append("and these bones (").AppendRule(infoVersion).Append("), they are ineligible to be loaded.")
                        .AppendLine();
                }
                else
                if (versionDiff > 0)
                {
                    showModConfigLine = false;
                    tB.AppendLine().Append("Based on your current mod configuration, and the version disparty, these bones are weighted ")
                        .AppendRule((EnabledMods.GetBonesWeight() ?? 0).ToString()).Append(", compared to the default of ").Append(ModRecordSet.BaseBonesWeight).Append(".")
                        .AppendLine();
                }
            }
            if (showModConfigLine)
            {
                tB.AppendLine().Append("Based on your current mod configuration, these bones are weighted ")
                    .AppendRule((EnabledMods.GetBonesWeight() ?? 0).ToString()).Append(", compared to the default of ").Append(ModRecordSet.BaseBonesWeight).Append(".")
                    .AppendLine();
            }
            else
            {
                if (ModsDiffer.IsDummy)
                    tB.AppendLine().Append("These bones are a placeholder and contain no data. As such, they have no mod configuration based weight for being loaded.");
                else
                    tB.AppendLine().Append("These bones have a malformed or corrupt mod configuration and, as such, cannot be weighted for encounter.");
                tB.AppendLine();
            }

            return tB.ToString();
        }

        public async Task<UIUtils.CascadableResult> ShowBlurbAsync()
        {
            SoundManager.PlayUISound("Sounds/UI/ui_notification", 1f, Combat: false, Interface: true);
            await Popup.NewPopupMessageAsync(
                message: Markup.Transform(GetBlurbString()),
                buttons: UIUtils.BackancelButton,
                contextTitle: $"Bones File Stats".Colored("yellow"),
                afterRender: FlippedRender,
                PopupID: $"{nameof(SaveBonesInfo)}.{nameof(ShowBlurbAsync)}::{ID}");

            return UIUtils.CascadableResult.Continue;
        }

        public static async Task<UIUtils.CascadableResult> ShowBlurbAsync(SaveBonesInfo SaveBonesInfo)
            => await SaveBonesInfo.ShowBlurbAsync()
            ;

        public static async Task<UIUtils.CascadableResult> AskForBonesReportAsync(SaveBonesInfo SaveBonesInfo)
        {
            if (await ServerUtils.TryReportBonesAsync(SaveBonesInfo.ID, null))
            {
                SaveBonesInfo._IsBlocked = null;
            }
            return UIUtils.CascadableResult.Continue;
        }

        public static async Task<UIUtils.CascadableResult> BlockUnblockAsync(SaveBonesInfo SaveBonesInfo)
        {
            if (!SaveBonesInfo.IsBlocked)
            {
                /*var existingReports = OsseousAsh.Hosts.GetReportsMatchingSpec(OsseousAsh.Config.ID, SaveBonesInfo.ID);
                if (!existingReports.IsNullOrEmpty())
                {
                    foreach (var report in existingReports)
                    {

                    }
                }*/
                switch (await Popup.ShowYesNoCancelAsync(
                    Message: "{{yellow|Please Note}}: It's not currently possible to unblock bones " +
                        "without contacting the server admin to have them do it manually.\n\n" +
                        "Are you sure you want to block these bones?"))
                {
                    case DialogResult.Cancel:
                        return UIUtils.CascadableResult.Cancel;
                    case DialogResult.Yes:
                        if (await ServerUtils.TryReportBonesAsync(
                            Report: new Report
                            {
                                UserID = Config.ID,
                                BonesID = SaveBonesInfo.ID,
                                Blocked = true,
                                Type = Report.ReportTypes.Other,
                                Description = "For Block",
                            },
                            Silent: true))
                        {
                            SaveBonesInfo._IsBlocked = null;
                        }
                        //Utils.Log("BlockUnblockAsync: DialogResult.Yes");
                        return UIUtils.CascadableResult.BackSilent;
                    case DialogResult.No:
                    default:
                        break;
                }
            }
            else
            {
                await Popup.NewPopupMessageAsync("Unblocking is unfortunately not supported in this verson of the OsseousAsh REST API.\n\n" +
                    "Please contact the server admin to have the block manually removed");
            }
            return UIUtils.CascadableResult.Continue;
        }

        public bool IsSourceCrematable(IBonesSource BonesSource)
            => IsYou
            || (BonesSource?.IsOnDisk() is true)
            ;

        public async Task<UIUtils.CascadableResult> CremateAsync(bool PickSources = true, [CallerMemberName] string CallingMethod = "")
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(CallingMethod), CallingMethod),
                });

            if (!IsCrematable)
            {
                Debug.CheckNah($"not {nameof(IsCrematable)}", Indent: indent[2]);
                return UIUtils.CascadableResult.BackSilent;
            }

            using var bonesSources = PickSources
                ? await BonesSources.SelectBonesSourcesFromPoolAsync(BonesSources.Where(IsSourceCrematable), DefaultValue: true, FirstDefaultOnly: false, Action: "Cremate")
                : BonesSources.CloneFromPool()
                ;

            if (bonesSources == null)
            {
                Debug.CheckNah($"{nameof(bonesSources)} is null", Indent: indent[2]);
                return UIUtils.CascadableResult.CancelSilent;
            }

            if (bonesSources.Count < 1)
            {
                Debug.CheckNah($"{nameof(bonesSources)} is empty", Indent: indent[2]);
                return UIUtils.CascadableResult.Back;
            }

            var results = ScopeDisposedList<ActionResult>.GetFromPool();
            Debug.Log($"Deleting from {bonesSources.Count.Things("bones source")}", Indent: indent[2]);
            bool any = false;
            try
            {
                foreach (var bonesSource in bonesSources)
                {
                    var cremated = await bonesSource.DeleteBonesAsync(BonesID);
                    results.Add(cremated);
                    if (cremated)
                        BonesSources.Remove(bonesSource);

                    Debug.YehNah(bonesSource.DisplayName.Strip(), cremated, Good: cremated, Indent: indent[3]);
                }
                any = results.Any(r => r);
                Debug.YehNah(Debug.CallingTypeAndMethodNames(), Good: any, Indent: indent[1]);
                return any
                    ? UIUtils.CascadableResult.Continue
                    : UIUtils.CascadableResult.BackSilent
                    ;
            }
            finally
            {
                if (any)
                {
                    BonesManager.ClearHasSaveBones();
                    BonesManager.System?.ClearEligibleSaveBonesInfoCache();
                }
            }
        }

        public bool CremateFromAllSources()
            => CremateAsync(false).WaitResult().ToBool()
            ;

        public static async Task<UIUtils.CascadableResult> ShowDebugInternalsAsync(SaveBonesInfo SaveBonesInfo)
            => await SaveBonesInfo.ShowDebugInternalsAsync()
            ;

        public async Task<UIUtils.CascadableResult> ShowDebugInternalsAsync()
        {
            SoundManager.PlayUISound("Sounds/UI/ui_notification", 1f, Combat: false, Interface: true);

            using var tB = TextBuilder.Get();

            foreach (var line in GetDebugInternals("-d"))
                tB.Append(line).AppendLine();

            tB.AppendLineEnd();

            await Popup.NewPopupMessageAsync(
                message: Markup.Transform(tB.ToString()),
                buttons: UIUtils.BackancelButton,
                contextTitle: $"Bones File Debug Internals".Colored("yellow"),
                afterRender: FlippedRender,
                PopupID: $"{nameof(SaveBonesInfo)}.{nameof(ShowDebugInternalsAsync)}::{ID}");

            return UIUtils.CascadableResult.Continue;
        }

        private static IEnumerable<string> GetDebugInternals(
            IVomitable Vomitable,
            int Indent = 0,
            params object[] Parameters
            )
        {
            using var outputs = ScopeDisposedList<string>.GetFromPool();
            foreach ((var member, var doVomit) in IVomitable.GetVomitMembers(Vomitable, Parameters))
            {
                try
                {
                    bool valueOnly = doVomit?.ValueOnly is true;

                    string label = valueOnly
                        ? doVomit?.Label
                            ?? member.Name
                        : null
                        ;

                    if (label != null)
                        label = $"{label}: ";

                    if (member is FieldInfo field)
                    {
                        var fieldValue = field.GetValue(Vomitable);

                        if (fieldValue is IVomitable vomitableField)
                        {
                            var fieldDebugInternals = GetDebugInternals(vomitableField, Indent + 1, Parameters);
                            string fieldDebugInternalsEmpty = fieldDebugInternals.IsNullOrEmpty() ? $" {fieldValue?.ToString() ?? "null"}" : null;
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? field.Name}:{fieldDebugInternalsEmpty}");
                            foreach (var fieldOutput in fieldDebugInternals)
                                outputs.Add(fieldOutput);
                        }
                        else
                        if (fieldValue is IVomitable[] vomitableArray)
                        {
                            string vomitArrayEmpty = vomitableArray.IsNullOrEmpty() ? $" {fieldValue?.ToString() ?? "null"}, empty" : $" {vomitableArray.Length}";
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? field.Name}:{vomitArrayEmpty}");
                            foreach (var vomitable in vomitableArray)
                            {
                                var vomitableDebugInternals = GetDebugInternals(vomitable, Indent + 2, Parameters);
                                string vomitableDebugInternalsEmpty = vomitableArray.IsNullOrEmpty() ? $" empty" : null;
                                outputs.Add($"{(Indent + 1).Indent(NBSP: true)}{fieldValue?.ToString()}:{vomitableDebugInternalsEmpty}");
                                foreach (var vomitableDebugInternal in vomitableDebugInternals)
                                    outputs.Add(vomitableDebugInternal);
                            }
                        }
                        else
                        if (fieldValue is IRenderable fieldRender)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? field.Name}: {GetDebugInternalsString(fieldRender) ?? "null"}");
                        }
                        else
                        if (fieldValue is ModRecordSet fieldModRecords)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? field.Name}: {GetDebugInternalsString(fieldModRecords) ?? "null"}");
                        }
                        else
                        if (fieldValue is BonesSpec fieldBonesSpec)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? field.Name}: {GetDebugInternalsString(fieldBonesSpec) ?? "null"}");
                        }
                        else
                        if (fieldValue is BonesStats fieldBonesStats)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? field.Name}: {GetDebugInternalsString(fieldBonesStats) ?? "null"}");
                        }
                        else
                        if (fieldValue is SaveBonesJSON fieldBonesJSON)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? field.Name}: {GetDebugInternalsString(fieldBonesJSON) ?? "null"}");
                        }
                        else
                        if (fieldValue is object[] objectArray)
                        {
                            string vomitArrayEmpty = objectArray.IsNullOrEmpty() ? $" {fieldValue?.ToString() ?? "null"}, empty" : $" {objectArray.Length}";
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? field.Name}:{vomitArrayEmpty}");
                            foreach (var fieldObject in objectArray)
                                outputs.Add($"{(Indent + 1).Indent(NBSP: true)}{fieldObject?.ToString() ?? "null"}");
                        }
                        else
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? field.Name}: {fieldValue?.ToString() ?? "null"}");
                    }
                    else
                    if (member is PropertyInfo property)
                    {
                        var propValue = property.GetValue(Vomitable);

                        if (propValue is IVomitable vomitableProp)
                        {
                            var propDebugInternals = GetDebugInternals(vomitableProp, Indent + 1, Parameters);
                            string propDebugInternalsEmpty = propDebugInternals.IsNullOrEmpty() ? $" {propValue?.ToString() ?? "null"}" : null;
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? property.Name}:{propDebugInternalsEmpty}");
                            foreach (var propOutput in propDebugInternals)
                                outputs.Add(propOutput);
                        }
                        else
                        if (propValue is IVomitable[] vomitableArray)
                        {
                            string vomitArrayEmpty = vomitableArray.IsNullOrEmpty() ? $" {propValue?.ToString() ?? "null"}, empty" : $" {vomitableArray.Length}";
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? property.Name}:{vomitArrayEmpty}");
                            foreach (var vomitable in vomitableArray)
                            {
                                var vomitableDebugInternals = GetDebugInternals(vomitable, Indent + 2, Parameters);
                                string vomitableDebugInternalsEmpty = vomitableArray.IsNullOrEmpty() ? $" empty" : null;
                                outputs.Add($"{(Indent + 1).Indent(NBSP: true)}{propValue?.ToString()}:{vomitableDebugInternalsEmpty}");
                                foreach (var vomitableDebugInternal in vomitableDebugInternals)
                                    outputs.Add(vomitableDebugInternal);
                            }
                        }
                        else
                        if (propValue is IRenderable propRender)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? property.Name}: {GetDebugInternalsString(propRender) ?? "null"}");
                        }
                        else
                        if (propValue is ModRecordSet propModRecords)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? property.Name}: {GetDebugInternalsString(propModRecords) ?? "null"}");
                        }
                        else
                        if (propValue is BonesSpec propBonesSpec)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? property.Name}: {GetDebugInternalsString(propBonesSpec) ?? "null"}");
                        }
                        else
                        if (propValue is BonesStats propBonesStats)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? property.Name}: {GetDebugInternalsString(propBonesStats) ?? "null"}");
                        }
                        else
                        if (propValue is SaveBonesJSON propBonesJSON)
                        {
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? property.Name}: {GetDebugInternalsString(propBonesJSON) ?? "null"}");
                        }
                        else
                        if (propValue is object[] objectArray)
                        {
                            string vomitArrayEmpty = objectArray.IsNullOrEmpty() ? $" {propValue?.ToString() ?? "null"}, empty" : $" {objectArray.Length}";
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? property.Name}:{vomitArrayEmpty}");
                            foreach (var propObject in objectArray)
                                outputs.Add($"{(Indent + 1).Indent(NBSP: true)}{propObject?.ToString() ?? "null"}");
                        }
                        else
                            outputs.Add($"{Indent.Indent(NBSP: true)}{label ?? property.Name}: {propValue?.ToString() ?? "null"}");
                    }
                }
                catch (Exception x)
                {
                    Utils.Error($"{typeof(SaveBonesInfo)} failed to {nameof(GetDebugInternals)} for {nameof(member)} {member.Name}", x);
                }
            }

            foreach (var output in outputs)
                yield return output;
        }

        private static string GetDebugInternalsString(IRenderable Renderable)
            => Renderable?.getTile() ?? "NO_TILE"
            ;

        private static string GetDebugInternalsString(ModRecordSet ModRecordSet)
            => ModRecordSet.GetNullString()
            ;

        private static string GetDebugInternalsString(BonesSpec BonesSpec)
            => BonesSpec.GetNullString()
            ;

        private static string GetDebugInternalsString(BonesStats BonesStats)
            => BonesStats.GetNullString()
            ;

        private static string GetDebugInternalsString(SaveBonesJSON BonesJSON)
            => BonesJSON.GetNullString()
            ;

        private IEnumerable<string> GetDebugInternals(
            int Indent,
            params object[] Parameters
            )
            => GetDebugInternals(this, Indent, Parameters)
            ;

        private IEnumerable<string> GetDebugInternals(params object[] Parameters)
            => GetDebugInternals(this, 0, Parameters)
            ;

        public bool IsEligibleForCurrentSave(bool Strict = false)
        {
            if (JSON is not SaveBonesJSON bonesJSON)
                return false;

            if (ID != null
                && ID == The.Game?.GameID)
                return false;

            if (IsBlocked)
                return false;

            if (bonesJSON.SaveVersion < Const.MIN_SAVE_VERSION)
                return false;

            if (bonesJSON.SaveVersion < XRLGame.MaxSaveVersion)
                return !Strict; // false;

            if (!GenotypeFactory.GenotypesByName.ContainsKey(bonesJSON.GenotypeName))
                return !Strict;

            if (!SubtypeFactory.SubtypesByName.ContainsKey(bonesJSON.SubtypeName))
                return !Strict;

            if (!GameObjectFactory.Factory.HasBlueprint(bonesJSON.Blueprint))
                return !Strict;

            if (bonesJSON.SaveVersion != XRLGame.SaveVersion)
            {
                if (bonesJSON.SaveVersion < XRLGame.SaveVersion)
                    return !Strict;

                if (bonesJSON.SaveVersion > XRLGame.SaveVersion)
                    return false;
            }

            return true;
        }

        private string GetLevelPair()
            => $"{nameof(SaveBonesJSON.Level)}: {JSON.Level}"
            ;

        private string GetZoneTierPair()
            => $"{nameof(BonesSpec.ZoneTier)}: {BonesSpec.ZoneTier}"
            ;

        private string GetZoneTerrainTypePair()
            => $"TerrainType: {(BonesSpec.ZoneTerrainType.IsNullOrEmpty() ? "none" : BonesSpec.ZoneTerrainType)}"
            ;

        private string GetZoneIDPair()
            => $"{nameof(SaveBonesJSON.ZoneID)}: {JSON.ZoneID}"
            ;

        public string GetBonesPickerLine()
            => JSON != null
            ? $"{GetName()}, {GetLevelPair()}, {GetZoneTierPair()},\n[{BonesSources.GetSourceTypesColorString()}], {GetZoneTerrainTypePair()}, {JSON.ZoneID}"
            : GetName()
            ;

        public bool AttemptLoad(
            Zone Z,
            ZoneBonesAllocation.AllocationTypes Type,
            out bool Blocked,
            string Context = null
            )
            => BonesManager.System.AttemptLoadBones(
                Z: Z,
                PickedBones: this,
                Type: Type, 
                Blocked: out Blocked,
                Context: Context)
            ;

        public async Task<bool> TryRestoreModsAsync()
        {
            await The.UiContext;
            if (await RestoreModsLoadedAsync())
            {
                var goToBones = Task.Run(() =>
                {
                    BonesManagement.instance.Preselected = this;
                    NavigationController.instance.SuspendContextWhile(BonesManagement.instance.BonesMenu).Wait();
                });
                await goToBones;
                return goToBones.IsCompletedSuccessfully;
            }
            return false;
        }

        public async Task<bool> RestoreModsLoadedAsync()
        {
            using var indent = new Indent();

            using var tB = TextBuilder.Get();
            string title = "Mod Configuration Comparison".Colored("yellow");
            if (ModsDiffer.IsError)
            {
                Debug.LogTypeMethod(
                    Indent: indent[1],
                    ArgPairs: new Debug.ArgPair[]
                    {
                        Debug.Arg(nameof(ModsDiffer), nameof(ModsDiffer.IsError)),
                    });

                tB.Append("There was an error loading this Bones' enabled mods.")
                    .AppendLine()
                    .AppendLine().Append("If this bones file comes from online, it's possible some of the data was lost during transfer.")
                    .AppendLine().Append("If it originates from \"on-disk\" then its \"save info\" is likely corrupt.")
                    .AppendLine()
                    .AppendLine().Append("Please consider reporting these bones if they're online and this issue persists.");

                await Popup.NewPopupMessageAsync(
                    message: tB.ToString(),
                    buttons: PopupMessage.SingleButton,
                    title: title,
                    afterRender: ModConfigIcon);

                return false;
            }

            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(EnabledMods), EnabledMods != null ? EnabledMods.Count : "null"),
                    Debug.Arg(nameof(ModsDiffer), $"{(ModsDiffer.IsDummy ? null : "not ")}{nameof(ModsDiffer.IsDummy)}"),
                });

            var unavailableWhereBonesEnabled = ModsDiffer.UnavailableWhereBonesEnabled;
            var enabledWhereBonesDisabled = ModsDiffer.EnabledWhereBonesDisabled;
            var disabledWhereBonesEnabled = ModsDiffer.DisabledWhereBonesEnabled;
            var enabledWhereBonesEnabled = ModsDiffer.EnabledWhereBonesEnabled;

            Debug.Log(nameof(unavailableWhereBonesEnabled), Indent: indent[2]);
            if (Debug.Do())
                Debug.Loggregrate(
                    Source: unavailableWhereBonesEnabled,
                    Proc: r => r.ID,
                    Empty: "none",
                    PostProc: s => $": {s}",
                    Indent: indent[3]);

            Debug.Log(nameof(enabledWhereBonesDisabled), Indent: indent[2]);
            if (Debug.Do())
                Debug.Loggregrate(
                Source: enabledWhereBonesDisabled,
                Proc: r => r.ID,
                Empty: "none",
                PostProc: s => $": {s}",
                Indent: indent[3]);

            Debug.Log(nameof(disabledWhereBonesEnabled), Indent: indent[2]);
            if (Debug.Do())
                Debug.Loggregrate(
                Source: disabledWhereBonesEnabled,
                Proc: r => r.ID,
                Empty: "none",
                PostProc: s => $": {s}",
                Indent: indent[3]);

            Debug.Log(nameof(enabledWhereBonesEnabled), Indent: indent[2]);
            if (Debug.Do())
                Debug.Loggregrate(
                Source: enabledWhereBonesEnabled,
                Proc: r => r.ID,
                Empty: "none",
                PostProc: s => $": {s}",
                Indent: indent[3]);

            UIUtils.CascadableResult result = UIUtils.CascadableResult.Continue;
            int defaultSelection = 0;
            if (ModsDiffer?.IsDummy is false)
            {
                using var options = new CascadablePickOptionDataSetAsync<IEnumerable<ModRecord>>(UIUtils.CascadableResult.CancelSilent, UIUtils.CascadableResult.CancelSilent);
                do
                {
                    tB.Clear();
                    options.ClearItems(Dispose: true);

                    if (!unavailableWhereBonesEnabled.IsNullOrEmpty())
                    {
                        tB.AppendColored("red", "Attention").Append(": one or more mods enabled in this bones file are ")
                            .AppendColored("red", "not available");

                        if (unavailableWhereBonesEnabled.Count > 9)
                            tB.Append(".").AppendLine().Append("Use the menu option below to view the entire list.");
                        else
                            tB.Append("; they'll be listed at the bottom.");
                        
                        tB.AppendLineEnd();
                    }
                    if (!enabledWhereBonesDisabled.IsNullOrEmpty())
                    {
                        enabledWhereBonesDisabled
                            .Select(r => r.ToString())
                            .Aggregate(
                                seed: tB.AppendLine().Append("These ").AppendColored("green", "enabled").Append(" mods are ")
                                    .AppendColored("yellow", "disabled").Append(" in this bones file:"),
                                func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("yellow", n))
                            .AppendLineEnd();
                    }
                    if (!disabledWhereBonesEnabled.IsNullOrEmpty())
                    {
                        disabledWhereBonesEnabled
                            .Select(r => r.ToString())
                            .Aggregate(
                                seed: tB.AppendLine().Append("These ").AppendColored("black", "disabled").Append(" mods are ").AppendColored("red", "enabled").Append(" in this bones file:"),
                                func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("red", n))
                            .AppendLineEnd();
                    }
                    if (!enabledWhereBonesEnabled.IsNullOrEmpty())
                    {
                        enabledWhereBonesEnabled
                            .Select(r => r.ToString())
                            .Aggregate(
                                seed: tB.AppendLine().Append("These ").AppendColored("green", "enabled").Append(" mods are ")
                                    .AppendColored("green", "enabled").Append(" in this bones file:"),
                                func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("green", n))
                            .AppendLineEnd();
                    }
                    if (!unavailableWhereBonesEnabled.IsNullOrEmpty()
                        && unavailableWhereBonesEnabled.Count < 10)
                    {
                        tB.AppendUnavailableMods(unavailableWhereBonesEnabled)
                            .AppendLineEnd();
                    }
                    if (!disabledWhereBonesEnabled.IsNullOrEmpty())
                    {
                        options.Add(new CascadablePickOptionDataAsync<IEnumerable<ModRecord>>
                        {
                            Element = disabledWhereBonesEnabled,
                            Text = "Restart {{yellow|adding enabled}} mods from bones file's mod configuration",
                            Hotkey = 'a',
                            Icon = new Renderable(
                                Tile: "UI/sw_newchar.bmp",
                                ColorString: "&w",
                                TileColor: "&w",
                                DetailColor: 'W'),
                            Callback = (e) => Task.Run(() => EnableMods(e)),
                        });
                    }
                    if (!enabledWhereBonesDisabled.IsNullOrEmpty())
                    {
                        options.Add(new CascadablePickOptionDataAsync<IEnumerable<ModRecord>>
                        {
                            Element = enabledWhereBonesDisabled,
                            Text = "Restart {{red|using}} bones file's {{red|entire}} (available) mod configuration",
                            Hotkey = 'u',
                            Icon = new Renderable(
                                Tile: "UI/sw_lastchar.bmp",
                                ColorString: $"&W",
                                TileColor: $"&W",
                                DetailColor: 'R'),
                            Callback = (e) => Task.Run(() 
                                => (UIUtils.CascadableResult)Math.Min(
                                    val1: (int)DisableMods(e),
                                    val2: (int)EnableMods(disabledWhereBonesEnabled))
                                ),
                        });
                    }
                    if (!unavailableWhereBonesEnabled.IsNullOrEmpty()
                        && unavailableWhereBonesEnabled.Count > 9)
                    {
                        int unavailableCount = unavailableWhereBonesEnabled.Count;
                        using var tb2 = TextBuilder.Get()
                            .Append("Show ").Append(unavailableCount).Append(" ").AppendColored("red", "unavailable")
                            .AppendThings(unavailableCount, " mod").Append(" ").AppendColored("red", "enabled").Append(" in this bones file");
                        options.Add(new CascadablePickOptionDataAsync<IEnumerable<ModRecord>>
                        {
                            Element = unavailableWhereBonesEnabled,
                            Text = tb2.ToString(),
                            Hotkey = 'x',
                            Icon = UnavailableModsIcon,
                            Callback = async delegate (IEnumerable<ModRecord> element)
                            {
                                using var tb3 = TextBuilder.Get().AppendUnavailableMods(element);

                                await Popup.NewPopupMessageAsync(
                                    message: tb3.ToString(),
                                    title: unavailableCount.Things("unavailable mod").Colored("red"),
                                    afterRender: UnavailableModsIcon);

                                return UIUtils.CascadableResult.BackSilent;
                            },
                        });
                    }
                    if (!options.IsNullOrEmpty())
                    {
                        result = await UIUtils.PerformPickOptionAsync(
                            OptionDataSet: options,
                            Title: title,
                            Intro: tB.ToString(),
                            IntroIcon: ModConfigIcon,
                            DefaultSelected: !options.IsNullOrEmpty() ? defaultSelection : -1,
                            LastSelectedCallback: c => defaultSelection = Math.Clamp(c, 0, options.Count - 1),
                            FinalSelectedCallback: async delegate (PickOptionData<IEnumerable<ModRecord>, Task<UIUtils.CascadableResult>> o, Task<UIUtils.CascadableResult> r)
                            {
                                var result = await r?.AwaitResultIfNotIsCompletedSuccessfully();
                                if (result is UIUtils.CascadableResult.Continue)
                                {
                                    ModManager.WriteModSettings();
                                    GameManager.Restart();
                                    return UIUtils.CascadableResult.CancelSilent;
                                }
                                return result;
                            });
                    }
                    else
                    {
                        result = UIUtils.CascadableResult.CancelSilent;
                        await Popup.NewPopupMessageAsync(
                            message: tB.ToString(),
                            title: title,
                            afterRender: ModConfigIcon);
                    }
                }
                while (!result.IsCancel());
            }
            return false;
        }

        public static async Task<UIUtils.CascadableResult> AskRestoreModsAsync(SaveBonesInfo SaveBonesInfo)
            => (await SaveBonesInfo.RestoreModsLoadedAsync())
            ? UIUtils.CascadableResult.Cancel
            : UIUtils.CascadableResult.Continue
            ;

        public static UIUtils.CascadableResult EnableMods(IEnumerable<string> ModsToEnable)
        {
            if (ModsToEnable.IsNullOrEmpty())
                return UIUtils.CascadableResult.BackSilent
                    ;

            bool any = false;
            foreach (string modToEnable in ModsToEnable)
            {
                if (ModManager.GetMod(modToEnable) is ModInfo modInfo
                    && modInfo.IsEnabled != true)
                {
                    modInfo.IsEnabled = true;
                    any = true;
                }
            }
            return any
                ? UIUtils.CascadableResult.Continue
                : UIUtils.CascadableResult.BackSilent
                ;
        }

        public static UIUtils.CascadableResult EnableMods(IEnumerable<ModRecord> ModsToEnable)
            => EnableMods(ModsToEnable.Select(r => r.ID))
            ;

        public static UIUtils.CascadableResult DisableMods(IEnumerable<string> ModsToDisable)
        {
            if (ModsToDisable.IsNullOrEmpty())
                return UIUtils.CascadableResult.BackSilent
                    ;

            bool any = false;
            foreach (string modToDisable in ModsToDisable)
            {
                if (ModManager.GetMod(modToDisable) is ModInfo modInfo
                    && modInfo.IsEnabled != false)
                {
                    modInfo.IsEnabled = false;
                    any = true;
                }
            }
            return any
                ? UIUtils.CascadableResult.Continue
                : UIUtils.CascadableResult.BackSilent
                ;
        }

        public static UIUtils.CascadableResult DisableMods(IEnumerable<ModRecord> ModsToDisable)
            => DisableMods(ModsToDisable.Select(r => r.ID))
            ;

        public ModsDifferInfo GetModsDifferInfo()
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(EnabledMods), EnabledMods != null ? EnabledMods.Count : "null"),
                });

            if (EnabledMods.IsNullOrEmpty())
                EnabledMods = new(ID, ModVersion, JSON.ModsEnabled);

            return ModsDifferInfo.Get(EnabledMods, IsDummy);
        }

        public int CompareTo(SaveBonesInfo Other)
        {
            if (Other == null)
                return -1;

            int timeComp = SaveTimeValue.CompareTo(Other.SaveTimeValue);
            if (timeComp != 0)
                return timeComp;

            var noneType = IBonesSource.SourceType.None;
            var sourceType = BonesSources.GetBestSource(BonesID)?.Type ?? noneType;
            var otherSourceType = Other.BonesSources.GetBestSource(BonesID)?.Type ?? noneType;

            if (sourceType == noneType
                || otherSourceType == noneType)
                return (sourceType == noneType).CompareTo(otherSourceType == noneType);

            return sourceType.CompareTo(otherSourceType);
        }

        public void Dispose()
        {
            ID = null;
            Name = null;
            Description = null;
            SaveTime = null;
            Info = null;
            Version = null;
            JSON = null;
            Directory = null;
            Size = null;
            ModsEnabled = null;

            _BonesSources?.Dispose();
            _BonesSources = null;
        }

        public virtual SaveBonesInfo Vomit(
            bool ForceName = false,
            Indent Indent = null,
            [CallerMemberName] string CallingMethod = "",
            params object[] Parameters
            )
            => (SaveBonesInfo)IVomitable.Vomit(
                Object: this,
                ForceName: ForceName,
                Indent: Indent,
                CallingMethod: CallingMethod,
                Parameters: Parameters)
            ;

        public static bool operator >(SaveBonesInfo x, SaveBonesInfo y)
            => DateTime.Compare(
                t1: (x?.SaveTimeValue).GetValueOrDefault(),
                t2: (y?.SaveTimeValue).GetValueOrDefault())
                > 0
            ;
        public static bool operator <=(SaveBonesInfo x, SaveBonesInfo y)
            => !(x > y)
            ;

        public static bool operator <(SaveBonesInfo x, SaveBonesInfo y)
            => DateTime.Compare(
                t1: (x?.SaveTimeValue).GetValueOrDefault(),
                t2: (y?.SaveTimeValue).GetValueOrDefault())
                < 0
            ;

        public static bool operator >=(SaveBonesInfo x, SaveBonesInfo y)
            => !(x < y)
            ;
    }
}
