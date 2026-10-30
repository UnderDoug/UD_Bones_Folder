using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Platform.IO;

using UnityEngine;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.Const;

using GameObject = XRL.World.GameObject;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class BonesRecord : IDisposable
    {
        public Guid UserID;
        public string BonesID;
        public SaveBonesJSON SaveBonesJSON;
        public BonesSpec BonesSpec;
        public BonesStats BonesStats;
        public ModRecordCollection ModConfig;
        public int Size;

        [JsonIgnore]
        public double SizeInKb => Size / 1000;

        [JsonIgnore]
        public double SizeInMb => Size / 1000000;

        public BonesRecord()
            : base()
        { }

        public BonesRecord(
            Guid UserID,
            string BonesID,
            SaveBonesJSON SaveBonesJSON,
            BonesSpec BonesSpec,
            BonesStats BonesStats,
            ModRecordCollection ModConfig,
            byte[] SavGz
            )
            : this()
        {
            this.UserID = UserID;
            this.BonesID = BonesID;
            this.SaveBonesJSON = SaveBonesJSON;
            this.BonesSpec = BonesSpec;
            this.BonesStats = BonesStats;
            this.ModConfig = ModConfig;
            Size = Buffer.ByteLength(SavGz);
        }

        public BonesRecord(
            Guid UserID,
            string BonesID,
            SaveBonesJSON SaveBonesJSON,
            BonesSpec BonesSpec,
            byte[] SavGz
            )
            : this(
                  UserID: UserID,
                  BonesID: BonesID,
                  SaveBonesJSON: SaveBonesJSON,
                  BonesSpec: BonesSpec,
                  BonesStats: null,
                  ModConfig: null,
                  SavGz: SavGz)
        { }

        public override string ToString()
            => $"{nameof(BonesID)}: {BonesID}, " +
            $"{nameof(SaveBonesJSON)}: {(SaveBonesJSON != null ? "not " : null)}null, " +
            $"{nameof(BonesSpec)}: {(BonesSpec != null ? "not " : null)}null, " +
            $"{nameof(BonesStats)}: {(BonesStats != null ? "not " : null)}null, " +
            $"{nameof(ModConfig)}: {(ModConfig != null ? "not " : null)}null, " +
            $"{nameof(Size)}: {Size.Things(typeof(byte).Name)}"
            ;

        public void Dispose()
        {
            UserID = default;
            BonesID = null;
            SaveBonesJSON?.Dispose();
            SaveBonesJSON = null;
            BonesSpec?.Dispose();
            BonesSpec = null;
            BonesStats?.Dispose();
            BonesStats = null;
            ModConfig?.Dispose();
            ModConfig = null;
            Size = 0;
        }
    }
}
