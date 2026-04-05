using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Genkit;

using Kobold;

using Qud.UI;

using XRL;
using XRL.Collections;
using XRL.Language;
using XRL.Messages;
using XRL.UI;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using static UD_Bones_Folder.Mod.Const;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasVariableReplacer]
    public static class Utils
    {
        public static ModInfo ThisMod => ModManager.GetMod(MOD_ID);

        public static string AuthorOnPlatforms => $"{ThisMod.Manifest.Author} on GitHub (UnderDoug), on Discord (.underdoug), or on the Steam Workshop (UnderDoug)";

        public static string BothBonesLocations 
            => $"{DataManager.SanitizePathForDisplay(BonesManager.BonesSyncPath)} -OR- " +
            $"{DataManager.SanitizePathForDisplay(BonesManager.BonesSavePath)}"
            ;

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static Dictionary<string, HashSet<string>> BlueprintsByCategory = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static Dictionary<int, HashSet<string>> BlueprintsByTier = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static Dictionary<string, HashSet<string>> BlueprintsByWeaponSkill = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static Dictionary<string, HashSet<string>> BlueprintsByEquipmentSlot = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static Dictionary<string, HashSet<string>> BlueprintsBySpecies = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static Dictionary<string, HashSet<string>> BlueprintsByClass = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static Dictionary<string, HashSet<string>> BlueprintsByPaintedWall = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static Dictionary<string, HashSet<string>> BlueprintsByPaintedFence = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static HashSet<string> CachedBlueprints = new();

        [ModSensitiveCacheInit]
        public static void CacheBlueprintsBySpec()
        {
            using var status = Loading.StartTask("Converting Lunar Regents");

            foreach (var blueprint in XRL.World.GameObjectFactory.Factory.GetBlueprintsInheritingFrom("PhysicalObject"))
            {
                if (!SpriteManager.HasTextureInfo(blueprint.GetRenderable().Tile))
                    continue;
                
                if (blueprint.TryGetPartParameter(nameof(Physics), nameof(Physics.Category), out string physicsCategory))
                    CacheValue(ref BlueprintsByCategory, physicsCategory, blueprint.Name);
                
                CacheValue(ref BlueprintsByTier, blueprint.Tier, blueprint.Name);

                CacheValue(ref BlueprintsByTier, blueprint.TechTier, blueprint.Name);

                if (blueprint.TryGetPartParameter(nameof(MeleeWeapon), nameof(MeleeWeapon.Skill), out string meleeWeaponSkill))
                    CacheValue(ref BlueprintsByWeaponSkill, meleeWeaponSkill, blueprint.Name);

                if (blueprint.TryGetPartParameter(nameof(MeleeWeapon), nameof(MeleeWeapon.Slot), out string meleeWeaponSlot))
                    CacheValue(ref BlueprintsByEquipmentSlot, meleeWeaponSlot, blueprint.Name);

                if (blueprint.TryGetPartParameter(nameof(MissileWeapon), nameof(MissileWeapon.Skill), out string missileWeaponSkill))
                    CacheValue(ref BlueprintsByWeaponSkill, missileWeaponSkill, blueprint.Name);

                if (blueprint.TryGetPartParameter(nameof(MissileWeapon), nameof(MissileWeapon.SlotType), out string missileWeaponSlot))
                    CacheValue(ref BlueprintsByEquipmentSlot, meleeWeaponSlot, blueprint.Name);

                if (blueprint.TryGetPartParameter(nameof(Armor), nameof(Armor.WornOn), out string armorSlot))
                    CacheValue(ref BlueprintsByEquipmentSlot, armorSlot, blueprint.Name);

                if (blueprint.TryGetStringPropertyOrTag("Species", out string speciesTag))
                    CacheValue(ref BlueprintsBySpecies, speciesTag, blueprint.Name);

                if (blueprint.TryGetStringPropertyOrTag("Class", out string classTag))
                    CacheValue(ref BlueprintsByClass, classTag, blueprint.Name);

                if (blueprint.TryGetStringPropertyOrTag("PaintedWall", out string paintedWallTag))
                    CacheValue(ref BlueprintsByPaintedWall, paintedWallTag, blueprint.Name);

                if (blueprint.TryGetStringPropertyOrTag("PaintedFence", out string paintedFenceTag))
                    CacheValue(ref BlueprintsByPaintedFence, paintedFenceTag, blueprint.Name);
            }
        }

        public class BlueprintSpec
        {
            public string Category;
            public HashSet<int> Tiers;
            public string WeaponSkill;
            public string EquipmentSlot;
            public string Species;
            public string Class;
            public string PaintedWall;
            public string PaintedFence;

            public BlueprintSpec()
            {
            }

            public BlueprintSpec(XRL.World.GameObject GameObject)
                : this()
            {
                Category = GameObject?.Physics?.Category;

                if ((GameObject?.HasIntProperty("Tier") is true)
                    || (GameObject?.HasIntProperty("TechTIer") is true))
                {
                    Tiers = new();
                    if (GameObject?.GetIntPropertyIfSet("Tier") is int tier)
                        Tiers.Add(tier);

                    if (GameObject?.GetIntPropertyIfSet("TechTier") is int techTier)
                        Tiers.Add(techTier);
                }

                using var workingList = ScopeDisposedList<string>.GetFromPool();
                if (GameObject?.GetPart<MissileWeapon>()?.Skill is string missileSkill)
                    workingList.Add(missileSkill);
                if (GameObject.GetStringProperty("ImprovisedWeapon", "false").EqualsNoCase("true")
                    && GameObject?.GetPart<MeleeWeapon>()?.Skill is string meleeSkill)
                    workingList.Add(meleeSkill);
                ProcessWorkingList(out WeaponSkill, workingList);

                if (GameObject?.GetPart<MissileWeapon>()?.SlotType?.CachedCommaExpansion() is IEnumerable<string> missileSlots)
                    workingList.AddRange(missileSlots);
                if (GameObject?.GetPart<Armor>()?.WornOn?.CachedCommaExpansion() is IEnumerable<string> armorSlots)
                    workingList.AddRange(armorSlots);
                if (GameObject.GetStringProperty("ImprovisedWeapon", "false").EqualsNoCase("true")
                    && GameObject?.GetPart<MeleeWeapon>()?.Slot?.CachedCommaExpansion() is IEnumerable<string> meleeSlots)
                    workingList.AddRange(meleeSlots);
                if (GameObject?.GetStringProperty("UsesSlots")?.CachedCommaExpansion() is IEnumerable<string> usesSlots)
                    workingList.AddRange(usesSlots);
                ProcessWorkingList(out EquipmentSlot, workingList);

                if (GameObject?.GetStringProperty("Species") is string speciesProp)
                    workingList.Add(speciesProp);
                ProcessWorkingList(out Species, workingList);

                if (GameObject?.GetStringProperty(nameof(Class)) is string classProp)
                    Class = classProp;

                if (GameObject?.GetStringProperty(nameof(PaintedWall)) is string paintedWallProp)
                    PaintedWall = paintedWallProp;

                if (GameObject?.GetStringProperty(nameof(PaintedFence)) is string paintedFenceProp)
                    PaintedFence = paintedFenceProp;
            }
            public BlueprintSpec(BlueprintSpec Source)
            {
                Category = Source.Category;
                Tiers = Source.Tiers;
                WeaponSkill = Source.WeaponSkill;
                EquipmentSlot = Source.Category;
                Category = Source.Category;
                Category = Source.Category;
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

            private static void ProcessWorkingList(out string Field, ScopeDisposedList<string> WorkingList)
            {
                Field = null;
                if (!WorkingList.IsNullOrEmpty())
                    WorkingList.Aggregate(Field, CommaDelimitedAggregator);
                WorkingList.Clear();
            }

            protected IEnumerable<string> GetMatching<T>(Dictionary<T, HashSet<string>> Cache, T Key, T NoValue, T AllValue)
            {
                if (Equals(Key, NoValue))
                    yield break;

                if (Equals(Key, AllValue))
                    foreach (var element in Cache.Values.GetUnionOfSets())
                        yield return element;
                else
                    foreach (var element in Cache.GetValue(Key) ?? Enumerable.Empty<string>())
                        yield return element;
            }

            public IEnumerable<string> GetMatchingStringKey(Dictionary<string, HashSet<string>> Cache, string Key)
            {
                var output = new HashSet<string>();
                if (Key?.CachedCommaExpansion().ToList() is List<string> keys)
                {
                    foreach (var key in keys)
                        output.UnionWith(GetMatching(Cache, key, null, string.Empty));
                }
                return output;
            }

            public IEnumerable<string> GetMatchingCategory()
                => GetMatchingStringKey(BlueprintsByCategory, Category)
                ;

            public IEnumerable<string> GetMatchingTier()
            {
                if (Tiers == null)
                    return Enumerable.Empty<string>();

                if (Tiers.IsNullOrEmpty())
                    return BlueprintsByTier.Values.GetUnionOfSets();
                else
                {
                    var output = new HashSet<string>();
                    foreach (int tier in Tiers)
                        output.UnionWith(GetMatching(BlueprintsByTier, tier, -1, 9));

                    return output;
                }
            }

            public IEnumerable<string> GetMatchingWeaponSkill()
                => GetMatchingStringKey(BlueprintsByWeaponSkill, WeaponSkill)
                ;

            public IEnumerable<string> GetMatchingEquipmentSlot()
                => GetMatchingStringKey(BlueprintsByEquipmentSlot, EquipmentSlot)
                ;

            public IEnumerable<string> GetMatchingSpecies()
                => GetMatchingStringKey(BlueprintsBySpecies, Species)
                ;

            public IEnumerable<string> GetMatchingClass()
                => GetMatchingStringKey(BlueprintsByClass, Class)
                ;

            public IEnumerable<string> GetMatchingPaintedWall()
                => GetMatchingStringKey(BlueprintsByPaintedWall, PaintedWall)
                ;

            public IEnumerable<string> GetMatchingPaintedFence()
                => GetMatchingStringKey(BlueprintsByPaintedFence, PaintedFence)
                ;

            public IEnumerable<string> GetMatchingSpec()
            {
                if (IsEmpty)
                    return Enumerable.Empty<string>();

                var output = new HashSet<string>(CachedBlueprints);

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
                    return output.IntersectWithUnlessEmptyOrNull(GetMatchingCategory())
                        .IntersectWithUnlessEmptyOrNull(GetMatchingWeaponSkill())
                        .IntersectWithUnlessEmptyOrNull(GetMatchingEquipmentSlot())
                        .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedWall())
                        .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedFence())
                        ;
                }
                return output;
            }
        }

        public static HashSet<string> GetAlternativeBlueprintsBySpec(BlueprintSpec Spec)
            => new (Spec.GetMatchingSpec())
            ;

        private static void CacheValue<T>(ref Dictionary<T, HashSet<string>> Cache, T Key, string Value)
        {
            if (!Cache.ContainsKey(Key))
                Cache[Key] = new();
            Cache[Key].Add(Value);

            CachedBlueprints.Add(Value);
        }

        public static void Error(object Message)
            => ThisMod.Error(Message)
            ;

        public static void Error(object Context, Exception X)
            => Error($"{Context}: {X}")
            ;

        public static void Warn(object Message)
            => ThisMod.Warn(Message)
            ;

        public static void Info(object Message)
            => MetricsManager.LogModInfo(ThisMod, Message)
            ;

        public static void Log(object Message)
            => UnityEngine.Debug.Log(Message)
            ;

        [VariableObjectReplacer]
        public static string UD_RegalTitle(DelegateContext Context)
        {
            string output = UD_Bones_MoonKingFever.REGAL_TITLE;
            if (Context?.Target != null
                && Context.Target.TryGetEffect(out UD_Bones_MoonKingFever moonKingFever))
                output = moonKingFever.RegalTitle.Colored("rainbow");

            return output;
        }

        public static void GetMinMax<T>(T Operand1, T Operand2, out T Min, out T Max)
            where T : IComparable<T>
        {
            Min = Operand1;
            Max = Operand2;
            if (Operand1.CompareTo(Operand2) > 0)
            {
                Min = Operand2;
                Max = Operand1;
            }
        }
        public static string DelimitedAggregator<T>(string Accumulator, T Next, string Delimiter)
            => Accumulator + (!Accumulator.IsNullOrEmpty() ? Delimiter : null) + Next
            ;

        public static string CommaDelimitedAggregator<T>(string Accumulator, T Next)
            => DelimitedAggregator(Accumulator, Next, ",")
            ;

        public static string CommaSpaceDelimitedAggregator<T>(string Accumulator, T Next)
            => DelimitedAggregator(Accumulator, Next, ", ")
            ;

        public static string NewLineDelimitedAggregator<T>(string Accumulator, T Next)
            => DelimitedAggregator(Accumulator, Next, "\n")
            ;

        public static string PeriodDelimitedAggregator<T>(string Accumulator, T Next)
            => DelimitedAggregator(Accumulator, Next, ".")
            ;

        public static string PeriodSpaceDelimitedAggregator<T>(string Accumulator, T Next)
            => DelimitedAggregator(Accumulator, Next, ". ")
            ;

        public static string CallChain(params string[] Strings)
            => Strings?.Aggregate("", PeriodDelimitedAggregator)
            ;

        private static string SafeInvoke<T>(this Func<string, string> PostProc, Func<T, string> Proc, T Element, string NoArg)
        {
            string proc = Proc?.Invoke(Element) ?? Element?.ToString() ?? NoArg;
            if (PostProc != null)
                proc = PostProc(proc);
            return proc;
        }

        public static IEnumerable<T> Log<T>(IEnumerable<T> Source, object Message)
        {
            Log(Message);
            return Source;
        }

        public static IEnumerable<T> Loggregrate<T>(
            IEnumerable<T> Source,
            Func<T, string> Proc = null,
            string Empty = null,
            Func<string, string> PostProc = null
            )
            => Source.IsNullOrEmpty()
            ? Log(Source, PostProc?.Invoke(Empty) ?? Empty)
            : Source.Aggregate(
                seed: Source,
                func: (a, n) => Log(a, PostProc.SafeInvoke(Proc, n, "NO_ELEMENT")))
            ;

        public static async Task PopupShowAsync(
            string Message,
            string Title = null,
            bool Capitalize = true,
            bool LogMessage = true,
            Location2D PopupLocation = null
            )
        {
            if (Capitalize)
                Message = ColorUtility.CapitalizeExceptFormatting(Message);

            if (LogMessage)
                MessageQueue.AddPlayerMessage(Message: Message, Capitalize: Capitalize);

            await Popup.NewPopupMessageAsync(
                message: Message,
                buttons: PopupMessage.AcceptButton,
                title: Title,
                PopupLocation: PopupLocation);
        }

        public static bool TileExists(string Tile)
            => Tile != null
            && SpriteManager.GetTextureInfo(Tile, false) is not null
            ;
    }
}
