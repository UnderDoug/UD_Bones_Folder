using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using Newtonsoft.Json;

namespace UD_Bones_Folder.Mod.Logging
{
    public interface IVomitable
    {
        private static BindingFlags AllInstance
            => BindingFlags.Instance
            | BindingFlags.Public
            | BindingFlags.NonPublic
            ;

        [NoVomit]
        [JsonIgnore]
        bool OptIn => false;

        [NoVomit]
        [JsonIgnore]
        bool DoVomit => Debug.DoDebugSetting;

        public static bool ParametersHasFlag(object[] Parameters, char Flag)
        {
            if (Parameters.IsNullOrEmpty())
                return false;

            foreach (var parameter in Parameters)
            {
                if (parameter is not string stringParam
                    || !stringParam.StartsWith("-")
                    || stringParam.StartsWith("--"))
                    continue;

                if (stringParam.Contains(Flag))
                    return true;
            }

            return false;
        }

        public static bool ParametersHasFlag(object[] Parameters, string Flag)
        {
            if (Parameters.IsNullOrEmpty())
                return false;

            foreach (var parameter in Parameters)
            {
                if (parameter is not string stringParam
                    || !stringParam.StartsWith("--"))
                    continue;

                if (stringParam[2..].Equals(Flag))
                    return true;
            }

            return false;
        }

        public static IEnumerable<KeyValuePair<MemberInfo, DoVomitAttribute>> GetVomitMembers(
            IVomitable Object,
            params object[] Parameters
            )
        {
            if (Object is null)
                yield break;

            var type = Object.GetType();

            foreach (var member in type.GetMembers(AllInstance).IteratorSafe())
            {
                if (member.GetCustomAttribute<NoVomitAttribute>() != null)
                    continue;

                if (member.DeclaringType != type)
                    if (ParametersHasFlag(Parameters, 'd')
                        || ParametersHasFlag(Parameters, "declaredOnly"))
                        continue;

                var doVomit = member.GetCustomAttribute<DoVomitAttribute>();

                if (Object.OptIn
                    && doVomit == null)
                    continue;

                yield return new(member, doVomit);
            }
        }

        public static IVomitable Vomit(
            IVomitable Object,
            bool ForceName = false,
            Indent Indent = null,
            [CallerMemberName] string CallingMethod = "",
            params object[] Parameters
            )
        {
            if (Object is null)
                return Object;

            using var indent = new Indent(Indent ?? Debug.LastIndent);

            var type = Object.GetType();

            if (ForceName
                || (!ParametersHasFlag(Parameters, "noName")
                    && !ParametersHasFlag(Parameters, 'n')))
            {
                string typeName = type.ToStringWithGenerics();
                if (ParametersHasFlag(Parameters, "namespace")
                    || ParametersHasFlag(Parameters, 'N'))
                    typeName = $"{type.Namespace}.{typeName}";

                Debug.Log(typeName, Indent: indent);
            }

            foreach ((var member, var doVomit) in GetVomitMembers(Object, Parameters))
            {
                try
                {
                    bool valueOnly = doVomit?.ValueOnly is true;

                    string label = valueOnly ? doVomit?.Label ?? member.Name : null;

                    if (label != null)
                        label = $"{label}: ";

                    if (member is FieldInfo field)
                    {
                        var fieldValue = field.GetValue(Object);

                        if (fieldValue is IVomitable vomitableField)
                            Vomit(
                                Object: vomitableField,
                                ForceName: true,
                                Indent: indent[1],
                                CallingMethod: CallingMethod,
                                Parameters: Parameters);
                        else
                            Debug.Log(
                                Message: $"{label ?? field.Name}: {fieldValue?.ToString() ?? "null"}",
                                Indent: indent[1],
                                CallingMethod: CallingMethod);
                    }
                    else
                    if (member is PropertyInfo property)
                    {
                        var propValue = property.GetValue(Object);

                        if (propValue is IVomitable vomitableProp)
                            Vomit(
                                Object: vomitableProp,
                                ForceName: true,
                                Indent: indent[1],
                                CallingMethod: CallingMethod,
                                Parameters: Parameters);
                        else
                            Debug.Log(
                                Message: $"{label ?? property.Name}: {propValue?.ToString() ?? "null"}",
                                Indent: indent[1],
                                CallingMethod: CallingMethod);
                    }
                }
                catch (Exception x)
                {
                    Utils.Error($"{type} failed to {nameof(Vomit)} {nameof(member)} {member.Name}", x);
                }
            }

            return Object;
        }

        IEnumerable<KeyValuePair<MemberInfo, DoVomitAttribute>> GetVomitMembers(params object[] Parameters)
            => GetVomitMembers(this, Parameters)
            ;

        IVomitable Vomit(
            bool ForceName = false,
            Indent Indent = null,
            [CallerMemberName] string CallingMethod = "",
            params object[] Parameters
            )
            => Vomit(
                Object: this,
                ForceName: ForceName,
                Indent: Indent,
                CallingMethod: CallingMethod,
                Parameters: Parameters)
            ;
    }

    public interface IVomitable<T> : IVomitable where T : IVomitable<T>
    {
        new T Vomit(
            bool ForceName = false,
            Indent Indent = null,
            [CallerMemberName] string CallingMethod = "",
            params object[] Parameters
            )
            => (T)((IVomitable)this).Vomit(
                ForceName: ForceName,
                Indent: Indent,
                CallingMethod: CallingMethod,
                Parameters: Parameters)
            ;
    }
}
