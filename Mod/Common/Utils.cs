using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Genkit;

using HarmonyLib;

using Kobold;

using Qud.UI;

using XRL;
using XRL.Collections;
using XRL.Core;
using XRL.Language;
using XRL.Messages;
using XRL.Rules;
using XRL.UI;
using XRL.World;
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

        public const double FPS_MODULO = 8.0;

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        public static Dictionary<string, string> EquipmentFrameByTileColor = new();

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

            foreach (var blueprint in GameObjectFactory.Factory.GetBlueprintsInheritingFrom("PhysicalObject"))
            {
                if (!blueprint.GetRenderable().Tile.IsTile())
                    continue;

                if (blueprint.IsExcludedFromDynamicEncounters())
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
            public string DebugName;
            public string Blueprint;

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
                DebugName = GameObject.DebugName;
                Blueprint = GameObject.Blueprint;

                if (BonesManager.System.HasBlueprintReplacement(Blueprint)
                    && BonesManager.System.HasTileReplacement(Blueprint))
                {
                    Log($"{nameof(BlueprintSpec)}: Already have entry for {Blueprint}");
                    return;
                }

                Category = GameObject?.Physics?.Category;

                try
                {
                    if ((GameObject?.HasIntProperty("Tier") is true)
                        || (GameObject?.HasIntProperty("TechTIer") is true))
                    {
                        Tiers = new();
                        if (GameObject?.GetIntPropertyIfSet("Tier") is int tier)
                            Tiers.Add(tier);

                        if (GameObject?.GetIntPropertyIfSet("TechTier") is int techTier)
                            Tiers.Add(techTier);
                    }
                }
                catch (Exception x)
                {
                    Error($"{nameof(BlueprintSpec)} Tiers", x);
                }

                using var workingList = ScopeDisposedList<string>.GetFromPool();
                try
                {
                    if (GameObject?.GetPart<MissileWeapon>()?.Skill is string missileSkill)
                        workingList.Add(missileSkill);
                    if (GameObject.GetStringProperty("ImprovisedWeapon", "false").EqualsNoCase("true")
                        && GameObject?.GetPart<MeleeWeapon>()?.Skill is string meleeSkill)
                        workingList.Add(meleeSkill);
                    ProcessWorkingList(out WeaponSkill, workingList);
                }
                catch (Exception x)
                {
                    Error($"{nameof(BlueprintSpec)} Skills", x);
                    workingList.Clear();
                }

                try
                {
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
                }
                catch (Exception x)
                {
                    Error($"{nameof(BlueprintSpec)} Slots", x);
                    workingList.Clear();
                }

                try
                {
                    if (GameObject?.GetStringProperty("Species") is string speciesProp)
                        workingList.Add(speciesProp);
                    ProcessWorkingList(out Species, workingList);
                }
                catch (Exception x)
                {
                    Error($"{nameof(BlueprintSpec)} Species", x);
                    workingList.Clear();
                }

                try
                {
                    if (GameObject?.GetStringProperty(nameof(Class)) is string classProp)
                        Class = classProp;

                    if (GameObject?.GetStringProperty(nameof(PaintedWall)) is string paintedWallProp)
                        PaintedWall = paintedWallProp;

                    if (GameObject?.GetStringProperty(nameof(PaintedFence)) is string paintedFenceProp)
                        PaintedFence = paintedFenceProp;
                }
                catch (Exception x)
                {
                    Error($"{nameof(BlueprintSpec)} Class, PaintedWall, PintedFence", x);
                }
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

            public IEnumerable<string> GetDebugLines(int Indent = 0)
            {
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
                    if (output.IntersectWithUnlessEmptyOrNull(GetMatchingCategory())
                            .IntersectWithUnlessEmptyOrNull(GetMatchingWeaponSkill())
                            .IntersectWithUnlessEmptyOrNull(GetMatchingEquipmentSlot())
                            .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedWall())
                            .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedFence())
                        .IsNullOrEmpty())
                    {
                        output.Clear();
                        return output.IntersectWithUnlessEmptyOrNull(GetMatchingCategory())
                            .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedWall())
                            .IntersectWithUnlessEmptyOrNull(GetMatchingPaintedFence())
                            ;
                    }
                    return output;
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
            => $"=LunarShader:{UD_Bones_LunarRegent.GetRegalTitle(Context.Target)}="
                .StartReplace()
                .ToString()
            ;

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

        public static exTextureInfo GetSpriteManagerInvalidInfoNaughty(string DebugForTile = null)
        {
            string fieldName = "InvalidInfo";
            exTextureInfo textureInfo = SpriteManager.GetTextureInfo("Text_32.bmp"); // this is what it defaults to at the time this was written
            // Log($"{nameof(GetSpriteManagerInvalidInfoNaughty)}({nameof(DebugForTile)}: {DebugForTile ?? "NO_TILE"})");
            try
            {
                var field = typeof(SpriteManager).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    ?? throw new ArgumentOutOfRangeException("name", $"Field \"{fieldName}\" was not found in {nameof(Type)} {typeof(SpriteManager)}");

                if (!typeof(exTextureInfo).IsAssignableFrom(field.FieldType))
                    throw new InvalidCastException($"{fieldName} field in {nameof(Type)} {typeof(SpriteManager)} is {field.FieldType.Name} which cannot be cast to {typeof(exTextureInfo)}");

                textureInfo = (field.GetValue(null) as exTextureInfo);
            }
            catch (ArgumentOutOfRangeException x)
            {
                Error($"", x);
                textureInfo = SpriteManager.GetTextureInfo("Text_32.bmp");
            }
            catch (InvalidCastException x)
            {
                Error($"", x);
                textureInfo = SpriteManager.GetTextureInfo("Text_32.bmp");
            }
            return textureInfo;
        }

        public static bool TileExists(string Tile)
            => Tile != null
            && SpriteManager.GetTextureInfo(Tile, false) != null
            && SpriteManager.GetTextureInfo(Tile) != GetSpriteManagerInvalidInfoNaughty(Tile)
            ;

        public static IEnumerable<string> YieldRainbowColors()
        {
            yield return "r";
            yield return "R";
            yield return "W";
            yield return "G";
            yield return "B";
            yield return "b";
            yield return "m";
        }

        public static ScopeDisposedList<string> ScopeDiscposedRainbowColorsListFromPool()
            => ScopeDisposedList<string>.GetFromPoolFilledWith(YieldRainbowColors())
            ;

        public static string GetRainbowColorAtIndex(int Offset)
        {
            using var rainbowColors = ScopeDiscposedRainbowColorsListFromPool();
            if (Offset < 0)
                Offset = Math.Abs(Offset + rainbowColors.Count);
            Offset %= rainbowColors.Count;
            return rainbowColors[Offset];
        }

        public static bool IsRainbowColor(string Color)
            => YieldRainbowColors().Contains(Color)
            ;

        public static bool IsRainbowColor(char Color)
            => IsRainbowColor(Color.ToString())
            ;

        public static string GetNextRainbowColor(string Color)
        {
            if (!IsRainbowColor(Color))
                return null;

            using var rainbowColors = ScopeDiscposedRainbowColorsListFromPool();
            int colorIndex = rainbowColors.IndexOf(Color);

            return GetRainbowColorAtIndex(colorIndex + 1);
        }

        public static string GetNextRainbowColor(char Color)
            => GetNextRainbowColor(Color.ToString())
            ;

        public static string GetPrevRainbowColor(string Color)
        {
            using var rainbowColors = ScopeDiscposedRainbowColorsListFromPool();
            if (!rainbowColors.Contains(Color))
                return null;

            int colorIndex = rainbowColors.IndexOf(Color);

            return GetRainbowColorAtIndex(colorIndex - 1);
        }

        public static string GetAnimatedRainbowShader(int Offset = 0, string Style = "sequence")
        {
            using var rainbowColors = ScopeDiscposedRainbowColorsListFromPool();

            string output = null;
            for (int i = 0; i < rainbowColors.Count; i++)
                output = DelimitedAggregator(output, GetRainbowColorAtIndex(Offset + i), "-");

            if (output.IsNullOrEmpty())
                return "rainbow";

            // Log($"{output} {Style}");
            return $"{output} {Style}";
        }

        public static int GetFPSModuloOrRandom()
        {
            if (XRLCore.CurrentFrameAccumulator == 0)
                return Stat.RandomCosmetic(0, 7000);
            return (int)Math.Ceiling(XRLCore.CurrentFrameAccumulator / FPS_MODULO);
        }

        public static string GetAnimatedRainbowShaderForFrame()
            => GetAnimatedRainbowShader(GetFPSModuloOrRandom())
            ;

        public static string GetRainbowColorForFPS()
            => GetRainbowColorAtIndex(GetFPSModuloOrRandom())
            ;

        [VariablePostProcessor(Keys = new string[] { "LunarShader" })]
        public static void LunarShaderPost(DelegateContext Context)
        {
            if (!Context.Value.IsNullOrEmpty())
            {
                string shader;
                if (!Context.Parameters.IsNullOrEmpty()
                    && Context.Parameters[0] == "*")
                    shader = GetAnimatedRainbowShader(Stat.RandomCosmetic(0, 7000));
                else
                    shader = GetAnimatedRainbowShaderForFrame();

                var oldValue = Context.Value.ToString();
                Context.Value.Clear();
                Context.Value.AppendColored(shader, oldValue);
            }
        }

        [VariableReplacer(Keys = new string[] { "LunarShader" })]
        public static string LunarShader(DelegateContext Context)
        {
            if (!Context.Parameters.IsNullOrEmpty()
                && Context.Parameters[0] is string text
                && !text.IsNullOrEmpty())
            {
                string shader;
                if (Context.Parameters.Count > 1
                    && Context.Parameters[1] == "*")
                    shader = GetAnimatedRainbowShader(Stat.RandomCosmetic(0, 7000));
                else
                    shader = GetAnimatedRainbowShaderForFrame();

                return text.Color(shader);
            }
            return null;
        }

        public static string GetAnimatedRainbowShaderEquipmentFrame(int TileColorIndex)
        {
            EquipmentFrameByTileColor ??= new();
            string tileColor = GetRainbowColorAtIndex(TileColorIndex);
            if (!EquipmentFrameByTileColor.ContainsKey(tileColor))
            {
                if (IsRainbowColor(tileColor))
                {
                    string color3 = tileColor;
                    string color2 = GetPrevRainbowColor(color3);
                    string color1 = GetPrevRainbowColor(color2);
                    string color4 = GetNextRainbowColor(color3);
                    string color5 = GetNextRainbowColor(color4);
                    string color6 = GetNextRainbowColor(color5);
                    EquipmentFrameByTileColor[tileColor] = $"{color1}{color2}{color5}{color6}";
                }
                else
                    EquipmentFrameByTileColor[tileColor] = null;
            }
            return EquipmentFrameByTileColor[tileColor];
        }

        public static string GetAnimatedRainbowShaderEquipmentFrame(string Color)
        {
            if (!Color.IsNullOrEmpty()
                && Color.Length > 1)
                Color = $"{ColorUtility.FindLastForeground(Color)}";

            if (!IsRainbowColor(Color))
                return null;

            using var rainbowColors = ScopeDiscposedRainbowColorsListFromPool();
            return GetAnimatedRainbowShaderEquipmentFrame(rainbowColors.IndexOf(Color));
        }

        public static string GetAnimatedRainbowShaderEquipmentFrame(RenderEvent Render)
            => GetAnimatedRainbowShaderEquipmentFrame($"{Render.GetForegroundColorChar()}")
            ;

        public static string GetAnimatedRainbowShaderEquipmentFrame(IRenderable Render)
            => Render is RenderEvent renderEvent
            ? GetAnimatedRainbowShaderEquipmentFrame(renderEvent)
            : GetAnimatedRainbowShaderEquipmentFrame($"{Render.getTileColor()}")
            ;
    }
}
