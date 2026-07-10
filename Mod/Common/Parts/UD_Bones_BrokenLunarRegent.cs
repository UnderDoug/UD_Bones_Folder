using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Qud.API;

using XRL.Collections;
using XRL.Rules;
using XRL.World.Anatomy;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;

using UD_Bones_Folder.Mod.Events;
using HarmonyLib;
using XRL.World.ZoneBuilders;
using XRL.World;
using XRL.Wish;
using XRL.UI;
using XRL.World.AI;
using UD_Bones_Folder.Mod.Serialization.PseudoTypes;
using XRL;
using HistoryKit;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_BrokenLunarRegent
        : IScribedPart
        , IModEventHandler<LoadLunarRegentEvent>
    {
        public const string BASE_BROKEN_LUNAR_REGENT = "Base Broken Lunar Regent";

        private static IEnumerable<SaveBonesInfo> CachedSaveBonesInfo;

        public static List<string> ContextsToIgnoreEncountered = new()
        {
            $"Plaidman.SaltShuffleRevival.FactionEntity",
        };

        public LunarPartyIDs CachedCourtiers;

        public string BonesID;

        public bool HasEnteredCell;

        public bool PreferMad;

        public bool ExcludeMad;

        public string ForPlayerLevel;

        public string OriginalDescription;

        protected bool HasIncremented;

        private AllegianceSet OriginalAllegience;

        public string Context;

        public UD_Bones_BrokenLunarRegent()
        { }

        public static void ClearCachedSaveBonesInfo()
        {
            CachedSaveBonesInfo = null;
        }

        public static bool BonesNotEncounteredOrFailedAlready(SaveBonesInfo BonesInfo)
            => !BonesManager.System.Encountered.Contains(BonesInfo.ID)
            && !BonesManager.System.FailedToLoadBones.Contains(BonesInfo.ID)
            ;

        public int GetForPlayerLevel()
            => !int.TryParse(ForPlayerLevel, out int forPlayerLevel)
            ? ParentObject?.Level ?? 0
            : forPlayerLevel
            ;

        public bool IsNotExcludeMadOrNotMad(SaveBonesInfo BonesInfo)
            => !ExcludeMad
            || !BonesInfo.IsMad
            ;

        public bool IsWithinDefinedLevel(SaveBonesInfo BonesInfo)
            => GetForPlayerLevel() < 5
            || BonesInfo.GetBonesJSON() is not SaveBonesJSON bonesJSON
            || BonesSpec.IsWithinLevel(bonesJSON.Level, GetForPlayerLevel())
            ;

        public bool MatchesSpec(SaveBonesInfo BonesInfo)
            => BonesNotEncounteredOrFailedAlready(BonesInfo)
            && IsNotExcludeMadOrNotMad(BonesInfo)
            && IsWithinDefinedLevel(BonesInfo)
            ;

        public bool IsReliquaryOfThisRegent(GameObject GameObject)
            => GameObject.TryGetPart(out UD_Bones_LunarReliquary reliquaryPart)
            && reliquaryPart.BonesID == BonesID
            && GameObject.GetBlueprint().InheritsFromSafe("Lunar Reliquary")
            ;

        public static string GetANonLegendaryCreatureBlueprint(Predicate<GameObjectBlueprint> Where = null, Random Rnd = null, int? AroundLevel = null)
        {
            Rnd ??= Stat.Rnd2;
            using var shortList = ScopeDisposedList<GameObjectBlueprint>.GetFromPool();
            using var aggregatedModels = ScopeDisposedList<string>.GetFromPool();
            using var shuffledBlueprints = ScopeDisposedList<GameObjectBlueprint>.GetFromPoolFilledWith(GameObjectFactory.Factory.BlueprintList);
            shuffledBlueprints.ShuffleInPlace(Rnd);
            int targetLevel = AroundLevel.GetValueOrDefault();
            do
            {
                foreach (var blueprint in shuffledBlueprints)
                {
                    if (!EncountersAPI.IsEligibleForDynamicEncounters(blueprint)
                        || !EncountersAPI.IsLegendaryEligible(blueprint))
                        continue;

                    if (targetLevel > 0)
                    {
                        if (!blueprint.HasStat("Level")
                            || blueprint.BaseStat("Level") != targetLevel)
                            continue;
                    }

                    if (Where?.Invoke(blueprint) is false)
                        continue;

                    if (blueprint.GetTag("AggregateWith", null) is string tag)
                    {
                        if (aggregatedModels.Contains(tag))
                            continue;

                        aggregatedModels.Add(tag);
                    }

                    shortList.Add(blueprint);
                }
                if (!shortList.IsNullOrEmpty())
                    return shortList.FirstOrDefault()?.Name;

                targetLevel--;
                shuffledBlueprints.ShuffleInPlace(Rnd);
            }
            while (targetLevel > 0);

            return null;
        }

        public static GameObject GetANonLegendaryCreature(Predicate<GameObjectBlueprint> Where = null, Random Rnd = null, int? AroundLevel = null, bool AllowAnimated = true)
        {
            Rnd ??= Stat.Rnd2;

            if (AllowAnimated
                && If.OneIn(5000))
                return EncountersAPI.GetAnAnimatedObject();

            return GameObject.Create(GetANonLegendaryCreatureBlueprint(Where, Rnd, AroundLevel));
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(LoadLunarRegentEvent.ID, EventOrder.EXTREMELY_LATE);
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == BeforeObjectCreatedEvent.ID
            || ID == EnteredCellEvent.ID
            ;

        public override bool HandleEvent(BeforeObjectCreatedEvent E)
        {
            Context = E.Context;

            OriginalAllegience = ParentObject?.Brain?.Allegiance;
            OriginalDescription = ParentObject?.GetPart<Description>()?._Short;

            if (CachedSaveBonesInfo.IsNullOrEmpty())
                CachedSaveBonesInfo = BonesManager.System?.GetEligibleSaveBonesInfo()
                    ?? BonesManager.GetSaveBonesInfoAsync(bones => bones.IsLooselyEligible).WaitResult();

            var originalObject = ParentObject;
            string originalPrimaryFaction = originalObject.GetPrimaryFaction();

            var rnd = ParentObject.GetSeededRandom(nameof(UD_Bones_BrokenLunarRegent));

            GameObject replacement = null;
            if (CachedSaveBonesInfo.Where(MatchesSpec) is IEnumerable<SaveBonesInfo> saveBonesInfos
                && !saveBonesInfos.IsNullOrEmpty())
            {
                var bonesWeights = new Dictionary<string, int>();
                using var bonesInfoList = ScopeDisposedList<SaveBonesInfo>.GetFromPoolFilledWith(saveBonesInfos);
                foreach (var bonesInfo in bonesInfoList)
                {
                    if (bonesInfo.GetBonesWeight() is int weight)
                    {
                        if (!bonesWeights.ContainsKey(bonesInfo.ID))
                            bonesWeights[bonesInfo.ID] = 0;

                        bonesWeights[bonesInfo.ID] += weight;
                    }
                }

                int biggestWeight = bonesWeights.Aggregate(0, (a, n) => Math.Max(a, n.Value));
                int nonMadCount = bonesInfoList.Where(b => !b.IsMad).Count();

                int madCount = bonesInfoList.Where(b => b.IsMad).Count();
                int notMadCount = bonesInfoList.Where(b => !b.IsMad).Count();

                bool forceMad = PreferMad
                    && madCount <= (notMadCount * 0.25)
                    ;
                
                if (PreferMad
                    && !forceMad)
                {
                    foreach (var bonesInfo in bonesInfoList)
                    {
                        if (PreferMad
                            && bonesWeights.ContainsKey(bonesInfo.ID))
                        {
                            if (bonesInfo.IsMad)
                                bonesWeights[bonesInfo.ID] += biggestWeight;
                            else
                                bonesWeights[bonesInfo.ID] = Math.Max(1, bonesWeights[bonesInfo.ID] / nonMadCount);
                        }
                    }
                }

                var bonesBag = bonesWeights.ToBallBag(rnd);

                while (!bonesBag.IsNullOrEmpty()
                    && bonesBag.Count > 10 // prevents exauhsting bones before the player has an oportunity to fight them
                    && bonesBag.PickOne() is string bonesID
                    && bonesInfoList.FirstOrDefault(bonesInfo => bonesInfo.ID == bonesID) is SaveBonesInfo pickedBones)
                {
                    try
                    {
                        if (BonesManager.System.AttemptLoadBones(
                            ObjectID: ParentObject.ID,
                            PickedBones: pickedBones,
                            LunarRegent: out replacement,
                            CachedCourtiers: out CachedCourtiers,
                            LogEncountered: !LoadLunarRegentEvent.CheckContextAny(Context, ContextsToIgnoreEncountered.ToArray()),
                            ProcPreLoad: delegate (GameObject go)
                            {
                                if (go.IsLunarRegent())
                                {
                                    if (forceMad)
                                        go?.SetMad(true);

                                    go?.AddPart(this);
                                }
                            },
                            Context: Context))
                            break;

                        originalObject?.AddPart(this);
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to load {nameof(BonesData)} for {nameof(SaveBonesInfo)}.{nameof(pickedBones.ID)} {pickedBones.ID}", x);
                    }
                }
                bonesBag?.Clear();

                if (replacement == null
                    && !originalObject.HasPart<UD_Bones_BrokenLunarRegent>())
                    originalObject?.AddPart(this);
            }
            int aroundLevel = GetForPlayerLevel();
            if (aroundLevel < 5)
                aroundLevel = 0;
            E.ReplacementObject = replacement
                ?? GetANonLegendaryCreature(
                    Where: delegate (GameObjectBlueprint model)
                    {
                        return !model.InheritsFromSafe(BASE_BROKEN_LUNAR_REGENT)
                            && model.GetPrimaryFaction() == originalPrimaryFaction
                            ;
                    },
                    Rnd: rnd,
                    AroundLevel: aroundLevel,
                    AllowAnimated: false)
                ?? GetANonLegendaryCreature(
                    Where: delegate (GameObjectBlueprint model)
                    {
                        return !model.InheritsFromSafe(BASE_BROKEN_LUNAR_REGENT)
                            && model.GetPrimaryFaction() == originalPrimaryFaction
                            ;
                    },
                    Rnd: rnd,
                    AllowAnimated: false)
                ?? GetANonLegendaryCreature(
                    Where: model => !model.InheritsFromSafe(BASE_BROKEN_LUNAR_REGENT),
                    Rnd: rnd,
                    AroundLevel: aroundLevel,
                    AllowAnimated: true)
                ?? GetANonLegendaryCreature(
                    Where: model => !model.InheritsFromSafe(BASE_BROKEN_LUNAR_REGENT),
                    Rnd: rnd,
                    AllowAnimated: true)
                ?? GameObject.Create("Dog")
                ;
            return base.HandleEvent(E);
        }

        public virtual bool HandleEvent(LoadLunarRegentEvent E)
        {
            if (ParentObject == E.LunarObject
                && E.CheckContextNot(PseudoZone.RECLAIM_CONTEXT))
            {
                if (ParentObject.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                {
                    BonesID = lunarRegentPart.BonesID;
                    lunarRegentPart.Broken = true;

                    if (!OriginalDescription.IsNullOrEmpty())
                        lunarRegentPart.AdjustBakedShortDesc(Postfix: $"\n\n{OriginalDescription}");
                }

                if (ParentObject.HasEffect<UD_Bones_MoonKingFever>())
                    ParentObject.RemoveEffect<UD_Bones_MoonKingFever>();

                if (!OriginalAllegience.IsNullOrEmpty())
                {
                    ParentObject.Brain.Mobile = true;
                    ParentObject.Brain.Factions = "";
                    ParentObject.Brain.Allegiance.Clear();

                    foreach ((var faction, var amount) in OriginalAllegience.IteratorSafe())
                        ParentObject.Brain.Allegiance.Add(faction, amount);

                    ParentObject.Brain.Allegiance.Hostile = OriginalAllegience.Hostile;
                    ParentObject.Brain.Allegiance.Calm = OriginalAllegience.Calm;

                    ParentObject.Brain.Opinions.Clear();
                    ParentObject.Brain.Goals.Clear();
                }

                if (ParentObject.RequirePart<ConversationScript>() is ConversationScript convoScript)
                    convoScript.ConversationID = "UD_Bones_LunarRegent_Broken";

                if (ParentObject.GetInventoryAndEquipment().FirstOrDefault(IsReliquaryOfThisRegent) is GameObject reliquary
                    && !ParentObject.Inventory.FireEvent(Event.New("CommandRemoveObject", "Object", reliquary)))
                    reliquary.Obliterate(Silent: true);

                ParentObject.PerformActionRecursively(go => go.RemovePart<UD_Bones_FragileLunarObject>());
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EnteredCellEvent E)
        {
            if (!HasEnteredCell
                && ParentObject != null)
            {
                HasEnteredCell = true;
                CachedSaveBonesInfo = null;

                if (ParentObject.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart)
                    && lunarRegentPart.GetSourceSaveBonesInfo() is SaveBonesInfo saveBonesInfo)
                {
                    saveBonesInfo.IncrementEncountered();
                    if (!HasIncremented)
                    {
                        HasIncremented = true;
                        lunarRegentPart.IncrementBroken();
                    }
                }

                if (CachedCourtiers != null
                    && CachedCourtiers.TryPullCachedLunarCourtiers(out CoalescibleSet<GameObject> lunarCourtiers))
                {
                    var partyCount = lunarCourtiers.Count;
                    var cellsList = E.Cell.GetConnectedSpawnLocations(partyCount * 2);
                    foreach (var lunarCourtier in lunarCourtiers)
                    {
                        if (cellsList.IsNullOrEmpty())
                        {
                            cellsList ??= new();
                            var prospectiveCell = E.Cell.getClosestEmptyCell() ?? E.Cell.getClosestPassableCell();
                            if (prospectiveCell != null)
                                cellsList.Add(prospectiveCell);
                        }

                        if (!cellsList.IsNullOrEmpty())
                        {
                            if (lunarCourtier.TryGetPart(out UD_Bones_LunarCourtier lunarCourtierPart)
                                && lunarCourtierPart.PerformAllyship(ParentObject, Force: true, Initial: true))
                            {
                                lunarCourtier.MakeActive();
                                lunarCourtier.Energy.BaseValue = 0;
                                var placementCell = cellsList.GetRandomElement();
                                placementCell.AddObject(lunarCourtier);
                                cellsList.Remove(placementCell);
                            }
                        }
                    }
                }
            }
            ParentObject?.RemovePart(this);
            return base.HandleEvent(E);
        }
    }
}

namespace UD_Bones_Folder.Mod
{
    [HarmonyDebug]
    [HarmonyPatch]
    [HasWishCommand]
    public static class ZoneBuilder_Patches
    {
        [WishCommand(Command = "show warden blueprints")]
        public static bool ShowWardenBlueprints_WishHandler(string Iterations)
        {
            Iterations ??= "10000";
            if (!int.TryParse(Iterations, out int iterations))
                return false;

            iterations = Math.Clamp(iterations, 1, 100000);

            Dictionary<string, int> blueprints = new();
            for (int i = 0; i < iterations; i++)
            {
                string bp = EncountersAPI.GetANonLegendaryCreatureBlueprint(ob
                    => (ob.HasPart("Body")
                        || ob.HasTagOrProperty("BodySubstitute"))
                    && (ob.HasPart("Combat")
                        || ob.HasTagOrProperty("BodySubstitute"))
                    && !ob.HasTag("Merchant")
                    && !ob.HasTag("ExcludeFromVillagePopulations"));

                if (!blueprints.ContainsKey(bp))
                    blueprints[bp] = 0;

                blueprints[bp]++;
            }

            List<KeyValuePair<int, string>> output = new();
            Utils.Log($"warden blueprints x {iterations:#,##0}:");
            foreach ((var blueprint, var count) in blueprints)
                output.Add(new(count, $"{blueprint}: {count:#,##0} ({(double)(((double)count / (double)iterations) * 100):#,#0.000}%)"));

            output = output.OrderByDescending(kvp => kvp.Key).ToList();
            foreach ((var count, var text) in output)
            {
                Utils.Log(text);
                if (text.Contains("Broken Lunar Regent"))
                    Popup.Show(text);
            }

            return true;
        }

        [WishCommand(Command = "show warden blueprints")]
        public static bool ShowWardenBlueprints_WishHandler()
            => ShowWardenBlueprints_WishHandler("10000")
            ;

        [WishCommand(Command = "tons of statues")]
        public static bool TonsOfStatues_WishHandler(string Iterations)
        {
            if (The.Player?.CurrentZone is not Zone currentZone)
                return false;

            Iterations ??= "1000";
            if (!int.TryParse(Iterations, out int iterations))
                return false;

            iterations = Math.Clamp(iterations, 1, 1000);

            int originalIterations = iterations;
            Utils.Log($"{nameof(TonsOfStatues_WishHandler)}, {nameof(iterations)}: {iterations}");
            foreach (var cell in currentZone.LoopCells())
            {
                if (iterations-- <= 0)
                    break;

                if (cell.HasObject(go => go.IsPlayer() || go.IsLedBy(The.Player)))
                {
                    //Utils.Log($"Skipping Cell: {cell?.DebugName ?? "NO_CELL"}");
                    iterations++;
                    continue;
                }

                try
                {
                    cell.Clear(Combat: true).AddObject("Random Stone Statue");
                    //Utils.Log($"{1.Indent()}iteration: {originalIterations - iterations}");
                }
                catch (Exception x)
                {
                    Utils.Error(nameof(TonsOfStatues_WishHandler), x);
                }
            }
            return true;
        }

        [WishCommand(Command = "tons of statues")]
        public static bool TonsOfStatues_WishHandler()
            => TonsOfStatues_WishHandler("1000")
            ;

        [WishCommand(Command = "tons of converts")]
        public static bool TonsOfConverts_WishHandler(string Iterations)
        {
            if (The.Player?.CurrentZone is not Zone currentZone)
                return false;

            Iterations ??= "100";
            if (!int.TryParse(Iterations, out int iterations))
                return false;

            iterations = Math.Clamp(iterations, 1, 100);

            int originalIterations = iterations;
            //Utils.Log($"{nameof(TonsOfStatues_WishHandler)}, {nameof(iterations)}: {iterations}");
            foreach (var cell in currentZone.LoopCells())
            {
                if (iterations-- <= 0)
                    break;

                if (cell.HasObject(go => go.IsPlayer() || go.IsLedBy(The.Player)))
                {
                    iterations++;
                    continue;
                }

                try
                {
                    cell.Clear(Combat: true).AddObject(GameObject.Create("Yd Freeholder Still"), System: true);
                    //Utils.Log($"{1.Indent()}iteration: {originalIterations - iterations}");
                }
                catch (Exception x)
                {
                    Utils.Error(nameof(TonsOfConverts_WishHandler), x);
                }
            }
            return true;
        }

        [WishCommand(Command = "tons of converts")]
        public static bool TonsOfConverts_WishHandler()
            => TonsOfConverts_WishHandler("100")
            ;

        [HarmonyPatch(
            declaringType: typeof(Village),
            methodName: nameof(Village.generateWarden))]
        [HarmonyPostfix]
        public static void generateWarden_LogVillageLocation_Postfix(GameObject __result, Zone ___zone)
        {
            if (__result.IsLunarRegent())
                Utils.Log($"{nameof(Village)}.{nameof(Village.generateWarden)}({nameof(GameObject)}: {__result.DebugName}, {nameof(Zone.ZoneID)}: {___zone?.ZoneID})");
        }

        [HarmonyPatch(
            declaringType: typeof(Village),
            methodName: nameof(Village.generateMayor))]
        [HarmonyPostfix]
        public static void generateMayor_LogVillageLocation_Postfix(GameObject __result, Zone ___zone)
        {
            if (__result.IsLunarRegent())
                Utils.Log($"{nameof(Village)}.{nameof(Village.generateMayor)}({nameof(GameObject)}: {__result.DebugName}, {nameof(Zone.ZoneID)}: {___zone?.ZoneID})");
        }

        [HarmonyPatch(
            declaringType: typeof(Village),
            methodName: nameof(Village.generateMerchant))]
        [HarmonyPostfix]
        public static void generateMerchant_LogVillageLocation_Postfix(GameObject __result, Zone ___zone)
        {
            if (__result.IsLunarRegent())
                Utils.Log($"{nameof(Village)}.{nameof(Village.generateMerchant)}({nameof(GameObject)}: {__result.DebugName}, {nameof(Zone.ZoneID)}: {___zone?.ZoneID})");
        }

        [HarmonyPatch(
            declaringType: typeof(Village),
            methodName: nameof(Village.generateImmigrant))]
        [HarmonyPostfix]
        public static void generateImmigrant_LogVillageLocation_Postfix(GameObject __result, Zone ___zone)
        {
            if (__result.IsLunarRegent())
                Utils.Log($"{nameof(Village)}.{nameof(Village.generateImmigrant)}({nameof(GameObject)}: {__result.DebugName}, {nameof(Zone.ZoneID)}: {___zone?.ZoneID})");
        }

        [HarmonyPatch(
            declaringType: typeof(Village),
            methodName: nameof(Village.generateTinker))]
        [HarmonyPostfix]
        public static void generateTinker_LogVillageLocation_Postfix(GameObject __result, Zone ___zone)
        {
            if (__result.IsLunarRegent())
                Utils.Log($"{nameof(Village)}.{nameof(Village.generateTinker)}({nameof(GameObject)}: {__result.DebugName}, {nameof(Zone.ZoneID)}: {___zone?.ZoneID})");
        }

        [HarmonyPatch(
            declaringType: typeof(Village),
            methodName: nameof(Village.generateApothecary))]
        [HarmonyPostfix]
        public static void generateApothecary_LogVillageLocation_Postfix(GameObject __result, Zone ___zone)
        {
            if (__result.IsLunarRegent())
                Utils.Log($"{nameof(Village)}.{nameof(Village.generateApothecary)}({nameof(GameObject)}: {__result.DebugName}, {nameof(Zone.ZoneID)}: {___zone?.ZoneID})");
        }

        [HarmonyPatch(
            declaringType: typeof(VillageBase),
            methodName: nameof(Village.generateVillager))]
        [HarmonyPostfix]
        public static void generateVillager_LogVillageLocation_Postfix(GameObject __result, Zone ___zone)
        {
            if (__result.IsLunarRegent())
                Utils.Log($"{nameof(VillageBase)}.{nameof(VillageBase.generateVillager)}({nameof(GameObject)}: {__result.DebugName}, {nameof(Zone.ZoneID)}: {___zone?.ZoneID})");
        }
    }
}
