using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Platform.IO;

using Qud.API;
using Qud.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using Event = XRL.World.Event;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class SaveBonesInfo : SaveGameInfo
    {
        private static readonly string[] InfoFiles = new string[2]
        {
            $"{UD_Bones_BonesSaver.BonesName}.json",
            $"{UD_Bones_BonesSaver.BonesName}.sav.json"
        };

        public string FileName;

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


        public ZoneRequest ZoneRequest => new(ZoneID);

        public SaveBonesInfo()
            : base()
        { }

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
                    File.WriteAllText(bonesFilePath, JsonUtility.ToJson(bonesJSON, prettyPrint: true));
            }
            else
            {
                string toValue = "a GameID when it already has one";
                if (bonesJSON.Pending.EqualsNoCase($"{false}"))
                    toValue = $"\"{false}\" when it already is";
                Utils.Warn($"Attempted to set {DataManager.SanitizePathForDisplay(BonesInfo.Directory)} {nameof(SaveBonesJSON)}.{nameof(Pending)} to {toValue}.");
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

        public void Cremate()
        {
            BonesManager.DeleteBonesInfoDirectory(Directory);
        }

        public async Task<bool> TryRestoreModsAsync()
        {
            await The.UiContext;
            return await RestoreModsLoadedAsync(ModsEnabled);
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
                    var options = new string[2]
                    {
                        "Restart {{yellow|adding enabled}} mods from bones file's mod configuration",
                        "Restart {{red|using}} bones file's {{red|entire}} mod configuration",
                    };
                    int picked = await Popup.PickOptionAsync(
                        Title: "Mod Configuration Differs",
                        Intro: Event.FinalizeString(sB),
                        Options: options,
                        AllowEscape: true);

                    if (picked < 0)
                        return false;

                    if (picked < 1)
                        foreach (string item2 in bonesHasButNotLoaded)
                            ModManager.GetMod(item2).IsEnabled = true;

                    if (picked < 2)
                        foreach (string item in loadedButBonesMissing)
                            ModManager.GetMod(item).IsEnabled = false;

                    ModManager.WriteModSettings();
                    GameManager.Restart();
                }
                else
                {
                    var buttons = new List<QudMenuItem>(PopupMessage.AcceptButton);
                    var button = buttons[0];
                    button.text = "Continue";
                    await Popup.NewPopupMessageAsync(
                        message: Event.FinalizeString(sB),
                        buttons: buttons,
                        title: "Mod Configuration");
                }
            }
            Event.ResetTo(sB);
            return false;
        }
    }
}
