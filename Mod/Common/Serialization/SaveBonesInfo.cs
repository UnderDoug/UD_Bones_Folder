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

        public bool IsMad => GetBonesJSON().IsCharIconSwapped()
            || !GenotypeFactory.GenotypesByName.ContainsKey(GenotypeName)
            || !SubtypeFactory.SubtypesByName.ContainsKey(SubtypeName)
            || !GameObjectFactory.Factory.BlueprintList.Any(bp => bp.Name == GetBonesJSON()?.Blueprint)
            ;

        private ModsDifferInfo _ModsDiffer;
        public ModsDifferInfo ModsDiffer => _ModsDiffer ??= GetModsDifferInfo();

        public ZoneRequest ZoneRequest => new(BonesSpec.ZoneID);

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

        private FileLocationData _FileLocationData;
        public FileLocationData FileLocationData
        {
            get
            {
                if (_FileLocationData is null
                    && GetBonesJSON() is SaveBonesJSON bonesJSON
                    && !Directory.IsNullOrEmpty())
                {
                    using var assumedLocationData = FileLocationData.NewAssumed(Directory);
                    FileLocationData.LocationType type = assumedLocationData.Type;
                    if (bonesJSON.FileLocationType != FileLocationData.LocationType.None)
                        type = bonesJSON.FileLocationType;

                    _FileLocationData = new(type, assumedLocationData.Path, assumedLocationData.Host);
                    // Utils.Log($"New {nameof(_FileLocationData)}: {_FileLocationData?.SanitiseForDisplay() ?? "NO_DATA"}");
                }
                return _FileLocationData;
            }
        }

        public OsseousAsh.Host Host => FileLocationData?.Host;

        public bool IsOnline
            => FileLocationData.Type == FileLocationData.LocationType.Online
            || Host != null
            ;

        public bool IsCrematable
            => FileLocationData.Type < FileLocationData.LocationType.Mod
            && FileLocationData.Exists()
            ;

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

        public async Task<System.IO.Stream> GetSavGzStreamAsync()
        {
            if ((await GetBonesFilePathAsync()) is string bonesPath)
                return File.OpenRead(bonesPath);

            byte[] cloudBytes = Host.GetBonesSavGz(ID);

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
                sB.Append("come from").Append(OsseousAshHandle);
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

            sB.AppendLine().AppendBonesStatsBlurb(Stats, Name);

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

            var sB = Event.NewStringBuilder();
            if (bonesHasButNotAvailable.Count > 0)
            {
                sB.Append("One or more mods enabled in this save are ")
                    .AppendColored("red", "not available")
                    .Append(": ");

                bonesHasButNotAvailable
                    .Select(ModManager.GetModTitle)
                    .Aggregate(
                        seed: sB,
                        func: (a, n) =>
                            a.AppendLine()
                                .AppendColored("y", ":").Append(" ")
                                .AppendColored("red", n)
                            )
                    .AppendLine()
                    .AppendLine()
                    .Append("Do you still wish to try to load this save?");

                if ((await Popup.NewPopupMessageAsync(
                    message: sB.ToString(),
                    buttons: PopupMessage.YesNoButton,
                    title: "Incomplete Mod Configuration")).command != PopupMessage.YesNoButton[0].command)
                {
                    Event.ResetTo(sB);
                    return false;
                }
                sB.Clear();
            }
            if (!bothHaveLoaded.IsNullOrEmpty()
                || !loadedButBonesMissing.IsNullOrEmpty()
                || !bonesHasButNotLoaded.IsNullOrEmpty())
            {
                if (!loadedButBonesMissing.IsNullOrEmpty())
                {
                    loadedButBonesMissing
                        .Select(ModManager.GetModTitle)
                        .Aggregate(
                            seed: sB.Compound("These enabled mods are {{yellow|disabled}} in this bones file:", '\n'),
                            func: (a, n) =>
                                a.AppendLine()
                                    .AppendColored("y", ":").Append(" ")
                                    .AppendColored("yellow", n)
                                )
                        .AppendLine();
                }
                if (!bonesHasButNotLoaded.IsNullOrEmpty())
                {
                    bonesHasButNotLoaded
                        .Select(ModManager.GetModTitle)
                        .Aggregate(
                            seed: sB.Compound("These disabled mods are {{red|enabled}} in this bones file:", '\n'),
                            func: (a, n) =>
                                a.AppendLine()
                                    .AppendColored("y", ":").Append(" ")
                                    .AppendColored("red", n)
                                )
                        .AppendLine();
                }
                if (!bothHaveLoaded.IsNullOrEmpty())
                {
                    bothHaveLoaded
                        .Select(ModManager.GetModTitle)
                        .Aggregate(
                            seed: sB.Compound("These enabled mods are {{green|enabled}} in this bones file:", '\n'),
                            func: (a, n) =>
                                a.AppendLine()
                                    .AppendColored("y", ":").Append(" ")
                                    .AppendColored("green", n)
                                )
                        .AppendLine();
                }
                if (!loadedButBonesMissing.IsNullOrEmpty()
                    || !bonesHasButNotLoaded.IsNullOrEmpty())
                {
                    sB.AppendLine();
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
                }
                else
                {
                    await Popup.NewPopupMessageAsync(
                        message: Event.FinalizeString(sB),
                        title: "Mod Configuration");
                }
            }
            Event.ResetTo(sB);
            return false;
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
