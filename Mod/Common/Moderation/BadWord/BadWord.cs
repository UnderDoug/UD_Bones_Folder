using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Newtonsoft.Json;

using Platform.IO;

using UD_Bones_Folder.Mod.Serialization;

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
    public class BadWord : IComposite, IBadWord
    {
        [Serializable]
        public class BadWordEqualityComparer : CompositeEqualityComparer<BadWord>
        {
            public BadWordEqualityComparer()
                : base()
            { }

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

        public static string DisplayNameContext => nameof(BadWordExtensions.HasBadDisplayName);

        public static BadWordEqualityComparer DefaultEqualityComparer = new BadWordEqualityComparer();

        public static RegexOptions DefaultRegexOptions => RegexOptions.IgnoreCase | RegexOptions.Multiline;
        public static RegexOptions DefaultRegexOptionsCompiled => DefaultRegexOptions | RegexOptions.Compiled;

        public static string WildCard => "\\w*";

        [JsonProperty]
        private string id;
        /// <summary>
        /// String in lowercase characters used as a unique ID for the profanity.
        /// </summary>
        [JsonIgnore]
        public string ID
        {
            get => id;
            set => id = value;
        }

        [JsonProperty]
        private string match;
        /// <summary>
        /// String in lowercase characters. Asterisks (*) can be used to indicate the previous character can have one or more appearances. Pipes (|) can be used as a separator if matching multiple terms under this profanity.
        /// </summary>
        [JsonIgnore]
        public string Match
        {
            get => match;
            set => match = value;
        }

        [JsonProperty]
        private IBadWord.SeverityLevel severity;
        /// <summary>
        /// Integer from 1 to 4 corresponding to a supported <see href="https://github.com/dsojevic/profanity-list/tree/main#severity-levels">Severity Level</see>.
        /// </summary>
        [JsonIgnore]
        public IBadWord.SeverityLevel Severity
        {
            get => severity;
            set => severity = value;
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        private string[] tags;
        /// <summary>
        /// An optional array of lowercase strings to indicate how this profanity is tagged. These should be in English.
        /// </summary>
        [JsonIgnore]
        public string[] Tags
        {
            get => tags;
            set => tags = value;
        }

        [JsonProperty]
        public bool allow_partial;
        /// <summary>
        /// Whether or not this profanity should be used with partial matching. Explicitly set to false when the match may otherwise have hundreds or thousands of exceptions.
        /// </summary>
        [JsonIgnore]
        public bool AllowPartial
        {
            get => allow_partial;
            set => allow_partial = value;
        }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        private string[] exceptions;
        /// <summary>
        /// An optional array of lowercase strings indicating exceptions to this profanity if using partial matching. An asterisk (*) is used as a placeholder for the matched word.
        /// </summary>
        /// <example>
        /// sp* would be a valid exception ('sparse') for the profanity arse if relying on partial matching.
        /// </example>
        [JsonIgnore]
        public string[] Exceptions
        {
            get => exceptions;
            set => exceptions = value;
        }

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

        private StringSet _Patterns;
        public StringSet Patterns
        {
            get => _Patterns;
            protected set => _Patterns = value;
        }

        private StringSet _ProcessedExceptions;
        public StringSet ProcessedExceptions
        {
            get => _ProcessedExceptions;
            protected set => _ProcessedExceptions = value;
        }

        [JsonIgnore]
        public bool IsTest => Tags?.Any(s => s.EqualsNoCase("test")) is true;

        public BadWord()
        {
            AllowPartial = true;
        }

        public BadWord Init()
        {
            Patterns = new StringSet();
            foreach (var match in Matches.IteratorSafe())
                if (ProducePattern(match, AllowPartial, Exceptions) is string pattern)
                    Patterns.Add(pattern);

            return this;
        }

        public override string ToString()
            => $"{ID} ({(int)Severity}, {Severity}) [{Tags.Aggregate((string)null, Utils.CommaSpaceDelimitedAggregator) ?? "no tags"}]"
            ;

        public bool HasTag(string Tag)
            => Tags?.Contains(Tag) is true
            ;

        public static string ProcessMatchException(
            string Match,
            string Exception,
            bool IncludeWildCard = true
            )
        {
            if (Match.IsNullOrEmpty()
                || Exception.IsNullOrEmpty()
                || !Exception.Contains('*'))
                return null;

            string wildCard = IncludeWildCard
                ? WildCard
                : null
                ;

            return $"{wildCard}{Exception.Replace("*", Match)}{wildCard}";
        }

        public static string ProcessMatchString(
            string Match,
            bool AllowPartial = true,
            bool IncludeWildCard = true
            )
        {
            Match = Match.Replace('*', '+');
            string pattern = Match;

            if (!AllowPartial)
                return $"\\b{pattern}\\b";

            string wildCard = IncludeWildCard
                ? WildCard
                : null
                ;

            return $"{wildCard}{pattern}{wildCard}";
        }

        public static string ProcessMatchExceptionString(string Match, string[] Exceptions = null)
            => ProcessMatchExceptions(Match, Exceptions)
                .Aggregate((string)null, Utils.PipeDelimitedAggregator)
            ;

        public static IEnumerable<string> ProcessMatchExceptions(string Match, string[] Exceptions = null, bool IncludeWildCard = true)
        {
            string match = ProcessMatchString(Match, AllowPartial: !Exceptions.IsNullOrEmpty(), IncludeWildCard: false);
            foreach (var exception in Exceptions.IteratorSafe())
                if (ProcessMatchException(match, exception, IncludeWildCard: IncludeWildCard) is string exceptionString)
                    yield return exceptionString;
        }

        public static string ProducePattern(
            string Match,
            bool AllowPartial = true,
            string[] Exceptions = null
            )
        {
            Match = ProcessMatchString(Match, AllowPartial: true, IncludeWildCard: false);

            string pattern = Match;

            if (!AllowPartial)
                return $"(\\b{pattern}\\b)+";

            pattern = $"{WildCard}{pattern}{WildCard}";

            string exceptions = ProcessMatchExceptionString(Match, Exceptions);

            if (!exceptions.IsNullOrEmpty())
                pattern = $"(?:{exceptions}|({pattern}))+";

            return pattern;
        }

        public bool Check(string Text)
        {
            if (Text.IsNullOrEmpty())
                return false;

            foreach (var pattern in Patterns.IteratorSafe())
                if (pattern.MatchesCaptureGroups(Text, DefaultRegexOptions))
                    return true;

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
                    badDisplayName.GetDebugLines().Loggregate(
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
            var severity = IBadWord.DefaultSeverity;
            if (!Parameters.IsNullOrEmpty()
                && Parameters.Split(' ') is string[] parameters
                && !parameters.IsNullOrEmpty())
            {
                if (int.TryParse(parameters[0], out int severityInt))
                    severity = (IBadWord.SeverityLevel)Math.Clamp(severityInt, (int)IBadWord.SeverityLevel.All, (int)IBadWord.SeverityLevel.Severe);
                else
                if (!Enum.TryParse(parameters[0], out severity))
                    severity = IBadWord.DefaultSeverity;
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
                        badDisplayName.GetDebugLines().Loggregate(
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
        private static bool HasBadWord(
            this Description Description,
            IBadWord.SeverityLevel MinimumLevel = IBadWord.DefaultSeverity,
            bool IgnoreCache = false
            )
        {
            try
            {
                return Description?._Short is string description
                && description.HasBadWord(MinimumLevel, IgnoreCache)
                ;
            }
            catch (Exception x)
            {
                if (Options.DebugEnableBadWordFilterLogging)
                    Utils.Warn($"Error while {nameof(BadWord)} checking description of {Description?.ParentObject?.DebugName ?? "NO_OBJECT"}", x);
                return false;
            }
        }

        public static bool RequireFilterCacheResult(string Key, Func<bool> Proc)
        {
            BadWordSet.FilterResultCache ??= new();
            var filterResultCache = BadWordSet.FilterResultCache;

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

            BadWordSet.FilterResultCache ??= new();
            var filterResultCache = BadWordSet.FilterResultCache;

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

        public static string GetBaseDisplayNameForModeration(this GameObject Object)
            => Object.GetDisplayName(
                AsIfKnown: true,
                Single: true,
                NoConfusion: true,
                NoColor: true,
                Stripped: true,
                Visible: true,
                WithoutTitles: true,
                Short: true,
                BaseOnly: true,
                Context: BadWord.DisplayNameContext)
            ;

        public static bool HasBadDisplayName(
            this GameObject Object,
            out BadDisplayName BadDisplayName,
            IBadWord.SeverityLevel MinimumLevel = IBadWord.DefaultSeverity,
            bool IgnoreCache = false,
            bool Silent = true
            )
        {
            BadDisplayName = null;

            Stopwatch sw = null;
            if (!Silent)
                sw = Stopwatch.StartNew();

            try
            {
                if (Object == null)
                    return false;

                string baseDisplayName = Object.GetBaseDisplayNameForModeration();

                if (!baseDisplayName.IsNullOrEmpty()
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

                if (!(bool)BadDisplayName)
                    BadDisplayName = null;

                return (bool)BadDisplayName;
            }
            finally
            {
                if (!Silent
                    && Object != null)
                    Utils.Log($"{nameof(HasBadDisplayName)} for {Object.DebugName.Strip()} took {sw.Elapsed.ValueUnits()}");

                sw?.Stop();
            }
        }

        public static bool HasBadDescription(
            this GameObject Object,
            out bool InDescription,
            IBadWord.SeverityLevel MinimumLevel = IBadWord.DefaultSeverity,
            bool IgnoreCache = false,
            bool Silent = true
            )
        {
            Stopwatch sw = null;
            if (!Silent)
                sw = Stopwatch.StartNew();

            try
            {
                return InDescription = Object.TryGetPart(out Description description)
                    && description.HasBadWord(MinimumLevel, IgnoreCache)
                    ;
            }
            finally
            {
                if (!Silent
                    && Object != null)
                    Utils.Log($"{nameof(HasBadDescription)} for {Object.DebugName.Strip()} took {sw.Elapsed.ValueUnits()}");

                sw?.Stop();
            }
        }

        public static bool HasBadWord(
            this GameObject Object,
            out BadDisplayName BadDisplayName,
            out bool InDescription,
            IBadWord.SeverityLevel MinimumLevel = IBadWord.DefaultSeverity,
            bool IgnoreCache = false,
            bool Silent = true
            )
        {
            Stopwatch sw = null;
            if (!Silent)
                sw = Stopwatch.StartNew();

            try
            {
                InDescription = false;
                BadDisplayName = null;
                if (Object == null)
                    return false;

                Object.HasBadDisplayName(out BadDisplayName, MinimumLevel, IgnoreCache);

                Object.HasBadDescription(out InDescription, MinimumLevel, IgnoreCache);

                return (bool)BadDisplayName
                    || InDescription
                    ;
            }
            finally
            {
                if (!Silent
                    && Object != null)
                    Utils.Log($"{nameof(HasBadWord)} for {Object.DebugName.Strip()} took {sw.Elapsed.ValueUnits()}");

                sw?.Stop();
            }
            
        }
    }
}
