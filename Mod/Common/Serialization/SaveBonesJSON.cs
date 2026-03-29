using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Platform.IO;

using Qud.API;

using XRL.World;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class SaveBonesJSON : SaveGameJSON
    {
        public string ModVersion;

        public string ZoneID;
        public string DeathReason;

        public string GenotypeName;
        public string SubtypeName;

        public string ZoneTerrainType;
        public int ZoneTier;
        public string ZoneRegion;

        public string Pending;

        public SaveBonesJSON()
            : base()
        { 
            Pending = $"{false}";
        }

        public SaveBonesJSON(
            string ZoneID,
            string DeathReason
            )
            : this()
        {
            this.ZoneID = ZoneID;
            this.DeathReason = DeathReason;
        }

        public SaveBonesJSON(IDeathEvent DeathEvent)
            : this(
                  ZoneID: DeathEvent?.Dying?.CurrentZone?.ZoneID,
                  DeathReason: DeathEvent?.Reason)
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
                  ThirdPersonDeathReason: DeathEvent?.Reason,
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
                    Name = "Corrupt info file".WithColor("R"),
                    Size = $"Total size: {GetDirectorySize(DirPath) / 1000000}mb",
                    Info = "",
                    Directory = DirPath
                };
            }

            var saveBonesInfo = new SaveBonesInfo
            {
                json = json,
                Directory = DirPath,
                Size = $"Total size: {GetDirectorySize(DirPath) / 1000000}mb",
                ID = json.ID,
                Version = json.GameVersion,
                Name = json.Name,
                Description = $"Level {json.Level} {json.GenoSubType} [{json.GameMode}]",
                Info = $"{json.Location}, {json.InGameTime} turn {json.Turn}",
                SaveTime = json.SaveTime,
                ModsEnabled = json.ModsEnabled,

                ModVersion = Utils.ThisMod.Assembly.GetName().Version.ToString(),

                FileName = FilePath,
            };
            if (json.SaveVersion < 395
                || json.SaveVersion > 400)
            {
                string olderVersionString = $"Older Version ({json.GameVersion})".WithColor("R");
                saveBonesInfo.Name = $"{olderVersionString} {saveBonesInfo.Name}";
            }

            return saveBonesInfo;
        }
    }
}
