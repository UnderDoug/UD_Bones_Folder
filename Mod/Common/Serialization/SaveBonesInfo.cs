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

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class SaveBonesInfo
        : SaveGameInfo
        , IBonesFileElement
        , IComparable<SaveBonesInfo>
        , IDisposable
    {
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

        public SaveBonesJSON JSON => json as SaveBonesJSON;

        public string UserHandle => JSON?.Handle;

        public DateTime SaveTimeValue;

        public string ModVersion => JSON?.ModVersion;

        public string DeathReason => JSON?.DeathReason ?? "They died mysteriously...";

        public string GenotypeName => JSON?.GenotypeName ?? "Human";
        public string SubtypeName => JSON?.SubtypeName ?? "Adventurer";

        public BonesSpec BonesSpec => JSON?.BonesSpec;

        public BonesStats Stats => JSON?.Stats;

        private bool? _IsMad;
        public bool IsMad => _IsMad ??= (JSON?.IsCharIconSwapped() is true)
            || !GameObjectFactory.Factory.HasBlueprint(JSON?.Blueprint ?? "MISSING_BLUEPRINT")
            ;

        public ModRecordCollection EnabledMods
        {
            get => JSON?.EnabledMods;
            set => JSON.EnabledMods = value;
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
            && (IsYou
                || BonesSources.Any(bs => bs.IsOnDisk()))
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
            using var bonesSources = BonesManager.GetBonesSourcesFromPool().Intersect(BonesSources);
            return await bonesSources.GetBonesSavGzAsync(ID);
        }

        public async Task<BonesSavGz> GetBonesSavGzStreamAsync()
        {
            if (await LoadBonesSavGzAsync() is not BonesSavGz bonesSavGz)
                return null;

            if (!bonesSavGz.TryEnsureLoadable())
            {
                bonesSavGz.Dispose();
                return null;
            }

            return bonesSavGz;
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
            var sB = Event.NewStringBuilder();

            sB.Append("These bones ");
            if (IsYou)
                sB.Append("are yours");
            else
                sB.Append("come from ").Append(UserHandle);
            sB.Append(".");

            var preferredBonesSource = BonesSources.FirstOrDefault() ?? BonesPile.DummyFolder;

            sB.AppendLine()
                .AppendLine().Append("They ");

            if (preferredBonesSource.Type != IBonesSource.SourceType.None)
                sB.Append("were ");

            sB.Append(preferredBonesSource.BlurbSavedVerb).Append(" ");

            sB.Append(preferredBonesSource.BlurbDisplayName)
                .Append(" on ").Append(GetSaveTimeString()).Append(", ")
                .AppendRule(SaveTimeValue.TimeAgo("ago")).Append(".")
                .AppendLine();

            if ((BonesSources?.Count ?? 0) > 1)
            {
                sB.AppendLine().Append("There is also a copy ");

                if (BonesSources.Count == 2)
                {
                    var otherLocation = BonesSources[1];
                    sB.Append(otherLocation.BlurbSavedVerb).Append(" ").Append(otherLocation.BlurbDisplayName);
                }
                else
                {
                    sB.Append("in the following additional locations:");
                    for (int i = 1; i < BonesSources.Count; i++)
                        sB.AppendLine()
                            .Append(BonesSources[i].BlurbSavedVerb);
                }
                sB.AppendLine();
            }

            sB.AppendLine()
                .Append("They are from version ").AppendRule(GetModVersion()).Append(" of the ").AppendColored("Y", Utils.ModTitle).Append(" mod");
            if (GetModVersion() == Utils.ModVersion)
            {
                sB.Append(", which is the current one.");
            }
            else
                sB.Append(".");
            sB.AppendLine();

            sB.AppendLine().AppendBonesStatsBlurb(Stats, Name)
                .AppendLine();

            bool showModConfigLine = true;
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
                    sB.AppendLine().Append("Based on the ").AppendRule("version difference").Append(" between the ").AppendColored("Y", Utils.ModTitle)
                        .Append(" mod (").AppendRule(modVersion).Append("), ")
                        .Append("and these bones (").AppendRule(infoVersion).Append("), they are ineligible to be loaded.")
                        .AppendLine();
                }
                else
                if (versionDiff > 0)
                {
                    showModConfigLine = false;
                    sB.AppendLine().Append("Based on your current mod configuration, and the version disparty, these bones are weighted ")
                        .AppendRule((EnabledMods.GetBonesWeight() ?? 0).ToString()).Append(", compared to the default of ").Append(ModRecordCollection.BaseBonesWeight).Append(".")
                        .AppendLine();
                }
            }
            if (showModConfigLine)
            {
                sB.AppendLine().Append("Based on your current mod configuration, these bones are weighted ")
                    .AppendRule((EnabledMods.GetBonesWeight() ?? 0).ToString()).Append(", compared to the default of ").Append(ModRecordCollection.BaseBonesWeight).Append(".")
                    .AppendLine();
            }

            return Event.FinalizeString(sB);
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

        public bool Cremate()
        {
            if (!IsCrematable)
                return false;

            using var bonesSources = BonesSources.CloneFromPool();
            var results = ScopeDisposedList<ActionResult>.GetFromPool();
            try
            {
                foreach (var bonesSource in bonesSources)
                {
                    var cremated = bonesSource.DeleteBones(BonesID);
                    results.Add(cremated);
                    if (cremated)
                        BonesSources.Remove(bonesSource);
                }
                return results.Any(r => r);
            }
            finally
            {
                if (results.Any(r => r))
                {
                    BonesManager.ClearHasSaveBones();
                    BonesManager.System?.ClearEligibleSaveBonesInfoCache();
                }
            }
        }

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
            => $"{nameof(BonesSpec.ZoneTerrainType)}: {(BonesSpec.ZoneTerrainType.IsNullOrEmpty() ? "none" : BonesSpec.ZoneTerrainType)}"
            ;

        private string GetZoneIDPair()
            => $"{nameof(SaveBonesJSON.ZoneID)}: {JSON.ZoneID}"
            ;

        public string GetBonesPickerLine()
            => JSON != null
            ? $"{GetName()}, {GetLevelPair()}, {GetZoneTierPair()},\n{GetZoneTerrainTypePair()}, {GetZoneIDPair()}"
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
            var enabledMods = EnabledMods;

            if (enabledMods.IsNullOrEmpty())
                return false;

            var loadedMods = ModRecord.GetRunningMods();

            using var bonesHasButNotAvailable = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(enabledMods);
            var availableMods = ModRecord.GetAvailableMods();
            bonesHasButNotAvailable.RemoveAll(r => availableMods.Any(ar => ModRecord.EQComp.Equals(r, ar)));

            using var loadedButBonesMissing = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(loadedMods);
            loadedButBonesMissing.RemoveAll(r => enabledMods.Any(ar => ModRecord.EQComp.Equals(r, ar)));

            using var bonesHasButNotLoaded = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(enabledMods);
            bonesHasButNotLoaded.RemoveAll(delegate (ModRecord r)
            {
                return loadedMods.Any(ar => ModRecord.EQComp.Equals(r, ar))
                    || bonesHasButNotAvailable.Any(ar => ModRecord.EQComp.Equals(r, ar));
            });

            using var bothHaveLoaded = ScopeDisposedList<ModRecord>.GetFromPoolFilledWith(enabledMods);
            bothHaveLoaded.RemoveAll(delegate (ModRecord r)
            {
                return bonesHasButNotLoaded.Any(ar => ModRecord.EQComp.Equals(r, ar))
                    || bonesHasButNotAvailable.Any(ar => ModRecord.EQComp.Equals(r, ar))
                    || loadedButBonesMissing.Any(ar => ModRecord.EQComp.Equals(r, ar));
            });

            UIUtils.CascadableResult result = UIUtils.CascadableResult.Continue;
            using var tB = TextBuilder.Get();
            string title = "Mod Configuration Comparison".Colored("yellow");
            if (!bonesHasButNotAvailable.IsNullOrEmpty()
                || !bothHaveLoaded.IsNullOrEmpty()
                || !loadedButBonesMissing.IsNullOrEmpty()
                || !bonesHasButNotLoaded.IsNullOrEmpty())
            {
                using var options = new CascadablePickOptionDataSetAsync<IEnumerable<ModRecord>>(UIUtils.CascadableResult.CancelSilent, UIUtils.CascadableResult.CancelSilent);
                do
                {
                    tB.Clear();
                    options.ClearItems(Dispose: true);

                    if (!bonesHasButNotAvailable.IsNullOrEmpty())
                    {
                        tB.AppendColored("red", "Attention").Append(": one or more mods enabled in this bones file are ")
                            .AppendColored("red", "not available");

                        if (bonesHasButNotAvailable.Count > 9)
                            tB.Append(".").AppendLine().Append("Use the menu option below to view the entire list.");
                        else
                            tB.Append("; they'll be listed at the bottom.");
                        
                        tB.AppendLineEnd();
                    }
                    if (!loadedButBonesMissing.IsNullOrEmpty())
                    {
                        loadedButBonesMissing
                            .Select(r => r.ToString())
                            .Aggregate(
                                seed: tB.AppendLine().Append("These ").AppendColored("green", "enabled").Append(" mods are ")
                                    .AppendColored("yellow", "disabled").Append(" in this bones file:"),
                                func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("yellow", n))
                            .AppendLineEnd();
                    }
                    if (!bonesHasButNotLoaded.IsNullOrEmpty())
                    {
                        bonesHasButNotLoaded
                            .Select(r => r.ToString())
                            .Aggregate(
                                seed: tB.AppendLine().Append("These ").AppendColored("black", "disabled").Append(" mods are ").AppendColored("red", "enabled").Append(" in this bones file:"),
                                func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("red", n))
                            .AppendLineEnd();
                    }
                    if (!bothHaveLoaded.IsNullOrEmpty())
                    {
                        bothHaveLoaded
                            .Select(r => r.ToString())
                            .Aggregate(
                                seed: tB.AppendLine().Append("These ").AppendColored("green", "enabled").Append(" mods are ")
                                    .AppendColored("green", "enabled").Append(" in this bones file:"),
                                func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("green", n))
                            .AppendLineEnd();
                    }
                    if (!bonesHasButNotAvailable.IsNullOrEmpty()
                        && bonesHasButNotAvailable.Count < 10)
                    {
                        tB.AppendUnavailableMods(bonesHasButNotAvailable)
                            .AppendLineEnd();
                    }
                    if (!bonesHasButNotLoaded.IsNullOrEmpty())
                    {
                        options.Add(new CascadablePickOptionDataAsync<IEnumerable<ModRecord>>
                        {
                            Element = bonesHasButNotLoaded,
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
                    if (!loadedButBonesMissing.IsNullOrEmpty())
                    {
                        options.Add(new CascadablePickOptionDataAsync<IEnumerable<ModRecord>>
                        {
                            Element = loadedButBonesMissing,
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
                                    val2: (int)EnableMods(bonesHasButNotLoaded))
                                ),
                        });
                    }
                    if (!bonesHasButNotAvailable.IsNullOrEmpty()
                        && bonesHasButNotAvailable.Count > 9)
                    {
                        int unavailableCount = bonesHasButNotAvailable.Count;
                        var sB2 = Event.NewStringBuilder()
                            .Append("Show ").Append(unavailableCount).Append(" ").AppendColored("red", "unavailable")
                            .AppendThings(unavailableCount, " mod").Append(" ").AppendColored("red", "enabled").Append(" in this bones file");
                        options.Add(new CascadablePickOptionDataAsync<IEnumerable<ModRecord>>
                        {
                            Element = bonesHasButNotAvailable,
                            Text = Event.FinalizeString(sB2),
                            Hotkey = 'x',
                            Icon = UnavailableModsIcon,
                            Callback = async delegate (IEnumerable<ModRecord> element)
                            {
                                string message = Event.FinalizeString(Event.NewStringBuilder().AppendUnavailableMods(element));

                                await Popup.NewPopupMessageAsync(
                                    message: message,
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
                            DefaultSelected: !options.IsNullOrEmpty() ? 0 : -1,
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
            json = null;
            Directory = null;
            Size = null;
            ModsEnabled = null;

            _BonesSources.Dispose();
            _BonesSources = null;
        }

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
