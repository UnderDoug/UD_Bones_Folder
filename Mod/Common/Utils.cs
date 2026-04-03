using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL;
using XRL.Language;
using XRL.World.Effects;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using static UD_Bones_Folder.Mod.Const;

namespace UD_Bones_Folder.Mod
{
    [HasVariableReplacer]
    public static class Utils
    {
        public static ModInfo ThisMod => ModManager.GetMod(MOD_ID);

        public static string AuthorOnPlatforms => $"{ThisMod.Manifest.Author} on GitHub (UnderDoug), on Discord (.underdoug), or on the Steam Workshop (UnderDoug)";

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
    }
}
