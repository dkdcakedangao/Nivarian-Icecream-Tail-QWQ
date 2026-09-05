using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace NivarianIcecreamTail
{
    public sealed class StockGenerator_IcecreamTailSeasoning : StockGenerator
    {
        private static readonly string[] SeasoningDefNames =
        {
            "IcecreamTailMilkSeasoning",
            "IcecreamTailStrawberrySeasoning",
            "IcecreamTailVanillaSeasoning",
            "IcecreamTailChocolateSeasoning",
            "IcecreamTailBeerSeasoning"
        };

        public override bool HandlesThingDef(ThingDef thingDef)
        {
            return thingDef != null && SeasoningDefNames.Contains(thingDef.defName);
        }

        public override IEnumerable<Thing> GenerateThings(PlanetTile forTile, Faction faction = null)
        {
            string seasoningDefName = SeasoningDefNames.RandomElement();
            ThingDef seasoningDef = DefDatabase<ThingDef>.GetNamedSilentFail(seasoningDefName);
            if (seasoningDef != null)
            {
                yield return ThingMaker.MakeThing(seasoningDef);
            }
        }
    }
}
