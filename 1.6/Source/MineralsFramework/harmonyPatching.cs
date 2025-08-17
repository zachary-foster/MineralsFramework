using HarmonyLib;
using RimWorld;      // RimWorld specific functions 
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;   // Always needed
using Verse;         // RimWorld universal objects 

namespace MineralsFramework
{

    [HarmonyPatch(typeof(World))]
    [HarmonyPatch("NaturalRockTypesIn")]
    public static class Patch_World_NaturalRockTypesIn
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Target method: Rand.RangeInclusive(int, int)
            MethodInfo rangeInclusiveMethod = AccessTools.Method(typeof(Rand), "RangeInclusive", new[] { typeof(int), typeof(int) });
            // Static field: MineralsFrameworkMain.Settings
            FieldInfo settingsField = AccessTools.Field(typeof(MineralsFrameworkMain), "Settings");
            // Instance field: MineralsFrameworkSettings.terrainCountRangeSetting
            FieldInfo terrainCountRangeField = AccessTools.Field(typeof(MineralsFrameworkSettings), "terrainCountRangeSetting");
            // Struct fields: IntRange.min and IntRange.max
            FieldInfo minField = AccessTools.Field(typeof(IntRange), "min");
            FieldInfo maxField = AccessTools.Field(typeof(IntRange), "max");

            var codes = new List<CodeInstruction>(instructions);
            bool patched = false;

            for (int i = 0; i < codes.Count; i++)
            {
                if (!patched && i < codes.Count - 2)
                {
                    // Check for constant '2' followed by constant '3'
                    if (IsLdcI4(codes[i], 2) && IsLdcI4(codes[i + 1], 3))
                    {
                        // Verify next instruction is the target method call
                        if (codes[i + 2].opcode == OpCodes.Call && codes[i + 2].operand == rangeInclusiveMethod)
                        {
                            // Preserve labels and jump targets
                            var labels = codes[i].labels;
                            codes[i].labels = new List<Label>();

                            // Replace constant '2' load with settings access
                            codes[i] = new CodeInstruction(OpCodes.Ldsfld, settingsField) { labels = labels };
                            codes[i + 1] = new CodeInstruction(OpCodes.Ldfld, terrainCountRangeField);
                            codes.Insert(i + 2, new CodeInstruction(OpCodes.Ldfld, minField));
                            codes.Insert(i + 3, new CodeInstruction(OpCodes.Ldsfld, settingsField));
                            codes.Insert(i + 4, new CodeInstruction(OpCodes.Ldfld, terrainCountRangeField));
                            codes.Insert(i + 5, new CodeInstruction(OpCodes.Ldfld, maxField));

                            patched = true;
                            i += 5; // Skip added instructions
                        }
                    }
                }
            }

            if (!patched)
                Log.Error("[MineralsFramework] Failed to patch World.NaturalRockTypesIn. Custom terrain counts will not work.");

            return codes;
        }

        // Helper to check if instruction loads a specific integer constant
        private static bool IsLdcI4(CodeInstruction instruction, int value)
        {
            // Check optimized opcodes for small integers
            switch (value)
            {
                case 0: return instruction.opcode == OpCodes.Ldc_I4_0;
                case 1: return instruction.opcode == OpCodes.Ldc_I4_1;
                case 2: return instruction.opcode == OpCodes.Ldc_I4_2;
                case 3: return instruction.opcode == OpCodes.Ldc_I4_3;
                case 4: return instruction.opcode == OpCodes.Ldc_I4_4;
                case 5: return instruction.opcode == OpCodes.Ldc_I4_5;
                case 6: return instruction.opcode == OpCodes.Ldc_I4_6;
                case 7: return instruction.opcode == OpCodes.Ldc_I4_7;
                case 8: return instruction.opcode == OpCodes.Ldc_I4_8;
                default:
                    // Check generic ldc.i4 with operand
                    return instruction.opcode == OpCodes.Ldc_I4 &&
                           instruction.operand is int operandValue &&
                           operandValue == value;
            }
        }
    }

    [StaticConstructorOnStartup]
    static class HarmonyPatches
    {
        // this static constructor runs to create a HarmonyInstance and install a patch.
        static HarmonyPatches()
        {
            Harmony harmony = new Harmony("com.zacharyfoster.mineralsrock");

            // Spawn rocks on map generation
            MethodInfo targetmethod = AccessTools.Method(typeof(GenStep_RockChunks), "Generate");
            HarmonyMethod postfixmethod = new HarmonyMethod(typeof(HarmonyPatches).GetMethod("initNewMapRocks"));
            harmony.Patch(targetmethod, null, postfixmethod);

            // Spawn ice after plants
            MethodInfo icetargetmethod = AccessTools.Method(typeof(GenStep_Plants), "Generate");
            HarmonyMethod icepostfixmethod = new HarmonyMethod(typeof(HarmonyPatches).GetMethod("initNewMapIce"));
            harmony.Patch(icetargetmethod, null, icepostfixmethod);


            harmony.PatchAll();


        }

        public static void initNewMapRocks(GenStep_RockChunks __instance, Map map)
        {
            mapBuilder.initRocks(map);
        }

        public static void initNewMapIce(GenStep_RockChunks __instance, Map map)
        {
            mapBuilder.initIce(map);
        }
    }
}
