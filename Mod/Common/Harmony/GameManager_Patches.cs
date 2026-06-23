using System;
using System.Collections.Generic;
using System.Reflection.Emit;

using HarmonyLib;

namespace UD_Bones_Folder.Mod.Harmony
{
    [HarmonyPatch(typeof(GameManager))]
    public static class GameManager_Patches
    {
        public const string METHOD_NAME = "StartGameThread";

        [HarmonyPatch(
            declaringType: typeof(GameManager),
            methodName: METHOD_NAME)]
        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> StartGameThread_UseSetParent_Transpile(
            IEnumerable<CodeInstruction> Instructions,
            ILGenerator Generator
            )
        {
            bool doVomit = false;
            string patchMethodName = $"{nameof(GameManager_Patches)}.{METHOD_NAME}()";
            int metricsCheckSteps = 0;

            CodeMatcher codeMatcher = new(Instructions, Generator);

            // OverlayCharacter[m, n].gameObject.transform.parent = OverlayRoot.transform;
            // just the "parent = " portion.
            var match_set_parent = new CodeMatch(ins => ins.Calls(AccessTools.PropertySetter(typeof(UnityEngine.Transform), nameof(UnityEngine.Transform.parent))));

            if (codeMatcher.Start().MatchStartForward(match_set_parent).IsInvalid)
            {
                Utils.Error($"{patchMethodName}: ({metricsCheckSteps}) {nameof(CodeMatcher.MatchStartForward)} failed to find instructions {nameof(match_set_parent)}");
                Utils.Error($"{patchMethodName}:     {match_set_parent.opcode} {match_set_parent.operand}");
                codeMatcher.Vomit(Generator, doVomit);
                return Instructions;
            }
            metricsCheckSteps++;

            // replace with transform.SetParent(Transform, false)
            var instr_SetParent_worldPositionStays_False = new CodeInstruction[2]
            {
                new(OpCodes.Ldc_I4_0),
                new(
                    opcode: OpCodes.Call,
                    operand: AccessTools.Method(
                        type: typeof(UnityEngine.Transform),
                        name: nameof(UnityEngine.Transform.SetParent),
                        parameters: new Type[]
                        {
                            typeof(UnityEngine.Transform),
                            typeof(bool),
                        }))
            };

            codeMatcher
                .RemoveInstruction()
                .Insert(instr_SetParent_worldPositionStays_False)
                ;

            Utils.Info($"Successfully transpiled {patchMethodName}");
            return codeMatcher.Vomit(Generator, doVomit).InstructionEnumeration();
        }
    }
}
