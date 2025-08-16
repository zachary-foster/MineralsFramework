using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace MineralsFramework
{
    public class MineralsFrameworkMain : Mod
    {
        public static MineralsFrameworkSettings Settings;

        public MineralsFrameworkMain(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MineralsFrameworkSettings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "MineralsFramework";
        }
    }

}
