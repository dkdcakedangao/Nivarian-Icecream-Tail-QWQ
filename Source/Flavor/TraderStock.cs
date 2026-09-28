using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

// 商队
namespace NivarianIcecreamTail
{
    public sealed class StockGenerator_IcecreamTailSeasoning : StockGenerator
    {
        private static readonly string[] SeasoningDefNames =
        {   
        // 除了彩虹糖的其他口味，都会刷新在涅的武装商队~
            "IcecreamTailMilkSeasoning",
            "IcecreamTailStrawberrySeasoning",
            "IcecreamTailVanillaSeasoning",
            "IcecreamTailMintSeasoning",
            "IcecreamTailMatchaSeasoning",
            "IcecreamTailNutmegSeasoning",
            "IcecreamTailChocolateSeasoning",
            "IcecreamTailBeerSeasoning",
            "IcecreamTailFrostberryStompedSeasoning",
            "IcecreamTailFrostberryImitationSeasoning",
            "IcecreamTailFrostberryNiraSeasoning"
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
