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
        public string BonesID;

        public SaveBonesJSON SaveBonesJSON;

        public byte[] SavGz;

        public OsseousAshRecord(
            string BonesID,
            SaveBonesJSON SaveBonesJSON,
            byte[] Buffer)
        {
            this.BonesID = BonesID;
            this.SaveBonesJSON = SaveBonesJSON;
            SavGz = Buffer;
        }
    }
}
