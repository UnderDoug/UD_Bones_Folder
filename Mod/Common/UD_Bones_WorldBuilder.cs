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

            if (Options.DebugEnableNoExhuming
                && !Options.DebugEnablePickingBones)
                return;

            // comment this out to re-enable this.
            if (Options.DebugEnableNoExhuming
                || !Options.DebugEnableNoExhuming)
                return;

            WorldCreationProgress.StepProgress("Exhuming Moon King...");

            if (BonesManager == null)
                return;

            if (BonesManager.GetAvailableSaveBonesInfo() is not IEnumerable<SaveBonesInfo> savedBonesInfos
                || savedBonesInfos.IsNullOrEmpty())
                return;

            SaveBonesInfo pickedBones = null;
            int pickedValue = 0;
            if (Options.DebugEnablePickingBones)
            {
                Utils.Info($"Picking bones...");

                using var bonesList = ScopeDisposedList<SaveBonesInfo>.GetFromPoolFilledWith(
                    items: savedBonesInfos.OrderBy(b => b, SaveBonesInfo.SaveBonesInfoComparerDescending));

                using var optionsList = ScopeDisposedList<string>.GetFromPool();
                using var renderList = ScopeDisposedList<BonesRender>.GetFromPool();
                using var hotkeyList = ScopeDisposedList<char>.GetFromPool();

                // Add None Please:
                optionsList.Add("none please");
                renderList.Add(new(GameObjectFactory.Factory.GetBlueprintIfExists("Lunar Face"), false));
                renderList[0].TileColor = "&K";
                renderList[0].DetailColor = 'K';
                hotkeyList.Add('n');

                // Add Roll It!:
                optionsList.Add("{{yellow|roll it!}}");
                renderList.Add(new("Abilities/sw_skill_pointed_circle.png", ColorString: "&y", TileColor: "&y", DetailColor: 'W'));
                hotkeyList.Add('r');

                int offset = optionsList.Count;

                foreach (var bones in bonesList)
                {
                    renderList.Add(bones.Render);
                    string bonesOption = bones.GetName();
                    if (bones.GetBonesJSON() is SaveBonesJSON bonesJSON)
                        bonesOption = $"{bonesOption}, Level {bonesJSON.Level}, {bonesJSON.Location} ({bonesJSON.ZoneID})";
                    optionsList.Add(bonesOption);
                    hotkeyList.Add(' ');
                }

                var icon = new BonesRender(GameObjectFactory.Factory.GetBlueprintIfExists("Lunar Face"), HFlip: false, IsMad: true);
                
                if (savedBonesInfos.Any(b => b.IsMad))
                    icon.SetTile(Const.MOON_KING_FEVER_TILE);

                string neutralRegalTitle = UD_Bones_MoonKingFever.REGAL_TITLE.Pluralize();

                var picked = Popup.PickOptionAsync(
                    Title: $"Eligible =LunarShader:{neutralRegalTitle}:*= For This Run".StartReplace().ToString(),
                    Intro: "Pick a lunar regent to exhume.",
                    Options: optionsList,
                    Icons: renderList,
                    IntroIcon: icon,
                    AllowEscape: true);

                picked.Wait();
                pickedValue = picked.Result;
                if (pickedValue >= offset)
                    pickedBones = bonesList[pickedValue - offset];
            }

            if (!Options.DebugEnablePickingBones
                || pickedValue == 1)
            {
                if (savedBonesInfos
                    .Aggregate(
                        seed: new BallBag<SaveBonesInfo>(),
                        func: delegate (BallBag<SaveBonesInfo> acc, SaveBonesInfo next)
                        {
                            if (next.GetBonesWeight() is int weight)
                                acc.Add(next, weight);
                            return acc;
                        })
                    .PluckOne() is not SaveBonesInfo pluckedBonesInfo)
                    return;

                pickedBones = pluckedBonesInfo;
            }

            if (pickedBones != null)
            {
                BoneZoneID = pickedBones.BonesSpec.ZoneID;
                if (pickedBones.ZoneRequest is ZoneRequest pluckedZR
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

                SaveBonesInfo.SetPending(pickedBones, The.Game?.GameID);

                // The.ZoneManager.ClearZoneBuilders(BoneZoneID);
                // The.ZoneManager.SetZoneProperty(BoneZoneID, "SkipTerrainBuilders", true);
                The.ZoneManager.AddZonePostBuilder(
                    ZoneID: BoneZoneID,
                    Class: nameof(BonesZoneBuilder),
                    Key1: nameof(BonesZoneBuilder.SaveBonesInfoID), Value1: pickedBones.ID,
                    Key2: nameof(BonesZoneBuilder.ZoneID), Value2: pickedBones.BonesSpec.ZoneID);

                Utils.Info($"Bones pending: {pickedBones.Name}, {pickedBones.GetBonesJSON().Location} " +
                    $"({pickedBones.BonesSpec.ZoneID}) {{{pickedBones.ID}}}");
            }
        }
    }
}
