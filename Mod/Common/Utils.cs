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
            }
        }

        public class BlueprintSpec
        {
            public string Category;
            public int? Tier;
            public string WeaponSkill;
            public string EquipmentSlot;

            public bool IsEmpty
                => Category.IsNullOrEmpty()
                && Tier == null
                && WeaponSkill.IsNullOrEmpty()
                && EquipmentSlot.IsNullOrEmpty()
                ;
        }

        public static HashSet<string> GetAlternativeBlueprintsBySpec(BlueprintSpec Spec)
        {
            HashSet<string> output = new();

            if (Spec == null
                || Spec.IsEmpty)
            {
                foreach (var categorySet in BlueprintsByCategory.Values)
                    output.UnionWith(categorySet);

                foreach (var tierSet in BlueprintsByTier.Values)
                    output.UnionWith(tierSet);

                foreach (var weaponSkillSet in BlueprintsByWeaponSkill.Values)
                    output.UnionWith(weaponSkillSet);

                foreach (var equipmentSlotSet in BlueprintsByEquipmentSlot.Values)
                    output.UnionWith(equipmentSlotSet);
            }

            return output;
        }

        private static void CacheValue<T>(ref Dictionary<T, HashSet<string>> Cache, T Key, string Value)
        {
            if (!Cache.ContainsKey(Key))
                Cache[Key] = new();
            Cache[Key].Add(Value);
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
            => UnityEngine.Debug.Log(Message);

        [VariableObjectReplacer]
        public static string UD_RegalTitle(DelegateContext Context)
        {
            string output = UD_Bones_MoonKingFever.REGAL_TITLE;
            if (Context.Target.TryGetEffect(out UD_Bones_MoonKingFever moonKingFever))
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
    }
}
