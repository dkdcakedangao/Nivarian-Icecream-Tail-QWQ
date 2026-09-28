using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

// 石书的入口，def，正文的检查、除错。随机日期、石书抽取
namespace NivarianIcecreamTail
{
    public sealed class IcecreamTailHistoryDef : Def
    {
        public List<string> lines = new List<string>();

        public bool HasValidLines
        {
            get { return lines != null && lines.Count > 0 && lines.All(line => !string.IsNullOrWhiteSpace(line)); }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (!HasValidLines)
            {
                yield return "History needs at least one line and must not contain blank lines.";
            }
        }
    }

    // Only remember choices, not visual playback or Pawn references. No tick/update work.
    public sealed class NutmegHistoryGameComponent : GameComponent
    {
        private const int DateCount = 100 * 4 * 15;
        private Dictionary<int, int> lastDates = new Dictionary<int, int>();
        private Dictionary<int, string> lastHistories = new Dictionary<int, string>();

        public NutmegHistoryGameComponent(Game game) { }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref lastDates, "nutmegLastDates", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref lastHistories, "nutmegLastHistories", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (lastDates == null) lastDates = new Dictionary<int, int>();
                if (lastHistories == null) lastHistories = new Dictionary<int, string>();
            }
        }

        public List<string> Draw(int pawnId)
        {
            int previousDate;
            bool hasPreviousDate = lastDates.TryGetValue(pawnId, out previousDate);
            // Draw uniformly from all 6000 dates except the previous one; no retry loop.
            int date = Rand.Range(0, hasPreviousDate ? DateCount - 1 : DateCount);
            if (hasPreviousDate && date >= previousDate) date++;
            lastDates[pawnId] = date;

            List<string> text = new List<string>
            {
                "冰淇淋里藏着一张小便签：",
                "边缘世界" + (5500 + date / 60) + "年，"
                    + ((Quadrum)(date % 60 / 15)).Label() + "，" + (date % 15 + 1) + "日"
            };
            string previousHistory;
            lastHistories.TryGetValue(pawnId, out previousHistory);
            List<IcecreamTailHistoryDef> histories = DefDatabase<IcecreamTailHistoryDef>.AllDefsListForReading
                .Where(history => history.HasValidLines && history.defName != previousHistory).ToList();
            if (histories.Count > 0)
            {
                IcecreamTailHistoryDef history = histories.RandomElement();
                text.AddRange(history.lines);
                lastHistories[pawnId] = history.defName;
            }
            // With no alternative story, show only the note/date rather than repeat the story.
            return text;
        }
    }
}
