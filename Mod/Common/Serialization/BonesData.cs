using System;
using System.Linq;
using System.Collections.Generic;

using Kobold;

using XRL;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.Effects;
using XRL.World.Parts;

using static XRL.World.Cell;

using static UD_Bones_Folder.Mod.SerializationExtensions;
using XRL.Core;
using XRL.Collections;
using UD_Bones_Folder.Mod.Events;
using XRL.World.ZoneParts;
using XRL.World.Capabilities;
using XRL.World.Parts.Skill;
using XRL.World.Parts.Mutation;
using UD_Bones_Folder.Mod.Serialization.PseudoTypes;
using UD_Bones_Folder.Mod.BonesSystem;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class BonesData : IComposite, IDisposable
    {
        public static BonesManager BonesManager => BonesManager.System;
        public string BonesID;
        public Zone BonesZone;
        public PseudoZone PseudoZone;

        public Guid OsseousAshID;
        public string OsseousAshHandle;

        public BonesData()
        { }

        public BonesData(string BonesID, Zone BonesZone, Guid? OsseousAshID = null, string OsseousAshHandle = null)
            : this()
        {
            this.BonesID = BonesID;
            this.BonesZone = BonesZone;

            if (OsseousAshID != null
                && !OsseousAshHandle.IsNullOrEmpty())
            {
                this.OsseousAshID = OsseousAshID.GetValueOrDefault();
                this.OsseousAshHandle = OsseousAshHandle;
            }
        }

        public static BonesData GetFromSaveBonesInfo(SaveBonesInfo SaveBonesInfo, bool NoStatus = false)
            => BonesManager?.ExhumeLunarRegent(SaveBonesInfo, NoStatus)
            ;


        public GameObject ExtractLunarRegent(
            SaveBonesInfo BonesInfo,
            out LunarPartyIDs CachedLunarCourtiers,
            out bool Blocked,
            Action<GameObject> ProcPreLoad = null,
            Action<GameObject> ProcPostLoad = null
            )
        {
            CachedLunarCourtiers = null;
            Blocked = false;

            if (PseudoZone != null)
            {
                if (!PseudoZone.TryExtractLunarParty(
                    BonesInfo: BonesInfo,
                    LunarParty: out LunarParty lunarParty,
                    Blocked: out Blocked,
                    ProcPreLoad: ProcPreLoad,
                    ProcPostLoad: ProcPostLoad)
                    || Blocked)
                    return null;

                CachedLunarCourtiers = lunarParty?.CacheLunarCourtiers();

                return lunarParty?.LunarRegent;
            }

            if (BonesZone != null)
            {
                if (ExtractLunarParty(
                    BonesInfo: BonesInfo,
                    ProcPreLoad: ProcPreLoad,
                    ProcPostLoad: ProcPostLoad) is LunarParty lunarParty)
                {
                    if (lunarParty?.LunarRegent != null)
                    {
                        CachedLunarCourtiers = lunarParty?.CacheLunarCourtiers();
                        return lunarParty?.LunarRegent;
                    }
                }
            }

            return null;
        }

        public bool TryExtractLunarRegent(
            SaveBonesInfo BonesInfo,
            out GameObject LunarRegent,
            out LunarPartyIDs CachedLunarCourtiers,
            out bool Blocked,
            Action<GameObject> ProcPreLoad = null,
            Action<GameObject> ProcPostLoad = null
            )
            => (LunarRegent = ExtractLunarRegent(
                BonesInfo: BonesInfo,
                CachedLunarCourtiers: out CachedLunarCourtiers,
                Blocked: out Blocked,
                ProcPreLoad: ProcPreLoad,
                ProcPostLoad: ProcPostLoad)) != null
                && !Blocked
            ;

        public LunarParty ExtractLunarParty(
            SaveBonesInfo BonesInfo,
            Action<GameObject> ProcPreLoad = null,
            Action<GameObject> ProcPostLoad = null
            )
        {
            if (BonesZone == null)
                return null;

            using var crossGameObjects = ScopeDisposedList<CrossGameObject>.GetFromPool();

            var lunarParty = new LunarParty();
            foreach (var bonesCell in BonesZone.GetCells())
            {
                if (bonesCell.Objects is not ObjectRack bonesObjects)
                    continue;

                for (int i = 0; i < bonesObjects.Count; i++)
                {
                    if (bonesObjects[i] is GameObject bonesObject)
                    {
                        string bonesObjectDebugName = bonesObject.DebugName;
                        string catchFlag = "top";
                        try
                        {
                            if (!bonesObject.IsLunarRegent(BonesID)
                                && !bonesObject.IsLunarCourtier(BonesID))
                                continue;

                            var crossGameObject = CrossGameObject.CreateFrom(bonesObject);

                            bonesObject = crossGameObject.Clone;

                            // Anything you want to do to objects, do it AFTER here
                            // ####################################################

                            int serializedBaseID = 0;
                            bonesObject.PerformActionRecursively(
                                Action: delegate (GameObject go)
                                {
                                    if (serializedBaseID == 0)
                                        serializedBaseID = crossGameObject.Original.BaseID;
                                    else
                                        serializedBaseID = go.BaseID;

                                    go.AddPart(new UD_Bones_ReportBones
                                    {
                                        LoadedBonesID = BonesID,
                                        SerializedBaseID = serializedBaseID,
                                    });
                                });

                            TransmuteBrain(crossGameObject.Original, crossGameObject.Clone, BonesZone, null);

                            catchFlag = nameof(Extensions.ApplyRegistrar);
                            bonesObject.ApplyRegistrar();

                            catchFlag = nameof(Extensions.TryFeverWarp);
                            bonesObject.TryFeverWarp(BonesInfo);

                            if (bonesObject.TryGetPart(out GivesRep givesRep))
                                givesRep.wasParleyed = false;

                            if (bonesObject.IsLunarRegent(BonesID))
                            {
                                if (bonesObject.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                                    if (lunarRegentPart.BonesID == BonesID)
                                        lunarParty.LunarRegent = bonesObject;

                                ProcPreLoad?.Invoke(bonesObject);

                                LoadLunarRegentEvent.Send(
                                    BonesInfo: BonesInfo,
                                    LunarObject: bonesObject,
                                    Context: PseudoZone.EXTRACT_CONTEXT);
                                AfterLoadedLunarRegentEvent.Send(
                                    BonesInfo: BonesInfo,
                                    LunarObject: bonesObject,
                                    Context: PseudoZone.EXTRACT_CONTEXT);

                                ProcPostLoad?.Invoke(bonesObject);
                            }

                            if (bonesObject.IsLunarCourtier(BonesID))
                            {
                                lunarParty.LunarCourtiers ??= new();
                                lunarParty.LunarCourtiers.Add(bonesObject);
                            }

                            catchFlag = nameof(GameObject.CurrentCell);
                            if (bonesObject.CurrentCell is Cell oldBonesCell)
                                oldBonesCell.RemoveObject(bonesObject);
                        }
                        catch (Exception x)
                        {
                            Utils.Error($"{nameof(bonesObjects)}[{i}] ({catchFlag}): {bonesObjectDebugName}", x);
                        }
                    }
                }
            }

            using (var courtiersToRemove = ScopeDisposedList<GameObject>.GetFromPool())
            {
                foreach (var lunarCourtier in lunarParty.LunarCourtiers.IteratorSafe())
                {
                    if (!EarlyBeforeLoadLunarCourtierEvent.Check(
                        BonesInfo: BonesInfo,
                        LunarObject: lunarCourtier,
                        LunarRegent: lunarParty.LunarRegent,
                        Context: PseudoZone.EXTRACT_CONTEXT)
                        || !BeforeLoadLunarCourtierEvent.Check(
                            BonesInfo: BonesInfo,
                            LunarObject: lunarCourtier,
                            LunarRegent: lunarParty.LunarRegent,
                            Context: PseudoZone.EXTRACT_CONTEXT))
                    {
                        courtiersToRemove.Add(lunarCourtier);
                        continue;
                    }

                    LoadLunarCourtierEvent.Send(
                        BonesInfo: BonesInfo,
                        LunarObject: lunarCourtier,
                        LunarRegent: lunarParty.LunarRegent,
                        Context: PseudoZone.EXTRACT_CONTEXT);
                    AfterLoadedLunarCourtierEvent.Send(
                        BonesInfo: BonesInfo,
                        LunarObject: lunarCourtier,
                        LunarRegent: lunarParty.LunarRegent,
                        Context: PseudoZone.EXTRACT_CONTEXT);
                }
                foreach (var courtierToRemove in courtiersToRemove)
                {
                    lunarParty.LunarCourtiers?.Remove(courtierToRemove);
                    courtierToRemove?.Obliterate();
                }
            }

            foreach (var crossGameObject in crossGameObjects)
            {
                if (crossGameObject.Clone is not GameObject clone
                    || crossGameObject.Original is not GameObject original)
                    continue;

                if (crossGameObject.Clone == lunarParty.LunarRegent)
                    continue;

                if (lunarParty.LunarCourtiers.IsNullOrEmpty()
                    || lunarParty.LunarCourtiers.All(courtier => courtier != crossGameObject.Clone))
                    continue;

                //Transmutation.TransmuteBrain(original, clone);
            }

            crossGameObjects?.Clear();

            if (lunarParty.LunarRegent == null)
            {
                lunarParty.Dispose();
                return null;
            }
            //AfterBonesZoneLoadedEvent.Send(BonesZone, BonesID, lunarParty?.LunarRegent, BonesZone);
            return lunarParty;
        }

        public bool TryExtractLunarParty(out LunarParty LunarParty, SaveBonesInfo BonesInfo)
            => (LunarParty = ExtractLunarParty(BonesInfo)) != null
            ;

        public LunarPartyIDs CacheLunarParty(SaveBonesInfo BonesInfo)
        {
            using var lunarParty = ExtractLunarParty(BonesInfo);
            return lunarParty?.CacheLunarParty();
        }

        public bool TryCacheLunarParty(out LunarPartyIDs LunarPartyIDs, SaveBonesInfo BonesInfo)
            => (LunarPartyIDs = CacheLunarParty(BonesInfo)) != null
            ;

        public bool Apply(
            Zone Zone,
            SaveBonesInfo BonesInfo,
            ZoneBonesAllocation.AllocationTypes Type,
            out GameObject LunarRegent,
            out bool Blocked
            )
        {
            LunarRegent = null;
            Blocked = false;

            //Utils.Log($"{nameof(BonesData)}.{nameof(Apply)}({nameof(Zone)}: {Zone?.ZoneID ?? "NO_ZONE"})");
            if (Zone.GetZoneProperty(nameof(BonesID), null) is string existingBonesID)
            {
                if (existingBonesID != BonesID)
                    Utils.Warn($"Loading {nameof(SaveBonesInfo)} for zone that has already loaded a different bones: " +
                        $"{nameof(existingBonesID)} {existingBonesID}, {nameof(BonesData)}.{nameof(BonesID)} {BonesID}. " +
                        $"Zone may have errors.");
                else
                    Utils.Warn($"{nameof(SaveBonesInfo)} for zone that has already loaded this bones: " +
                        $"{nameof(existingBonesID)} {existingBonesID}, {nameof(BonesData)}.{nameof(BonesID)} {BonesID}. " +
                        $"Zone may have errors.");
            }

            Zone.SetZoneProperty(nameof(BonesID), BonesID);
            if (PseudoZone != null)
                return ApplyPseudoZone(Zone, BonesInfo, Type, out LunarRegent, out Blocked);

            if (BonesZone != null)
                return ApplyZone(Zone, BonesInfo, Type, out LunarRegent, BonesInfo.IsMad);

            return false;
        }

        public bool ApplyPseudoZone(
            Zone Zone,
            SaveBonesInfo BonesInfo,
            ZoneBonesAllocation.AllocationTypes Type,
            out GameObject LunarRegent,
            out bool Blocked
            )
        {
            LunarRegent = null;
            Blocked = false;
            if (PseudoZone == null)
                return false;

            //Utils.Log($"{nameof(BonesData)}.{nameof(ApplyPseudoZone)}({nameof(Zone)}: {Zone?.ZoneID ?? "NO_ZONE"})");
            if (!PseudoZone.TryApplyToZone(
                Zone: Zone,
                BonesInfo: BonesInfo,
                Type: Type,
                LunarRegent: out LunarRegent,
                Blocked: out Blocked)
                || Blocked)
                return false;

            return true;
        }

        public bool ApplyZone(
            Zone Zone,
            SaveBonesInfo BonesInfo,
            ZoneBonesAllocation.AllocationTypes Type,
            out GameObject LunarRegent,
            bool IsMad
            )
        {
            LunarRegent = null;
            if (BonesZone == null)
                return false;

            foreach (var zonePart in BonesZone.Parts.IteratorSafe())
                Zone.AddPart(zonePart, true);

            Dictionary<string, LunarParty> lunarParties = null;
            using var crossGameObjects = ScopeDisposedList<CrossGameObject>.GetFromPool();
            foreach (var cell in Zone.GetCells())
            {
                if (BonesZone.GetCell(cell.Location) is not Cell bonesCell)
                    continue;

                cell.Clear(Important: true, Combat: true, alsoExclude: go => go.HasPart<GameUnique>());

                cell.PaintTile = bonesCell.PaintTile;
                cell.PaintTileColor = bonesCell.PaintTileColor;
                cell.PaintRenderString = bonesCell.PaintRenderString;
                cell.PaintColorString = bonesCell.PaintColorString;
                cell.PaintDetailColor = bonesCell.PaintDetailColor;

                if (bonesCell.Objects is not ObjectRack bonesObjects)
                    continue;

                for (int i = 0; i < bonesObjects.Count; i++)
                {
                    if (bonesObjects[i] is GameObject bonesObject)
                    {
                        string bonesObjectDebugName = bonesObject.DebugName;
                        string catchFlag = "top";
                        try
                        {
                            var crossGameObject = CrossGameObject.CreateFrom(bonesObject);
                            crossGameObjects.Add(crossGameObject);

                            bonesObject = crossGameObject.Clone;

                            // Anything you want to do to objects, do it AFTER here
                            // ####################################################

                            int serializedBaseID = 0;
                            bonesObject.PerformActionRecursively(
                                Action: delegate (GameObject go)
                                {
                                    if (serializedBaseID == 0)
                                        serializedBaseID = crossGameObject.Original.BaseID;
                                    else
                                        serializedBaseID = go.BaseID;

                                    go.AddPart(new UD_Bones_ReportBones
                                    {
                                        LoadedBonesID = BonesID,
                                        SerializedBaseID = serializedBaseID,
                                    });
                                });

                            TransmuteBrain(crossGameObject.Original, crossGameObject.Clone, BonesZone, Zone);

                            catchFlag = nameof(Extensions.ApplyRegistrar);
                            bonesObject.ApplyRegistrar();

                            catchFlag = nameof(Extensions.TryFeverWarp);
                            bonesObject.TryFeverWarp(BonesInfo);

                            if (bonesObject.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                            {
                                lunarParties ??= new();
                                if (!lunarParties.TryGetValue(lunarRegentPart.BonesID, out var lunarParty))
                                    lunarParty = lunarParties[lunarRegentPart.BonesID] = new();
                                lunarParty.LunarRegent = bonesObject;
                            }
                            else
                            if (bonesObject.TryGetPart(out UD_Bones_LunarCourtier lunarCoutierPart))
                            {
                                lunarParties ??= new();
                                if (!lunarParties.TryGetValue(lunarCoutierPart.BonesID, out var lunarParty))
                                    lunarParty = lunarParties[lunarCoutierPart.BonesID] = new();

                                lunarParty.LunarCourtiers ??= new();
                                lunarParty.LunarCourtiers.Add(bonesObject);
                            }

                            if (bonesObject.TryGetPart(out GivesRep givesRep))
                                givesRep.wasParleyed = false;

                            catchFlag = $"{nameof(Extensions.IsLunarRegent)}?";
                            if (bonesObject.IsLunarRegent(BonesID))
                            {
                                catchFlag = $"{nameof(Extensions.IsLunarRegent)}: true";
                                LunarRegent = bonesObject;

                                bonesObject.SetStringProperty("OriginalPlayerBody", null, RemoveIfNull: true);

                                if (!GameObjectFactory.Factory.HasBlueprint(LunarRegent.Blueprint))
                                {
                                    LunarRegent.Blueprint = "Lunar Regent";
                                    IsMad = true;
                                }

                                catchFlag = $"{nameof(IsMad)}?";
                                if (IsMad)
                                {
                                    catchFlag = $"{nameof(IsMad)}: true";
                                    LunarRegent.Render.Tile = Const.MAD_LUNAR_REGENT_TILE;
                                    if (LunarRegent.TryGetPart(out lunarRegentPart))
                                        lunarRegentPart.IsMad = true;
                                }
                                else
                                    catchFlag = $"{nameof(IsMad)}: false";

                                LunarRegent?.AddOpinion<OpinionMollify>(The.Player);
                                The.Player.AddOpinion<OpinionMollify>(LunarRegent);

                                catchFlag = nameof(Description);
                                if (LunarRegent.TryGetPart(out Description description))
                                {
                                    string whoItWas = OsseousAshHandle ?? OsseousAsh.DefaultOsseousAshHandle;
                                    if (OsseousAsh.Config == null
                                        || OsseousAsh.Config.ID == OsseousAshID)
                                        whoItWas = $"you";

                                    description._Short = $"It was {whoItWas}.";
                                }

                                catchFlag = $"{nameof(BonesManager.BonesFileName)}AttitudeSetup";
                                var attitudeSetup = Event.New($"{nameof(BonesManager.BonesFileName)}AttitudeSetup")
                                    .SetParameter(nameof(LunarRegent), LunarRegent)
                                    .SetParameter(nameof(The.Player), The.Player);

                                catchFlag = nameof(GameObject.FireEvent);
                                if (The.Player.FireEvent(attitudeSetup))
                                {
                                    var brain = LunarRegent.Brain;
                                    brain?.PushGoal(new Kill(The.Player));
                                    ApplyHostility(The.Player, brain, 0);
                                }

                                if (LunarRegent.Render is Render render)
                                    render.Visible = true;
                            }
                            else
                                catchFlag = $"{nameof(Extensions.IsLunarRegent)}: false";

                            catchFlag = nameof(GameObject.CurrentCell);
                            if (bonesObject.CurrentCell is Cell oldBonesCell)
                                oldBonesCell.RemoveObject(bonesObject);

                            catchFlag = nameof(Cell.AddObject);
                            cell.AddObject(bonesObject);

                            catchFlag = nameof(GameObject.MakeActive);
                            bonesObject.MakeActive();

                            catchFlag = nameof(GameObject.ForfeitTurn);
                            bonesObject.ForfeitTurn();
                        }
                        catch (Exception x)
                        {
                            Utils.Error($"{nameof(bonesObjects)}[{i}] ({catchFlag}): {bonesObjectDebugName}", x);
                        }
                    }
                }
            }

            if (!lunarParties.IsNullOrEmpty())
            {
                foreach ((var regentID, var lunarParty) in lunarParties)
                {
                    if (lunarParty.LunarRegent == null)
                        continue;

                    if (regentID != BonesID)
                        continue;

                    if (lunarParty.LunarCourtiers is HashSet<GameObject> lunarCourtiers)
                        foreach (var lunarCourtier in lunarCourtiers)
                            if (lunarCourtier.TryGetPart(out UD_Bones_LunarCourtier lunarCourtierPart))
                                lunarCourtierPart.PerformAllyship(lunarParty.LunarRegent, Force: true, Initial: true);
                }
            }

            foreach (var crossGameObject in crossGameObjects)
            {
                if (crossGameObject.Clone is not GameObject clone
                    || crossGameObject.Original is not GameObject original)
                    continue;

                if (crossGameObject.Clone == LunarRegent)
                    continue;

                if (lunarParties.IsNullOrEmpty())
                    continue;

                if (!lunarParties.TryGetValue(BonesID, out var lunarParty)
                    || lunarParty.LunarCourtiers.IsNullOrEmpty()
                    || lunarParty.LunarCourtiers.All(courtier => courtier != crossGameObject.Clone))
                    continue;

                //Transmutation.TransmuteBrain(original, clone);
            }

            foreach ((var bonesID, var lunarParty) in lunarParties.IteratorSafe())
                lunarParty.Dispose();
            lunarParties?.Clear();

            crossGameObjects?.Clear();

            AfterBonesZoneLoadedEvent.Send(Zone, BonesID, LunarRegent, BonesZone);
            return LunarRegent != null;
        }

        public static void ApplyHostility(GameObject Actor, Brain Brain, int Depth)
        {
            if (Actor == null)
                return;
            if (Depth >= 100)
                return;

            Brain.AddOpinion<OpinionInscrutable>(Actor);

            ApplyHostility(Actor.PartyLeader, Brain, Depth + 1);

            if (Actor.TryGetEffect<Dominated>(out var Effect))
                ApplyHostility(Effect.Dominator, Brain, Depth + 1);
        }

        public static void TransmuteBrain(
            GameObject Original,
            GameObject Clone,
            Zone BonesZone,
            Zone NewZone
            )
        {
            if (Original.Brain is not Brain originalBrain
                || Clone.Brain is not Brain cloneBrain)
                return;

            Clone.PartyLeader = Original.PartyLeader;
            if (!originalBrain.PartyMembers.IsNullOrEmpty())
                foreach ((int partyMemberID, PartyMember partyMember) in originalBrain.PartyMembers)
                    cloneBrain.PartyMembers[partyMemberID] = partyMember;

            if (!originalBrain.Allegiance.IsNullOrEmpty())
                (originalBrain.Allegiance, cloneBrain.Allegiance) = (cloneBrain.Allegiance, originalBrain.Allegiance);

            TransferPartyInZone(Original, Clone, BonesZone);
            TransferPartyInZone(Original, Clone, NewZone);
        }

        public static void TransferPartyInZone(GameObject Original, GameObject Clone, Zone Zone)
        {
            if (Zone == null)
                return;

            foreach (GameObject gameObject in Zone.YieldObjects())
            {
                if (gameObject.PartyLeader != Original)
                    continue;

                if (gameObject.TryGetEffect<Proselytized>(out var proselytized))
                {
                    if (!Clone.HasPart<Persuasion_Proselytize>())
                        continue;

                    proselytized.Proselytizer = Clone;
                }

                if (gameObject.TryGetEffect<Beguiled>(out var beguiled))
                {
                    if (!Clone.HasPart<Beguiling>())
                        continue;

                    beguiled.Beguiler = Clone;
                }

                gameObject.PartyLeader = Clone;
                gameObject.Brain.Goals.Clear();
            }
        }

        public void Cremate()
            => BonesManager?.CremateLunarRegent(BonesID)
            ;

        public void Dispose()
        {
            BonesID = null;
            BonesZone?.Release();
            BonesZone = null;
            PseudoZone?.Dispose();
            PseudoZone = null;
            OsseousAshID = default;
            OsseousAshHandle = null;
        }
    }
}
