using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using XRL.World.Conversations.Parts;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
using XRL.World.Parts.Skill;
using XRL.World.Skills;
using XRL.World.Tinkering;
using XRL.World.WorldBuilders;

using Event = XRL.World.Event;

namespace UD_Bones_Folder.Mod.UI
{
    [HasModSensitiveStaticCache]
    public partial class BonesModeModule : QudEmbarkBuilderModule<BonesModeModuleData>
    {
        public static Dictionary<string, List<Delegate>> BuilderModuleDelegates = new();

        public const string BONES_MODE = "UD_Bones_Bones";

        public static int MinimumLevelForBones => UD_Bones_BonesSaver.MinimumLevelForBones;
        public static int MaximumLevelForBonesMode => 80;
        public static int LevelRollDisadvantage => 2;

        public string Mode => builder?.GetModule<QudGamemodeModule>()?.data?.Mode;

        #region XML Parsing

        public class GameTypeDescriptor
        {
            public string ID;

            public string Title;

            public string IconTile;

            public string IconForeground;

            public string IconDetail;

            public string Description;
        }

        public Dictionary<string, GameTypeDescriptor> GameTypes = new();

        protected GameTypeDescriptor CurrentReadingGameTypeDescriptor;

        public override Dictionary<string, Action<XmlDataHelper>> XmlNodes
        {
            get
            {
                Dictionary<string, Action<XmlDataHelper>> xmlNodes = base.XmlNodes;
                xmlNodes.Add("types", HandleTypesNode);
                return xmlNodes;
            }
        }

        public Dictionary<string, Action<XmlDataHelper>> XmlTypesNodes => new Dictionary<string, Action<XmlDataHelper>>
        {
            { "type", HandleTypeNode },
            { "icon", HandleTypeIconNode },
            { "description", HandleTypeDescriptionNode }
        };

        public void HandleTypesNode(XmlDataHelper xml)
        {
            xml.HandleNodes(XmlTypesNodes);
        }

        protected void HandleTypeNode(XmlDataHelper xml)
        {
            string attribute = xml.GetAttribute("ID");
            if (!GameTypes.TryGetValue(attribute, out CurrentReadingGameTypeDescriptor))
            {
                CurrentReadingGameTypeDescriptor = new GameTypeDescriptor
                {
                    ID = attribute
                };
                GameTypes.Add(attribute, CurrentReadingGameTypeDescriptor);
            }
            CurrentReadingGameTypeDescriptor.Title = xml.GetAttribute("Title");
            xml.HandleNodes(XmlTypesNodes);
            CurrentReadingGameTypeDescriptor = null;
        }

        protected void HandleTypeIconNode(XmlDataHelper xml)
        {
            CurrentReadingGameTypeDescriptor.IconTile = xml.GetAttribute("Tile");
            CurrentReadingGameTypeDescriptor.IconDetail = xml.GetAttributeString("Detail", "W");
            CurrentReadingGameTypeDescriptor.IconForeground = xml.GetAttributeString("Foreground", "y");
            xml.DoneWithElement();
        }

        protected void HandleTypeDescriptionNode(XmlDataHelper xml)
        {
            CurrentReadingGameTypeDescriptor.Description = xml.GetTextNode();
        }

        #endregion
        
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

        public string GetSelectedType()
        {
            return data?.type;
        }

        public void SelectType(string type)
        {
            setData(new BonesModeModuleData(type));
            AdvanceToEnd();
        }

        [ModSensitiveCacheInit]
        public static void CacheBuilderModuleDelegates()
        {
            BuilderModuleDelegates ??= new();
            BuilderModuleDelegates.Clear();

            var methods = ModManager.GetMethodsWithAttribute(typeof(BonesModeModuleActionAttribute), typeof(HasBonesModeModuleActionAttribute));

            Utils.Log($"{nameof(CacheBuilderModuleDelegates)}...");
            foreach (var method in methods.IteratorSafe())
            {
                Utils.Log($"{1.Indent()}{method.Name}");
                foreach (var attribute in method.GetCustomAttributes<BonesModeModuleActionAttribute>())
                {
                    var modInfo = ModManager.GetMod(method.DeclaringType.Assembly);
                    if (!typeof(AbstractBuilderModuleWindowBase).IsAssignableFrom(attribute.TargetWindow))
                    {
                        Utils.Warn(
                            ModInfo: modInfo,
                            Message: $"{method.DeclaringType.Name}.{method.Name} has {attribute.GetType().Name} with invalid " +
                                $"{nameof(attribute.TargetWindow)} argument: {attribute.TargetWindow.Name}, " +
                                $"cannot be assigned to {nameof(Type)} {nameof(AbstractBuilderModuleWindowBase)} and will be skipped.");
                        continue;
                    }

                    if (method.GetParameters() is ParameterInfo[] parameters)
                    {
                        if (parameters.Length != 2)
                        {
                            Utils.Log($"{2.Indent()}{nameof(parameters)}.{nameof(parameters.Length)}: {parameters.Length}");
                            continue;
                        }
                        if (!typeof(AbstractBuilderModuleWindowBase).IsAssignableFrom(parameters[0].ParameterType))
                        {
                            Utils.Log($"{2.Indent()}{nameof(parameters)}[0].{nameof(ParameterInfo.ParameterType)}: {parameters[0].ParameterType.Name}");
                            continue;
                        }
                        if (!typeof(AbstractQudEmbarkBuilderModule).IsAssignableFrom(parameters[1].ParameterType))
                        {
                            Utils.Log($"{2.Indent()}{nameof(parameters)}[1].{nameof(ParameterInfo.ParameterType)}: {parameters[1].ParameterType.Name}");
                            continue;
                        }
                        if (method.ReturnType != typeof(void))
                        {
                            Utils.Warn(
                                ModInfo: modInfo,
                                Message: $"{method.DeclaringType.Name}.{method.Name} returns {method.ReturnType.Name} instead of {typeof(void)}. " +
                                    $"This return value won't be utilised by the {Utils.ModTitle} mod.");
                        }
                        string key = attribute.TargetWindow?.Name ?? "All";
                        if (!BuilderModuleDelegates.TryGetValue(key, out var delegates))
                            delegates = BuilderModuleDelegates[key] = new();
                        try
                        {
                            Type actionType = null;
                            if (method.ReturnType == typeof(void))
                            {
                                actionType = typeof(Action<,>).MakeGenericType(
                                    typeArguments: new Type[]
                                    {
                                        parameters[0].ParameterType,
                                        parameters[1].ParameterType,
                                    });
                            }
                            else
                            {
                                actionType = typeof(Func<,,>).MakeGenericType(
                                    typeArguments: new Type[]
                                    {
                                        parameters[0].ParameterType,
                                        parameters[1].ParameterType,
                                        method.ReturnType,
                                    });
                            }
                            delegates.Add(method.CreateDelegate(actionType));
                            continue;
                        }
                        catch (Exception x)
                        {
                            Utils.Error($"Failed to create {nameof(Delegate)} for {method.DeclaringType.Name}.{method.Name}", x);
                        }
                    }
                    Utils.Log($"{2.Indent()}Skipped");
                }
            }
        }

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

                    try
                    {
                        if (activeWindow.window is QudCustomizeCharacterModuleWindow customizeCharacterModuleWindow
                        && customizeCharacterModuleWindow.module is QudCustomizeCharacterModule customizeCharacterModule)
                        {
                            if (customizeCharacterModule.data == null)
                                customizeCharacterModule.setData(new());

                            if (SeededPerMyriadChance(nameof(Gender), 5000))
                                customizeCharacterModule.data.gender = Gender.GetAllGenericPersonal()
                                    .GetRandomElement(SeededGenerator(nameof(Gender), 1));

                            var scrollContext = customizeCharacterModuleWindow.prefabComponent.scrollContext;
                            var scrollContextData = scrollContext?.data;
                            if (!scrollContextData.IsNullOrEmpty())
                            {
                                for (int i = 0; i < scrollContextData.Count; i++)
                                {
                                    scrollContext.selectedPosition = i;
                                    if (SeededPerMyriadChance(activeWindow?.window?.GetType()?.Name ?? nameof(RandomSelection), 7000, 1))
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
                                if (SeededPerMyriadChance(activeWindow?.window?.GetType()?.Name ?? nameof(RandomSelection), 7000, 1))
                                    customizeCharacterModule.setPet(customizeCharacterModuleWindow.GetPets().GetRandomElement().Id);
                            }
                        }

                        if (activeWindow.window is QudChooseStartingLocationModuleWindow chooseStartingLocationModuleWindow
                            && chooseStartingLocationModuleWindow.module is QudChooseStartingLocationModule chooseStartingLocationModule)
                        {
                            chooseStartingLocationModuleWindow.RandomSelectionNoUI();
                            continue;
                        }
                    }
                    finally
                    {
                        if (BuilderModuleDelegates.TryGetValue(activeWindow.window.GetType().Name, out var builderDelegates))
                            foreach (var builderDelegate in builderDelegates.IteratorSafe())
                                builderDelegate.DynamicInvoke(activeWindow.window, activeWindow.window._module);

                        if (BuilderModuleDelegates.TryGetValue("All", out builderDelegates))
                            foreach (var builderDelegate in builderDelegates.IteratorSafe())
                                builderDelegate.DynamicInvoke(activeWindow.window, activeWindow.window._module);
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
                    SimulateBeingSomewhereCool(player, worldBuilder, Silent: true);

                    if (player.TryGetPart(out UD_Bones_BonesSaver bonesSaver))
                    {
                        bonesSaver.BonesMode = (data.type == "InstantDeath");
                        bonesSaver.PetBlueprint = info.getData<QudCustomizeCharacterModuleData>()?.pet;
                    }
                }
            }
            return base.handleBootEvent(id, game, info, element);
        }

        public static int GetNDisadvantage(string Method, int Low, int High, int N, int? Iteration = null)
        {
            using var values = ScopeDisposedList<int>.GetFromPool();
            for (int i = 0; i <= Math.Max(0, N); i++)
                values.Add(SeededRandom(Method, Low, High, i + Iteration.GetValueOrDefault()));

            return Utils.GetDisadvantage(values.ToArray());
        }

        public static int GetNAdvantage(string Method, int Low, int High, int N, int? Iteration = null)
        {
            using var values = ScopeDisposedList<int>.GetFromPool();
            for (int i = 0; i <= Math.Max(0, N); i++)
                values.Add(SeededRandom(Method, Low, High, i + Iteration.GetValueOrDefault()));

            return Utils.GetAdvantage(values.ToArray());
        }

        public static int GetRandomLevel()
            => LevelRollDisadvantage >= 0 
            ? GetNDisadvantage(nameof(GetRandomLevel), MinimumLevelForBones, MaximumLevelForBonesMode, LevelRollDisadvantage)
            : GetNAdvantage(nameof(GetRandomLevel), MinimumLevelForBones, MaximumLevelForBonesMode, Math.Abs(LevelRollDisadvantage))
            ;

        public static int GetRandomXPForLevel(int Level)
        {
            int low = Leveler.GetXPForLevel(Level);
            int high = Leveler.GetXPForLevel(Level + 1) - 1;
            return SeededRandom($"{nameof(GetRandomXPForLevel)}", low, high);
        }

        public static string GetSeedFor(string Method, int? Iteration = null)
            => $"{BONES_MODE}::{The.Game?.GameID}:{Method}{(Iteration != null ? $":{Iteration}" : Iteration)}"
            ;

        public static int SeededRandom(string Method, int Low, int High, int? Iteration = null)
            => Stat.SeededRandom(GetSeedFor(Method, Iteration), Low, High)
            ;

        public static bool SeededOdds(string Method, int Odds, int ChanceIn, int? Iteration = null)
            => SeededRandom(Method, 1, ChanceIn, Iteration) <= Odds
            ;

        public static bool SeededPerMyriadChance(string Method, int Chance, int? Iteration = null)
            => SeededOdds(Method, Chance, 10000, Iteration)
            ;

        public static Random SeededGenerator(string Method, int? Iteration = null)
            => Stat.GetSeededRandomGenerator(GetSeedFor(Method, Iteration))
            ;

        public static bool SimulateNormalRunProgress(GameObject Player)
        {
            try
            {
                Utils.SuppressPopupsWhile(() => Player.AwardXP(GetRandomXPForLevel(GetRandomLevel() - (Player.Level - 1))));

                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                ReceiveStageSpecificStuff(Player);

                int virtualCreditWedges = CalculateVirtualCreditWedges(Player); ;
                // Utils.Log($"{nameof(virtualCreditWedges)}: {virtualCreditWedges} (Start)");
                GiveOverTierStuff(Player, ref virtualCreditWedges);

                GiveTierStuff(Player, ref virtualCreditWedges);

                RecoverRelics(Player, ref virtualCreditWedges);

                GiveExoticStuff(Player, ref virtualCreditWedges);

                PerformVeryIntelligentPointAssignment(Player);

                LearnDataDisksIfPossible(Player);

                // AdvanceRapidly(Player); // this might be happening automatically

                GrowLimbs(Player);

                BecomeSomewhat(Player, ref virtualCreditWedges);

                CashOutVirtualCreditWedges(Player, ref virtualCreditWedges);
                
                SpillDramsAttemptingToManageThem(Player);

                UnderstandThings(Player);

                PeerBeyondTheVeil(Player);

                InstallSomeCells(Player);

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

        private static bool GetIntimateWithPaxKlanq(GameObject Player, int PerMyriadChance)
        {
            if (!SeededPerMyriadChance(nameof(GetIntimateWithPaxKlanq), Chance: PerMyriadChance))
                return true;

            if (Player.Body.GetBody() is BodyPart playerBody)
            {
                BodyPart target = null;
                if (Player.Body.LoopParts().Where(limb => FungalSporeInfection.BodyPartSuitableForFungalInfection(limb)) is IEnumerable<BodyPart> infectableLimbs
                    && !infectableLimbs.IsNullOrEmpty())
                {
                    using (var randomBodyParts = ScopeDisposedList<BodyPart>.GetFromPoolFilledWith(infectableLimbs))
                    {
                        if (!randomBodyParts.IsNullOrEmpty())
                        {
                            randomBodyParts.ShuffleInPlace(SeededGenerator(nameof(PaxInfectLimb)));
                            var bodyParts = new List<BodyPart>(Player.Body.GetParts());
                            var bodyPartsEnumerable = randomBodyParts.IteratorSafe();
                            
                            foreach (var bodyPart in bodyPartsEnumerable)
                            {
                                bool success = false;
                                Utils.SuppressPopupsWhile(delegate ()
                                {
                                    success = bodyPart != null
                                        && PaxInfectLimb.InfectLimb(bodyParts, bodyPart, bodyPart.GetOrdinalName());
                                });

                                if (success)
                                {
                                    target = bodyPart;
                                    break;
                                }
                            }
                            
                        }
                    }
                }
                return target.Equipped?.Blueprint == "PaxInfection";
            }
            return false;
        }

        public static bool ReceiveStageSpecificStuff(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out _);

                int modifiedTier = tier;
                if (SeededPerMyriadChance(nameof(ReceiveStageSpecificStuff), 500, 1))
                    modifiedTier = Tier.Constrain(++modifiedTier);
                else
                if (SeededPerMyriadChance(nameof(ReceiveStageSpecificStuff), 1000, 2))
                    modifiedTier = Tier.Constrain(--modifiedTier);

                bool klanqed = false;

                if (modifiedTier == 8)
                    Player.ReceivePopulation("UD_Bones_BonesMode_Final");
                else
                if (modifiedTier >= 7)
                    Player.ReceivePopulation("UD_Bones_BonesMode_Reef");
                else
                if (modifiedTier >= 6)
                    Player.ReceivePopulation("UD_Bones_BonesMode_Tomb");
                else
                {
                    if (modifiedTier >= 5)
                    {
                        klanqed = true;
                        GetIntimateWithPaxKlanq(Player, 2000);
                    }
                    Player.ReceivePopulation($"UD_Bones_BonesMode_Tier {Tier.Constrain(modifiedTier - 1)}");
                }
                if (modifiedTier >= 6)
                {
                    Player?.Body?.GetBody()?.AddPart("Floating Nearby", Dynamic: true);
                    if (!klanqed)
                        GetIntimateWithPaxKlanq(Player, 8000);
                }
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
                    if (SeededPerMyriadChance(nameof(GiveOverTierStuff), 3000, i))
                    {
                        int wares = overTier[i] - 1;
                        if (SeededPerMyriadChance($"{nameof(GiveOverTierStuff)}:{nameof(wares)}", 2000, i))
                            wares = overTier[i];
                        Player.ReceivePopulation($"UD_Bones_BonesMode_Tier {Tier.Constrain(wares)}");
                    }

                    int additionalCredits = GetNAdvantage(
                            Method: $"{nameof(Extensions.AdjustCyberneticsLicensePointsFromWedges)}:{nameof(overTier)}:{i}",
                            Low: -2,
                            High: 8,
                            N: Math.Clamp(i, 1, 5));

                    if (additionalCredits > 0)
                        Player.AdjustCyberneticsLicensePointsFromWedges(
                            Amount: VirtualCreditWedges + additionalCredits,
                            Remaining: out VirtualCreditWedges);

                    int cyberTier = overTier[i] - 1;
                    if (SeededPerMyriadChance($"{nameof(GiveOverTierStuff)}:{nameof(cyberTier)}", 2000, i))
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

        public static bool GiveOverTierStuff(GameObject Player)
        {
            int discard = 0;
            return GiveOverTierStuff(Player, ref discard);
        }

        public static bool GiveTierStuff(GameObject Player, ref int VirtualCreditWedges)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                var tieredRelics = new Dictionary<int, List<GameObject>>();
                foreach (var cachedObject in (The.ZoneManager?.CachedObjects?.Values).IteratorSafe())
                {
                    if (!cachedObject.HasStringProperty("RelicName"))
                        continue;

                    if (!cachedObject.TryGetPart(out TakenAchievement takenAch)
                        || takenAch.AchievementID != Achievement.RECOVER_RELIC?.ID)
                        continue;

                    int tierKey = cachedObject.GetTier();
                    if (!tieredRelics.ContainsKey(tierKey))
                        tieredRelics[tierKey] = Event.NewGameObjectList();

                    tieredRelics[tierKey].Add(cachedObject);
                }

                Player.ReceivePopulation($"UD_Bones_BonesMode_Tier {Tier.Constrain(tier - 1)}");
                for (int i = tier; i > 0; i--)
                {
                    if (SeededPerMyriadChance(nameof(GiveTierStuff), 5000, i))
                    {
                        int waresTier = i - 1;
                        if (SeededPerMyriadChance($"{nameof(GiveTierStuff)}:{nameof(waresTier)}", 2000, i))
                            waresTier = i;
                        Player.ReceivePopulation($"UD_Bones_BonesMode_Tier {Tier.Constrain(waresTier)}");
                    }

                    int cyberTicker = 0;
                    Player.AdjustCyberneticsLicensePointsFromWedges(
                        Amount: VirtualCreditWedges + GetNAdvantage(
                            Method: $"{nameof(Extensions.AdjustCyberneticsLicensePointsFromWedges)}:{nameof(tier)}:{i}",
                            Low: Math.Clamp(i / 2, 0, 2),
                            High: Math.Clamp(i, 2, 3),
                            N: 2,
                            Iteration: cyberTicker++),
                        Remaining: out VirtualCreditWedges);

                    int cyberTier = i - 1;
                    if (SeededPerMyriadChance($"{nameof(GiveTierStuff)}:{nameof(cyberTier)}", 2000, i))
                        cyberTier = i;
                    Player.ReceiveObjectFromPopulation($"Cybernetics{Tier.Constrain(cyberTier)}");
                    if (SeededPerMyriadChance($"{Utils.CallChain(nameof(GiveTierStuff), "Cybernetics")}", i * 750, i))
                    {
                        cyberTier = i - 1;
                        if (SeededPerMyriadChance($"{nameof(GiveTierStuff)}:{nameof(cyberTier)}:2", 2000, i))
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

        public static bool GiveTierStuff(GameObject Player)
        {
            int discard = 0;
            return GiveTierStuff(Player, ref discard);
        }

        public static int GetCreditWedgeTotalFromRelic(GameObject Relic)
            => Tier.Constrain(Relic.GetTier()) switch
            {
                >= 8 => 7,
                >= 6 => 6,
                >= 4 => 4,
                >= 3 => 3,
                _ => 2,
            }
            ;

        private static bool RecoverRelic(GameObject Player, ref GameObject Relic, ref int VirtualCreditWedges)
        {
            Relic = The.ZoneManager.GetCachedObjects(Relic.ID);

            Relic.RemovePart<TakenAchievement>();

            if (!Relic.TryRemoveFromContext()
                || !Player.ReceiveObject(Relic))
            {
                Relic.AddPart(new TakenAchievement { AchievementID = "ACH_RECOVER_RELIC" });

                The.ZoneManager.CacheObject(Relic, cacheTwiceOk: true);
                return false;
            }

            Relic.SetImportant(true, player: true);
            if (Relic.HasPart<TrainingBook>())
            {
                Utils.Log($"{nameof(RecoverRelics)}({Relic.DebugName ?? "NO_OBJECT"}) is Book");
                var relic = Relic;
                Utils.SuppressPopupsWhile(delegate ()
                {
                    InventoryActionEvent.Check(
                        Object: relic,
                        Actor: Player,
                        Item: relic,
                        Command: "Read",
                        OverrideEnergyCost: true,
                        Silent: true,
                        EnergyCostOverride: 0);
                });

                Player.AdjustCyberneticsLicensePointsFromWedges(
                    Amount: VirtualCreditWedges + GetCreditWedgeTotalFromRelic(Relic),
                    Remaining: out VirtualCreditWedges);
            }
            return true;
        }

        public static bool RecoverRelics(GameObject Player, ref int VirtualCreditWedges)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                var tieredRelics = new Dictionary<int, ScopeDisposedList<GameObject>>();
                foreach (var cachedObject in (The.ZoneManager?.CachedObjects?.Values).IteratorSafe())
                {
                    if (!cachedObject.HasStringProperty("RelicName"))
                        continue;

                    if (!cachedObject.TryGetPart(out TakenAchievement takenAch)
                        || takenAch.AchievementID != Achievement.RECOVER_RELIC?.ID)
                        continue;

                    int tierKey = Tier.Constrain(cachedObject.GetTier());
                    if (!tieredRelics.ContainsKey(tierKey))
                        tieredRelics[tierKey] = ScopeDisposedList<GameObject>.GetFromPool();

                    tieredRelics[tierKey].Add(cachedObject);
                }

                foreach (var cachedRelicsList in tieredRelics.Values.IteratorSafe())
                    cachedRelicsList.StableSortInPlace((x, y) => x.BaseID.CompareTo(y.BaseID));

                using var leftoverRelics = ScopeDisposedList<GameObject>.GetFromPool();
                Random relicRnd = null;
                for (int i = tier; i > 0; i--)
                {
                    if (tieredRelics.TryGetValue(i, out var relics)
                        && !relics.IsNullOrEmpty())
                    {
                        int relicCount = SeededRandom(Utils.CallChain(nameof(RecoverRelics), nameof(relicCount)), Math.Min(i - 2, 1), relics.Count, i);

                        relicRnd = SeededGenerator($"{Utils.CallChain(nameof(RecoverRelics), nameof(RelicGenerator))}:{nameof(tier)}", i);

                        for (int j = 0; j < relicCount; j++)
                        {
                            if (relics.IsNullOrEmpty())
                                break;

                            relics.ShuffleInPlace(relicRnd);
                            if (relics.TakeAt(0) is GameObject relic)
                                RecoverRelic(Player, ref relic, ref VirtualCreditWedges);

                        }
                        while (!relics.IsNullOrEmpty()
                            && relics.TakeAt(0) is GameObject leftoverRelic)
                            leftoverRelics.Add(leftoverRelic);

                        relics.Dispose();
                    }
                }

                leftoverRelics.StableSortInPlace((x, y) => x.BaseID.CompareTo(y.BaseID));

                for (int i = 0; i < overTier.Count; i++)
                {
                    int relicCount = leftoverRelics.Count;
                    if (Math.Max(i, 1) < leftoverRelics.Count)
                        relicCount = SeededRandom(Utils.CallChain(nameof(RecoverRelics), nameof(relicCount)), Math.Max(i, 1), leftoverRelics.Count, i);

                    relicRnd = SeededGenerator($"{Utils.CallChain(nameof(RecoverRelics), nameof(RelicGenerator))}:{nameof(overTier)}", i);
                    for (int j = 0; j < relicCount; j++)
                    {
                        if (leftoverRelics.IsNullOrEmpty())
                            break;

                        leftoverRelics.ShuffleInPlace(relicRnd);
                        if (leftoverRelics.TakeAt(0) is GameObject relic)
                            RecoverRelic(Player, ref relic, ref VirtualCreditWedges);
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(RecoverRelics)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool GiveExoticStuff(GameObject Player, ref int VirtualCreditWedges)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                for (int i = 4; i < tier; i++)
                {
                    if (Player.Inventory.GetFirstObject(go => go.GetBlueprint().InheritsFromSafe("Otherpearl")) != null)
                        break;

                    if (SeededPerMyriadChance($"{nameof(GiveExoticStuff)}:Otherpearl", 4000, i)
                        && Player.ReceiveObject("Otherpearl"))
                        break;
                }

                for (int i = 4; i < tier; i++)
                {
                    if (Player.Inventory.GetFirstObject(go => go.GetBlueprint().InheritsFromSafe("Fist of the Ape God")) != null)
                        break;

                    if (SeededPerMyriadChance($"{nameof(GiveExoticStuff)}:Fist of the Ape God", 4000, i)
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

        public static void SpendMutationPointsInSmallChunks(GameObject Player, int? MaxToSpend = null, bool Silent = true)
        {
            if (Player == null)
                return;

            int maxPointsToSpend = Player.Stat("MP");
            if (MaxToSpend.HasValue)
                maxPointsToSpend = Math.Min(MaxToSpend.Value, Player.Stat("MP"));


            int maxAttempts = 200;
            if (maxPointsToSpend > 0)
            {
                var sB = Event.NewStringBuilder().AppendLine();
                int totalSpent = 0;
                int lastRemainingPoints = 0;
                int lastPointsToSpend = 0;
                bool stuck = false;
                int attempts = 0;
                while (Player.Stat("MP") > 0
                    && Player.Stat("MP") != lastRemainingPoints
                    && totalSpent < maxPointsToSpend
                    && !stuck
                    && ++attempts < maxAttempts)
                {
                    int maxPointSpend = Math.Max(1, Math.Min(Player.Stat("MP"), 4));
                    int pointsToSpend = SeededRandom(nameof(GameObject.RandomlySpendPoints), 1, maxPointSpend, attempts);

                    int stuckPoints = Player.Stat("MP");
                    if (lastRemainingPoints != stuckPoints)
                        stuck = false;
                    else
                    {
                        stuck = true;
                        pointsToSpend += lastPointsToSpend;
                    }

                    lastPointsToSpend = pointsToSpend;
                    lastRemainingPoints = Player.Stat("MP");
                    totalSpent += pointsToSpend;
                    Player.RandomlySpendPoints(maxAPtospend: 0, maxSPtospend: 0, maxMPtospend: pointsToSpend, result: sB);

                    if (stuckPoints != lastRemainingPoints)
                        stuck = false;
                }

                var resultString = sB.ToString().Strip().Trim();
                Event.ResetTo(sB);
                if (!Silent
                    && Player.IsPlayer())
                {
                    using var resultStrings = ScopeDisposedList<string>.GetFromPoolFilledWith(resultString.Split("! ").IteratorSafe());

                    for (int i = 0; i < resultStrings.Count; i++)
                        resultStrings[i] = resultStrings[i].Replace("base rank in ", "").Trim();

                    Utils.Log(resultStrings.Aggregate("Spending Mutation Points...", Utils.NewLineDelimitedAggregator));

                    if (attempts >= 200)
                        Utils.Log($"Mutation point spend aborted early due to {nameof(attempts)} exceeding {maxAttempts}.");
                    else
                    if (stuck)
                        Utils.Log($"Mutation point spend aborted early due to being {nameof(stuck)}.");
                    if (Player.Stat("MP") == lastRemainingPoints)
                        Utils.Log($"Mutation point spend aborted early due to {nameof(lastRemainingPoints)} being equal to the current remaining points.");
                }
            }
        }

        private static bool SkillIsNotMeaningfullyLearnable(IBaseSkillEntry Entry, GameObject Player, int RemainingPoints)
        {
            if (RemainingPoints <= 0)
                return true;

            if (Player == null)
                return true;

            if (Player.HasPart(Entry.Class))
                return true;

            if (Entry.Cost > RemainingPoints)
                return true;

            if (Entry is PowerEntry power
                && !Player.HasPart(power.ParentSkill.Class))
                return SkillIsNotMeaningfullyLearnable(power.ParentSkill, Player, RemainingPoints);

            return false;
        }

        private static bool LearnASkill(
            IBaseSkillEntry Entry,
            GameObject Player,
            ref int TotalSpent,
            ScopeDisposedList<IBaseSkillEntry> SkillsList,
            ScopeDisposedList<IBaseSkillEntry> UnlearnedSkills,
            int Depth = 0,
            bool Silent = true
            )
        {
            if (Entry == null)
                return false;

            if (Player == null)
                return false;

            if (SkillsList == null)
                return false;

            if (UnlearnedSkills == null)
                return false;

            if (Player.HasPart(Entry.Class))
            {
                if (!Silent)
                    Utils.Log($"{(Depth + 2).Indent()}[{Const.CROSS}] Already Know {Entry.Class}");
                SkillsList.Remove(Entry);
                UnlearnedSkills.Remove(Entry);
                return false;
            }

            Utils.SuppressPopupsWhile(() => Player.AddSkill(Entry.Class));

            if (!Player.HasPart(Entry.Class))
            {
                if (!Silent)
                    Utils.Log($"{(Depth + 2).Indent()}[{Const.CROSS}] Issue Learning {Entry.Class}");
                return false;
            }

            SkillsList.Remove(Entry);
            UnlearnedSkills.Remove(Entry);

            foreach (var unlearnedSkill in UnlearnedSkills.IteratorSafe())
            {
                if (unlearnedSkill is PowerEntry unlearnedPower
                    && unlearnedPower.ParentSkill == Entry
                    && unlearnedPower.Cost <= 0
                    && !Player.HasPart(unlearnedPower.Class))
                {
                    LearnASkill(
                        Entry: unlearnedPower,
                        Player: Player,
                        TotalSpent: ref TotalSpent,
                        SkillsList: SkillsList,
                        UnlearnedSkills: UnlearnedSkills,
                        Depth: Depth + 1,
                        Silent: Silent);
                }
            }

            Player.Statistics["SP"].Penalty += Entry.Cost;
            TotalSpent += Entry.Cost;

            if (!Silent)
                Utils.Log($"{(Depth + 2).Indent()}[{Const.TICK}] Learned {Entry.Class}");
            return true;
        }

        private static readonly List<string> FightingStyles = new()
        {
            nameof(SingleWeaponFighting),
            nameof(Multiweapon_Fighting),
        };

        private static readonly List<string> WeaponSkills = new()
        {
            nameof(Axe),
            nameof(Cudgel),
            nameof(LongBlades),
            nameof(ShortBlades),
        };

        private static readonly List<string> MissileSkills = new()
        {
            nameof(Pistol),
            nameof(Rifles),
            nameof(HeavyWeapons),
        };

        private static readonly List<List<string>> NegativePreferences = new()
        {
            FightingStyles,
            WeaponSkills,
            MissileSkills,
        };

        public static bool IsSkillOrPowerOfSkill(IBaseSkillEntry BaseSkillEntry, string SkillClass)
        {
            if (SkillClass == BaseSkillEntry.Class)
                return true;

            if (BaseSkillEntry is PowerEntry powerEntry
                && SkillClass == powerEntry.ParentSkill.Class)
                return true;

            return false;
        }

        public static bool HasNegativePreference(IBaseSkillEntry BaseSkillEntry, GameObject Player, bool? PreferSingleWeaponFighting)
        {
            bool hasNegPref = false;
            if (!WeaponSkills.Any(e => Player.HasPart(e)))
            {
                hasNegPref = !WeaponSkills.Contains(BaseSkillEntry.Class);
                //Utils.Log($"{2.Indent()}[{Const.CROSS}] {BaseSkillEntry.Class}; No weapon skills and not weapon skill.");
                return hasNegPref;
            }

            if (!MissileSkills.Any(e => Player.HasPart(e)))
            {
                hasNegPref = !MissileSkills.Contains(BaseSkillEntry.Class);
                //Utils.Log($"{2.Indent()}[{(!hasNegPref ? Const.TICK : Const.CROSS)}] {BaseSkillEntry.Class}; No missile skills.");
                return hasNegPref;
            }

            if (!FightingStyles.Any(e => Player.HasPart(e)))
            {
                if (!FightingStyles.Contains(BaseSkillEntry.Class))
                {
                    //Utils.Log($"{2.Indent()}[{Const.CROSS}] {BaseSkillEntry.Class}; No fighting style.");
                    return true;
                }
                else
                if (PreferSingleWeaponFighting.HasValue)
                {
                    hasNegPref = PreferSingleWeaponFighting.GetValueOrDefault()
                        ? BaseSkillEntry.Class != nameof(SingleWeaponFighting)
                        : BaseSkillEntry.Class != nameof(Multiweapon_Fighting)
                        ;
                    //Utils.Log($"{2.Indent()}[{(!hasNegPref ? Const.TICK : Const.CROSS)}] {BaseSkillEntry.Class}; No fighting style, preferred fighting style is {(PreferSingleWeaponFighting.GetValueOrDefault() ? nameof(SingleWeaponFighting) : nameof(Multiweapon_Fighting))}.");
                    return hasNegPref;
                }
                else
                {
                    //Utils.Log($"{2.Indent()}[{Const.TICK}] {BaseSkillEntry.Class}; No fighting style.");
                    return false;
                }
            }

            foreach (var negativePrefList in NegativePreferences.IteratorSafe())
            {
                foreach (var negPref in negativePrefList)
                {
                    if (Player.HasPart(negPref))
                    {
                        if (negativePrefList.Any(skillClass => negPref != BaseSkillEntry.Class && IsSkillOrPowerOfSkill(BaseSkillEntry, skillClass)))
                        {
                            //Utils.Log($"{2.Indent()}[{Const.CROSS}] {BaseSkillEntry.Class}; have skill {negPref}.");
                            return true;
                        }
                    }
                }
            }
            //Utils.Log($"{2.Indent()}[{Const.TICK}] {BaseSkillEntry.Class}; kept in shortlist.");
            return false;
        }

        public static void SpendSkillPointsRandomly(
            GameObject Player,
            int? MaxToSpend = null,
            bool Silent = true
            )
        {
            if (Player == null)
                return;

            int maxPointsToSpend = Player.Stat("SP");
            if (MaxToSpend.HasValue)
                maxPointsToSpend = Math.Min(MaxToSpend.Value, Player.Stat("SP"));

            using var skillsList = ScopeDisposedList<IBaseSkillEntry>.GetFromPoolFilledWith(SkillFactory.Factory.SkillList.Values);
            skillsList.AddRange(SkillFactory.Factory.PowersByClass.Values);
            skillsList.OrderBy(entry => entry.Name);
            skillsList.RemoveAll(entry => Player.HasSkill(entry.Class));

            if (SeededPerMyriadChance($"{nameof(SpendSkillPointsRandomly)}.{nameof(Nonlinearity_Tomorrowful)}", 9500))
                skillsList.RemoveAll(entry => entry.Name.StartsWith(nameof(Nonlinearity)));

            bool? preferSingleWeaponFighting = null;
            if (Player.IsTrueKin())
                preferSingleWeaponFighting = true;
            else
            if (Player.IsChimera())
                preferSingleWeaponFighting = false;

            using var skillsShortList = ScopeDisposedList<IBaseSkillEntry>.GetFromPool();

            int totalSpent = 0;
            int remainingPoints = maxPointsToSpend;
            int attempts = 0;

            if (!Silent)
                Utils.Log($"{nameof(SpendSkillPointsRandomly)}(For: {Player?.DebugName ?? "NO_OBJECT"}, {nameof(maxPointsToSpend)}: {maxPointsToSpend})");

            while (Player.Stat("SP") > 0
                && remainingPoints > 0
                && totalSpent < maxPointsToSpend
                && !skillsList.IsNullOrEmpty()
                && attempts++ < 250)
            {
                skillsList.RemoveAll(entry => SkillIsNotMeaningfullyLearnable(entry, Player, remainingPoints));

                var eligibleSkills = skillsList.Where(entry => entry.MeetsRequirements(Player));

                skillsShortList.Clear();
                skillsShortList.AddRange(eligibleSkills);

                /*if (!Silent)
                    Utils.Log($"{1.Indent()}Processing {nameof(skillsShortList)}, {nameof(eligibleSkills)}: {eligibleSkills.Count()}");*/
                skillsShortList.RemoveAll(entry => HasNegativePreference(entry, Player, preferSingleWeaponFighting));

                /*if (!Silent)
                    Utils.Log($"{1.Indent()}{nameof(skillsShortList)}: {skillsShortList.Count()}");*/

                if (!skillsShortList.IsNullOrEmpty())
                {
                    /*foreach (var shortListSkill in skillsShortList)
                        if (!Silent)
                            Utils.Log($"{2.Indent()}{nameof(shortListSkill)}: {shortListSkill.Class}, {nameof(shortListSkill.Cost)}: {shortListSkill.Cost}");*/
                    eligibleSkills = skillsShortList.IteratorSafe();
                }

                if (eligibleSkills.IsNullOrEmpty())
                {
                    /*Utils.Log($"{1.Indent()}[{Const.CROSS}] {nameof(eligibleSkills)}: {eligibleSkills.Count()}, " +
                        $"{nameof(attempts)}: {attempts}, " +
                        $"{nameof(remainingPoints)}: {remainingPoints}");*/
                    break;
                }

                if (!eligibleSkills.Any(entry => entry.Cost <= remainingPoints))
                {
                    if (!Silent)
                    {
                        Utils.Log($"{1.Indent()}[{Const.CROSS}] {nameof(eligibleSkills)}: {eligibleSkills.Count()}, " +
                            $"{nameof(attempts)}: {attempts}, " +
                            $"{nameof(remainingPoints)}: {remainingPoints}, " +
                            $"lowest cost: {eligibleSkills.Aggregate(int.MaxValue, (a, n) => Math.Min(a, n.Cost))}");
                    }
                    break;
                }

                using var unlearnedSkills = ScopeDisposedList<IBaseSkillEntry>.GetFromPoolFilledWith(eligibleSkills);

                int index = SeededRandom(nameof(SpendSkillPointsRandomly), 0, unlearnedSkills.Count - 1, attempts);
                if (unlearnedSkills.TakeAt(index) is not IBaseSkillEntry skill)
                {
                    if (!Silent)
                    {
                        Utils.Log($"{1.Indent()}{Const.UNCHECKED} {nameof(unlearnedSkills)}: {unlearnedSkills.Count()}, " +
                            $"{nameof(index)}: {index}, " +
                            $"{nameof(attempts)}: {attempts}, " +
                            $"{nameof(remainingPoints)}: {remainingPoints}");
                    }
                    continue;
                }

                if (!Silent)
                {
                    Utils.Log($"{1.Indent()}{Const.CHECKED} {nameof(unlearnedSkills)}: {unlearnedSkills.Count()}, " +
                        $"{nameof(index)}: {index}, " +
                        $"{nameof(attempts)}: {attempts}, " +
                        $"{nameof(remainingPoints)}: {remainingPoints}");
                    Utils.Log($"{2.Indent()}{nameof(skill)}: {skill.Class}, {nameof(skill.Cost)}: {skill.Cost}");
                }

                if (skill is PowerEntry power
                    && !Player.HasPart(power.ParentSkill.Class))
                {
                    if (!Silent)
                        Utils.Log($"{2.Indent()}[/] Missing Parent Skill: {power.ParentSkill.Class}");

                    unlearnedSkills.Add(skill);

                    if (unlearnedSkills.Contains(power.ParentSkill)
                        || (power.ParentSkill.MeetsRequirements(Player)
                            && power.ParentSkill.Cost <= remainingPoints))
                    {
                        if (!Silent)
                            Utils.Log($"{3.Indent()}{nameof(skill)} set to Parent: {power.ParentSkill.Class}");
                        skill = power.ParentSkill;
                    }
                    else
                    {
                        if (!Silent)
                            Utils.Log($"{2.Indent()}[{Const.CROSS}] Unable to learn Parent: {power.ParentSkill.Class}");
                        continue;
                    }
                }

                LearnASkill(
                    Entry: skill,
                    Player: Player,
                    TotalSpent: ref totalSpent,
                    SkillsList: skillsList,
                    UnlearnedSkills: unlearnedSkills,
                    Silent: Silent);

                remainingPoints = Math.Min(Player.Stat("SP"), maxPointsToSpend - totalSpent);
            }
        }

        public static Dictionary<string, int> GetAbilityStats(GameObject Player)
        {
            var abilityStats = new Dictionary<string, int>();
            foreach ((var name, var stat) in Player.Statistics.IteratorSafe())
            {
                if (name.EqualsNoCase("Strength")
                    || name.EqualsNoCase("Agility")
                    || name.EqualsNoCase("Toughness")
                    || name.EqualsNoCase("Intelligence")
                    || name.EqualsNoCase("Willpower")
                    || name.EqualsNoCase("Ego"))
                    abilityStats.Add(name, stat.Value);

                if (abilityStats.Count >= 6)
                    break;
            }

            return abilityStats;
        }

        public static void SpendAbilityPointsWeighted(GameObject Player, int? MaxToSpend = null)
        {
            if (Player == null)
                return;

            int maxPointsToSpend = Player.Stat("AP");
            if (MaxToSpend.HasValue)
                maxPointsToSpend = Math.Min(MaxToSpend.Value, Player.Stat("AP"));

            int totalSpent = 0;
            int remainingPoints = maxPointsToSpend;
            int attempts = 0;
            while (Player.Stat("AP") > 0
                && remainingPoints > 0
                && totalSpent < maxPointsToSpend
                && ++attempts < 200)
            {
                var abilityBag = GetAbilityStats(Player).ToBallBag(SeededGenerator(nameof(SpendAbilityPointsWeighted), attempts));

                var abilityStat = Player.Statistics[abilityBag.PluckOne()];

                abilityBag.Clear();

                abilityStat.BaseValue++;
                Player.Statistics["AP"].Penalty++;

                totalSpent++;
                remainingPoints = Math.Min(Player.Stat("AP"), maxPointsToSpend - totalSpent);
            }
        }

        public static bool PerformVeryIntelligentPointAssignment(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                int low = (Math.Max(tier - 2, 1) + overTier.Count) - 2;
                int high = low + 3 + (int)Math.Floor(overTier.Count / 2.0);
                int amount = SeededRandom($"{nameof(Nectar_Tonic_Applicator)}::Amount", low, high) * 2;

                bool isMutant = Player.IsMutant();
                var abilityStats = GetAbilityStats(Player).Keys.IteratorSafe().ToList();
                int abilityStatsCount = abilityStats.Count;
                if (Player.IsMutant())
                {
                    for (int i = 0; i < amount; i++)
                    {
                        int index = SeededRandom($"{nameof(Nectar_Tonic_Applicator)}::Apply", 0, abilityStatsCount * 1000, i) % (abilityStatsCount + 1);
                        if (index >= abilityStatsCount)
                            Player.GainMP(Math.Max(tier - 2, 1) + overTier.Count);
                        else
                        if (!Player.Statistics.TryGetValue(abilityStats[index], out var abilityStat))
                            Player.GainAP(1);
                        else
                        {
                            Player.GainAP(1);
                            abilityStat.BaseValue++;
                            Player.Statistics["AP"].Penalty++;
                        }
                    }
                }
                else
                    Player.GainAP(amount); // represents getting some eaters injectors.

                SpendMutationPointsInSmallChunks(Player, Silent: false);
                SpendAbilityPointsWeighted(Player);
                SpendSkillPointsRandomly(Player);
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(PerformVeryIntelligentPointAssignment)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool LearnDataDisksIfPossible(GameObject Player)
        {
            if (Player.HasPart(nameof(Tinkering_Tinker1)))
            {
                using var dataDisks = ScopeDisposedList<DataDisk>.GetFromPool();
                foreach (var dataDiskObject in Player.GetInventoryAndEquipment(go => go.HasPart<DataDisk>()).IteratorSafe())
                    if (dataDiskObject.TryGetPart(out DataDisk dataDisk))
                        dataDisks.Add(dataDisk);

                foreach (var dataDisk in dataDisks?.IteratorSafe())
                {
                    Utils.SuppressPopupsWhile(delegate ()
                    {
                        InventoryActionEvent.Check(
                            Object: dataDisk.ParentObject,
                            Actor: Player,
                            Item: dataDisk.ParentObject,
                            Command: "LearnFromDataDisk",
                            OverrideEnergyCost: true,
                            Silent: true,
                            EnergyCostOverride: 0);
                    });
                }
            }
            return true;
        }

        public static bool AdvanceRapidly(GameObject Player)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                if (!Player.IsPlayer()
                    && Player.IsMutant()
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

                    // makes up for the 1:3 odds used by the base game's random pick.
                    for (int i = 0; i < physicalMutationsCount; i++)
                        if (SeededOdds(nameof(Mutations.AddChimericBodyPart), 1000, 3000, i))
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
                                //Utils.Log($"{nameof(VirtualCreditWedges)}: {virtualCreditWedges} ({go.DebugName})");
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

                        using var implantedList = ScopeDisposedList<GameObjectBlueprint>.GetFromPool();
                        bool checkImplanted(GameObjectBlueprint Model, bool ExcludeInstalled)
                            => !ExcludeInstalled
                            || !implantedList.Contains(Model)
                            ;

                        bool matchesSpec(GameObjectBlueprint Model, BodyPart NextPart, int AvailableLP, bool ExcludeInstalled = false)
                            => Model.TryGetPartParameter(nameof(CyberneticsBaseItem), nameof(CyberneticsBaseItem.Slots), out string slots)
                            && slots.CachedCommaExpansion().Any(s => s.EqualsNoCase(NextPart.Type))
                            && Model.TryGetPartParameter(nameof(CyberneticsBaseItem), nameof(CyberneticsBaseItem.Cost), out int cost)
                            && cost <= AvailableLP
                            && !Model.HasPart(nameof(LifeSaver))
                            //&& int.TryParse(Model.GetPropertyOrTag("Tier"), out var itemTier)
                            //&& itemTier <= (tier + 2)
                            && checkImplanted(Model, ExcludeInstalled)
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
                            var rnd = SeededGenerator(Utils.CallChain(nameof(BodyPart), nameof(BodyPart.Implant), nameof(matchesSpec)), implantLoops);
                            GameObject implant = Player.GetInventoryAndEquipment(
                                    Filter: delegate(GameObject go)
                                    {
                                        return !Player.GetInstalledCybernetics().Contains(go)
                                            && matchesSpec(
                                                Model: go.GetBlueprint(),
                                                NextPart: nextPart,
                                                AvailableLP: availableLP,
                                                ExcludeInstalled: true);
                                    }
                                    ).GetRandomElement(rnd)
                                ?? EncountersAPI.GetAnItem(
                                    filter: model => matchesSpec(
                                        Model: model,
                                        NextPart: nextPart,
                                        AvailableLP: availableLP,
                                        ExcludeInstalled : true)
                                    )
                                ?? Player.GetInventoryAndEquipment(
                                    Filter: delegate (GameObject go)
                                    {
                                        return !Player.GetInstalledCybernetics().Contains(go)
                                            && matchesSpec(
                                                Model: go.GetBlueprint(),
                                                NextPart: nextPart,
                                                AvailableLP: availableLP,
                                                ExcludeInstalled: false);
                                    }
                                    ).GetRandomElement(rnd)
                                ?? EncountersAPI.GetAnItem(
                                    filter: model => matchesSpec(
                                        Model: model,
                                        NextPart: nextPart,
                                        AvailableLP: availableLP,
                                        ExcludeInstalled: false)
                                    );

                            if (implant != null)
                            {
                                implant.RemoveFromContext();
                                implant.MakeUnderstood();
                                nextPart.Implant(implant);
                                if (nextPart.Cybernetics != implant)
                                {
                                    bodyParts.Add(nextPart);
                                    Player.ReceiveObject(implant);

                                    if (implant.GetBlueprint() is GameObjectBlueprint model
                                        && !implantedList.Contains(model))
                                        implantedList.Add(model);
                                }
                            }
                            else
                                bodyParts.Add(nextPart);

                            bodyParts.ShuffleInPlace(SeededGenerator(Utils.CallChain(nameof(BodyPart), nameof(BodyPart.Implant)), implantLoops++));
                            usedLP = Player.GetUsedCyberneticsLicensePoints();
                            availableLP = totalLP - usedLP;
                        }
                    }
                    /*Utils.Log($"{nameof(BecomeSomewhat)}(" +
                        $"{nameof(Player.Level)}: {Player.Level}, " +
                        $"{nameof(CyberneticsTerminal.Licenses)}: {Player.GetUsedCyberneticsLicensePoints()}/{Player.GetCyberneticsLicensePoints()})");*/
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BecomeSomewhat)} for {Player?.DebugName ?? "MISSING_PLAYER"}", x);
                return false;
            }
            return true;
        }

        public static bool CashOutVirtualCreditWedge(GameObject Player, int Amount, ref int VirtualCreditWedges, int Depth = 0)
        {
            if (VirtualCreditWedges <= 0)
            {
                // Utils.Log($"{(Depth + 1).Indent()}Out of Virtual Credit Wedges.");
                return false;
            }

            if (Amount <= 0)
            {
                // Utils.Log($"{(Depth + 1).Indent()}Zero or fewer wedges to produce.");
                return false;
            }

            Amount = Math.Clamp(Amount, 1, 3);
            if (Amount > VirtualCreditWedges)
                return CashOutVirtualCreditWedge(Player, Amount - 1, ref VirtualCreditWedges, Depth + 1);

            string creditWedgeBlueprint = $"CyberneticsCreditWedge{(Amount > 1 ? Amount : null)}";

            if (Player.FindObjectInInventory(creditWedgeBlueprint) is not GameObject creditWedge)
                Player.ReceiveObject(creditWedgeBlueprint);
            else
                creditWedge.Count++;

            VirtualCreditWedges -= Amount;
            // Utils.Log($"{(Depth + 1).Indent()}{nameof(Amount)}: {Amount}; Created a {creditWedgeBlueprint}; {nameof(VirtualCreditWedges)}: {VirtualCreditWedges}");
            return true;
        }

        public static bool CashOutVirtualCreditWedges(GameObject Player, ref int VirtualCreditWedges)
        {
            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                int ticker = 0;
                int amount;

                if (!Player.IsTrueKin())
                    VirtualCreditWedges /= 2;

                // Utils.Log($"{nameof(CashOutVirtualCreditWedges)}(Total: {VirtualCreditWedges})");
                do
                    amount = SeededRandom(nameof(CashOutVirtualCreditWedges), 1, 3, ticker++);
                while (CashOutVirtualCreditWedge(Player, amount, ref VirtualCreditWedges));
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

                if (!Player.HasPart<Dystechnia>())
                {
                    Utils.SuppressPopupsWhile(delegate ()
                    {
                        int intMod = Math.Min(1, Player.StatMod("Intelligence") + 3);
                        Player.PerformActionRecursively(delegate (GameObject go)
                        {
                            if (!go.IsBroken()
                                && go.TryGetPart(out Examiner examiner)
                                && !go.Understood())
                            {
                                for (int i = 0; i < intMod; i++)
                                {
                                    InventoryActionEvent.Check(
                                        Object: go,
                                        Actor: Player,
                                        Item: go,
                                        Command: "Examine",
                                        OverrideEnergyCost: true,
                                        Silent: true,
                                        EnergyCostOverride: 0);
                                }
                            }
                        });
                        Player.Energy.BaseValue = 1000;
                    });
                }
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
                    var recursiveObjects = Player.GetObjectsRecursively(Where: go => go != Player);
                    using (var shuffledInventory = ScopeDisposedList<GameObject>.GetFromPool())
                    {
                        int failedAttempts = 0;
                        do
                        {
                            shuffledInventory.Clear();
                            shuffledInventory.AddRange(recursiveObjects);

                            shuffledInventory.RemoveAll(go => go.HasPart<ModExtradimensional>());

                            if (!shuffledInventory.IsNullOrEmpty())
                                shuffledInventory.ShuffleInPlace(SeededGenerator(nameof(GameObject.GetPsychicGlimmer), glimmer));

                            if (shuffledInventory[0].SplitFromStack() is not GameObject itemToMod)
                                itemToMod = shuffledInventory[0];

                            int reduction = Math.Max(7, itemToMod.GetComplexity() * Math.Max(1, itemToMod.GetExamineDifficulty()));

                            if (!ItemModding.ApplyModification(itemToMod, new ModExtradimensional()))
                                failedAttempts++;
                            else
                            {
                                itemToMod.SetStringProperty("NeverStack", "1");
                                if (Player.ReceiveObject(itemToMod))
                                    glimmer -= reduction;
                            }
                        }
                        while (glimmer > 0
                            && !shuffledInventory.IsNullOrEmpty()
                            && failedAttempts < 10);
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

        public static bool InstallSomeCells(GameObject Player)
        {
            var rnd = SeededGenerator(nameof(InstallSomeCells));
            Player.PerformActionRecursivelyInRandomOrder(
                Action: delegate (GameObject go)
                {
                    if (Player.GetInventory(go => go.HasPartDescendedFrom<IEnergyCell>()) is not List<GameObject> energyCells
                        || energyCells.IsNullOrEmpty())
                        return;

                    if (!go.TryGetPart(out EnergyCellSocket cellSocket))
                        return;

                    energyCells.ShuffleInPlace(rnd);
                    if (energyCells.FirstOrDefault() is not GameObject firstRandomCell)
                        return;

                    var energyCell = firstRandomCell.SplitFromStack();
                    energyCell.RemoveFromContext();
                    cellSocket.SetCell(energyCell);
                },
                Where: go => go.TryGetPart(out EnergyCellSocket cellSocket) && cellSocket.Cell == null,
                Rnd: rnd);
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

                    //Utils.Log($"{nameof(ShedAFewPounds)} ({ticker}) - {nameof(failedAttempts)}: {failedAttempts}, Droppable: {playerInventoryWithWeightLowestValueRatioFirst.Count}, Slimmable: {playerInventoryWithWeightHeaviest.Count}");
                    playerInventoryWithWeightLowestValueRatioFirst.StableSortInPlace(WeightValueComparison);
                    playerInventoryWithWeightHeaviest.StableSortInPlace(WeightComparison);

                    bool atMaxSpheres = sphereOfNegativeWeight != null
                        && sphereOfNegativeWeight.Count >= maxSpheres;

                    var slimmableItem = playerInventoryWithWeightHeaviest.FirstOrDefault();

                    var droppableItem = playerInventoryWithWeightLowestValueRatioFirst.FirstOrDefault();

                    if (SeededPerMyriadChance(Utils.CallChain(nameof(ShedAFewPounds), nameof(IsDroppableItem)), 8000, ticker)
                        && droppableItem != null)
                    {
                        droppableItem.SplitFromStack();
                        //Utils.Log($"{1.Indent()}Dropping: {droppableItem?.DebugName ?? "NO_ITEM"}");
                        if (!TryDoDisassembly(droppableItem, Player))
                            droppableItem?.Obliterate();

                        playerInventoryWithWeightLowestValueRatioFirst.Clear();
                        playerInventoryWithWeightLowestValueRatioFirst.AddRange(Player.Inventory.Objects);
                        playerInventoryWithWeightLowestValueRatioFirst.RemoveAll(isNotDroppable);
                    }
                    else
                    if (SeededPerMyriadChance(Utils.CallChain(nameof(ShedAFewPounds), nameof(IsSphereOfNegWeight)), 6000, ticker)
                        || slimmableItem != null)
                    {
                        if (!atMaxSpheres)
                        {
                            if (sphereOfNegativeWeight == null
                                && Player.ReceiveObject("Small Sphere of Negative Weight"))
                            {
                                sphereOfNegativeWeight = Player.Inventory.GetFirstObject(IsSphereOfNegWeight);
                                sphereOfNegativeWeight.MakeUnderstood();
                                //Utils.Log($"{1.Indent()}Adding: {sphereOfNegativeWeight?.DebugName ?? "NO_ITEM"}");
                            }
                            else
                            if (sphereOfNegativeWeight != null)
                            {
                                //Utils.Log($"{1.Indent()}Adding: {sphereOfNegativeWeight?.DebugName ?? "NO_ITEM"}: {sphereOfNegativeWeight.Count}++");
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
                        if (SeededPerMyriadChance(Utils.CallChain(nameof(ShedAFewPounds), nameof(ModWillowy)), 2000, ticker)
                            || !slimmableItem.ApplyModification(new ModSlender()))
                            slimmableItem.ApplyModification(new ModWillowy());
                        //Utils.Log($"{1.Indent()}Slimming: {slimmableItem?.DebugName ?? "NO_ITEM"}, {nameof(ModWillowy)}: {slimmableItem.HasPart<ModWillowy>()}, {nameof(ModSlender)}: {slimmableItem.HasPart<ModSlender>()}");

                        playerInventoryWithWeightHeaviest.Clear();
                        playerInventoryWithWeightHeaviest.AddRange(Player.Inventory.Objects);
                        playerInventoryWithWeightHeaviest.RemoveAll(isNotSlimmableObjectTierAppropriately);
                    }

                    Player.FlushWeightCaches();

                    if (carriedWeight == Player.GetCarriedWeight())
                    {
                        //Utils.Log($"{1.Indent()}Attempt Failed...");
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

        public static bool SimulateBeingSomewhereCool(GameObject Player, JoppaWorldBuilder WorldBuilder, bool Silent = false)
        {
            WorldBuilder ??= UD_Bones_WorldBuilder.Builder;
            if (WorldBuilder == null)
            {
                if (!Silent)
                    Popup.Show($"Missing a necessary instance of a {nameof(JoppaWorldBuilder)}. Unable to {nameof(SimulateBeingSomewhereCool)}.\n\n:(");
                return false;
            }

            try
            {
                GetTierAndOverTier(Player, out int tier, out List<int> overTier);

                int maxTier = Math.Max(1, tier + (overTier.Count / 2));
                Location2D randomZone = null;
                int zoneTries = 0;
                while (randomZone == null
                    && zoneTries++ < 25
                    && maxTier > 0)
                {
                    int zoneTier = Tier.Constrain(GetNAdvantage(nameof(WorldBuilder.getLocationOfTier), 1, maxTier--, 3, zoneTries));
                    randomZone = WorldBuilder
                        .PeekLocationOfTier(
                            Tier: zoneTier,
                            MutableOnly: false);
                }

                randomZone ??= Location2D.Get(
                    X: SeededRandom(nameof(JoppaWorldBuilder.ZoneIDFromXY), 0, Const.MAX_ZONE_X, 1),
                    Y: SeededRandom(nameof(JoppaWorldBuilder.ZoneIDFromXY), 0, Const.MAX_ZONE_Y, 2));

                var zoneReq = new ZoneRequest(WorldBuilder.ZoneIDFromXY("JoppaWorld", randomZone.X, randomZone.Y));

                int overtierCount = overTier.Count;

                int undergroundThreshold = 1500 + (overtierCount * 200);

                if (SeededPerMyriadChance(Utils.CallChain(nameof(Zone), nameof(Zone.Z)), undergroundThreshold))
                {
                    int maxStrata = Math.Max(10 + tier * (tier + 4), 11);

                    if (tier >= 8)
                        maxStrata = 998 + (overtierCount * 125);

                    zoneReq = new(
                        WorldID: zoneReq.WorldID,
                        WorldX: zoneReq.WorldX,
                        WorldY: zoneReq.WorldY,
                        X: zoneReq.X,
                        Y: zoneReq.Y,
                        Z: GetNDisadvantage(Utils.CallChain(nameof(Zone), nameof(Zone.Z)), 11, maxStrata, 6));
                }

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
                        foreach (var step in findpath?.Steps.IteratorSafe())
                        {
                            if (step.IsSolidFor(Player))
                                step.Clear(Combat: true);
                            step.ForeachAdjacentCell(delegate (Cell c)
                            {
                                if (step.IsSolidFor(Player)
                                    && SeededPerMyriadChance(nameof(FindPath), 1750, adjacentTicker++))
                                    c.Clear(Combat: true);
                            });
                        }
                    }
                    if (destintionCell != null)
                    {
                        Player.SystemLongDistanceMoveTo(destintionCell, energyCost: 0);
                        Player.Energy.BaseValue = 1000;
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"Moving player somewhere cool", x);
                return false;
            }
            return true;
        }

        public static bool SimulateBeingSomewhereCool(GameObject Player, bool Silent = false)
            => SimulateBeingSomewhereCool(Player, UD_Bones_WorldBuilder.Builder, Silent)
            ;
    }
}