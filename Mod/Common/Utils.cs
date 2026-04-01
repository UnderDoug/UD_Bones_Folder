using System;
using System.Collections.Generic;
using System.Text;
using System.Numerics;

using XRL;
using XRL.Language;
using XRL.World.Effects;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;
using XRL.Collections;
using System.Linq;

namespace UD_Bones_Folder.Mod
{
    [HasVariableReplacer]
    public static class Utils
    {
        public const string MOD_ID = "UD_Bones_Folder";

        public static ModInfo ThisMod => ModManager.GetMod(MOD_ID);

        public static string BothBonesLocations 
            => $"{DataManager.SanitizePathForDisplay(BonesManager.BonesSyncPath)} -OR- " +
            $"{DataManager.SanitizePathForDisplay(BonesManager.BonesSavePath)}"
            ;

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
                output = moonKingFever.RegalTitle.WithColor("rainbow");

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

        public static List<string> GetCriticalWarningUnion(
            IEnumerable<string> CriticalItems,
            IEnumerable<string> WarningItems
            )
        {
            using var sharedValues = ScopeDisposedList<string>.GetFromPoolFilledWith(CriticalItems);
            using var missingCritcalItems = ScopeDisposedList<string>.GetFromPool();
            using var missingWarningItems = ScopeDisposedList<string>.GetFromPool();

            foreach (var warningItem in WarningItems)
                if (!sharedValues.Contains(warningItem))
                    sharedValues.Add(warningItem);

            foreach (var sharedValue in sharedValues)
            {
                if (!CriticalItems.Any(i => i == sharedValue))
                    missingCritcalItems.Add(sharedValue);
                if (!WarningItems.Any(i => i == sharedValue))
                    missingWarningItems.Add(sharedValue);
            }

            foreach (var missingCritical in missingCritcalItems)
                sharedValues.Remove(missingCritical);

            foreach (var missingWarning in missingWarningItems)
                sharedValues.Remove(missingWarning);

            var output = new List<string>();

            missingCritcalItems.Aggregate(
                seed: output,
                func: delegate (List<string> accumulator, string next)
                {
                    accumulator.Add(next.WithColor("R"));
                    return accumulator;
                });

            missingWarningItems.Aggregate(
                seed: output,
                func: delegate (List<string> accumulator, string next)
                {
                    accumulator.Add(next.WithColor("W"));
                    return accumulator;
                });

            return sharedValues.Aggregate(
                seed: output,
                func: delegate (List<string> accumulator, string next)
                {
                    accumulator.Add(next);
                    return accumulator;
                });
        }
    }
}
