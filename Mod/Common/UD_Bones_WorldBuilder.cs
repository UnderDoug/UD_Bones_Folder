using System;
using System.Collections.Generic;

using Genkit;

using Qud.API;

using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World.Parts;
using XRL.World.ZoneBuilders;

using Bones.Mod;
using System.Linq;

namespace XRL.World.WorldBuilders
{
    [HasGameBasedStaticCache]
    [HasWishCommand]
    [JoppaWorldBuilderExtension]
    public class UD_Bones_WorldBuilder : IJoppaWorldBuilderExtension
    {
        public static BonesManager BonesManager => BonesManager.System;

        public JoppaWorldBuilder Builder;

        [GameBasedStaticCache(CreateInstance = false)]
        public static string BoneZoneID = null;

        public override void OnAfterBuild(JoppaWorldBuilder Builder)
        {
            MetricsManager.rngCheckpoint("UD_FleshGolems_MadMonger_Lair_Start");
            this.Builder = Builder;
            Builder.BuildStep("Exhuming Moon Kings", ExhumeMoonKing);
            MetricsManager.rngCheckpoint("UD_FleshGolems_MadMonger_Lair_Finish");
        }

        public void ExhumeMoonKing(string WorldID)
        {
            if (WorldID != "JoppaWorld")
                return;

            WorldCreationProgress.StepProgress("Exhuming Moon King...");

            if (BonesManager == null)
                return;

            if (BonesManager.GetSavedBonesInfo() is not IEnumerable<SaveGameInfo> savedBonesInfos
                || savedBonesInfos.IsNullOrEmpty())
                return;

            var saveWeights = new Dictionary<SaveGameInfo, int?>();
            foreach (var savedBonesInfo in savedBonesInfos)
            {
                if (savedBonesInfo.ModsEnabled.IsNullOrEmpty())
                {
                    saveWeights[savedBonesInfo] = null;
                    continue;
                }
                int saveWeight = 100;
                foreach (var savedBonesMod in savedBonesInfo.ModsEnabled)
                {
                    if (!BonesManager.RunningMods.Contains(savedBonesMod))
                        saveWeight -= 10;
                    else
                        saveWeight += 10;
                }
                foreach (var runningMod in BonesManager.RunningMods)
                {
                    if (!savedBonesInfo.ModsEnabled.Contains(runningMod))
                        saveWeight -= 10;
                }
                saveWeights[savedBonesInfo] = Math.Max(1, saveWeight);
            }

            int maxWeight = saveWeights.Aggregate(0, (a, n) => n.Value.GetValueOrDefault() > a ? n.Value.GetValueOrDefault() : a);
            var bonesInfoBag = new BallBag<SaveGameInfo>();

            foreach ((var savedBones, var bonesWeight) in saveWeights)
                bonesInfoBag.Add(savedBones, bonesWeight ?? maxWeight);

            if (bonesInfoBag.PluckOne() is not SaveGameInfo pluckedBonesInfo)
                return;

            if (BonesManager.ExhumeMoonKing(pluckedBonesInfo) is not BonesData bonesData)
                return;

            Location2D bonesParasang = Location2D.Get(bonesData.BonesZone.wX, bonesData.BonesZone.wY);
            Location2D bonesZone = Location2D.Get(bonesParasang.X * 3 + bonesData.BonesZone.X, bonesParasang.Y * 3 + +bonesData.BonesZone.Y);
            BoneZoneID = bonesData.BonesZone.ZoneID;

            The.ZoneManager.ClearZoneBuilders(BoneZoneID);
            The.ZoneManager.SetZoneProperty(BoneZoneID, "SkipTerrainBuilders", true);
            The.ZoneManager.AddZoneBuilder(BoneZoneID, 4500, nameof(BoneZoneBuilder), nameof(BoneZoneBuilder.BonesData), bonesData);
        }
    }
}
