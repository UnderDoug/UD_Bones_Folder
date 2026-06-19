using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Newtonsoft.Json;

using UD_Bones_Folder.Mod.Serialization;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptIn)]
    [JsonConverter(typeof(SetArrayConverter))]
    [Serializable]
    public class BonesStatSet : CoalescibleSet<BonesStat>
    {
        public class SetArrayConverter : JsonConverter<BonesStatSet>
        {
            public override BonesStatSet ReadJson(JsonReader reader, Type objectType, BonesStatSet existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.StartArray)
                {
                    var deserialized = serializer.Deserialize<BonesStat[]>(reader);
                    //Utils.Log($"{nameof(BonesStatSet)}.{nameof(SetArrayConverter)}.{nameof(ReadJson)}, {nameof(deserialized)}: {deserialized?.Length ?? -1}");

                    return new(deserialized);
                }

                return new();
            }

            public override void WriteJson(JsonWriter writer, BonesStatSet value, JsonSerializer serializer)
            {
                //Utils.Log($"{nameof(BonesStatSet)}.{nameof(SetArrayConverter)}.{nameof(WriteJson)}, {nameof(value)}: {(value == null ? "null" : "not null")}");
                serializer.Serialize(writer, (value ?? new()).ToArray());
            }
        }

        [Serializable]
        public class BonesStatEqualityComparer : CompositeEqualityComparer<BonesStat>
        {
            public static BonesStatEqualityComparer DefaultComparer => new();

            public BonesStatEqualityComparer()
                : base()
            { }

            public override bool Equals(BonesStat x, BonesStat y)
            {
                if (x == null
                    || y == null)
                    return (x == null) == (y == null);

                return x.SameAs(y);
            }

            public override int GetHashCode(BonesStat obj)
                => obj?.GetHashCode() ?? 0
                ;
        }

        public BonesStatSet()
            : base(BonesStatEqualityComparer.DefaultComparer)
        { }

        public BonesStatSet(IEnumerable<BonesStat> Source)
            : base(Source, BonesStatEqualityComparer.DefaultComparer)
        { }

        protected  BonesStatSet(IEnumerable<BonesStat> Source, CompositeEqualityComparer<BonesStat> EqualityComparer, Coalescer<BonesStat> Coalescer)
            : base(Source, EqualityComparer ?? BonesStatEqualityComparer.DefaultComparer, Coalescer ?? Coalescer<BonesStat>.Default)
        { }

        public BonesStatSet Clone(CompositeEqualityComparer<BonesStat> EqualityComparer = null, Coalescer<BonesStat> Coalescer = null)
            => new(this, EqualityComparer ?? GetEqualityComparer(), Coalescer ?? GetCoalescer())
            ;

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
            => GetStatTotal() is int statTotal
                && statTotal != 0
            ? GetStatValue(OsseousAshID) / statTotal
            : 0
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
