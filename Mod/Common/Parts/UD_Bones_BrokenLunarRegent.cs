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

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_BrokenLunarRegent : IScribedPart
    {
        private static IEnumerable<SaveBonesInfo> CachedSaveBonesInfo;

        public LunarPartyIDs CachedParty;

        public string BonesID;

        public bool HasEnteredCell;

        public bool PreferMad;

        public bool ExcludeMad;

        public int ForPlayerLevel;

        public bool HasIncremented;

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == BeforeObjectCreatedEvent.ID
            || ID == EnteredCellEvent.ID
            ;

        public static bool BonesNotEncounteredOrFailedAlready(SaveBonesInfo BonesInfo)
            => !BonesManager.System.Encountered.Contains(BonesInfo.ID)
            && !BonesManager.System.FailedToLoadBones.Contains(BonesInfo.ID)
            ;

        public bool IsNotExcludeMadOrNotMad(SaveBonesInfo BonesInfo)
            => !ExcludeMad
            || !BonesInfo.IsMad
            ;

        public bool IsWithinDefinedLevel(SaveBonesInfo BonesInfo)
            => ForPlayerLevel == 0
            || BonesInfo.BonesSpec is not BonesSpec bonesSpec
            || BonesSpec.IsWithinLevel(bonesSpec.Level, ForPlayerLevel)
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

        public override bool HandleEvent(BeforeObjectCreatedEvent E)
        {
            GameObject replacement = null;

            if (CachedSaveBonesInfo.IsNullOrEmpty())
                CachedSaveBonesInfo = BonesManager.System.GetEligibleSaveBonesInfo();

            if (CachedSaveBonesInfo.Where(MatchesSpec) is IEnumerable<SaveBonesInfo> saveBonesInfos
                && !saveBonesInfos.IsNullOrEmpty())
            {
                var bonesBag = new BallBag<SaveBonesInfo>();
                //int attempts = 0;
                foreach (var bonesInfo in saveBonesInfos)
                    if (bonesInfo.GetBonesWeight() is int weight)
                        bonesBag.Add(bonesInfo, weight);

                int biggestWeight = bonesBag.Aggregate(0, (a, n) => Math.Max(a, bonesBag[n]));
                for (int i = 0; i < bonesBag.Count; i++)
                {
                    try
                    {
                        if (bonesBag[i] is SaveBonesInfo bonesInfo
                            && bonesBag[bonesInfo] is int weight)
                        {
                            if (PreferMad
                                && bonesInfo.IsMad)
                                bonesBag[bonesInfo] += biggestWeight;
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Warn("Issue while prefering Mad Lunar Regents", x);
                    }
                }

                while (!bonesBag.IsNullOrEmpty()
                    && bonesBag.Count > 10 // prevents exauhsting bones before the player has an oportunity to fight them
                    && bonesBag.PickOne() is SaveBonesInfo pickedBones
                    //&& attempts++ < 50
                    )
                {
                    try
                    {
                        if (BonesManager.System.AttemptLoadBones(
                            ObjectID: ParentObject.ID,
                            PickedBones: pickedBones,
                            LunarRegent: out replacement,
                            CachedParty: out CachedParty,
                            PostLoad: delegate (GameObject lunarRegent)
                            {
                                if (lunarRegent.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                                {
                                    BonesID = lunarRegentPart.BonesID;
                                    lunarRegentPart.Broken = true;
                                }

                                if (lunarRegent.HasEffect<UD_Bones_MoonKingFever>())
                                    lunarRegent.RemoveEffect<UD_Bones_MoonKingFever>();

                                lunarRegent.Brain.Mobile = true;
                                lunarRegent.Brain.Factions = "";
                                lunarRegent.Brain.Allegiance.Clear();
                                foreach ((var faction, var amount) in ParentObject.Brain.Allegiance)
                                    lunarRegent.Brain.Allegiance.Add(faction, amount);
                                lunarRegent.Brain.Allegiance.Hostile = ParentObject.Brain.Allegiance.Hostile;
                                lunarRegent.Brain.Allegiance.Calm = ParentObject.Brain.Allegiance.Calm;
                                lunarRegent.Brain.Opinions.Clear();
                                lunarRegent.Brain.Goals.Clear();

                                lunarRegent.AddPart(this);

                                if (lunarRegent.GetInventoryAndEquipment().FirstOrDefault(IsReliquaryOfThisRegent) is GameObject reliquary
                                    && !lunarRegent.Inventory.FireEvent(Event.New("CommandRemoveObject", "Object", reliquary)))
                                    reliquary.Obliterate(Silent: true);

                                lunarRegent.PerformActionRecursively(go => go.RemovePart<UD_Bones_FragileLunarObject>());
                            }))
                        {
                            if (!BonesManager.System.Encountered.Contains(BonesID))
                                BonesManager.System.Encountered.Add(BonesID);

                            break;
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to load {nameof(BonesData)} for {nameof(SaveBonesInfo)}.{nameof(pickedBones.ID)} {pickedBones.ID}", x);
                    }
                }
                bonesBag?.Clear();
            }
            E.ReplacementObject = replacement ?? EncountersAPI.GetANonLegendaryCreature(model => !model.InheritsFromSafe("Broken Lunar Regent"));
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EnteredCellEvent E)
        {
            if (!HasEnteredCell)
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

                if (CachedParty != null
                    && CachedParty.TryPullCachedLunarCourtiers(out HashSet<GameObject> lunarCourtiers))
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
                                var placementCell = cellsList.GetRandomElement();
                                placementCell.AddObject(lunarCourtier);
                                cellsList.Remove(placementCell);
                            }
                        }
                    }
                }
            }
            ParentObject.RemovePart(this);
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
