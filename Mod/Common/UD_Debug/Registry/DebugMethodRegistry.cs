using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using HarmonyLib;

using XRL;
using XRL.Collections;
using XRL.World;

using static UD_Bones_Folder.Mod.Utils;
using static UD_Bones_Folder.Mod.Const;
using static UD_Bones_Folder.Mod.Logging.Debug;

namespace UD_Bones_Folder.Mod.Logging
{
    [HasModSensitiveStaticCache]
    public class DebugMethodRegistry : CoalescibleSet<MethodRegistryEntry>
    {
        public enum LoggingLevel
        {
            /// <summary>No logging under any circumstances</summary>
            None,

            /// <summary>Logging when <see cref="Options.DebugEnableLogging"/> is <see langword="true"/>, and <see cref="Debug.SilenceLogging"/> is <see langword="false"/></summary>
            Setting,

            /// <summary>Always logs no matter what</summary>
            Always,
        }

        public const BindingFlags BINDING_FLAGS_ALL_DECLARED = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        public static readonly object _MethodRegistryLock = new();
        // [ModSensitiveStaticCache(CreateEmptyInstance = false)]
        private static DebugMethodRegistry _Instance;
        public static DebugMethodRegistry Instance => GetRegistry();

        private static bool _GotRegistry = false;

        public DebugMethodRegistry()
            : base()
        { }

        public bool this[MethodBase MethodBase]
            => !TryGetValue(MethodBase, out bool value)
            || value
            ;

        public bool this[MethodInfo MethodInfo]
            => !TryGetValue(MethodInfo, out bool value)
            || value
            ;

        public static DebugMethodRegistry GetRegistry()
        {
            lock (_MethodRegistryLock)
            {
                if (_GotRegistry)
                    return _Instance;

                _Instance ??= new();
                string thisMethod = CallChain(nameof(DebugMethodRegistry), nameof(GetRegistry));

                var methodAttributeType = typeof(RegisterAsyncMethodForDebugAttribute);
                var classAttributeType = typeof(HasRegisterMethodForDebugAttribute);
                //Utils.Log(thisMethod);
                if (ModManager.GetMethodsWithAttribute(methodAttributeType, classAttributeType) is not List<MethodInfo> asyncSelfRegistrationMethods
                    || asyncSelfRegistrationMethods.IsNullOrEmpty())
                    Info($"{thisMethod} didn't find any {methodAttributeType.ToStringWithGenerics()} decorated methods");
                else
                {
                    //Utils.Log($"{1.Indent()}{nameof(asyncSelfRegistrationMethods)}: {asyncSelfRegistrationMethods.GetNullString()}");
                    foreach (var asyncSelfRegistrationMethod in asyncSelfRegistrationMethods)
                    {
                        var attributes = asyncSelfRegistrationMethod.GetCustomAttributes<RegisterAsyncMethodForDebugAttribute>();
                        var declaringType = asyncSelfRegistrationMethod.DeclaringType;
                        var mod = ModManager.GetMod(declaringType.Assembly) ?? ThisMod;
                        // var parameters = asyncSelfRegistrationMethod.GetParameters().Select(pi => pi.ParameterType).ToArray();
                        string methodName = asyncSelfRegistrationMethod.Name;
                        string fullMethod = CallChain(declaringType.ToStringWithGenerics(), asyncSelfRegistrationMethod.MethodSignature(true));

                        //Utils.Log($"{2.Indent()}registering {methodName} for {attributes.Count().Things("attribute")}");
                        foreach (var attribute in attributes)
                        {
                            var doDebug = attribute.DoDebug;
                            try
                            {
                                if (attribute.MethodGenerics is not Type[] methodGenerics
                                    || methodGenerics.IsNullOrEmpty())
                                {
                                    _Instance.Register(MethodRegistryEntry.NewAsyncEntry(declaringType, methodName, doDebug, SelfRegistered: true));
                                    //Utils.Log($"{3.Indent()}Registered non-generic");
                                }
                                else
                                {
                                    _Instance.Register(MethodRegistryEntry.NewAsyncEntry(declaringType, methodName, null, attribute.MethodGenerics, doDebug, SelfRegistered: true));
                                    string genericsString = attribute.MethodGenerics.Aggregate((string)null, (a, n) => CommaSpaceDelimitedAggregator(a, n.ToStringWithGenerics()));
                                    //Utils.Log($"{3.Indent()}Registered as generic <{genericsString}>");
                                }
                            }
                            catch (Exception x)
                            {
                                mod.Error($"{thisMethod} failed to {nameof(RegisterAsyncMethod)} {fullMethod}: {x}");
                            }
                        }
                    }
                }

                methodAttributeType = typeof(RegisterMethodForDebugAttribute);
                if (ModManager.GetMethodsWithAttribute(methodAttributeType, classAttributeType) is not List<MethodInfo> selfRegistrationMethods
                    || selfRegistrationMethods.IsNullOrEmpty())
                    Info($"{thisMethod} didn't find any {methodAttributeType.ToStringWithGenerics()} decorated methods");
                else
                {
                    //Utils.Log($"{1.Indent()}{nameof(selfRegistrationMethods)}: {selfRegistrationMethods.GetNullString()}");
                    foreach (var selfRegistrationMethod in selfRegistrationMethods)
                    {
                        var attributes = selfRegistrationMethod.GetCustomAttributes<RegisterMethodForDebugAttribute>();
                        var declaringType = selfRegistrationMethod.DeclaringType;
                        var mod = ModManager.GetMod(declaringType.Assembly) ?? ThisMod;
                        string fullMethod = CallChain(declaringType.ToStringWithGenerics(), selfRegistrationMethod.MethodSignature(true));
                        //Utils.Log($"{2.Indent()}registering {selfRegistrationMethod.Name} for {attributes.Count().Things("attribute")}");
                        foreach (var attribute in attributes)
                        {
                            var doDebug = attribute.DoDebug;
                            try
                            {
                                if (attribute.MethodGenerics is Type[] methodGenerics
                                    && !methodGenerics.IsNullOrEmpty())
                                {
                                    string genericsString = methodGenerics.Aggregate((string)null, (a, n) => CommaSpaceDelimitedAggregator(a, n?.Name ?? "MISSING"));
                                    try
                                    {
                                        _Instance.Register(new(selfRegistrationMethod.MakeGenericMethod(methodGenerics), doDebug, SelfRegistered: true));
                                        //Utils.Log($"{3.Indent()}Registered as generic <{genericsString}>");
                                    }
                                    catch (Exception x)
                                    {
                                        mod.Error($"{thisMethod} failed to make {fullMethod} into generic method with types {genericsString ?? "MISSING_TYPES"}: {x}");
                                    }
                                    continue;
                                }
                                _Instance.Register(new(selfRegistrationMethod, doDebug, SelfRegistered: true));
                                //Utils.Log($"{3.Indent()}Registered non-generic");
                            }
                            catch (Exception x)
                            {
                                mod.Error($"{thisMethod} failed to {nameof(RegisterAsyncMethod)} {fullMethod}: {x}");
                            }
                        }
                    }
                }

                if (ModManager.GetMethodsWithAttribute(typeof(RegistersMethodsForDebugAttribute)) is not List<MethodInfo> debugRegistryMethods
                    || debugRegistryMethods.IsNullOrEmpty())
                {
                    Error($"{thisMethod} failed to retrieve any {nameof(RegistersMethodsForDebugAttribute)} decorated methods");
                    return _Instance;
                }
                foreach (var debugRegistryMethod in debugRegistryMethods)
                {
                    try
                    {
                        var methodToInvoke = debugRegistryMethod;
                        var mod = ModManager.GetMod(debugRegistryMethod.DeclaringType.Assembly) ?? ThisMod;
                        string fullMethod = CallChain(debugRegistryMethod.DeclaringType.ToStringWithGenerics(), debugRegistryMethod.MethodSignature(true));
                        if (methodToInvoke.ContainsGenericParameters)
                        {
                            if (debugRegistryMethod.GetGenericArguments() is not Type[] genericArgs
                                || debugRegistryMethod.MakeGenericMethod(genericArgs) is not MethodInfo madeMethod)
                            {
                                mod.Warn($"Generic method {fullMethod} found during {thisMethod}");
                                continue;
                            }
                            methodToInvoke = madeMethod;
                        }
                        methodToInvoke.Invoke(null, new object[] { _Instance });
                    }
                    catch (Exception x)
                    {
                        MetricsManager.LogException(thisMethod, x, GAME_MOD_EXCEPTION);
                    }
                }
                return _Instance;
            }
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
            lock (_MethodRegistryLock)
            {
                if (!DoDebugSetting)
                    return false;

                if (CallingMethod.IsNullOrEmpty()
                    || Instance.IsNullOrEmpty())
                    return DoDebugSetting;

                if (!Instance.Contains(CallingMethod))
                {
                    // WarnOnce($"{nameof(DebugMethodRegistry)}.{nameof(Instance)} does not contain {CallingMethod}");
                    return DoDebugSetting;
                }

                try
                {
                    if (!TryGetCallingTypeAndMethod(out _, out var callingMethodBase))
                    {
                        // WarnOnce($"{nameof(DebugMethodRegistry)} {nameof(TryGetCallingTypeAndMethod)} for {CallingMethod} but failed");
                        return DoDebugSetting;
                    }

                    if (!Instance.TryGetValue(callingMethodBase, out bool? registryMethodValue, out bool? registryMethodGenericValue))
                    {
                        // WarnOnce($"{nameof(DebugMethodRegistry)}.{nameof(Instance)}.{nameof(TryGetValue)} for {callingMethodBase.Name} but failed");
                        return DoDebugSetting;
                    }

                    if (registryMethodValue.GetValueOrDefault())
                    {
                        // WarnOnce($"{nameof(DebugMethodRegistry)} got {nameof(registryMethodValue)} {registryMethodValue} for {callingMethodBase.Name}");
                        return DoDebugSetting;
                    }

                    if (registryMethodGenericValue.GetValueOrDefault())
                    {
                        // WarnOnce($"{nameof(DebugMethodRegistry)} got {nameof(registryMethodGenericValue)} {registryMethodGenericValue} for {callingMethodBase.Name}");
                        return DoDebugSetting;
                    }
                    // WarnOnce($"{nameof(DebugMethodRegistry)} got {nameof(registryMethodValue)} {registryMethodValue} for {callingMethodBase.Name}");
                }
                catch (Exception x)
                {
                    MetricsManager.LogException(CallChain(nameof(DebugMethodRegistry), nameof(GetDoDebug)), x, GAME_MOD_EXCEPTION);
                }
                return false;
            }
        }

        public static void LogRegistry()
        {
            lock (_MethodRegistryLock)
            {
                string debug = "DEBUG ";
                string thisMod = $"[{ThisMod.DisplayTitleStripped}] - ";
                string callChain = CallChain(nameof(DebugMethodRegistry), nameof(LogRegistry));
                if (!Instance.IsNullOrEmpty())
                {
                        UnityEngine.Debug.Log($"{debug}{thisMod}Start of {callChain}");

                        foreach (var methodEntry in Instance.OrderBy(dmr => dmr.GetTypeAndMethodName(true, true)))
                            UnityEngine.Debug.Log($"{debug}{methodEntry.ToString(IncludeSourceMod: true, FullSignature: true, IncludeNamespace: true)}");

                        UnityEngine.Debug.Log($"{debug}{thisMod}End of {callChain}");
                }
                else
                    UnityEngine.Debug.Log($"{debug}{thisMod}{callChain} called but {nameof(Instance)} not ready");
            }
        }

        public DebugMethodRegistry Register(MethodRegistryEntry RegisterEntry)
        {
            if (RegisterEntry.GetMethod() == null)
            {
                Warn($"{CallChain(GetType().ToStringWithGenerics(), nameof(Register))} passed null {nameof(MethodBase)} in {RegisterEntry.GetCallerFilePosition()}");
                return this;
            }
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
            if (TryGetCallingTypeAndMethod(out var callingType, out var callingMethod))
            {
                bool any = false;
                foreach (var method in callingType.GetMethods(BINDING_FLAGS_ALL_DECLARED).IteratorSafe())
                {
                    if (method.Name == MethodName)
                    {
                        any = true;
                        Register(method, Value);
                    }
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
                foreach (var typeMethod in Type.GetMethods(BINDING_FLAGS_ALL_DECLARED).IteratorSafe())
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
            foreach (var typeMethod in Type.GetMethods(BINDING_FLAGS_ALL_DECLARED).IteratorSafe())
                if (Methods.Contains(typeMethod as MethodBase))
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

        public DebugMethodRegistry RegisterEach<T>(
            Type Type,
            params KeyValuePair<T, bool>[] MethodValues
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
            if (MethodValues.Any(kvp => kvp.Value))
                RegisterEachValue(Type, true, MethodValues.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray());

            if (MethodValues.Any(kvp => !kvp.Value))
                RegisterEachValue(Type, false, MethodValues.Where(kvp => !kvp.Value).Select(kvp => kvp.Key).ToArray());

            return this;
        }

        public DebugMethodRegistry RegisterEach(
            Type Type,
            params MethodRegistryEntry[] Entries
            )
        {
            if (Entries.IsNullOrEmpty())
            {
                string callingTypeAndMethod = CallingTypeAndMethodNames(ConvertGenerics: true);
                string thisMethod = CallChain(GetType().ToStringWithGenerics(), nameof(RegisterEach));
                MetricsManager.LogModWarning(
                    mod: ModManager.GetMod(Type.Assembly),
                    Message: $"{callingTypeAndMethod} passed empty {nameof(Entries)} to {thisMethod}");
                return this;
            }

            foreach (var entry in Entries)
                Register(entry);

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
                MethodValues: Type.GetMethods(BINDING_FLAGS_ALL_DECLARED)
                    ?.Aggregate(
                        seed: new Dictionary<MethodBase, bool>(),
                        func: (acc, next) => TypeValueMethodInfoValueAggregator(acc, next, MinEventTypeValues)
                    )
                );
        }

        public static bool TypeFilter(
            Type Type,
            string MethodName,
            string InnerMethodName,
            Type[] Parameters = null,
            Type[] InnerGenerics = null
            )
            => Type.Name.StartsWith($"<{MethodName}>")
            && AccessTools.GetDeclaredMethods(Type).Any(m
                => m.Name == InnerMethodName
                && (Parameters.IsNullOrEmpty()
                    || Parameters.ElementsMatch(m.GetParameters()?.Select(p => p.ParameterType).ToArray())
                && (InnerGenerics.IsNullOrEmpty()
                    || InnerGenerics.Length == (m.GetGenericArguments()?.Length ?? 0)))
            )
            ;

        public static MethodBase GetMethodInnerMethod(
            Type Type,
            string Name,
            string InnerMethodName,
            Type[] Parameters = null,
            Type[] MethodTypeGenerics = null,
            Type[] InnerGenerics = null
            )
        {
            if (AccessTools.FirstInner(Type, t
                => TypeFilter(
                    Type: t,
                    MethodName: Name,
                    InnerMethodName: InnerMethodName,
                    Parameters: Parameters,
                    InnerGenerics: InnerGenerics)
                ) is not Type methodInnerType)
                return AccessTools.Method(Type, Name, Parameters, MethodTypeGenerics);

            if (!MethodTypeGenerics.IsNullOrEmpty())
                methodInnerType = methodInnerType?.MakeGenericType(MethodTypeGenerics);

            if (AccessTools.Method(methodInnerType, InnerMethodName, Parameters, InnerGenerics) is not MethodInfo method)
                return null;

            if (!InnerGenerics.IsNullOrEmpty())
                method.MakeGenericMethod(InnerGenerics);

            return method;
        }

        public static MethodBase GetAsyncMethod(
            Type Type,
            string Name,
            Type[] Parameters = null,
            Type[] MethodGenerics = null
            )
            => GetMethodInnerMethod(
                Type: Type,
                Name: Name,
                InnerMethodName: "MoveNext",
                Parameters: Parameters,
                MethodTypeGenerics: MethodGenerics,
                InnerGenerics: null)
            ;

        public DebugMethodRegistry RegisterAsyncMethod(
            Type Class,
            string MethodName,
            Type[] Parameters,
            Type[] MethodGenerics,
            bool Value
            )
            => Register(GetAsyncMethod(Class, MethodName, Parameters, MethodGenerics), Value)
            ;

        public DebugMethodRegistry RegisterAsyncMethod(
            Type Class,
            string MethodName,
            bool Value,
            Type[] Parameters = null,
            Type[] Generics = null
            )
            => RegisterAsyncMethod(Class, MethodName, Parameters, Generics, Value)
            ;

        public bool Contains<T>(T MethodBase, out bool HasStrictValue, out bool HasGenericValue)
            where T : MethodBase
        {
            HasStrictValue = false;
            HasGenericValue = false;
            if (MethodBase == null)
                return false;

            HasStrictValue = this.Any(e => e.Equals(MethodBase, Strict: true));
            HasGenericValue = this.Any(e => e.EqualsGeneric(MethodBase));

            return HasStrictValue
                || HasGenericValue
                ;
        }

        public bool Contains<T>(T MethodBase, bool Strict = true)
            where T : MethodBase
            => MethodBase != null
            && this.Any(e => e.Equals(MethodBase, Strict))
            ;

        public bool Contains(Type DeclaringType, string MethodName)
            => !MethodName.IsNullOrEmpty()
            && DeclaringType
                ?.GetMethods(BINDING_FLAGS_ALL_DECLARED).IteratorSafe()
                ?.Where(m => m.Name == MethodName)
                ?.Select(m => m as MethodBase) is IEnumerable<MethodBase> declaredTypeMethods
            && !declaredTypeMethods.IsNullOrEmpty()
            && declaredTypeMethods.Any(m => Contains(m))
            ;

        public bool Contains(string MethodName)
            => !MethodName.IsNullOrEmpty()
            && this.Any(e
                    => e.MethodName == MethodName
                    || (e.DeclaringTypeName?.StartsWith($"<{MethodName}>") is true)
                )
            ;

        public bool GetValue<T>(T MethodBase)
            where T : MethodBase
        {
            foreach (var entry in this)
                if (entry.Equals(MethodBase) is true)
                    return (bool)entry;

            throw new ArgumentOutOfRangeException(nameof(MethodBase), "Not found.");
        }

        public bool GetGenericValue<T>(T MethodBase)
            where T : MethodBase
        {
            foreach (var entry in this)
                if (entry.EqualsGeneric(MethodBase))
                    return (bool)entry;

            throw new ArgumentOutOfRangeException(nameof(MethodBase), "Not found.");
        }

        public bool TryGetValue<T>(
            T MethodBase,
            out bool? Value,
            out bool? Generic
            )
            where T : MethodBase
        {
            Value = null;
            Generic = null;
            if (!Contains(MethodBase, out bool hasValue, out bool hasGenericValue))
                return false;

            if (hasValue)
                Value = GetValue(MethodBase);

            if (hasGenericValue)
                Generic = GetGenericValue(MethodBase);

            return Value.HasValue
                || Generic.HasValue
                ;
        }

        public bool TryGetValue<T>(
            T MethodBase,
            out bool Value
            )
            where T : MethodBase
        {
            Value = default;
            if (!TryGetValue(MethodBase, out bool? strictValue, out bool? genericValue))
                return false;

            Value = (strictValue ?? genericValue).GetValueOrDefault();

            return true;
        }

        public bool TryGetStrictValue<T>(
            T MethodBase,
            out bool Value
            )
            where T : MethodBase
        {
            Value = default;
            if (!Contains(MethodBase))
                return false;

            Value = GetValue(MethodBase);

            return true;
        }
    }
}
