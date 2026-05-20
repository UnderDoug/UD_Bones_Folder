using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;

using Platform.IO;

using Qud.API;
using Qud.UI;

using UD_Bones_Folder.Mod.UI;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class SaveBonesJSON : SaveGameJSON, IDisposable
    {
        [JsonIgnore]
        public static string FileName => $"{BonesManager.BonesFileName}.json";

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
        public long Size;

        [JsonIgnore]
        private bool CharIconSwapped;
        [JsonIgnore]
        private string OriginalCharIcon;

        public SaveBonesJSON()
            : base()
        { }

        public static SaveBonesJSON DummyBonesJSON(
            BonesManagement.VisibilityModes VisibilityMode,
            FileLocationData.LocationType LocationType = FileLocationData.LocationType.None
            )
            => new SaveBonesJSON
            {
                OsseousAshID = Guid.Empty,
                OsseousAshHandle = $"A Highlight Entropic Being",
                SaveVersion = 400,
                GameVersion = typeof(MainMenu).Assembly.GetName().Version.ToString(),
                ID = Guid.Empty.ToString(),
                Name = $"Bones That Are Yet To Exist",
                Level = 0,
                GenoSubType = "Future Bones",
                GameMode = VisibilityMode.ToString(),
                CharIcon = "Mutations/amnesia.bmp",
                FColor = 'K',
                DColor = 'B', //LocationType.TypeColor()[0],

                Location = $"A yet to be played run",
                InGameTime = $"{0:D2}:{0:D2}:{0:D2}",
                Turn = 0,
                SaveTime = $"{DateTime.Now.ToLongDateString()} at {DateTime.Now.ToLongTimeString()}",
                ModsEnabled = ModManager.GetRunningMods().ToList(),

                ModVersion = Utils.ThisMod.Manifest.Version.ToString(),

                SaveTimeValue = DateTime.Now.Ticks,

                ZoneID = "BonesWorld.11.22.1.1.10",

                BonesSpec = new BonesSpec(),
                Stats = new(),

                DeathReason = $"They don't exist yet",
                GenotypeName = "Future",
                SubtypeName = "Bones",
                Blueprint = "Humanoid",
            }
            ;

        public static SaveBonesInfo InfoFromJson(
            SaveBonesJSON SaveBonesJSON,
            FileLocationData FileLocationData,
            long SaveSize,
            bool IsDummy = false
            )
        {
            if (SaveBonesJSON == null)
            {
                return new SaveBonesInfo
                {
                    Name = "Corrupt info file".Colored("R"),
                    Size = $"Total size: {SaveSize / 1000}kb",
                    Info = "",
                    Directory = FileLocationData
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
                Directory = FileLocationData,
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

                GenotypeName = SaveBonesJSON.GenotypeName ?? "Human",
                SubtypeName = SaveBonesJSON.SubtypeName ?? "Adventurer",

                BonesSpec = bonesSpec,

                IsDummy = IsDummy,
            };

            saveBonesInfo.FileLocationDataSet.Add(FileLocationData);

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
            FileLocationData FileLocationData,
            long SaveSize
            )
            => InfoFromJson(this, FileLocationData, SaveSize)
            ;

        public static async Task<SaveBonesInfo> ReadSaveBonesJson(FileLocationData FileLocationData)
        {
            if (FileLocationData == null)
                return null;

            try
            {
                return InfoFromJson(
                    SaveBonesJSON: await File.ReadAllJsonAsync<SaveBonesJSON>(FileLocationData.WithFileName(FileName)),
                    FileLocationData: FileLocationData,
                    SaveSize: FileLocationData.GetDirectorySize());
            }
            catch (Exception x)
            {
                Utils.Error($"Loading bones json {FileLocationData.SanitiseForDisplay(FileName)}", x);
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

        public SaveBonesJSON Clone()
            => new SaveBonesJSON
            {
                OsseousAshID = OsseousAshID,
                OsseousAshHandle = OsseousAshHandle,
                SaveVersion = SaveVersion,
                GameVersion = GameVersion,
                ID = ID,
                Name = Name,
                Level = Level,
                GenoSubType = GenoSubType,
                GameMode = GameMode,
                CharIcon = CharIcon,
                FColor = FColor,
                DColor = DColor,

                Location = Location,
                InGameTime = InGameTime,
                Turn = Turn,
                SaveTime = SaveTime,
                ModsEnabled = new(ModsEnabled),

                ModVersion = ModVersion,

                SaveTimeValue = SaveTimeValue,

                ZoneID = ZoneID,

                BonesSpec = BonesSpec,
                Stats = new(Stats),

                FileLocationType = FileLocationType,

                DeathReason = DeathReason,
                GenotypeName = GenotypeName,
                SubtypeName = SubtypeName,
                Blueprint = Blueprint,
            }
            ;

        public void Dispose()
        {
            OsseousAshID = Guid.Empty;
            OsseousAshHandle = null;
            SaveVersion = 0;
            GameVersion = null;
            ID = null;
            Name = null;
            Level = 0;
            GenoSubType = null;
            GameMode = null;
            CharIcon = null;
            FColor = '\0';
            DColor = '\0';

            Location = null;
            InGameTime = null;
            Turn = 0L;
            SaveTime = null;
            ModsEnabled = null;

            ModVersion = null;

            SaveTimeValue = 0L;

            ZoneID = null;

            BonesSpec = null;
            Stats = null;

            FileLocationType = FileLocationData.LocationType.None;

            DeathReason = null;
            GenotypeName = null;
            SubtypeName = null;
            Blueprint = null;
        }
    }
}
