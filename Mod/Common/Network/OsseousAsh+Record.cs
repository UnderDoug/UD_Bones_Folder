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
    public static partial class OsseousAsh
    {
        [JsonObject(MemberSerialization.OptOut)]
        [Serializable]
        public class Record : IDisposable
        {
            public string BonesID;
            public SaveBonesJSON SaveBonesJSON;
            public int Size;

            [JsonIgnore]
            public double SizeInKb => Size / 1000;

            [JsonIgnore]
            public double SizeInMb => Size / 1000000;

            public Record(
                string BonesID,
                SaveBonesJSON SaveBonesJSON,
                byte[] SavGz
                )
            {
                this.BonesID = BonesID;
                this.SaveBonesJSON = SaveBonesJSON;
                Size = Buffer.ByteLength(SavGz);
            }

            public override string ToString()
            {
                return $"{nameof(BonesID)}: {BonesID}, " +
                    $"{nameof(SaveBonesJSON)}: {(SaveBonesJSON != null ? "not " : null)}null, " +
                    $"{nameof(Size)}: {Size.Things(typeof(byte).Name)}";
            }

            public void Dispose()
            {
                BonesID = null;
                SaveBonesJSON = null;
                Size = 0;
            }
        }
    }
}
