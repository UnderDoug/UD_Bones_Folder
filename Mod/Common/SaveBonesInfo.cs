using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Platform.IO;

using Qud.API;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace Bones.Mod
{
    [Serializable]
    public class SaveBonesInfo : SaveGameInfo
    {
        private static readonly string[] InfoFiles = new string[2]
        {
            $"{BonesSaver.BonesName}.json",
            $"{BonesSaver.BonesName}.sav.json"
        };

        public string ZoneID;
        public string ThirdPersonDeathReason;

        public ZoneRequest ZoneRequest => new(ZoneID);

        public SaveBonesInfo()
            : base()
        { }

        public SaveBonesInfo(
            string ZoneID,
            string ThirdPersonDeathReason
            )
            : this()
        {
            this.ZoneID = ZoneID;
            this.ThirdPersonDeathReason = ThirdPersonDeathReason;
        }

        public SaveBonesInfo(IDeathEvent DeathEvent)
            : this(
                  ZoneID: DeathEvent?.Dying?.CurrentZone?.ZoneID,
                  ThirdPersonDeathReason: DeathEvent?.ThirdPersonReason)
        { }

        public SaveBonesInfo(
            string ZoneID,
            string ThirdPersonDeathReason,
            SaveGameInfo Source
            )
            : this(ZoneID, ThirdPersonDeathReason)
        {
            if (Source != null)
            {
                ID = Source.ID;
                Name = Source.Name;
                Description = Source.Description;
                SaveTime = Source.SaveTime;
                Info = Source.Info;
                Version = Source.Version;
                json = new SaveBonesJSON(ZoneID, ThirdPersonDeathReason, Source.json);
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
                  ThirdPersonDeathReason: DeathEvent?.ThirdPersonReason,
                  Source: Source)
        { }

        public static SaveBonesInfo GetSaveBonesInfo(string Directory)
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
                        return SaveBonesJSON.ReadSaveBonesJson(Directory, path).Result;
                    }
                }
                if (!System.IO.Directory.EnumerateFileSystemEntries(Directory).Any(f => !f.EndsWith("Cache.db")))
                {
                    try
                    {
                        System.IO.Directory.Delete(Directory, recursive: true);
                    }
                    catch (Exception message)
                    {
                        MetricsManager.LogWarning(message);
                    }
                }
                else
                    MetricsManager.LogWarning($"Weird bones directory with no .json file present: {DataManager.SanitizePathForDisplay(Directory)}");

            }
            catch (ThreadInterruptedException x)
            {
                throw x;
            }
            catch (Exception x)
            {
                MetricsManager.LogWarning(x);
            }
            return null;
        }
    }
}
