using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;

using Platform.IO;

using Qud.API;
using Qud.UI;

using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.OsseousAsh;
using UD_Bones_Folder.Mod.UI;

using XRL;
using XRL.World;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptIn)]
    [Serializable]
    public class SaveBonesJSON
        : SaveGameJSON
        , IBonesFileElement
        , IDisposable
    {
        [JsonIgnore]
        public static string FileName => $"{BonesManager.BonesFileName}.json";

        [JsonProperty(nameof(ID))] private string _ID // Base class member
        {
            get => ID;
            set => ID = value;
        }

        [JsonProperty] public bool? IsYou;
        [JsonProperty] public string Handle;

        [JsonProperty(nameof(InfoVersion))] // Base class member
        private int _InfoVersion
        {
            get => InfoVersion;
            set => InfoVersion = value;
        }

        [JsonProperty(nameof(SaveVersion))] // Base class member
        private int _SaveVersion
        {
            get => SaveVersion;
            set => SaveVersion = value;
        }

        [JsonProperty(nameof(GameVersion))] // Base class member
        private string _GameVersion
        {
            get => GameVersion;
            set => GameVersion = value;
        }

        [JsonProperty] public string ModVersion;
        [JsonProperty] public long SaveTimeValue;

        [JsonProperty(nameof(Name))] // Base class member
        private string _Name
        {
            get => Name;
            set => Name = value;
        }

        [JsonProperty(nameof(Level))] // Base class member
        private int _Level
        {
            get => Level;
            set => Level = value;
        }

        [JsonProperty(nameof(GenoSubType))] // Base class member
        private string _GenoSubType
        {
            get => GenoSubType;
            set => GenoSubType = value;
        }

        [JsonProperty(nameof(GameMode))] // Base class member
        private string _GameMode
        {
            get => GameMode;
            set => GameMode = value;
        }

        [JsonProperty(nameof(CharIcon))] // Base class member
        private string _CharIcon
        {
            get => CharIcon;
            set => CharIcon = value;
        }

        [JsonProperty(nameof(FColor))] // Base class member
        private char _FColor
        {
            get => FColor;
            set => FColor = value;
        }

        [JsonProperty(nameof(DColor))] // Base class member
        private char _DColor
        {
            get => DColor;
            set => DColor = value;
        }

        [JsonProperty(nameof(Location))] // Base class member
        private string _Location
        {
            get => Location;
            set => Location = value;
        }

        [JsonProperty(nameof(InGameTime))] // Base class member
        private string _InGameTime
        {
            get => InGameTime;
            set => InGameTime = value;
        }

        [JsonProperty(nameof(Turn))] // Base class member
        private long _Turn
        {
            get => Turn;
            set => Turn = value;
        }

        [JsonProperty(nameof(SaveTime))] // Base class member
        private string _SaveTime
        {
            get => SaveTime;
            set => SaveTime = value;
        }

        [JsonProperty] public string ZoneID;
        [JsonProperty] public string DeathReason;

        [JsonProperty] public string GenotypeName;
        [JsonProperty] public string SubtypeName;
        [JsonProperty] public string Blueprint;

        [JsonProperty] public BonesSpec BonesSpec;

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? Downloaded;

        [JsonProperty] public BonesStats BonesStats;

        [JsonProperty(nameof(Size))] private long? _Size;
        [JsonIgnore] public long Size
        {
            get => _Size.GetValueOrDefault();
            set => _Size = value;
        }

        [JsonIgnore] private bool CharIconSwapped;
        [JsonIgnore] private string OriginalCharIcon;

        // [JsonIgnore] private List<string> _ModsEnabled; // Base class member; to remember it exists
        [JsonProperty] public ModRecordSet EnabledMods;

        [JsonIgnore] public string BonesID
        {
            get => ID;
            set => ID = value;
        }

        public SaveBonesJSON()
            : base()
        {
            InfoVersion = 2;
        }

        public static SaveBonesJSON DummyBonesJSON(BonesManagement.VisibilityModes VisibilityMode)
            => new()
            {
                IsYou = true,
                Handle = $"A Highlight Entropic Being",
                SaveVersion = XRLGame.SaveVersion,
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
                EnabledMods = ModRecordSet.NewEmpty(),

                ModVersion = Utils.ThisMod.Manifest.Version.ToString(),

                SaveTimeValue = DateTime.Now.ToUniversalTime().Ticks,

                ZoneID = "BonesWorld.11.22.1.1.10",

                BonesSpec = new BonesSpec(),
                BonesStats = new(),

                DeathReason = $"They don't exist yet",
                GenotypeName = "Future",
                SubtypeName = "Bones",
                Blueprint = "Humanoid",
            }
            ;

        public static SaveBonesInfo CreateInfoFrom(
            SaveBonesJSON SaveBonesJSON,
            string Directory,
            long SaveSize,
            bool IsDummy = false
            )
        {
            string saveSize = ((int)SaveSize).FormatBytes();
            if (SaveBonesJSON == null)
            {
                return new SaveBonesInfo
                {
                    Name = "Corrupt info file".Colored("R"),
                    Size = $"Total size: {saveSize}",
                    Info = "",
                    Directory = Directory
                };
            }

            DateTime saveTimeValue = SaveBonesInfo.GetSaveTime(SaveBonesJSON);

            /*SaveBonesJSON.BonesSpec ??= new BonesSpec
            {
                Level = SaveBonesJSON.Level,
                ZoneID = SaveBonesJSON.ZoneID,
            };*/

            return new SaveBonesInfo
            {
                JSON = SaveBonesJSON,
                Directory = Directory,
                Size = $"Total size: {saveSize}",
                ID = SaveBonesJSON.ID,
                Version = SaveBonesJSON.GameVersion,
                Name = SaveBonesJSON.Name,
                Description = $"Level {SaveBonesJSON.Level} {SaveBonesJSON.GenoSubType}",// [{json.GameMode}]",
                Info = $"{SaveBonesJSON.Location}, {SaveBonesJSON.InGameTime} turn {SaveBonesJSON.Turn}",
                SaveTime = SaveBonesInfo.GetSaveTimeString(SaveBonesJSON),

                SaveTimeValue = saveTimeValue,

                IsDummy = IsDummy,
            };
        }

        public static SaveBonesInfo CreateInfoFrom(
            SaveBonesJSON SaveBonesJSON,
            IBonesSource BonesSource
            )
            => CreateInfoFrom(
                    SaveBonesJSON: SaveBonesJSON,
                    Directory: BonesSource.SourceID,
                    SaveSize: SaveBonesJSON.Size)
                .AddSource(BonesSource)
            ;

        public SaveBonesInfo CreateInfoWithSource(IBonesSource BonesSource)
            => CreateInfoFrom(
                SaveBonesJSON: this,
                BonesSource: BonesSource)
            ;

        public virtual SaveBonesJSON MarkAsOnline(Host Host)
        {
            Downloaded = true;
            return this;
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
            try
            {
                EitherSideOf.Invoke();
            }
            finally
            {
                if (swappedIcon)
                    HotSwapCharIcon();
            }
        }

        public async Task HotSwapCharIconAsync(Task EitherSideOf)
        {
            bool swappedIcon = IsCharIconSwapped();
            if (swappedIcon)
                HotSwapCharIcon();
            try
            {
                await EitherSideOf;
            }
            finally
            {
                if (swappedIcon)
                    HotSwapCharIcon();
            }
        }

        public SaveBonesJSON CopyTo(SaveBonesJSON Other)
        {
            Other.ID = ID;

            Other.IsYou = IsYou;
            Other.Handle = Handle;

            Other.InfoVersion = InfoVersion;
            Other.SaveVersion = SaveVersion;
            Other.GameVersion = GameVersion;
            Other.ModVersion = ModVersion;
            Other.SaveTimeValue = SaveTimeValue;

            Other.Name = Name;
            Other.Level = Level;
            Other.GenoSubType = null;
            Other.GameMode = GameMode;
            Other.CharIcon = IsCharIconSwapped() ? OriginalCharIcon : CharIcon;
            Other.FColor = FColor;
            Other.DColor = DColor;
            Other.Location = Location;
            Other.InGameTime = InGameTime;
            Other.Turn = Turn;
            Other.SaveTime = SaveTime;

            Other.ZoneID = ZoneID;
            Other.DeathReason = DeathReason;

            Other.GenotypeName = GenotypeName;
            Other.SubtypeName = SubtypeName;
            Other.Blueprint = Blueprint;

            Other.BonesSpec = new(BonesSpec);

            Other.Downloaded = Downloaded;

            Other.BonesStats = new(BonesStats);

            Other._Size = _Size;

            Other.CharIconSwapped = CharIconSwapped;
            Other.OriginalCharIcon = OriginalCharIcon;

            Other.ModsEnabled = null;
            Other.EnabledMods = new(EnabledMods);

            return Other;
        }

        public SaveBonesJSON CloneForUpload(
            out BonesSpec BonesSpec,
            out BonesStats BonesStats,
            out ModRecordSet EnabledMods
            )
        {
            var bonesJSON = CopyTo(new());
            BonesSpec = bonesJSON.BonesSpec;
            BonesStats = bonesJSON.BonesStats;
            EnabledMods = bonesJSON.EnabledMods;

            BonesSpec.ID = bonesJSON.BonesID;
            BonesStats.ID = bonesJSON.BonesID;
            EnabledMods.ID = bonesJSON.BonesID;

            bonesJSON.BonesSpec = null;
            bonesJSON.BonesStats = null;
            bonesJSON.EnabledMods = null;

            bonesJSON._Size = null;

            return bonesJSON;
        }

        public SaveBonesJSON Clone()
            => CopyTo(new())
            ;

        public void Dispose()
        {
            ID = null;

            IsYou = null;
            Handle = null;

            InfoVersion = 0;
            SaveVersion = 0;
            GameVersion = null;
            ModVersion = null;
            SaveTimeValue = 0L;

            Name = null;
            Level = 0;
            GenoSubType = null;
            GameMode = null;
            CharIcon = null;
            FColor = default;
            DColor = default;
            Location = null;
            InGameTime = null;
            Turn = 0;
            SaveTime = null;

            ZoneID = null;
            DeathReason = null;

            GenotypeName = null;
            SubtypeName = null;
            Blueprint = null;

            BonesSpec = null;

            Downloaded = null;

            BonesStats = null;

            _Size = null;

            CharIconSwapped = false;
            OriginalCharIcon = null;

            ModsEnabled = null;
            EnabledMods = null;
        }
    }
}
