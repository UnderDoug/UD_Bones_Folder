using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;

using Platform.IO;

using Qud.API;

using XRL.World;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class SaveBonesJSON : SaveGameJSON
    {
        [JsonProperty]
        public Guid OsseousAshID;
        public string OsseousAshHandle;

        public string ModVersion;
        public long SaveTimeValue;

        public string ZoneID;
        public string DeathReason;

        public string GenotypeName;
        public string SubtypeName;
        public string Blueprint;

        [JsonProperty]
        public BonesSpec BonesSpec;

        [JsonProperty]
        public DirectoryInfo.DirectoryType DirectoryType;

        public string Pending;
        public int Encountered;

        [JsonIgnore]
        private bool CharIconSwapped;
        [JsonIgnore]
        private string OriginalCharIcon;

        public SaveBonesJSON()
            : base()
        { 
            Pending = $"{false}";
        }

        public static long GetDirectorySize(string Path)
        {
            IEnumerable<string> files = Directory.EnumerateFiles(Path);

            long size = 0L;
            foreach (string file in files)
                size += new FileInfo(file).Length;

            return size;
        }

        public static SaveBonesInfo InfoFromJson(
            SaveBonesJSON SaveBonesJSON,
            string DirPath,
            string FilePath,
            long SaveSize
            )
        {
            if (SaveBonesJSON == null)
            {
                return new SaveBonesInfo
                {
                    Name = "Corrupt info file".Colored("R"),
                    Size = $"Total size: {SaveSize / 1000}kb",
                    Info = "",
                    Directory = DirPath
                };
            }

            DateTime saveTimeValue;
            try
            {
                saveTimeValue = new DateTime(SaveBonesJSON.SaveTimeValue).ToLocalTime();
            }
            catch //(Exception x)
            {
                // Utils.Error(Utils.CallChain(nameof(SaveBonesJSON), nameof(ReadSaveBonesJson)), x);
                saveTimeValue = new DateTime(2026, 04, 01, 11, 59, 59, DateTimeKind.Local);
            }

            var bonesSpec = SaveBonesJSON.BonesSpec
                ?? new BonesSpec
                {
                    Level = SaveBonesJSON.Level,
                    ZoneID = SaveBonesJSON.ZoneID,
                };

            var saveBonesInfo = new SaveBonesInfo
            {
                json = SaveBonesJSON,
                Directory = DirPath,
                Size = $"Total size: {SaveSize / 1000}kb",
                ID = SaveBonesJSON.ID,
                Version = SaveBonesJSON.GameVersion,
                Name = SaveBonesJSON.Name,
                Description = $"Level {SaveBonesJSON.Level} {SaveBonesJSON.GenoSubType}",// [{json.GameMode}]",
                Info = $"{SaveBonesJSON.Location}, {SaveBonesJSON.InGameTime} turn {SaveBonesJSON.Turn}",
                SaveTime = SaveBonesJSON.SaveTime,
                ModsEnabled = SaveBonesJSON.ModsEnabled,

                JSONFilePath = FilePath,
                SaveTimeValue = saveTimeValue,

                ModVersion = SaveBonesJSON.ModVersion,

                DeathReason = SaveBonesJSON.DeathReason,

                GenotypeName = SaveBonesJSON.GenotypeName,
                SubtypeName = SaveBonesJSON.SubtypeName,

                BonesSpec = bonesSpec,
            };

            if (!SaveBonesJSON.CharIcon.IsTile())
                SaveBonesJSON.HotSwapCharIcon();

            if (SaveBonesJSON.SaveVersion < 395
                || SaveBonesJSON.SaveVersion > 400)
            {
                string olderVersionString = $"Older Version ({SaveBonesJSON.GameVersion})".Colored("R");
                saveBonesInfo.Name = $"{olderVersionString} {saveBonesInfo.Name}";
            }

            return saveBonesInfo;
        }

        public static async Task<SaveBonesInfo> ReadSaveBonesJson(string DirPath, string FilePath)
        {
            SaveBonesJSON bonesJSON = null;
            try
            {
                bonesJSON = await File.ReadAllJsonAsync<SaveBonesJSON>(FilePath);
            }
            catch (Exception x)
            {
                Utils.Error($"Loading bones json {FilePath}", x);
            }
            return InfoFromJson(bonesJSON, DirPath, FilePath, GetDirectorySize(DirPath));
        }

        public bool IsCharIconSwapped()
            => CharIconSwapped
            ;

        public void HotSwapCharIcon()
        {
            if (CharIconSwapped)
            {
                CharIcon = OriginalCharIcon;
                CharIconSwapped = false;
            }
            else
            {
                OriginalCharIcon = CharIcon;
                CharIcon = Const.MAD_LUNAR_REGENT_TILE;
                CharIconSwapped = true;
            }
        }
    }
}
