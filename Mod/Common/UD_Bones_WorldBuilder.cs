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
            UD_Bones_WorldBuilder.Builder ??= Builder;
            base.OnAfterBuild(Builder);
        }

        public override void OnAfterMutableInit(JoppaWorldBuilder Builder)
        {
            UD_Bones_WorldBuilder.Builder ??= Builder;
            Builder?.BuildStep("Diagnosing Moon King Fever", DiagnoseMoonKingFever);
            base.OnAfterMutableInit(Builder);
        }

        public void DiagnoseMoonKingFever(string WorldID)
        {
            if (WorldID != "JoppaWorld")
                return;

            BonesManager.MutableLocations = AllMutableLocations();
        }
        
        public class SerializeEachLocationVerbose : SerializeEachLocation
        {
            public string Name;
            public override void WriteEach(SerializationWriter Writer, Location2D Element)
            {
                //Utils.Log($"{nameof(SerializeEachLocationVerbose)}.{nameof(WriteEach)}({Name}: {Element})");
                Writer.Write(Element);
            }

            public override Location2D ReadEach(SerializationReader Reader)
            {
                var value = Reader.ReadLocation2D();
                Utils.Log($"{nameof(SerializeEachLocationVerbose)}.{nameof(ReadEach)}({Name}: {value})");
                return value;
            }
        }

        public SerializeableSet<Location2D> AllMutableLocations()
        {
            var mutableLocations = new SerializeableSet<Location2D>(new SerializeEachLocationVerbose() { Name = nameof(AllMutableLocations) });

            foreach (var parasangs in (Builder?.worldInfo?.terrainLocations?.Values).IteratorSafe())
                foreach (var parasang in parasangs.IteratorSafe())
                    foreach (var location in parasang.YieldParasangZoneLocations().IteratorSafe())
                        if (Builder.mutableMap.GetMutable(location) > 0)
                            mutableLocations.Add(location);

            return mutableLocations;
        }
    }
}
