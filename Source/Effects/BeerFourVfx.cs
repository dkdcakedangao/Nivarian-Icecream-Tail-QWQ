using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

// 特效
namespace NivarianIcecreamTail
{
    public static class BeerFourVfxUtility
    {
        private const string WinMoteDefName = "Mote_IcecreamTailBeerWin";
        private const string WinSoundDefName = "IcecreamTailBeerWin";
        private const string Star04MoteDefName = "Mote_IcecreamTailBeerStar04";
        private const string Star08MoteDefName = "Mote_IcecreamTailBeerStar08";

        // 四酒
        public static void TryTriggerFourBeerTransition(Pawn pawn)
        {
            if (pawn == null || !IcecreamTailUtility.IsNivarian(pawn) || !IcecreamTailMod.Enabled || !pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            IcecreamTailSettings settings = IcecreamTailMod.Settings;
            if (settings == null || !settings.EnableBeerWinEasterEgg || !Rand.Chance(Mathf.Clamp01(settings.BeerWinEasterEggChance)))
            {
                return;
            }

            // 彩蛋WIN
            Texture2D winTexture = ContentFinder<Texture2D>.Get("UI/WIN!!!!/win", false);
            if (winTexture == null)
            {
                Log.Error("Nivarian Icecream Tail: missing WIN texture at UI/WIN!!!!/win.");
            }
            else
            {
                ThingDef moteDef = DefDatabase<ThingDef>.GetNamedSilentFail(WinMoteDefName);
                if (moteDef == null)
                {
                    Log.Error("Nivarian Icecream Tail: missing WIN mote definition.");
                }
                else
                {
                    MoteMaker.MakeAttachedOverlay(pawn, moteDef, new Vector3(0f, 0f, 0.9f), 1f);
                }
            }

            SoundDef soundDef = DefDatabase<SoundDef>.GetNamedSilentFail(WinSoundDefName);
            if (soundDef == null)
            {
                Log.Error("Nivarian Icecream Tail: missing WIN sound definition.");
            }
            else
            {
                soundDef.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
            }

            if (IcecreamTailDebug.EasterEggEnabled)
            {
                IcecreamTailDebug.EasterEgg("啤酒四酒 WIN 彩蛋触发：" + IcecreamTailDebug.PawnInfo(pawn));
            }
        }

        // 四酒特效
        public static void SpawnBeerStar(Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map == null || !IcecreamTailUtility.IsNivarian(pawn) || pawn.Map.moteCounter.SaturatedLowPriority)
            {
                return;
            }

            string defName = Rand.Bool ? Star04MoteDefName : Star08MoteDefName;
            ThingDef moteDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (moteDef == null)
            {
                return;
            }

            Vector3 location = pawn.DrawPos;
            location.x += Rand.Range(-0.38f, 0.38f);
            location.z -= 0.55f;
            Mote_IcecreamTailBeerStar mote = MoteMaker.MakeStaticMote(location, pawn.Map, moteDef, Rand.Range(0.22f, 0.42f), true, Rand.Range(0f, 360f)) as Mote_IcecreamTailBeerStar;
            if (mote != null)
            {
                mote.Initialize(pawn);
                mote.rotationRate = Rand.Range(-18f, 18f);
                mote.SetVelocity(Rand.Range(-8f, 8f), Rand.Range(0.12f, 0.20f));
            }
        }
    }

    public sealed class HediffCompProperties_IcecreamTailBeerFourVfx : HediffCompProperties
    {
        public HediffCompProperties_IcecreamTailBeerFourVfx()
        {
            compClass = typeof(HediffComp_IcecreamTailBeerFourVfx);
        }
    }

    // 除错，gpt写的
    public sealed class HediffComp_IcecreamTailBeerFourVfx : HediffComp
    {
        private int ticksUntilNextSpawn;

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref ticksUntilNextSpawn, "ticksUntilNextSpawn", 0);
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (parent == null || parent.pawn == null)
            {
                return;
            }

            ticksUntilNextSpawn -= delta;
            if (ticksUntilNextSpawn > 0)
            {
                return;
            }

            if (parent.Severity < 4f || !IcecreamTailMod.Enabled || !IcecreamTailUtility.IsNivarian(parent.pawn) || !parent.pawn.Spawned || parent.pawn.Map == null)
            {
                ticksUntilNextSpawn = 7;
                return;
            }

            int spawnCount = 0;
            while (ticksUntilNextSpawn <= 0 && spawnCount < 12)
            {
                BeerFourVfxUtility.SpawnBeerStar(parent.pawn);
                ticksUntilNextSpawn += 7;
                spawnCount++;
            }
        }
    }

    public sealed class Mote_IcecreamTailBeerStar : MoteThrown
    {
        private Pawn owner;
        private int ticksUntilFlashUpdate;

        public void Initialize(Pawn pawn)
        {
            owner = pawn;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref owner, "owner");
        }

        protected override void Tick()
        {
            base.Tick();
            if (Destroyed)
            {
                return;
            }

            if (!IcecreamTailTemporaryAbilityUtility.IsFourBeerEligible(owner) || !owner.Spawned || owner.Map != Map)
            {
                Destroy();
                return;
            }

            if (ticksUntilFlashUpdate > 0)
            {
                ticksUntilFlashUpdate--;
                return;
            }

            ticksUntilFlashUpdate = 1;
            int flashTick = (Find.TickManager.TicksGame + offsetRandom) % 14;
            float phase = flashTick / 14f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(phase * Mathf.PI * 2f);
            bool magicBody = IcecreamTailTemporaryAbilityUtility.HasMagicBody(owner);
            Color first = magicBody ? new Color(1f, 0.72f, 0.08f, 1f) : new Color(1f, 0.08f, 0.08f, 1f);
            Color second = magicBody ? new Color(1f, 0.06f, 0.04f, 1f) : new Color(1f, 0.82f, 0.82f, 1f);
            Color color = flashTick < 7 ? first : second;
            color.a = Mathf.Lerp(0.65f, 1f, pulse);
            instanceColor = color;
        }
    }
}
