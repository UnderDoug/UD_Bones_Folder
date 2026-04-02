using System;
using System.Collections.Generic;
using System.Linq;

using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World.ZoneBuilders;

using UD_Bones_Folder.Mod;

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

            if (BonesManager.GetAvailableSavedBonesInfo() is not IEnumerable<SaveBonesInfo> savedBonesInfos
                || savedBonesInfos.IsNullOrEmpty())
                return;

            var saveWeights = new Dictionary<SaveBonesInfo, int?>();
            foreach (var savedBonesInfo in savedBonesInfos)
            {
                if (savedBonesInfo.ModsEnabled.IsNullOrEmpty())
                {
                    saveWeights[savedBonesInfo] = null;
                    continue;
                }
                int saveWeight = 50;
                saveWeight += savedBonesInfo.ModsDiffer.EnabledWhereBonesDisabled * -1;
                saveWeight += savedBonesInfo.ModsDiffer.DisabledWhereBonesEnabled * -2;
                saveWeight = Math.Max(1, saveWeight);
                foreach (var savedBonesMod in savedBonesInfo.ModsEnabled)
                    if (!BonesManager.RunningMods.Contains(savedBonesMod))
                        saveWeight += 1;

                saveWeight += savedBonesInfo.ModsDiffer.UnavailableWhereBonesEnabled * -4;
                saveWeights[savedBonesInfo] = Math.Max(1, saveWeight);
            }

            int maxWeight = saveWeights.Aggregate(
                seed: 0,
                func: delegate (int accumulator, KeyValuePair<SaveBonesInfo, int?> next)
                {
                    if (next.Value is int value
                        && value > accumulator)
                        return value;
                    return accumulator;
                });

            var bonesInfoBag = new BallBag<SaveBonesInfo>();

            foreach ((var savedBones, var bonesWeight) in saveWeights)
                bonesInfoBag.Add(savedBones, bonesWeight ?? maxWeight);

            if (bonesInfoBag.PluckOne() is not SaveBonesInfo pluckedBonesInfo)
                return;

            BoneZoneID = pluckedBonesInfo.ZoneID;
            if (pluckedBonesInfo.ZoneRequest is ZoneRequest pluckedZR
                && pluckedZR.Z > 20)
            {
                BoneZoneID = ZoneID.Assemble(
                    World: pluckedZR.WorldID,
                    ParasangX: pluckedZR.WorldX,
                    ParasangY: pluckedZR.WorldY,
                    ZoneX: pluckedZR.X,
                    ZoneY: pluckedZR.Y,
                    ZoneZ: Stat.Random(15, 20));
            }

            SaveBonesInfo.SetPending(pluckedBonesInfo, The.Game?.GameID).Wait();

            // The.ZoneManager.ClearZoneBuilders(BoneZoneID);
            // The.ZoneManager.SetZoneProperty(BoneZoneID, "SkipTerrainBuilders", true);
            The.ZoneManager.AddZonePostBuilder(
                ZoneID: BoneZoneID,
                Class: nameof(BonesZoneBuilder),
                Key1: nameof(BonesZoneBuilder.SaveBonesInfoID), Value1: pluckedBonesInfo.ID,
                Key2: nameof(BonesZoneBuilder.ZoneID), Value2: pluckedBonesInfo.ZoneID);
        }
    }
}
