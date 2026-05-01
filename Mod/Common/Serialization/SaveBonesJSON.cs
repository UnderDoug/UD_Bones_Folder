using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;

using Platform.IO;

using Qud.API;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class SaveBonesJSON : SaveGameJSON
    {
        [JsonIgnore]
        public static string FileName => $"{UD_Bones_BonesSaver.BonesName}.json";

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
        public FileLocationData.LocationType FileLocationType;

        [JsonIgnore]
        private BonesStats _Stats;
        public BonesStats Stats
        {
            get => _Stats ??= new();
            set => _Stats = value;
        }

        [JsonIgnore]
        private bool CharIconSwapped;
        [JsonIgnore]
        private string OriginalCharIcon;

        public SaveBonesJSON()
            : base()
        { }

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
            string SaveLocation,
            string FileName,
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
                    Directory = SaveLocation
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
                Directory = SaveLocation,
                Size = $"Total size: {SaveSize / 1000}kb",
                ID = SaveBonesJSON.ID,
                Version = SaveBonesJSON.GameVersion,
                Name = SaveBonesJSON.Name,
                Description = $"Level {SaveBonesJSON.Level} {SaveBonesJSON.GenoSubType}",// [{json.GameMode}]",
                Info = $"{SaveBonesJSON.Location}, {SaveBonesJSON.InGameTime} turn {SaveBonesJSON.Turn}",
                SaveTime = SaveBonesJSON.SaveTime,
                ModsEnabled = SaveBonesJSON.ModsEnabled,

                JSONFilePath = FileName,
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
        public SaveBonesInfo InfoFromJson(
            string SaveLocation,
            string FileName,
            long SaveSize
        ) => InfoFromJson(this, SaveLocation, FileName, SaveSize)
        ;

        public static async Task<SaveBonesInfo> ReadSaveBonesJson(string DirPath, string FileName)
        {
            try
            {
                // Utils.Log($"{nameof(ReadSaveBonesJson)}: {DataManager.SanitizePathForDisplay(DirPath)}, {FileName}");
                return InfoFromJson(
                    SaveBonesJSON: await File.ReadAllJsonAsync<SaveBonesJSON>(Path.Combine(DirPath, FileName)),
                    SaveLocation: DirPath,
                    FileName: FileName,
                    SaveSize: GetDirectorySize(DirPath));
            }
            catch (Exception x)
            {
                Utils.Error($"Loading bones json {DataManager.SanitizePathForDisplay(Path.Combine(DirPath, FileName))}", x);
                return null;
            }
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

        public void HotSwapCharIcon(Action EitherSideOf)
        {
            bool swappedIcon = IsCharIconSwapped();
            if (swappedIcon)
                HotSwapCharIcon();

            EitherSideOf.Invoke();

            if (swappedIcon)
                HotSwapCharIcon();
        }
    }
}
