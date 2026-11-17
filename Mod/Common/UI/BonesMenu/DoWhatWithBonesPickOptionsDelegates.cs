using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.Logging;
using UD_Bones_Folder.Mod.UI;

using XRL;

using static UD_Bones_Folder.Mod.OsseousAsh.HostSet;

namespace UD_Bones_Folder.Mod.UI
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    public class HasManageBonesPickOptionsDelegateAttribute : Attribute
    {
    }

    /// <summary>
    /// Methods decorated with this attribute should meet the following requirements:<br />
    /// - be <see langword="static"/>, and<br />
    /// - match the full signature of <see cref="ManageBonesPickOptionsDelegate"/><br />
    /// <br />
    /// It will be cached as a delegatge and called any time <see cref="BonesManagement"/> wants to <see cref="BonesManagement.AskHowManageBones(SaveBonesInfo)"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class ManageBonesPickOptionsDelegateAttribute : Attribute
    {
    }

    public delegate void ManageBonesPickOptionsDelegate(SaveBonesInfo BonesInfo, ref CascadablePickOptionDataSetAsync<SaveBonesInfo> Options);

    [HasRegisterMethodForDebug]
    internal readonly struct ManageBonesPickOptionsDelegateEntry
    {
        internal static Type[] ValidParameters => new Type[2]
        {
            typeof(SaveBonesInfo),
            typeof(CascadablePickOptionDataSetAsync<SaveBonesInfo>),
        };

        internal static string ValidParamString => ValidParameters
            .Select(t => t.ToStringWithGenerics())
            .Aggregate((string)null, Utils.CommaSpaceDelimitedAggregator)
            ;

        public readonly ManageBonesPickOptionsDelegate ProcessManageBonesPickOptions;

        public ManageBonesPickOptionsDelegateEntry(ManageBonesPickOptionsDelegate ProcessManageBonesPickOptions)
        {
            this.ProcessManageBonesPickOptions = ProcessManageBonesPickOptions;
        }

        private static string InvalidMethod(string Message = null)
            => $"Invalid method decorated with {nameof(ManageBonesPickOptionsDelegateEntry)}{(Message.IsNullOrEmpty() ? "." : $"; {Message}")}"
            ;

        [RegisterMethodForDebug(true)]
        public static ManageBonesPickOptionsDelegateEntry Create(MethodInfo DelegateMethod)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(DelegateMethod), DelegateMethod?.MethodSignature() ?? "MISSING_METHOD"),
                });

            if (!DelegateMethod.IsStatic)
                throw new DelegateModifierException(
                    Modifier: DelegateModifierException.ModifierType.Static,
                    Message: InvalidMethod("Must be static."));
            Debug.CheckYeh(nameof(DelegateMethod.IsStatic), Indent: indent[2]);

            var targetReturnType = typeof(void);
            if (targetReturnType != DelegateMethod.ReturnType)
                throw new DelegateReturnTypeException(
                    Type: DelegateMethod.ReturnType,
                    RequiredType: targetReturnType,
                    Message: InvalidMethod($"Must return {targetReturnType.ToStringWithGenerics()}, but returns {DelegateMethod.ReturnType.ToStringWithGenerics()}."));
            Debug.CheckYeh(nameof(DelegateMethod.ReturnType), Indent: indent[2]);

            var parameters = DelegateMethod.GetParameters();
            string paramString = parameters
                .Select(p => p.ParameterType.ToStringWithGenerics())
                .Aggregate((string)null, Utils.CommaSpaceDelimitedAggregator);
            int paramCount = parameters?.Length ?? 0;
            string fewMany = paramCount < ValidParameters.Length ? "few" : "many";
            if (paramCount != ValidParameters.Length)
                throw new DelegateParameterException(InvalidMethod($"{paramCount} is too {fewMany} parameters ({paramString}). Must have exactly {ValidParameters.Length} ({ValidParamString})."));
            Debug.CheckYeh(nameof(parameters), Indent: indent[2]);

            var validParams = ValidParameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (i >= validParams.Length)
                    throw new DelegateParameterException(InvalidMethod($"{paramCount} is too many parameters ({paramString}). Must have exactly {ValidParameters.Length} ({ValidParamString})."));

                string paramName = parameters[i].ParameterType.ToStringWithGenerics();
                if (parameters[i].ParameterType != validParams[i]
                    && !validParams[i].IsAssignableFrom(parameters[i].ParameterType)
                    && parameters[i].ParameterType.IsAssignableFrom(validParams[i]))
                    throw new DelegateParameterException(
                        ParameterInfos: parameters,
                        Message: InvalidMethod($"Parameter {i}, {paramName}, is not, and cannot be cast to or from, {validParams[i].ToStringWithGenerics()}."));
                Debug.CheckYeh($"ParamType {i}", paramName, Indent: indent[2]);
            }
            Debug.CheckYeh($"new {nameof(ManageBonesPickOptionsDelegateEntry)}", Indent: indent[1]);
            return new ManageBonesPickOptionsDelegateEntry((ManageBonesPickOptionsDelegate)DelegateMethod.CreateDelegate(typeof(ManageBonesPickOptionsDelegate)));
        }

        [RegisterMethodForDebug(true)]
        public static bool TryCreate(
            MethodInfo DelegateMethod,
            out ManageBonesPickOptionsDelegateEntry DelegateEntry,
            ModInfo ModInfo = null
            )
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(DelegateMethod), DelegateMethod?.MethodSignature() ?? "MISSING_METHOD"),
                    Debug.ArgOut(nameof(DelegateEntry)),
                    Debug.Arg(nameof(ModInfo), ModInfo?.ID ?? "MISSING_MOD_INFO"),
                });

            var modInfo = ModInfo ?? Utils.ThisMod;
            try
            {
                DelegateEntry = Create(DelegateMethod);
                return true;
            }
            catch (Exception x)
            {
                modInfo.Warn($"Failed creating {nameof(DelegateEntry)} for {DelegateMethod?.Name ?? "MISSING_METHOD"}: {x}");
                DelegateEntry = default;
                return false;
            }
        }
    }
}
