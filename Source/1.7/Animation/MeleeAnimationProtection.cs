using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace NivarianIcecreamTail
{
    [HarmonyPatch]
    internal static class Patch_CharacterAnimation_MeleeIdleProtection
    {
        private static Type idle;
        private static bool Prepare() { idle = AccessTools.TypeByName("AM.Idle.IdleControllerComp"); return idle != null; }
        private static MethodBase TargetMethod() { return AccessTools.Method(idle, "StartAnim"); }
        private static bool Prefix(ThingComp __instance)
        {
            return !IcecreamTailSkill3Runtime.HasNativeProtection || !IcecreamTailSkill3Runtime.IsProtected(__instance.parent as Pawn);
        }
    }

    // Gameplay ownership stays in the main module, even when animation is unavailable.
    [HarmonyPatch]
    internal static class Patch_CharacterAnimation_MeleeStart
    {
        private static Type parameters, renderer;
        private static bool Prepare()
        {
            parameters = AccessTools.TypeByName("AM.AnimationStartParameters");
            renderer = AccessTools.TypeByName("AM.AnimRenderer");
            return parameters != null && renderer != null;
        }
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(parameters, "TryTrigger", new[] { renderer.MakeByRefType() });
        }
        internal static bool Blocks(Pawn main, Pawn second, List<Pawn> extra)
        {
            if (IcecreamTailSkill3Runtime.IsProtected(main) || IcecreamTailSkill3Runtime.IsProtected(second)) return true;
            if (extra != null)
                for (int i = 0; i < extra.Count; i++) if (IcecreamTailSkill3Runtime.IsProtected(extra[i])) return true;
            return false;
        }
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var original = new List<CodeInstruction>(instructions);
            Label proceed = generator.DefineLabel();
            original[0].labels.Add(proceed);
            yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(IcecreamTailSkill3Runtime), "HasNativeProtection"));
            yield return new CodeInstruction(OpCodes.Brfalse, proceed);
            foreach (string field in new[] { "MainPawn", "SecondPawn", "ExtraPawns" })
            {
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(parameters, field));
            }
            yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patch_CharacterAnimation_MeleeStart), "Blocks"));
            yield return new CodeInstruction(OpCodes.Brfalse, proceed);
            yield return new CodeInstruction(OpCodes.Ldarg_1);
            yield return new CodeInstruction(OpCodes.Ldnull);
            yield return new CodeInstruction(OpCodes.Stind_Ref);
            yield return new CodeInstruction(OpCodes.Ldc_I4_0);
            yield return new CodeInstruction(OpCodes.Ret);
            foreach (var instruction in original) yield return instruction;
        }
    }
}
