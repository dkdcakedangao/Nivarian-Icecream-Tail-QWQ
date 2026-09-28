using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Nivarian_Race.Code.AbilityOverride;
using UnityEngine;

namespace NivarianIcecreamTail
{
    // 预览扇形的入口~通过拦截，从而实现：薄荷吐息的范围颜色为蓝色~不可用目标时为红色~
    // 这真的有用么？我不道啊？
    [HarmonyPatch(typeof(CompAbilityEffect_DrakeFrostBreath), "DrawEffectPreview")]
    internal static class Patch_DrakeFrostBreath_MintPreviewColor
    {
        private static Color PreviewColor(CompAbilityEffect_DrakeFrostBreath effect)
        {
            return effect.parent != null && effect.parent.def != null &&
                effect.parent.def.defName == "IcecreamTailAbilityMintBreath"
                ? new Color(0.45f, 0.75f, 1f)
                : Color.cyan;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var cyanGetter = AccessTools.PropertyGetter(typeof(Color), "cyan");
            var colorMethod = AccessTools.Method(typeof(Patch_DrakeFrostBreath_MintPreviewColor), "PreviewColor");
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(cyanGetter))
                {
                    // 预载一下
                    yield return new CodeInstruction(OpCodes.Ldarg_0)
                        .MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = colorMethod;
                }
                yield return instruction;
            }
        }
    }
}
