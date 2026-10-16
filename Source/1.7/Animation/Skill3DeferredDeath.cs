using Verse;

// 伤害相关的，基本上就是动画播放期间免死，需要记录伤害来源，防止伤害来源丢失什么的。属于是除错了
namespace NivarianIcecreamTail
{
    internal sealed class Skill3DeferredDeath : IExposable
    {
        internal DamageInfo Damage;
        internal Hediff Culprit;
        private DamageDef damageDef;
        private Thing instigator, intendedTarget;
        private ThingDef weapon;
        private BodyPartRecord hitPart;
        private float amount, armorPenetration, angle;
        private DamageInfo.SourceCategory category;
        private bool instigatorGuilty, spawnFilth, checkForJobOverride, preventCascade;
        private RimWorld.QualityCategory weaponQuality;

        public Skill3DeferredDeath() { }
        internal Skill3DeferredDeath(DamageInfo damage, Hediff culprit) { Damage = damage; Culprit = culprit; }

        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                damageDef = Damage.Def; instigator = Damage.Instigator; intendedTarget = Damage.IntendedTarget;
                weapon = Damage.Weapon; hitPart = Damage.HitPart;
                amount = Damage.Amount; armorPenetration = Damage.ArmorPenetrationInt; angle = Damage.Angle;
                category = Damage.Category; instigatorGuilty = Damage.InstigatorGuilty; spawnFilth = Damage.SpawnFilth;
                checkForJobOverride = Damage.CheckForJobOverride; preventCascade = Damage.PreventCascade; weaponQuality = Damage.WeaponQuality;
            }
            Scribe_Defs.Look(ref damageDef, "damageDef");
            Scribe_References.Look(ref instigator, "instigator");
            Scribe_References.Look(ref intendedTarget, "intendedTarget");
            Scribe_Defs.Look(ref weapon, "weapon");
            Scribe_BodyParts.Look(ref hitPart, "hitPart");
            Scribe_References.Look(ref Culprit, "culprit");
            Scribe_Values.Look(ref amount, "amount");
            Scribe_Values.Look(ref armorPenetration, "armorPenetration");
            Scribe_Values.Look(ref angle, "angle");
            Scribe_Values.Look(ref category, "category");
            Scribe_Values.Look(ref instigatorGuilty, "instigatorGuilty");
            Scribe_Values.Look(ref spawnFilth, "spawnFilth");
            Scribe_Values.Look(ref checkForJobOverride, "checkForJobOverride");
            Scribe_Values.Look(ref preventCascade, "preventCascade");
            Scribe_Values.Look(ref weaponQuality, "weaponQuality");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
                Damage = new DamageInfo(damageDef, amount, armorPenetration, angle, instigator, hitPart, weapon,
                    category, intendedTarget, instigatorGuilty, spawnFilth, weaponQuality, checkForJobOverride, preventCascade);
        }
    }
}
