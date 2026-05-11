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
using XRL.World.Tinkering;
using XRL.World.WorldBuilders;

using Event = XRL.World.Event;

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
            if (BonesManager.System != null)
                BonesManager.System.EmbarkBuilder = builder;

            if (Mode == BONES_MODE)
            {
                if (id == QudGameBootModule.BOOTEVENT_AFTERBOOTPLAYEROBJECT
                    && element is GameObject player)
                {
                    SimulateNormalRunProgress(player);
                }
                else
                if (id == QudGameBootModule.BOOTEVENT_GAMESTARTING
                    && UD_Bones_WorldBuilder.Builder is JoppaWorldBuilder worldBuilder)
                {
                    player = The.Player;
                    SimulateBeingSomewhereCool(player, worldBuilder);

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
            int high = Leveler.GetXPForLevel(Level + 1) - 1;
            return SeededRandom($"{nameof(GetRandomXPForLevel)}", low, high);
        }

        public static string GetSeedFor(string Method, int? Iteration = null)
            => $"{BONES_MODE}::{The.Game.GameID}:{Method}{(Iteration != null ? $":{Iteration}" : Iteration)}"
            ;

        public static int SeededRandom(string Method, int Low, int High, int? Iteration = null)
            => Stat.SeededRandom(GetSeedFor(Method, Iteration), Low, High)
            ;

        public static bool SeededOdds(string Method, int Odds, int ChanceIn, int? Iteration = null)
            => SeededRandom(Method, 1, ChanceIn, Iteration) <= Odds
            ;

        public static bool SeededOddsIn10000(string Method, int Odds, int? Iteration = null)
            => SeededOdds(Method, Odds, 10000, Iteration)
            ;

        public static Random SeededGenerator(string Method, int? Iteration = null)
            => Stat.GetSeededRandomGenerator(GetSeedFor(Method, Iteration))
            ;

        public void AdvanceToEnd()
        {
            builder.info.GameSeed = Guid.NewGuid().ToString();
            QudGameBootModule.SeedGame(The.Game, builder.info);
            var activeWindow = builder.activeWindow;
            do
            {
                try
                {
                    Utils.Log($"{nameof(BonesModeModule)}.{nameof(AdvanceToEnd)}, {activeWindow?.window?.GetType()?.Name ?? "NO_WINDOW"}");
                    builder.SkippingUIUpdates = true;

                    builder.advance();
                    if (builder.activeWindow == null
                        || builder.activeWindow == activeWindow)
                        break;

                    activeWindow = builder.activeWindow;

                    if (activeWindow.window is QudCustomizeCharacterModuleWindow customizeCharacterModuleWindow
                        && customizeCharacterModuleWindow.module is QudCustomizeCharacterModule customizeCharacterModule)
                    {
                        if (customizeCharacterModule.data == null)
                            customizeCharacterModule.setData(new());

                        if (SeededOddsIn10000(nameof(Gender), 5000))
                            customizeCharacterModule.data.gender = Gender.GetAllGenericPersonal()
                                .GetRandomElement(SeededGenerator(nameof(Gender), 1));

                        var scrollContext = customizeCharacterModuleWindow.prefabComponent.scrollContext;
                        var scrollContextData = scrollContext?.data;
                        if (!scrollContextData.IsNullOrEmpty())
                        {
                            for (int i = 0; i < scrollContextData.Count; i++)
                            {
                                scrollContext.selectedPosition = i;
                                if (SeededOddsIn10000(activeWindow?.window?.GetType()?.Name ?? nameof(RandomSelection), 7000, 1))
                                {
                                    customizeCharacterModuleWindow.RandomSelection();
                                }
                            }
                        }
                        else
                        {
                            string name = The.Core.GenerateRandomPlayerName(builder.GetModule<QudSubtypeModule>().data.Subtype);
                            name = builder.info.fireBootEvent(QudGameBootModule.BOOTEVENT_GENERATERANDOMPLAYERNAME, null, name);
                            customizeCharacterModule.setName(name);
                            if (SeededOddsIn10000(activeWindow?.window?.GetType()?.Name ?? nameof(RandomSelection), 7000, 1))
                                customizeCharacterModule.setPet(customizeCharacterModuleWindow.GetPets().GetRandomElement().Id);
                        }
                    }

                    if (activeWindow.window is QudChooseStartingLocationModuleWindow chooseStartingLocationModuleWindow
                        && chooseStartingLocationModuleWindow.module is QudChooseStartingLocationModule chooseStartingLocationModule)
                    {
                        chooseStartingLocationModuleWindow.RandomSelectionNoUI();
                        continue;
                    }

                    activeWindow.window.DebugQuickstart(Mode);
                }
                catch (Exception x)
                {
                    Utils.Error($"{nameof(BonesModeModule)} Advancing to end, {activeWindow?.window?.GetType()?.Name ?? "NO_WINDOW"}", x);
                    break;
                }
                finally
                {
                    builder.SkippingUIUpdates = false;
                }
            }
            while (builder?.activeWindow != null);
        }

        public static bool SimulateNormalRunProgress(GameObject Player)
        {
            try
            {
                Player.AwardXP(GetRandomXPForLevel(GetRandomLevel() - (Player.Level - 1)));
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                ReceiveStageSpecificStuff(Player);

                int virtualCreditWedges = CalculateVirtualCreditWedges(Player); ;
                // Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (Start)");
                GiveOverTierStuff(Player, ref virtualCreditWedges);

                GiveTierStuff(Player, ref virtualCreditWedges);

                GiveExoticStuff(Player, ref virtualCreditWedges);

                PerformVeryIntelligentPointAssignment(Player);

                // AdvanceRapidly(Player); // this might be happening automatically

                GrowLimbs(Player);

                BecomeSomewhat(Player, ref virtualCreditWedges);

                CashOutVirtualCreditWedges(Player, ref virtualCreditWedges);
                
                SpillDramsAttemptingToManageThem(Player);

                UnderstandThings(Player);

                PeerBeyondTheVeil(Player);

                Player.Brain?.PerformReequip(Silent: true, Initial: true);

                ShedAFewPounds(Player);
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(SimulateNormalRunProgress)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static void GetTierAndOverTier(GameObject Player, out int Tier, out List<int> OverTier)
        {
            Tier = XRL.World.Capabilities.Tier.Constrain(Player.GetTier());
            int overTier = Math.Max(0, Player.GetTier() - Tier);
            OverTier = new(overTier);
            while (OverTier.Capacity < OverTier.Count
                && OverTier.Count <= overTier)
                OverTier.Add(XRL.World.Capabilities.Tier.MAXIMUM);
        }

        public static bool ReceiveStageSpecificStuff(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out _);

                if (tier == 8)
                    Player.ReceivePopulation("FinalSupply");
                else
                    if (tier >= 7)
                    Player.ReceivePopulation("ReefBetaSupply");
                else
                    if (tier >= 6)
                    Player.ReceivePopulation("TombSupply");
                else
                    Player.ReceivePopulation($"Tier{Tier.Constrain(tier - 1)}Wares");
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(ReceiveStageSpecificStuff)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static int CalculateVirtualCreditWedges(GameObject Player)
        {
            int virtualCreditWedges = 0;
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                virtualCreditWedges = (tier * 2) + (overTier.Count * 3);
                // Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (Start)");
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(CalculateVirtualCreditWedges)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return 0;
            }
            return virtualCreditWedges;
        }

        public static bool GiveOverTierStuff(GameObject Player, ref int VirtualCreditWedges)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                for (int i = 0; i < overTier.Count; i++)
                {
                    if (SeededOddsIn10000(nameof(GiveOverTierStuff), 3000, i))
                    {
                        int wares = overTier[i] - 1;
                        if (SeededOddsIn10000($"{nameof(GiveOverTierStuff)}:{nameof(wares)}", 2000, i))
                            wares = overTier[i];
                        Player.ReceivePopulation($"Tier{Tier.Constrain(wares)}Wares");
                    }

                    int relicCount = SeededRandom(Utils.CallChain(nameof(GiveOverTierStuff), nameof(RelicGenerator)), -2, 2, i);
                    for (int j = 0; j < relicCount; j++)
                    {
                        string relicSeed = $"{Utils.CallChain(nameof(RelicGenerator), nameof(RelicGenerator.GetPeriodFromRelicTier))}:{i}";
                        int relicTier = SeededRandom(relicSeed, 1, 5, j);

                        var relic = RelicGenerator.GenerateRelic(RelicGenerator.GetPeriodFromRelicTier(relicTier));
                        if (Player.ReceiveObject(relic))
                            relic.SetImportant(true, player: true);

                        Player.AdjustCyberneticsLicensePointsFromWedges(
                            Amount: VirtualCreditWedges + GetNAdvantage(
                                Method: $"{nameof(Extensions.AdjustCyberneticsLicensePointsFromWedges)}:{nameof(overTier)}:{i}",
                                Low: 3,
                                High: 6,
                                N: 2,
                                Iteration: j),
                            Remaining: out VirtualCreditWedges);
                        // Utils.Log($"{nameof(VirtualCreditWedges)}: {VirtualCreditWedges} ({nameof(overTier)} [{i},{j}])");
                    }
                    int cyberTier = overTier[i] - 1;
                    if (SeededOddsIn10000($"{nameof(GiveOverTierStuff)}:{nameof(cyberTier)}", 2000, i))
                        cyberTier = overTier[i];
                    Player.ReceiveObjectFromPopulation($"Cybernetics{cyberTier}");
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(GiveOverTierStuff)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool GiveTierStuff(GameObject Player, ref int VirtualCreditWedges)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                Player.ReceivePopulation($"Tier{Tier.Constrain(tier - 1)}Wares");
                for (int i = tier; i > 0; i--)
                {
                    if (SeededOddsIn10000(nameof(GiveTierStuff), 5000, i))
                    {
                        int wares = i - 1;
                        if (SeededOddsIn10000($"{nameof(GiveTierStuff)}:{nameof(wares)}", 2000, i))
                            wares = i;
                        Player.ReceivePopulation($"Tier{Tier.Constrain(wares)}Wares");
                    }

                    int cyberTicker = 0;
                    if (i > 2
                        || SeededOddsIn10000($"{Utils.CallChain(nameof(GiveTierStuff), nameof(RelicGenerator))}:{nameof(tier)}", 2000, i))
                    {
                        int relicCount = SeededRandom(Utils.CallChain(nameof(GiveTierStuff), nameof(RelicGenerator)), -2, 2, i);
                        for (int j = 0; j < relicCount; j++)
                        {
                            var relic = RelicGenerator.GenerateRelic(RelicGenerator.GetPeriodFromRelicTier(i));
                            if (Player.ReceiveObject(relic))
                                relic.SetImportant(true, player: true);

                            Player.AdjustCyberneticsLicensePointsFromWedges(
                                Amount: VirtualCreditWedges + GetNAdvantage(
                                    Method: $"{nameof(Extensions.AdjustCyberneticsLicensePointsFromWedges)}:{nameof(tier)}:{i}",
                                    Low: Math.Clamp(i / 2, 1, 2),
                                    High: Math.Clamp(i, 2, 6),
                                    N: 2,
                                    Iteration: cyberTicker++),
                                Remaining: out VirtualCreditWedges);
                            // Utils.Log($"{nameof(VirtualCreditWedges)}: {VirtualCreditWedges} ({nameof(tier)} [{i},{j}])");
                        }
                    }
                    Player.AdjustCyberneticsLicensePointsFromWedges(
                        Amount: VirtualCreditWedges + GetNAdvantage(
                            Method: $"{nameof(Extensions.AdjustCyberneticsLicensePointsFromWedges)}:{nameof(tier)}:{i}",
                            Low: Math.Clamp(i / 2, 1, 2),
                            High: Math.Clamp(i, 2, 6),
                            N: 2,
                            Iteration: cyberTicker++),
                        Remaining: out VirtualCreditWedges);

                    int cyberTier = i - 1;
                    if (SeededOddsIn10000($"{nameof(GiveTierStuff)}:{nameof(cyberTier)}", 2000, i))
                        cyberTier = i;
                    Player.ReceiveObjectFromPopulation($"Cybernetics{Tier.Constrain(cyberTier)}");
                    if (SeededOddsIn10000($"{Utils.CallChain(nameof(GiveTierStuff), "Cybernetics")}", i * 750, i))
                    {
                        cyberTier = i - 1;
                        if (SeededOddsIn10000($"{nameof(GiveTierStuff)}:{nameof(cyberTier)}:2", 2000, i))
                            cyberTier = i;
                        Player.ReceiveObjectFromPopulation($"Cybernetics{Tier.Constrain(cyberTier)}");
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(GiveTierStuff)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool GiveExoticStuff(GameObject Player, ref int VirtualCreditWedges)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                if (Player.Inventory.GetFirstObject(go => go.HasPart<SultanMask>()) is GameObject sultanMask)
                {
                    sultanMask.Obliterate();
                    Player.ReceiveObject("The Kesil Face");
                }

                for (int i = 4; i < tier; i++)
                {
                    if (Player.Inventory.GetFirstObject(go => go.GetBlueprint().InheritsFromSafe("Otherpearl")) != null)
                        break;

                    if (SeededOddsIn10000($"{nameof(GiveExoticStuff)}:Otherpearl", 4000, i)
                        && Player.ReceiveObject("Otherpearl"))
                        break;
                }

                for (int i = 4; i < tier; i++)
                {
                    if (Player.Inventory.GetFirstObject(go => go.GetBlueprint().InheritsFromSafe("Fist of the Ape God")) != null)
                        break;

                    if (SeededOddsIn10000($"{nameof(GiveExoticStuff)}:Fist of the Ape God", 4000, i)
                        && Player.ReceiveObject("Fist of the Ape God"))
                        break;
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(GiveExoticStuff)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool PerformVeryIntelligentPointAssignment(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                if (Player.IsMutant())
                {
                    Player.GainMP(Math.Max(tier - 2, 1) + overTier.Count); // represents getting some eaters injectors.
                    Player.GainAP(Math.Max(tier - 2, 1) + overTier.Count); // represents getting some eaters injectors.
                }
                else
                    Player.GainAP((Math.Max(tier - 2, 1) * 2) + (overTier.Count * 2)); // represents getting some eaters injectors.

                int startingMP = Player.Stat("MP");
                if (startingMP > 0)
                {
                    int lastRemainingPoints = 0;
                    int lastPointsToSpend = 0;
                    bool stuck = false;
                    int attempts = 0;
                    while (Player.Stat("MP") > 0
                        && Player.Stat("MP") != lastRemainingPoints
                        && !stuck
                        && ++attempts < 200)
                    {
                        int maxPointSpend = Math.Max(1, Math.Min(Player.Stat("MP"), 4));
                        int pointsToSpend = SeededRandom(nameof(GameObject.RandomlySpendPoints), 1, maxPointSpend, attempts);

                        if (lastRemainingPoints != Player.Stat("MP"))
                            stuck = false;
                        else
                        {
                            stuck = true;
                            pointsToSpend += lastPointsToSpend;
                        }

                        lastPointsToSpend = pointsToSpend;
                        lastRemainingPoints = Player.Stat("MP");
                        Player.RandomlySpendPoints(maxAPtospend: 0, maxSPtospend: 0, maxMPtospend: pointsToSpend);
                    }
                }
                Player.RandomlySpendPoints();
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(PerformVeryIntelligentPointAssignment)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool AdvanceRapidly(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                if (Player.IsMutant()
                    && !Player.IsEsper())
                {
                    for (int i = 0; i < Player.Level; i++)
                    {
                        int iLevel = i + 1;
                        int rapidAdvancement = ((iLevel + 5) % 10 == 0) ? 3 : 0;
                        Leveler.RapidAdvancement(rapidAdvancement, Player);
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(AdvanceRapidly)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool GrowLimbs(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                if (Player.IsChimera()
                    && Player.TryGetPart(out Mutations mutations))
                {
                    int physicalMutationsCount = Player.GetPhysicalMutations().Count;

                    for (int i = 0; i < physicalMutationsCount; i++)
                        if (SeededOdds(nameof(Mutations.AddChimericBodyPart), 4000, 7000, i))
                            mutations.AddChimericBodyPart();
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(GrowLimbs)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool BecomeSomewhat(GameObject Player, ref int VirtualCreditWedges)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                if (Player.IsTrueKin())
                {
                    using (var creditWedges = ScopeDisposedList<CyberneticsCreditWedge>.GetFromPool())
                    {
                        int virtualCreditWedges = VirtualCreditWedges;
                        Player.ForeachInventoryAndEquipment(delegate (GameObject go)
                        {
                            if (go.TryGetPart(out CyberneticsCreditWedge creditWedgePart)
                                && creditWedgePart.Credits > 0)
                            {
                                virtualCreditWedges += creditWedgePart.Credits * go.Count;
                                creditWedges.Add(creditWedgePart);
                                Utils.Log($"{nameof(VirtualCreditWedges)}: {virtualCreditWedges} ({go.DebugName})");
                            }
                        });

                        VirtualCreditWedges = virtualCreditWedges;

                        while (!creditWedges.IsNullOrEmpty()
                            && creditWedges.TakeAt(0) is CyberneticsCreditWedge creditWedgePart)
                            creditWedgePart.ParentObject?.Obliterate();
                    }

                    Player.AdjustCyberneticsLicensePointsFromWedges(VirtualCreditWedges, out VirtualCreditWedges);

                    if (Player.Body.LoopParts() is IEnumerable<BodyPart> loopedParts
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
                            && !Model.HasPart(nameof(LifeSaver))
                            //&& int.TryParse(Model.GetPropertyOrTag("Tier"), out var itemTier)
                            //&& itemTier <= (tier + 2)
                            ;

                        int totalLP = Player.GetCyberneticsLicensePoints();
                        int usedLP = Player.GetUsedCyberneticsLicensePoints();
                        int availableLP = totalLP - usedLP;
                        int attempts = 0;
                        while (attempts++ < 25
                            && availableLP > 0
                            && !bodyParts.IsNullOrEmpty()
                            && bodyParts.TakeAt(0) is BodyPart nextPart)
                        {
                            if (EncountersAPI.GetAnItem(model => matchesSpec(model, nextPart, availableLP)) is GameObject cybernetic
                                || (cybernetic = Player.GetInventoryAndEquipment(go => matchesSpec(go.GetBlueprint(), nextPart, availableLP)).GetRandomElement()) != null)
                            {
                                cybernetic.MakeUnderstood();
                                nextPart.Implant(cybernetic);
                                if (nextPart.Cybernetics != cybernetic)
                                {
                                    bodyParts.Add(nextPart);
                                    Player.ReceiveObject(cybernetic);
                                }
                            }
                            else
                                bodyParts.Add(nextPart);

                            bodyParts.ShuffleInPlace(SeededGenerator(Utils.CallChain(nameof(BodyPart), nameof(BodyPart.Implant)), implantLoops++));
                            usedLP = Player.GetUsedCyberneticsLicensePoints();
                            availableLP = totalLP - usedLP;
                        }
                    }
                    Utils.Log($"{nameof(BecomeSomewhat)}(" +
                        $"{nameof(Player.Level)}: {Player.Level}, " +
                        $"{nameof(CyberneticsTerminal.Licenses)}: {Player.GetUsedCyberneticsLicensePoints()}/{Player.GetCyberneticsLicensePoints()})");
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BecomeSomewhat)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool CashOutVirtualCreditWedges(GameObject Player, ref int virtualCreditWedges)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                while (virtualCreditWedges >= 3)
                {
                    if (Player.FindObjectInInventory("CyberneticsCreditWedge3") is not GameObject creditWedge3)
                        Player.ReceiveObject("CyberneticsCreditWedge3");
                    else
                        creditWedge3.Count++;

                    virtualCreditWedges -= 3;
                    // Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (CyberneticsCreditWedge3)");
                }
                while (virtualCreditWedges >= 2)
                {
                    if (Player.FindObjectInInventory("CyberneticsCreditWedge2") is not GameObject creditWedge2)
                        Player.ReceiveObject("CyberneticsCreditWedge2");
                    else
                        creditWedge2.Count++;

                    virtualCreditWedges -= 2;
                    // Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (CyberneticsCreditWedge2)");
                }
                while (virtualCreditWedges >= 1)
                {
                    if (Player.FindObjectInInventory("CyberneticsCreditWedge") is not GameObject creditWedge1)
                        Player.ReceiveObject("CyberneticsCreditWedge");
                    else
                        creditWedge1.Count++;

                    virtualCreditWedges -= 1;
                    // Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (CyberneticsCreditWedge1)");
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(CashOutVirtualCreditWedges)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool IsWaterContainer(GameObject go)
            => go.GetBlueprint().InheritsFromSafe("WaterContainer")
            ;

        public static bool IsEmptyWaterContainer(GameObject go)
            => IsWaterContainer(go)
            && go.TryGetPart(out LiquidVolume liquidVolume)
            && liquidVolume.Volume <= 0
            ;

        public static bool IsBasicWaterContainer(GameObject go)
            => go.GetBlueprint().InheritsFromSafe("Waterskin")
            || go.GetBlueprint().InheritsFromSafe("Canteen")
            ;

        public static bool IsEmptyBasicWaterContainer(GameObject go)
            => IsBasicWaterContainer(go)
            && go.TryGetPart(out LiquidVolume liquidVolume)
            && liquidVolume.Volume <= 0
            ;

        public static bool SpillDramsAttemptingToManageThem(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                int ticker = 0;
                while (Player.GetFreeDrams() > (tier + overTier.Count) * 24)
                    Player.UseDrams(SeededRandom(nameof(GameObject.GetFreeDrams), 1, 8, ticker++));

                while (Player.Inventory.GetObjectCount(IsEmptyBasicWaterContainer) > 7)
                    Player.Inventory.GetFirstObject(IsEmptyBasicWaterContainer).Obliterate();
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(SpillDramsAttemptingToManageThem)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool UnderstandThings(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                foreach (var item in Player.GetInventoryEquipmentAndCybernetics() ?? Enumerable.Empty<GameObject>())
                    if (!item.Understood())
                        item.MakeUnderstood(ShowMessage: false);

            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(UnderstandThings)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool PeerBeyondTheVeil(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                int glimmer = Player.GetPsychicGlimmer();
                if (glimmer > 20)
                {
                    using (var shuffledInventory = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(Player.Inventory.Objects))
                    {
                        shuffledInventory.RemoveAll(go => go.HasPart<ModExtradimensional>());

                        if (!shuffledInventory.IsNullOrEmpty())
                            shuffledInventory.ShuffleInPlace(SeededGenerator(nameof(GameObject.GetPsychicGlimmer), glimmer));

                        while (glimmer > 0
                            && !shuffledInventory.IsNullOrEmpty())
                        {
                            if (shuffledInventory[0].SplitFromStack() is not GameObject itemToMod)
                                itemToMod = shuffledInventory[0];

                            int reduction = Math.Max(7, itemToMod.GetComplexity() * Math.Max(1, itemToMod.GetExamineDifficulty()));
                            ItemModding.ApplyModification(itemToMod, new ModExtradimensional());
                            itemToMod.SetStringProperty("NeverStack", "1");
                            if (Player.ReceiveObject(itemToMod))
                                glimmer -= reduction;

                            shuffledInventory.Clear();
                            shuffledInventory.AddRange(Player.Inventory.Objects);

                            shuffledInventory.RemoveAll(go => go.HasPart<ModExtradimensional>());

                            if (!shuffledInventory.IsNullOrEmpty())
                                shuffledInventory.ShuffleInPlace(SeededGenerator(nameof(GameObject.GetPsychicGlimmer), glimmer));
                        }
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(PeerBeyondTheVeil)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool IsSphereOfNegWeight(GameObject GO)
            => GO.Blueprint == "Small Sphere of Negative Weight"
            ;

        public static bool IsScrappable(GameObject GO)
            => !IsSphereOfNegWeight(GO)
            && !GO.IsImportant()
            && GO.TryGetPart(out TinkerItem tinkerItem)
            && tinkerItem.CanBeDisassembled()
            ;

        private static bool TryDoDisassembly(GameObject GO, GameObject Player)
        {
            if (!IsScrappable(GO))
                return false;

            if (!GO.TryGetPart(out TinkerItem tinkerItem))
                return false;

            if (!Player.HasSkill(nameof(Tinkering_Disassemble)))
                return false;

            bool interrupt = false;
            int disassembleBonus = Player.GetIntProperty("DisassembleBonus");
            int bitChance = 50;
            disassembleBonus = GetTinkeringBonusEvent.GetFor(Player, GO, "Disassemble", bitChance, disassembleBonus, ref interrupt);
            if (interrupt)
                return false;

            bitChance += disassembleBonus;

            try
            {
                InventoryActionEvent.Check(GO, Player, GO, "EmptyForDisassemble");
            }
            catch (Exception x)
            {
                Utils.Error("EmptyForDisassemble", x);
            }

            string bitsString = "";
            if (tinkerItem.Bits.Length == 1)
            {
                bool isWholeBit = tinkerItem.NumberMade <= 1;
                bool canGetBit = isWholeBit
                    || Stat.Random(0, tinkerItem.NumberMade) == 0;

                if (canGetBit)
                    bitsString += tinkerItem.Bits;
            }
            else
            {
                int bitCount = tinkerItem.Bits.Length - 1;
                for (int i = 0; i < tinkerItem.Bits.Length; i++)
                {
                    bool isBestBit = bitCount == i;
                    bool byChance = isBestBit
                        || bitChance.in100();

                    bool isWholeBit = tinkerItem.NumberMade <= 1;
                    bool canGetBit = isWholeBit
                        || Stat.Random(0, tinkerItem.NumberMade) == 0;

                    if (byChance
                        && canGetBit)
                        bitsString += tinkerItem.Bits[i];
                }
            }
            if (Player.HasRegisteredEvent("ModifyBitsReceived"))
            {
                Event @event = Event.New("ModifyBitsReceived", "Item", GO, "Bits", bitsString);
                Player.FireEvent(@event);
                bitsString = @event.GetStringParameter("Bits", "");
            }

            if (bitsString.IsNullOrEmpty())
                return false;

            GO.Destroy();
            Player.RequirePart<BitLocker>().AddBits(bitsString);
            return true;
        }

        public static bool IsDroppableItem(GameObject GO)
            => !IsSphereOfNegWeight(GO)
            && !GO.IsImportant()
            && (!IsWaterContainer(GO)
                || IsEmptyBasicWaterContainer(GO))
            && GO.Weight > 1
            ;

        public static bool IsSlimmableObject(GameObject GO)
            => (ItemModding.ModificationApplicable(nameof(ModWillowy), GO)
                || ItemModding.ModificationApplicable(nameof(ModSlender), GO)
                || ItemModding.ModificationApplicable(nameof(ModSuspensor), GO))
            && !IsSphereOfNegWeight(GO)
            && GO.Weight > 1
            ;

        public static bool IsSlimmableObjectLowTier(GameObject GO)
            => (ItemModding.ModificationApplicable(nameof(ModWillowy), GO)
                || ItemModding.ModificationApplicable(nameof(ModSlender), GO))
            && !IsSphereOfNegWeight(GO)
            && GO.Weight > 1
            ;

        public static int WeightValueComparison(GameObject x, GameObject y)
        {
            if (x == null
                || y == null)
                return (x == null).CompareTo(y == null) * -1;

            return (Math.Max(1, x.ValueEach) / x.WeightEach).CompareTo(Math.Max(1, y.ValueEach) / y.WeightEach);
        }

        public static int WeightComparison(GameObject x, GameObject y)
        {
            if (x == null
                || y == null)
                return (x == null).CompareTo(y == null) * -1;

            return x.WeightEach.CompareTo(y.WeightEach) * -1;
        }

        public static bool ShedAFewPounds(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                bool isNotSlimmableObjectTierAppropriately(GameObject go)
                    => (tier > 6 && !IsSlimmableObject(go))
                    || (!IsSlimmableObjectLowTier(go))
                    ;

                bool isNotDroppable(GameObject go)
                    => !IsDroppableItem(go)
                    ;

                using (var scrapList = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(Player.Inventory.Objects))
                {
                    scrapList.RemoveAll(go => !Tinkering_Disassemble.ConsiderScrap(go));

                    while (!scrapList.IsNullOrEmpty()
                        && scrapList.TakeAt(0) is GameObject scrap)
                    {
                        if (!TryDoDisassembly(scrap, Player))
                            scrap.Destroy();
                    }
                }

                using var playerInventoryWithWeightLowestValueRatioFirst = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(Player.Inventory.Objects);
                playerInventoryWithWeightLowestValueRatioFirst.RemoveAll(isNotDroppable);

                using var playerInventoryWithWeightHeaviest = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(Player.Inventory.Objects);
                playerInventoryWithWeightHeaviest.RemoveAll(isNotSlimmableObjectTierAppropriately);

                GameObject sphereOfNegativeWeight = Player.Inventory.GetFirstObject(IsSphereOfNegWeight);

                int carriedWeight = Player.GetCarriedWeight();
                int maxCarryCapacity = Player.GetMaxCarriedWeight();

                int maxCarryModLow = -(int)Math.Ceiling(maxCarryCapacity * 0.1);
                int maxCarryModHigh = (int)Math.Ceiling(maxCarryCapacity * 0.05);
                maxCarryCapacity += GetNDisadvantage(nameof(GameObject.GetCarriedWeight), maxCarryModLow, maxCarryModHigh, 2);

                int ticker = 0;
                int failedAttempts = 0;
                int maxSpheres = (int)Math.Ceiling((tier + overTier.Count).Fibonacci() * (2.5 + (overTier.Count * 0.5)));
                if (tier < 5)
                    maxSpheres = 2;
                if (tier < 8)
                    maxSpheres = 4;

                maxSpheres += SeededRandom(Utils.CallChain(nameof(ShedAFewPounds), nameof(maxSpheres)), Math.Max(0, -tier), tier);
                while (carriedWeight > maxCarryCapacity
                    && failedAttempts < 25)
                {
                    ticker++;

                    Utils.Log($"{nameof(ShedAFewPounds)} ({ticker}) - {nameof(failedAttempts)}: {failedAttempts}, Droppable: {playerInventoryWithWeightLowestValueRatioFirst.Count}, Slimmable: {playerInventoryWithWeightHeaviest.Count}");
                    playerInventoryWithWeightLowestValueRatioFirst.StableSortInPlace(WeightValueComparison);
                    playerInventoryWithWeightHeaviest.StableSortInPlace(WeightComparison);

                    bool atMaxSpheres = sphereOfNegativeWeight != null
                        && sphereOfNegativeWeight.Count >= maxSpheres;

                    var slimmableItem = playerInventoryWithWeightHeaviest.FirstOrDefault();

                    var droppableItem = playerInventoryWithWeightLowestValueRatioFirst.FirstOrDefault();

                    if (SeededOddsIn10000(Utils.CallChain(nameof(ShedAFewPounds), nameof(IsDroppableItem)), 8000, ticker)
                        && droppableItem != null)
                    {
                        droppableItem.SplitFromStack();
                        Utils.Log($"{1.Indent()}Dropping: {droppableItem?.DebugName ?? "NO_ITEM"}");
                        if (!TryDoDisassembly(droppableItem, Player))
                        droppableItem?.Obliterate();

                        playerInventoryWithWeightLowestValueRatioFirst.Clear();
                        playerInventoryWithWeightLowestValueRatioFirst.AddRange(Player.Inventory.Objects);
                        playerInventoryWithWeightLowestValueRatioFirst.RemoveAll(isNotDroppable);
                    }
                    else
                    if (SeededOddsIn10000(Utils.CallChain(nameof(ShedAFewPounds), nameof(IsSphereOfNegWeight)), 6000, ticker)
                        || slimmableItem != null)
                    {
                        if (!atMaxSpheres)
                        {
                            if (sphereOfNegativeWeight == null
                                && Player.ReceiveObject("Small Sphere of Negative Weight"))
                            {
                                sphereOfNegativeWeight = Player.Inventory.GetFirstObject(IsSphereOfNegWeight);
                                sphereOfNegativeWeight.MakeUnderstood();
                                Utils.Log($"{1.Indent()}Adding: {sphereOfNegativeWeight?.DebugName ?? "NO_ITEM"}");
                            }
                            else
                            if (sphereOfNegativeWeight != null)
                            {
                                Utils.Log($"{1.Indent()}Adding: {sphereOfNegativeWeight?.DebugName ?? "NO_ITEM"}: {sphereOfNegativeWeight.Count}++");
                                sphereOfNegativeWeight.Count++;
                            }
                        }
                        else
                        if (slimmableItem != null
                            && droppableItem != null)
                        {
                            failedAttempts--;
                        }
                    }
                    else
                    {
                        slimmableItem.SplitFromStack();
                        if (SeededOddsIn10000(Utils.CallChain(nameof(ShedAFewPounds), nameof(ModWillowy)), 2000, ticker)
                            || !slimmableItem.ApplyModification(new ModSlender()))
                            slimmableItem.ApplyModification(new ModWillowy());
                        Utils.Log($"{1.Indent()}Slimming: {slimmableItem?.DebugName ?? "NO_ITEM"}, {nameof(ModWillowy)}: {slimmableItem.HasPart<ModWillowy>()}, {nameof(ModSlender)}: {slimmableItem.HasPart<ModSlender>()}");

                        playerInventoryWithWeightHeaviest.Clear();
                        playerInventoryWithWeightHeaviest.AddRange(Player.Inventory.Objects);
                        playerInventoryWithWeightHeaviest.RemoveAll(isNotSlimmableObjectTierAppropriately);
                    }

                    Player.FlushWeightCaches();

                    if (carriedWeight == Player.GetCarriedWeight())
                    {
                        Utils.Log($"{1.Indent()}Attempt Failed...");
                        failedAttempts++;
                    }

                    carriedWeight = Player.GetCarriedWeight();
                    maxCarryCapacity = Player.GetMaxCarriedWeight();
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(ShedAFewPounds)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool SimulateBeingSomewhereCool(GameObject Player, JoppaWorldBuilder WorldBuilder)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                int maxTier = tier;
                Location2D randomParasang = null;
                int parasangTries = 0;
                while (randomParasang == null
                    && parasangTries++ < 25
                    && maxTier > 0)
                    randomParasang = WorldBuilder.getLocationOfTier(GetNAdvantage(nameof(WorldBuilder.getLocationOfTier), 1, maxTier--, 2));

                int xOffset = SeededRandom(nameof(JoppaWorldBuilder.ZoneIDFromXY), -1, 1, 1);
                int yOffset = SeededRandom(nameof(JoppaWorldBuilder.ZoneIDFromXY), -1, 1, 2);
                int zoneX = Math.Clamp(randomParasang.X + xOffset, 0, (80 * 3) - 1);
                int zoneY = Math.Clamp(randomParasang.Y + yOffset, 0, (25 * 3) - 1);
                var zoneReq = new ZoneRequest(WorldBuilder.ZoneIDFromXY("JoppaWorld", zoneX, zoneY));

                int undergroundThreshold = 2500 + (overTier.Count * 200);
                int maxStrata = 1000 + (overTier.Count * 125);
                if (SeededOddsIn10000(Utils.CallChain(nameof(Zone), nameof(Zone.Z)), undergroundThreshold))
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
                    var destintionCell = destinationZone.GetEmptyReachableCellsNInFromEdge(N: 3).GetRandomElement(SeededGenerator(nameof(Zone.GetEmptyCells)))
                        ?? destinationZone.GetEmptyReachableCells().GetRandomElement(SeededGenerator(nameof(Zone.GetEmptyCells)))
                        ?? destinationZone.GetEmptyCellsNInFromEdge(N: 3).GetRandomElement(SeededGenerator(nameof(Zone.GetEmptyCells)))
                        ?? destinationZone.GetEmptyCells().GetRandomElement(SeededGenerator(nameof(Zone.GetEmptyCells)))
                        ?? destinationZone.GetCells().GetRandomElement(SeededGenerator(nameof(Zone.GetEmptyCells)))?.Clear(Combat: true);

                    if (!destintionCell.IsReachable())
                    {
                        int zoneWidthEdge = destinationZone.Width - 1;
                        int zoneHeightEdge = destinationZone.Height - 1;
                        var edgeCell = destinationZone.GetCells(c => Utils.IsEdgeCell(c, destinationZone)).GetRandomElement(SeededGenerator(nameof(Utils.IsEdgeCell)));

                        var findpath = new FindPath(edgeCell, destintionCell);

                        int adjacentTicker = 0;
                        foreach (var step in findpath?.Steps ?? Enumerable.Empty<Cell>())
                        {
                            if (step.IsSolidFor(Player))
                                step.Clear(Combat: true);
                            step.ForeachAdjacentCell(delegate (Cell c)
                            {
                                if (step.IsSolidFor(Player)
                                    && SeededOddsIn10000(nameof(FindPath), 1750, adjacentTicker++))
                                    c.Clear(Combat: true);
                            });
                        }
                    }
                    if (destintionCell != null)
                        Player.SystemLongDistanceMoveTo(destintionCell);
                }
            }
            catch (Exception x)
            {
                Utils.Error($"Moving player somewhere cool", x);
                return false;
            }
            return true;
        }

        public static bool SimulateBeingSomewhereCool(GameObject Player)
            => SimulateBeingSomewhereCool(Player, UD_Bones_WorldBuilder.Builder)
            ;
    }
}
