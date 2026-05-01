using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class BonesStat
    {
        public string OsseousAshID;
        public int Value;

        public BonesStat()
        { }

        public BonesStat(
            string OsseousAshID,
            int Value
        ) : this()
        {
            this.OsseousAshID = OsseousAshID;
            this.Value = Value;
        }

        public BonesStat(
            Guid OsseousAshID,
            int Value
        ) : this(OsseousAshID.ToString(), Value)
        { }

        public bool SameAs(BonesStat Other)
            => OsseousAshID == Other?.OsseousAshID
            ;

        public BonesStat Increment()
        {
            Value++;
            return this;
        }

        public override string ToString()
            => $"\"{nameof(OsseousAshID)}\": \"{OsseousAshID}\", \"{nameof(Value)}\": \"{Value}\"";

        public static implicit operator KeyValuePair<string, int>(BonesStat BonesStat)
            => new(BonesStat?.OsseousAshID, BonesStat?.Value ?? 0)
            ;

        public static implicit operator BonesStat(KeyValuePair<string, int> Pair)
            => new(Pair.Key, Pair.Value)
            ;
    }
}
