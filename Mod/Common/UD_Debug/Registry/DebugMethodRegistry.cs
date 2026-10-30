using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using XRL;

using static UD_Bones_Folder.Mod.Utils;
using static UD_Bones_Folder.Mod.Const;
using static UD_Bones_Folder.Mod.Options;
using static UD_Bones_Folder.Mod.Logging.Debug;
using static UD_Bones_Folder.Mod.Logging.Indent;
using System.Linq;
using XRL.World;

namespace UD_Bones_Folder.Mod.Logging
{
    [HasModSensitiveStaticCache]
    public class DebugMethodRegistry : HashSet<MethodRegistryEntry>
    {
        [ModSensitiveStaticCache(CreateEmptyInstance = false)]
        private static DebugMethodRegistry _Instance;
        public static DebugMethodRegistry Instance => GetRegistry(ref _Instance);

        private static bool _GotRegistry = false;

        public DebugMethodRegistry()
        {
        }

        public bool this[MethodBase MethodBase]
            => !TryGetValue(MethodBase, out bool value)
            || value;

        public bool this[MethodInfo MethodInfo]
            => !TryGetValue(MethodInfo, out bool value)
            || value;

        public static DebugMethodRegistry GetRegistry(ref DebugMethodRegistry Registry)
        {
            Registry ??= new();

            if (_GotRegistry)
                return Registry;

            string thisMethod = CallChain(nameof(DebugMethodRegistry), nameof(GetRegistry));
            if (ModManager.GetMethodsWithAttribute(typeof(UD_DebugRegistryAttribute)) is not List<MethodInfo> debugRegistryMethods
                || debugRegistryMethods.IsNullOrEmpty())
            {
                MetricsManager.LogModError(
                    mod: ThisMod,
                    Message: $"{thisMethod} failed to retrieve any {nameof(UD_DebugRegistryAttribute)} decorated methods");

                return Registry;
            }
            foreach (MethodInfo debugRegistryMethod in debugRegistryMethods)
            {
                try
                {
                    MethodInfo methodToInvoke = debugRegistryMethod;
                    if (methodToInvoke.ContainsGenericParameters)
                    {
                        if (debugRegistryMethod.GetGenericArguments() is not Type[] genericArgs
                            || debugRegistryMethod.MakeGenericMethod(genericArgs) is not MethodInfo madeMethod)
                        {
                            string fullMethod = CallChain(
                                debugRegistryMethod.DeclaringType.ToStringWithGenerics(),
                                debugRegistryMethod.MethodSignature(true));
                            MetricsManager.LogModWarning(
                                    mod: ModManager.GetMod(debugRegistryMethod.DeclaringType.Assembly),
                                    Message: $"Generic method {fullMethod} found during {thisMethod}");
                            continue;
                        }
                        methodToInvoke = madeMethod;
                    }
                    methodToInvoke.Invoke(null, new object[] { Registry });
                }
                catch (Exception x)
                {
                    MetricsManager.LogException(thisMethod, x, GAME_MOD_EXCEPTION);
                }
            }
            return Registry;
        }

        [ModSensitiveCacheInit]
        public static void CacheDebugRegistry()
        {
            _GotRegistry = false;
            LogRegistry();
            _GotRegistry = true;
        }

        public static bool GetDoDebug(string CallingMethod = null)
        {
            if (!DoDebugSetting)
                return false;

            if (CallingMethod.IsNullOrEmpty()
                || Instance.IsNullOrEmpty()
                || Instance.None(m => m.MethodName == CallingMethod))
                return DoDebugSetting;

            try
            {
                if (TryGetCallingTypeAndMethod(out _, out MethodBase callingMethodBase)
                    && Instance.TryGetValue(callingMethodBase, out bool registryMethodValue)
                    && !registryMethodValue)
                    return false;
            }
            catch (Exception x)
            {
                MetricsManager.LogException(CallChain(nameof(DebugMethodRegistry), nameof(GetDoDebug)), x, GAME_MOD_EXCEPTION);
            }
            return DoDebugSetting;
        }

        public static void LogRegistry()
        {
            string debug = "DEBUG ";
            string thisMod = $"[{ThisMod.DisplayTitleStripped}] - ";
            string callChain = CallChain(nameof(DebugMethodRegistry), nameof(LogRegistry));
            if (!Instance.IsNullOrEmpty())
            {
                UnityEngine.Debug.Log($"{debug}{thisMod}Start of {callChain}");

                foreach (MethodRegistryEntry methodEntry in Instance)
                    UnityEngine.Debug.Log($"{debug}{methodEntry.ToString(IncludeSourceMod: true, FullSignature: true)}");

                UnityEngine.Debug.Log($"{debug}{thisMod}End of {callChain}");
            }
            else
                UnityEngine.Debug.Log($"{debug}{thisMod}{callChain} called but {nameof(Instance)} not ready");

        }

        public DebugMethodRegistry Register(MethodRegistryEntry RegisterEntry)
        {
            MethodBase methodBase = RegisterEntry.GetMethod();
            string thisMethodName = CallChain(GetType().ToStringWithGenerics(), nameof(Register));
            string declaringType = methodBase?.DeclaringType?.Name;
            bool value = RegisterEntry.GetValue();

            if (methodBase == null)
                MetricsManager.LogModWarning(
                    mod: ThisMod,
                    Message: $"{thisMethodName} passed null {nameof(MethodBase)}");

            Add(RegisterEntry);
            return this;
        }

        public DebugMethodRegistry Register<T>(
            T MethodBase,
            bool Value
            )
            where T : MethodBase
            => Register(new MethodRegistryEntry(MethodBase, Value))
            ;

        public DebugMethodRegistry Register(
            Type Class,
            string MethodName,
            bool Value
            )
            => Register(Class?.GetMethod(MethodName), Value)
            ;

        public DebugMethodRegistry Register(
            string MethodName,
            bool Value
            )
        {
            string thisMethodName = $"{nameof(Debug)}.{nameof(Register)}";
            if (TryGetCallingTypeAndMethod(out Type callingType, out MethodBase callingMethod))
            {
                bool any = false;
                foreach (MethodBase methodBase in callingType.GetMethods() ?? new MethodInfo[0])
                    if (methodBase.Name == MethodName)
                    {
                        any = true;
                        Register(methodBase, Value);
                    }
                if (!any)
                    MetricsManager.LogModWarning(
                        mod: ModManager.GetMod(callingType.Assembly),
                        Message: $"{CallerSignatureString(callingType, callingMethod)} failed to register any methods called {MethodName} with {thisMethodName}");
            }
            else
                MetricsManager.LogModWarning(ThisMod, $"{thisMethodName} couldn't get {nameof(callingType)}");

            return this;
        }

        public DebugMethodRegistry RegisterEachValue(
            Type Type,
            bool Value,
            params string[] Methods
            )
        {
            string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
            string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterEachValue));
            if (Methods.IsNullOrEmpty())
            {
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(Methods)} to {thisMethod}");
                return this;
            }
            try
            {
                foreach (MethodBase typeMethod in Type.GetMethods())
                {
                    if (Methods.Contains(typeMethod.Name))
                    {
                        var methodToRegister = typeMethod;
                        if (typeMethod.ContainsGenericParameters)
                        {
                            if (typeMethod is not MethodInfo typeMethodInfo
                                || typeMethodInfo.GetGenericArguments() is not Type[] genericArgs
                                || typeMethodInfo.MakeGenericMethod(genericArgs) is not MethodInfo madeMethod)
                            {
                                string methodSig = typeMethod.MethodSignature(true);
                                MetricsManager.LogModWarning(
                                    mod: ModManager.GetMod(Type.Assembly),
                                    Message: $"{callingTypeAndMethod} failed to make non-generic {methodSig} for {thisMethod}");
                                continue;
                            }
                            methodToRegister = madeMethod;
                        }
                        Register(methodToRegister, Value);
                    }
                }
            }
            catch (Exception x)
            {
                MetricsManager.LogException(thisMethod, x, GAME_MOD_EXCEPTION);
            }

            return this;
        }

        public DebugMethodRegistry RegisterEachValue<T>(
            Type Type,
            bool Value,
            params T[] Methods
            )
            where T : MethodBase
        {
            if (Methods.IsNullOrEmpty())
            {
                string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
                string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterEachValue));
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(Methods)} to {thisMethod}");
                return this;
            }
            foreach (MethodBase typeMethod in Type.GetMethods() ?? new MethodBase[0])
                if (Methods.Contains(typeMethod))
                    Register(typeMethod, Value);

            return this;
        }

        public DebugMethodRegistry RegisterEach(
            Type Type,
            Dictionary<string, bool> MethodNameValues
            )
        {
            if (MethodNameValues.IsNullOrEmpty())
            {
                string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
                string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterEach));
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(MethodNameValues)} to {thisMethod}");
                return this;
            }
            if (MethodNameValues.Values.Any(v => v))
                RegisterEachValue(Type, true, MethodNameValues.Where(e => e.Value).Select(e => e.Key).ToArray());

            if (MethodNameValues.Values.Any(v => !v))
                RegisterEachValue(Type, false, MethodNameValues.Where(e => !e.Value).Select(e => e.Key).ToArray());

            return this;
        }

        public DebugMethodRegistry RegisterEach<T>(
            Type Type,
            Dictionary<T, bool> MethodValues
            )
            where T : MethodBase
        {
            if (MethodValues.IsNullOrEmpty())
            {
                string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
                string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterEach));
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(MethodValues)} to {thisMethod}");
                return this;
            }
            if (MethodValues.Values.Any(v => v))
                RegisterEachValue(Type, true, MethodValues.Where(e => e.Value).Select(e => e.Key).ToArray());

            if (MethodValues.Values.Any(v => !v))
                RegisterEachValue(Type, false, MethodValues.Where(e => !e.Value).Select(e => e.Key).ToArray());

            return this;
        }

        public DebugMethodRegistry RegisterEachFalse(
            Type Type,
            params string[] Methods
            )
        {
            if (Methods.IsNullOrEmpty())
            {
                string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
                string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterEachFalse));
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(Methods)} to {thisMethod}");
                return this;
            }
            return RegisterEachValue(Type, false, Methods);
        }

        public DebugMethodRegistry RegisterEachFalse<T>(
            Type Type,
            params T[] Methods
            )
            where T : MethodBase
        {
            if (Methods.IsNullOrEmpty())
            {
                string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
                string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterEachFalse));
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(Methods)} to {thisMethod}");
                return this;
            }
            return RegisterEachValue(Type, false, Methods);
        }

        public DebugMethodRegistry RegisterEachTrue(
            Type Type,
            params string[] Methods)
        {
            if (Methods.IsNullOrEmpty())
            {
                string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
                string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterEachTrue));
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(Methods)} to {thisMethod}");
                return this;
            }
            return RegisterEachValue(Type, true, Methods);
        }

        public DebugMethodRegistry RegisterEachTrue<T>(
            Type Type,
            params T[] Methods)
            where T : MethodBase
        {
            if (Methods.IsNullOrEmpty())
            {
                string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
                string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterEachTrue));
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(Methods)} to {thisMethod}");
                return this;
            }
            return RegisterEachValue(Type, true, Methods);
        }

        private static Dictionary<MethodBase, bool> TypeValueMethodInfoValueAggregator(
            Dictionary<MethodBase, bool> Accumulator,
            MethodInfo Next,
            Dictionary<Type, bool> TypeValues
            )
        {
            if (Next.Name != nameof(GameObject.HandleEvent))
                return Accumulator;

            if (Next.GetParameters() is not ParameterInfo[] paramInfos
                || paramInfos.Length != 1)
                return Accumulator;

            if (paramInfos[0].ParameterType is not Type type
                || !TypeValues.ContainsKey(type))
                return Accumulator;

            Accumulator[Next] = TypeValues[type];
            return Accumulator;
        }

        public DebugMethodRegistry RegisterHandleEventVariants(
            Type Type,
            Dictionary<Type, bool> MinEventTypeValues
            )
        {
            if (MinEventTypeValues.IsNullOrEmpty())
            {
                string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
                string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterHandleEventVariants));
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(MinEventTypeValues)} to {thisMethod}");
                return this;
            }
            return RegisterEach(
                Type: Type,
                MethodValues: Type.GetMethods()?.Aggregate(
                    seed: new Dictionary<MethodBase, bool>(),
                    func: (acc, next) => TypeValueMethodInfoValueAggregator(acc, next, MinEventTypeValues)));
        }

        public bool Contains<T>(T MethodBase)
            where T : MethodBase
            => MethodBase is not null
            && this.Any(e => e.Equals(MethodBase as MethodBase))
            ;

        public bool Contains(Type DeclaringType, string MethodName)
            => !MethodName.IsNullOrEmpty()
            && DeclaringType
                ?.GetMethods()
                ?.Where(m => m.Name == MethodName)
                ?.Select(m => m as MethodBase) is MethodBase[] declaredTypeMethods
            && !declaredTypeMethods.IsNullOrEmpty()
            && declaredTypeMethods.Any(m => Contains(m))
            ;

        public bool Contains(string MethodName)
            => !MethodName.IsNullOrEmpty()
            && this.Any(e => e.GetMethod()?.Name == MethodName)
            ;

        public bool GetValue<T>(T MethodBase)
            where T : MethodBase
        {
            foreach (MethodRegistryEntry registryEntry in this)
                if (registryEntry.Equals(MethodBase as MethodBase))
                    return (bool)registryEntry;

            throw new ArgumentOutOfRangeException(nameof(MethodBase), "Not found.");
        }

        public bool TryGetValue<T>(
            T MethodBase,
            out bool Value
            )
            where T : MethodBase
            => (Value = Contains(MethodBase) && GetValue(MethodBase)) != default
            ;
    }
}
