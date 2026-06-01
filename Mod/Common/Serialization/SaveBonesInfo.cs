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

using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;

using ColorUtility = ConsoleLib.Console.ColorUtility;
using Event = XRL.World.Event;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class SaveBonesInfo : SaveGameInfo, IComparable<SaveBonesInfo>
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

        public class ModsDifferInfo
        {
            public int UnavailableWhereBonesEnabled;
            public int EnabledWhereBonesDisabled;
            public int DisabledWhereBonesEnabled;
            public int DifferLevel
            {
                get
                {
                    if (UnavailableWhereBonesEnabled < 0)
                        return -1;
                    if (DisabledWhereBonesEnabled > 0)
                        return 3;
                    if (DisabledWhereBonesEnabled > 0)
                        return 2;
                    return EnabledWhereBonesDisabled > 0
                        ? 1
                        : 0
                        ;
                }
            }
            public bool IsRed => DifferLevel >= 2;
            public bool IsYellow => DifferLevel == 1;

            public override string ToString()
            {
                if (DifferLevel < 0)
                    return "{{y|mods:}} {{red|error}}";

                if (DifferLevel == 0)
                    return "{{y|mods:}} {{green|\u00fb}}";

                string output = "mods:".Colored("y");

                string extras = null;
                if (EnabledWhereBonesDisabled > 0)
                    extras = $"{extras}{(-EnabledWhereBonesDisabled).Signed()}".Colored("yellow");

                if (DisabledWhereBonesEnabled > 0)
                {
                    if (!extras.IsNullOrEmpty())
                        extras += "|".Colored("y");
                    extras = $"{extras}{DisabledWhereBonesEnabled.Signed()}".Colored("red");
                }

                if (UnavailableWhereBonesEnabled > 0)
                {
                    if (!extras.IsNullOrEmpty())
                        extras += "|".Colored("y");
                    extras = $"{extras}X{UnavailableWhereBonesEnabled}".Colored("red");
                }
                return $"{output} {extras}";
            }
        }

        public static string Sav => $"{BonesManager.BonesFileName}.sav";
        public static string SavGz => $"{Sav}.gz";
        public static string SavGzBak => $"{SavGz}.bak";
        public static string Json => SaveBonesJSON.FileName;

        public static SaveBonesInfoComparer SaveBonesInfoComparerDescending = new SaveBonesInfoComparer(Ascending: true);

        public static int BaseBonesWeight = 50;

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

        public static IRenderable YesAvailableModsIcon = new Renderable(UnavailableModsIcon).setDetailColor('y');

        public bool IsYou
        {
            get
            {
                if (GetBonesJSON() is not SaveBonesJSON bonesJSON)
                    return true;

                if (bonesJSON.IsYou is bool isYou)
                    return isYou;

                if (!OsseousAshID.IsEmptyOrDefault())
                    return OsseousAshID == OsseousAsh.Config.ID;

                return true;
            }
        }

        public Guid OsseousAshID
            => GetBonesJSON()?.OsseousAshID
            ?? Guid.Empty;

        public string OsseousAshHandle => GetBonesJSON()?.OsseousAshHandle;

        public string JSONFilePath;
        public DateTime SaveTimeValue;

        public string ModVersion;

        public string DeathReason;

        public string GenotypeName;
        public string SubtypeName;

        public BonesSpec BonesSpec;

        public BonesStats Stats => GetBonesJSON()?.Stats;

        private bool? _IsMad;
        public bool IsMad => _IsMad ??= (GetBonesJSON()?.IsCharIconSwapped() is true)
            || !GameObjectFactory.Factory.HasBlueprint(GetBonesJSON()?.Blueprint ?? "MISSING_BLUEPRINT")
            ;

        private ModsDifferInfo _ModsDiffer;
        public ModsDifferInfo ModsDiffer => _ModsDiffer ??= GetModsDifferInfo();

        public ZoneRequest ZoneRequest => BonesSpec != null
            ? new(BonesSpec.ZoneID)
            : default
            ;

        public bool WasCremated;

        public bool? _IsEligible;
        public bool IsEligible => _IsEligible ??= IsEligibleForCurrentSave(Strict: true);

        public bool? _IsLooselyEligible;
        public bool IsLooselyEligible => _IsLooselyEligible ??= IsEligibleForCurrentSave();

        private BonesRender _Render;
        public BonesRender Render => _Render ??= new(GetBonesJSON());
        public BonesRender FlippedRender => new(Render, HFlip: !Render.GetHFlip());

        public string FullBonesPathSav => GetFullBonesPathSav(FileLocationData);
        public string FullBonesPathSavGz => GetFullBonesPathSavGz(FileLocationData);
        public string FullBonesPathBak => GetFullBonesPathBak(FileLocationData);

        public string DisplayDirectory => FileLocationData.SanitiseForDisplay();
        public string BonesBakDisplay => FileLocationData.SanitiseForDisplay(SavGzBak);

        public bool IsDummy;

        public FileLocationData FileLocationData
        {
            get
            {
                if (FileLocationDataSet.IsNullOrEmpty()
                    && GetBonesJSON() is SaveBonesJSON bonesJSON
                    && !Directory.IsNullOrEmpty())
                {
                    using var assumedLocationData = FileLocationData.NewAssumed(Directory);
                    FileLocationData.LocationType type = assumedLocationData.Type;
                    if (bonesJSON.FileLocationType != FileLocationData.LocationType.None)
                        type = bonesJSON.FileLocationType;

                    FileLocationDataSet.Add(new FileLocationData(type, assumedLocationData.Path, assumedLocationData.Host));
                    // Utils.Log($"New {nameof(_FileLocationData)}: {_FileLocationData?.SanitiseForDisplay() ?? "NO_DATA"}");
                }
                return FileLocationDataSet.OrderBy(data => data.Type).FirstOrDefault(delegate (FileLocationData locationData)
                {
                    if (locationData.Type.IsFile())
                    {
                        if (locationData.FileExists(SavGz))
                            return true;

                        if (locationData.FileExists(Sav))
                            return true;

                        return false;
                    }
                    return true;
                });
            }
            set
            {
                if (value == null)
                    FileLocationDataSet?.Clear();
                else
                if (!FileLocationDataSet.Any(fld => fld.SameAs(value)))
                    FileLocationDataSet.Add(value);
            }
        }

        private HashSet<FileLocationData> _FileLocationDataSet;
        public HashSet<FileLocationData> FileLocationDataSet => _FileLocationDataSet ??= new();

        public OsseousAsh.Host Host => FileLocationDataSet.FirstOrDefault(ld => ld.Type.IsOnline())?.Host;

        public bool IsOnline
            => FileLocationData != null
            && FileLocationData.Type.IsOnline()
            && Host != null
            ;

        public bool IsCrematable
            => !IsDummy
            && FileLocationData != null
            && FileLocationData.Type.IsFile()
            && FileLocationData.Exists()
            ;

        protected bool? _IsBlocked;
        public bool IsBlocked => _IsBlocked ??= OsseousAsh.IsBonesBlocked(ID);

        public bool IsGenerated => GetBonesJSON()?.GameMode == BonesModeModule.BONES_MODE;

        public SaveBonesInfo()
            : base()
        { }

        public static SaveBonesInfo DummyBonesInfo(
            BonesManagement.VisibilityModes VisibilityMode,
            FileLocationData LocationData
            )
            => SaveBonesJSON.InfoFromJson(
                SaveBonesJSON: SaveBonesJSON.DummyBonesJSON(VisibilityMode),
                FileLocationData: LocationData,
                SaveSize: 0L,
                IsDummy: true)
            ;

        public string GetName()
            => $"{(IsMad ? "Mad " : null)}{Name}"
                .StartReplace()
                .ToString()
            ;

        public XRL.Version GetModVersion()
            => new(ModVersion)
            ;

        public string GetBonesMenuDataRowString(int N)
            => N switch
            {
                0 => $"{GetName()}::{Description}".Colored("W"),
                1 => ColorUtility.CapitalizeExceptFormatting(Info),
                2 => $"{DeathReason} on {GetSaveTimeString()}",
                3 => $"{Size} {"{" + ID + "} "}".Colored("K"),
                _ => throw new ArgumentOutOfRangeException(nameof(N), "Must be between 0 and 3 inclusive."),
            }
            ;

        private static async Task SafeWriteSaveBonesJSONAsync(FileLocationData LocationData, SaveBonesJSON BonesJSON, bool RequireExisting = true)
        {
            if (LocationData is null)
            {
                Utils.Warn($"Attempted to safe write SaveBonesJSON to null {nameof(FileLocationData)}");
                return;
            }
            if (LocationData.Type >= FileLocationData.LocationType.Mod)
            {
                Utils.Warn($"Attempted to safe write SaveBonesJSON to \"{LocationData.Type}\" file location type");
                return;
            }

            if (!RequireExisting
                || await LocationData.FileExistsAsync(Json))
            {
                // Utils.Log($"{nameof(SafeWriteSaveBonesJSONAsync)} {LocationData?.SanitiseForDisplay(Json)}");
                BonesJSON.HotSwapCharIcon(EitherSideOf: () =>
                    LocationData.WriteAsync(Json, BonesJSON, Formatting.Indented, Ensure: true).Wait()
                    );
            }
        }

        private async Task SafeWriteSaveBonesJSONAsync(bool RequireExisting = true)
            => await SafeWriteSaveBonesJSONAsync(FileLocationData, GetBonesJSON(), RequireExisting)
            ;

        private void IncrementStat(Func<bool> IncrementStatFunc, string StatName = null)
        {
            // Utils.Log($"{nameof(IncrementStat)} {StatName ?? "NO_STAT_NAME"} via {nameof(FileLocationData)}: {FileLocationData?.SanitiseForDisplay(Json) ?? "NO_DATA"}");
            if (GetBonesJSON() is not SaveBonesJSON bonesJSON)
            {
                Utils.Warn($"Attempted to {nameof(IncrementStat)} {StatName ?? "NO_STAT_NAME"} for null {nameof(SaveBonesJSON)}.");
                return;
            }
            if (!IncrementStatFunc.Invoke())
            {
                Utils.Log($"Failed to {nameof(IncrementStat)} {StatName ?? "NO_STAT_NAME"}, {nameof(IncrementStatFunc)} returned {false}");
                return;
            }
            foreach (var fileLocationData in FileLocationDataSet)
            {
                fileLocationData.PerformBasedOnTypeAsync(
                    Value: bonesJSON,
                    OnlineCallback: v => fileLocationData.Host.PutBonesStats(v.ID, bonesJSON),
                    ModCallback: v => Task.Run(() => Utils.Warn($"Currently unable to increment Mod-loaded bones files.")),
                    FileCallback: v => SafeWriteSaveBonesJSONAsync(),
                    DefaultCallback: v => Task.Run(() => Utils.Warn($"Attempted to increment stat for unknown or missing File Location Type: {(FileLocationData?.Type)?.ToString() ?? "NO_DATA"}."))
                    ).Wait();
            }
        }

        public void IncrementEncountered()
            => IncrementStat(
                IncrementStatFunc: () => Stats?.IncrementEncountered(OsseousAsh.Config.ID) is true,
                StatName: Utils.CallChain(nameof(SaveBonesInfo), nameof(Stats), nameof(Stats.Encountered)))
                ;

        public void IncrementDefeated()
            => IncrementStat(
                IncrementStatFunc: () => Stats?.IncrementDefeated(OsseousAsh.Config.ID) is true,
                StatName: Utils.CallChain(nameof(SaveBonesInfo), nameof(Stats), nameof(Stats.Defeated)))
                ;

        public void IncrementReclaimed()
            => IncrementStat(
                IncrementStatFunc: () => Stats?.IncrementReclaimed(OsseousAsh.Config.ID) is true,
                StatName: Utils.CallChain(nameof(SaveBonesInfo), nameof(Stats), nameof(Stats.Reclaimed)))
                ;

        public void IncrementBroken()
            => IncrementStat(
                IncrementStatFunc: () => Stats?.IncrementBroken(OsseousAsh.Config.ID) is true,
                StatName: Utils.CallChain(nameof(SaveBonesInfo), nameof(Stats), nameof(Stats.Broken)))
                ;

        public static void RepairBonesSpec(SaveBonesInfo BonesInfo, BonesSpec BonesSpec)
        {
            if (BonesInfo?.GetBonesJSON() is not SaveBonesJSON bonesJSON)
            {
                Utils.Warn($"Attempted to repair {nameof(SaveBonesJSON)}.{nameof(SaveBonesJSON.BonesSpec)} for null {nameof(SaveBonesJSON)}.");
                return;
            }
            if (BonesInfo?.FileLocationData is not FileLocationData locationData)
            {
                Utils.Warn($"Attempted to repair {nameof(SaveBonesJSON)}.{nameof(SaveBonesJSON.BonesSpec)} for {nameof(SaveBonesInfo)} with no {nameof(FileLocationData)}.");
                return;
            }

            bonesJSON.BonesSpec = BonesSpec;
            locationData.PerformBasedOnTypeAsync(
                Value: bonesJSON,
                OnlineCallback: v => locationData.Host.PutBonesStats(v.ID, bonesJSON),
                ModCallback: v => Task.Run(() => Utils.Warn($"Currently unable to repair Mod-loaded BonesSpecs.")),
                FileCallback: v => SafeWriteSaveBonesJSONAsync(locationData, bonesJSON, RequireExisting: true),
                DefaultCallback: v => Task.Run(() => Utils.Warn($"Attempted to repair BonesSpec for unknown or missing File Location Type: {(locationData?.Type)?.ToString() ?? "NO_DATA"}."))
                ).Wait();
        }

        public static async Task<SaveBonesInfo> GetPhysicalSaveBonesInfoAsync(FileLocationData FileLocationData)
        {
            if (FileLocationData == null)
                return null;

            if (!FileLocationData.Type.IsTwixtInclusive(FileLocationData.LocationType.Local, FileLocationData.LocationType.Synced))
            {
                Utils.Warn($"Attempted to get \"on disk\" {nameof(SaveBonesInfo)} from {nameof(FileLocationData)} " +
                    $"of inappropriate type: {FileLocationData.Type}({(int)FileLocationData.Type})");
                return null;
            }
            try
            {
                if (Path.GetFileNameWithoutExtension(FileLocationData).EqualsNoCase("mods")
                    || Path.GetFileNameWithoutExtension(FileLocationData).EqualsNoCase("textures"))
                    return null;

                if (FileLocationData.FileExists(Json))
                    return await SaveBonesJSON.ReadSaveBonesJson(FileLocationData);

                if (!Platform.IO.Directory.EnumerateFiles(FileLocationData).Any(f => !f.EndsWith(".json")))
                {
                    try
                    {
                        Platform.IO.Directory.Delete(FileLocationData);
                    }
                    catch (Exception x)
                    {
                        Utils.Warn(x);
                    }
                }
                else
                    Utils.Warn($"Weird bones directory with no .json file present: {FileLocationData.SanitiseForDisplay()}");
            }
            catch (ThreadInterruptedException x)
            {
                throw x;
            }
            catch (Exception x)
            {
                Utils.Warn(x);
            }
            return null;
        }

        public SaveBonesJSON GetBonesJSON()
            => json as SaveBonesJSON
            ;


        public static string GetFullBonesPathSav(FileLocationData FileLocationData)
            => FileLocationData.WithFileName(Sav)
            ;

        public static string GetFullBonesPathSavGz(FileLocationData FileLocationData)
            => FileLocationData.WithFileName(SavGz)
            ;

        public static string GetFullBonesPathBak(FileLocationData FileLocationData)
            => FileLocationData.WithFileName(SavGzBak)
            ;

        public async Task<string> GetBonesFilePathAsync()
        {
            if (!IsOnline)
            {
                string bonesPath = null;
                int attempts = 0;
                int setCount = FileLocationDataSet.Count;
                while (FileLocationData != null
                    && attempts++ < setCount
                    && FileLocationData.Type.IsFile())
                {
                    var fileLocationData = FileLocationData;
                    bonesPath = FullBonesPathSavGz;
                    if (!await File.ExistsAsync(bonesPath))
                    {
                        bonesPath = FullBonesPathSav;
                        if (!await File.ExistsAsync(bonesPath))
                        {
                            Utils.Error($"No saved bones exist. ({DisplayDirectory})");

                            try
                            {
                                if (!FileLocationData.TryDeleteDirectory(delegate (FileLocationData fld)
                                {
                                    FileLocationDataSet.Remove(fld);
                                    BonesManager.ClearHasSaveBones();
                                }))
                                {
                                    Utils.Warn($"Failed to delete empty bones directory. ({DisplayDirectory})");
                                }
                            }
                            catch (Exception x)
                            {
                                Utils.Error($"Deleting empty bones directory failed ({DisplayDirectory})", x);
                            }
                            finally
                            {
                                FileLocationDataSet.Remove(fileLocationData);
                                BonesManager.ClearHasSaveBones();
                            }
                        }
                    }
                }
                return bonesPath;
            }
            return null;
        }

        public string GetBonesFilePath()
            => GetBonesFilePathAsync().WaitResult()
            ;

        public async Task<byte[]> GetSavGzBytes()
            => await Host.GetBonesSavGz(ID)
            ;

        public async Task<System.IO.Stream> GetSavGzStreamAsync()
        {
            if ((await GetBonesFilePathAsync()) is string bonesPath)
                return File.OpenRead(bonesPath);

            byte[] cloudBytes = await GetSavGzBytes();

            return !cloudBytes.IsNullOrEmpty()
                ? new System.IO.MemoryStream(cloudBytes)
                : null
                ;
        }

        public System.IO.Stream GetSavGzStream()
            => GetSavGzStreamAsync().WaitResult()
            ;

        public IEnumerable<string> GetDebugLines()
        {
            yield return $"{nameof(ID)}: {ID}";
            yield return $"{nameof(Name)}: {Name}";

            yield return $"{nameof(WasCremated)}: {WasCremated}";

            yield return $"{nameof(IsMad)}: {IsMad}";

            yield return $"{nameof(IsEligible)}: {IsEligible}";
            yield return $"{nameof(IsLooselyEligible)}: {IsLooselyEligible}";

            var bonesJSON = GetBonesJSON();

            yield return $"{nameof(bonesJSON.DeathReason)}: {bonesJSON.DeathReason}";
            yield return $"{nameof(bonesJSON.Location)}: {bonesJSON.Location}";

            yield return $"{nameof(bonesJSON.GameVersion)}: {bonesJSON?.GameVersion ?? "missing"}";
            yield return $"{nameof(bonesJSON.SaveVersion)}: {(bonesJSON?.SaveVersion)?.ToString() ?? "missing"}";
            yield return $"{nameof(ModVersion)}: {ModVersion ?? "missing"}";

        }

        public int? GetBonesWeight()
        {
            if (ModsEnabled.IsNullOrEmpty())
                return null;

            int saveWeight = BaseBonesWeight;

            saveWeight += ModsDiffer.EnabledWhereBonesDisabled * -1;
            saveWeight += ModsDiffer.DisabledWhereBonesEnabled * -2;

            foreach (var runningMod in BonesManager.RunningMods)
                if (ModsEnabled.Contains(runningMod))
                    saveWeight += 1;

            saveWeight += ModsDiffer.UnavailableWhereBonesEnabled * -4;

            /*if (Stats.Encountered is BonesStatSet encountered)
            {
                int timesEncountered = encountered.GetStatValue(OsseousAsh.Config.ID);
                // do something on the basis of times encountered maybe?
            }*/

            if (GetModVersion() is XRL.Version saveVersion
                && Utils.ModVersion is XRL.Version modVersion)
            {
                if (modVersion < saveVersion)
                    saveWeight = 0;

                if (modVersion.Build != saveVersion.Build)
                    saveWeight = 0;

                int versionDiff = Math.Max(0, modVersion.Revision - saveVersion.Revision);

                int diffReduction = 100;
                for (int i = 0; i < versionDiff; i++)
                {
                    if (diffReduction <= 0
                        || saveWeight <= 0)
                        break;

                    saveWeight -= diffReduction;

                    diffReduction *= 2;
                }
            }

            return Math.Max(1, saveWeight);
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
            => GetSaveTime(GetBonesJSON())
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
            if (OsseousAshID == OsseousAsh.Config?.ID)
                sB.Append("are yours");
            else
                sB.Append("come from ").Append(OsseousAshHandle);
            sB.Append(".");

            sB.AppendLine()
                .AppendLine().Append("They ");
            switch (FileLocationData?.Type)
            {
                case FileLocationData.LocationType.Synced:
                case FileLocationData.LocationType.Local:
                    sB.Append("were saved to ");
                    break;
                case FileLocationData.LocationType.Mod:
                    sB.Append("were compiled in ");
                    break;
                case FileLocationData.LocationType.Online:
                    sB.Append("were uploaded to ");
                    break;
                case FileLocationData.LocationType.None:
                default:
                    sB.Append("materialized mytseriously in ");
                    break;
            }

            sB.Append(FileLocationData?.ShortDisplayName() ?? FileLocationData.MissingLocaitonShortDisplayName)
                .Append(" on ").Append(GetSaveTimeString()).Append(", ")
                .AppendRule(SaveTimeValue.TimeAgo("ago")).Append(".")
                .AppendLine();

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
                        .AppendRule((GetBonesWeight() ?? 0).ToString()).Append(", compared to the default of ").Append(BaseBonesWeight).Append(".")
                        .AppendLine();
                }
            }
            if (showModConfigLine)
            {
                sB.AppendLine().Append("Based on your current mod configuration, these bones are weighted ")
                    .AppendRule((GetBonesWeight() ?? 0).ToString()).Append(", compared to the default of ").Append(BaseBonesWeight).Append(".")
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
            if (await OsseousAsh.TryReportBonesAsync(SaveBonesInfo.ID, null))
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
                        if (await OsseousAsh.TryReportBonesAsync(
                            Report: new OsseousAsh.Report
                            {
                                OsseousAshID = OsseousAsh.Config.ID,
                                BonesID = SaveBonesInfo.ID,
                                Blocked = true,
                                Type = OsseousAsh.Report.ReportTypes.Other,
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

        public void Cremate()
        {
            if (IsCrematable)
            {
                using var fileLocationDataList = ScopeDisposedList<FileLocationData>.GetFromPoolFilledWith(FileLocationDataSet);
                foreach (var fileLocationData in fileLocationDataList)
                {
                    bool didCremate = fileLocationData.TryDeleteDirectory(delegate (FileLocationData fld)
                    {
                        FileLocationDataSet.Remove(fld);
                        BonesManager.ClearHasSaveBones();
                    });
                    if (didCremate)
                    {
                        WasCremated = true;
                    }
                }
            }
        }

        public bool IsEligibleForCurrentSave(bool Strict = false)
        {
            if (GetBonesJSON() is not SaveBonesJSON bonesJSON)
                return false;

            if (ID == The.Game.GameID)
                return false;

            if (IsBlocked)
                return false;

            if (bonesJSON.SaveVersion < Const.MIN_SAVE_VERSION)
                return false;

            if (bonesJSON.SaveVersion < XRLGame.MaxSaveVersion)
                return false;

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
            => $"{nameof(SaveBonesJSON.Level)}: {GetBonesJSON().Level}"
            ;

        private string GetZoneTierPair()
            => $"{nameof(BonesSpec.ZoneTier)}: {BonesSpec.ZoneTier}"
            ;

        private string GetZoneTerrainTypePair()
            => $"{nameof(BonesSpec.ZoneTerrainType)}: {(BonesSpec.ZoneTerrainType.IsNullOrEmpty() ? "none" : BonesSpec.ZoneTerrainType)}"
            ;

        private string GetZoneIDPair()
            => $"{nameof(SaveBonesJSON.ZoneID)}: {GetBonesJSON().ZoneID}"
            ;

        public string GetBonesPickerLine()
            => GetBonesJSON() != null
            ? $"{GetName()}, {GetLevelPair()}, {GetZoneTierPair()},\n{GetZoneTerrainTypePair()}, {GetZoneIDPair()}"
            : GetName()
            ;

        public bool AttemptLoad(Zone Z, ZoneBonesAllocation.AllocationTypes Type, out bool Blocked)
            => BonesManager.System.AttemptLoadBones(Z, this, Type, out Blocked)
            ;

        public async Task<bool> TryRestoreModsAsync()
        {
            await The.UiContext;
            if (await RestoreModsLoadedAsync(ModsEnabled))
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

        public async Task<bool> RestoreModsLoadedAsync(List<string> Enabled)
        {
            using var loadedMods = ScopeDisposedList<string>.GetFromPool();
            if (ModManager.GetRunningMods() is IEnumerable<string> runningMods)
                loadedMods.AddRange(runningMods);
            else
            {
                Utils.Error("Failed to get running mods", new InvalidOperationException("Impossibly empty running mods list"));
                return false;
            }

            using var bonesHasButNotAvailable = ScopeDisposedList<string>.GetFromPoolFilledWith(Enabled.Except(ModManager.GetAvailableMods()));
            using var loadedButBonesMissing = ScopeDisposedList<string>.GetFromPoolFilledWith(loadedMods.Except(Enabled));

            using var bonesHasButNotLoaded = ScopeDisposedList<string>.GetFromPoolFilledWith(
                items: Enabled
                    .Except(loadedMods)
                    .Except(bonesHasButNotAvailable));

            using var bothHaveLoaded = ScopeDisposedList<string>.GetFromPoolFilledWith(
                items: Enabled
                    .Except(bonesHasButNotLoaded)
                    .Except(bonesHasButNotAvailable)
                    .Except(loadedButBonesMissing));

            UIUtils.CascadableResult result = UIUtils.CascadableResult.Continue;
            var sB = Event.NewStringBuilder();
            string title = "Mod Configuration Comparison".Colored("yellow");
            if (!bonesHasButNotAvailable.IsNullOrEmpty()
                || !bothHaveLoaded.IsNullOrEmpty()
                || !loadedButBonesMissing.IsNullOrEmpty()
                || !bonesHasButNotLoaded.IsNullOrEmpty())
            {
                var options = new PickOptionDataSetAsync<IEnumerable<string>, UIUtils.CascadableResult>();
                do
                {
                    if (!bonesHasButNotAvailable.IsNullOrEmpty())
                    {
                        sB.AppendColored("red", "Attention").Append(": one or more mods enabled in this bones file are ")
                            .AppendColored("red", "not available");

                        if (bonesHasButNotAvailable.Count > 9)
                            sB.Append(".").AppendLine().Append("Use the menu option below to view the entire list.");
                        else
                            sB.Append("; they'll be listed at the bottom.");
                        
                        sB.AppendLineEnd();
                    }
                    if (!loadedButBonesMissing.IsNullOrEmpty())
                    {
                        loadedButBonesMissing
                            .Select(ModManager.GetModTitle)
                            .Aggregate(
                                seed: sB.AppendLine().Append("These ").AppendColored("green", "enabled").Append(" mods are ")
                                    .AppendColored("yellow", "disabled").Append(" in this bones file:"),
                                func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("yellow", n))
                            .AppendLineEnd();
                    }
                    if (!bonesHasButNotLoaded.IsNullOrEmpty())
                    {
                        bonesHasButNotLoaded
                            .Select(ModManager.GetModTitle)
                            .Aggregate(
                                seed: sB.AppendLine().Append("These ").AppendColored("black", "disabled").Append(" mods are ").AppendColored("red", "enabled").Append(" in this bones file:"),
                                func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("red", n))
                            .AppendLineEnd();
                    }
                    if (!bothHaveLoaded.IsNullOrEmpty())
                    {
                        bothHaveLoaded
                            .Select(ModManager.GetModTitle)
                            .Aggregate(
                                seed: sB.AppendLine().Append("These ").AppendColored("green", "enabled").Append(" mods are ")
                                    .AppendColored("green", "enabled").Append(" in this bones file:"),
                                func: (a, n) => a.AppendLine().AppendColored("y", ":").Append(" ").AppendColored("green", n))
                            .AppendLineEnd();
                    }
                    if (!bonesHasButNotAvailable.IsNullOrEmpty()
                        && bonesHasButNotAvailable.Count < 10)
                    {
                        sB.AppendUnavailableMods(bonesHasButNotAvailable)
                            .AppendLineEnd();
                    }

                    options.Clear();
                    if (!bonesHasButNotLoaded.IsNullOrEmpty())
                    {
                        options.Add(new PickOptionDataAsync<IEnumerable<string>, UIUtils.CascadableResult>
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
                        options.Add(new PickOptionDataAsync<IEnumerable<string>, UIUtils.CascadableResult>
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
                        options.Add(new PickOptionDataAsync<IEnumerable<string>, UIUtils.CascadableResult>
                        {
                            Element = bonesHasButNotAvailable,
                            Text = Event.FinalizeString(sB2),
                            Hotkey = 'x',
                            Icon = UnavailableModsIcon,
                            Callback = async delegate (IEnumerable<string> element)
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
                            Intro: sB.ToString(),
                            IntroIcon: ModConfigIcon,
                            DefaultSelected: !options.IsNullOrEmpty() ? 0 : -1,
                            OnBackCallback: () => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                            OnEscapeCallback: () => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                            FinalSelectedCallback: async delegate (PickOptionData<IEnumerable<string>, Task<UIUtils.CascadableResult>> o, Task<UIUtils.CascadableResult> r)
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
                            message: sB.ToString(),
                            title: title,
                            afterRender: ModConfigIcon);
                    }
                }
                while (!result.IsCancel());
            }
            Event.ResetTo(sB);
            return false;
        }

        public static async Task<UIUtils.CascadableResult> AskRestoreModsAsync(SaveBonesInfo SaveBonesInfo)
            => (await SaveBonesInfo.RestoreModsLoadedAsync(SaveBonesInfo.ModsEnabled))
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

        public ModsDifferInfo GetModsDifferInfo()
        {
            using var loadedMods = ScopeDisposedList<string>.GetFromPool();

            if (ModManager.GetRunningMods() is IEnumerable<string> runningMods)
                loadedMods.AddRange(runningMods);
            else
            {
                Utils.Error("Failed to get running mods", new InvalidOperationException("Impossibly empty running mods list"));
                return new ModsDifferInfo
                {
                    UnavailableWhereBonesEnabled = -1,
                };
            }

            using var bonesHasButNotAvailable = ScopeDisposedList<string>.GetFromPoolFilledWith(ModsEnabled.Except(ModManager.GetAvailableMods()));
            using var loadedButBonesMissing = ScopeDisposedList<string>.GetFromPoolFilledWith(loadedMods.Except(ModsEnabled));

            using var bonesHasButNotLoaded = ScopeDisposedList<string>.GetFromPoolFilledWith(
                items: ModsEnabled
                    .Except(loadedMods)
                    .Except(bonesHasButNotAvailable));

            return new ModsDifferInfo
            {
                UnavailableWhereBonesEnabled = bonesHasButNotAvailable.Count,
                EnabledWhereBonesDisabled = loadedButBonesMissing.Count,
                DisabledWhereBonesEnabled = bonesHasButNotLoaded.Count,
            };
        }

        public int CompareTo(SaveBonesInfo Other)
        {
            if (Other == null)
                return -1;

            int timeComp = SaveTimeValue.CompareTo(Other.SaveTimeValue);
            if (timeComp != 0)
                return timeComp;

            return FileLocationData.Type.CompareTo(Other.FileLocationData.Type);
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
