using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FuzzySharp;

using Newtonsoft.Json;

using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.Collections;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptIn)]
    [HasModSensitiveStaticCache]
    [Serializable]
    public class BlueprintSpec : IComposite, IDisposable
    {
        [Serializable]
        public class SimilarityRecord : IComposite, IDisposable
        {
            [Serializable]
            public class EqualityComparer
                : CompositeEqualityComparer<SimilarityRecord>
                , IComposite
                , IDisposable
            {
                public override bool WantFieldReflection => false;

                protected bool Strict;

                public EqualityComparer()
                { }

                public EqualityComparer(bool Strict)
                    : this()
                {
                    this.Strict = Strict;
                }

                public override void Write(SerializationWriter Writer)
                {
                    Writer.Write(Strict);
                }

                public override void Read(SerializationReader Reader)
                {
                    Strict = Reader.ReadBoolean();
                }

                public bool IsStrict()
                    => Strict
                    ;

                public override bool Equals(SimilarityRecord x, SimilarityRecord y)
                    => x is null
                        || y is null
                    ? (x is null) == (y is null)
                    : x.SameAs(y, Strict)
                    ;

                public override int GetHashCode(SimilarityRecord obj)
                    => obj?.ToString()?.GetHashCode()
                    ?? 0
                    ;

                public void Dispose()
                {
                    Strict = default;
                }
            }

            [Serializable]
            public class Comparer
                : Comparer<SimilarityRecord>
                , IComposite
                , IDisposable
            {
                public virtual bool WantFieldReflection => false;

                protected bool LeastSimilarFirst;

                protected int Multi => GetMulti(LeastSimilarFirst);

                public Comparer()
                { }

                public Comparer(bool LeastSimilarFirst)
                    : this()
                {
                    this.LeastSimilarFirst = LeastSimilarFirst;
                }

                public virtual void Write(SerializationWriter Writer)
                {
                    Writer.Write(LeastSimilarFirst);
                }

                public virtual void Read(SerializationReader Reader)
                {
                    LeastSimilarFirst = Reader.ReadBoolean();
                }

                public bool IsLeastSimilarFirst()
                    => LeastSimilarFirst
                    ;

                protected static int GetMulti(bool LeastSimilarFirst)
                    => LeastSimilarFirst
                    ? 1
                    : -1
                    ;

                protected static int FilterValue(int Value, int Multi)
                    => Value * Multi
                    ;

                protected int FilterValue(int Value)
                    => FilterValue(Value, Multi)
                    ;

                public static int Compare(SimilarityRecord x, SimilarityRecord y, bool LeastSimilarFirst)
                    => x is null
                        || y is null
                    ? (x is null).CompareTo(y is null)
                    : FilterValue(x.Similarity.CompareTo(y.Similarity), GetMulti(LeastSimilarFirst))
                    ;

                public override int Compare(SimilarityRecord x, SimilarityRecord y)
                    => Compare(x, y, LeastSimilarFirst)
                    ;

                public void Dispose()
                {
                    LeastSimilarFirst = default;
                }
            }

            public static SimilarityRecord Empty => new SimilarityRecord
            {
                Other = null,
                Similarity = -1,
            };

            public BlueprintSpec Other;
            public int Similarity;

            public bool IsEmpty()
                => DebugString() == Empty.DebugString()
                ;

            public static string GetKey(BlueprintSpec BlueprintSpec)
                => BlueprintSpec?.Blueprint
                ?? "NULL"
                ;

            public override string ToString()
                => GetKey(Other)
                ;

            public string DebugString()
                => $"{this}@{Similarity}"
                ;

            public bool Matches(string Key)
                => (Key.IsNullOrEmpty() 
                    && IsEmpty())
                || Key == ToString()
                ;

            public bool SameAs(SimilarityRecord Other, bool Strict = false)
                => (!Strict
                    && Matches(Other?.ToString()))
                || DebugString() == Other?.DebugString();

            public bool Matches(BlueprintSpec Other)
                => Matches(Other.ToString())
                ;

            public static SimilarityRecord MakeFor(BlueprintSpec Primary, BlueprintSpec Other)
            {
                if (Primary == null
                    || Other == null)
                {
                    //Utils.Log($"{2.Indent()}{nameof(SimilarityRecord)}.{nameof(MakeFor)}({Primary?.Blueprint ?? "NO_PRIMARY"}, {Other?.Blueprint ?? "NO_OTHER"}): {nameof(Empty)}");
                    return Empty;
                }
                var record = new SimilarityRecord
                {
                    Other = Other,
                    Similarity = Primary.GetSimilartyTo(Other),
                };
                //Utils.Log($"{2.Indent()}{nameof(SimilarityRecord)}.{nameof(MakeFor)}({Primary?.Blueprint ?? "NO_PRIMARY"}, {Other?.Blueprint ?? "NO_OTHER"}): {record}");
                return record;
            }

            public static bool TryMakeFor(BlueprintSpec Primary, BlueprintSpec Other, out SimilarityRecord Result)
                => !(Result = MakeFor(Primary, Other)).IsEmpty()
                ;

            public static bool TryGetFor(BlueprintSpec Primary, BlueprintSpec Other, out SimilarityRecord Result)
            {
                //Utils.Log($"{2.Indent()}{nameof(SimilarityRecord)}.{nameof(TryGetFor)}({Primary?.Blueprint ?? "NO_PRIMARY"}, {Other?.Blueprint ?? "NO_OTHER"})");
                Result = Empty;
                if (Primary == null)
                    return false;

                string primaryKey = GetKey(Primary);
                string otherKey = GetKey(Other);

                SimilarityCache ??= new();
                if (!SimilarityCache.ContainsKey(primaryKey))
                    SimilarityCache[primaryKey] = new();

                var results = SimilarityCache[primaryKey];

                if (!results.ContainsKey(otherKey))
                    results[otherKey] = MakeFor(Primary, Other);

                Result = results[otherKey];

                return !Result.IsEmpty();
            }

            public static SimilarityRecord RentFor(BlueprintSpec Primary, BlueprintSpec Other)
            {
                if (TryGetFor(Primary, Other, out var Result))
                    return Result.Clone();

                return Empty;
            }

            public SimilarityRecord Clone()
                => new()
                {
                    Other = new(Other),
                    Similarity = Similarity,
                };

            public SimilarityRecord CopyFrom(SimilarityRecord Source)
            {
                Other = new(Source.Other);
                Similarity = Source.Similarity;
                return this;
            }

            public int GetWeight()
                => Math.Clamp(Similarity, MIN_SIMILARTY, MAX_SIMILARTY)
                ;

            public void Clear()
            {
                Other = null;
                Similarity = 0;
            }

            public void Dispose()
            {
                Clear();
            }

            public static implicit operator KeyValuePair<string, int>(SimilarityRecord Operand)
                => new(GetKey(Operand?.Other), Operand?.Similarity ?? MAX_SIMILARTY)
                ;
        }

        [Serializable]
        public class EqualityComparer
            : CompositeEqualityComparer<BlueprintSpec>
            , IComposite
            , IDisposable
        {
            public override bool WantFieldReflection => false;

            protected bool BlueprintOnly;

            public EqualityComparer()
            { }

            public EqualityComparer(bool BlueprintOnly)
                : this()
            {
                this.BlueprintOnly = BlueprintOnly;
            }

            public override void Write(SerializationWriter Writer)
            {
                Writer.Write(BlueprintOnly);
            }

            public override void Read(SerializationReader Reader)
            {
                BlueprintOnly = Reader.ReadBoolean();
            }

            public override bool Equals(BlueprintSpec x, BlueprintSpec y)
            {
                if (x is null
                    || y is null)
                    return (x is null) == (y is null);

                if (BlueprintOnly)
                    return x.Blueprint == y.Blueprint;

                return x.SameAs(y);
            }

            public override int GetHashCode(BlueprintSpec obj)
                => obj?.ToString()?.GetHashCode()
                ?? 0
                ;

            public void Dispose()
            {
                BlueprintOnly = default;
            }
        }

        [Serializable]
        public class Comparer
            : Comparer<BlueprintSpec>
            , IComposite
            , IDisposable
        {
            public virtual bool WantFieldReflection => false;

            protected static SimilarityRecord.Comparer DistanceComparer = BlueprintSpec.SimilarityComparer;

            protected BlueprintSpec Primary;
            protected bool LeastSimilarFirst;

            public Comparer(BlueprintSpec Primary)
            {
                this.Primary = Primary;
            }

            public Comparer(BlueprintSpec Primary, bool LeastSimilarFirst)
                : this(Primary)
            {
                this.LeastSimilarFirst = LeastSimilarFirst;
            }

            public virtual void Write(SerializationWriter Writer)
            {
                Writer.WriteComposite(Primary);
                Writer.Write(LeastSimilarFirst);
            }

            public virtual void Read(SerializationReader Reader)
            {
                Primary = Reader.ReadComposite<BlueprintSpec>();
                LeastSimilarFirst = Reader.ReadBoolean();
            }

            public BlueprintSpec GetPrimary()
                => Primary
                ;

            public bool IsLeastSimilarFirst()
                => LeastSimilarFirst
                ;

            public static int Compare(BlueprintSpec x, BlueprintSpec y, BlueprintSpec Primary, bool LeastSimilarFirst)
            {
                if (x is null
                    || y is null)
                    return (x is null).CompareTo(y is null);

                if (!SimilarityRecord.TryGetFor(Primary, x, out var xSpecDistance))
                    xSpecDistance = SimilarityRecord.Empty;

                if (!SimilarityRecord.TryGetFor(Primary, y, out var ySpecDistance))
                    ySpecDistance = SimilarityRecord.Empty;

                return SimilarityRecord.Comparer.Compare(xSpecDistance, ySpecDistance, LeastSimilarFirst);
            }

            public int Compare(BlueprintSpec x, BlueprintSpec y, BlueprintSpec Primary)
                => Compare(x, y, Primary, LeastSimilarFirst)
                ;

            public int Compare(BlueprintSpec x, BlueprintSpec y, bool LeastSimilarFirst)
                => Compare(x, y, Primary, LeastSimilarFirst)
                ;

            public override int Compare(BlueprintSpec x, BlueprintSpec y)
                => Compare(x, y, Primary, LeastSimilarFirst)
                ;

            public void Dispose()
            {
                LeastSimilarFirst = default;
            }
        }

        public const string BASE_BLUEPRINT = "PhysicalObject";

        public static SimilarityRecord.EqualityComparer DistanceEqualityComparer => new(Strict: false);

        public static SimilarityRecord.Comparer SimilarityComparer => new(LeastSimilarFirst: false);

        public static EqualityComparer DefaultEqualityComparer => new(BlueprintOnly: false);

        public class StringSetArrayConverter : JsonConverter<StringSet>
        {
            public override StringSet ReadJson(JsonReader reader, Type objectType, StringSet existingValue, bool hasExistingValue, JsonSerializer serializer)
                => reader.TokenType == JsonToken.StartArray
                ? new(serializer.Deserialize<string[]>(reader))
                : new()
                ;

            public override void WriteJson(JsonWriter writer, StringSet value, JsonSerializer serializer)
                => serializer.Serialize(writer, (value ?? new()).ToArray())
                ;
        }

        public class ListArrayConverter<T> : JsonConverter<List<T>>
        {
            public override List<T> ReadJson(JsonReader reader, Type objectType, List<T> existingValue, bool hasExistingValue, JsonSerializer serializer)
                => reader.TokenType == JsonToken.StartArray
                ? new(serializer.Deserialize<T[]>(reader))
                : new()
                ;

            public override void WriteJson(JsonWriter writer, List<T> value, JsonSerializer serializer)
                => serializer.Serialize(writer, (value ?? new()).ToArray())
                ;
        }

        public static string TierProp => GetPropName(nameof(Tier));
        public static string TechTierProp => GetPropName(nameof(TechTier));
        public static string UsesSlotsProp => GetPropName(nameof(GameObject.UsesSlots));
        public static string SpeciesProp => GetPropName(nameof(Species));
        public static string ClassProp => GetPropName(nameof(Class));
        public static string RoleProp => GetPropName(nameof(Role));
        public static string PaintedWallProp => GetPropName(nameof(PaintedWall));
        public static string PaintedFenceProp => GetPropName(nameof(PaintedFence));
        public static string ImprovisedWeaponProp => GetPropName("ImprovisedWeapon");

        public static string TRUE => $"{true}";
        public static string FALSE => $"{false}";

        public const int MIN_SIMILARTY = 1;
        public const int MAX_SIMILARTY = 9999;

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        public static Dictionary<string, Dictionary<string, SimilarityRecord>> SimilarityCache = new();

        public string DebugName;

        [JsonProperty]
        public string Blueprint;

        [JsonConverter(typeof(ListArrayConverter<string>))]
        [JsonProperty]
        public List<string> BlueprintTree;

        [JsonProperty]
        public string Category;

        [JsonProperty]
        public int? Tier;

        [JsonProperty]
        public int? TechTier;

        // [NonSerialized]
        [JsonConverter(typeof(StringSetArrayConverter))]
        public StringSet WeaponSkills;

        // [NonSerialized]
        [JsonConverter(typeof(StringSetArrayConverter))]
        public StringSet EquipmentSlots;

        [JsonProperty]
        public string Species;

        [JsonProperty]
        public string Class;

        [JsonProperty]
        public string Role;

        [JsonProperty]
        public string PaintedWall;

        [JsonProperty]
        public string PaintedFence;

        public bool IsEmpty
            => Category == null
            && Tier == null
            && TechTier == null
            && WeaponSkills == null
            && EquipmentSlots == null
            && Species == null
            && Class == null
            && Role == null
            && PaintedWall == null
            && PaintedFence == null
            ;

        public bool IsWildCard
            => IsFieldWildCard(ref Category)
            && IsFieldWildCard(ref Tier)
            && IsFieldWildCard(ref TechTier)
            && IsFieldWildCard(ref WeaponSkills)
            && IsFieldWildCard(ref EquipmentSlots)
            && IsFieldWildCard(ref Species)
            && IsFieldWildCard(ref Class)
            && IsFieldWildCard(ref Role)
            && IsFieldWildCard(ref PaintedWall)
            && IsFieldWildCard(ref PaintedFence)
            ;

        public bool IsAll
            => !Category.IsNullOrEmpty()
            && !Tier.IsNullOrDefault()
            && !TechTier.IsNullOrDefault()
            && !WeaponSkills.IsNullOrEmpty()
            && !EquipmentSlots.IsNullOrEmpty()
            && !Species.IsNullOrEmpty()
            && !Class.IsNullOrEmpty()
            && !Role.IsNullOrEmpty()
            && !PaintedWall.IsNullOrEmpty()
            && !PaintedFence.IsNullOrEmpty()
            ;

        public BlueprintSpec()
        { }

        public BlueprintSpec(GameObject GameObject)
            : this()
        {
            if (GameObject == null)
                throw new ArgumentNullException(nameof(GameObject));

            DebugName = GameObject.DebugName;
            Blueprint = GameObject.Blueprint;

            BlueprintTree = GetBlueprintTree(GameObject);

            Category = GameObject?.Physics?.Category;

            try
            {
                if (GameObjectFactory.Factory.HasBlueprint(GameObject.Blueprint))
                {
                    Tier = GameObject.GetTier();
                    TechTier = GameObject.GetTechTier();
                }
                else
                {
                    SetTierFromProps(GameObject);
                    SetTechTierFromProps(GameObject);
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} Tiers", x);

                if (Tier.IsNullOrDefault())
                    SetTierFromProps(GameObject);

                if (TechTier.IsNullOrDefault())
                    SetTechTierFromProps(GameObject);
            }

            try
            {
                foreach (var slot in GameObject.UsesSlots.CachedCommaExpansion().IteratorSafe())
                {
                    EquipmentSlots ??= new();
                    EquipmentSlots.Add(slot);
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(GameObject)}.{nameof(GameObject.UsesSlots)}", x);
            }

            try
            {
                if (GameObject.TryGetPart(out CyberneticsBaseItem cyberneticsBaseItem))
                {
                    foreach (var slot in cyberneticsBaseItem.Slots.CachedCommaExpansion().IteratorSafe())
                    {
                        EquipmentSlots ??= new();
                        EquipmentSlots.Add(slot);
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(CyberneticsBaseItem)}", x);
            }

            try
            {
                if (GameObject.TryGetPart(out MissileWeapon missileWeapon))
                {
                    WeaponSkills ??= new();
                    WeaponSkills.Add(missileWeapon.Skill);

                    foreach (var slot in missileWeapon.SlotType.CachedCommaExpansion().IteratorSafe())
                    {
                        EquipmentSlots ??= new();
                        EquipmentSlots.Add(slot);
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(MissileWeapon)}", x);
            }

            try
            {
                if (GameObject.TryGetPart(out MeleeWeapon meleeWeapon)
                    && !meleeWeapon.IsImprovisedWeapon())
                {
                    WeaponSkills ??= new();
                    WeaponSkills.Add(meleeWeapon.Skill);

                    foreach (var slot in meleeWeapon.Slot.CachedCommaExpansion().IteratorSafe())
                    {
                        EquipmentSlots ??= new();
                        EquipmentSlots.Add(slot);
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(MeleeWeapon)}", x);
            }

            try
            {
                if (GameObject.TryGetPart(out Armor armor))
                {
                    foreach (var slot in armor.WornOn.CachedCommaExpansion().IteratorSafe())
                    {
                        EquipmentSlots ??= new();
                        EquipmentSlots.Add(slot);
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Armor)}", x);
            }

            try
            {
                if (GameObjectFactory.Factory.HasBlueprint(GameObject.Blueprint))
                    Species = GameObject.GetSpecies();
                else
                    SetSpeciesFromProps(GameObject);
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Species)}", x);

                if (Species.IsNullOrEmpty())
                    SetSpeciesFromProps(GameObject);
            }

            try
            {
                if (GameObjectFactory.Factory.HasBlueprint(GameObject.Blueprint))
                {
                    Class = GameObject.GetClass();

                    Role = GameObject.GetPropertyOrTag(nameof(Role));

                    PaintedWall = GameObject.GetPropertyOrTag(nameof(PaintedWall));

                    PaintedFence = GameObject.GetPropertyOrTag(nameof(PaintedFence));
                }
                else
                {
                    SetClassFromProps(GameObject);

                    SetRoleFromProps(GameObject);

                    SetPaintedWallFromProps(GameObject);

                    SetPaintedFenceFromProps(GameObject);
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Class)}, {nameof(Role)}, {nameof(PaintedWall)}, {nameof(PaintedFence)}", x);

                if (Class.IsNullOrEmpty())
                    SetClassFromProps(GameObject);

                if (Role.IsNullOrEmpty())
                    SetRoleFromProps(GameObject);

                if (PaintedWall.IsNullOrEmpty())
                    SetPaintedWallFromProps(GameObject);

                if (PaintedFence.IsNullOrEmpty())
                    SetPaintedFenceFromProps(GameObject);
            }
        }

        public BlueprintSpec(GameObjectBlueprint Model)
            : this()
        {
            if (Model == null)
                throw new ArgumentNullException(nameof(Model));

            DebugName = $"{Model.Name} ({nameof(GameObjectBlueprint)})";
            Blueprint = Model.Name;

            BlueprintTree = GetBlueprintTree(Model);

            Category = Model.GetPartParameter<string>(nameof(Physics), nameof(Physics.Category));

            try
            {
                if (Model.GetTag(nameof(Tier), null) is string tierString
                    && int.TryParse(tierString, out int tier))
                    Tier = tier;

                if (Model.GetTag(nameof(TechTier), null) is string techTierString
                    && int.TryParse(techTierString, out int techTier))
                    TechTier = techTier;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} Tiers", x);
            }

            try
            {
                foreach (var slot in Model.GetStringPropertyOrTag(nameof(GameObject.UsesSlots)).CachedCommaExpansion().IteratorSafe())
                {
                    EquipmentSlots ??= new();
                    EquipmentSlots.Add(slot);
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(GameObject.UsesSlots)}", x);
            }

            try
            {
                if (Model.TryGetPartParameter(nameof(CyberneticsBaseItem), nameof(CyberneticsBaseItem.Slots), out string cyberneticsSlots))
                {
                    foreach (var slot in cyberneticsSlots.CachedCommaExpansion().IteratorSafe())
                    {
                        EquipmentSlots ??= new();
                        EquipmentSlots.Add(slot);
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(CyberneticsBaseItem)}", x);
            }

            try
            {
                if (Model.TryGetPartBlueprint<MissileWeapon>(out var missileWeaponModel))
                {
                    if (missileWeaponModel.TryGetParameter(nameof(MissileWeapon.Skill), out string missileWeaponSkill))
                    {
                        WeaponSkills ??= new();
                        WeaponSkills.Add(missileWeaponSkill);
                    }
                    if (missileWeaponModel.TryGetParameter(nameof(MissileWeapon.SlotType), out string missileWeaponSlotType))
                    {
                        foreach (var slot in missileWeaponSlotType.CachedCommaExpansion().IteratorSafe())
                        {
                            EquipmentSlots ??= new();
                            EquipmentSlots.Add(slot);
                        }
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(CyberneticsBaseItem)}", x);
            }

            try
            {
                if (Model.TryGetPartBlueprint<MeleeWeapon>(out var meleeWeaponModel))
                {
                    if ((meleeWeaponModel.Reflector?.GetInstance() as MeleeWeapon
                        ?? Activator.CreateInstance(meleeWeaponModel.T) as MeleeWeapon) is MeleeWeapon meleeWeapon)
                        meleeWeaponModel.InitializePartInstance(meleeWeapon);
                    else
                        meleeWeapon = null;

                    if (meleeWeapon?.IsImprovisedWeapon() is not true)
                    {
                        if (meleeWeaponModel.TryGetParameter(nameof(MeleeWeapon.Skill), out string meleeWeaponSkill))
                        {
                            WeaponSkills ??= new();
                            WeaponSkills.Add(meleeWeaponSkill);
                        }
                        if (meleeWeaponModel.TryGetParameter(nameof(MeleeWeapon.Slot), out string meleeWeaponSlot))
                        {
                            foreach (var slot in meleeWeaponSlot.CachedCommaExpansion().IteratorSafe())
                            {
                                EquipmentSlots ??= new();
                                EquipmentSlots.Add(slot);
                            }
                        }
                    }
                    meleeWeapon = null;
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(MeleeWeapon)}", x);
            }

            try
            {
                if (Model.TryGetPartBlueprint<Armor>(out var armorModel))
                {
                    if (armorModel.TryGetParameter(nameof(Armor.WornOn), out string armorWornOn))
                    {
                        foreach (var slot in armorWornOn.CachedCommaExpansion().IteratorSafe())
                        {
                            EquipmentSlots ??= new();
                            EquipmentSlots.Add(slot);
                        }
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Armor)}", x);
            }

            try
            {
                if (Model.TryGetStringPropertyOrTag(nameof(Species), out string speciesPropTag)
                    && !speciesPropTag.IsNullOrEmpty())
                {
                    Species = speciesPropTag;
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Species)}", x);
            }

            try
            {
                if (Model.TryGetStringPropertyOrTag(nameof(Class), out string classPropTag)
                    && !classPropTag.IsNullOrEmpty())
                {
                    Class = classPropTag;
                }
                if (Model.TryGetStringPropertyOrTag(nameof(Role), out string rolePropTag)
                    && !rolePropTag.IsNullOrEmpty())
                {
                    Role = rolePropTag;
                }
                if (Model.TryGetStringPropertyOrTag(nameof(PaintedWall), out string paintedWallPropTag)
                    && !paintedWallPropTag.IsNullOrEmpty())
                {
                    PaintedWall = paintedWallPropTag;
                }
                if (Model.TryGetStringPropertyOrTag(nameof(PaintedFence), out string paintedFencePropTag)
                    && !paintedFencePropTag.IsNullOrEmpty())
                {
                    PaintedFence = paintedFencePropTag;
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Class)},  {nameof(Role)}, {nameof(PaintedWall)}, {nameof(PaintedFence)}", x);
            }

            if (PaintedWall != null
                || PaintedFence != null)
            {
                Tier ??= 0;
                TechTier ??= 0;
                Species ??= string.Empty;
                Class ??= string.Empty;
                Role ??= string.Empty;
            }
            else
            {
                Species ??= Model.DisplayName();
                Class ??= Model.Name;
            }
        }

        public BlueprintSpec(BlueprintSpec Source)
        {
            if (Source == null)
                return;

            DebugName = Source.DebugName;

            Blueprint = Source.Blueprint;
            if (Source.BlueprintTree != null)
            {
                BlueprintTree = new();
                if (Source.BlueprintTree.Count > 0)
                    BlueprintTree.AddRange(Source.BlueprintTree);
            }

            Category = Source.Category;

            Tier = Source.Tier;
            TechTier = Source.TechTier;

            if (Source.WeaponSkills != null)
            {
                WeaponSkills = new();
                if (Source.WeaponSkills.Count > 0)
                    WeaponSkills.AddRange(Source.WeaponSkills);
            }

            if (Source.EquipmentSlots != null)
            {
                EquipmentSlots = new();
                if (Source.EquipmentSlots.Count > 0)
                    EquipmentSlots.AddRange(Source.EquipmentSlots);
            }

            Species = Source.Species;
            Class = Source.Class;
            Role = Source.Role;
            PaintedWall = Source.PaintedWall;
            PaintedFence = Source.PaintedFence;
        }

        #region Serialization

        public virtual void Write(SerializationWriter Writer)
        {
            /*Writer.WriteComposite(WeaponSkills);
            Writer.WriteComposite(EquipmentSlots);*/
        }

        public virtual void Read(SerializationReader Reader)
        {
            if (Reader.ModVersions.TryGetValue(Utils.ThisMod.ID, out XRL.Version modVersion)
                && modVersion < StringSet.AddedIn)
            {
                WeaponSkills = (StringSet)Reader.ReadStringHashSet();
                EquipmentSlots = (StringSet)Reader.ReadStringHashSet();
            }
        }

        #endregion

        public static string GetPropName(string FieldName)
            => !FieldName.IsNullOrEmpty()
            ? Utils.CallChain(nameof(UD_Bones_BlueprintSpec), FieldName)
            : null
            ;

        public static Comparer GetComparer(BlueprintSpec Primary)
            => new Comparer(
                Primary: Primary,
                LeastSimilarFirst: false)
            ;

        public static List<string> GetBlueprintTree(
            GameObjectBlueprint Model,
            string StopAfter = BASE_BLUEPRINT
            )
        {
            List<string> blueprintTree = new();
            var parentBlueprint = Model;
            while (parentBlueprint.Inherits != null)
            {
                string debugPostText = $"{nameof(GameObjectBlueprint)} in {Model.Name} inheritance tree, " +
                    $"{blueprintTree.Count.Things("blueprint")} in";

                try
                {
                    parentBlueprint = GameObjectFactory.Factory.GetBlueprintIfExists(parentBlueprint.Inherits);
                    if (parentBlueprint?.Name is not string blueprintName)
                    {
                        Utils.Warn($"Strange, nameless {debugPostText}");
                        break;
                    }
                    blueprintTree.Add(blueprintName);

                    if (blueprintName == StopAfter)
                        break;
                }
                catch (Exception x)
                {
                    Utils.Error($"Issue retreiving {debugPostText}", x);
                    break;
                }
            }
            return blueprintTree;
        }

        public static List<string> GetBlueprintTree(GameObject GameObject)
            => GameObject != null
                && GameObjectFactory.Factory.HasBlueprint(GameObject.Blueprint)
            ? GetBlueprintTree(GameObject.GetBlueprint())
            : null
            ;

        private void SetTierFromProps(GameObject GameObject)
        {
            Tier = 0;
            if (GameObject.HasIntProperty(nameof(Tier)))
                Tier = GameObject.GetIntProperty(nameof(Tier));
            if (GameObject.HasIntProperty(nameof(TierProp)))
                Tier = GameObject.GetIntProperty(nameof(TierProp));
        }

        private void SetTechTierFromProps(GameObject GameObject)
        {
            TechTier = 0;
            if (GameObject.HasIntProperty(nameof(TechTier)))
                TechTier = GameObject.GetIntProperty(nameof(TechTier));
            if (GameObject.HasIntProperty(nameof(TechTierProp)))
                TechTier = GameObject.GetIntProperty(nameof(TechTierProp));
        }

        private void SetSpeciesFromProps(GameObject GameObject)
        {
            Species = GameObject.GetDisplayName(AsIfKnown: true, NoConfusion: true, NoColor: true, Stripped: true, BaseOnly: true);
            if (GameObject.HasStringProperty(nameof(Species)))
                Species = GameObject.GetStringProperty(nameof(Species));
            if (GameObject.HasStringProperty(nameof(SpeciesProp)))
                Species = GameObject.GetStringProperty(nameof(SpeciesProp));
        }

        private void SetClassFromProps(GameObject GameObject)
        {
            Class = GameObject.Blueprint;
            if (GameObject.HasStringProperty(nameof(Class)))
                Class = GameObject.GetStringProperty(nameof(Class));
            if (GameObject.HasStringProperty(nameof(ClassProp)))
                Class = GameObject.GetStringProperty(nameof(ClassProp));
        }

        private void SetRoleFromProps(GameObject GameObject)
        {
            Role = "";
            if (GameObject.HasStringProperty(nameof(Role)))
                Role = GameObject.GetStringProperty(nameof(Role));
            if (GameObject.HasStringProperty(nameof(RoleProp)))
                Role = GameObject.GetStringProperty(nameof(RoleProp));
        }

        private void SetPaintedWallFromProps(GameObject GameObject)
        {
            PaintedWall = "";
            if (GameObject.HasStringProperty(nameof(PaintedWall)))
                PaintedWall = GameObject.GetStringProperty(nameof(PaintedWall));
            if (GameObject.HasStringProperty(nameof(PaintedWallProp)))
                PaintedWall = GameObject.GetStringProperty(nameof(PaintedWallProp));
        }

        private void SetPaintedFenceFromProps(GameObject GameObject)
        {
            PaintedFence = "";
            if (GameObject.HasStringProperty(nameof(PaintedFence)))
                PaintedFence = GameObject.GetStringProperty(nameof(PaintedFence));
            if (GameObject.HasStringProperty(nameof(PaintedFenceProp)))
                PaintedFence = GameObject.GetStringProperty(nameof(PaintedFenceProp));
        }

        public bool SameAs(BlueprintSpec Other)
        {
            if (Other == null)
                return false;

            if (Blueprint != Other.Blueprint)
                return false;

            if ((BlueprintTree?.Count ?? -1) != (Other.BlueprintTree?.Count ?? -1))
                return false;
            if (!BlueprintTree.IteratorSafe().All(s => Other.BlueprintTree.IteratorSafe().Contains(s))
                || !Other.BlueprintTree.IteratorSafe().All(s => BlueprintTree.IteratorSafe().Contains(s)))
                return false;

            if (Category != Other.Category)
                return false;

            if (Tier != Other.Tier)
                return false;

            if (TechTier != Other.TechTier)
                return false;

            if ((WeaponSkills?.Count ?? -1) != (Other.WeaponSkills?.Count ?? -1))
                return false;
            if (!WeaponSkills.IteratorSafe().All(s => Other.WeaponSkills.IteratorSafe().Contains(s))
                || !Other.WeaponSkills.IteratorSafe().All(s => WeaponSkills.IteratorSafe().Contains(s)))
                return false;

            if ((EquipmentSlots?.Count ?? -1) != (Other.EquipmentSlots?.Count ?? -1))
                return false;
            if (!EquipmentSlots.IteratorSafe().All(s => Other.EquipmentSlots.IteratorSafe().Contains(s))
                || !Other.EquipmentSlots.IteratorSafe().All(s => EquipmentSlots.IteratorSafe().Contains(s)))
                return false;

            if (Species != Other.Species)
                return false;

            if (Class != Other.Class)
                return false;

            if (Role != Other.Role)
                return false;

            if (PaintedWall != Other.PaintedWall)
                return false;

            if (PaintedFence != Other.PaintedFence)
                return false;

            return true;
        }

        public static BlueprintSpec CreateFrom(GameObjectBlueprint Blueprint)
        {
            string catchFlag = "Top";
            try
            {
                /*catchFlag = "Check Excluded";
                if (Blueprint.IsExcludedFromDynamicEncounters())
                    return null;*/

                catchFlag = "Check Base";
                if (Blueprint.IsBaseBlueprint())
                    return null;

                catchFlag = "Checking Texture";
                if (Blueprint.GetPart("Render") is not GamePartBlueprint renderPartBlueprint
                    || !renderPartBlueprint.TryGetParameter(nameof(Render.Tile), out string renderTile)
                    || !renderTile.IsTile())
                    return null;

                return new(Blueprint);
            }
            catch (Exception x)
            {
                Utils.Error($"Failed to create {nameof(BlueprintSpec)} for {Blueprint.NameOrMissing()} at {nameof(catchFlag)} {catchFlag}", x);
                return null;
            }
        }

        public static bool TryCreateFrom(GameObjectBlueprint Blueprint, out BlueprintSpec Result)
            => (Result = CreateFrom(Blueprint)) != null
            ;

        protected static bool IsFieldWildCard<T>(ref T Field)
        {
            if (Field == null)
                return false;

            if (Field is string stringField)
                return stringField == string.Empty;

            if (Field is Array arrayField)
                return arrayField.Length == 0;

            if (Field is ICollection collectionField)
                return collectionField.Count == 0;

            if (Field is IEnumerable enumerableField)
                return enumerableField.GetEnumerator() is IEnumerator enumerator
                    && enumerator.Current is not null;

            if (Field is int intField)
                return intField == 0;

            return false;
        }

        private static string GetDebugString<T>(ref T Field, Func<T, string> ToString = null)
        {
            if (Field is null)
                return "null";

            if (IsFieldWildCard(ref Field))
                return "*";

            return ToString != null
                ? ToString.Invoke(Field)
                : Field.ToString()
                ;
        }

        public IEnumerable<KeyValuePair<string, string>> GetDebugPairs()
        {
            yield return new(nameof(DebugName), DebugName ?? "NONE");
            yield return new(nameof(Blueprint), Blueprint ?? "NONE");

            yield return new(
                key: nameof(BlueprintTree),
                value: GetDebugString(
                    Field: ref BlueprintTree,
                    ToString: f => f.Aggregate((string)null, Utils.NewLineDelimitedAggregator))
                );

            yield return new(nameof(Category), GetDebugString(Field: ref Category));
            yield return new(nameof(Tier), GetDebugString(Field: ref Tier));
            yield return new(nameof(TechTier), GetDebugString(Field: ref TechTier));

            yield return new(
                key: nameof(WeaponSkills),
                value: GetDebugString(
                    Field: ref WeaponSkills,
                    ToString: f => f.Aggregate((string)null, Utils.NewLineDelimitedAggregator))
                );

            yield return new(
                key: nameof(EquipmentSlots),
                value: GetDebugString(
                    Field: ref EquipmentSlots,
                    ToString: f => f.Aggregate((string)null, Utils.NewLineDelimitedAggregator))
                );

            yield return new(nameof(Species), GetDebugString(Field: ref Species));
            yield return new(nameof(Class), GetDebugString(Field: ref Class));
            yield return new(nameof(Role), GetDebugString(Field: ref Role));
            yield return new(nameof(PaintedWall), GetDebugString(Field: ref PaintedWall));
            yield return new(nameof(PaintedFence), GetDebugString(Field: ref PaintedFence));
        }

        public IEnumerable<string> GetDebugLines(int Indent = 0, bool ForInternals = false)
        {
            if (ForInternals)
                Indent = 0;

            yield return $"{Indent.Indent()}{nameof(DebugName)}: {DebugName ?? "NONE"}";
            yield return $"{Indent.Indent()}{nameof(Blueprint)}: {Blueprint ?? "NONE"}";

            yield return $"{Indent.Indent()}{nameof(BlueprintTree)}: {GetDebugString(Field: ref BlueprintTree, ToString: f => null)}";
            foreach (var blueprint in BlueprintTree.IteratorSafe())
            {
                if (!ForInternals)
                    yield return $"{(Indent + 1).Indent()}: {blueprint}";
                else
                    yield return $"\n{blueprint}";
            }

            yield return $"{Indent.Indent()}{nameof(Category)}: {GetDebugString(Field: ref Category)}";
            yield return $"{Indent.Indent()}{nameof(Tier)}: {GetDebugString(Field: ref Tier)}";
            yield return $"{Indent.Indent()}{nameof(TechTier)}: {GetDebugString(Field: ref TechTier)}";

            yield return $"{Indent.Indent()}{nameof(WeaponSkills)}: {GetDebugString(Field: ref WeaponSkills, ToString: f => null)}";
            foreach (var weaponSkill in WeaponSkills.IteratorSafe())
            {
                if (!ForInternals)
                    yield return $"{(Indent + 1).Indent()}: {weaponSkill}";
                else
                    yield return $"\n{weaponSkill}";
            }

            yield return $"{Indent.Indent()}{nameof(EquipmentSlots)}: {GetDebugString(Field: ref EquipmentSlots, ToString: f => null)}";
            foreach (var equipmentSlot in EquipmentSlots.IteratorSafe())
            {
                if (!ForInternals)
                    yield return $"{(Indent + 1).Indent()}: {equipmentSlot}";
                else
                    yield return $"\n{equipmentSlot}";
            }

            yield return $"{Indent.Indent()}{nameof(Species)}: {GetDebugString(Field: ref Species)}";
            yield return $"{Indent.Indent()}{nameof(Class)}: {GetDebugString(Field: ref Class)}";
            yield return $"{Indent.Indent()}{nameof(Role)}: {GetDebugString(Field: ref Role)}";
            yield return $"{Indent.Indent()}{nameof(PaintedWall)}: {GetDebugString(Field: ref PaintedWall)}";
            yield return $"{Indent.Indent()}{nameof(PaintedFence)}: {GetDebugString(Field: ref PaintedFence)}";
        }

        public string DebugString(bool ForInternals = false)
            => GetDebugLines(ForInternals: ForInternals)
                .IteratorSafe()
                .Aggregate((string)null, Utils.NewLineDelimitedAggregator)
            ;

        protected static int GetCollectionSimilarity(
            ICollection<string> Primary,
            ICollection<string> Other,
            int MaxUnitSimilarity,
            int MinUnitSimilarity = 0,
            int? MaxTotalSimilarty = null
            )
        {
            if (IsFieldWildCard(ref Primary))
            {
                var prospectiveSimilarity = Other.IteratorSafe().Count() * MaxUnitSimilarity;
                return MaxTotalSimilarty.HasValue
                    ? Math.Min(prospectiveSimilarity, MaxTotalSimilarty.GetValueOrDefault())
                    : prospectiveSimilarity
                    ;
            }

            int similarty = 0;
            var safePrimary = Primary.IteratorSafe();
            var safeOther = Other.IteratorSafe();

            int countDiff = Math.Abs(safePrimary.Count() - safeOther.Count());

            foreach (var primaryEntry in safePrimary)
            {
                if (MaxTotalSimilarty.HasValue
                    && similarty <= MaxTotalSimilarty.GetValueOrDefault())
                {
                    similarty = MaxTotalSimilarty.GetValueOrDefault();
                    break;
                }
                if (safeOther.Contains(primaryEntry))
                {
                    similarty += Math.Clamp(countDiff, MinUnitSimilarity, MaxUnitSimilarity);
                    continue;
                }
            }
            return similarty;
        }

        protected static int GetFieldSimilarity<T>(
            ref T Primary,
            T Other,
            int SimilarityWhenIdentical,
            int DistanceMultiplier = 1
            )
            where T : IEquatable<T>
        {
            try
            {
                if (IsFieldWildCard(ref Primary))
                    return SimilarityWhenIdentical;

                if (Primary is null
                    || Other is null)
                    return 0;

                if (!EqualityComparer<T>.Default.Equals(Primary, Other))
                {
                    if (Primary is string stringPrimary
                        && Other is string stringOther)
                    {
                        int distance = (DistanceMultiplier * Levenshtein.EditDistance(stringPrimary, stringOther));
                        return Math.Max(0, SimilarityWhenIdentical - distance);
                    }

                    return 0;
                }
                return SimilarityWhenIdentical;
            }
            catch (Exception x)
            {
                Utils.Warn($"Failed getting similarity of {nameof(Primary)} {Primary} and {nameof(Other)} {Other}", x);
                return 0;
            }
        }

        public int GetSimilartyTo(BlueprintSpec Other)
        {
            if (Other == null)
                return 0;

            if (IsWildCard)
                return MAX_SIMILARTY;

            int similarity = MIN_SIMILARTY;

            similarity += GetCollectionSimilarity(
                Primary: BlueprintTree,
                Other: Other.BlueprintTree,
                MaxUnitSimilarity: 100,
                MinUnitSimilarity: 0,
                MaxTotalSimilarty: 300);

            if (!BlueprintTree.IsNullOrEmpty()
                && !IsFieldWildCard(ref BlueprintTree)
                && !Other.BlueprintTree.IsNullOrEmpty())
            {
                if (BlueprintTree.Count > 1
                    && (Other.BlueprintTree.Count <= 1
                        || BlueprintTree[1] != Other.BlueprintTree[1]))
                    similarity -= 400;

                if (BlueprintTree.Count > 2
                    && (Other.BlueprintTree.Count <= 2
                        || BlueprintTree[2] != Other.BlueprintTree[2]))
                    similarity -= 200;

                if (BlueprintTree.Count > 3
                    && (Other.BlueprintTree.Count <= 3
                        || BlueprintTree[3] != Other.BlueprintTree[3]))
                    similarity -= 100;
            }

            similarity += GetFieldSimilarity(ref Category, Other.Category, SimilarityWhenIdentical: 1000, DistanceMultiplier: 100);

            similarity += (XRL.World.Capabilities.Tier.MAXIMUM + 2).Fibonacci();
            if (!IsFieldWildCard(ref Tier))
                similarity -= Math.Abs((Tier.GetValueOrDefault() + 2).Fibonacci() - (Other.Tier.GetValueOrDefault() + 2).Fibonacci());

            similarity += (XRL.World.Capabilities.Tier.MAXIMUM + 2).Fibonacci();
            if (!IsFieldWildCard(ref TechTier))
                similarity -= Math.Abs((TechTier.GetValueOrDefault() + 2).Fibonacci() - (Other.TechTier.GetValueOrDefault() + 2).Fibonacci());

            similarity += GetCollectionSimilarity(
                Primary: WeaponSkills,
                Other: Other.WeaponSkills,
                MaxUnitSimilarity: 100,
                MinUnitSimilarity: 0,
                MaxTotalSimilarty: 200);
            
            similarity += GetCollectionSimilarity(
                Primary: EquipmentSlots,
                Other: Other.EquipmentSlots,
                MaxUnitSimilarity: 100,
                MinUnitSimilarity: 0,
                MaxTotalSimilarty: 200);

            similarity += GetFieldSimilarity(ref Species, Other.Species, SimilarityWhenIdentical: 100, DistanceMultiplier: 10);

            similarity += GetFieldSimilarity(ref Class, Other.Class, SimilarityWhenIdentical: 50, DistanceMultiplier: 5);
            similarity += GetFieldSimilarity(ref Role, Other.Role, SimilarityWhenIdentical: 250, DistanceMultiplier: 100);

            similarity += GetFieldSimilarity(ref PaintedWall, Other.PaintedWall, SimilarityWhenIdentical: 250, DistanceMultiplier: 25);
            similarity += GetFieldSimilarity(ref PaintedFence, Other.PaintedFence, SimilarityWhenIdentical: 250, DistanceMultiplier: 25);

            return Math.Clamp(similarity, MIN_SIMILARTY, MAX_SIMILARTY);
        }

        public IEnumerable<SimilarityRecord> GetOrderedSimilarityRecords()
        {
            //Utils.Log($"{nameof(GetOrderedSimilarityRecords)} for {Blueprint ?? "NULL"} ({nameof(Utils.CachedBlueprintSpecs)}: {(Utils.CachedBlueprintSpecs?.Count)?.ToString() ?? "null"})");
            //List<SimilarityRecord> list = null;

            foreach (var validSpec in (Utils.CachedBlueprintSpecs?.Values).IteratorSafe())
            {
                //Utils.Log($"{1.Indent()}{validSpec?.Blueprint ?? $"{nameof(validSpec)}_NULL"}:");
                if (SimilarityRecord.TryGetFor(this, validSpec, out var similarityRecord))
                {
                    if (!similarityRecord.IsEmpty())
                    {
                        yield return similarityRecord;
                        /*list ??= new();
                        list.Add(similarityRecord);*/
                        //Utils.Log($"{3.Indent()}{similarityRecord.DebugString()}");
                    }
                    /*else
                        Utils.Log($"{3.Indent()}{nameof(similarityRecord)} is {nameof(SimilarityRecord.Empty)}");*/
                }
                /*else
                    Utils.Log($"{3.Indent()}{validSpec?.Blueprint ?? "NULL"} got {nameof(SimilarityRecord.Empty)} {nameof(similarityRecord)}");*/
                //Utils.Log($"{1.Indent()}{Blueprint} is {similarityRecord.DebugString()}");
            }

            /*if (list.IsNullOrEmpty())
                return list.IteratorSafe();

            list.StableSortInPlace(SimilarityComparer);
            return list;*/
        }

        public void Dispose()
        {
            DebugName = null;
            Blueprint = null;
            BlueprintTree = null;

            Category = null;

            Tier = null;
            TechTier = null;

            WeaponSkills = null;
            EquipmentSlots = null;

            Species = null;
            Class = null;
            Role = null;
            PaintedWall = null;
            PaintedFence = null;
        }
    }
}
