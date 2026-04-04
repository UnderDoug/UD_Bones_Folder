using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Platform.IO;

using Qud.API;

using XRL.World;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class SaveBonesJSON : SaveGameJSON
    {
        [Serializable]
        public class BonesRender : Renderable
        {
            public bool HFlip;

            public BonesRender()
                : base()
            {
                HFlip = true;
            }

            public BonesRender(
                string Tile,
                char FColor,
                char DColor
                )
                : base(
                      Tile: Tile,
                      TileColor: $"&{FColor}",
                      DetailColor: DColor)
            {
                HFlip = true;
            }

            public BonesRender(
                string Tile,
                char FColor,
                char DColor,
                bool HFlip
                )
                : this(
                      Tile: Tile,
                      FColor: FColor,
                      DColor: DColor)
            {
                this.HFlip = HFlip;
            }

            public BonesRender(
                SaveBonesJSON BonesJSON,
                bool HFlip = true
                )
                : this(
                      Tile: BonesJSON.CharIcon,
                      FColor: BonesJSON.FColor,
                      DColor: BonesJSON.DColor,
                      HFlip: HFlip)
            { }

            public BonesRender(
                IRenderable Source,
                bool HFlip = true
                )
                : base(Source)
            {
                this.HFlip = HFlip;
            }

            public override bool getHFlip()
                => HFlip;
        }

        public string ModVersion;
        public long SaveTimeValue;

        public string ZoneID;
        public string DeathReason;

        public string GenotypeName;
        public string SubtypeName;
        public string Blueprint;

        public string ZoneTerrainType;
        public int ZoneTier;
        public string ZoneRegion;

        public string Pending;

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
                    Name = "Corrupt info file".Colored("R"),
                    Size = $"Total size: {GetDirectorySize(DirPath) / 1000}kb",
                    Info = "",
                    Directory = DirPath
                };
            }
            DateTime saveTimeValue;
            try
            {
                saveTimeValue = new DateTime(json.SaveTimeValue).ToLocalTime();
            }
            catch //(Exception x)
            {
                // Utils.Error(Utils.CallChain(nameof(SaveBonesJSON), nameof(ReadSaveBonesJson)), x);
                saveTimeValue = new DateTime(2026, 04, 01, 11, 59, 59, DateTimeKind.Local);
            }

            var saveBonesInfo = new SaveBonesInfo
            {
                json = json,
                Directory = DirPath,
                Size = $"Total size: {GetDirectorySize(DirPath) / 1000}kb",
                ID = json.ID,
                Version = json.GameVersion,
                Name = json.Name,
                Description = $"Level {json.Level} {json.GenoSubType}",// [{json.GameMode}]",
                Info = $"{json.Location}, {json.InGameTime} turn {json.Turn}",
                SaveTime = json.SaveTime,
                ModsEnabled = json.ModsEnabled,

                FileName = FilePath,
                SaveTimeValue = saveTimeValue,

                ModVersion = json.ModVersion,

                ZoneID = json.ZoneID,
                DeathReason = json.DeathReason,

                GenotypeName = json.GenotypeName,
                SubtypeName = json.SubtypeName,

                ZoneTerrainType = json.ZoneTerrainType,
                ZoneTier = json.ZoneTier,
                ZoneRegion = json.ZoneRegion,
            };
            if (json.SaveVersion < 395
                || json.SaveVersion > 400)
            {
                string olderVersionString = $"Older Version ({json.GameVersion})".Colored("R");
                saveBonesInfo.Name = $"{olderVersionString} {saveBonesInfo.Name}";
            }

            return saveBonesInfo;
        }

        public BonesRender GetRender()
            => new(this, true)
            ;
    }
}
