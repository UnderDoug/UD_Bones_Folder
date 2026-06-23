using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Platform.IO;

using UD_Bones_Folder.Mod.Serialization;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;

using SeverityLevel = UD_Bones_Folder.Mod.Moderation.IBadWord.SeverityLevel;

namespace UD_Bones_Folder.Mod.Moderation
{
    [HasModSensitiveStaticCache]
    [Serializable]
    public class BadWordSet : CompositeSet<BadWord>, IComposite, IBadWord
    {
        public static string FilterFileName => "profanity-list.json";
        private static string _FilterFilePath;
        public static string FilterFilePath => _FilterFilePath ??= Utils.ThisMod.Files.FirstOrDefault(f => f.Name == FilterFileName).FullName;

        [ModSensitiveStaticCache(CreateEmptyInstance = false)]
        private static Dictionary<SeverityLevel, BadWordSet> _BadWordSetCache;
        public static Dictionary<SeverityLevel, BadWordSet> BadWordSetCache => _BadWordSetCache;

        [ModSensitiveStaticCache(CreateEmptyInstance = false)]
        private static BadWordSet _AllBadWords;
        public static BadWordSet AllBadWords
        {
            get
            {
                if (_AllBadWords == null)
                {
                    try
                    {
                        if (File.Exists(FilterFilePath)
                            && File.ReadAllJson<BadWord[]>(FilterFilePath) is BadWord[] badWords)
                        {
                            _AllBadWords = new BadWordSet(badWords.IteratorSafe()) { Severity = SeverityLevel.All };
                            foreach (var badWord in _AllBadWords)
                                badWord.Init();
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to initialize {nameof(AllBadWords)}. There won't be a profanity filter until the mod sensitive static cache is refreshed", x);
                        _AllBadWords = new(BadWord.DefaultEqualityComparer, Coalescer<BadWord>.Default);
                    }
                }
                return _AllBadWords;
            }
        }

        public static Dictionary<SeverityLevel, string> MegaPatterns = new();

        public static Dictionary<string, bool> FilterResultCache = new();

        private static bool _Original_DebugEnableBadWordFilterTestWords = Options.DebugEnableBadWordFilterTestWords;
        public static bool Original_DebugEnableBadWordFilterTestWords
        {
            get => _Original_DebugEnableBadWordFilterTestWords;
            set
            {
                if (_Original_DebugEnableBadWordFilterTestWords != value)
                {
                    _Original_DebugEnableBadWordFilterTestWords = value;
                    InitCache();
                }
            }
        }

        private SeverityLevel severity;
        /// <summary>
        /// Represents the minimum <see cref="SeverityLevel"/> captured by this set. Only words this severe or worse will be filtered.
        /// </summary>
        public SeverityLevel Severity
        {
            get => severity;
            set => severity = value;
        }

        public BadWordSet()
            : base(BadWord.DefaultEqualityComparer, Coalescer<BadWord>.Default)
        { }

        public BadWordSet(
            int Capacity,
            CompositeEqualityComparer<BadWord> EqualityComparer,
            Coalescer<BadWord> Coalescer
            )
            : base(Capacity, EqualityComparer ?? BadWord.DefaultEqualityComparer, Coalescer ?? Coalescer<BadWord>.Default)
        {
        }

        public BadWordSet(int Capacity)
            : this(Capacity, null, null)
        { }

        public BadWordSet(CompositeEqualityComparer<BadWord> EqualityComparer, Coalescer<BadWord> Coalescer)
            : this(0, EqualityComparer, Coalescer)
        { }

        public BadWordSet(
            IEnumerable<BadWord> Enumerable,
            CompositeEqualityComparer<BadWord> EqualityComparer,
            Coalescer<BadWord> Coalescer
            )
            : this(Enumerable?.Count() ?? 0, EqualityComparer, Coalescer)
        {
            if (!Enumerable.IsNullOrEmpty())
                AddRange(Enumerable);
        }

        public BadWordSet(IEnumerable<BadWord> Enumerable)
            : this(Enumerable, null, null)
        { }

        public BadWordSet(
            IReadOnlyList<BadWord> List,
            CompositeEqualityComparer<BadWord> EqualityComparer,
            Coalescer<BadWord> Coalescer
            )
            : this(List.IteratorSafe(), EqualityComparer, Coalescer)
        { }

        public BadWordSet(IReadOnlyList<BadWord> List)
            : this(List, null, null)
        { }

        [ModSensitiveCacheInit]
        public static void Init()
        {
            Loading.LoadTask($"Initializing bad word set...", InitializeBadWordSetsInternal);
            InitCache();
        }

        public static void InitCache()
        {
            Loading.LoadTask($"Caching bad word filter mega patterns...", CacheMegaPatterns);
            Loading.LoadTask($"Caching compiled Regicies for bad word filter ...", CacheCompiledRegex);
            Loading.LoadTask($"Caching blueprint strings for bad word filter ...", CacheMostObjectBlueprints);
        }

        public static void InitializeBadWordSets()
            => InitializeBadWordSets(CalledManually: true)
            ;

        private static void InitializeBadWordSetsInternal()
            => InitializeBadWordSets(CalledManually: false)
            ;

        private static void InitializeBadWordSets(bool CalledManually)
        {
            if (CalledManually)
                Utils.Warn($"{nameof(BadWordSet)}.{nameof(InitializeBadWordSets)} {nameof(CalledManually)}: {CalledManually}", new Exception());

            AllBadWords?.Clear();
            _AllBadWords = null;
            _ = AllBadWords;

            _BadWordSetCache ??= new();
            BadWordSetCache?.Clear();

            if (AllBadWords != null)
            {
                int patternCount = AllBadWords.Aggregate(0, (a, n) => a + (n.Patterns?.Count ?? 0));
                string badWordEntries = AllBadWords.Count.Things($"{nameof(BadWord)} entry");
                Utils.Info($"Stored {badWordEntries} ({patternCount.Things($"pattern")}) in {nameof(AllBadWords)}...");

                var tags = new Dictionary<string, int>();
                var ratings = new Dictionary<SeverityLevel, int>();
                foreach (var badWord in AllBadWords)
                {
                    BadWordSetCache.RequireEntry(badWord.Severity, ProcNewEntry: s => new BadWordSet
                    {
                        EqualityComparer = BadWord.DefaultEqualityComparer,
                        Coalescer = Coalescer<BadWord>.Default,
                        Severity = s,
                    }).Add(badWord);

                    ratings.Increment(badWord.Severity);

                    foreach (var tag in badWord.Tags.IteratorSafe())
                        tags.Increment(tag);
                }

                Utils.Log($"Unionising {nameof(BadWordSet)}s with higher {nameof(SeverityLevel)} peers...");
                foreach ((var minimumSeverity, var badWordSet) in BadWordSetCache.IteratorSafe())
                {
                    foreach (var iteratedSeverity in (IBadWord.SeverityLevelValueCache?.Values).IteratorSafe())
                    {
                        if (iteratedSeverity <= minimumSeverity)
                            continue;

                        if (!iteratedSeverity.IsTwixtInclusive(SeverityLevel.Mild, SeverityLevel.Severe))
                            continue;

                        if (BadWordSetCache.TryGetValue(iteratedSeverity, out var moreSevereBadWordSet))
                        {
                            badWordSet.UnionWith(moreSevereBadWordSet);
                            Utils.Log($"{1.Indent()}Added {iteratedSeverity.ToStringWithNum()} rated {nameof(BadWord)}s to {nameof(BadWordSet)}: {minimumSeverity.ToStringWithNum()}");
                        }
                    }
                }

                if (Options.DebugEnableBadWordFilterLogging)
                {
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
                }
            }
        }

        public static void CacheMegaPatterns()
        {
            MegaPatterns ??= new();
            MegaPatterns.Clear();
            if (AllBadWords != null)
            {
                Original_DebugEnableBadWordFilterTestWords = Options.DebugEnableBadWordFilterTestWords;

                IBadWord.SeverityLevelValueCache.ForEach(delegate(string name, SeverityLevel severity)
                {
                    if (!severity.IsTwixtInclusive(SeverityLevel.Mild, SeverityLevel.Severe))
                        return;

                    if (!MegaPatterns.TryGetValue(severity, out string megaPattern))
                    {
                        if (BadWordSetCache.IsNullOrEmpty())
                            InitializeBadWordSets();

                        if (BadWordSetCache.IsNullOrEmpty())
                        {
                            Utils.Warn($"Failed to initialize {nameof(BadWordSetCache)}. Unable to produce mega pattern for {severity.ToStringWithNum()}");
                            return;
                        }

                        if (!BadWordSetCache.TryGetValue(severity, out var badwordSet))
                        {
                            Utils.Warn($"{nameof(BadWordSetCache)} contians no entry for {severity.ToStringWithNum()}. Unable to produce mega pattern for severity level");
                            return;
                        }

                        megaPattern = badwordSet.ProduceTrieMegaPattern(severity);

                        MegaPatterns[severity] = megaPattern;
                        if (Options.DebugEnableBadWordFilterLogging)
                            Utils.Info($"Produced a trie mega pattern for minimum severity level {severity.ToStringWithNum()}: {megaPattern.Length:#,##0} characters.");
                    }
                });
            }
        }

        public static void CacheCompiledRegex()
        {
            foreach ((var severity, var pattern) in MegaPatterns.IteratorSafe())
            {
                _ = pattern.MatchesCaptureGroups("Text", BadWord.DefaultRegexOptions);

                if (Options.DebugEnableBadWordFilterLogging)
                    Utils.Info($"Compiled {nameof(Regex)} for minimum severity level {SeverityLevel.Severe} ({(int)SeverityLevel.Severe})");
            }
        }

        public static void CacheMostObjectBlueprints()
        {
            FilterResultCache ??= new();
            FilterResultCache.Clear();

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

        public IEnumerable<string> GetProcessedBadWordMatches(Predicate<BadWord> Where)
        {
            foreach (var badWord in this)
            {
                if (Where?.Invoke(badWord) is false)
                    continue;

                foreach (var match in badWord.Matches.IteratorSafe())
                    if (BadWord.ProcessMatchString(match, AllowPartial: true, IncludeWildCard: false) is string processedMatch)
                        yield return processedMatch;
            }
        }

        public IEnumerable<string> GetProcessedBadWordMatches()
            => GetProcessedBadWordMatches(null)
            ;

        public IEnumerable<string> GetProcessedBadWordExceptions(Predicate<BadWord> Where)
        {
            foreach (var badWord in this)
            {
                if (Where?.Invoke(badWord) is false)
                    continue;

                foreach (var match in badWord.Matches.IteratorSafe())
                    foreach (var exception in BadWord.ProcessMatchExceptions(match, badWord.Exceptions, IncludeWildCard: false).IteratorSafe())
                        yield return exception;
            }
        }

        public IEnumerable<string> GetProcessedBadWordExceptions()
            => GetProcessedBadWordExceptions(null)
            ;

        private static void BenchmarkMatches(
            object Name,
            string Description,
            string FullPattern,
            string WordsString,
            bool MatchPositive = true
            )
        {
            bool regexMatchTest = Utils.BenchmarkReturn(() => FullPattern.MatchesCaptureGroups(WordsString, BadWord.DefaultRegexOptionsCompiled, Silent: true),
                Description: $"{Description} {nameof(Extensions.MatchesCaptureGroups)} full word string");
            Utils.Log($"{Name} Match Test: {(MatchPositive == regexMatchTest ? "success" : "failure")}");
        }

        private static void BenchmarkIndividualMatches(
            string Descrition,
            string DescriptionInner,
            string FullPattern,
            IEnumerable<string> Words,
            bool MatchPositive = true,
            bool FullOutput = false
            )
        {
            int wordsCount = Words.Count();
            var results = new List<string>();
            Utils.Benchmark(
                Action: delegate ()
                {
                    foreach (var word in Words)
                    {
                        bool result;
                        if (!FullOutput)
                            result = FullPattern.MatchesCaptureGroups(word.Replace("+", "").Replace("*", ""), BadWord.DefaultRegexOptionsCompiled, Silent: true);
                        else
                        {
                            result = Utils.BenchmarkReturn(
                                Func: delegate ()
                                {
                                    return FullPattern.MatchesCaptureGroups(word.Replace("+", ""), BadWord.DefaultRegexOptions, Silent: true);
                                },
                                Description: $"{DescriptionInner} {nameof(Extensions.MatchesCaptureGroups)} for {word}")
                            ;
                        }

                        if (MatchPositive != result)
                            results.Add(word);
                    }
                },
                Description: Descrition);

            Utils.Log($"Individual word results, {(MatchPositive ? "missed" : "caught")} {results.Count}/{wordsCount} {(MatchPositive ? "match" : "exception")} words:");
            results.Loggregate(
                Proc: s => $"\"{s}\"; {(MatchPositive ? "match missed" : "exception caught")}",
                Empty: $"none {Const.TICK}",
                PostProc: s => $"{1.Indent()}: {s}");
        }

        private void RunMatchesBenchmark(
            Trie MatchTrie,
            object TrieName,
            string FullPattern,
            bool DoStarTest
            )
        {
            var matchWords = MatchTrie.GetWordStrings(ExcludeSpecialTokens: true);
            string matchWordsString = matchWords.Aggregate("", Utils.CommaSpaceDelimitedAggregator).Replace("+", "");

            if (DoStarTest)
            {
                Utils.Log($"{nameof(matchWordsString)}: {matchWordsString}");
                Utils.Log($"{nameof(FullPattern)}: {FullPattern}");

                string regexReplaceTest = Utils.BenchmarkReturn(() => Regex.Replace(matchWordsString, FullPattern, "****"),
                    Description: $"{TrieName}:Matches {nameof(Regex)}.{nameof(Regex.Replace)}",
                    Default: "ERROR");
                Utils.Log($"{TrieName} Replace Test: \n{regexReplaceTest}");
            }

            BenchmarkMatches(TrieName, $"{TrieName}:Matches", FullPattern, matchWordsString);
            BenchmarkIndividualMatches($"{TrieName} Match Test for individual words", $"{TrieName}:Matches", FullPattern, GetProcessedBadWordMatches());
            BenchmarkIndividualMatches($"{TrieName} Match Test for individual generated words", $"{TrieName}:Matches", FullPattern, matchWords);
        }

        private void RunFullBenchmark(
            Trie MatchTrie,
            Trie ExceptionTrie,
            object TrieName,
            string MatchPatternString,
            string ExceptionPatternString,
            string FullPattern,
            bool DoStarTest
            )
        {
            var matchWords = MatchTrie.GetWordStrings(ExcludeSpecialTokens: true);
            string matchWordsString = matchWords.Aggregate("", Utils.CommaSpaceDelimitedAggregator).Replace("+", "");

            var exceptionWords = ExceptionTrie.GetWordStrings(ExcludeSpecialTokens: true);
            string exceptionWordsString = exceptionWords.Aggregate("", Utils.CommaSpaceDelimitedAggregator).Replace("+", "");
            
            if (DoStarTest)
            {
                Utils.Log($"{nameof(matchWordsString)}: {matchWordsString}");
                Utils.Log($"{nameof(MatchPatternString)}: {MatchPatternString}");

                Utils.Log($"{nameof(exceptionWordsString)}: {exceptionWordsString}");
                Utils.Log($"{nameof(ExceptionPatternString)}: {ExceptionPatternString}");

                string fullTrieWords = $"## {TrieName} Match\n{matchWordsString}\n## {TrieName} Exception:\n{exceptionWordsString}";
                string regexReplaceTest = Utils.BenchmarkReturn(() => Regex.Replace(fullTrieWords, FullPattern, "****"),
                    Description: $"{TrieName}:Full {nameof(Regex)}.{nameof(Regex.Replace)}",
                    Default: "ERROR");
                Utils.Log($"{TrieName} Replace Test: \n{regexReplaceTest}");
            }

            BenchmarkMatches(TrieName, $"{TrieName}:Matches", FullPattern, matchWordsString);
            BenchmarkIndividualMatches($"{TrieName} Match Test for individual words", $"{TrieName}:Matches", FullPattern, GetProcessedBadWordMatches());
            BenchmarkIndividualMatches($"{TrieName} Match Test for individual generated words", $"{TrieName}:Matches", FullPattern, matchWords);
            BenchmarkIndividualMatches($"{TrieName} Exception Match Test for individual words", $"{TrieName}:Exceptions", FullPattern, GetProcessedBadWordExceptions(), MatchPositive: false);
            BenchmarkIndividualMatches($"{TrieName} Exception Match Test for individual generated words", $"{TrieName}:Exceptions", FullPattern, exceptionWords, MatchPositive: false);
        }

        public string ProduceTrieMegaPattern(
            object TrieName,
            bool DoStarTest = false // temp true, normally false
            )
        {
            bool doBenchmark = false; // temp true, normally false
            using var matches = ScopeDisposedList<string>.GetFromPool();
            using var exceptions = ScopeDisposedList<string>.GetFromPool();
            foreach (var badWord in this)
            {
                if (badWord.IsTest
                    && !Options.DebugEnableBadWordFilterTestWords)
                    continue;

                foreach (var match in badWord.Matches.IteratorSafe())
                {
                    if (BadWord.ProcessMatchString(match, badWord.AllowPartial, IncludeWildCard: false) is string matchPattern
                        && !matches.Contains(matchPattern))
                    {
                        matches.Add(matchPattern);
                    }
                    foreach (var exception in BadWord.ProcessMatchExceptions(match, badWord.Exceptions, IncludeWildCard: false).IteratorSafe())
                        if (!exceptions.Contains(exception))
                            exceptions.Add(exception);
                }
            }
            if (matches.IsNullOrEmpty())
                return null;

            using var matchTrie = new Trie($"{TrieName}:Matches", matches);

            if (!matchTrie.IsValid
                || matchTrie.GetUnboundRegexPattern(Capturing: true) is not string matchPatternString)
                return null;

            string fullPattern = $"{matchPatternString}+";
            //string fullPattern = $"\\b{matchPatternString}+\\b"; //this wasn't allowing partials

            if (exceptions.IsNullOrEmpty())
            {
                if (doBenchmark)
                    RunMatchesBenchmark(matchTrie, TrieName, fullPattern, DoStarTest);

                return fullPattern;
            }

            using var exceptionTrie = new Trie($"{TrieName}:Exceptions", exceptions);

            if (!exceptionTrie.IsValid
                || exceptionTrie.GetUnboundRegexPattern(Capturing: false) is not string exceptionPatternString)
            {
                if (doBenchmark)
                    RunMatchesBenchmark(matchTrie, TrieName, fullPattern, DoStarTest);

                return fullPattern;
            }

            fullPattern = $"(?:{exceptionPatternString}|{matchPatternString})+";
            //fullPattern = $"\\b(?:{exceptionPatternString}|{matchPatternString})+\\b"; //this wasn't allowing partials

            if (doBenchmark)
                RunFullBenchmark(
                    MatchTrie: matchTrie,
                    ExceptionTrie: exceptionTrie,
                    TrieName: TrieName,
                    MatchPatternString: matchPatternString,
                    ExceptionPatternString: exceptionPatternString,
                    FullPattern: fullPattern,
                    DoStarTest: DoStarTest);

            return fullPattern;
        }

        public string ProduceTrieMegaPattern()
            => ProduceTrieMegaPattern(Severity)
            ;

        public bool Check(string Text)
        {
            throw new NotImplementedException();
        }
    }

    public static class BadWordSetExtensions
    {
        public static bool HasBadWord(
            this string Text,
            SeverityLevel MinimumSeverity = IBadWord.DefaultSeverity,
            bool IgnoreCache = false
            )
        {
            if (Text.IsNullOrEmpty())
                return false;

            string textStripped = Text.Strip();

            if (!BadWordSet.FilterResultCache.TryGetValue(textStripped, out bool result)
                || !IgnoreCache)
            {
                result = MinimumSeverity == SeverityLevel.All;

                if (!result
                    && MinimumSeverity != SeverityLevel.None)
                {
                    if (!BadWordSet.MegaPatterns.TryGetValue(MinimumSeverity, out string megaPattern))
                    {
                        if (BadWordSet.BadWordSetCache.IsNullOrEmpty())
                            BadWordSet.InitializeBadWordSets();

                        if (BadWordSet.BadWordSetCache.IsNullOrEmpty())
                        {
                            Utils.Warn($"Failed to initialize {nameof(BadWordSet.BadWordSetCache)}. Unable to produce mega pattern for {MinimumSeverity.ToStringWithNum()}");
                            return false;
                        }

                        if (!BadWordSet.BadWordSetCache.TryGetValue(MinimumSeverity, out var badwordSet))
                        {
                            Utils.Warn($"{nameof(BadWordSet.BadWordSetCache)} contians no entry for {MinimumSeverity.ToStringWithNum()}. Unable to produce mega pattern for severity level");
                            return false;
                        }

                        megaPattern = badwordSet.ProduceTrieMegaPattern();
                        BadWordSet.MegaPatterns[MinimumSeverity] = megaPattern;
                        //Utils.Info($"produced the following mega pattern for minimum severity level {MinimumSeverity} ({(int)MinimumSeverity}):\n{megaPattern}");
                    }

                    if (!megaPattern.IsNullOrEmpty())
                        result = megaPattern.MatchesCaptureGroups(textStripped, BadWord.DefaultRegexOptions, MegaPattern: true);
                    else
                        result = BadWordSet.AllBadWords.IteratorSafe().Any(b => b.Severity >= MinimumSeverity && b.Check(textStripped));
                }

                if (!IgnoreCache)
                    BadWordSet.FilterResultCache[textStripped] = result;
            }

            return result;
        }
    }
}
