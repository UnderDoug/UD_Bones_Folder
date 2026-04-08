using System;
using System.Linq;

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

        public bool Apply(Zone Zone, out GameObject MoonKing, bool IsMad)
        {
            MoonKing = null;
            if (BonesZone == null)
                return false;

            BonesZone.ZoneID = XRL.World.ZoneID.Assemble("BonesWorld", BonesZone.wX, BonesZone.wY, BonesZone.X, BonesZone.Y, BonesZone.Z);

            if (BonesZone.Parts != null)
            {
                foreach (var zonePart in BonesZone.Parts)
                {
                    Zone.Parts ??= new();
                    Zone.AddPart(zonePart, true);
                }
            }

            foreach (var cell in Zone.GetCells())
            {
                cell.Clear(Important: true, Combat: true);

                if (BonesZone.GetCell(cell.Location) is not Cell bonesCell)
                    continue;

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
                            catchFlag = nameof(Extensions.ApplyRegistrar);
                            bonesObject.ApplyRegistrar();

                            catchFlag = nameof(Extensions.FeverWarp);
                            bonesObject.FeverWarp(BonesID);

                            catchFlag = $"{nameof(Extensions.IsMoonKing)}?";
                            if (bonesObject.IsMoonKing(BonesID))
                            {
                                catchFlag = $"{nameof(Extensions.IsMoonKing)}: true";
                                MoonKing = bonesObject;

                                if (!GameObjectFactory.Factory.HasBlueprint(MoonKing.Blueprint))
                                {
                                    MoonKing.Blueprint = "Lunar Regent";
                                    IsMad = true;
                                }

                                catchFlag = $"{nameof(IsMad)}?";
                                if (IsMad)
                                {
                                    catchFlag = $"{nameof(IsMad)}: true";
                                    MoonKing.Render.Tile = Const.MAD_LUNAR_REGENT_TILE;
                                    if (MoonKing.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                                        lunarRegentPart.IsMad = true;
                                }
                                else
                                    catchFlag = $"{nameof(IsMad)}: false";

                                MoonKing?.AddOpinion<OpinionMollify>(The.Player);
                                The.Player.AddOpinion<OpinionMollify>(MoonKing);

                                catchFlag = nameof(Description);
                                if (MoonKing.TryGetPart(out Description description))
                                    description._Short = "It was you.";

                                catchFlag = $"{nameof(UD_Bones_BonesSaver.BonesName)}AttitudeSetup";
                                var attitudeSetup = Event.New($"{nameof(UD_Bones_BonesSaver.BonesName)}AttitudeSetup")
                                    .SetParameter(nameof(MoonKing), MoonKing)
                                    .SetParameter(nameof(The.Player), The.Player);

                                catchFlag = nameof(GameObject.FireEvent);
                                if (The.Player.FireEvent(attitudeSetup))
                                {
                                    var brain = MoonKing.Brain;
                                    brain?.PushGoal(new Kill(The.Player));
                                    ApplyHostility(The.Player, brain, 0);
                                }

                                if (MoonKing.Render is Render render)
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
            return MoonKing != null;
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
