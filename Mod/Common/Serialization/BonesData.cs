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

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class BonesData : IComposite
    {
        public static BonesManager BonesManager => BonesManager.System;
        public string BonesID;
        public string ZoneID;
        public Zone BonesZone;

        public BonesData()
        { }

        public BonesData(string BonesID, string ZoneID, Zone BonesZone)
            : this()
        {
            this.BonesID = BonesID;
            this.ZoneID = ZoneID;
            this.BonesZone = BonesZone;
        }

        public static BonesData GetFromSaveBonesInfo(string ZoneID, SaveBonesInfo SaveBonesInfo)
            => BonesManager?.ExhumeMoonKing(ZoneID, SaveBonesInfo)
            ;

        public bool Apply(Zone Zone, out GameObject LunarRegent, bool IsMad)
        {
            LunarRegent = null;
            if (BonesZone == null)
                return false;

            BonesZone.ZoneID = XRL.World.ZoneID.Assemble("BonesWorld", BonesZone.wX, BonesZone.wY, BonesZone.X, BonesZone.Y, BonesZone.Z);

            foreach (var zonePart in BonesZone.Parts ?? Enumerable.Empty<IZonePart>())
                Zone.AddPart(zonePart, true);


            Dictionary<string, GameObject> lunarRegents = null;
            Dictionary<string, HashSet<UD_Bones_LunarCourtier>> lunarRegentCompanions = null;
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
                            bonesObject = bonesObject.DeepCopy(CopyEffects: true, CopyID: false);

                            // Anything you want to do to objects, do it AFTER here
                            // ####################################################

                            catchFlag = nameof(Extensions.ApplyRegistrar);
                            bonesObject.ApplyRegistrar();

                            catchFlag = nameof(Extensions.FeverWarp);
                            bonesObject.FeverWarp(BonesID);

                            if (bonesObject.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                            {
                                lunarRegents ??= new();
                                lunarRegents[lunarRegentPart.BonesID] = bonesObject;
                            }
                            else
                            if (bonesObject.TryGetPart(out UD_Bones_LunarCourtier lunarCoutierPart))
                            {
                                lunarRegentCompanions ??= new();

                                if (!lunarRegentCompanions.ContainsKey(lunarCoutierPart.BonesID)
                                    || lunarRegentCompanions[lunarCoutierPart.BonesID] == null)
                                    lunarRegentCompanions[lunarCoutierPart.BonesID] = new();

                                lunarRegentCompanions[lunarCoutierPart.BonesID].Add(lunarCoutierPart);
                            }

                            if (bonesObject.TryGetPart(out GivesRep givesRep))
                                givesRep.wasParleyed = false;

                            catchFlag = $"{nameof(Extensions.IsMoonKing)}?";
                            if (bonesObject.IsMoonKing(BonesID))
                            {
                                catchFlag = $"{nameof(Extensions.IsMoonKing)}: true";
                                LunarRegent = bonesObject;

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
                                    string whoItWas = OsseousAsh.DefaultOsseousAshHandle;
                                    if (OsseousAsh.Config != null)
                                        whoItWas = $"=OsseousAshHandle:{OsseousAsh.Config.ID}:Handle:{OsseousAsh.Config.Handle}=";

                                    description._Short = $"It was {whoItWas}.";
                                }

                                catchFlag = $"{nameof(UD_Bones_BonesSaver.BonesName)}AttitudeSetup";
                                var attitudeSetup = Event.New($"{nameof(UD_Bones_BonesSaver.BonesName)}AttitudeSetup")
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
                                catchFlag = $"{nameof(Extensions.IsMoonKing)}: false";

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

            if (!lunarRegents.IsNullOrEmpty()
                && !lunarRegentCompanions.IsNullOrEmpty())
                foreach ((var regentID , var lunarRegent) in lunarRegents)
                    if (lunarRegentCompanions.TryGetValue(regentID, out var lunarCompanions)
                        && !lunarCompanions.IsNullOrEmpty())
                        foreach (var lunarCompanion in lunarCompanions)
                            lunarCompanion.PerformAllyship(lunarRegent, Force: true, Initial: true);

            lunarRegents?.Clear();
            lunarRegentCompanions?.Clear();

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

        public void Cremate()
            => BonesManager?.CremateMoonKing(BonesID)
            ;
    }
}
