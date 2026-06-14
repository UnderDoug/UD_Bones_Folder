using System;
using System.Collections.Generic;
using System.Linq;

using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World.ZoneBuilders;

using UD_Bones_Folder.Mod;
using Options = UD_Bones_Folder.Mod.Options;
using XRL.Collections;
using ConsoleLib.Console;
using XRL.World.Effects;
using XRL.Core;
using Genkit;
using UD_Bones_Folder.Mod.Serialization;
using UD_Bones_Folder.Mod.Serialization.Delegates;

namespace XRL.World.WorldBuilders
{
    [HasGameBasedStaticCache]
    [HasWishCommand]
    [JoppaWorldBuilderExtension]
    public class UD_Bones_WorldBuilder : IJoppaWorldBuilderExtension
    {
        public static BonesManager BonesManager => BonesManager.System;

        public static JoppaWorldBuilder Builder;

        [GameBasedStaticCache(CreateInstance = false)]
        public static string BoneZoneID = null;

        public override void OnAfterBuild(JoppaWorldBuilder Builder)
        {
            //MetricsManager.rngCheckpoint("UD_Bones_WorldBuilder_Start");
            UD_Bones_WorldBuilder.Builder ??= Builder;
            Builder?.BuildStep("Exhuming Lunar Regents", ExhumeLunarRegent);
            //MetricsManager.rngCheckpoint("UD_Bones_WorldBuilder_Finish");
            base.OnAfterBuild(Builder);
        }

        public override void OnAfterMutableInit(JoppaWorldBuilder Builder)
        {
            /*if (The.ZoneManager.ZoneBuilders is Dictionary<string, ZoneBuilderCollection> zoneBuilders)
            {
                foreach ((var zoneID, var zoneBuilderCollection) in zoneBuilders)
                {

                }
            }*/

            UD_Bones_WorldBuilder.Builder ??= Builder;
            Builder?.BuildStep("Diagnosing Moon King Fever", ExhumeLunarRegent);
            base.OnAfterMutableInit(Builder);
        }

        public void ExhumeLunarRegent(string WorldID)
        {
            if (WorldID != "JoppaWorld")
                return;

            BonesManager.MutableLocations = AllMutableLocations();
        }

        public SerializeableSet<Location2D> AllMutableLocations()
        {
            var mutableLocations = new SerializeableSet<Location2D>(SerializeEachLocation.Default);

            foreach (var parasangs in (Builder?.worldInfo?.terrainLocations?.Values).IteratorSafe())
                foreach (var parasang in parasangs.IteratorSafe())
                    foreach (var location in parasang.YieldParasangZoneLocations().IteratorSafe())
                        if (Builder.mutableMap.GetMutable(location) > 0)
                            mutableLocations.Add(location);

            return mutableLocations;
        }
    }
}
