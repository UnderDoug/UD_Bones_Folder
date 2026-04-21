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

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class OsseousAshRecord
    {
        [JsonObject(MemberSerialization.OptOut)]
        [Serializable]
        public class SavGzJSON
        {
            [JsonProperty("type")]
            public string Type { get; set; } = "Buffer";

            [JsonProperty("data")]
            public byte[] Data { get; set; } = null;

            public SavGzJSON()
            { }

            public SavGzJSON(byte[] Data)
                : this()
            {
                this.Data = Data;
            }

            public static explicit operator byte[](SavGzJSON SavGzJSON)
                => SavGzJSON?.Data
                ;

            public static explicit operator SavGzJSON(byte[] Buffer)
                => new(Buffer)
                ;
        }

        public string BonesID;

        public SaveBonesJSON SaveBonesJSON;

        //public SavGzJSON SavGz;
        public byte[] SavGz;

        public OsseousAshRecord(
            string BonesID,
            SaveBonesJSON SaveBonesJSON,
            byte[] SavGz
            )
        {
            this.BonesID = BonesID;
            this.SaveBonesJSON = SaveBonesJSON;
            //this.SavGz = (SavGzJSON)SavGz;
            this.SavGz = SavGz;
        }

        public override string ToString()
        {
            /*return $"{nameof(BonesID)}: {BonesID}, " +
                $"{nameof(SaveBonesJSON)}: {(SaveBonesJSON != null ? "not " : null)}null, " +
                $"{nameof(SavGz)}: {((int)(SavGz?.Data?.LongLength ?? 0L)).Things(typeof(byte).Name)}";*/
            return $"{nameof(BonesID)}: {BonesID}, " +
                $"{nameof(SaveBonesJSON)}: {(SaveBonesJSON != null ? "not " : null)}null, " +
                $"{nameof(SavGz)}: {(SavGz?.Length ?? 0).Things(typeof(byte).Name)}";
        }
    }
}
