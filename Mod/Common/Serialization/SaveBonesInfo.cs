using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Platform.IO;

using Qud.API;

using UnityEngine;

using XRL;
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

        public string ZoneID;
        public string DeathReason;

        public string FileName;

        public string Pending => GetBonesJSON()?.Pending;

        public bool IsPending => Pending?.EqualsNoCase($"{false}") is not true;


        public ZoneRequest ZoneRequest => new(ZoneID);

        public SaveBonesInfo()
            : base()
        { }

        public SaveBonesInfo(
            string ZoneID,
            string DeathReason
            )
            : this()
        {
            this.ZoneID = ZoneID;
            this.DeathReason = DeathReason;
        }

        public SaveBonesInfo(IDeathEvent DeathEvent)
            : this(
                  ZoneID: DeathEvent?.Dying?.CurrentZone?.ZoneID,
                  DeathReason: DeathEvent?.Reason)
        { }

        public SaveBonesInfo(
            string ZoneID,
            string DeathReason,
            SaveGameInfo Source
            )
            : this(ZoneID, DeathReason)
        {
            if (Source != null)
            {
                ID = Source.ID;
                Name = Source.Name;
                Description = Source.Description;
                SaveTime = Source.SaveTime;
                Info = Source.Info;
                Version = Source.Version;
                json = new SaveBonesJSON(ZoneID, DeathReason, Source.json);
                Directory = Source.Directory;
                Size = Source.Size;
                ModsEnabled = Source.ModsEnabled;
            }
        }

        public SaveBonesInfo(
            IDeathEvent DeathEvent,
            SaveBonesInfo Source
            )
            : this(
                  ZoneID: DeathEvent?.Dying?.CurrentZone?.ZoneID,
                  DeathReason: DeathEvent?.Reason,
                  Source: Source)
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
            UD_Bones_BonesManager.DeleteBonesInfoDirectory(Directory);
        }
    }
}
