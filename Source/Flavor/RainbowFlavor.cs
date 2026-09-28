using System.Linq;
using Verse;

// 彩虹糖的口味解析，会建池，然后之后都是复用这个池~
// 但是有除错
namespace NivarianIcecreamTail
{
    public static partial class IcecreamTailFlavorUtility
    {
        internal const string RainbowFlavorDefName = "IcecreamTailFlavorRainbowCandy";

        // Build on the first rainbow meal, after Def references are resolved; reuse thereafter.
        private static IcecreamTailFlavorDef[] rainbowCandidates;

        internal static IcecreamTailFlavorDef ResolveEaterFlavor(IcecreamTailFlavorDef tailFlavor)
        {
            if (tailFlavor == null || tailFlavor.defName != RainbowFlavorDefName)
            {
                return tailFlavor;
            }
            if (rainbowCandidates == null)
            {
                rainbowCandidates = DefDatabase<IcecreamTailFlavorDef>.AllDefsListForReading
                    .Where(flavor => flavor.defName != RainbowFlavorDefName && flavor.eaterBuff != null).ToArray();
            }
            return rainbowCandidates[Rand.Range(0, rainbowCandidates.Length)];
        }
    }
}
