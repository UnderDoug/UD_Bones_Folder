using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL;
using XRL.Names;
using XRL.World;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod.Moderation
{
    [Serializable]
    public class OriginalDisplayName : IComposite
    {
        public string BaseName;
        public string Adjectives;
        public KeyValuePair<string, int> SizeAdjective;
        public string[] FactionAdjective;
        public string[] Honorifics;
        public string[] Titles;
        public string[] Epithets;

        public IEnumerable<KeyValuePair<string, string>> GetDebugPairs()
        {
            yield return new (nameof(BaseName), BaseName ?? "none");
            yield return new (nameof(Adjectives), Adjectives ?? "none");

            string sizeAdjective = "none";
            if (SizeAdjective.Key != null)
                sizeAdjective = $"{SizeAdjective.Key ?? "none"}: {SizeAdjective.Value}";
            yield return new(nameof(SizeAdjective), sizeAdjective);

            string factionAdjective = "none";
            if (!FactionAdjective.IsNullOrEmpty())
                factionAdjective = FactionAdjective.Aggregate("", (a, n) => Utils.CommaSpaceDelimitedAggregator(a, n ?? "none"));
            yield return new(nameof(FactionAdjective), factionAdjective);

            string honorifics = "none";
            if (!Honorifics.IsNullOrEmpty())
                honorifics = Honorifics.Aggregate("", (a, n) => Utils.CommaSpaceDelimitedAggregator(a, n ?? "none"));
            yield return new(nameof(Honorifics), honorifics);

            string titles = "none";
            if (!Titles.IsNullOrEmpty())
                titles = Titles.Aggregate("", (a, n) => Utils.CommaSpaceDelimitedAggregator(a, n ?? "none"));
            yield return new(nameof(Titles), titles);

            string epithets = "none";
            if (!Epithets.IsNullOrEmpty())
                epithets = Epithets.Aggregate("", (a, n) => Utils.CommaSpaceDelimitedAggregator(a, n ?? "none")); ;
            yield return new(nameof(Epithets), epithets);
        }

        public IEnumerable<string> GetDebugLines()
        {
            foreach ((var key, var value) in GetDebugPairs().IteratorSafe())
                yield return $"{key}: {value}"
                    ;
        }

        public string DebugString()
            => GetDebugLines()
                .IteratorSafe()
                .Aggregate((string)null, Utils.NewLineDelimitedAggregator)
            ;

        public static string GetErrorText(
            string MethodName,
            GameObject Object,
            int ModerationActions
            )
            => $"{MethodName} for {Object?.DebugName ?? "NO_OBJECT"}, {Math.Abs(ModerationActions).Things("too many action")}"
            ;

        public static InvalidOperationException GetInvalidModerationOperationException()
            => new("More moderation actions were reversed than were originally taken.")
            ;

        public bool RestoreBaseName(
            GameObject Object,
            ref int ModerationActions,
            bool ClearAfter = false
            )
        {
            if (BaseName != null
                && Object != null)
            {
                Object.DisplayName = BaseName;
                ModerationActions--;

                if (ModerationActions < 0)
                    Utils.Error(GetErrorText(nameof(RestoreBaseName), Object, ModerationActions), GetInvalidModerationOperationException());

                if (ClearAfter)
                    BaseName = null;

                return true;
            }
            return false;
        }

        public bool RestoreAdjectives(
            GameObject Object,
            ref int ModerationActions,
            bool ClearAfter = false
            )
        {
            if (Adjectives != null
                && Object != null)
            {
                Object.RequirePart<DisplayNameAdjectives>().Adjectives = Adjectives;
                ModerationActions--;

                if (ModerationActions < 0)
                    Utils.Error(GetErrorText(nameof(RestoreAdjectives), Object, ModerationActions), GetInvalidModerationOperationException());

                if (ClearAfter)
                    Adjectives = null;

                return true;
            }
            return false;
        }

        public bool RestoreSizeAdjective(
            GameObject Object,
            ref int ModerationActions,
            bool ClearAfter = false
            )
        {
            if (SizeAdjective.Key != null
                && Object != null)
            {
                var sizeAdjective = Object.RequirePart<SizeAdjective>();
                sizeAdjective.Adjective = SizeAdjective.Key;
                sizeAdjective.OrderAdjust = SizeAdjective.Value;
                ModerationActions--;

                if (ModerationActions < 0)
                    Utils.Error(GetErrorText(nameof(RestoreSizeAdjective), Object, ModerationActions), GetInvalidModerationOperationException());

                if (ClearAfter)
                    SizeAdjective = default;

                return true;
            }
            return false;
        }

        public bool RestoreFactionAdjective(
            GameObject Object,
            ref int ModerationActions,
            bool ClearAfter = false
            )
        {
            if (!FactionAdjective.IsNullOrEmpty()
                && Object != null)
            {
                var factionAdjective = Object.RequirePart<DisplayNameFactionAdjective>();
                factionAdjective.Faction = FactionAdjective[0];
                factionAdjective.FactionAdjective = FactionAdjective[1];
                factionAdjective.NonFactionAdjective = FactionAdjective[2];
                ModerationActions--;

                if (ModerationActions < 0)
                    Utils.Error(GetErrorText(nameof(RestoreFactionAdjective), Object, ModerationActions), GetInvalidModerationOperationException());

                if (ClearAfter)
                    FactionAdjective = null;

                return true;
            }
            return false;
        }

        public bool RestoreHonorifics(
            GameObject Object,
            ref int ModerationActions,
            bool ClearAfter = false
            )
        {
            if (!Honorifics.IsNullOrEmpty()
                && Object != null)
            {
                var honorifics = Object.RequirePart<Honorifics>();
                honorifics.HonorificList = Honorifics[0];
                honorifics.HonorificOrder = Honorifics[1];
                ModerationActions--;

                if (ModerationActions < 0)
                    Utils.Error(GetErrorText(nameof(RestoreHonorifics), Object, ModerationActions), GetInvalidModerationOperationException());

                if (ClearAfter)
                    Honorifics = null;

                return true;
            }
            return false;
        }

        public bool RestoreTitles(
            GameObject Object,
            ref int ModerationActions,
            bool ClearAfter = false
            )
        {
            if (!Titles.IsNullOrEmpty()
                && Object != null)
            {
                var titles = Object.RequirePart<Titles>();
                titles.TitleList = Titles[0];
                titles.TitleOrder = Titles[1];
                ModerationActions--;

                if (ModerationActions < 0)
                    Utils.Error(GetErrorText(nameof(RestoreTitles), Object, ModerationActions), GetInvalidModerationOperationException());

                if (ClearAfter)
                    Titles = null;

                return true;
            }
            return false;
        }

        public bool RestoreEpithets(
            GameObject Object,
            ref int ModerationActions,
            bool ClearAfter = false
            )
        {
            if (!Epithets.IsNullOrEmpty()
                && Object != null)
            {
                var epithets = Object.RequirePart<Epithets>();
                epithets.EpithetList = Epithets[0];
                epithets.EpithetOrder = Epithets[1];
                ModerationActions--;

                if (ModerationActions < 0)
                    Utils.Error(GetErrorText(nameof(RestoreEpithets), Object, ModerationActions), GetInvalidModerationOperationException());

                if (ClearAfter)
                    Epithets = null;

                return true;
            }
            return false;
        }

        public bool RestoreDisplayName(
            GameObject Object,
            ref int ModerationActions,
            out bool AnyFailed,
            bool ClearAfter = false
            )
        {
            AnyFailed = false;

            if (Object == null)
                return false;

            try
            {
                RestoreBaseName(Object, ref ModerationActions, ClearAfter);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(RestoreDisplayName), nameof(RestoreBaseName)), x);
                AnyFailed = true;
            }

            try
            {
                RestoreAdjectives(Object, ref ModerationActions, ClearAfter);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(RestoreDisplayName), nameof(RestoreAdjectives)), x);
                AnyFailed = true;
            }

            try
            {
                RestoreSizeAdjective(Object, ref ModerationActions, ClearAfter);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(RestoreDisplayName), nameof(RestoreSizeAdjective)), x);
                AnyFailed = true;
            }

            try
            {
                RestoreFactionAdjective(Object, ref ModerationActions, ClearAfter);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(RestoreDisplayName), nameof(RestoreFactionAdjective)), x);
                AnyFailed = true;
            }

            try
            {
                RestoreHonorifics(Object, ref ModerationActions, ClearAfter);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(RestoreDisplayName), nameof(RestoreHonorifics)), x);
                AnyFailed = true;
            }

            try
            {
                RestoreTitles(Object, ref ModerationActions, ClearAfter);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(RestoreDisplayName), nameof(RestoreTitles)), x);
                AnyFailed = true;
            }

            try
            {
                RestoreEpithets(Object, ref ModerationActions, ClearAfter);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(RestoreDisplayName), nameof(RestoreEpithets)), x);
                AnyFailed = true;
            }

            return true;
        }

        public void Clear()
        {
            BaseName = null;
            Adjectives = null;
            SizeAdjective = default;
            FactionAdjective = null;
            Honorifics = null;
            Titles = null;
            Epithets = null;
        }
    }
}
