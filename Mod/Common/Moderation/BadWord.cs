using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

using Platform.IO;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.Wish;
using XRL.World;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod.Moderation
{
    [HasModSensitiveStaticCache]
    [HasWishCommand]
    [JsonObject(MemberSerialization.OptIn)]
    [Serializable]
    public class BadWord : IComposite
    {
        [Serializable]
        public enum SeverityLevel
        {
            All,
            Mild,
            Medium,
            Strong,
            Severe,
            None,
        }

        [Serializable]
        public class BadWordEqualityComparer : EqualityComparer<BadWord>, IComposite
        {
            public override bool Equals(BadWord x, BadWord y)
                => x == null
                    || y == null
                ? (x == null) == (y == null)
                : x.ID == y.ID
                ;

            public override int GetHashCode(BadWord obj)
                => obj.ID?.GetHashCode()
                ?? 0
                ;
        }

        public const SeverityLevel DefaultSeverity = SeverityLevel.Strong;

        public static BadWordEqualityComparer DefaultEqualityComparer = new BadWordEqualityComparer();

        public static string FilterFileName => "profanity-list.json";
        private static string _FilterFilePath;
        public static string FilterFilePath => _FilterFilePath ??= Utils.ThisMod.Files.FirstOrDefault(f => f.Name == FilterFileName).FullName;

        private static HashSet<BadWord> _BadWordsSet;
        public static HashSet<BadWord> BadWordsSet
        {
            get
            {
                if (_BadWordsSet == null)
                {
                    if (File.Exists(FilterFilePath)
                        && File.ReadAllJson<BadWord[]>(FilterFilePath) is BadWord[] badWords)
                    {
                        _BadWordsSet = new(badWords, DefaultEqualityComparer);
                        foreach (var badWord in _BadWordsSet)
                            badWord.Init();
                    }
                }
                return _BadWordsSet;
            }
        }

        [ModSensitiveStaticCache(createEmptyInstance: true)]
        public static Dictionary<SeverityLevel, string> MegaPatterns = new();

        [ModSensitiveStaticCache(createEmptyInstance: true)]
        public static Dictionary<string, bool> FilterResultCache = new();

        public static RegexOptions DefaultOptions => RegexOptions.IgnoreCase | RegexOptions.Multiline;

        public static string WildCard => "\\w*";

        /// <summary>
        /// String in lowercase characters used as a unique ID for the profanity.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public string ID;

        /// <summary>
        /// String in lowercase characters. Asterisks (*) can be used to indicate the previous character can have one or more appearances. Pipes (|) can be used as a separator if matching multiple terms under this profanity.
        /// </summary>
        [JsonProperty(PropertyName = "match")]
        public string Match;

        /// <summary>
        /// Integer from 1 to 4 corresponding to a supported <see href="https://github.com/dsojevic/profanity-list/tree/main#severity-levels">Severity Level</see>.
        /// </summary>
        [JsonProperty(PropertyName = "severity")]
        public SeverityLevel Severity;

        /// <summary>
        /// An otional array of lowercase strings to indicate how this profanity is tagged. These should be in English.
        /// </summary>
        [JsonProperty(PropertyName = "tags", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Tags;

        /// <summary>
        /// Whether or not this profanity should be used with partial matching. Explicitly set to false when the match may otherwise have hundreds or thousands of exceptions.
        /// </summary>
        [JsonProperty(PropertyName = "allow_partial")]
        public bool AllowPartial;

        /// <summary>
        /// An otional array of lowercase strings indicating exceptions to this profanity if using partial matching. An asterisk (*) is used as a placeholder for the matched word.
        /// </summary>
        /// <example>
        /// sp* would be a valid exception ('sparse') for the profanity arse if relying on partial matching.
        /// </example>
        [JsonProperty(PropertyName = "exceptions", NullValueHandling = NullValueHandling.Ignore)]
        public string[] Exceptions;

        protected List<string> _Matches;
        public List<string> Matches
        {
            get
            {
                if (!Match.IsNullOrEmpty())
                {
                    if (Match.Contains('|'))
                        _Matches ??= Match.Split('|').ToList();
                    else
                        _Matches ??= new List<string> { Match };
                }
                return _Matches;
            }
        }

        protected HashSet<string> Patterns;

        protected HashSet<string> ProcessedExceptions;

        public BadWord()
        {
            AllowPartial = true;
        }

        [ModSensitiveCacheInit]
        public static void InitCache()
        {
            _BadWordsSet?.Clear();
            _BadWordsSet = null;
            _ = BadWordsSet;

            if (BadWordsSet != null)
            {
                Utils.Info($"Stored {BadWordsSet.Count.Things($"{nameof(BadWord)} entry")} ({BadWordsSet.Aggregate(0, (a, n) => a + (n.Patterns?.Count ?? 0)).Things($"pattern")}) in {nameof(BadWordsSet)}...");

                var tags = new Dictionary<string, int>();
                var ratings = new Dictionary<SeverityLevel, int>();
                foreach (var badWord in BadWordsSet)
                {
                    if (!ratings.ContainsKey(badWord.Severity))
                        ratings[badWord.Severity] = 0;
                    ratings[badWord.Severity]++;

                    foreach (var tag in badWord.Tags.IteratorSafe())
                    {
                        if (!tags.ContainsKey(tag))
                            tags[tag] = 0;
                        tags[tag]++;
                    }
                }

                Utils.Log($"{nameof(BadWord)} tag counts:");
                tags.Loggregate(
                    Proc: kvp => $"{kvp.Key}: {kvp.Value}",
                    Empty: "none",
                    PostProc: s => $"{1.Indent()}: {s}");

                Utils.Log($"{nameof(BadWord)} severity counts:");
                ratings.Loggregate(
                    Proc: kvp => $"{kvp.Key} ({(int)kvp.Key}): {kvp.Value}",
                    Empty: "none",
                    PostProc: s => $"{1.Indent()}: {s}");

                MegaPatterns ??= new();
                MegaPatterns.Clear();

                if (!MegaPatterns.TryGetValue(SeverityLevel.Mild, out string megaPattern))
                {
                    megaPattern = BadWordsSet.ProduceMegaPattern(b => b.Severity >= SeverityLevel.Mild);
                    MegaPatterns[SeverityLevel.Mild] = megaPattern;
                    Utils.Info($"Produced a mega pattern for minimum severity level {SeverityLevel.Mild} ({(int)SeverityLevel.Mild}): {megaPattern.Length:#,##0} characters.");
                }

                if (!MegaPatterns.TryGetValue(SeverityLevel.Medium, out megaPattern))
                {
                    megaPattern = BadWordsSet.ProduceMegaPattern(b => b.Severity >= SeverityLevel.Medium);
                    MegaPatterns[SeverityLevel.Medium] = megaPattern;
                    Utils.Info($"Produced a mega pattern for minimum severity level {SeverityLevel.Medium} ({(int)SeverityLevel.Medium}): {megaPattern.Length:#,##0} characters.");
                }

                if (!MegaPatterns.TryGetValue(SeverityLevel.Strong, out megaPattern))
                {
                    megaPattern = BadWordsSet.ProduceMegaPattern(b => b.Severity >= SeverityLevel.Strong);
                    MegaPatterns[SeverityLevel.Strong] = megaPattern;
                    Utils.Info($"Produced a mega pattern for minimum severity level {SeverityLevel.Strong} ({(int)SeverityLevel.Strong}): {megaPattern.Length:#,##0} characters.");
                }

                if (!MegaPatterns.TryGetValue(SeverityLevel.Severe, out megaPattern))
                {
                    megaPattern = BadWordsSet.ProduceMegaPattern(b => b.Severity >= SeverityLevel.Severe);
                    MegaPatterns[SeverityLevel.Severe] = megaPattern;
                    Utils.Info($"Produced a mega pattern for minimum severity level {SeverityLevel.Severe} ({(int)SeverityLevel.Severe}): {megaPattern.Length:#,##0} characters.");
                }

                Loading.LoadTask($"Caching profanity filter results...", CacheMostObjectBlueprints);
            }

            FilterResultCache ??= new();
            FilterResultCache.Clear();
        }

        public static void CacheMostObjectBlueprints()
        {
            var models = GameObjectFactory.Factory?.GetBlueprintsInheritingFrom("PhysicalObject");

            if (models.IsNullOrEmpty())
            {
                Utils.Info($"{nameof(GameObjectFactory)} not initialized yet.");
                return;
            }

            foreach (var model in models.IteratorSafe())
            {
                if (model.InheritsFromSafe("Widget")
                    || model.IsBaseBlueprint())
                    continue;

                model.CacheModelNotFiltered();
            }
        }

        public BadWord Init()
        {
            Patterns = new HashSet<string>();
            foreach (var match in Matches.IteratorSafe())
                if (ProducePattern(match, AllowPartial, Exceptions) is string pattern)
                    Patterns.Add(pattern);

            return this;
        }

        public bool HasTag(string Tag)
            => Tags?.Contains(Tag) is true
            ;

        public static string ProcessMatchException(string Match, string Exception, bool AllowPartial = true)
        {
            if (Match.IsNullOrEmpty()
                || Exception.IsNullOrEmpty()
                || !Exception.Contains('*'))
                return null;

            string exception = Exception.Replace("*", Match);

            if (!AllowPartial)
                return $"\\b{exception}\\b";

            return $"{WildCard}{exception}{WildCard}";
        }

        public static string ProcessMatchString(string Match, bool AllowPartial = true)
        {
            Match = Match.Replace('*', '+');
            string pattern = Match;

            if (!AllowPartial)
                return $"(\\b{pattern}\\b)+";

            return $"{WildCard}{pattern}{WildCard}";
        }

        public static string ProcessMatchExceptionString(string Match, string[] Exceptions = null)
        {
            return Exceptions.IteratorSafe().Aggregate(
                seed: "",
                func: delegate (string acc, string next)
                {
                    if (ProcessMatchException(Match, next) is not string exception)
                        return acc;
                    return Utils.DelimitedAggregator(acc, exception, "|");
                });
        }

        public static string ProducePattern(string Match, bool AllowPartial = true, string[] Exceptions = null)
        {
            Match = Match.Replace('*', '+');
            string pattern = Match;

            if (!AllowPartial)
                return $"(\\b{pattern}\\b)+";

            pattern = $"{WildCard}{pattern}{WildCard}";

            string exceptions = ProcessMatchExceptionString(Match, Exceptions);

            if (!exceptions.IsNullOrEmpty())
                pattern = $"(?:{exceptions}|({pattern}))+";

            return pattern;
        }

        public bool Check(string Text, int Indent = 0)
        {
            if (Text.IsNullOrEmpty())
                return false;

            foreach (var pattern in Patterns.IteratorSafe())
            {
                //Utils.Log($"{Indent.Indent()}testing: {pattern}");
                if (pattern.MatchesCaptureGroups(Text, DefaultOptions))
                    return true;
            }

            return false;
        }

        public static bool HasBadWord(string Text)
            => Text.HasBadWord(Options.ModerationMinimumSeverityLevel)
            ;

        public static bool HasBadWord(GameObject GameObject)
        {
            var model = GameObject.GetBlueprint();
            try
            {
                var severity = Options.ModerationMinimumSeverityLevel;
                Utils.Log($"{model.Name} ({model.DisplayName()}), {nameof(Options.ModerationMinimumSeverityLevel)}: {(int)severity} ({severity})");
                if (GameObject.HasBadWord(out var badDisplayName, out bool inDescription, MinimumLevel: severity, IgnoreCache: true))
                {
                    badDisplayName.DebugStrings().Loggregate(
                        Proc: s => $"] {s}",
                        Empty: "] empty",
                        PostProc: s => $"{2.Indent()}{s}");
                    Utils.Log($"{2.Indent()}{nameof(inDescription)}: {inDescription}");
                    return true;
                }
            }
            catch (Exception x)
            {
                Utils.Log($"{model.Name} ({model.DisplayName()}): Exception, {x}");
            }
            return false;
        }

        #region Wishes

        [WishCommand(Command = "badword test")]
        public static bool TestBadWord_WishHandler(string Test)
        {
            if (Test.IsNullOrEmpty())
                return false;

            Utils.Log($"Testing Profanity Filter on provided test text:");
            Utils.Log($"Minimum severity level is {(int)Options.ModerationMinimumSeverityLevel} ({Options.ModerationMinimumSeverityLevel})");
            Utils.Log(Test);
            bool result = Test.HasBadWord(Options.ModerationMinimumSeverityLevel);
            Utils.Log($"{1.Indent()}Contains a match: {result}");

            return true;
        }

        [WishCommand(Command = "badword test blueprints")]
        public static bool TestBlueprints_WishHandler(string Parameters)
        {
            SeverityLevel severity = DefaultSeverity;
            if (!Parameters.IsNullOrEmpty()
                && Parameters.Split(' ') is string[] parameters
                && !parameters.IsNullOrEmpty())
            {
                if (int.TryParse(parameters[0], out int severityInt))
                    severity = (SeverityLevel)Math.Clamp(severityInt, (int)SeverityLevel.All, (int)SeverityLevel.Severe);
                else
                if (!Enum.TryParse(parameters[0], out severity))
                    severity = DefaultSeverity;
            }

            var models = GameObjectFactory.Factory.GetBlueprintsInheritingFrom("Object").Where(model => !model.InheritsFromSafe("Widget") && !model.IsBaseBlueprint());

            GameObject dummyObject = null;
            int modelsCount = models.Count();
            int counter = 0;

            Utils.Log($"Performing {nameof(BadWord)} test on {modelsCount.Things("object")} it's possible to encounter in game...");
            Utils.Log($"Minimum severity level is {(int)severity} ({severity})");
            foreach (var model in models)
            {
                string counterString = $"{counter++}".PadLeft(modelsCount.ToString().Length, ' ');
                Loading.SetLoadingStatus($"Checking blueprint: {counterString}/{modelsCount}");
                try
                {
                    SerializationExtensions.PerformSilently(delegate ()
                    {
                        dummyObject = model.createSample();
                    });
                    if (dummyObject.HasBadWord(out var badDisplayName, out bool inDescription, MinimumLevel: severity))
                    {
                        dummyObject.HasBadWord(out badDisplayName, out inDescription, MinimumLevel: severity, IgnoreCache: true);
                        Utils.Log($"^^^# {model.Name} ({model.DisplayName()})");
                        badDisplayName.DebugStrings().Loggregate(
                            Proc: s => $"] {s}",
                            Empty: "] empty",
                            PostProc: s => $"{2.Indent()}{s}");
                        Utils.Log($"{2.Indent()}{nameof(inDescription)}: {inDescription}");
                    }
                }
                catch (Exception x)
                {
                    Utils.Log($"{model.Name} ({model.DisplayName()}): Exception, {x}");
                }
                finally
                {
                    SerializationExtensions.PerformSilently(delegate ()
                    {
                        dummyObject?.Obliterate(Silent: true);
                        dummyObject = null;
                    });
                }
            }
            Loading.SetLoadingStatus(null);
            return true;
        }

        [WishCommand(Command = "badword test blueprints")]
        public static bool TestBlueprints_WishHandler()
            => TestBlueprints_WishHandler(null)
            ;

        #endregion
    }

    public static class BadWordExtensions
    {
        public static string ProduceMegaPattern(this HashSet<BadWord> BadWordSet, Predicate<BadWord> Where)
        {
            using var matches = ScopeDisposedList<string>.GetFromPool();
            using var exceptions = ScopeDisposedList<string>.GetFromPool();
            foreach (var badWord in BadWordSet.IteratorSafe())
            {
                if (Where?.Invoke(badWord) is false)
                    continue;

                foreach (var match in badWord.Matches.IteratorSafe())
                {
                    if (BadWord.ProcessMatchString(match, badWord.AllowPartial) is string matchPattern
                        && !matches.Contains(matchPattern))
                    {
                        matches.Add(matchPattern);
                    }
                    if (BadWord.ProcessMatchExceptionString(match, badWord.Exceptions) is string exceptionPattern
                        && !exceptions.Contains(exceptionPattern))
                    {
                        exceptions.Add(exceptionPattern);
                    }
                }
            }
            if (matches.IsNullOrEmpty()
                || matches.Aggregate("", (a, n) => Utils.DelimitedAggregator(a, $"({n})", "|")) is not string matchesString
                || matchesString.IsNullOrEmpty())
                return null;

            if (exceptions.IsNullOrEmpty()
                || exceptions.Aggregate("", (a, n) => Utils.DelimitedAggregator(a, n, "|")) is not string exceptionsString
                || exceptionsString.IsNullOrEmpty())
                return $"(?:{matchesString})+";

            return $"(?:{exceptionsString}|{matchesString})+";
        }

        public static bool HasBadWord(
            this string Text,
            BadWord.SeverityLevel MinimumLevel = BadWord.DefaultSeverity,
            bool IgnoreCache = false
            )
        {
            if (Text.IsNullOrEmpty())
                return false;

            string text = Text.Strip();

            if (!BadWord.FilterResultCache.TryGetValue(text, out bool result)
                || !IgnoreCache)
            {
                result = MinimumLevel == BadWord.SeverityLevel.All;

                if (!result
                    && MinimumLevel != BadWord.SeverityLevel.None)
                {
                    if (!BadWord.MegaPatterns.TryGetValue(MinimumLevel, out string megaPattern))
                    {
                        megaPattern = BadWord.BadWordsSet.ProduceMegaPattern(b => b.Severity >= MinimumLevel);
                        BadWord.MegaPatterns[MinimumLevel] = megaPattern;
                        Utils.Info($"produced the following mega pattern for minimum severity level {MinimumLevel} ({(int)MinimumLevel}):\n{megaPattern}");
                    }
                    if (!megaPattern.IsNullOrEmpty())
                        result = megaPattern.MatchesCaptureGroups(Text, BadWord.DefaultOptions);
                    else
                        result = BadWord.BadWordsSet.IteratorSafe().Any(b => b.Severity >= MinimumLevel && b.Check(text));
                }

                if (!IgnoreCache)
                    BadWord.FilterResultCache[text] = result;
            }

            return result;
        }

        private static bool HasBadWord(
            this Description Description,
            BadWord.SeverityLevel MinimumLevel = BadWord.DefaultSeverity,
            bool IgnoreCache = false
            )
        {
            try
            {
                return Description?.GetShortDescription(AsIfKnown: true, NoConfusion: true) is string description
                && description.HasBadWord(MinimumLevel, IgnoreCache)
                ;
            }
            catch (Exception x)
            {
                // Utils.Warn($"Error while {nameof(BadWord)} checking description of {Description?.ParentObject?.DebugName ?? "NO_OBJECT"}", x);
                return false;
            }
        }

        public static bool RequireFilterCacheResult(string Key, Func<bool> Proc)
        {
            BadWord.FilterResultCache ??= new();
            var filterResultCache = BadWord.FilterResultCache;

            string key = Key.Strip();
            if (!filterResultCache.ContainsKey(key))
                filterResultCache[key] = Proc?.Invoke() is true;

            return filterResultCache[key];
        }

        public static bool RequireFilterCacheResult(string Key, bool Value)
            => RequireFilterCacheResult(Key, () => Value)
            ;

        public static bool CacheModelNotFiltered(this GameObjectBlueprint Model)
        {
            if (Model == null)
                return false;

            BadWord.FilterResultCache ??= new();
            var filterResultCache = BadWord.FilterResultCache;

            if (Model.DisplayName() is string baseDisplayName)
                RequireFilterCacheResult(baseDisplayName, false);

            if (Model.GetPartParameter<string>(nameof(DisplayNameAdjectives), nameof(DisplayNameAdjectives.Adjectives)) is string adjectives)
                RequireFilterCacheResult(adjectives, false);

            if (Model.GetPartParameter<string>(nameof(SizeAdjective), nameof(SizeAdjective.Adjective)) is string adjective)
                RequireFilterCacheResult(adjective, false);

            if (Model.GetPartParameter<string>(nameof(DisplayNameFactionAdjective), nameof(DisplayNameFactionAdjective.FactionAdjective)) is string factionAdjective)
                RequireFilterCacheResult(factionAdjective, false);
            if (Model.GetPartParameter<string>(nameof(DisplayNameFactionAdjective), nameof(DisplayNameFactionAdjective.NonFactionAdjective)) is string nonFactionAdjective)
                RequireFilterCacheResult(nonFactionAdjective, false);

            if (Model.GetPartParameter<string>(nameof(Honorifics), nameof(Honorifics.HonorificList)) is string honorificList)
                RequireFilterCacheResult(honorificList, false);
            if (Model.GetPartParameter<string>(nameof(Honorifics), nameof(Honorifics.HonorificOrder)) is string honorificOrder)
                RequireFilterCacheResult(honorificOrder, false);

            if (Model.GetPartParameter<string>(nameof(Titles), nameof(Titles.TitleList)) is string titleList)
                RequireFilterCacheResult(titleList, false);
            if (Model.GetPartParameter<string>(nameof(Titles), nameof(Titles.TitleOrder)) is string titleOrder)
                RequireFilterCacheResult(titleOrder, false);

            if (Model.GetPartParameter<string>(nameof(Epithets), nameof(Epithets.EpithetList)) is string epithetList)
                RequireFilterCacheResult(epithetList, false);
            if (Model.GetPartParameter<string>(nameof(Epithets), nameof(Epithets.EpithetOrder)) is string epithetOrder)
                RequireFilterCacheResult(epithetOrder, false);

            if (Model.GetPartParameter<string>(nameof(Description), nameof(Description._Short)) is string shortDesc)
                RequireFilterCacheResult(shortDesc, false);

            return true;
        }

        public static bool HasBadWord(
            this GameObject Object,
            out BadDisplayName BadDisplayName,
            out bool InDescription,
            BadWord.SeverityLevel MinimumLevel = BadWord.DefaultSeverity,
            bool IgnoreCache = false
            )
        {
            InDescription = false;
            BadDisplayName = null;
            if (Object == null)
                return false;

            if (Object.BaseDisplayName is string baseDisplayName
                && baseDisplayName.HasBadWord(MinimumLevel, IgnoreCache))
            {
                BadDisplayName ??= new();
                BadDisplayName.IsBase = true;
            }

            if (Object.TryGetPart(out DisplayNameAdjectives adjectives)
                && adjectives.Adjectives.HasBadWord(MinimumLevel, IgnoreCache))
            {
                BadDisplayName ??= new();
                BadDisplayName.IsAdjective = true;
            }

            if (Object.TryGetPart(out SizeAdjective sizeAdjective)
                && sizeAdjective.Adjective.HasBadWord(MinimumLevel, IgnoreCache))
            {
                BadDisplayName ??= new();
                BadDisplayName.IsSizeAdjective = true;
            }

            if (Object.TryGetPart(out DisplayNameFactionAdjective factionAdjective)
                && (factionAdjective.FactionAdjective.HasBadWord(MinimumLevel, IgnoreCache)
                    || factionAdjective.NonFactionAdjective.HasBadWord(MinimumLevel, IgnoreCache)))
            {
                BadDisplayName ??= new();
                BadDisplayName.IsFactionAdjective = true;
            }

            if (Object.TryGetPart(out Honorifics honorifics)
                && (honorifics.HonorificList.HasBadWord(MinimumLevel, IgnoreCache)
                    || honorifics.HonorificOrder.HasBadWord(MinimumLevel, IgnoreCache)))
            {
                BadDisplayName ??= new();
                BadDisplayName.IsHonorific = true;
            }

            if (Object.TryGetPart(out Titles titles)
                && (titles.TitleList.HasBadWord(MinimumLevel, IgnoreCache)
                    || titles.TitleOrder.HasBadWord(MinimumLevel, IgnoreCache)))
            {
                BadDisplayName ??= new();
                BadDisplayName.IsTitle = true;
            }

            if (Object.TryGetPart(out Epithets epithets)
                && (epithets.EpithetList.HasBadWord(MinimumLevel, IgnoreCache)
                    || epithets.EpithetOrder.HasBadWord(MinimumLevel, IgnoreCache)))
            {
                BadDisplayName ??= new();
                BadDisplayName.IsEpithet = true;
            }

            if (Object.TryGetPart(out Description description)
                && description.HasBadWord(MinimumLevel, IgnoreCache))
                InDescription = true;

            return BadDisplayName != null
                || InDescription
                ;
        }
    }
}
