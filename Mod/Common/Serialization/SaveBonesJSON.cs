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
        public Guid OsseousAshID;
        public string OsseousAshHandle;

        public string ModVersion;
        public long SaveTimeValue;

        public string ZoneID;
        public string DeathReason;

        public string GenotypeName;
        public string SubtypeName;
        public string Blueprint;
/*
        [JsonProperty]
        public Dictionary<string, string> BonesSpec;
*/
        [JsonProperty]
        public BonesSpec BonesSpec;

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

            if (bonesJSON == null)
            {
                return new SaveBonesInfo
                {
                    Name = "Corrupt info file".Colored("R"),
                    Size = $"Total size: {GetDirectorySize(DirPath) / 1000}kb",
                    Info = "",
                    Directory = DirPath
                };
            }

            DateTime saveTimeValue;
            try
            {
                saveTimeValue = new DateTime(bonesJSON.SaveTimeValue).ToLocalTime();
            }
            catch //(Exception x)
            {
                // Utils.Error(Utils.CallChain(nameof(SaveBonesJSON), nameof(ReadSaveBonesJson)), x);
                saveTimeValue = new DateTime(2026, 04, 01, 11, 59, 59, DateTimeKind.Local);
            }

            var bonesSpec = bonesJSON.BonesSpec
                ?? new BonesSpec
                {
                    Level = bonesJSON.Level,
                    ZoneID = bonesJSON.ZoneID,
                };

            var saveBonesInfo = new SaveBonesInfo
            {
                json = bonesJSON,
                Directory = DirPath,
                Size = $"Total size: {GetDirectorySize(DirPath) / 1000}kb",
                ID = bonesJSON.ID,
                Version = bonesJSON.GameVersion,
                Name = bonesJSON.Name,
                Description = $"Level {bonesJSON.Level} {bonesJSON.GenoSubType}",// [{json.GameMode}]",
                Info = $"{bonesJSON.Location}, {bonesJSON.InGameTime} turn {bonesJSON.Turn}",
                SaveTime = bonesJSON.SaveTime,
                ModsEnabled = bonesJSON.ModsEnabled,

                JSONFilePath = FilePath,
                SaveTimeValue = saveTimeValue,

                ModVersion = bonesJSON.ModVersion,

                DeathReason = bonesJSON.DeathReason,

                GenotypeName = bonesJSON.GenotypeName,
                SubtypeName = bonesJSON.SubtypeName,

                BonesSpec = bonesSpec,
            };

            if (!bonesJSON.CharIcon.IsTile())
                bonesJSON.HotSwapCharIcon();

            if (bonesJSON.SaveVersion < 395
                || bonesJSON.SaveVersion > 400)
            {
                string olderVersionString = $"Older Version ({bonesJSON.GameVersion})".Colored("R");
                saveBonesInfo.Name = $"{olderVersionString} {saveBonesInfo.Name}";
            }

            return saveBonesInfo;
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
