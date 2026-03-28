using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Platform.IO;

using Qud.API;

using XRL.World;

namespace Bones.Mod
{
    [Serializable]
    public class SaveBonesJSON : SaveGameJSON
    {
        public string ZoneID;
        public string ThirdPersonDeathReason;

        public string Pending;

        public SaveBonesJSON()
            : base()
        { 
            Pending = $"{false}";
        }

        public SaveBonesJSON(
            string ZoneID,
            string ThirdPersonDeathReason
            )
            : this()
        {
            this.ZoneID = ZoneID;
            this.ThirdPersonDeathReason = ThirdPersonDeathReason;
        }

        public SaveBonesJSON(IDeathEvent DeathEvent)
            : this(
                  ZoneID: DeathEvent?.Dying?.CurrentZone?.ZoneID,
                  ThirdPersonDeathReason: DeathEvent?.ThirdPersonReason)
        { }

        public SaveBonesJSON(
            string ZoneID,
            string ThirdPersonDeathReason,
            SaveGameJSON Source
            )
            : this(ZoneID, ThirdPersonDeathReason)
        {
            if (Source != null)
            {
                InfoVersion = Source.InfoVersion;
                SaveVersion = Source.SaveVersion;
                GameVersion = Source.GameVersion;
                ID = Source.ID;
                Name = Source.Name;
                Level = Source.Level;
                GenoSubType = Source.GenoSubType;
                GameMode = Source.GameMode;
                CharIcon = Source.CharIcon;
                FColor = Source.FColor;
                DColor = Source.DColor;
                Location = Source.Location;
                InGameTime = Source.InGameTime;
                Turn = Source.Turn;
                SaveTime = Source.SaveTime;
                ModsEnabled = Source.ModsEnabled;
            }
        }

        public SaveBonesJSON(
            IDeathEvent DeathEvent,
            SaveGameJSON Source
            )
            : this(
                  ZoneID: DeathEvent?.Dying?.CurrentZone?.ZoneID,
                  ThirdPersonDeathReason: DeathEvent?.ThirdPersonReason,
                  Source: Source)
        { }

        public static long GetDirectorySize(string Path)
        {
            IEnumerable<string> files = Directory.EnumerateFiles(Path);

            long size = 0L;
            foreach (string file in files)
                size += new FileInfo(file).Length;

            return size;
        }

        public static async Task<SaveBonesInfo> ReadSaveBonesJson(string DirPath, string FilePath)
        {
            SaveBonesJSON json = null;
            try
            {
                json = await File.ReadAllJsonAsync<SaveBonesJSON>(FilePath);
            }
            catch (Exception x)
            {
                Utils.Error($"Loading save json {FilePath}", x);
            }

            if (json == null)
            {
                return new SaveBonesInfo
                {
                    Name = "&RCorrupt info file",
                    Size = "Total size: " + GetDirectorySize(DirPath) / 1000000 + "mb",
                    Info = "",
                    Directory = DirPath
                };
            }

            var saveBonesInfo = new SaveBonesInfo
            {
                json = json,
                Directory = DirPath,
                Size = "Total size: " + GetDirectorySize(DirPath) / 1000000 + "mb",
                ID = json.ID,
                Version = json.GameVersion,
                Name = json.Name,
                Description = $"Level {json.Level} {json.GenoSubType} [{json.GameMode}]",
                Info = $"{json.Location}, {json.InGameTime} turn {json.Turn}",
                SaveTime = json.SaveTime,
                ModsEnabled = json.ModsEnabled,
                ZoneID = json.ZoneID,
                FileName = FilePath,
            };
            if (json.SaveVersion < 395
                || json.SaveVersion > 400)
            {
                string olderVersionString = $"Older Version ({json.GameVersion})".Color("R");
                saveBonesInfo.Name = $"{olderVersionString} {saveBonesInfo.Name}";
            }

            return saveBonesInfo;
        }
    }
}
