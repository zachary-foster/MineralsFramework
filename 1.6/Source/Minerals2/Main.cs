using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using Verse;

namespace Minerals2
{
    class Minerals2Main : Mod
    {
        public static Minerals2Settings Settings;

        public Minerals2Main(ModContentPack content) : base(content)
        {
            Settings = GetSettings<Minerals2Settings>();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        public override string SettingsCategory()
        {
            return "Minerals2";
        }
    }

}
