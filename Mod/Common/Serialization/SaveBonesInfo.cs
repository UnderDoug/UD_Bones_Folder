using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConsoleLib.Console;

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

        public static SaveBonesInfoComparer SaveBonesInfoComparerDescending = new SaveBonesInfoComparer(Ascending: true);

        private static readonly string[] InfoFiles = new string[2]
        {
            $"{UD_Bones_BonesSaver.BonesName}.json",
            $"{UD_Bones_BonesSaver.BonesName}.sav.json"
        };

        public string FileName;
        public DateTime SaveTimeValue;

        public string ModVersion;

        public string ZoneID;
        public string DeathReason;

        public string GenotypeName;
        public string SubtypeName;

        public string ZoneTerrainType;
        public int ZoneTier;
        public string ZoneRegion;

        public string Pending => GetBonesJSON()?.Pending;

        public bool IsPending => Pending?.EqualsNoCase($"{false}") is not true;

        public int Encountered => GetBonesJSON()?.Encountered ?? -1;

        public bool IsMad => GetBonesJSON().IsCharIconSwapped()
            || !GenotypeFactory.GenotypesByName.ContainsKey(GenotypeName)
            || !SubtypeFactory.SubtypesByName.ContainsKey(SubtypeName)
            ;

        private ModsDifferInfo _ModsDiffer;
        public ModsDifferInfo ModsDiffer => _ModsDiffer ??= GetModsDifferInfo();

        public ZoneRequest ZoneRequest => new(ZoneID);

        public bool WasCremated;

        public bool? _IsEligible;
        public bool IsEligible => _IsEligible ??= IsEligibleForCurrentSave(Strict: true);

        public bool? _IsLooselyEligible;
        public bool IsLooselyEligible => _IsLooselyEligible ??= IsEligibleForCurrentSave();

        private BonesRender _Render;
        public BonesRender Render => _Render ??= new(GetBonesJSON());

        public string DisplayDirectory => DataManager.SanitizePathForDisplay(Directory);

        public SaveBonesInfo()
            : base()
        { }

        public string GetName()
            => $"{(IsMad ? "Mad " : null)}{Name}".StartReplace().ToString();

        public static async Task SetPending(SaveBonesInfo BonesInfo, string Pending)
        {
            if (Pending.IsNullOrEmpty())
                Pending = $"{false}";

            if (BonesInfo.GetBonesJSON() is not SaveBonesJSON bonesJSON)
            {
                Utils.Warn($"Attempted to set {nameof(SaveBonesJSON)}.{nameof(Pending)} to {Pending ?? $"{false}"} for null {nameof(SaveBonesJSON)}.");
                return;
            }

            if (bonesJSON.Pending.EqualsNoCase($"{false}") != Pending.EqualsNoCase($"{false}"))
            {
                bonesJSON.Pending = Pending;
                string bonesFilePath = Path.Combine(BonesInfo.Directory, BonesInfo.FileName);
                if (await File.ExistsAsync(bonesFilePath))
                {
                    bool swappedIcon = bonesJSON.IsCharIconSwapped();
                    if (swappedIcon)
                        bonesJSON.HotSwapCharIcon();

                    File.WriteAllText(bonesFilePath, JsonUtility.ToJson(bonesJSON, prettyPrint: true));

                    if (swappedIcon)
                        bonesJSON.HotSwapCharIcon();
                }
            }
            else
            {
                string toValue = "a GameID when it already has one";
                if (bonesJSON.Pending.EqualsNoCase($"{false}"))
                    toValue = $"\"{false}\" when it already is";
                Utils.Warn($"Attempted to set {BonesInfo.DisplayDirectory} {nameof(SaveBonesJSON)}.{nameof(Pending)} to {toValue}.");
            }
        }

        public static async Task IncrementEncountered(SaveBonesInfo BonesInfo)
        {
            if (BonesInfo.GetBonesJSON() is not SaveBonesJSON bonesJSON)
            {
                Utils.Warn($"Attempted to increment {nameof(SaveBonesJSON)}.{nameof(SaveBonesJSON.Encountered)} for null {nameof(SaveBonesJSON)}.");
                return;
            }

            bonesJSON.Encountered++;
            string bonesFilePath = Path.Combine(BonesInfo.Directory, BonesInfo.FileName);
            if (await File.ExistsAsync(bonesFilePath))
            {
                bool swappedIcon = bonesJSON.IsCharIconSwapped();
                if (swappedIcon)
                    bonesJSON.HotSwapCharIcon();

                File.WriteAllText(bonesFilePath, JsonUtility.ToJson(bonesJSON, prettyPrint: true));

                if (swappedIcon)
                    bonesJSON.HotSwapCharIcon();
            }
        }

        public static async Task<SaveBonesInfo> GetSaveBonesInfo(string Directory)
        {
            try
            {
                if (Path.GetFileNameWithoutExtension(Directory).EqualsNoCase("mods")
                    || Path.GetFileNameWithoutExtension(Directory).EqualsNoCase("textures"))
                    return null;

                foreach (string infoFile in InfoFiles)
                {
                    if (Path.Combine(Directory, infoFile) is string path
                        && File.Exists(path))
                    {
                        return await SaveBonesJSON.ReadSaveBonesJson(Directory, path);
                    }
                }
                if (!Platform.IO.Directory.EnumerateFiles(Directory).Any(f => !f.EndsWith("Cache.db")))
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
            => json as SaveBonesJSON;


        public IEnumerable<string> GetDebugLines()
        {
            yield return $"{nameof(ID)}: {ID}";
            yield return $"{nameof(Name)}: {Name}";
            yield return $"{nameof(ZoneID)}: {ZoneID}";

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

            int saveWeight = 50;

            saveWeight += ModsDiffer.EnabledWhereBonesDisabled * -1;
            saveWeight += ModsDiffer.DisabledWhereBonesEnabled * -2;

            foreach (var runningMod in BonesManager.RunningMods)
                if (ModsEnabled.Contains(runningMod))
                    saveWeight += 1;

            saveWeight = +ModsDiffer.UnavailableWhereBonesEnabled * -4;

            return Math.Max(1, saveWeight);
        }

        public void Cremate()
        {
            BonesManager.DeleteBonesInfoDirectory(Directory);
            WasCremated = true;
        }

        public bool IsEligibleForCurrentSave(bool Strict = false)
        {
            if (GetBonesJSON() is not SaveBonesJSON bonesJSON)
                return false;

            if (IsPending)
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

        public int CompareTo(SaveBonesInfo other)
            => other != null
            ? SaveTimeValue.CompareTo(other.SaveTimeValue)
            : -1
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
