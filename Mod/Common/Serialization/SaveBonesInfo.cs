using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Platform.IO;

using Qud.API;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

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
        public string ModVersion;

        public string ZoneID;
        public string DeathReason;

        public string GenotypeName;
        public string SubtypeName;

        public string ZoneTerrainType;
        public int ZoneTier;
        public string ZoneRegion;

        public string FileName;

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
            if (await The.Core.RestoreModsLoadedAsync(ModsEnabled))
            {
                var unionizedMods = Utils.GetCriticalWarningUnion(BonesManager.RunningMods, ModsEnabled);
                
                if (unionizedMods.IsNullOrEmpty())
                {
                    Utils.Error("Failed to get running mods", new InvalidOperationException("Impossibly empty running mods list"));
                    return false;
                }

                string message = "The below mods are now enabled:\n";
                if (unionizedMods.Count != BonesManager.RunningMods.Count()
                    || unionizedMods.Count == ModsEnabled.Count)
                {
                    message = "Of the below mods,\n" +
                        "\tthe {{R|red}} are enabled in the bones file but are not loaded,\n" +
                        "\t the {{Y|yellow}} are loaded but missing from the bones file:\n";
                }

                return Popup.ShowAsync(
                    Message: unionizedMods.Aggregate(
                        seed: message,
                        func: Utils.NewLineDelimitedAggregator)
                    ).IsCompletedSuccessfully;
            }

            return false;
        }
    }
}
