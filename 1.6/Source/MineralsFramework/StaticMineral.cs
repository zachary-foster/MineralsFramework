
using LudeonTK;
using RimWorld;      // RimWorld specific functions 
using RimWorld.Planet;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;   // Always needed
using Verse;         // RimWorld universal objects 

namespace MineralsFramework
{
    /// <summary>
    /// Mineral class
    /// </summary>
    /// <author>zachary-foster</author>
    /// <permission>No restrictions</permission>
    public class StaticMineral : Mineable
    {

        // ======= Private Variables ======= //
        protected float yieldPct = 0;
        protected float sizeWhenLastPrinted = 0f;
        protected int currentTextureIndex = 0;

        // The current size of the mineral
        protected float mySize = 1f;

        // Cache for mineral texture locations
        protected Vector3[] textureLocations;

        // Cache for mineral texture sizes
        protected float[] textureSizes;

        // Cache for mineral texture indexes
        protected int[] textureIndexes;

        public float Size
        {
            get
            {
                return mySize;
            }

            set
            {
                if (value < 0)
                {
                    value = 0;
                }
                else if (value > 1)
                {
                    value = 1;
                }
                mySize = value;
            }
        }


        protected float? myNearbyThingEffectAbundFactor = null;
        public virtual float NearbyThingEffectAbundFactor
        {
            get
            {
                if (myNearbyThingEffectAbundFactor == null) // not yet set
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

        protected float? myNearbyThingEffectSizeFactor = null;
        public virtual float NearbyThingEffectSizeFactor
        {
            get
            {
                if (myNearbyThingEffectSizeFactor == null) // not yet set
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



        public virtual ThingDef_StaticMineral Attributes
        {
            get
            {
                return def as ThingDef_StaticMineral;
            }
        }




        // ======= Yeilding resources ======= //

        public virtual void IncPctYeild(float amount, Pawn miner)
        {
            // Increase yeild for when it is destroyed
            float minerYield = 1f;
            if (miner.def.race.IsMechanoid)
            {
                minerYield = ((SkillNeed_Direct)DefDatabase<StatDef>.GetNamed("MiningYield").skillNeedFactors.Find(s => s.skill.defName == "Mining" && s is SkillNeed_Direct)).valuesPerLevel[miner.RaceProps.mechFixedSkillLevel];
            }
            else if (miner.RaceProps.Animal)
            {
                float toolPower = 1;
                foreach (var tool in miner.Tools)
                {
                    if (tool.power > toolPower)
                        toolPower = Mathf.Clamp((tool.power / 10f), 0.6f, 1.4f);
                }
                if (toolPower > 1)
                {
                    toolPower = 1 - (toolPower - 1);
                }
                else if (toolPower < 1)
                {
                    toolPower = 1 + (1 - toolPower);
                }
                minerYield = toolPower;
            }
            else
            {
                minerYield = miner.GetStatValue(StatDefOf.MiningYield, true);
            }
            //Log.Message("minerYield is: " + minerYield);
            int minerSkill = 10;
            if (miner.def.race.IsMechanoid)
            {
                minerSkill = miner.RaceProps.mechFixedSkillLevel;
            }
            else if (miner.RaceProps.Animal)
            {
                float toolPower = 2;
                foreach (var tool in miner.Tools)
                {
                    if (tool.power > toolPower)
                        toolPower = Mathf.Clamp(tool.power, 2, 20);
                }
                minerSkill = Convert.ToInt32(toolPower);
            }
            else
            {
                minerSkill = miner.skills.GetSkill(SkillDefOf.Mining).Level;
            }
            float proportionDamaged = (float)Mathf.Min(amount, HitPoints) / (float)MaxHitPoints;
            float proportionMined = proportionDamaged * minerYield;
            yieldPct += proportionMined;

            // Drop resources
            foreach (RandomResourceDrop toDrop in Attributes.randomlyDropResources)
            {
                // Check that resource is available
                ThingDef myThingDef = DefDatabase<ThingDef>.GetNamed(toDrop.resourceDefName, false);
                if (myThingDef == null)
                {
                    continue;
                }

                // Check that minimum skill is enough
                if (minerSkill < toDrop.minMiningSkill && !miner.def.race.IsMechanoid && !miner.RaceProps.Animal)
                {
                    continue;
                }

                // Find drop chance for this resource
                float dropChance = Size * toDrop.dropProbability * MineralsFrameworkMain.Settings.resourceDropFreqSetting;
                if (toDrop.scaleYieldBySkill)
                {
                    if (toDrop.wasteProduct)
                    {
                        dropChance *= proportionDamaged / minerYield;
                    } else
                    {
                        dropChance *= proportionMined;
                    }
                } else
                {
                    dropChance *= proportionDamaged;
                }
                if (dropChance < 1)
                {
                    if (Rand.Range(0f, 1f) > dropChance)
                    {
                        continue;
                    } else
                    {
                        dropChance = 1f;
                    }
                }

                // Drop resource
                int dropNum = (int)Math.Round(toDrop.dcuntPerDrop * MineralsFrameworkMain.Settings.resourceDropAmountSetting * dropChance);
                if (dropNum >= 1)
                {
                    Thing thing = ThingMaker.MakeThing(myThingDef, null);
                    thing.stackCount = dropNum;
                    if (toDrop.minified) {
                        thing = thing.MakeMinified();
                    }
                    GenPlace.TryPlaceThing(thing, Position, Map, ThingPlaceMode.Near, null);
                }
            }
        }

        public virtual float MiningSpeedFactor()
        {
            return Attributes.mineSpeedFactor * MineralsFrameworkMain.Settings.miningEffortSetting / Size;
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (dinfo.Def == DamageDefOf.Mining && dinfo.Instigator != null && dinfo.Instigator is Pawn pawn)
            {
                dinfo.SetAmount(dinfo.Amount * MiningSpeedFactor());
                IncPctYeild(dinfo.Amount, pawn);
            }
            base.PreApplyDamage(ref dinfo, out absorbed);
        }



        // ======= Appearance ======= //

        public float GetSizeBasedOnNearest(Vector3 subcenter, float baseSize)
        {
            float distToTrueCenter = Vector3.Distance(this.TrueCenter(), subcenter);
            float sizeOfNearest = 0;
            float distToNearest = 1;
            for (int xOffset = -1; xOffset <= 1; xOffset++)
            {
                for (int zOffset = -1; zOffset <= 1; zOffset++)
                {
                    if (xOffset == 0 & zOffset == 0)
                    {
                        continue;
                    }
                    IntVec3 checkedPosition = Position + new IntVec3(xOffset, 0, zOffset);
                    if (checkedPosition.InBounds(Map))
                    {
                        List<Thing> list = Map.thingGrid.ThingsListAt(checkedPosition);
                        foreach (Thing item in list)
                        {
                            if (item.def.defName == Attributes.defName)
                            {
                                float distanceToPos = Vector3.Distance(item.TrueCenter(), subcenter);

                                if (distToNearest > distanceToPos & distanceToPos <= 1)
                                {
                                    distToNearest = distanceToPos;
                                    sizeOfNearest = ((StaticMineral)item).Size;
                                }
                            }
                        }
                    }
                }
            }

            float correctedSize = (0.75f - distToTrueCenter) * baseSize + (1 - distToNearest) * sizeOfNearest;
            //Log.Message("this.size=" + this.size + " sizeOfNearest=" + sizeOfNearest + " distToNearest=" + distToNearest + " distToTrueCenter=" + distToTrueCenter);
            //Log.Message(this.size + " -> " + correctedSize + "  dist = " + distToNearest);

            return Attributes.visualSizeRange.LerpThroughRange(correctedSize);
        }

        public static float RandPos(float clustering, float spread)
        {
            // Weighted average of normal and uniform distribution
            return (Rand.Gaussian(0, 0.2f) * clustering + Rand.Range(-0.5f, 0.5f) * (1 - clustering)) * spread;
        }

        public virtual bool IsWaterLikeTerrain(TerrainDef t)
        {
            return t.IsWater || t.IsIce || t.IsFlood || t.IsRiver;
        }

        public virtual float SubmersibleFactor()
        {
            // Check that underwater minerals are enabled
            if (!MineralsFrameworkMain.Settings.underwaterMineralsSetting)
            {
                return 1f;
            }

            // Check that it is submersible
            if (Attributes.submergedSize >= 1)
            {
                return 1f;
            }

            // Check if is on dry land
            TerrainDef myTerrain = Map.terrainGrid.TerrainAt(Position);
            if (myTerrain == null || !IsWaterLikeTerrain(myTerrain))
            {
                return 1f;
            }

            // count number of dry cells aroud it
            float dryCount = 0;
            float spotsChecked = 0;
            for (int xOffset = -Attributes.submergedRadius; xOffset <= Attributes.submergedRadius; xOffset++)
            {
                for (int zOffset = -Attributes.submergedRadius; zOffset <= Attributes.submergedRadius; zOffset++)
                {
                    spotsChecked++;
                    IntVec3 checkedPosition = Position + new IntVec3(xOffset, 0, zOffset);
                    if (checkedPosition.InBounds(Map))
                    {
                        TerrainDef terrain = Map.terrainGrid.TerrainAt(checkedPosition);
                        if (terrain != null && !IsWaterLikeTerrain(terrain))
                        {
                            dryCount++;
                        }
                    }
                }
            }

            // calculate
            float propDry = 0f;
            if (spotsChecked > 0)
            {
                propDry = dryCount / spotsChecked;
            }
            return Attributes.submergedSize + (1 - Attributes.submergedSize) * propDry;
        }

        public virtual float PrintSizeFactor()
        {
            float effectiveSize = 1f;
            effectiveSize *= SubmersibleFactor();
            return effectiveSize;
        }

        public virtual float PrintSize()
        {
            return PrintSizeFactor() * Size;
        }

        public virtual void InitializeTextureLocations()
        {

            Rand.PushState();
            Rand.Seed = Position.GetHashCode() + Attributes.defName.GetHashCode();

            // initalize the array if it has not already been initalized
            if (textureLocations == null)
            {
                textureLocations = new Vector3[Attributes.maxMeshCount];
            }

            // Calculate the location of each texture
            Vector3 trueCenter = this.TrueCenter();
            for (int i = 0; i < textureLocations.Length; i++)
            {
                Vector3 pos = trueCenter;
                pos.x += RandPos(Attributes.visualClustering, Attributes.visualSpread * MineralsFrameworkMain.Settings.visualSpreadFactor);
                pos.z += RandPos(Attributes.visualClustering, Attributes.visualSpread * MineralsFrameworkMain.Settings.visualSpreadFactor);
                pos.z += Attributes.verticalOffset;
                pos.y = Attributes.Altitude;
                textureLocations[i] = pos;
            }

            // The size effects the altitude, which is a location attribute, so:
            InitializeTextureSizes();

            Rand.PopState();
        }

        public virtual Vector3 GetTextureLocation(int index)
        {
            // initalize the array if it has not already been initalized
            if (textureLocations == null)
            {
                InitializeTextureLocations();
            }

            // Return per-calculated location
            return(textureLocations[index]);
        }

        public virtual float CustomAltitude(int i) {
//            float zProportionOfTextureBottom = 1f - (getTextureLocation(i).z - (getTextureSize(i) / 2f)) / Map.Size.z;
//            float xPropDistToEven = Math.Abs(1f - ((getTextureLocation(i).x + 0.5f) % 2f));
            return Attributes.Altitude;// + zProportionOfTextureBottom * 0.01f + xPropDistToEven * 0.001f / Map.Size.z;
        } 

        public virtual void InitializeTextureSizes() {
        
            Rand.PushState();
            Rand.Seed = Position.GetHashCode() + Attributes.defName.GetHashCode();

            // initalize the array if it has not already been initalized
            if (textureSizes == null)
            {
                textureSizes = new float[Attributes.maxMeshCount];
            }

            // Calculate the size of each texture
            for (int i = 0; i < textureLocations.Length; i++)
            {
                // Get location of texture
                Vector3 pos = GetTextureLocation(i);

                // Adjust size for distance from center to other crystals
                float thisSize = GetSizeBasedOnNearest(pos, Size);

                // Add random variation
                thisSize += (thisSize * Rand.Range(- Attributes.visualSizeVariation, Attributes.visualSizeVariation));

                // Make large textures appear on top
                if (Attributes.largeTexturesOnTop)
                {
                    textureLocations[i].y = CustomAltitude(i) + 0.01f * thisSize;
                }
                else
                {
                    textureLocations[i].y = CustomAltitude(i);
                }

                textureSizes[i] = thisSize;

            }

            Rand.PopState();

        }

        public virtual float GetTextureSize(int index)
        {
            // initalize the array if it has not already been initalized
            if (textureSizes == null)
            {
                InitializeTextureSizes();
            }

            // Return per-calculated location
            return(textureSizes[index]);
        }

        public virtual void InitializeTextures() {

            Rand.PushState();
            Rand.Seed = Position.GetHashCode() + Attributes.defName.GetHashCode();

            // initalize the array if it has not already been initalized
            if (textureIndexes == null)
            {
                textureIndexes = new int[Attributes.maxMeshCount];
            }
                
            List<int> possibilities = Enumerable.Range(0, Attributes.GetTexturePaths().Count).OrderBy(order=>Rand.Range(0, 100)).ToList();
            for (int i = 0; i < Attributes.maxMeshCount; i++)
            {
                // get a new random set of textures if run out of options
                if (possibilities.Count == 0)
                {
                    possibilities = Enumerable.Range(0, Attributes.GetTexturePaths().Count).OrderBy(order=>Rand.Range(0, 100)).ToList();
                }
                textureIndexes[i] = possibilities[0];
                possibilities.RemoveAt(0);
            }

            Rand.PopState();

        }

        public virtual string GetTexturePath()
        {
            // initalize the array if it has not already been initalized
            if (textureIndexes == null)
            {
                InitializeTextures();
            }
                
            return(Attributes.GetTexturePaths()[textureIndexes[currentTextureIndex]]);
        }

        
        public static bool IsSameOrSubclass(Type potentialBase, Type potentialDescendant)
        {
            return potentialDescendant.IsSubclassOf(potentialBase)
                || potentialDescendant == potentialBase;
        }

        public static bool IsMineral(Thing thing)
        {
            return IsSameOrSubclass(typeof(StaticMineral), thing.GetType());
        }

        public static Thing IsMineralWall(Map map, IntVec3 pos)
        {
            if (pos.InBounds(map))
            {
                List<Thing> list = pos.GetThingList(map);
                foreach (Thing item in list)
                {

                    if (IsMineral(item) && item.def.passability == Traversability.Impassable)
                    {
                        return item;
                    }
                }
            }
            return null;
        }

        public virtual float InteractWithWalls(int i, ref Vector3 center, float size)
        {
            if (MineralsFrameworkMain.Settings.mineralsGrowUpWallsSetting && Attributes.growsUpWalls)
            {
                Vector3 squareCenter = this.TrueCenter();
                float leftOverlap = (squareCenter.x - 0.5f) - (center.x - size / 2);
                if (leftOverlap > 0) // left
                {
                    IntVec3 leftSide = Position - new IntVec3(1, 0, 0);
                    Thing leftWall = IsMineralWall(Map, leftSide);
                    if (leftWall != null)
                    {
                        // Put half of the textures on the front of the wall
                        if (Rand.Bool)
                        {
                            center.y = leftWall.def.Altitude + 0.1f;
                        }
                        // make textures higher up the wall show on top
                        center.y += Math.Min(leftOverlap / size, 1f) * 0.1f;

                        // rotate based on proportion of texture overlapping
                        return Math.Min(90f * (leftOverlap / size), 90f);
                    }
                }
                float rightOverlap = (center.x + size / 2) - (squareCenter.x + 0.5f);
                if (rightOverlap > 0)
                {
                    IntVec3 rightSide = Position + new IntVec3(1, 0, 0);
                    Thing rightWall = IsMineralWall(Map, rightSide);
                    if (rightWall != null)
                    {
                        // Put half of the textures on the front of the wall
                        if (Rand.Bool)
                        {
                            center.y = rightWall.def.Altitude + 0.1f;
                        }
                        // make textures higher up the wall show on top
                        center.y += Math.Min(rightOverlap / size, 1f) * 0.1f;

                        // rotate based on proportion of texture overlapping
                        return -Math.Min(90f * (rightOverlap / size), 90f);
                    }
                }
                float topOverlap = (center.z + size / 2) - (squareCenter.z + 0.5f);
                if (topOverlap > 0)
                {
                    IntVec3 topSide = Position + new IntVec3(0, 0, 1);
                    Thing topWall = IsMineralWall(Map, topSide);
                    if (topWall != null)
                    {
                        center.y = topWall.def.Altitude + 0.1f;
                        return 180;
                    }
                }
            }
            if (Attributes.printOverWalls)
            {
                Vector3 squareCenter = this.TrueCenter();
                float topOverlap = (center.z + size / 2) - (squareCenter.z + 0.4f);
                if (topOverlap > 0)
                {
                    IntVec3 topSide = Position + new IntVec3(0, 0, 1);
                    Thing topWall = IsMineralWall(Map, topSide);
                    if (topWall != null)
                    {
                        center.y = topWall.def.Altitude + 0.001f;
                        return 0f;
                    }
                }
            }

            return 0f;
        }

        public virtual bool HiddenInSnow(int i)
        {
            if (Attributes.hiddenInSnowThreshold > 10f)
            {
                return false;
            }
            return SnowLevel() > Attributes.hiddenInSnowThreshold * GetTextureSize(i) / Attributes.visualSizeRange.max;
        }

        public virtual void PrintSubTexture(SectionLayer layer, int i, float sizeFactor = 1f)
        {
            Rand.PushState();
            Rand.Seed = Position.GetHashCode() + Attributes.defName.GetHashCode() + i.GetHashCode();

            // Get location
            Vector3 center = GetTextureLocation(i);

            // Get size
            float thisSize = GetTextureSize(i) * sizeFactor;
            if (thisSize <= 0)
            {
                Rand.PopState();
                return;
            }

            // Check if snow is covering it
            if (HiddenInSnow(i))
            {
                Rand.PopState();
                return;
            }

            // Get rotation
            float thisRotation = InteractWithWalls(i, ref center, thisSize);

            // Print image
            Material matSingle = Graphic.MatSingle;
            Vector2 sizeVec = new Vector2(thisSize, thisSize);
            Printer_Plane.PrintPlane(layer, center, sizeVec, matSingle, thisRotation, Rand.Bool, null, null, Attributes.topVerticesAltitudeBias * thisSize, 0f);

            Rand.PopState();
        }


        public override void Print(SectionLayer layer)
        {

            // get print size
            float sizeFactor = PrintSizeFactor();

            if (sizeFactor <= 0.05f)
            {
                return;
            }

            Rand.PushState();
            Rand.Seed = Attributes.defName.GetHashCode() + thingIDNumber.GetHashCode();

            if (this.Attributes.graphicData.graphicClass.Name != "Graphic_Random" || this.Attributes.graphicData.linkType == LinkDrawerType.CornerFiller) {
                currentTextureIndex = 0;
                base.Print(layer);
			} else {
                int numToPrint = Mathf.CeilToInt(PrintSize() * (float)Attributes.maxMeshCount);
				if (numToPrint < 1)
				{
					numToPrint = 1;
				}
				for (int i = 0; i < numToPrint; i++)
				{
                    PrintSubTexture(layer, i, sizeFactor);
                    currentTextureIndex = i;
				}
			}

            Rand.PopState();

        }


        public virtual string ResourceDropList()
        {
            var dropDict = new Dictionary<string, DropInfo>();

            // Add thing dropped on destruction first
            if (Attributes.building.mineableDropChance > 0 && Attributes.building.mineableThing != null)
            {
                string defName = Attributes.building.mineableThing.defName;
                float amount = Attributes.building.mineableDropChance;
                if (Attributes.building.mineableYield != 0)
                {
                    amount = Attributes.building.mineableDropChance * (float)Attributes.building.mineableYield;
                }
                if (dropDict.TryGetValue(defName, out DropInfo existingDrop))
                {
                    existingDrop.Amount += amount;
                    existingDrop.Ooutput = $"{existingDrop.Amount} {Attributes.building.mineableThing.label}";
                }
                else
                {
                    dropDict[defName] = new DropInfo
                    {
                        Amount = amount,
                        Ooutput = $"{amount} {Attributes.building.mineableThing.label}"
                    };
                }
            }

            // Add each extra resource that can be dropped
            foreach (RandomResourceDrop resource in Attributes.randomlyDropResources)
            {
                // Check that resource is available
                ThingDef myThingDef = DefDatabase<ThingDef>.GetNamed(resource.resourceDefName, false);
                if (myThingDef == null)
                {
                    continue;
                }

                float meanDrop = resource.dcuntPerDrop * resource.dropProbability * Size * MineralsFrameworkMain.Settings.resourceDropAmountSetting * MineralsFrameworkMain.Settings.resourceDropFreqSetting;
                if (meanDrop < 0.01f) 
                {
                    continue;
                }
                else if (meanDrop >= 1)
                {
                    meanDrop = (float)Math.Floor(meanDrop);
                }
                else
                {
                    meanDrop = (float)Math.Round(meanDrop, 2);
                }

                string defName = resource.resourceDefName;
                if (dropDict.TryGetValue(defName, out DropInfo existingDrop))
                {
                    existingDrop.Amount += meanDrop;
                    existingDrop.Ooutput = $"{existingDrop.Amount} {myThingDef.label}";
                }
                else
                {
                    dropDict[defName] = new DropInfo
                    {
                        Amount = meanDrop,
                        Ooutput = $"{meanDrop} {myThingDef.label}"
                    };
                }
            }

            // Sort by resource abundance and create output
            var sortedDrops = dropDict.Values.OrderByDescending(x => x.Amount).ToList();
            return string.Join(", ", sortedDrops.Select(x => x.Ooutput));
        }


        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("Size: " + Size.ToStringPercent());
            stringBuilder.AppendLine("Mining speed: " + MiningSpeedFactor().ToStringPercent());
            float propSubmerged = 1 - SubmersibleFactor();
            if (propSubmerged > 0)
            {
                stringBuilder.AppendLine("Submerged: " + propSubmerged.ToStringPercent());
            }
            stringBuilder.AppendLine("Resources: " + ResourceDropList());
            return stringBuilder.ToString().TrimEndNewlines();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<float>(ref mySize, "mySize", 1);
        }
        public override void Destroy(DestroyMode mode)
        {
            if (!string.IsNullOrEmpty(Attributes.makeTerrainOnDestroy))
            {
                TerrainDef newTerrain = DefDatabase<TerrainDef>.GetNamed(Attributes.makeTerrainOnDestroy, false);
                if (newTerrain != null && Map != null && Position.InBounds(Map))
                {
                    Map.terrainGrid.SetTerrain(Position, newTerrain);
                }
            }
            base.Destroy(mode);
        }
        public virtual float SnowLevel()
        {
            if (Map == null)
            {
                return 0f;
            }
            if (Attributes.passability == Traversability.Impassable)
            {
                if (Position.Roofed(Map))
                {
                    return 0f;
                }

                float total = 0f;
                int numChecked = 0;
                for (int xOffset = -1; xOffset <= 1; xOffset++)
                {
                    for (int zOffset = -1; zOffset <= 1; zOffset++)
                    {
                        IntVec3 checkedPosition = Position + new IntVec3(xOffset, 0, zOffset);
                        if (checkedPosition.InBounds(Map) && (! checkedPosition.Impassable(Map)))
                        {
                            total += checkedPosition.GetSnowDepth(Map);
                            numChecked += 1;
                        }
                    }
                }
                if (numChecked == 0)
                {
                    return 0f;
                }
                else
                {
                    return total / numChecked;
                }
            }
            else
            {
                return Position.GetSnowDepth(Map);
            }
        }


        public override Graphic Graphic
        {
            get
            {
      
                // Pick a random path 
                string printedTexturePath = GetTexturePath();

                // Check if it should be snowy
                if (Attributes.hasSnowyTextures && SnowLevel() > Attributes.snowTextureThreshold)
                {
                    printedTexturePath += "_s";
                }
                Graphic printedTexture = GraphicDatabase.Get<Graphic_Single>(printedTexturePath, Attributes.graphicData.shaderType.Shader);

                // convert to corner filler if needed
                printedTexture = GraphicDatabase.Get<Graphic_Single>(printedTexture.path, printedTexture.Shader, printedTexture.drawSize, DrawColor, DrawColorTwo, printedTexture.data);
                if (Attributes.graphicData.linkType == LinkDrawerType.CornerFiller)
                {
                     return new Graphic_LinkedCornerFiller(printedTexture);
                }
                else
                {
                    return  printedTexture;

                }

             }
        }

        public virtual float RandomColorProb(Color colorUsed) {
            Rand.PushState();
            Rand.Seed = Map.GetHashCode() + colorUsed.GetHashCode();
            float output = Rand.Range(0.1f, 1f);
            Rand.PopState();
            return output * output * output;
        }

        public override Color DrawColor {
            get
            {
                if (this.Attributes.coloredByTerrain)
                {
                    TerrainDef terrain = this.Position.GetTerrain(this.Map);
                    if (terrain.graphic.Color == Color.white)
                    {
                        return base.DrawColor;
                    }
                    else
                    {
                        return terrain.graphic.Color;
                    }
                }

                if (this.Attributes.randomColorsOne != null && this.Attributes.randomColorsOne.Count > 0)
                {
                    if (Attributes.seedRandomColorByMap)
                    {
                        return this.Attributes.randomColorsOne.RandomElementByWeight(RandomColorProb);
                    }
                    else
                    {
                        return this.Attributes.randomColorsOne.RandomElement();
                    }
          
                }

                return base.DrawColor;
            }
        }

        public override Color DrawColorTwo
        {
            get
            {
                if (this.Attributes.randomColorsTwo != null && this.Attributes.randomColorsTwo.Count > 0)
                {
                    if (Attributes.seedRandomColorByMap)
                    {
                        return this.Attributes.randomColorsTwo.RandomElementByWeight(RandomColorProb);
                    }
                    else
                    {
                        return this.Attributes.randomColorsTwo.RandomElement();
                    }

                }

                return base.DrawColorTwo;
            }
        }
    }



    /// <summary>
    /// RandomResourceDrop class.
    /// </summary>
    /// <author>zachary-foster</author>
    /// <permission>No restrictions</permission>
    public class RandomResourceDrop
    {
        public string resourceDefName;
        public float dropProbability;
        public int dcuntPerDrop = 1;
        public int minMiningSkill = 0;
        public bool scaleYieldBySkill = true;
        public bool wasteProduct = false;
        public bool minified = false;
    }


    /// <summary>
    /// NeededNearby class.
    /// </summary>
    /// <author>zachary-foster</author>
    /// <permission>No restrictions</permission>
    public class NearbyThingEffect
    {
        // Terrain or thing defnames to look for
        public List<string> defNames;
        // How far to look for DefNames relative to a given position. 0 means only the given position
        public float radius = 1f;
        // The amount that will be multiplied to the spawn probability or size when DefNames is found
        public float foundFactor = 1f;
        // The minimum amount that will be multiplied to the spawn probability or size when DefNames is not found
        public float notFoundFactor = 0f;
        // Return a value between FoundFactor and NotFoundFactor base on minimum distance to DefNames
        public bool scaleByDistance = false;
        // Return a value between FoundFactor and NotFoundFactor base on proportion of area occupied by DefNames
        public bool scaleByArea = false;
        // Determines the how values are interpoleted between FoundFactor and NotFoundFactor. 1 = linear, lower = slow falloff, higher = fast falloff
        public float scaleFalloff = 1f;
    }



    /// <summary>
    /// ThingDef_StaticMineral class.
    /// </summary>
    /// <author>zachary-foster</author>
    /// <permission>No restrictions</permission>
    public class ThingDef_StaticMineral : ThingDef
    {

        // The probability that this mineral type will be spawned at all on a given map
        public float perMapProbability = 1f; 

        // For a given map, the minimum/maximum probablility a cluster will spawn for every possible location
        public float minClusterProbability = 0.001f; 
        public float maxClusterProbability = 0.01f;

        // How far away it can spawn from an existing location
        // Even though it is a static mineral, the map initialization uses "reproduction" to make clusters 
        public int spawnRadius = 2; 

        // How  many squares each cluster will be
        public int minClusterSize = 1;
        public int maxClusterSize = 10;

        // The range of starting sizes of individuals in clusters
        public float initialSizeMin = 0.3f;
        public float initialSizeMax = 0.9f;

        // How much initial sizes of individuals randomly vary
        public float initialSizeVariation = 0.3f;

        // The biomes this can appear in. If null, all are allowed
        public List<string> allowedBiomes;

        // How nearby things or terrains affect spawn abundance
        public List<NearbyThingEffect> nearbyThingAbundEffects;

        // How nearby things or terrains affect spawn size
        public List<NearbyThingEffect> nearbyThingSizeEffects;

        // If true, only grows under roofs
        public bool mustBeUnderRoof = false;
        public bool mustBeNotUnderRoof = false;
        public bool mustBeUnderThickRoof = false;
        public bool mustBeNotUnderThickRoof = false;
        public bool mustBeNearPassable = false; 
        public bool mustBeNotNearPassable = false;
        public bool mustBeNearRoof = false;
        public int mustBeNearRoofDist = 1;

        // Things this mineral replaces when a map is initialized
        public List<string> thingsToReplace; 

        // If it replaces everything
        public bool replaceAll = false;

        // If it must replace something in order to spawned
        public bool mustReplace = false;

        // The radius that will be searched to replace things. 0 = only checked cell
        public int replaceRadius = 0;

        // The minmum propotion of things in radius to replace for a replacement to happen 
        public float replaceThreshold = 0.3f;

        // If it can spawn on other things
        public bool canSpawnOnThings = false;

        // What stage of map generation the thing is spawned during (rocks or ice)
        public string newMapGenStep = "rocks";
        
        // Order in which minerals are spawned during map generation (lower numbers first)
        public int newMapSpawnOrder = 100;

        // Minimum distance from the nearest settlement the inital spawn needs to be in order to be spawned at the maximum probablity
        public float otherSettlementMiningRadius = 0f;

        // If the mean size of minerals spawned at map generation is scaled by the relative abundance in that map
        public bool sizeScaledByAbundance = false;

        // The maximum number of images that will be printed per square
        public int maxMeshCount = 4;

        // The size range of images printed
        public FloatRange visualSizeRange  = new FloatRange(0.3f, 1.0f);

        // between 0 and 1. 0 = uniform distribution, 1 = normal distribution
        public float visualClustering = 0.5f;

        // 1 = everything appears within cell and can spawn at edge when visualClustering == 0
        public float visualSpread = 1.5f;

        // 0 = all images in cluster are same size
        public float visualSizeVariation = 0.2f;

        // If graphic overlapping with nearby wall textures are rotated
        public bool growsUpWalls = false;

        // If textures overlapping walls above them should be printed on top
        public bool printOverWalls = false;

        // If largest textures are printed on top, ro if vertical order matters
        public bool largeTexturesOnTop = false;

        // How much to change the vertical position of the texture. Positive is up
        public float verticalOffset = 0f;

        // at what snow depth the snow texture is used for full sized minerals
        public float snowTextureThreshold = 0.8f;

        // at what snow depth a full sized mineral is completely hidden
        public float hiddenInSnowThreshold = 1f;

        // Has something to do with how textures on the same layer get stacked
        public float topVerticesAltitudeBias = 0.01f;
        
        // If the primary color is based on the stone below it
        public bool coloredByTerrain = false;

        // If defined, randomly pick colors from this set
        public List<Color> randomColorsOne;
        public List<Color> randomColorsTwo;

        // If true, then the probability of each color is randomly chosen for each map, so each map has distinctive colors.
        public bool seedRandomColorByMap = true;

        // If smaller than 1, it looks smaller in water
        public float submergedSize = 1;

        // How big of an area is checked when determining how submerged something is
        public int submergedRadius = 2;

        // Other resources it might drop
        public List<RandomResourceDrop> randomlyDropResources;

        // How easy it is to mine
        public float mineSpeedFactor = 1f;

        // Tags which determine how some options behave
        public List<string> tags;

        // List of defnames that this should be used as a template to create new defs for by replacing occurances of a string with each defname
        public List<string> isTemplateFor;
        public string templateReplaceString;

        // Populated based on graphicData.texPath, not meant for configuration via XML
        public List<string> texturePaths;
        public List<string> snowTexturePaths;
        public bool hasSnowyTextures = false;
        public string makeTerrainOnSpawn;
        public string makeTerrainOnDestroy;


        public virtual ThingDef_StaticMineral DeepCopy()
        {
            ThingDef_StaticMineral copy = new ThingDef_StaticMineral
            {
                defName = this.defName,
                label = this.label,
                description = this.description,
                thingClass = this.thingClass,
                category = this.category,
                selectable = this.selectable,
                neverMultiSelect = this.neverMultiSelect,
                altitudeLayer = this.altitudeLayer,
                passability = this.passability,
                castEdgeShadows = this.castEdgeShadows,
                fillPercent = this.fillPercent,
                coversFloor = this.coversFloor,
                blockWind = this.blockWind,
                blockLight = this.blockLight,
                saveCompressible = this.saveCompressible,
                staticSunShadowHeight = this.staticSunShadowHeight,
                holdsRoof = this.holdsRoof,
                pathCost = this.pathCost,
                mineable = this.mineable,
                leaveResourcesWhenKilled = this.leaveResourcesWhenKilled,
                filthLeaving = this.filthLeaving,
                drawerType = this.drawerType,
                scatterableOnMapGen = this.scatterableOnMapGen,
                hideAtSnowOrSandDepth = this.hideAtSnowOrSandDepth,
                statBases = this.statBases?.Select(s => new StatModifier { stat = s.stat, value = s.value }).ToList(),
                uiIconPath = this.uiIconPath
            };
            
            if (this.building != null)
            {
                copy.building = new BuildingProperties
                {
                    isInert = this.building.isInert,
                    canBuildNonEdificesUnder = this.building.canBuildNonEdificesUnder,
                    isNaturalRock = this.building.isNaturalRock,
                    isResourceRock = this.building.isResourceRock,
                    mineableDropChance = this.building.mineableDropChance,
                    mineableYield = this.building.mineableYield,
                    mineableThing = this.building.mineableThing,
                    mineableNonMinedEfficiency = this.building.mineableNonMinedEfficiency,
                    veinMineable = this.building.veinMineable,
                    smoothedThing = this.building.smoothedThing,
                    claimable = this.building.claimable,
                    alwaysDeconstructible = this.building.alwaysDeconstructible,
                    isEdifice = this.building.isEdifice,
                    destroyShakeAmount = this.building.destroyShakeAmount,
                    mineablePreventMeteorite = this.building.mineablePreventMeteorite,
                    ai_neverTrashThis = this.building.ai_neverTrashThis
                };
            }

            if (this.graphicData != null)
            {
                copy.graphicData = new GraphicData
                {
                    shaderType = this.graphicData.shaderType,
                    graphicClass = this.graphicData.graphicClass,
                    texPath = this.graphicData.texPath,
                    color = this.graphicData.color,
                    colorTwo = this.graphicData.colorTwo,
                    drawSize = this.graphicData.drawSize,
                    linkType = this.graphicData.linkType,
                    linkFlags = this.graphicData.linkFlags
                };

                if (this.graphicData.damageData != null) {
                    copy.graphicData.damageData = new DamageGraphicData()
                    {
                        cornerTL = this.graphicData.damageData.cornerTL,
                        cornerTR = this.graphicData.damageData.cornerTR,
                        cornerBL = this.graphicData.damageData.cornerBL,
                        cornerBR = this.graphicData.damageData.cornerBR,
                        edgeTop = this.graphicData.damageData.edgeTop,
                        edgeBot = this.graphicData.damageData.edgeBot,
                        edgeLeft = this.graphicData.damageData.edgeLeft,
                        edgeRight = this.graphicData.damageData.edgeRight,
                        enabled = this.graphicData.damageData.enabled
                    };
                }

            }

            // Copy all custom fields
            copy.perMapProbability = this.perMapProbability;
            copy.minClusterProbability = this.minClusterProbability;
            copy.maxClusterProbability = this.maxClusterProbability;
            copy.spawnRadius = this.spawnRadius;
            copy.minClusterSize = this.minClusterSize;
            copy.maxClusterSize = this.maxClusterSize;
            copy.initialSizeMin = this.initialSizeMin;
            copy.initialSizeMax = this.initialSizeMax;
            copy.initialSizeVariation = this.initialSizeVariation;
            copy.nearbyThingAbundEffects = this.nearbyThingAbundEffects?.Select(x => new NearbyThingEffect
            {
                defNames = x.defNames != null ? new List<string>(x.defNames) : null,
                radius = x.radius,
                foundFactor = x.foundFactor,
                notFoundFactor = x.notFoundFactor,
                scaleByDistance = x.scaleByDistance,
                scaleByArea = x.scaleByArea,
                scaleFalloff = x.scaleFalloff
            }).ToList();
            copy.nearbyThingSizeEffects = this.nearbyThingSizeEffects?.Select(x => new NearbyThingEffect
            {
                defNames = x.defNames != null ? new List<string>(x.defNames) : null,
                radius = x.radius,
                foundFactor = x.foundFactor,
                notFoundFactor = x.notFoundFactor,
                scaleByDistance = x.scaleByDistance,
                scaleByArea = x.scaleByArea,
                scaleFalloff = x.scaleFalloff
            }).ToList();
            copy.mustBeUnderRoof = this.mustBeUnderRoof;
            copy.mustBeNotUnderRoof = this.mustBeNotUnderRoof;
            copy.mustBeUnderThickRoof = this.mustBeUnderThickRoof;
            copy.mustBeNotUnderThickRoof = this.mustBeNotUnderThickRoof;
            copy.mustBeNearPassable = this.mustBeNearPassable;
            copy.mustBeNotNearPassable = this.mustBeNotNearPassable;
            copy.mustBeNearRoof = this.mustBeNearRoof;
            copy.mustBeNearRoofDist = this.mustBeNearRoofDist;
            copy.thingsToReplace = this.thingsToReplace != null ? new List<string>(this.thingsToReplace) : null;
            copy.replaceAll = this.replaceAll;
            copy.mustReplace = this.mustReplace;
            copy.replaceRadius = this.replaceRadius;
            copy.replaceThreshold = this.replaceThreshold;
            copy.canSpawnOnThings = this.canSpawnOnThings;
            copy.newMapGenStep = this.newMapGenStep;
            copy.newMapSpawnOrder = this.newMapSpawnOrder;
            copy.otherSettlementMiningRadius = this.otherSettlementMiningRadius;
            copy.sizeScaledByAbundance = this.sizeScaledByAbundance;
            copy.maxMeshCount = this.maxMeshCount;
            copy.visualSizeRange = this.visualSizeRange;
            copy.visualClustering = this.visualClustering;
            copy.visualSpread = this.visualSpread;
            copy.visualSizeVariation = this.visualSizeVariation;
            copy.growsUpWalls = this.growsUpWalls;
            copy.printOverWalls = this.printOverWalls;
            copy.largeTexturesOnTop = this.largeTexturesOnTop;
            copy.verticalOffset = this.verticalOffset;
            copy.snowTextureThreshold = this.snowTextureThreshold;
            copy.hiddenInSnowThreshold = this.hiddenInSnowThreshold;
            copy.topVerticesAltitudeBias = this.topVerticesAltitudeBias;
            copy.coloredByTerrain = this.coloredByTerrain;
            copy.randomColorsOne = this.randomColorsOne != null ? new List<Color>(this.randomColorsOne) : null;
            copy.randomColorsTwo = this.randomColorsTwo != null ? new List<Color>(this.randomColorsTwo) : null;
            copy.seedRandomColorByMap = this.seedRandomColorByMap;
            copy.submergedSize = this.submergedSize;
            copy.submergedRadius = this.submergedRadius;
            copy.randomlyDropResources = this.randomlyDropResources?.Select(d => new RandomResourceDrop
            {
                resourceDefName = d.resourceDefName,
                dropProbability = d.dropProbability,
                dcuntPerDrop = d.dcuntPerDrop,
                minMiningSkill = d.minMiningSkill,
                scaleYieldBySkill = d.scaleYieldBySkill,
                wasteProduct = d.wasteProduct,
                minified = d.minified
            }).ToList();
            copy.isTemplateFor = this.isTemplateFor != null ? new List<string>(this.isTemplateFor) : null;
            copy.templateReplaceString = this.templateReplaceString;  // string is immutable so direct assignment is safe
            copy.tags = this.tags != null ? new List<string>(this.tags) : null;
            copy.texturePaths = this.texturePaths != null ? new List<string>(this.texturePaths) : null;
            copy.snowTexturePaths = this.snowTexturePaths != null ? new List<string>(this.snowTexturePaths) : null;
            copy.hasSnowyTextures = this.hasSnowyTextures;
            copy.mineSpeedFactor = this.mineSpeedFactor;

            return copy;
        }

        // ======= Spawning clusters ======= //


        public StaticMineral TryReproduce(Map map, IntVec3 position)
        {
            if (!TryFindReproductionDestination(map, position, out IntVec3 dest))
            {
                return null;
            }
            return TrySpawnAt(dest, map, 0.01f);
        }


        public virtual StaticMineral TrySpawnCluster(Map map, IntVec3 position, float size, int clusterCount)
        {
            StaticMineral mineral = TrySpawnAt(position, map, size);
            if (mineral != null)
            {
                GrowCluster(map, mineral, clusterCount);

            }
            return mineral;
        }

        public virtual StaticMineral SpawnCluster(Map map, IntVec3 position, float size, int clusterCount)
        {
            StaticMineral mineral = SpawnAt(map, position, size);
            if (mineral != null)
            {
                GrowCluster(map, mineral, clusterCount);

            }
            return mineral;
        }


        public virtual void GrowCluster(Map map, StaticMineral sourceMineral, int times)
        {
            if (times > 0)
            {
                StaticMineral newGrowth = sourceMineral.Attributes.TryReproduce(map, sourceMineral.Position);
                if (newGrowth != null)
                {
                    newGrowth.Size = Rand.Range(1f - initialSizeVariation, 1f + initialSizeVariation) * sourceMineral.Size;
                    GrowCluster(map, newGrowth, times - 1);
                }

            }
        }


        public virtual Thing ThingToReplaceAtPos(Map map, IntVec3 position)
        {
            //if (defName == "BigColdstoneCrystal") Log.Message("ThingToReplaceAtPos: checking for " + defName +  " at " + position, true);
            if (thingsToReplace == null || thingsToReplace.Count == 0)
            {
                //if (defName == "BigColdstoneCrystal") Log.Message("ThingToReplaceAtPos: no replacement defined", true);
                return(null);
            }
            int spotsChecked = 0;
            float replaceCount = 0;
            for (int xOffset = -replaceRadius; xOffset <= replaceRadius; xOffset++)
            {
                for (int zOffset = -replaceRadius; zOffset <= replaceRadius; zOffset++)
                {
                    spotsChecked += 1;
                    IntVec3 checkedPosition = position + new IntVec3(xOffset, 0, zOffset);
                    if (checkedPosition.InBounds(map))
                    {
                        foreach (Thing thing in map.thingGrid.ThingsListAt(checkedPosition))
                        {
                            if (thing == null || thing.def == null)
                            {
                                continue;
                            }

                            if (thingsToReplace.Any(thing.def.defName.Equals))
                            {
                                if (StaticMineral.IsMineral(thing))
                                {
                                    replaceCount += ((StaticMineral) thing).Size;
                                }
                                else
                                {
                                    replaceCount += 1;
                                }
                            }
                        }
                    }
                }
            }
            if (((float)replaceCount) / ((float)spotsChecked) > replaceThreshold)
            {
                //Log.Message(this.defName + " can replace at " + position, true);
                foreach (Thing thing in map.thingGrid.ThingsListAt(position))
                {
                    if (thing == null || thing.def == null)
                    {
                        continue;
                    }
                    if (thingsToReplace.Any(thing.def.defName.Equals))
                    {
                        return (thing);
                    }
                }
            }
            else
            {
                //Log.Message(this.defName + " can not replace at " + position + " with density " + ((float)replaceCount) / ((float)spotsChecked), true);
                return(null);
            }
            return(null);
        }

        // ======= Spawning conditions ======= //

        public bool IsNearPassable(Map map, IntVec3 position, int radius = 1)
        {
            for (int xOffset = -radius; xOffset <= radius; xOffset++)
            {
                for (int zOffset = -radius; zOffset <= radius; zOffset++)
                {
                    IntVec3 checkedPosition = position + new IntVec3(xOffset, 0, zOffset);
                    if (checkedPosition.InBounds(map))
                    {
                        if (!checkedPosition.Impassable(map))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;

        }


        public virtual bool CanSpawnAt(Map map, IntVec3 position, bool initialSpawn)
        {

            // Check that location is in the map
            if (! position.InBounds(map))
            {
                return false;
            }

            // Check that it is under a roof if it needs to be
            if (!IsRoofConditionOk(map, position))
            {
                return false;
            }

            // Look for stuff in the way
            if (PlaceIsBlocked(map, position, initialSpawn))
            {
                return false;
            }

            // Check for things it must replace
            if (mustReplace && ThingToReplaceAtPos(map, position) == null)
            {
                return false;
            }

            // Check if it is near passable if needed
            bool nearPassable = IsNearPassable(map, position);
            if (mustBeNearPassable && !nearPassable) {
                return false;
            }
            if (mustBeNotNearPassable && nearPassable) {
                return false;
            }

            return true;
        }

        public virtual bool PlaceIsBlocked(Map map, IntVec3 position, bool initialSpawn)
        {
            if (ThingToReplaceAtPos(map, position) != null)
            {
                return false;
            }
            foreach (Thing thing in map.thingGrid.ThingsListAt(position))
            {
                if (thing == null || thing.def == null)
                {
                    continue;
                }

                // Blocked by pawns, items, and plants
                if (! canSpawnOnThings) {
                    if (thing.def.category == ThingCategory.Pawn ||
                        thing.def.category == ThingCategory.Item ||
                        thing.def.category == ThingCategory.Plant ||
                        thing.def.category == ThingCategory.Building
                    )
                    {
                        return true;
                    }
                }

                // Blocked by impassible things
                if (thing.def.passability == Traversability.Impassable)
                {
                    return true;
                }
                    
            }
            return false;
        }

		public static bool PosHasThing(Map map, IntVec3 position, List<string> things)
		{
			if (things == null || things.Count == 0)
			{
				return false;
			}

			TerrainDef terrain = map.terrainGrid.TerrainAt(position);
			if (things.Any(terrain.defName.Equals))
			{
				return true;
			}

			foreach (Thing thing in map.thingGrid.ThingsListAt(position))
			{
				if (thing == null || thing.def == null)
				{
					continue;
				}

				if (things.Any(thing.def.defName.Equals))
				{
					return true;
				}
			}
			return false;
		}

        public virtual bool CanSpawnInBiome(Map map) 
        {
            if (allowedBiomes == null || allowedBiomes.Count == 0)
            {
                return true;
            }
            else
            {
                return allowedBiomes.Any(map.Biome.defName.Equals);
            }
        }


        // ======= Spawning individuals ======= //


        public virtual StaticMineral TrySpawnAt(IntVec3 dest, Map map, float size)
        {
            if (CanSpawnAt(map, dest, false))
            {
                return SpawnAt(map, dest, size);
            }
            else
            {
                return null;
            }
        }

        public virtual StaticMineral SpawnAt(Map map, IntVec3 dest, float size)
        {
            // Remove things to replace
            Thing thingToRemove = ThingToReplaceAtPos(map, dest);
            thingToRemove?.Destroy(DestroyMode.Vanish);

            StaticMineral output = (StaticMineral)ThingMaker.MakeThing(this);
            GenSpawn.Spawn(output, dest, map, WipeMode.Vanish);
            output.Size = size;
            map.mapDrawer.MapMeshDirty(dest, MapMeshFlagDefOf.Buildings);
            map.edificeGrid.Register(output);
            if (!string.IsNullOrEmpty(makeTerrainOnSpawn))
            {
                TerrainDef newTerrain = DefDatabase<TerrainDef>.GetNamed(makeTerrainOnSpawn, false);
                if (newTerrain != null)
                {
                    map.terrainGrid.SetTerrain(dest, newTerrain);
                }
            }
            //Log.Message("Spawned " + defName + " at " + dest);
            return output;
        }
            

        // ======= Reproduction ======= //



        public virtual bool TryFindReproductionDestination(Map map,  IntVec3 position, out IntVec3 foundCell)
        {
            if ((!position.InBounds(map)) || position.DistanceToEdge(map) <= Mathf.CeilToInt(spawnRadius))
            {
                foundCell = position;
                return false;
            }
            Predicate<IntVec3> validator = isValidSite(map, position);
            try
            {
                return CellFinder.TryFindRandomCellNear(position, map, Mathf.CeilToInt(spawnRadius), validator, out foundCell);
            }
            catch
            {
                Log.Warning("Minerals: TryFindReproductionDestination: exception caught tying to spawn near" + position);
                foundCell = position;
                return false;
            }

            Predicate<IntVec3> isValidSite(Map myMap, IntVec3 myPosition)
            {
                return c => c.DistanceTo(myPosition) <= spawnRadius && CanSpawnAt(myMap, c, false);
            }

        }

        public virtual bool IsNearRoof(Map map, IntVec3 position, int radius = 1)
        {
            // Allow to spawn near roofs
            bool validator(IntVec3 c) => c.InBounds(map) && c.Roofed(map);

            if (CellFinder.TryFindRandomCellNear(position, map, radius, validator, out IntVec3 unused))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public virtual bool IsRoofConditionOk(Map map, IntVec3 position)
        {
            if (mustBeUnderThickRoof && (map.roofGrid.RoofAt(position) == null || (! map.roofGrid.RoofAt(position).isThickRoof)))
            {
                return false;
            }

            if (mustBeNotUnderThickRoof && (map.roofGrid.RoofAt(position) != null && map.roofGrid.RoofAt(position).isThickRoof))
            {
                return false;
            }

            if (mustBeUnderRoof && (! map.roofGrid.Roofed(position)))
            {
                return false;
            }

            if (mustBeNotUnderRoof && map.roofGrid.Roofed(position))
            {
                return false;
            }

            if (mustBeNearRoof && !IsNearRoof(map, position, mustBeNearRoofDist))
            {
                return false;
            }

            return true;
        }







        // ======= Map initialization ======= //


        public virtual void InitNewMap(Map map, float scaling = 1)
        {
            //Log.Message("Minerals: Initializing mineral '" + this.defName + "' with scaling of " + scaling);
            ReplaceThings(map, scaling);
            InitialSpawn(map, scaling);
        }

        public virtual float AbundanceSettingFactor()
        {
            float factor = 1f;
            if (tags == null || tags.Count <= 0)
            {
                return factor;
            }
            if (tags.Contains("crystal"))
            {
                factor *= MineralsFrameworkMain.Settings.crystalAbundanceSetting;
            }
            if (tags.Contains("boulder"))
            {
                factor *= MineralsFrameworkMain.Settings.boulderAbundanceSetting;
            }
            if (tags.Contains("small_rock"))
            {
                factor *= MineralsFrameworkMain.Settings.rocksAbundanceSetting;
            }
            if (tags.Contains("wall") && MineralsFrameworkMain.Settings.replaceWallsSetting == false)
            {
                factor = 0f;
            }
            return factor;
        }

        public virtual float DiversitySettingFactor()
        {
            float factor = 1f;
            if (tags == null || tags.Count <= 0)
            {
                return factor;
            }
            if (tags.Contains("crystal"))
            {
                factor *= MineralsFrameworkMain.Settings.crystalDiversitySetting;
            }
            return factor;
        }

        // The probablility of spawning at each point when a map is created
        public virtual float MapSpawnProbFactor(Map map)
        {
            float output = 1f;

            // Base value determined by world tile location
            Rand.PushState();
            Rand.Seed = map.GetHashCode();
            output *= Rand.Range(minClusterProbability, maxClusterProbability);
            Rand.PopState();

            // Apply distance to settlements factor
            output *= SettlementDistProbFactor(map);

            // Apply habitability factor if it is a valuable mineral
            if (otherSettlementMiningRadius > 3f)
            {
                output *= MapHabitabilitySpawnFactor(map);
            }

            return output;
        }

        // How spawning is effected by the habitability of the world location
        public virtual float MapHabitabilitySpawnFactor(Map map)
        {
            // Return max value for maps without world tiles (underground, asteroids etc)
            if (!map.Tile.Valid)
            {
                return 3f;
            }

            float output = 0.5f;

            // Value determined by map temperature
            float temp = map.mapTemperature.OutdoorTemp;
            temp = map.Tile.Tile.temperature; // Now safe since we checked Valid
            float diffFromIdeal = Math.Abs(temp - 15f);
            if (diffFromIdeal > 10f)
            {
                output += (diffFromIdeal - 10f) / 20f;
            }

            // Apply biome effects
            if (map.Biomes.Any(b => b.isExtremeBiome))
            {
                output += 0.5f;
            }

            // Never more than triple spawn rate
            if (output > 3)
            {
                output = 3f;
            }

            if (MineralsFrameworkMain.Settings.debugModeEnabled)
            {
                //Log.Message("Minerals: tileHabitabilitySpawnFactor: " + defName + ": " + output);
            }

            return output;
        }

        // How much the probablility of spawning reduces based on distance to nearest settlement 
        public virtual float SettlementDistProbFactor(Map map)
        {
            // Skip calculation for maps without world tiles (underground, asteroids etc)
            if (!map.Tile.Valid)
            {
                return 1f;
            }

            float output = 1f;
            if (otherSettlementMiningRadius > 0)
            {
                foreach (Settlement s in Find.WorldObjects.Settlements)
                {
                    float travelDist = Find.World.grid.TraversalDistanceBetween(map.Tile, s.Tile, false, (int)otherSettlementMiningRadius * 2);
                    if ((!s.Faction.IsPlayer) && travelDist < otherSettlementMiningRadius)
                    {
                        //Log.Message("Minerals: settlementDistProbFactor: " + defName + ": travelDist / otherSettlementMiningRadius:" + travelDist / otherSettlementMiningRadius);
                        output *= travelDist / otherSettlementMiningRadius;
                    }
                }
            }

            if (MineralsFrameworkMain.Settings.debugModeEnabled) { 
                //Log.Message("Minerals: settlementDistProbFactor: " + defName + ": " + output);
            }
            return output;
        }

        public virtual void SpawnInitialCluster(Map map, IntVec3 position, float size, int count)
        {
            SpawnCluster(map, position, size, count);
        }


        public virtual float NearbyThingFactor(Map map, IntVec3 position, List<NearbyThingEffect> effects)
        {
            if (effects == null || effects.Count == 0)
            {
                return 1f;
            }

            // Sort effects by radius ascending, then prioritize effects with 0 factors
            var sortedEffects = effects
                .OrderBy(e => e.radius)
                .ThenByDescending(e => (e.foundFactor == 0 || e.notFoundFactor == 0) ? 1 : 0)
                .ToList();

            float factor = 1f;
            
            foreach (var effect in sortedEffects)
            {
                float effectValue = 1f;

                if (effect.scaleByDistance && effect.scaleByArea)
                {
                    float foundScore = 0f;
                    float notFoundScore = 0f;
                    for (int x = -Mathf.FloorToInt(effect.radius); x <= Mathf.CeilToInt(effect.radius); x++)
                    {
                        for (int z = -Mathf.FloorToInt(effect.radius); z <= Mathf.CeilToInt(effect.radius); z++)
                        {
                            IntVec3 c = position + new IntVec3(x, 0, z);
                            if (c.InBounds(map))
                            {
                                float distance = position.DistanceTo(c);
                                if (distance <= effect.radius && PosHasThing(map, c, effect.defNames))
                                {
                                    foundScore = foundScore + effect.radius - distance;
                                }
                                else
                                {
                                    notFoundScore = notFoundScore + effect.radius - (x + z) / 2;
                                }
                            }
                        }
                    }
                    effectValue = Mathf.Lerp(
                        effect.notFoundFactor, effect.foundFactor,
                        (float)Math.Pow(foundScore / (foundScore + notFoundScore), effect.scaleFalloff)
                    );
                }
                else if (effect.scaleByArea)
                {
                    int count = 0;
                    int total = 0;
                    for (int x = -Mathf.FloorToInt(effect.radius); x <= Mathf.CeilToInt(effect.radius); x++)
                    {
                        for (int z = -Mathf.FloorToInt(effect.radius); z <= Mathf.CeilToInt(effect.radius); z++)
                        {
                            IntVec3 c = position + new IntVec3(x, 0, z);
                            if (c.InBounds(map))
                            {
                                float distance = position.DistanceTo(c);
                                if (distance <= effect.radius && PosHasThing(map, c, effect.defNames))
                                {
                                    count++;
                                }
                                else
                                {
                                    total++;
                                }
                            }
                        }
                    }
                    effectValue = Mathf.Lerp(
                        effect.notFoundFactor, effect.foundFactor,
                        (float)Math.Pow((float)count / (float)total, effect.scaleFalloff)
                    );
                }
                else 
                {
                    float minDistance = 999f;
                    bool found = false;
                    for (int xOffset = -(int)Math.Ceiling(effect.radius); xOffset <= (int)Math.Ceiling(effect.radius); xOffset++)
                    {
                        if (found)
                        {
                            break;
                        }
                        for (int zOffset = -(int)Math.Ceiling(effect.radius); zOffset <= (int)Math.Ceiling(effect.radius); zOffset++)
                        {
                            IntVec3 c = position + new IntVec3(xOffset, 0, zOffset);
                            if (c.InBounds(map))
                            {
                                float distance = position.DistanceTo(c);
                                if (distance <= effect.radius && PosHasThing(map, c, effect.defNames))
                                {
                                    minDistance = distance;
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (!found) {
                        effectValue = effect.notFoundFactor;
                    } else if (effect.scaleByDistance)
                    {
                        effectValue = Mathf.Lerp(
                            effect.foundFactor, effect.notFoundFactor,
                            (float)Math.Pow(minDistance / effect.radius, effect.scaleFalloff)
                        );
                    } else
                    {
                        effectValue = PosHasThing(map, position, effect.defNames) ? effect.foundFactor : effect.notFoundFactor;
                    }
                }

                factor *= effectValue;
                
                if (factor <= 0f)
                {
                    break;
                }
            }

            return factor;
        }

        public virtual void InitialSpawn(Map map, float abundScaling = 1f, float sizeScaling = 1f)
        {

            // Check that it is a valid biome
            if (! CanSpawnInBiome(map))
            {
                return;
            }

            // Select probability of spawing for this map
            float spawnProbability = MapSpawnProbFactor(map) * abundScaling * AbundanceSettingFactor();

            // Inferr size scaling factor based on abundance
            if (sizeScaledByAbundance)
            {
                sizeScaling *= spawnProbability / maxClusterProbability;
            }
            if (sizeScaling < 0.2f)
            {
                sizeScaling = 0.2f;
            }
            if (sizeScaling > 1.2f)
            {
                sizeScaling = 1.2f;
            }

            // Find spots to spawn it
            if (Rand.Range(0f, 1f) <= perMapProbability * DiversitySettingFactor() && spawnProbability > 0)
            {
                if (MineralsFrameworkMain.Settings.debugModeEnabled)
                {
                    Log.Message("MineralsFramework: " + defName + " will be spawned at a probability of " + spawnProbability);
                }
                IEnumerable<IntVec3> allCells = map.AllCells.InRandomOrder(null);
                foreach (IntVec3 position in allCells)
                {
                    if (! CanSpawnAt(map, position, true))
                    {
                        continue;
                    }
                    float positionProbFactor = NearbyThingFactor(map, position, nearbyThingAbundEffects) * spawnProbability;
                    if (Rand.Range(0f, 1f) < positionProbFactor)
                    {
                        float positionSizeFactor = NearbyThingFactor(map, position, nearbyThingSizeEffects) * sizeScaling;
                        if (positionSizeFactor > 0)
                        {
                            float spawnedSize = Rand.Range(initialSizeMin, initialSizeMax) * positionSizeFactor;
                            int spawnedCount = Rand.Range(minClusterSize, maxClusterSize);
                            SpawnInitialCluster(map, position, spawnedSize, spawnedCount);
                        }
                    }
                }
            }
        }

        public virtual bool AllowReplaceSetting()
        {
            bool output = true;
            if (replaceAll == false)
            {
                output = false;
            }
            if (tags.Contains("wall") && MineralsFrameworkMain.Settings.replaceWallsSetting == false)
            {
                output = false;
            }
            if (tags.Contains("chunk_replacer") && MineralsFrameworkMain.Settings.replaceChunksSetting == false)
            {
                output = false;
            }
            return output;
        }


        public virtual void ReplaceThings(Map map, float scaling = 1)
        {
            if (thingsToReplace == null || thingsToReplace.Count == 0 || AllowReplaceSetting() == false)
            {
                return;
            }

            // Find spots to spawn it
            map.regionAndRoomUpdater.Enabled = false;
            IEnumerable<IntVec3> allCells = map.AllCells.InRandomOrder(null);
            foreach (IntVec3 current in allCells)
            {
                if (!current.InBounds(map))
                {
                    continue;
                }

                // roof filters
                if (! IsRoofConditionOk(map, current))
                {
                    continue;
                }
                    
                Thing ToReplace = ThingToReplaceAtPos(map, current);
                if (ToReplace != null)
                {
                    ToReplace.Destroy(DestroyMode.Vanish);
                    SpawnAt(map, current, Rand.Range(initialSizeMin, initialSizeMax));
                }
            }
            map.regionAndRoomUpdater.Enabled = true;

        }

        public virtual List<string> GetTexturePaths()
        {
            if (texturePaths == null)
            {
                InitTexturePaths();
            }
            return texturePaths;
        }

        public virtual void InitTexturePaths()
        {
            // Get paths to textures
            string textureName = System.IO.Path.GetFileName(graphicData.texPath);
            texturePaths = new List<string> { };
            snowTexturePaths = new List<string> { };
            List<string> versions = new List<string> { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L" };
            foreach (string letter in versions)
            {
                string a_path = graphicData.texPath + "/" + textureName + letter;
                if (ContentFinder<Texture2D>.Get(a_path, false) != null)
                {
                    texturePaths.Add(a_path);
                    string snow_path = a_path + "_s";
                    if (ContentFinder<Texture2D>.Get(snow_path, false) != null)
                    {
                        hasSnowyTextures = true;
                        snowTexturePaths.Add(snow_path);
                    }
                }
            }

            // Check that there are enough snowy textures
            if (texturePaths.Count > 0 && snowTexturePaths.Count > 0 && texturePaths.Count != snowTexturePaths.Count)
            {
                Log.Warning("Minerals: Not an equal number of snowy and non-snowy textures for '" + graphicData.texPath + "'");
                hasSnowyTextures = false;
            }
            
        }

    }

    public class DropInfo
    {
        public float Amount { get; set; }
        public string Ooutput { get; set; }
    }
        
}
