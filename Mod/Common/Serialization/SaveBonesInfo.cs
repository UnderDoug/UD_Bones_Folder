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

using UD_Bones_Folder.Mod.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;

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

        public static string Sav => $"{UD_Bones_BonesSaver.BonesName}.sav";
        public static string SavGz => $"{Sav}.gz";
        public static string SavGzBak => $"{SavGz}.bak";
        public static string Json => SaveBonesJSON.FileName;

        public static SaveBonesInfoComparer SaveBonesInfoComparerDescending = new SaveBonesInfoComparer(Ascending: true);

        public static int BaseBonesWeight = 50;

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

        public bool IsMad => (GetBonesJSON()?.IsCharIconSwapped() is true)
            || (GenotypeName?.IsNullOrEmpty() is not false)
            || (SubtypeName?.IsNullOrEmpty() is not false)
            || !GenotypeFactory.GenotypesByName.ContainsKey(GenotypeName)
            || !SubtypeFactory.SubtypesByName.ContainsKey(SubtypeName)
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

        public string FullBonesPathSav => Path.Combine(Directory, Sav);
        public string FullBonesPathSavGz => Path.Combine(Directory, SavGz);
        public string FullBonesPathBak => Path.Combine(Directory, SavGzBak);

        public string DisplayDirectory => DataManager.SanitizePathForDisplay(Directory);
        public string BonesBakDisplay => DataManager.SanitizePathForDisplay(FullBonesPathBak);

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
                return FileLocationDataSet.OrderBy(data => data.Type).FirstOrDefault();
            }
        }

        private HashSet<FileLocationData> _FileLocationDataSet;
        public HashSet<FileLocationData> FileLocationDataSet => _FileLocationDataSet ??= new();

        public OsseousAsh.Host Host => FileLocationData?.Host;

        public bool IsOnline
            => FileLocationData.Type == FileLocationData.LocationType.Online
            || Host != null
            ;

        public bool IsCrematable
            => FileLocationData.Type < FileLocationData.LocationType.Mod
            && FileLocationData.Exists()
            ;

        public bool IsBlocked => OsseousAsh.IsBonesBlocked(ID); //OsseousAsh.IsBonesReported(ID)?.WaitResult() is true;

        public bool IsGenerated => GetBonesJSON()?.GameMode == BonesModeModule.BONES_MODE;

        public SaveBonesInfo()
            : base()
        { }

        public string GetName()
            => $"{(IsMad ? "Mad " : null)}{Name}"
                .StartReplace()
                .ToString()
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
            FileLocationData.PerformBasedOnTypeAsync(
                Value: bonesJSON,
                OnlineCallback: v => FileLocationData.Host.PutBonesStats(v.ID, bonesJSON),
                ModCallback: v => Task.Run(() => Utils.Warn($"Currently unable to increment Mod-loaded bones files.")),
                FileCallback: v => SafeWriteSaveBonesJSONAsync(),
                DefaultCallback: v => Task.Run(() => Utils.Warn($"Attempted to increment stat for unknown or missing File Location Type: {(FileLocationData?.Type)?.ToString() ?? "NO_DATA"}."))
                ).Wait();
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

        public static async Task<SaveBonesInfo> GetSaveBonesInfo(string Directory)
        {
            try
            {
                if (Path.GetFileNameWithoutExtension(Directory).EqualsNoCase("mods")
                    || Path.GetFileNameWithoutExtension(Directory).EqualsNoCase("textures"))
                    return null;

                if (Path.Combine(Directory, Json) is string path
                    && File.Exists(path))
                    return await SaveBonesJSON.ReadSaveBonesJson(Directory, Json);

                if (!Platform.IO.Directory.EnumerateFiles(Directory).Any(f => !f.EndsWith(".json")))
                {
                    try
                    {
                        Platform.IO.Directory.Delete(Directory);
                    }
                    catch (Exception x)
                    {
                        Utils.Warn(x);
                    }
                }
                else
                    Utils.Warn($"Weird bones directory with no .json file present: {DataManager.SanitizePathForDisplay(Directory)}");

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

        public async Task<string> GetBonesFilePathAsync()
        {
            string bonesPath = FullBonesPathSavGz;

            if (!IsOnline)
            {
                if (!await File.ExistsAsync(bonesPath))
                {
                    bonesPath = FullBonesPathSav;
                    if (!await File.ExistsAsync(bonesPath))
                    {
                        Utils.Error($"No saved bones exist. ({DisplayDirectory})");
                        return null;
                    }
                }
                return bonesPath;
            }
            return null;
        }

        public string GetBonesFilePath()
            => GetBonesFilePathAsync().WaitResult()
            ;

        public byte[] GetSavGzBytes()
            => Host.GetBonesSavGz(ID)
            ;

        public async Task<System.IO.Stream> GetSavGzStreamAsync()
        {
            if ((await GetBonesFilePathAsync()) is string bonesPath)
                return File.OpenRead(bonesPath);

            byte[] cloudBytes = GetSavGzBytes();

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

            return Math.Max(1, saveWeight);
        }

        public string OutputBlurb()
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
                .Append(" on ").Append(SaveTime).Append(", ")
                .AppendRule(SaveTimeValue.TimeAgo()).Append(" ago.")
                .AppendLine();

            sB.AppendLine().AppendBonesStatsBlurb(Stats, Name)
                .AppendLine();

            sB.AppendLine().Append("Based on your current mod configuration, these bones are weighted ")
                .AppendRule((GetBonesWeight() ?? 0).ToString()).Append(", compared to the default of ").Append(BaseBonesWeight).Append(".")
                .AppendLine();

            return Event.FinalizeString(sB);
        }

        public void Cremate()
        {
            if (IsCrematable)
            {
                BonesManager.DeleteBonesInfoDirectory(Directory);
                WasCremated = true;
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

        public bool AttemptLoad(Zone Z)
            => BonesManager.System.AttemptLoadBones(Z, this)
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
                            Icon = new Renderable(
                                Tile: "Abilities/abil_berate.bmp",
                                ColorString: $"&K",
                                TileColor: $"&K",
                                DetailColor: 'R'),
                            Callback = async delegate (IEnumerable<string> element)
                            {
                                string message = Event.FinalizeString(Event.NewStringBuilder().AppendUnavailableMods(element));

                                await Popup.NewPopupMessageAsync(
                                    message: message,
                                    title: unavailableCount.Things("unavailable mod").Colored("red"),
                                    afterRender: new Renderable(
                                        Tile: "Abilities/abil_berate.bmp",
                                        ColorString: $"&K",
                                        TileColor: $"&K",
                                        DetailColor: 'R'));

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
                            IntroIcon: new Renderable(
                                Tile: "Items/sw_toolbox_large.bmp",
                                ColorString: $"&c",
                                TileColor: $"&c",
                                DetailColor: 'C'),
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
                            afterRender: new Renderable(
                                Tile: "Items/sw_toolbox_large.bmp",
                                ColorString: $"&c",
                                TileColor: $"&c",
                                DetailColor: 'C'));
                    }

                }
                while (!result.IsCancel());

                Event.ResetTo(sB);

                if (!loadedButBonesMissing.IsNullOrEmpty()
                    || !bonesHasButNotLoaded.IsNullOrEmpty())
                {
                    /*
                    var options = new Dictionary<string, string>
                    {
                        { "Restart {{yellow|adding enabled}} mods from bones file's mod configuration", "Add" },
                        { "Restart {{red|using}} bones file's {{red|entire}} mod configuration", "Use" },
                    };
                    using var optionsStrings = ScopeDisposedList<string>.GetFromPoolFilledWith(options.Keys);

                    if (bonesHasButNotLoaded.IsNullOrEmpty())
                        optionsStrings.RemoveAt(0);

                    if (loadedButBonesMissing.IsNullOrEmpty())
                        optionsStrings.RemoveAt(1);

                    int pickedIndex = await Popup.PickOptionAsync(
                        Title: "Mod Configuration Differs",
                        Intro: Event.FinalizeString(sB),
                        Options: optionsStrings,
                        AllowEscape: true);

                    if (pickedIndex < 0)
                        return false;

                    string picked = options[optionsStrings[pickedIndex]];

                    if (picked.EqualsNoCase("Add")
                        || picked.EqualsNoCase("Use"))
                        foreach (string bonesExtra in bonesHasButNotLoaded)
                            ModManager.GetMod(bonesExtra).IsEnabled = true;

                    if (picked.EqualsNoCase("Use"))
                        foreach (string bonesMissing in loadedButBonesMissing)
                            ModManager.GetMod(bonesMissing).IsEnabled = false;

                    ModManager.WriteModSettings();
                    GameManager.Restart();
                    */
                }
                else
                {
                    /*
                    await Popup.NewPopupMessageAsync(
                        message: Event.FinalizeString(sB),
                        title: "Mod Configuration");
                    */
                }
            }
            Event.ResetTo(sB);
            return false;
        }

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
