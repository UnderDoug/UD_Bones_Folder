/*using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using XRL.Collections;
using XRL.World;

namespace UD_Bones_Folder.Mod.Harmony
{
    [HarmonyPatch(typeof(GameObject))]
    public static class GameObject_Patches
    {
        [HarmonyPatch(
            declaringType: typeof(GameObject),
            methodName: nameof(GameObject.DeepCopy),
            argumentTypes: new Type[] { typeof(bool), typeof(bool), typeof(Func<GameObject, GameObject>) })]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> DeepCopy_UseSafeAddRange_Transpile(IEnumerable<CodeInstruction> Instructions)
        {
            bool doVomit = false;
            string patchMethodName = $"{nameof(GameObject_Patches)}.{nameof(GameObject.DeepCopy)}({typeof(bool).Name}, {typeof(bool).Name}, {typeof(Func<GameObject, GameObject>).Name})";
            int metricsCheckSteps = 0;

            if (typeof(GameObject_Patches).GetMethods()
                .FirstOrDefault(
                    predicate: m => m.Name == nameof(AddRangeSafe)
                        && m.IsGenericMethod
                        && m.GetParameters().Length == 2)
                ?.GetParameters()
                is not ParameterInfo[] targetParamInfos)
            {
                Utils.Error($"{patchMethodName}: ({metricsCheckSteps}) failed to find target param types");
                return Instructions;
            }
            metricsCheckSteps++;

            if (typeof(global::Extensions).GetMethods()
                .FirstOrDefault(
                    predicate: m => m.Name == nameof(global::Extensions.AddRange)
                        && m.GetParameters() is ParameterInfo[] parameters
                        && parameters.Length == 2
                        && parameters[0].ParameterType.Name == targetParamInfos[0].ParameterType.Name
                        && parameters[1].ParameterType.Name == targetParamInfos[1].ParameterType.Name)
                is not MethodInfo addRangeMethod)
            {
                Utils.Error($"{patchMethodName}: ({metricsCheckSteps}) failed to find target method to replace");
                return Instructions;
            }
            metricsCheckSteps++;

            addRangeMethod = addRangeMethod.MakeGenericMethod(typeof(string), typeof(Guid));

            if (AccessTools.Method(
                type: typeof(GameObject_Patches),
                name: nameof(AddRangeSafe),
                parameters: new Type[2]
                {
                    typeof(IDictionary<string, Guid>),
                    typeof(IReadOnlyDictionary<string, Guid>),
                })
                is not MethodInfo addRangeSafeMethod)
            {
                Utils.Error($"{patchMethodName}: ({metricsCheckSteps}) failed to find replacement method");
                return Instructions;
            }
            metricsCheckSteps++;

            Utils.Info($"Successfully transpiled {patchMethodName}");
            return Instructions.MethodReplacer(addRangeMethod, addRangeSafeMethod).Vomit(doVomit);
        }

        public static void AddRangeSafe<K, V>(this IDictionary<K, V> Dictionary, IReadOnlyDictionary<K, V> Other)
        {
            if (Other.IsNullOrEmpty())
                return;

            foreach (var item in Other)
                if (!Dictionary.ContainsKey(item.Key))
                    Dictionary.Add(item.Key, item.Value);
        }

        public static void AddRangeSafe(this IDictionary<string, Guid> Dictionary, IReadOnlyDictionary<string, Guid> Other)
            => AddRangeSafe<string, Guid>(Dictionary, Other)
            ;
    }
}*/
