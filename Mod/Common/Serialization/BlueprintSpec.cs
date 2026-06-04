using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL;
using XRL.Collections;
using XRL.World;
using XRL.World.AI;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class BlueprintSpec : IComposite, IDisposable
    {
        public const string IMPROVISED_WEAPON = "ImprovisedWeapon";

        public static string TRUE => $"{true}";
        public static string FALSE => $"{false}";

        public string DebugName;
        public string Blueprint;

        public List<string> BlueprintTree;

        public string Category;
        public int? Tier;
        public int? TechTier;

        [NonSerialized]
        public HashSet<string> WeaponSkills;

        [NonSerialized]
        public HashSet<string> EquipmentSlots;

        public string Species;
        public string Class;
        public string Role;
        public string PaintedWall;
        public string PaintedFence;

        public BlueprintSpec()
        { }

        public BlueprintSpec(GameObject GameObject)
            : this()
        {
            if (GameObject == null)
                throw new ArgumentNullException(nameof(GameObject));

            DebugName = GameObject.DebugName;
            Blueprint = GameObject.Blueprint;

            BlueprintTree ??= new();
            var parentBlueprint = GameObject.GetBlueprint();
            while (parentBlueprint.Inherits != null)
            {
                try
                {
                    parentBlueprint = GameObjectFactory.Factory.GetBlueprintIfExists(parentBlueprint.Inherits);
                    if (parentBlueprint?.Name is not string blueprintName)
                    {
                        Utils.Warn($"Strange, nameless {nameof(GameObjectBlueprint)} in {GameObject.DebugName} inheritance tree, {BlueprintTree.Count.Things("blueprint")} in");
                        break;
                    }
                    BlueprintTree.Add(blueprintName);
                }
                catch (Exception x)
                {
                    Utils.Error($"Issue retreiving {nameof(GameObjectBlueprint)} in {GameObject.DebugName} inheritance tree, {BlueprintTree.Count.Things("blueprint")} in", x);
                    break;
                }
            }

            Category = GameObject?.Physics?.Category;

            try
            {
                Tier = GameObject.GetTier();
                TechTier = GameObject.GetTechTier();
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} Tiers", x);
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
                if (GameObject.GetSpecies() is string species
                    && !species.IsNullOrEmpty())
                {
                    Species = species;
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Species)}", x);
            }

            try
            {
                Class = GameObject.GetClass();

                Role = GameObject.GetPropertyOrTag(nameof(Role));

                PaintedWall = GameObject.GetPropertyOrTag(nameof(PaintedWall));

                PaintedFence = GameObject.GetPropertyOrTag(nameof(PaintedFence));
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Class)},  {nameof(Role)}, {nameof(PaintedWall)}, {nameof(PaintedFence)}", x);
            }
        }

        public BlueprintSpec(GameObjectBlueprint Model)
            : this()
        {
            if (Model == null)
                throw new ArgumentNullException(nameof(Model));

            DebugName = $"{Model.Name} ({nameof(GameObjectBlueprint)})";
            Blueprint = Model.Name;

            BlueprintTree ??= new();
            var parentBlueprint = GameObject.GetBlueprint();
            while (parentBlueprint.Inherits != null)
            {
                try
                {
                    parentBlueprint = GameObjectFactory.Factory.GetBlueprintIfExists(parentBlueprint.Inherits);
                    if (parentBlueprint?.Name is not string blueprintName)
                    {
                        Utils.Warn($"Strange, nameless {nameof(GameObjectBlueprint)} in {GameObject.DebugName} inheritance tree, {BlueprintTree.Count.Things("blueprint")} in");
                        break;
                    }
                    BlueprintTree.Add(blueprintName);
                }
                catch (Exception x)
                {
                    Utils.Error($"Issue retreiving {nameof(GameObjectBlueprint)} in {GameObject.DebugName} inheritance tree, {BlueprintTree.Count.Things("blueprint")} in", x);
                    break;
                }
            }

            Category = Model.GetPartParameter<string>(nameof(Physics), nameof(Physics.Category));

            try
            {
                Tier = Model.GetTag("Tier");
                TechTier = GameObject.GetTechTier();
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} Tiers", x);
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
                if (GameObject.GetSpecies() is string species
                    && !species.IsNullOrEmpty())
                {
                    Species = species;
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Species)}", x);
            }

            try
            {
                Class = GameObject.GetClass();

                Role = GameObject.GetPropertyOrTag(nameof(Role));

                PaintedWall = GameObject.GetPropertyOrTag(nameof(PaintedWall));

                PaintedFence = GameObject.GetPropertyOrTag(nameof(PaintedFence));
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BlueprintSpec)} {nameof(Class)},  {nameof(Role)}, {nameof(PaintedWall)}, {nameof(PaintedFence)}", x);
            }
        }

        public BlueprintSpec(BlueprintSpec Source)
        {
            DebugName = Source?.DebugName;
            Blueprint = Source?.Blueprint;

            Category = Source?.Category;
            Tiers = Source?.Tiers;
            WeaponSkill = Source?.WeaponSkill;
            EquipmentSlot = Source?.Category;
            Species = Source?.Species;
            Class = Source?.Class;
            PaintedWall = Source?.PaintedWall;
            PaintedFence = Source?.PaintedFence;
        }

        public bool IsEmpty
            => Category == null
            && Tiers == null
            && WeaponSkill == null
            && EquipmentSlot == null
            && Species == null
            && Class == null
            && PaintedWall == null
            && PaintedFence == null
            ;

        public bool IsAll
            => Category.IsNullOrEmpty()
            && Tiers.IsNullOrEmpty()
            && WeaponSkill.IsNullOrEmpty()
            && EquipmentSlot.IsNullOrEmpty()
            && Species.IsNullOrEmpty()
            && Class.IsNullOrEmpty()
            && PaintedWall.IsNullOrEmpty()
            && PaintedFence.IsNullOrEmpty()
            ;

        public IEnumerable<string> GetDebugLines(int Indent = 0)
        {
            yield return $"{Indent.Indent()}{nameof(DebugName)}: {DebugName ?? "NONE"}";
            yield return $"{Indent.Indent()}{nameof(Blueprint)}: {Blueprint ?? "NONE"}";

            if (AlreadyExists)
            {
                yield return $"{nameof(AlreadyExists)}: {AlreadyExists}";
                yield break;
            }
            yield return $"{Indent.Indent()}{nameof(Category)}: {Category ?? "NONE"}";
            yield return $"{Indent.Indent()}{nameof(Tiers)}: {Tiers?.Count ?? -1}";
            if (Tiers.IsNullOrEmpty())
                yield return $"{(Indent + 1).Indent()}: Empty";
            else
                foreach (var tier in Tiers)
                    yield return $"{(Indent + 1).Indent()}: {tier}";

            yield return $"{Indent.Indent()}{nameof(WeaponSkill)}: {WeaponSkill ?? "NONE"}";
            yield return $"{Indent.Indent()}{nameof(EquipmentSlot)}: {EquipmentSlot ?? "NONE"}";
            yield return $"{Indent.Indent()}{nameof(Species)}: {Species ?? "NONE"}";
            yield return $"{Indent.Indent()}{nameof(Class)}: {Class ?? "NONE"}";
            yield return $"{Indent.Indent()}{nameof(PaintedWall)}: {PaintedWall ?? "NONE"}";
            yield return $"{Indent.Indent()}{nameof(PaintedFence)}: {PaintedFence ?? "NONE"}";
        }

        private static void ProcessWorkingList(out string Field, IEnumerable<string> WorkingList)
        {
            Field = "";
            if (!WorkingList.IsNullOrEmpty())
                Field = WorkingList.Aggregate(Field, Utils.CommaDelimitedAggregator);
        }

        protected IEnumerable<string> GetMatching<T>(Dictionary<T, HashSet<string>> Cache, T Key, T NoValue, T AllValue)
        {
            if (Equals(Key, NoValue))
                return Enumerable.Empty<string>();

            if (Equals(Key, AllValue))
                return Cache.Values.GetUnionOfSets().IteratorSafe();
            else
                return Cache.GetValue(Key).IteratorSafe();
        }

        public IEnumerable<string> GetMatchingStringKey(Dictionary<string, HashSet<string>> Cache, string Key)
        {
            var output = new HashSet<string>();
            if (Key.IsNullOrEmpty())
                return output;

            if (Key?.CachedCommaExpansion().ToList() is List<string> keys
                && !keys.IsNullOrEmpty())
            {
                foreach (var key in keys)
                    output.UnionWith(GetMatching(Cache, key, null, string.Empty));
            }
            else
                output.UnionWith(GetMatching(Cache, Key, null, string.Empty));

            return output;
        }

        public IEnumerable<string> GetMatchingCategory()
            => GetMatchingStringKey(Utils.BlueprintsByCategory, Category)
            ;

        public IEnumerable<string> GetMatchingTier()
        {
            if (Tiers == null)
                return Enumerable.Empty<string>();

            if (Tiers.IsNullOrEmpty())
                return Utils.BlueprintsByTier.Values.GetUnionOfSets();
            else
            {
                var output = new HashSet<string>();
                foreach (int tier in Tiers)
                    output.UnionWith(GetMatching(Utils.BlueprintsByTier, tier, -1, 9));

                return output;
            }
        }

        public IEnumerable<string> GetMatchingWeaponSkill()
            => GetMatchingStringKey(Utils.BlueprintsByWeaponSkill, WeaponSkill)
            ;

        public IEnumerable<string> GetMatchingEquipmentSlot()
            => GetMatchingStringKey(Utils.BlueprintsByEquipmentSlot, EquipmentSlot)
            ;

        public IEnumerable<string> GetMatchingSpecies()
            => GetMatchingStringKey(Utils.BlueprintsBySpecies, Species)
            ;

        public IEnumerable<string> GetMatchingClass()
            => GetMatchingStringKey(Utils.BlueprintsByClass, Class)
            ;

        public IEnumerable<string> GetMatchingPaintedWall()
            => GetMatchingStringKey(Utils.BlueprintsByPaintedWall, PaintedWall)
            ;

        public IEnumerable<string> GetMatchingPaintedFence()
            => GetMatchingStringKey(Utils.BlueprintsByPaintedFence, PaintedFence)
            ;

        public IEnumerable<string> GetMatchingSpec()
        {
            if (IsEmpty)
                return Enumerable.Empty<string>();

            var output = new HashSet<string>(Utils.CachedBlueprints);

            if (IsAll)
                return output;

            if (output.IntersectWithUnlessEmptyOrNull(GetMatchingCategory())
                    .IntersectWithUnlessEmptyOrNull(GetMatchingTier())
                    .IntersectWithUnlessEmptyOrNull(GetMatchingWeaponSkill())
                    .IntersectWithUnlessEmptyOrNull(GetMatchingEquipmentSlot())
                    .IntersectWithUnlessEmptyOrNull(GetMatchingSpecies())
                    .IntersectWithUnlessEmptyOrNull(GetMatchingClass())
                    .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedWall())
                    .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedFence())
                .IsNullOrEmpty())
            {
                output.Clear();
                output.UnionWith(Utils.CachedBlueprints);
                if (output.IntersectWithUnlessEmptyOrNull(GetMatchingCategory())
                        .IntersectWithUnlessEmptyOrNull(GetMatchingWeaponSkill())
                        .IntersectWithUnlessEmptyOrNull(GetMatchingEquipmentSlot())
                        .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedWall())
                        .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedFence())
                    .IsNullOrEmpty())
                {
                    output.Clear();
                    output.UnionWith(Utils.CachedBlueprints);
                    if (output.IntersectWithUnlessEmptyOrNull(GetMatchingCategory())
                            .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedWall())
                            .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedFence())
                        .IsNullOrEmpty())
                        return output.IntersectWithUnlessEmptyOrNull(GetMatchingCategory())
                            ;
                    return output;
                }
                return output;
            }
            return output;
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
            PaintedWall = null;
            PaintedFence = null;
        }
    }
}
