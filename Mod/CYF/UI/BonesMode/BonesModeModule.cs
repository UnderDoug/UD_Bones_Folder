using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Genkit;

using Qud.API;

using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.CharacterBuilds.Qud.UI;
using XRL.Collections;
using XRL.Rules;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.AI.Pathfinding;
using XRL.World.Anatomy;
using XRL.World.Capabilities;
using XRL.World.Parts;
using XRL.World.Parts.Skill;
using XRL.World.Skills;
using XRL.World.Tinkering;
using XRL.World.WorldBuilders;

using Event = XRL.World.Event;

using Kernelmethod.ChooseYourFighter;
using Kernelmethod.ChooseYourFighter.Patches;

namespace UD_Bones_Folder.Mod.UI
{
    [HasBonesModeModuleAction]
    public partial class BonesModeModule : QudEmbarkBuilderModule<BonesModeModuleData>
    {
        [BonesModeModuleAction(TargetWindow = typeof(QudCustomizeCharacterModuleWindow))]
        public static void RandomizeTileIfChooseYourFighter(QudCustomizeCharacterModuleWindow Window, QudCustomizeCharacterModule Module)
        {
            Utils.Log($"{nameof(BonesModeModule)}.{nameof(RandomizeTileIfChooseYourFighter)} called.");
            try
            {
                if (SeededOddsIn10000(nameof(RandomizeTileIfChooseYourFighter), 5000))
                {
                    if (TileFactory.Models is IEnumerable<PlayerModel> playerModels
                        && (Window.module.builder ?? Module.builder) is EmbarkBuilder builder)
                    {
                        var tile = builder.fireBootEvent<string>(QudGameBootModule.BOOTEVENT_BOOTPLAYERTILE, null);
                        var fgColor = builder.fireBootEvent<string>(QudGameBootModule.BOOTEVENT_BOOTPLAYERTILEFOREGROUND, null) ?? "&y";
                        var bgColor = builder.fireBootEvent<string>(QudGameBootModule.BOOTEVENT_BOOTPLAYERTILEBACKGROUND, null);
                        var detailColor = builder.fireBootEvent<string>(QudGameBootModule.BOOTEVENT_BOOTPLAYERTILEDETAIL, null);

                        PlayerModel defaultModel = null;
                        if (tile != null)
                        {
                            defaultModel = new PlayerModel();
                            defaultModel.Tile = tile;
                            defaultModel.Foreground = fgColor;
                            defaultModel.Background = bgColor;
                            defaultModel.DetailColor = detailColor;
                        }

                        var modelData = QudCustomizeCharacterModuleWindow_SelectMenuOptionPatch.ModelData;

                        modelData.defaultModel = defaultModel;

                        if (SeededOddsIn10000(nameof(RandomizeTileIfChooseYourFighter), Odds: 1, Iteration: 1)
                            && GameObjectFactory.Factory
                                .BlueprintList
                                .Where(b => b.GetRenderable()?.Tile != null) is IEnumerable<GameObjectBlueprint> renderableBlueprints
                            && renderableBlueprints
                                .IteratorSafe()
                                .GetRandomElement(SeededGenerator(nameof(RandomizeTileIfChooseYourFighter))) is GameObjectBlueprint blueprint)
                        {
                            modelData.model = new PlayerModel(blueprint)
                            {
                                Id = "BLUEPRINT:" + blueprint.Name
                            };
                        }
                        else
                        {
                            modelData.model = playerModels
                                .Where(m => m.Category == ModelType.Preset || m.Category == ModelType.Expansion)
                                .IteratorSafe()
                                .GetRandomElement(SeededGenerator(nameof(RandomizeTileIfChooseYourFighter)));
                        }
                        Utils.Log($"{1.Indent()}{nameof(BonesModeModule)}.{nameof(RandomizeTileIfChooseYourFighter)} selected tile: {modelData?.model?.Id}");
                        return;
                    }
                }
                Utils.Log($"{1.Indent()}{nameof(BonesModeModule)}.{nameof(RandomizeTileIfChooseYourFighter)} did not select a tile");
            }
            catch (Exception x)
            {
                Utils.Error($"Failed to randomize player tile", x);
            }
        }
    }
}