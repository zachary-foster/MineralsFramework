
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;      // RimWorld specific functions 
using RimWorld.Planet;
using Verse;         // RimWorld universal objects 

namespace MineralsFramework
{
    /// <summary>
    /// Mineral class
    /// </summary>
    /// <author>zachary-foster</author>
    /// <permission>No restrictions</permission>
    public class DynamicMineral : StaticMineral
    {
        // Controls how often occasional checks are done, like distance to nearby things
        private int tickCounter = Rand.Range(0, 1000);

        public new ThingDef_DynamicMineral Attributes
        {
            get
            {
                return base.Attributes as ThingDef_DynamicMineral;
            }
        }


        public override float NearbyThingEffectAbundFactor
        {
            get
            {
                int ticksPerUpdate = 20;
                if (myNearbyThingEffectAbundFactor == null || tickCounter % ticksPerUpdate == 0) // not yet set
                {
                    myNearbyThingEffectAbundFactor = Attributes.NearbyThingFactor(Map, Position, Attributes.nearbyThingAbundEffects);
                }

                return (float)myNearbyThingEffectAbundFactor;
            }

            set
            {
                myNearbyThingEffectAbundFactor = value;
            }
        }


        public override float NearbyThingEffectSizeFactor
        {
            get
            {
                int ticksPerUpdate = 20;
                if (myNearbyThingEffectSizeFactor == null || tickCounter % ticksPerUpdate == 0) // not yet set
                {
                    myNearbyThingEffectSizeFactor = Attributes.NearbyThingFactor(Map, Position, Attributes.nearbyThingSizeEffects);
                }

                return (float)myNearbyThingEffectSizeFactor;
            }

            set
            {
                myNearbyThingEffectSizeFactor = value;
            }
        }



        public virtual float GrowthRate
        {
            get
            {
                float output = 1f; // If there are no growth rate factors, grow at full speed

                // Get growth rate factors
                List<float> rateFactors = AllGrowthRateFactors;
                List<float> positiveFactors = rateFactors.FindAll(fac => fac >= 0);
                List<float> negativeFactors = rateFactors.FindAll(fac => fac < 0);

                // if any factors are negative, add them together and ignore positive factors
                if (negativeFactors.Count > 0)
                {
                    output = negativeFactors.Sum();
                }
                else if (positiveFactors.Count > 0) // if all positive, multiply them
                {
                    output = positiveFactors.Aggregate(1f, (acc, val) => acc * val);
                }


                return output * MineralsFrameworkMain.Settings.mineralGrowthSetting;
            }
        }



        public float GrowthPerTick
        {
            get
            {
                float growthPerTick = (1f / (GenDate.TicksPerDay * Attributes.growDays));
                return growthPerTick * GrowthRate;
            }
        }


        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(base.GetInspectString());
            stringBuilder.AppendLine("Growth rate: " + GrowthRate.ToStringPercent());
            if (DebugSettings.godMode)
            {
                foreach (GrowthRateModifier mod in Attributes.AllRateModifiers)
                {
                    stringBuilder.AppendLine(mod.GetType().Name + ": " + mod.GrowthRateFactorAtPos(this));
                }
            }
            return stringBuilder.ToString().TrimEndNewlines();
        }


        public override void TickLong()
        {
            // Half the time, dont do anything
            if (Rand.Bool)
            {
                return;
            }

            // Try to grow
            float GrowthThisTick = GrowthPerTick;
            Size += GrowthThisTick * 4000; // 1 long tick = 2000

            // Try to reproduce
            if (GrowthThisTick > 0 && Size > Attributes.minReproductionSize && Rand.Range(0f, 1f) < Attributes.reproduceProp * GrowthRate * MineralsFrameworkMain.Settings.mineralReproductionSetting)
            {
                Attributes.TryReproduce(Map, Position);
            }

            // Refresh appearance if apparent size has changed
            float apparentSize = PrintSize();
            float sizeDiff = Math.Abs(sizeWhenLastPrinted - apparentSize);
            if (sizeDiff > 0.1f || (sizeDiff > 0.02f && Attributes.fastGraphicRefresh))
            {
                sizeWhenLastPrinted = apparentSize;
                base.Map.mapDrawer.MapMeshDirty(base.Position, MapMeshFlagDefOf.Things);
                InitializeTextureLocations();
            }

            // Count ticks for occasional updates, like dist to nearby terrain 
            tickCounter += 1;

            // Try to die
            if (Size <= 0 && Rand.Range(0f, 1f) < Attributes.deathProb)
            {
                Destroy(DestroyMode.Vanish);
            }

        }
            
        public List<float> AllGrowthRateFactors 
        {
            get
            {
                return Attributes.AllRateModifiers.Select(mod => mod.GrowthRateFactorAtPos(this)).ToList();
            }
        }


    }





    /// <summary>
    /// ThingDef_StaticMineral class.
    /// </summary>
    /// <author>zachary-foster</author>
    /// <permission>No restrictions</permission>
    public class ThingDef_DynamicMineral : ThingDef_StaticMineral
    {
        // The number of days it takes to grow at max growth speed
        public float growDays = 100f;

        // The smallest a mineral can be before reproducing
        public float minReproductionSize = 0.8f;

        // The probability of reproducing each tick
        public float reproduceProp = 0.001f;

        // The probability of being deleted each tick if size is 0
        public float deathProb = 0.001f;

        // chance of spawning de novo each tick
        public float spawnProb = 0.0001f;

        // Temperature effects on growth rate
        public TempGrowthRateModifier tempGrowthRateModifier;

        // Rain effects on growth rate
        public RainGrowthRateModifier rainGrowthRateModifier;

        // Light effects on growth rate
        public LightGrowthRateModifier lightGrowthRateModifier;

        // Fertility effects on growth rate
        public FertGrowthRateModifier fertGrowthRateModifier;

        // Distance to needed terrain effects on growth rate
        public NearbyThingGrowthRateModifier nearbyThingGrowthRateModifier;

        // Current size effects on growth rate
        public SizeGrowthRateModifier sizeGrowthRateModifier;

        // If true, the graphics are regenerated more often
        public bool fastGraphicRefresh = false;

        // The minimum number of crystals in clusters that are spawned during gameplay, not map creation
        public int minSpawnClusterSize = 1;

        // The maximum number of crystals in clusters that are spawned during gameplay, not map creation
        public int maxSpawnClusterSize = 1;


        public List<GrowthRateModifier> AllRateModifiers 
        {
            get 
            {
                List<GrowthRateModifier> output = new List<GrowthRateModifier>{
                    tempGrowthRateModifier,
                    rainGrowthRateModifier,
                    lightGrowthRateModifier,
                    fertGrowthRateModifier,
                    nearbyThingGrowthRateModifier,
                    sizeGrowthRateModifier
                };
                output.RemoveAll(item => item == null);
                return output;
            }
        }

        public List<GrowthRateModifier> MapRateModifiers
        {
            get
            {
                List<GrowthRateModifier> output = new List<GrowthRateModifier>{
                    tempGrowthRateModifier,
                    rainGrowthRateModifier,
                    lightGrowthRateModifier,
                    fertGrowthRateModifier,
                    nearbyThingGrowthRateModifier,
                    sizeGrowthRateModifier
                };
                output.RemoveAll(item => item == null || (!item.wholeMapEffect));
                return output;
            }
        }

        public List<GrowthRateModifier> PosRateModifiers
        {
            get
            {
                List<GrowthRateModifier> output = new List<GrowthRateModifier>{
                    tempGrowthRateModifier,
                    rainGrowthRateModifier,
                    lightGrowthRateModifier,
                    fertGrowthRateModifier,
                    nearbyThingGrowthRateModifier,
                    sizeGrowthRateModifier
                };
                output.RemoveAll(item => item == null || item.wholeMapEffect);
                return output;
            }
        }


        public override void InitNewMap(Map map, float scaling = 1)
        {
            scaling *= GrowthRateMapRecent(map);
            base.InitNewMap(map, scaling);
        }


        // ======= Growth rate factors ======= //
        public virtual float CombineGrowthRateFactors(List<float> rateFactors)
        {
            List<float> positiveFactors = rateFactors.FindAll(fac => fac >= 0);
            List<float> negativeFactors = rateFactors.FindAll(fac => fac < 0);

            // if any factors are negative, add them together and ignore positive factors
            if (negativeFactors.Count > 0)
            {
                return negativeFactors.Sum();
            }

            // if all positive, multiply them
            if (positiveFactors.Count > 0)
            {
                return positiveFactors.Aggregate(1f, (acc, val) => acc * val);
            }

            // If there are no growth rate factors, grow at full speed
            return 1f;
        }

        public virtual List<float> AllGrowthRateFactorsAtPos(IntVec3 aPosition, Map aMap, bool includePerMapEffects = true)
        {
            if (includePerMapEffects)
            {
                return AllRateModifiers.Select(mod => mod.GrowthRateFactorAtPos(this, aPosition, aMap)).ToList();
            }
            else
            {
                return PosRateModifiers.Select(mod => mod.GrowthRateFactorAtPos(this, aPosition, aMap)).ToList();
            }
        }

        public virtual List<float> AllGrowthRateFactorsAtMap(Map aMap)
        {
            return MapRateModifiers.Select(mod => mod.GrowthRateFactorAtMap(aMap)).ToList();
        }

        public virtual List<float> AllGrowthRateFactorsAtMapMean(Map aMap)
        {
            return AllRateModifiers.Select(mod => mod.GrowthRateFactorMapMean(aMap)).ToList();
        }
        public virtual List<float> AllGrowthRateFactorsMapRecent(Map aMap)
        {
            //foreach (growthRateModifier mod in allRateModifiers)
            //{
            //    Log.Message("GrowthRateMapRecent: " + mod.GetType().Name + ": " + mod.growthRateFactorMapRecent(this, aMap));
            //}
            return AllRateModifiers.Select(mod => mod.GrowthRateFactorMapRecent(this, aMap)).ToList();
        }

        //Growth rate for a given position at the current time
        public virtual float GrowthRateAtPos(Map aMap, IntVec3 aPosition, bool includePerMapEffects = true)
        {
            return CombineGrowthRateFactors(AllGrowthRateFactorsAtPos(aPosition, aMap, includePerMapEffects));
        }

        //Growth rate for the map at the current  time
        public virtual float GrowthRateAtMap(Map aMap)
        {
            return CombineGrowthRateFactors(AllGrowthRateFactorsAtMap(aMap));
        }

        //Growth rate for the map on average
        public virtual float GrowthRateMapMean(Map aMap)
        {
            return CombineGrowthRateFactors(AllGrowthRateFactorsAtMapMean(aMap));
        }

        public virtual float GrowthRateMapRecent(Map aMap)
        {
            return CombineGrowthRateFactors(AllGrowthRateFactorsMapRecent(aMap));
        }

    }


    public abstract class GrowthRateModifier
    {
        public float aboveMaxDecayRate;  // How quickly it decays when above maxStableFert
        public float maxStable; // Will decay above this level
        public float maxGrow; // Will not grow above this level
        public float maxIdeal; // Grows fastest at this level
        public float minIdeal; // Grows fastest at this level
        public float minGrow; // Will not grow below this level
        public float minStable; // Will decay below this fertility level
        public float belowMinDecayRate;  // How quickly it decays when below minStableFert
        public bool wholeMapEffect = false; // If a whole-map attribute can be used instead of a per-position attribute (faster)

        public abstract float ValueAtPos(DynamicMineral aMineral);
        public abstract float ValueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap);
        public abstract float ValueAtMap(Map aMap);
        public abstract float ValueAtMapSeasonal(Map aMap);
        public abstract float ValueAtMapMean(Map aMap);
        public abstract float ValueAtTile(World world, int worldTile);

        public virtual float GrowthRateFactor(float myValue)
        {
            // decays if too high or low
            float stableRangeSize = maxStable - minStable;
            if (myValue > maxStable)
            {
                return -aboveMaxDecayRate * (myValue - maxStable) / stableRangeSize;
            }
            if (myValue < minStable)
            {
                return -belowMinDecayRate * (minStable - myValue) / stableRangeSize;
            }

            // does not grow if too high or low
            if (myValue < minGrow || myValue > maxGrow)
            {
                return 0f;
            }

            // slowed growth if too high or low
            if (myValue < minIdeal)
            {
                return 1f - ((minIdeal - myValue) / (minIdeal - minGrow));
            }
            if (myValue > maxIdeal)
            {
                return 1f - ((myValue - maxIdeal) / (maxGrow - maxIdeal));
            }

            return 1f;
        }

        public virtual float GrowthRateFactorAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return GrowthRateFactor(ValueAtPos(myDef, aPosition, aMap));
        }

        public virtual float GrowthRateFactorAtPos(DynamicMineral aMineral)
        {
            return GrowthRateFactor(ValueAtPos(aMineral));
        }

        public virtual float GrowthRateFactorAtMap(Map aMap)
        {
            return GrowthRateFactor(ValueAtMap(aMap));
        }

        public virtual float GrowthRateFactorMapMean(Map aMap)
        {
            return GrowthRateFactor(ValueAtMapMean(aMap));
        }
        public virtual float GrowthRateFactorMapSeason(Map aMap)
        {
            return GrowthRateFactor(ValueAtMapSeasonal(aMap));
        }
        public virtual float GrowthRateFactorMapRecent(ThingDef_DynamicMineral myDef, Map aMap)
        {
            float mapMean = GrowthRateFactorMapMean(aMap);
            float mapSeason = GrowthRateFactorMapSeason(aMap);
            float meanWeight = (myDef.growDays * 2f) / 60f;
            if (meanWeight > 1f)
            {
                meanWeight = 1f;
            }
            if (meanWeight < 0f)
            {
                meanWeight = 0f;
            }
            return mapMean * meanWeight + mapSeason * (1 - meanWeight);
        }

    }

    public class TempGrowthRateModifier : GrowthRateModifier
    {
        public override float ValueAtPos(DynamicMineral aMineral)
        {
            return aMineral.Position.GetTemperature(aMineral.Map);
        }
        public override float ValueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return aPosition.GetTemperature(aMap);
        }
        public override float ValueAtMap(Map aMap)
        {
            return aMap.mapTemperature.OutdoorTemp;
        }
        public override float ValueAtTile(World world, int worldTile)
        {
            return world.tileTemperatures.GetOutdoorTemp(worldTile);
        }
        public override float ValueAtMapMean(Map aMap)
        {
            return aMap.TileInfo.temperature;
        }
        public override float ValueAtMapSeasonal(Map aMap)
        {
            return aMap.mapTemperature.SeasonalTemp;
        }
        public override float GrowthRateFactorMapMean(Map aMap)
        {
            return (GrowthRateFactor(ValueAtMapMean(aMap) + 15f) + GrowthRateFactor(ValueAtMapMean(aMap)) + GrowthRateFactor(ValueAtMapMean(aMap) - 15f)) / 3f;
        }
    }

    public class RainGrowthRateModifier : GrowthRateModifier
    {
        private float RainfallToRain(float rainfall)
        {
            float rainProxy = rainfall / 1000f;
            if (rainProxy > 3f)
            {
                rainProxy = 3f;
            }
            return rainProxy;
        }

        public override float ValueAtPos(DynamicMineral aMineral)
        {
            return aMineral.Map.weatherManager.curWeather.rainRate;
        }
        public override float ValueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return aMap.weatherManager.curWeather.rainRate;
        }
        public override float ValueAtMap(Map aMap)
        {
            return aMap.weatherManager.curWeather.rainRate;
        }
        public override float ValueAtTile(World world, int worldTile)
        {
            return RainfallToRain(world.grid.Tiles.ToList()[worldTile].rainfall);
        }
        public override float ValueAtMapMean(Map aMap)
        {
            return RainfallToRain(aMap.TileInfo.rainfall);
        }
        public override float ValueAtMapSeasonal(Map aMap)
        {
            //Log.Message("valueAtMapSeasonal: valueAtMapMean(aMap): " + valueAtMapMean(aMap));
            //Log.Message("valueAtMapSeasonal: growthRateFactor(valueAtMapMean(aMap) * 0.5f): " + growthRateFactor(valueAtMapMean(aMap) * 0.5f));
            //Log.Message("valueAtMapSeasonal: growthRateFactor(valueAtMapMean(aMap) * 1.5f): " + growthRateFactor(valueAtMapMean(aMap) * 1.5f));
            return (GrowthRateFactor(ValueAtMapMean(aMap) * 0.5f) + GrowthRateFactor(ValueAtMapMean(aMap) * 1.5f)) / 2f;
        }
        public override float GrowthRateFactorMapMean(Map aMap)
        {
            return (GrowthRateFactor(ValueAtMapMean(aMap) * 0.5f) + GrowthRateFactor(ValueAtMapMean(aMap) * 1.5f) + GrowthRateFactor(ValueAtMapMean(aMap))) / 3f;
        }

    }

    public class LightGrowthRateModifier : GrowthRateModifier
    {
        public float LightByBiome(List<BiomeDef> biomes)
        {
            if (biomes.Any(b => b.defName == "AB_RockyCrags"))
            {
                return 0f;
            }
            else
            {
                return 1f;
            }

        }
        public override float ValueAtPos(DynamicMineral aMineral)
        {
            return aMineral.Map.glowGrid.GroundGlowAt(aMineral.Position, false);
        }
        public override float ValueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return aMap.glowGrid.GroundGlowAt(aPosition, false);
        }
        public override float ValueAtMap(Map aMap)
        {
            throw new InvalidOperationException("lightGrowthRateModifier cannot be used with 'wholeMapEffect'");
        }
        public override float ValueAtTile(World world, int worldTile)
        {
            return LightByBiome(world.grid.Tiles.ToList()[worldTile].Biomes.ToList());
        }
        public override float ValueAtMapMean(Map aMap)
        {
            return LightByBiome(aMap.Biomes.ToList());
        }
        public override float ValueAtMapSeasonal(Map aMap)
        {
            return LightByBiome(aMap.Biomes.ToList());
        }
    }


    public class FertGrowthRateModifier : GrowthRateModifier
    {
        public override float ValueAtPos(DynamicMineral aMineral)
        {
            return aMineral.Map.fertilityGrid.FertilityAt(aMineral.Position);
        }
        public override float ValueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return aMap.fertilityGrid.FertilityAt(aPosition);
        }
        public override float ValueAtMap(Map aMap)
        {
            throw new InvalidOperationException("fertGrowthRateModifier cannot be used with 'wholeMapEffect'");
        }
        public override float ValueAtTile(World world, int worldTile)
        {
            return 1f;
        }
        public override float ValueAtMapMean(Map aMap)
        {
            return 1f;
        }
        public override float ValueAtMapSeasonal(Map aMap)
        {
            return 1f;
        }
        public override float GrowthRateFactorMapMean(Map aMap)
        {
            return 1f;
        }
        public override float GrowthRateFactorMapSeason(Map aMap)
        {
            return 1f;
        }
    }

    public class NearbyThingGrowthRateModifier : GrowthRateModifier
    {
        public override float ValueAtPos(DynamicMineral aMineral)
        {
            return (aMineral.NearbyThingEffectAbundFactor + aMineral.NearbyThingEffectSizeFactor) / 2f;
        }
        public override float ValueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return (myDef.NearbyThingFactor(aMap, aPosition, myDef.nearbyThingAbundEffects) + myDef.NearbyThingFactor(aMap, aPosition, myDef.nearbyThingSizeEffects)) /2;
        }
        public override float ValueAtMap(Map aMap)
        {
            throw new InvalidOperationException("nearbyThingGrowthRateModifier cannot be used with 'wholeMapEffect'");
        }
        public override float ValueAtTile(World world, int worldTile)
        {
            return 1f;
        }
        public override float ValueAtMapMean(Map aMap)
        {
            return 1f;
        }
        public override float ValueAtMapSeasonal(Map aMap)
        {
            return 1f;
        }
        public override float GrowthRateFactorMapMean(Map aMap)
        {
            return 1f;
        }
        public override float GrowthRateFactorMapSeason(Map aMap)
        {
            return 1f;
        }
    }


    public class SizeGrowthRateModifier : GrowthRateModifier
    {
        public override float ValueAtPos(DynamicMineral aMineral)
        {
            return aMineral.Size;
        }

        public override float ValueAtPos(ThingDef_DynamicMineral myDef, IntVec3 aPosition, Map aMap)
        {
            return 0.01f;
        }

        public override float ValueAtMap(Map aMap)
        {
            throw new InvalidOperationException("sizeGrowthRateModifier cannot be used with 'wholeMapEffect'");
        }
        public override float ValueAtTile(World world, int worldTile)
        {
            return 0.5f;
        }
        public override float ValueAtMapMean(Map aMap)
        {
            return 0.5f;
        }
        public override float ValueAtMapSeasonal(Map aMap)
        {
            return 1f;
        }
        public override float GrowthRateFactorMapMean(Map aMap)
        {
            return 1f;
        }
        public override float GrowthRateFactorMapSeason(Map aMap)
        {
            return 1f;
        }
    }



    public class DynamicMineralWatcher : MapComponent
    {

        public static int ticksPerLook = 1000; // 100 is about once a second on 1x speed
        public int tick_counter = 1;

        public DynamicMineralWatcher(Map map) : base(map)
        {
        }

        public override void MapComponentTick()
        {
            // Run each class' watcher
            tick_counter += 1;
            if (tick_counter > ticksPerLook)
            {
                tick_counter = 1;
                Look();
            }
        }

        // The main function controlling what is done each time the map is looked at
        public void Look()
        {
            //var watch = System.Diagnostics.Stopwatch.StartNew();
            SpawnDynamicMinerals();
            //watch.Stop();
            //Log.Message("========== SpawnDynamicMinerals() took: " + watch.ElapsedMilliseconds);
        }


        public void SpawnDynamicMinerals() 
        {
            foreach (ThingDef_DynamicMineral mineralType in DefDatabase<ThingDef_DynamicMineral>.AllDefs)
            {
                //var watch = System.Diagnostics.Stopwatch.StartNew();
                //Log.Message("Trying to spawn " + mineralType.defName);


                // Check that the map type is ok
                if (! mineralType.CanSpawnInBiome(map))
                {
                    continue;
                }
                //Log.Message("   Biome OK");

                // Get number of positions to check
                float perMapGrowthFactor = mineralType.GrowthRateAtMap(map);
                //Log.Message("   perMapGrowthFactor: " + perMapGrowthFactor);
                //Log.Message("   spawnProb: " + mineralType.spawnProb);
                float numToCheck = map.Area * mineralType.spawnProb * perMapGrowthFactor * MineralsFrameworkMain.Settings.mineralSpawningSetting;
                if (numToCheck <= 0)
                {
                    continue;
                }

                // If less than one cell should be checked, randomly decide to check one or none
                if (numToCheck < 1)
                {
                    if (Rand.Range(0f, 1f) < numToCheck)
                    {
                        numToCheck = 1;
                    } else
                    {
                        continue;
                    }
                }

                // Never check more than 1/10 of the map (performance failsafe)
                if (numToCheck > map.Area / 10)
                {
                    numToCheck = map.Area / 10;
                }

                // Round to integer
                numToCheck = (float) Math.Round(numToCheck);

                //Log.Message("   numToCheck: " + numToCheck);

                // Try to spawn in a subset of positions
                for (int i = 0; i < numToCheck; i++)
                {
                    // Pick a random location
                    IntVec3 aPos = CellIndicesUtility.IndexToCell(Rand.RangeInclusive(0, map.Area - 1), map.Size.x);

                    // Dont always spawn if growth rate is not good
                    if (Rand.Range(0f, 1f) > mineralType.GrowthRateAtPos(map, aPos, false))
                    {
                        continue;
                    }

                    // Try to spawn at that location
                    //Log.Message("Trying to spawn " + mineralType.defName);
                    //mineralType.TrySpawnAt(aPos, map, 0.01f);
                    mineralType.TrySpawnCluster(map, aPos, Rand.Range(0.01f, 0.05f), Rand.Range(mineralType.minSpawnClusterSize, mineralType.maxSpawnClusterSize));

                }

                //watch.Stop();
                //Log.Message("Spawning " + mineralType.defName + " took: " + watch.ElapsedMilliseconds);

            }
        }
    }
}



