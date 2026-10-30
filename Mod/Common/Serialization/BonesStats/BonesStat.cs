using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class BonesStat : IDisposable
    {
        public string UserID;
        public int Value;

        public BonesStat()
        { }

        public BonesStat(
            string UserID,
            int Value
        ) : this()
        {
            this.UserID = UserID;
            this.Value = Value;
        }

        public BonesStat(
            Guid UserID,
            int Value
        ) : this(UserID.ToString(), Value)
        { }

        public BonesStat Clone()
            => new()
            {
                UserID = UserID,
                Value = Value,
            };


        public bool SameAs(BonesStat Other)
            => UserID == Other?.UserID
            ;

        public BonesStat Increment()
        {
            Value++;
            return this;
        }

        public override string ToString()
            => $"\"{nameof(UserID)}\": \"{UserID}\", \"{nameof(Value)}\": \"{Value}\"";

        public void Dispose()
        {
            UserID = null;
            Value = 0;
        }

        public static implicit operator KeyValuePair<string, int>(BonesStat BonesStat)
            => new(BonesStat?.UserID, BonesStat?.Value ?? 0)
            ;

        public static implicit operator BonesStat(KeyValuePair<string, int> Pair)
            => new(Pair.Key, Pair.Value)
            ;
    }
}
