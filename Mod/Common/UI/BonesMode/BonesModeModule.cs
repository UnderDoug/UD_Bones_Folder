using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Genkit;

using Qud.API;

using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;
using XRL.Collections;
using XRL.Rules;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Anatomy;
using XRL.World.Capabilities;
using XRL.World.Parts;
using XRL.World.Tinkering;
using XRL.World.WorldBuilders;

namespace UD_Bones_Folder.Mod.UI
{
    public class BonesModeModule : QudEmbarkBuilderModule<BonesModeModuleData>
    {
        public const string BONES_MODE = "UD_Bones_Bones";

        public static int MinimumLevelForBones => UD_Bones_BonesSaver.MinimumLevelForBones;
        public static int MaximumLevelForBonesMode => 80;

        public string Mode => builder?.GetModule<QudGamemodeModule>()?.data?.Mode;

        public override bool shouldBeEditable()
            => builder?.IsEditableGameMode() is true;

        public override bool shouldBeEnabled()
            => Mode == BONES_MODE;

        public override bool IncludeInBuildCodes()
            => false
            ;

        private static bool IsQudGamemodeModuleWindowDescriptor(EmbarkBuilderModuleWindowDescriptor Descriptor)
            => Descriptor.module is QudGamemodeModule
            ;
        public override void assembleWindowDescriptors(List<EmbarkBuilderModuleWindowDescriptor> windows)
        {
            int index = windows.FindIndex(IsQudGamemodeModuleWindowDescriptor);
            if (index < 0)
                base.assembleWindowDescriptors(windows);
            else
                windows.InsertRange(index + 1, this.windows.Values);
        }

        public override object handleBootEvent(string id, XRLGame game, EmbarkInfo info, object element = null)
        {
            if (Mode == BONES_MODE)
            {
                if (id == QudGameBootModule.BOOTEVENT_AFTERBOOTPLAYEROBJECT
                    && element is GameObject player)
                {
                    try
                    {
                        player.AwardXP(GetRandomXPForLevel(GetRandomLevel()));
                        int tier = Tier.Constrain(player.GetTier());
                        int overTier = Math.Max(0, player.GetTier() - tier);

                        if (tier == 8)
                            player.ReceivePopulation("FinalSupply");
                        else
                        if (tier >= 7)
                            player.ReceivePopulation("ReefBetaSupply");
                        else
                        if (tier >= 6)
                            player.ReceivePopulation("TombSupply");
                        else
                            player.ReceivePopulation($"Tier{tier}Wares");

                        int virtualCreditWedges = (tier * 2) + (overTier * 3);
                        Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (Start)");
                        for (int i = 0; i < overTier; i++)
                        {
                            if (SeededRandom($"{nameof(GameObject.ReceivePopulation)}:{nameof(overTier)}", 1, 10000, i) >= 7000)
                            {
                                int wares = 7;
                                if (SeededRandom($"{nameof(GameObject.ReceivePopulation)}:{nameof(overTier)}:{nameof(wares)}", 1, 10000, i) >= 8000)
                                    wares = 8;
                                player.ReceivePopulation($"Tier{Tier.Constrain(wares)}Wares");
                            }

                            for (int j = 0; j < SeededRandom(nameof(RelicGenerator), -2, 1, i); j++)
                            {
                                string relicSeed = $"{Utils.CallChain(nameof(RelicGenerator), nameof(RelicGenerator.GetPeriodFromRelicTier))}:{i}";
                                int relicTier = SeededRandom(relicSeed, 1, 5, j);
                                player.ReceiveObject(RelicGenerator.GenerateRelic(RelicGenerator.GetPeriodFromRelicTier(relicTier)));
                                player.AdjustCyberneticsLicensePointsFromWedges(
                                    Amount: virtualCreditWedges + GetNAdvantage(
                                        Method: $"{nameof(Extensions.AdjustCyberneticsLicensePointsFromWedges)}:{nameof(overTier)}:{i}",
                                        Low: 3,
                                        High: 6,
                                        N: 2,
                                        Iteration: j),
                                    Remaining: out virtualCreditWedges);
                                Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} ({nameof(overTier)} [{i},{j}])");
                            }
                            player.ReceiveObjectFromPopulation($"Cybernetics{8}");
                        }

                        player.ReceivePopulation($"Tier{tier}Wares");
                        for (int i = tier; i > 0; i--)
                        {
                            if (SeededRandom($"{nameof(GameObject.ReceivePopulation)}:{nameof(tier)}", 1, 10000, i) >= 5000)
                            {
                                int wares = i - 1;
                                if (SeededRandom($"{nameof(GameObject.ReceivePopulation)}:{nameof(tier)}:{nameof(wares)}", 1, 10000, i) >= 8000)
                                    wares = i;
                                player.ReceivePopulation($"Tier{Tier.Constrain(wares)}Wares");
                            }

                            for (int j = 0; j < SeededRandom(nameof(RelicGenerator), -2, 3, i); j++)
                            {
                                player.ReceiveObject(RelicGenerator.GenerateRelic(RelicGenerator.GetPeriodFromRelicTier(i)));
                                player.AdjustCyberneticsLicensePointsFromWedges(
                                    Amount: virtualCreditWedges + GetNAdvantage(
                                        Method: $"{nameof(Extensions.AdjustCyberneticsLicensePointsFromWedges)}:{nameof(tier)}:{i}",
                                        Low: Math.Clamp(i / 3, 1, 2),
                                        High: Math.Clamp(i / 2, 2, 6),
                                        N: 2,
                                        Iteration: j),
                                    Remaining: out virtualCreditWedges);
                                Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} ({nameof(tier)} [{i},{j}])");
                            }
                            player.ReceiveObjectFromPopulation($"Cybernetics{i}");
                            player.ReceiveObjectFromPopulation($"Cybernetics{i}");
                        }

                        if (player.Inventory.GetFirstObject(go => go.HasPart<SultanMask>()) is GameObject sultanMask)
                        {
                            sultanMask.Obliterate();
                            player.ReceiveObject("The Kesil Face");
                        }

                        for (int i = 4; i < tier; i++)
                        {
                            if (player.Inventory.GetFirstObject(go => go.GetBlueprint().InheritsFromSafe("Otherpearl")) != null)
                                break;

                            if (SeededRandom($"Otherpearl", 1, 10000, i) >= 6000
                                && player.ReceiveObject("Otherpearl"))
                                break;
                        }

                        for (int i = 4; i < tier; i++)
                        {
                            if (player.Inventory.GetFirstObject(go => go.GetBlueprint().InheritsFromSafe("Fist of the Ape God")) != null)
                                break;

                            if (SeededRandom($"Fist of the Ape God", 1, 10000, i) >= 6000
                                && player.ReceiveObject("Fist of the Ape God"))
                                break;
                        }

                        int startingMP = player.Stat("MP");
                        if (startingMP > 0)
                        {
                            int lastRemainingPoints = 0;
                            int lastPointsToSpend = 0;
                            bool stuck = false;
                            int maxAttempts = 0;
                            while (player.Stat("MP") > 0
                                && player.Stat("MP") != lastRemainingPoints
                                && !stuck
                                && ++maxAttempts < 200)
                            {
                                int pointsToSpend = SeededRandom(nameof(GameObject.RandomlySpendPoints), 1, Math.Max(1, Math.Min(player.Stat("MP"), 4)), maxAttempts);

                                if (lastRemainingPoints != player.Stat("MP"))
                                    stuck = false;
                                else
                                {
                                    stuck = true;
                                    pointsToSpend += lastPointsToSpend;
                                }

                                lastPointsToSpend = pointsToSpend;
                                lastRemainingPoints = player.Stat("MP");
                                player.RandomlySpendPoints(maxAPtospend: 0, maxSPtospend: 0, maxMPtospend: pointsToSpend);
                            }
                        }
                        player.RandomlySpendPoints();

                        if (player.IsMutant()
                            && !player.IsEsper())
                        {
                            for (int i = 0; i < player.Level; i++)
                            {
                                int iLevel = i + 1;
                                int rapidAdvancement = ((iLevel + 5) % 10 == 0) ? 3 : 0;
                                Leveler.RapidAdvancement(rapidAdvancement, player);
                            }
                        }

                        if (player.IsChimera()
                            && player.TryGetPart(out Mutations mutations))
                        {
                            int physicalMutationsCount = player.GetPhysicalMutations().Count;

                            for (int i = 0; i < physicalMutationsCount; i++)
                                if (SeededRandom(nameof(Mutations.AddChimericBodyPart), 1, 7000, i) >= 3000)
                                    mutations.AddChimericBodyPart();
                        }

                        if (player.IsTrueKin())
                        {
                            using (var creditWedges = ScopeDisposedList<CyberneticsCreditWedge>.GetFromPool())
                            {
                                player.ForeachInventoryAndEquipment(delegate (GameObject go)
                                {
                                    if (go.TryGetPart(out CyberneticsCreditWedge creditWedgePart)
                                        && creditWedgePart.Credits > 0)
                                    {
                                        virtualCreditWedges += creditWedgePart.Credits * go.Count;
                                        creditWedges.Add(creditWedgePart);
                                        Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} ({go.DebugName})");
                                    }
                                });

                                while (!creditWedges.IsNullOrEmpty()
                                    && creditWedges.TakeAt(0) is CyberneticsCreditWedge creditWedgePart)
                                    creditWedgePart.ParentObject?.Obliterate();
                            }

                            player.AdjustCyberneticsLicensePointsFromWedges(virtualCreditWedges, out virtualCreditWedges);

                            if (player.Body.LoopParts() is IEnumerable<BodyPart> loopedParts
                                && !loopedParts.IsNullOrEmpty())
                            {
                                using var bodyParts = ScopeDisposedList<BodyPart>.GetFromPoolFilledWith(loopedParts);

                                bodyParts.RemoveAll(bp => !bp.CanReceiveCyberneticImplant());

                                int totalParts = bodyParts.Count;
                                int implantLoops = 0;
                                bodyParts.ShuffleInPlace(SeededGenerator(Utils.CallChain(nameof(BodyPart), nameof(BodyPart.Implant)), implantLoops++));

                                bool matchesSpec(GameObjectBlueprint Model, BodyPart NextPart, int AvailableLP)
                                    => Model.TryGetPartParameter(nameof(CyberneticsBaseItem), nameof(CyberneticsBaseItem.Slots), out string slots)
                                    && slots.CachedCommaExpansion().Any(s => s.EqualsNoCase(NextPart.Type))
                                    && Model.TryGetPartParameter(nameof(CyberneticsBaseItem), nameof(CyberneticsBaseItem.Cost), out int cost)
                                    && cost <= AvailableLP
                                    //&& int.TryParse(Model.GetPropertyOrTag("Tier"), out var itemTier)
                                    //&& itemTier <= (tier + 2)
                                    ;

                                int totalLP = player.GetCyberneticsLicensePoints();
                                int usedLP = player.GetUsedCyberneticsLicensePoints();
                                int availableLP = totalLP - usedLP;
                                int attempts = 0;
                                while (attempts++ < 25
                                    && availableLP > 0
                                    && !bodyParts.IsNullOrEmpty()
                                    && bodyParts.TakeAt(0) is BodyPart nextPart)
                                {
                                    if (EncountersAPI.GetAnItem(model => matchesSpec(model, nextPart, availableLP)) is GameObject cybernetic
                                        || (cybernetic = player.GetInventoryAndEquipment(go => matchesSpec(go.GetBlueprint(), nextPart, availableLP)).GetRandomElement()) != null)
                                    {
                                        cybernetic.MakeUnderstood();
                                        nextPart.Implant(cybernetic);
                                        if (nextPart.Cybernetics != cybernetic)
                                        {
                                            bodyParts.Add(nextPart);
                                            player.ReceiveObject(cybernetic);
                                        }
                                    }
                                    else
                                        bodyParts.Add(nextPart);

                                    bodyParts.ShuffleInPlace(SeededGenerator(Utils.CallChain(nameof(BodyPart), nameof(BodyPart.Implant)), implantLoops++));
                                    usedLP = player.GetUsedCyberneticsLicensePoints();
                                    availableLP = totalLP - usedLP;
                                }
                            }
                        }

                        while (virtualCreditWedges >= 3)
                        {
                            if (player.FindObjectInInventory("CyberneticsCreditWedge3") is not GameObject creditWedge3)
                                player.ReceiveObject("CyberneticsCreditWedge3");
                            else
                                creditWedge3.Count++;

                            virtualCreditWedges -= 3;
                            // Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (CyberneticsCreditWedge3)");
                        }
                        while (virtualCreditWedges >= 2)
                        {
                            if (player.FindObjectInInventory("CyberneticsCreditWedge2") is not GameObject creditWedge2)
                                player.ReceiveObject("CyberneticsCreditWedge2");
                            else
                                creditWedge2.Count++;

                            virtualCreditWedges -= 2;
                            // Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (CyberneticsCreditWedge2)");
                        }
                        while (virtualCreditWedges >= 1)
                        {
                            if (player.FindObjectInInventory("CyberneticsCreditWedge") is not GameObject creditWedge1)
                                player.ReceiveObject("CyberneticsCreditWedge");
                            else
                                creditWedge1.Count++;

                            virtualCreditWedges -= 1;
                            // Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (CyberneticsCreditWedge1)");
                        }

                        static bool isSphereOfNegWeight(GameObject go)
                            => go.Blueprint == "Small Sphere of Negative Weight"
                            ;

                        static bool isSlimmableObject(GameObject go)
                            => ItemModding.ModificationApplicable(nameof(ModWillowy), go)
                            && !isSphereOfNegWeight(go)
                            ;

                        static bool isEmptyWaterSkin(GameObject go)
                            => (go.GetBlueprint().InheritsFromSafe("Waterskin")
                                || go.GetBlueprint().InheritsFromSafe("Canteen"))
                            && go.TryGetPart(out LiquidVolume liquidVolume)
                            && liquidVolume.Volume <= 0
                            ;

                        static int weightComparison(GameObject x, GameObject y)
                        {
                            if (x == null
                                || y == null)
                                return (x == null).CompareTo(y == null) * -1;

                            return x.GetWeight().CompareTo(y.GetWeight()) * -1;
                        }

                        int ticker = 0;
                        while (player.GetFreeDrams() > (tier + overTier) * 24)
                            player.UseDrams(SeededRandom(nameof(GameObject.GetFreeDrams), 1, 8, ticker++));

                        while (player.Inventory.GetObjectCount(isEmptyWaterSkin) > 7)
                            player.Inventory.GetFirstObject(isEmptyWaterSkin).Obliterate();
                        
                        foreach (var item in player.Inventory?.Objects ?? Enumerable.Empty<GameObject>())
                            if (!item.Understood())
                                item.MakeUnderstood(ShowMessage: false);

                        int glimmer = player.GetPsychicGlimmer();
                        if (glimmer > 20)
                        {
                            using (var shuffledInventory = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(player.Inventory.Objects))
                            {
                                shuffledInventory.ShuffleInPlace(SeededGenerator(nameof(GameObject.GetPsychicGlimmer), glimmer));
                                shuffledInventory.RemoveAll(go => go.HasPart<ModExtradimensional>());

                                while (glimmer > 0
                                    && !shuffledInventory.IsNullOrEmpty())
                                {
                                    if (shuffledInventory[0].SplitFromStack() is not GameObject itemToMod)
                                        itemToMod = shuffledInventory[0];

                                    int reduction = Math.Max(10, itemToMod.GetComplexity() * Math.Max(1, itemToMod.GetExamineDifficulty()));
                                    ItemModding.ApplyModification(itemToMod, new ModExtradimensional());
                                    itemToMod.SetStringProperty("NeverStack", "1");
                                    if (player.ReceiveObject(itemToMod))
                                        glimmer -= reduction;

                                    shuffledInventory.Clear();
                                    shuffledInventory.AddRange(player.Inventory.Objects);

                                    shuffledInventory.RemoveAll(go => go.HasPart<ModExtradimensional>());
                                    shuffledInventory.ShuffleInPlace(SeededGenerator(nameof(GameObject.GetPsychicGlimmer), glimmer));
                                }
                            }
                        }

                        player.Brain?.PerformReequip(Silent: true, Initial: true);

                        GameObject sphereOfNegativeWeight = player.Inventory.GetFirstObject(isSphereOfNegWeight);
                        int carriedWeight = player.GetCarriedWeight();
                        int maxCarryCapacity = player.GetMaxCarriedWeight();
                        ticker = 0;
                        int failedAttempts = 0;
                        while (carriedWeight > maxCarryCapacity
                            && failedAttempts < 25)
                        {
                            GameObject slimmableItem = null;
                            if (player.Inventory?.GetObjects(isSlimmableObject) is List<GameObject> slimmableObjects)
                            {
                                slimmableObjects.StableSortInPlace(weightComparison);
                                slimmableItem = slimmableObjects.FirstOrDefault();
                            }
                            if (slimmableItem == null
                                || SeededRandom(nameof(GameObject.GetCarriedWeight), 1, 10000, ticker++) >= 2000)
                            {
                                if (sphereOfNegativeWeight == null
                                    && player.ReceiveObject("Small Sphere of Negative Weight"))
                                {
                                    sphereOfNegativeWeight = player.Inventory.GetFirstObject(isSphereOfNegWeight);
                                    sphereOfNegativeWeight.MakeUnderstood();
                                }
                                else
                                if (sphereOfNegativeWeight != null)
                                    sphereOfNegativeWeight.Count++;
                            }
                            else
                                slimmableItem.ApplyModification(new ModWillowy());

                            player.FlushWeightCaches();

                            if (carriedWeight == player.GetCarriedWeight())
                                failedAttempts++;

                            carriedWeight = player.GetCarriedWeight();
                            maxCarryCapacity = player.GetMaxCarriedWeight();
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Giving player cool stuff", x);
                    }
                }
                else
                if (id == QudGameBootModule.BOOTEVENT_GAMESTARTING
                    && UD_Bones_WorldBuilder.Builder is JoppaWorldBuilder worldBuilder)
                {
                    player = The.Player;
                    try
                    {
                        int tier = Tier.Constrain(player.GetTier());
                        int overTier = Math.Max(0, player.GetTier() - tier);

                        int maxTier = tier;
                        Location2D randomParasang = null;
                        int parasangTries = 0;
                        while (randomParasang == null
                            && parasangTries++ < 25
                            && maxTier > 0)
                            randomParasang = worldBuilder.getLocationOfTier(GetNAdvantage(nameof(worldBuilder.getLocationOfTier), 1, maxTier--, 2));

                        int xOffset = SeededRandom(nameof(JoppaWorldBuilder.ZoneIDFromXY), -1, 1, 1);
                        int yOffset = SeededRandom(nameof(JoppaWorldBuilder.ZoneIDFromXY), -1, 1, 2);
                        var zoneReq = new ZoneRequest(worldBuilder.ZoneIDFromXY("JoppaWorld", randomParasang.X + xOffset, randomParasang.Y + yOffset));

                        int undergroundThreshold = 7500 - (overTier * 200);
                        int maxStrata = 1000 + (overTier * 125);
                        if (SeededRandom(Utils.CallChain(nameof(Zone), nameof(Zone.Z)), 1, 10000) >= undergroundThreshold)
                            zoneReq = new(
                                WorldID: zoneReq.WorldID,
                                WorldX: zoneReq.WorldX,
                                WorldY: zoneReq.WorldY,
                                X: zoneReq.X,
                                Y: zoneReq.Y,
                                Z: GetNDisadvantage(Utils.CallChain(nameof(Zone), nameof(Zone.Z)), 11, maxStrata, 5));

                        var destinationZone = The.ZoneManager.GetZone(zoneReq.ZoneID);
                        if (destinationZone != null)
                        {
                            player.SystemLongDistanceMoveTo(destinationZone.GetEmptyCellsNInFromEdge(N: 3).GetRandomElement(SeededGenerator(nameof(Zone.GetEmptyCells)))
                                ?? destinationZone.GetEmptyCells().GetRandomElement(SeededGenerator(nameof(Zone.GetEmptyCells)))
                                ?? destinationZone.GetCells().GetRandomElement(SeededGenerator(nameof(Zone.GetEmptyCells))).Clear(Combat: true));
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Moving player somewhere cool", x);
                    }

                    if (player.TryGetPart(out UD_Bones_BonesSaver bonesSaver))
                        bonesSaver.BonesMode = true;
                }
            }
            return base.handleBootEvent(id, game, info, element);
        }

        public static int GetNDisadvantage(string Method, int Low, int High, int N, int? Iteration = null)
        {
            using var values = ScopeDisposedList<int>.GetFromPool();
            for (int i = 0; i < N; i++)
                values.Add(SeededRandom(Method, Low, High, i + Iteration.GetValueOrDefault()));

            return Utils.GetDisadvantage(values.ToArray());
        }

        public static int GetNAdvantage(string Method, int Low, int High, int N, int? Iteration = null)
        {
            using var values = ScopeDisposedList<int>.GetFromPool();
            for (int i = 0; i < N; i++)
                values.Add(SeededRandom(Method, Low, High, i + Iteration.GetValueOrDefault()));

            return Utils.GetAdvantage(values.ToArray());
        }

        public static int GetRandomLevel()
            => GetNDisadvantage(nameof(GetRandomLevel), MinimumLevelForBones, MaximumLevelForBonesMode, 2)
            ;

        public static int GetRandomXPForLevel(int Level)
        {
            int low = Leveler.GetXPForLevel(Level);
            int high = Leveler.GetXPForLevel(Level) - 1;
            return Stat.SeededRandom($"{BONES_MODE}::{The.Game.GameID}:{nameof(GetRandomXPForLevel)}", low, high);
        }

        private static string GetSeedFor(string Method, int? Iteration = null)
            => $"{BONES_MODE}::{The.Game.GameID}:{Method}{(Iteration != null ? $":{Iteration}" : Iteration)}"
            ;

        private static int SeededRandom(string Method, int Low, int High, int? Iteration = null)
            => Stat.SeededRandom(GetSeedFor(Method, Iteration), Low, High)
            ;

        private static Random SeededGenerator(string Method, int? Iteration = null)
            => Stat.GetSeededRandomGenerator(GetSeedFor(Method, Iteration))
            ;

        public void AdvanceToEnd()
        {
            builder.info.GameSeed = Guid.NewGuid().ToString();
            QudGameBootModule.SeedGame(The.Game, builder.info);
            var previousActiveWindow = builder.activeWindow;
            do
            {
                try
                {
                    builder.advance();
                    if (builder.activeWindow == null
                        || builder.activeWindow == previousActiveWindow)
                        break;

                    previousActiveWindow = builder.activeWindow;
                    previousActiveWindow.window.DebugQuickstart(Mode);
                }
                catch (Exception x)
                {
                    Utils.Error($"{nameof(BonesModeModule)} Advancing to end", x);
                    break;
                }
            }
            while (builder?.activeWindow != null);
        }
    }
}
