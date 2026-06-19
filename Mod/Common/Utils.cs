using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Genkit;

using HarmonyLib;

using HistoryKit;

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
using XRL.World.Conversations;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using ReplacerContext = XRL.World.Text.Delegates.DelegateContext;
using ConversationContext = XRL.World.Conversations.DelegateContext;

using static UD_Bones_Folder.Mod.Const;
using XRL.CharacterBuilds;
using UD_Bones_Folder.Mod.Moderation;
using System.Diagnostics;
using UD_Bones_Folder.Mod.Serialization;
using Newtonsoft.Json;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasVariableReplacer]
    [HasConversationDelegate]
    public static class Utils
    {
        public static ModInfo ThisMod => ModManager.GetMod(MOD_ID);

        public static XRL.Version ModVersion => ThisMod.Manifest.Version;
        public static string ModTitle => ThisMod.Manifest.Title;
        public static string Author => ThisMod.Manifest.Author;
        public static string AuthorOnPlatforms => $"{Author} on GitHub (UnderDoug), on Discord (.underdoug), or on the Steam Workshop (UnderDoug)";

        public static string BothBonesLocations 
            => $"{DataManager.SanitizePathForDisplay(BonesManager.BonesSyncPath)} -OR- " +
            $"{DataManager.SanitizePathForDisplay(BonesManager.BonesSavePath)}"
            ;

        public const double FPS_MODULO = 8.0;

        private static int LastSpriteManagerPathMapCount = 0;
        [ModSensitiveStaticCache(CreateEmptyInstance = false)]
        private static StringMap<exTextureInfo> _SpriteManagerPathMap;
        public static StringMap<exTextureInfo> SpriteManagerPathMap => _SpriteManagerPathMap ??= GetSpriteManagerPathMapNaughty();

        public static double CurrentFrame => UD_Bones_LunarColors.GetCurrentAnimationFrame();
        public static double CurrentKeyframe => UD_Bones_LunarColors.GetCurrentAnimationKeyframe();

        [ModSensitiveStaticCache(CreateEmptyInstance = false)]
        private static exTextureInfo _InvalidTextureInfo;
        public static exTextureInfo InvalidTextureInfo => _InvalidTextureInfo ??= GetSpriteManagerInvalidInfoNaughty();

        #region Blueprint Specs

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static StringSet CachedBlueprints = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        public static Dictionary<string, BlueprintSpec> CachedBlueprintSpecs = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        public static Dictionary<string, bool> IsTileCache = new();

        [ModSensitiveCacheInit]
        public static void CacheBlueprintsBySpec()
        {
            using (var status = Loading.StartTask("Converting Lunar Regents"))
            {
                foreach (var blueprint in GameObjectFactory.Factory.SafelyGetBlueprintsInheritingFrom(Mod.BlueprintSpec.BASE_BLUEPRINT))
                {
                    CachedBlueprintSpecs ??= new();
                    CachedBlueprintSpecs.Clear();

                    CachedBlueprints ??= new();
                    CachedBlueprints.Clear();

                    if (!CachedBlueprintSpecs.ContainsKey(blueprint.Name))
                    {
                        if (BlueprintSpec.TryCreateFrom(blueprint, out var cachedSpec))
                        {
                            CachedBlueprints.Add(blueprint.Name);
                            CachedBlueprintSpecs[blueprint.Name] = cachedSpec;

                            Log($"Created {nameof(BlueprintSpec)} from {blueprint.Name}");
                            Log(JsonConvert.SerializeObject(cachedSpec, Formatting.Indented));
                        }
                        else
                            Log($"Failed to Create {nameof(BlueprintSpec)} from {blueprint.Name}");
                    }
                }
            }
        }

        #endregion
        #region Pseudo-Debug

        public static void Error(ModInfo ModInfo, object Message)
            => (ModInfo ?? ThisMod).Error(Message)
            ;

        public static void Error(object Message)
            => Error(ModInfo: null, Message)
            ;

        public static void Error(ModInfo ModInfo, object Context, Exception X)
            => Error(ModInfo, $"{Context}: {X}")
            ;

        public static void Error(object Context, Exception X)
            => Error(ModInfo: null, Context, X)
            ;

        public static void Warn(ModInfo ModInfo, object Message)
            => (ModInfo ?? ThisMod).Warn(Message)
            ;

        public static void Warn(object Message)
            => Warn(ModInfo: null, Message)
            ;

        public static void Warn(ModInfo ModInfo, object Context, Exception X)
            => Warn(ModInfo, $"{Context}: {X}")
            ;

        public static void Warn(object Context, Exception X)
            => Warn(ModInfo: null, Context, X)
            ;

        public static void Warn(ModInfo ModInfo, object Message, StackTrace WithTrace)
            => Warn(ModInfo: ModInfo, Message: (WithTrace ?? new StackTrace(1)).FramesToString(Count: 5, SkipLines: 0, TextLineBefore: $"{Message}:"))
            ;

        public static void Warn(object Message, StackTrace WithTrace)
            => Warn(ModInfo: null, Message: Message, WithTrace: WithTrace ?? new StackTrace(1))
            ;

        public static void Info(object Message)
            => MetricsManager.LogModInfo(ThisMod, Message)
            ;

        public static void Log(object Message)
            => UnityEngine.Debug.Log(Message)
            ;

        public static T LogReturn<T>(object Message, T Return)
        {
            Log(Message);
            return Return;
        }

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

        public static IEnumerable<T> Loggregate<T>(
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

        #endregion
        #region Exceptions

        public class InnerArrayNullException : InvalidOperationException
        {
            public InnerArrayNullException(string ParamName)
                : base(ParamName + " is null when it shouldn't be.")
            {
            }
            public InnerArrayNullException()
                : this("An inner array")
            {
            }
        }

        public class CollectionModifiedException : InvalidOperationException
        {
            public CollectionModifiedException(string ParamName)
                : base(ParamName + " was modified; enumeration operation may not execute.")
            {
            }
            public CollectionModifiedException(Type CollectionType)
                : base(CollectionType.Name)
            {
            }
            public CollectionModifiedException()
                : base("Collection")
            {
            }
        }

        public class InvalidEnumValueException<T> : InvalidOperationException
            where T : struct, Enum
        {
            private static string GetUnderlyingValue(T Value)
                => Convert.ChangeType(Value, Enum.GetUnderlyingType(typeof(T))).ToString();

            public InvalidEnumValueException(T Value)
                : base(GetUnderlyingValue(Value) + " is not a valid value for " + typeof(T).ToStringWithGenerics() + ".")
            {
            }
        }

        #endregion
        #region Variable Replacers

        [VariableReplacer]
        public static string ud_nbsp(ReplacerContext Context)
        {
            string nbsp = "\xFF";
            string output = nbsp;
            if (!Context.Parameters.IsNullOrEmpty()
                && int.TryParse(Context.Parameters[0], out int count))
                output = nbsp.ThisManyTimes(count);

            return output;
        }

        private static string ProcessSpiceReplacer(ReplacerContext Context)
        {
            if (Context.Parameters.IsNullOrEmpty())
                return null;

            if (Context.Target != null)
            {
                for (int i = 0; i < Context.Parameters.Count; i++)
                {
                    if (Context.Parameters[i] is string parameter)
                    {
                        if (parameter.Contains("entity$"))
                        {
                            var entityCategoryPair = parameter.Split('$');
                            if (entityCategoryPair.Length < 2)
                            {
                                Context.Parameters[i] = "!random";
                                continue;
                            }
                            string category = entityCategoryPair[1];
                            if (category.ContainsNoCase("domain"))
                            {
                                if (!category.Contains("[")
                                    || !category.EndsWith("]")
                                    || category.Split('[') is not string[] categoryValuePair
                                    || categoryValuePair.Length <= 1
                                    || !int.TryParse(categoryValuePair[1].Replace("]", ""), out int domainRange))
                                    domainRange = 5;

                                Context.Parameters[i] = Context.Target.GetMythicDomain(domainRange);
                                continue;
                            }
                            if (category.ContainsNoCase("elements"))
                            {
                                var e = GetItemElementsEvent.FromPool();
                                e.HandleFor(Context.Target);
                                e.Weights ??= new();

                                var element = HistoricStringExpander.ExpandString("<spice.elements.!random>") ?? "might";

                                if (e.Weights.ContainsKey(element))
                                    e.Weights[element]++;
                                else
                                    e.Weights.Add(element, 1);

                                var elements = e.Weights.ToBallBag();
                                try
                                {
                                    if (elements.IsNullOrEmpty())
                                    {
                                        Context.Parameters[i] = element;
                                        continue;
                                    }

                                    if (!category.Contains("[")
                                        || !category.EndsWith("]")
                                        || category.Split('[') is not string[] categoryValuePair
                                        || categoryValuePair.Length <= 1)
                                        element = elements.PickOne();
                                    else
                                    if (categoryValuePair[1].Replace("]", "").EqualsNoCase("Random"))
                                        element = e.Weights.Keys.GetRandomElementCosmetic();
                                    else
                                    if (int.TryParse(categoryValuePair[1].Replace("]", ""), out int valueIndex))
                                        element = e.Weights.Keys.ElementAtOrDefault(valueIndex) ?? element;
                                    else
                                        element = elements.PickOne();

                                    Context.Parameters[i] = element;
                                    continue;
                                }
                                finally
                                {
                                    GetItemElementsEvent.ResetTo(ref e);
                                }
                            }
                            Context.Parameters[i] = "!random";
                        }
                    }
                }
            }

            string output = HistoricStringExpander.ExpandString($"<{Context.Parameters.Aggregate("spice", PeriodDelimitedAggregator)}>");
            return Context.Capitalize
                ? output.Capitalize()
                : output
                ;
        }

        [VariableReplacer(Keys = new string[] { "spice" }, Capitalization = true)]
        public static string UDSpiceReplacer(ReplacerContext Context)
            => ProcessSpiceReplacer(Context)
            ;

        [VariableObjectReplacer(Keys = new string[] { "spice" }, Capitalization = true)]
        public static string UDSpiceObjectReplacer(ReplacerContext Context)
            => ProcessSpiceReplacer(Context)
            ;

        private static bool? StoredAllowSecondPerson = null;
        [VariableReplacer("no2nd")]
        public static string UD_no2nd(ReplacerContext Context)
        {
            StoredAllowSecondPerson ??= Grammar.AllowSecondPerson;
            Grammar.AllowSecondPerson = false;
            return null;
        }

        [VariableReplacer("no2nd.restore")]
        public static string UD_no2nd_restore(ReplacerContext Context)
        {
            if (StoredAllowSecondPerson is bool storedAllowSecondPerson)
                Grammar.AllowSecondPerson = storedAllowSecondPerson;

            StoredAllowSecondPerson = null;
            return null;
        }

        [VariableObjectReplacer(Keys = new string[] { "RegalTitle", "UD_RegalTitle" })]
        public static string RegalTitle(ReplacerContext Context)
            => $"=LunarShader:{UD_Bones_LunarRegent.GetRegalTitle(Context.Target)}:{(Context.Target?.BaseID)?.ToString() ?? "*"}="
                .StartReplace()
                .ToString()
            ;

        [VariableObjectReplacer(Keys = new string[] { "A.RegalTitle" })]
        public static string UD_A_RegalTitle(ReplacerContext Context)
        {
            string adjective = null;
            if (!Context.Parameters.IsNullOrEmpty())
                adjective = Context.Parameters[0];

            string title = UD_Bones_LunarRegent.GetRegalTitle(Context.Target);
            string indefiniteArticle = Context.Target?.IsPlural is true
                ? "some"
                : adjective?.IndefiniteArticle() ?? title.IndefiniteArticle()
                ;

            if (!adjective.IsNullOrEmpty())
                adjective += " ";

            string shaderOffset = (Context.Target?.BaseID)?.ToString() ?? "*";

            if (Context.Capitalize)
                indefiniteArticle = indefiniteArticle.Capitalize();

            return $"{indefiniteArticle} {adjective}=LunarShader:{title}:{shaderOffset}="
                    .StartReplace()
                    .ToString()
                ;
        }

        #endregion
        #region Conversation Delegates

        [ConversationDelegate]
        public static bool IfLastChoiceAny(ConversationContext Context)
            => (ConversationUI.LastChoice?.ID).IsNullOrEmpty() == Context.Value.IsNullOrEmpty()
            || (Context.Value?.CachedCommaExpansion()?.Contains(ConversationUI.LastChoice?.ID) is true)
            ;

        [ConversationDelegate(Speaker = true)]
        public static void ModIntProperty(ConversationContext Context)
        {
            Context.Value.AsDelimitedSpans(',', out var property, out var value);
            if (value.IsEmpty)
                Context.Target.RemoveIntProperty(Context.Value);
            else
            if (int.TryParse(value, out int result))
                Context.Target.ModIntProperty(new string(property), result);
        }

        [ConversationDelegate(Speaker = true)]
        public static bool IfTestIntProperty(ConversationContext Context)
        {
            string[] array = Context.Value.Split(' ', 3);
            string propName = array[0];
            string @operator = (array.Length >= 2) ? array[1] : null;
            string testValueString = (array.Length >= 3) ? array[2] : null;

            if (!int.TryParse(testValueString, out int testValue))
                return false;

            return @operator?[0] == '!'
                ? !TestIntPropInternal(Context.Target, propName, @operator[1..], testValue)
                : TestIntPropInternal(Context.Target, propName, @operator, testValue)
                ;
        }

        private static bool TestIntPropInternal(
            GameObject Object,
            string Property,
            string Operator,
            int TestValue
            )
        {
            if (Operator != null
                && Object.TryGetIntProperty(Property, out var value))
            {
                return Operator switch
                {
                    "=" => value == TestValue,
                    ">" => value > TestValue,
                    ">=" => value >= TestValue,
                    "<" => value < TestValue,
                    "<=" => value <= TestValue,
                    "%" => value % TestValue == 0,
                    "&" => (value & TestValue) == TestValue,
                    _ => false,
                };
            }
            return false;
        }

        #endregion

        public static void GetMinMax<T>(T Operand1, T Operand2, out T Min, out T Max)
            where T : IConvertible
        {
            Min = (T)Convert.ChangeType(Math.Min(Convert.ToUInt64(Operand1), Convert.ToUInt64(Operand2)), typeof(T));
            Max = (T)Convert.ChangeType(Math.Max(Convert.ToUInt64(Operand1), Convert.ToUInt64(Operand2)), typeof(T));
        }

        public static void GetMinMax(double Operand1, double Operand2, out double Min, out double Max)
        {
            Min = Math.Min(Operand1, Operand2);
            Max = Math.Max(Operand1, Operand2);
        }

        public static void GetMinMax(int Operand1, int Operand2, out int Min, out int Max)
        {
            Min = Math.Min(Operand1, Operand2);
            Max = Math.Max(Operand1, Operand2);
        }

        public static void GetMinMax(out int Min, out int Max, params int[] Values)
        {
            if (Values.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Values), $"must have at least 1 Value to get min/max");
            Min = Values[0];
            Max = Values[0];
            foreach (int value in Values)
            {
                Max = Math.Max(value, Max);
                Min = Math.Min(Min, value);
            }
        }

        public static int GetDisadvantage(params int[] Values)
        {
            if (Values.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Values), $"must have at least 1 Value to get lowest");

            int min = Values[0];
            foreach (int value in Values)
                min = Math.Min(min, value);

            return min;
        }

        public static int GetAdvantage(params int[] Values)
        {
            if (Values.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(Values), $"must have at least 1 Value to get highest");

            int max = Values[0];
            foreach (int value in Values)
                max = Math.Max(value, max);

            return max;
        }

        #region Aggregator Functions

        public static string DelimitedAggregator<T>(string Accumulator, T Next, string Delimiter)
           => $"{Accumulator}{(!Accumulator.IsNullOrEmpty() ? Delimiter : null)}{Next}"
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

        public static string PipeDelimitedAggregator<T>(string Accumulator, T Next, Func<string, T, string> Proc)
            => DelimitedAggregator(Accumulator, Proc?.Invoke(Accumulator, Next) ?? Next?.ToString(), "|")
            ;

        public static string PipeDelimitedAggregator<T>(string Accumulator, T Next)
            => PipeDelimitedAggregator(Accumulator, Next, null)
            ;

        public static string CallChain(params string[] Strings)
            => Strings?.Aggregate("", PeriodDelimitedAggregator)
            ;


        #endregion

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

        private static exTextureInfo GetSpriteManagerInvalidInfoNaughty(string DebugForTile = null)
        {
            string fieldName = "InvalidInfo";
            var textureInfo = SpriteManager.GetTextureInfo("Text_32.bmp"); // this is what it defaults to at the time this was written
            var invalidTextureInfo = textureInfo;
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
                Error(nameof(GetSpriteManagerInvalidInfoNaughty), x);
                textureInfo = invalidTextureInfo;
            }
            catch (InvalidCastException x)
            {
                Error(nameof(GetSpriteManagerInvalidInfoNaughty), x);
                textureInfo = invalidTextureInfo;
            }
            return textureInfo;
        }

        private static StringMap<exTextureInfo> GetSpriteManagerPathMapNaughty()
        {
            string fieldName = "PathMap";
            StringMap<exTextureInfo> pathMap = null;
            // Log($"{nameof(GetSpriteManagerPathMapNaughty)}");
            try
            {
                var field = typeof(SpriteManager).GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    ?? throw new ArgumentOutOfRangeException("name", $"Field \"{fieldName}\" was not found in {nameof(Type)} {typeof(SpriteManager)}");

                if (!typeof(StringMap<exTextureInfo>).IsAssignableFrom(field.FieldType))
                    throw new InvalidCastException($"{fieldName} field in {nameof(Type)} {typeof(SpriteManager)} is {field.FieldType.Name} which cannot be cast to {typeof(StringMap<exTextureInfo>)}");

                pathMap = field.GetValue(null) as StringMap<exTextureInfo>;
                if (pathMap != null)
                    Log($"{nameof(GetSpriteManagerPathMapNaughty)}: retrieved.");
            }
            catch (ArgumentOutOfRangeException x)
            {
                Error(nameof(GetSpriteManagerPathMapNaughty), x);
                pathMap = null;
            }
            catch (InvalidCastException x)
            {
                Error(nameof(GetSpriteManagerPathMapNaughty), x);
                pathMap = null;
            }
            return pathMap;
        }

        public static bool TileExists(string Tile)
        {
            if (Tile == null)
                return false;

            bool textureNull = false;
            SerializationExtensions.PerformSilently(delegate()
            {
                textureNull = SpriteManager.GetTextureInfo(Tile, false) == null;
            });
            if (textureNull)
                return false;

            bool textureInvalid = false;
            SerializationExtensions.PerformSilently(delegate()
            {
                textureInvalid = SpriteManager.GetTextureInfo(Tile) == InvalidTextureInfo;
            });
            if (textureInvalid)
                return false;

            return true;
        }

        public static async Task<bool> TileExistsAsync(string Tile)
        {
            if (Tile == null)
                return false;

            bool isGameInValidState = GameManager.AwakeComplete
                && The.Game != null
                && The.Game.Running
                && The.GameContext != null;

            /*string currentContextString = "UnknownContext";
            if (The.CurrentContext == The.GameContext)
                currentContextString = nameof(The.GameContext);
            else
            if (The.CurrentContext == The.UiContext)
                currentContextString = nameof(The.UiContext);

            Log($"{nameof(The.CurrentContext)} is {currentContextString} (" +
                $"{nameof(isGameInValidState)}: {isGameInValidState}" +
                (LastSpriteManagerPathMapCount != (SpriteManagerPathMap?.Count ?? 0)
                    ? $", {nameof(SpriteManagerPathMap)}: {SpriteManagerPathMap?.Count ?? 0}"
                    : null) +
                $", {nameof(Tile)}: {Tile})");*/

            LastSpriteManagerPathMapCount = SpriteManagerPathMap?.Count ?? 0;
            var previousContext = The.CurrentContext;

            if (previousContext == The.GameContext
                && The.UiContext != null)
                await The.UiContext;
            
            /*if (isGameInValidState
                && The.CurrentContext != The.GameContext)
                await The.GameContext;*/

            try
            {
                if (isGameInValidState)
                {
                    if (SpriteManagerPathMap is StringMap<exTextureInfo> pathMap)
                    {
                        /*return pathMap.TryGetValue(Tile, out var textureInfo)
                            && textureInfo is not null
                            && textureInfo != InvalidTextureInfo
                            ;*/
                    }
                }

                bool textureNull = false;
                SerializationExtensions.PerformSilently(delegate ()
                {
                    textureNull = SpriteManager.GetTextureInfo(Tile, false) == null;
                });
                if (textureNull)
                    return false;

                bool textureInvalid = false;
                SerializationExtensions.PerformSilently(delegate ()
                {
                    textureInvalid = SpriteManager.GetTextureInfo(Tile) == InvalidTextureInfo;
                });
                if (textureInvalid)
                    return false;
            }
            finally
            {
                if (isGameInValidState)
                    if (The.CurrentContext != previousContext
                        //&& false
                        )
                        await previousContext;
            }
            return true;
        }

        public static bool CellIsNInFromEdge(Cell Cell, Zone Zone, int N)
        {
            if (!Cell.X.IsTwixt(N, Zone.Width - 1 - N))
                return false;

            if (!Cell.Y.IsTwixt(N, Zone.Height - 1 - N))
                return false;

            return true;
        }

        public static bool IsEdgeCell(Cell Cell, Zone Zone)
            => Cell.X == 0
            || Cell.X == Zone.Width - 1
            || Cell.Y == 0
            || Cell.Y == Zone.Height - 1
            ;

        public static bool IsCornerCell(Cell Cell, Zone Zone)
            => (Cell.X == 0
                || Cell.X == Zone.Width - 1)
            && (Cell.Y == 0
                || Cell.Y == Zone.Height - 1)
            ;

        public static T SuppressPopupsWhile<T>(Func<T> Func)
        {
            bool originalPopupSuppress = Popup.Suppress;
            Popup.Suppress = true;
            try
            {
                return Func != null
                    ? Func.Invoke()
                    : default
                    ;
            }
            finally
            {
                Popup.Suppress = originalPopupSuppress;
            }
        }

        public static object SuppressPopupsWhile(Func<object> Func)
            => SuppressPopupsWhile<object>(Func)
            ;

        public static void SuppressPopupsWhile(Action Action)
        {
            bool originalPopupSuppress = Popup.Suppress;
            Popup.Suppress = true;
            try
            {
                Action?.Invoke();
            }
            finally
            {
                Popup.Suppress = originalPopupSuppress;
            }
        }

        public static T GetEmbarkBuilderModule<T>()
            where T : AbstractEmbarkBuilderModule
        {
            string activeModulesString = nameof(EmbarkBuilderConfiguration) + "." + nameof(EmbarkBuilderConfiguration.activeModules);
            if (EmbarkBuilderConfiguration.activeModules.IsNullOrEmpty())
                throw new Exception(activeModulesString + " null or empty");

            foreach (AbstractEmbarkBuilderModule activeModule in EmbarkBuilderConfiguration.activeModules)
                if (activeModule.type == typeof(T).Name
                    && activeModule is T desiredModule)
                    return desiredModule;

            throw new Exception(typeof(T).Name + " not in " + activeModulesString);
        }

        public static bool TryGetEmbarkBuilderModule<T>(out T EmbarkBuilderModule)
            where T : AbstractEmbarkBuilderModule
        {
            try
            {
                EmbarkBuilderModule = GetEmbarkBuilderModule<T>();
            }
            catch (Exception x)
            {
                MetricsManager.LogModWarning(ThisMod, x);
                EmbarkBuilderModule = null;
                return false;
            }
            return true;
        }

        public static void Benchmark(Action Action, string Description)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                Action.Invoke();
            }
            catch (Exception x)
            {
                Error($"{nameof(Benchmark)}ing, {Description}", x);
            }
            finally
            {
                Info($"{Description} took {sw.Elapsed.ValueUnits()}...");
                sw.Stop();
            }
        }

        public static T BenchmarkReturn<T>(Func<T> Func, string Description, T Default = default)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                return Func.Invoke();
            }
            catch (Exception x)
            {
                Error($"{nameof(Benchmark)}ing, {Description}", x);
                return Default;
            }
            finally
            {
                Info($"{Description} took {sw.Elapsed.ValueUnits()}...");
                sw.Stop();
            }
        }

        public static Dictionary<string, T> GetValuesDictionary<T>(ref Dictionary<string, T> CachedValues)
            where T : struct, Enum
        {
            if (CachedValues.IsNullOrEmpty())
            {
                CachedValues ??= new();
                if (Enum.GetValues(typeof(T)) is IEnumerable<T> values)
                    foreach (T value in values)
                        CachedValues[value.ToString()] = value;
            }
            return CachedValues;
        }
    }
}
