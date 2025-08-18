using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

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
}
