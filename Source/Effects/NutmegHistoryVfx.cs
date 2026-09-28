using System.Collections.Generic;
using UnityEngine;
using Verse;

// 石山最密集的地方！
// 岁月石书的相关代码~
// 文字浮现什么的，冷却，触发资格什么的，都在这里
namespace NivarianIcecreamTail
{
    public static class NutmegHistoryUtility
    {
        public static bool Enabled
        {
            get { return IcecreamTailMod.Enabled && IcecreamTailMod.Settings != null && IcecreamTailMod.Settings.EnableNutmegHistoryEasterEgg; }
        }

        public static void TryShow(Pawn eater, IcecreamTailFlavorDef flavor)
        {
            if (!Enabled || flavor == null || flavor.defName != "IcecreamTailFlavorNutmeg"
                || !IcecreamTailUtility.IsNivarian(eater) || !eater.Spawned || eater.Dead)
            {
                return;
            }

            List<string> text = Current.Game.GetComponent<NutmegHistoryGameComponent>().Draw(eater.thingIDNumber);

            ThingDef moteDef = DefDatabase<ThingDef>.GetNamed("Mote_IcecreamTailNutmegHistory");
            // Only inspect this effect's instances at ingestion; no global pawn scan or static pawn cache.
            List<Thing> motes = eater.Map.listerThings.ThingsOfDef(moteDef);
            for (int i = 0; i < motes.Count; i++)
            {
                Mote_IcecreamTailNutmegHistory existing = motes[i] as Mote_IcecreamTailNutmegHistory;
                if (existing != null && existing.Eater == eater)
                {
                    existing.Initialize(eater, text);
                    return;
                }
            }

            Mote_IcecreamTailNutmegHistory mote = (Mote_IcecreamTailNutmegHistory)ThingMaker.MakeThing(moteDef);
            mote.Initialize(eater, text);
            GenSpawn.Spawn(mote, eater.Position, eater.Map);
        }
    }

    public sealed class Mote_IcecreamTailNutmegHistory : Mote
    {
        private const float LineSeconds = 2f;
        private const float FadeSeconds = 0.5f;
        private Pawn eater;
        private List<string> lines;
        private float elapsedSeconds;

        public Pawn Eater { get { return eater; } }

        public void Initialize(Pawn pawn, List<string> text)
        {
            eater = pawn;
            lines = text;
            elapsedSeconds = 0f;
        }

        protected override void TimeInterval(float deltaTime)
        {
            if (!NutmegHistoryUtility.Enabled || eater == null || eater.Dead || !eater.Spawned || eater.Map != Map || lines == null)
            {
                Destroy();
                return;
            }

            // GUI overlays are culled using Thing.Position, not the pawn's DrawPos.
            if (Position != eater.Position)
            {
                Position = eater.Position;
            }
            // Base AgeSecs includes pauses for real-time motes; keep our own unpaused clock.
            if (!Find.TickManager.Paused)
            {
                elapsedSeconds += Time.unscaledDeltaTime;
            }
            if (elapsedSeconds >= lines.Count * LineSeconds)
            {
                Destroy();
            }
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            // Text only; no world-space graphic.
        }

        public override void DrawGUIOverlay()
        {
            if (!NutmegHistoryUtility.Enabled || Destroyed || eater == null || eater.Dead || !eater.Spawned || eater.Map != Map
                || Map != Find.CurrentMap || Find.UIRoot.HideMotes || lines == null)
            {
                return;
            }
            int index = (int)(elapsedSeconds / LineSeconds);
            if (index >= lines.Count)
            {
                return;
            }
            float lineAge = elapsedSeconds - index * LineSeconds;
            float alpha = Mathf.Clamp01(Mathf.Min(lineAge, LineSeconds - lineAge) / FadeSeconds);
            Vector3 head = eater.DrawPos + new Vector3(0f, 0f, 0.9f);
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Color oldColor = GUI.color;
            GenMapUI.DrawText(new Vector2(head.x, head.z), lines[index], new Color(1f, 0.92f, 0.8f, alpha));
            Text.Font = oldFont;
            Text.Anchor = oldAnchor;
            GUI.color = oldColor;
        }
    }
}
