using RimWorld;
using System;
using System.Collections.Generic;
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
            mapBuilder.initRocks(map);
        }
    }

    public class GenStep_MineralsFramework_Ice : GenStep
    {
        public override int SeedPart => 1938421202; // Different unique seed

        public override void Generate(Map map, GenStepParams parms)
        {
            mapBuilder.initIce(map);
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
                mapBuilder.removeStartingChunks(map);
            }
        }

    }
}

