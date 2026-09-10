using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// 这也是抄kp的~~
// 爽啦！
namespace NivarianIcecreamTail
{
    public sealed class Command_IcecreamTailAbility : Command_Action
    {
        private const float ButtonSize = 75f;
        private static readonly Texture2D Background = ContentFinder<Texture2D>.Get("UI/skills/gizmo_background");
        private static readonly Texture2D RedBackground = ContentFinder<Texture2D>.Get("UI/skills/gizmo_background_red");
        private static readonly Texture2D Border = ContentFinder<Texture2D>.Get("Nivarian/Icon/iceflake_gizmo");
        private readonly Ability ability;
        private readonly Command_Ability abilityCommand;
        private readonly Texture2D background;

        // 这个public是gpt老师优化的
        // 虽然我感觉没啥区别
        public Command_IcecreamTailAbility(Ability ability, Pawn pawn)
        {
            this.ability = ability;
            abilityCommand = new Command_Ability(ability, pawn);
            background = ability.def.defName == IcecreamTailTemporaryAbilityUtility.Skill3AbilityDefName ? RedBackground : Background;
            defaultLabel = ability.def.LabelCap;
            defaultDesc = ability.Tooltip;
            icon = ability.def.uiIcon;
            iconDrawScale = 0.98f;
            shrinkable = false;
            groupable = false;
        }

        public override void ProcessInput(Event ev)
        {
            abilityCommand.ProcessInput(ev);
        }

        public override Texture2D BGTexture
        {
            get { return background; }
        }

        public override Texture2D BGTextureShrunk
        {
            get { return background; }
        }

        public override bool Disabled
        {
            get
            {
                bool value = abilityCommand.Disabled;
                base.Disabled = value;
                disabledReason = abilityCommand.disabledReason;
                return value;
            }
            set
            {
                base.Disabled = value;
                abilityCommand.Disabled = value;
                disabledReason = abilityCommand.disabledReason;
            }
        }

        public override string TopRightLabel
        {
            get { return abilityCommand.TopRightLabel; }
        }

        public override float GetWidth(float maxWidth)
        {
            return ButtonSize;
        }

        protected override GizmoResult GizmoOnGUIInt(Rect butRect, GizmoRenderParms parms)
        {
            GizmoResult result = base.GizmoOnGUIInt(butRect, parms);
            DrawCooldown(butRect);
            DrawInteractionChrome(butRect);
            GUI.DrawTexture(butRect, Border);
            return result;
        }

        private void DrawInteractionChrome(Rect butRect)
        {
            if (Disabled)
            {
                Widgets.DrawBoxSolid(butRect, new Color(0f, 0f, 0f, 0.25f));
            }

            if (Mouse.IsOver(butRect))
            {
                Widgets.DrawHighlight(butRect);
            }
        }

        private void DrawCooldown(Rect butRect)
        {
            int remaining = ability.CooldownTicksRemaining;
            int total = ability.CooldownTicksTotal;
            if (remaining <= 0 || total <= 0)
            {
                return;
            }

            float fraction = Mathf.Clamp01((float)remaining / total);
            Rect fillRect = new Rect(butRect.x, butRect.y + butRect.height * (1f - fraction), butRect.width, butRect.height * fraction);
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.DrawTexture(fillRect, BaseContent.WhiteTex);
            GUI.color = Color.white;
            GameFont previousFont = Text.Font;
            TextAnchor previousAnchor = Text.Anchor;
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Tiny;
            Widgets.Label(butRect, remaining.ToStringTicksToPeriod());
            Text.Font = previousFont;
            Text.Anchor = previousAnchor;
            GUI.color = previousColor;
        }
    }

    // 除错
    [HarmonyPatch(typeof(Pawn), "GetGizmos")]
    public static class Patch_Pawn_GetGizmos_IcecreamTailTemporaryAbilities
    {
        public static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }

            if (!__instance.Drafted || !IcecreamTailTemporaryAbilityUtility.IsFourBeerEligible(__instance) || __instance.abilities == null)
            {
                yield break;
            }

            // 开发者
            IcecreamTailSettings settings = IcecreamTailMod.Settings;
            if (Prefs.DevMode && settings != null && settings.ShowTemporaryAbilityCooldownDebug)
            {
                yield return new Command_Action
                {
                    defaultLabel = "清空临时技能冷却",
                    defaultDesc = "仅清空当前小人的冰淇淋尾巴临时技能冷却。",
                    icon = TexCommand.ClearPrioritizedWork,
                    action = delegate
                    {
                        int count = IcecreamTailTemporaryAbilityUtility.ResetTemporaryAbilityCooldowns(__instance);
                        Messages.Message("已清空 " + count + " 个临时技能冷却。", __instance, MessageTypeDefOf.NeutralEvent, false);
                    }
                };
            }

            AbilityDef burstDef = DefDatabase<AbilityDef>.GetNamedSilentFail(IcecreamTailTemporaryAbilityUtility.BurstAbilityDefName);
            Ability burst = burstDef == null ? null : __instance.abilities.GetAbility(burstDef, false);
            if (burst != null)
            {
                yield return new Command_IcecreamTailAbility(burst, __instance);
            }

            AbilityDef burst2Def = DefDatabase<AbilityDef>.GetNamedSilentFail(IcecreamTailTemporaryAbilityUtility.Burst2AbilityDefName);
            Ability burst2 = burst2Def == null ? null : __instance.abilities.GetAbility(burst2Def, false);
            if (burst2 != null)
            {
                yield return new Command_IcecreamTailAbility(burst2, __instance);
            }

            AbilityDef skill3Def = DefDatabase<AbilityDef>.GetNamedSilentFail(IcecreamTailTemporaryAbilityUtility.Skill3AbilityDefName);
            Ability skill3 = skill3Def == null ? null : __instance.abilities.GetAbility(skill3Def, false);
            if (skill3 != null)
            {
                yield return new Command_IcecreamTailAbility(skill3, __instance);
            }

            AbilityDef skill4Def = DefDatabase<AbilityDef>.GetNamedSilentFail(IcecreamTailTemporaryAbilityUtility.Skill4AbilityDefName);
            Ability skill4 = skill4Def == null ? null : __instance.abilities.GetAbility(skill4Def, false);
            if (skill4 != null)
            {
                yield return new Command_IcecreamTailAbility(skill4, __instance);
            }

            AbilityDef skill5Def = DefDatabase<AbilityDef>.GetNamedSilentFail(IcecreamTailTemporaryAbilityUtility.Skill5AbilityDefName);
            Ability skill5 = skill5Def == null ? null : __instance.abilities.GetAbility(skill5Def, false);
            if (skill5 != null)
            {
                yield return new Command_IcecreamTailAbility(skill5, __instance);
            }
        }
    }

}
