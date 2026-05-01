using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptIn)]
    [JsonConverter(typeof(SetArrayConverter))]
    [Serializable]
    public class BonesStatSet : HashSet<BonesStat>
    {
        public class SetArrayConverter : JsonConverter<BonesStatSet>
        {
            public override BonesStatSet ReadJson(JsonReader reader, Type objectType, BonesStatSet existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.StartArray)
                    return new(serializer.Deserialize<BonesStat[]>(reader));

                return new();
            }

            public override void WriteJson(JsonWriter writer, BonesStatSet value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, (value ?? new()).ToArray());
            }
        }

        public class BonesStatEqualityComparer : IEqualityComparer<BonesStat>
        {
            public static BonesStatEqualityComparer Default => new();

            public bool Equals(BonesStat x, BonesStat y)
            {
                if (x == null
                    || y == null)
                    return (x == null) == (y == null);

                return x.SameAs(y);
            }

            public int GetHashCode(BonesStat obj)
                => obj?.GetHashCode() ?? 0
                ;
        }

        [JsonObject(MemberSerialization.OptOut)]
        [Serializable]
        protected class BonesStatSetJSON
        {
            public BonesStat[] Stats;

            public BonesStatSetJSON()
            { }

            public BonesStatSetJSON(BonesStatSet Source)
                : this()
            {
                Source.RemoveWhere(h => h == null);
                Stats = Source.ToArray();
            }

            public BonesStatSet FromJSON()
                => new(Stats)
                ;
        }

        public BonesStatSet()
            : base(BonesStatEqualityComparer.Default)
        { }

        public BonesStatSet(IEnumerable<BonesStat> Source)
            : base(Source, BonesStatEqualityComparer.Default)
        { }

        public BonesStat GetStat(Guid OsseousAshID)
            => this.FirstOrDefault(stat => stat.OsseousAshID == OsseousAshID.ToString())
            ;

        public bool TryGetStat(Guid OsseousAshID, out BonesStat Stat)
            => (Stat = GetStat(OsseousAshID)) != null
            ;

        public bool IncrementStat(Guid OsseousAshID)
        {
            if (TryGetStat(OsseousAshID, out var stat)
                || Add(stat = new(OsseousAshID, 0)))
            {
                stat.Increment();
                return true;
            }
            return false;
        }

        public int GetStatValue(Guid OsseousAshID)
            => GetStat(OsseousAshID)?.Value
            ?? 0
            ;

        public int GetStatTotal()
            => this.Aggregate(0, (a, n) => a + n.Value)
            ;

        public double GetPercentOfStat(Guid OsseousAshID)
            => GetStatValue(OsseousAshID) / GetStatTotal()
            ;

        public IEnumerable<string> GetAllIDs(Predicate<BonesStat> Where = null)
        {
            foreach (var bonesStat in this)
                if (Where?.Invoke(bonesStat) is not false)
                    yield return bonesStat.OsseousAshID;
        }

        public bool HasValue(Guid OsseousAshID)
            => this.Any(stat => stat.OsseousAshID == OsseousAshID.ToString())
            ;
    }
}
