using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace MineralsFramework
{

    public class GenStep_MineralsFramework_Rocks : GenStep
    {
        public override int SeedPart => 1938421201; // Unique seed for this step

        public override void Generate(Map map, GenStepParams parms)
        {
            initRocks(map);
        }

        public static void initRocks(Map map)
        {
            // Spawn all minerals in specified order
            foreach (ThingDef_StaticMineral mineralType in DefDatabase<ThingDef_StaticMineral>.AllDefs.OrderBy(m => m.newMapSpawnOrder))
            {
                mineralType.InitNewMap(map);
            }
        }

    }

    public class GenStep_MineralsFramework_Ice : GenStep
    {
        public override int SeedPart => 1938421202; // Different unique seed

        public override void Generate(Map map, GenStepParams parms)
        {
            initIce(map);
        }

        public static void initIce(Map map)
        {
            foreach (ThingDef_StaticMineral mineralType in DefDatabase<ThingDef_StaticMineral>.AllDefs)
            {
                if (mineralType.newMapGenStep == "plants")
                {
                    mineralType.InitNewMap(map);
                }
            }
        }
    }

    public class GenStep_MineralsFramework_RemoveChunks : GenStep
    {
        public override int SeedPart => 1938421203; // Different unique seed

        public override void Generate(Map map, GenStepParams parms)
        {
            // Remove starting chunks
            if (MineralsFrameworkMain.Settings.removeStartingChunksSetting)
            {
                removeStartingChunks(map);
            }
        }

        public static void removeStartingChunks(Map map)
        {
            string[] toRemove = { "ChunkSandstone", "ChunkGranite", "ChunkLimestone", "ChunkSlate", "ChunkMarble", "MR_ChunkBasalt", "MAU_ChunkClaystone", "Filth_RubbleRock", "AB_ChunkCragstone", "AB_ChunkMudstone", "AB_ChunkObsidian", "GU_ChunkRoseQuartz", "AB_ChunkSlimeStone" };
            List<Thing> thingsToCheck = map.listerThings.AllThings;
            for (int i = thingsToCheck.Count - 1; i >= 0; i--)
            {
                if (toRemove.Any(thingsToCheck[i].def.defName.Equals))
                {
                    thingsToCheck[i].Destroy(DestroyMode.Vanish);
                }
            }
        }

    }

}
