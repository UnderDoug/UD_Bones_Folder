using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.UI;

using XRL;

using static UD_Bones_Folder.Mod.OsseousAsh.HostSet;

namespace UD_Bones_Folder.Mod.UI
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
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

    internal readonly struct ManageBonesPickOptionsDelegateEntry
    {
        internal static Type[] ValidParameters => new Type[2]
        {
            typeof(SaveBonesInfo),
            typeof(CascadablePickOptionDataSetAsync<SaveBonesInfo>),
        };

        internal static string ValidParamString => ValidParameters
            .Select(t => t.TypeStringWithGenerics())
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

        public static ManageBonesPickOptionsDelegateEntry Create(MethodInfo DelegateMethod)
        {
            if (!DelegateMethod.IsStatic)
                throw new DelegateModifierException(
                    Modifier: DelegateModifierException.ModifierType.Static,
                    Message: InvalidMethod("Must be static."));

            var targetReturnType = typeof(void);
            if (targetReturnType != DelegateMethod.ReturnType)
                throw new DelegateReturnTypeException(
                    Type: DelegateMethod.ReturnType,
                    RequiredType: targetReturnType,
                    Message: InvalidMethod($"Must return {targetReturnType.TypeStringWithGenerics()}, but returns {DelegateMethod.ReturnType.TypeStringWithGenerics()}."));

            var parameters = DelegateMethod.GetParameters();
            int paramCount = parameters?.Length ?? 0;
            string fewMany = paramCount < ValidParameters.Length ? "few" : "many";
            if (paramCount != ValidParameters.Length)
                throw new DelegateParameterException(InvalidMethod($"{paramCount} is too {fewMany} parameters. Must have exactly {ValidParameters.Length} ({ValidParamString})."));

            var validParams = ValidParameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (validParams.Length > i)
                    throw new DelegateParameterException(InvalidMethod($"{paramCount} is too many parameters. Must have exactly {ValidParameters.Length} ({ValidParamString})."));

                if (!validParams[i].IsAssignableFrom(parameters[i].ParameterType))
                    throw new DelegateParameterException(
                        ParameterInfos: parameters,
                        Message: InvalidMethod($"Parameter {i}, {parameters[i].ParameterType.TypeStringWithGenerics()}, cannot be assigned to {validParams[i].TypeStringWithGenerics()}."));
            }

            return new ManageBonesPickOptionsDelegateEntry(
                ProcessManageBonesPickOptions: (ManageBonesPickOptionsDelegate)DelegateMethod.CreateDelegate(typeof(ManageBonesPickOptionsDelegate))
                );
        }

        public static bool TryCreate(
            MethodInfo DelegateMethod,
            out ManageBonesPickOptionsDelegateEntry DelegateEntry,
            ModInfo ModInfo = null
            )
        {
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
