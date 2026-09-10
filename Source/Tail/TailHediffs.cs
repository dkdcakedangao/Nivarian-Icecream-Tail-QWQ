using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using Nivarian.Helper;
using RimWorld;
using UnityEngine;
using Verse;

namespace NivarianIcecreamTail
{
    public sealed class HediffCompProperties_IcecreamTailPlaceholder : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailPlaceholder()
        {
            compClass = typeof(HediffComp_IcecreamTailPlaceholder);
        }
    }

    public sealed class Hediff_IcecreamTailPlaceholder : HediffWithComps
    {
        public override string LabelBase
        {
            get { return IcecreamTailFlavorUtility.TailLabel(this, base.LabelBase); }
        }

        public override Color LabelColor
        {
            get { return IcecreamTailFlavorUtility.LabelColor(this); }
        }

        public override string TipStringExtra
        {
            get { return IcecreamTailFlavorUtility.ReadyTailDescription(this); }
        }
    }

    // buff引用
    // 待优化……
    public sealed class Hediff_IcecreamTailFlavorEaterBuff : HediffWithComps
    {
        private const string BeerBuffDefName = "IcecreamTailBeerEaterBuff";

        public override string LabelBase
        {
            get
            {
                if (def != null && def.defName == BeerBuffDefName)
                {
                    int level = Mathf.Clamp(Mathf.RoundToInt(Severity), 1, 4);
                    return "吃了啤酒味冰淇淋（" + new[] { "一酒", "二酒", "三酒", "四酒" }[level - 1] + "）";
                }

                return base.LabelBase;
            }
        }

        public override Color LabelColor
        {
            get
            {
                if (def != null && def.defName == BeerBuffDefName && Severity >= 4f)
                {
                    return new Color(1f, 0.18f, 0.18f);
                }

                return IcecreamTailFlavorUtility.BuffLabelColor(def);
            }
        }

        public override string TipStringExtra
        {
            get
            {
                TailEatingUtility.HediffComp_IcecreamTailBuffDuration duration = this.TryGetComp<TailEatingUtility.HediffComp_IcecreamTailBuffDuration>();
                string baseTip = base.TipStringExtra;
                if (duration == null)
                {
                    return baseTip;
                }

                // 似乎……没能正常实现呢……？
                // 现在实现了 嘛？
                // 把批注当log写不是好习惯
                // 但是我喜欢！
                string durationTip = "剩余时间：" + duration.RemainingTicks.ToStringTicksToPeriod();
                return baseTip.NullOrEmpty() ? durationTip : baseTip + "\n" + durationTip;
            }
        }
    }

    public sealed class Hediff_IcecreamTailSlow : HediffWithComps
    {
        public override Color LabelColor
        {
            get { return IcecreamTailColors.LabelBlue; }
        }
    }

    public sealed class HediffComp_IcecreamTailPlaceholder : HediffComp
    {
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            BodyPartRecord tailPart;
            if (!IcecreamTailMod.Enabled || IcecreamTailUtility.GetTailKind(parent.pawn, out tailPart) == TailKind.None || parent.Part != tailPart)
            {
                if (IcecreamTailDebug.TailEnabled)
                {
                    IcecreamTailDebug.Tail("成熟尾巴被清理：" + IcecreamTailDebug.PawnInfo(parent.pawn) + "，原因=" + (!IcecreamTailMod.Enabled ? "总开关关闭" : "尾巴不可用或部位已变更"));
                }

                parent.pawn.health.RemoveHediff(parent);
            }
        }
    }

}
