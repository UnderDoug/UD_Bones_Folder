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

            if (BonesZone.Parts != null)
            {
                foreach (var zonePart in BonesZone.Parts)
                {
                    Zone.Parts ??= new();
                    Zone.AddPart(zonePart, true);
                }
            }

            using var oldBonesList = ScopeDisposedList<GameObject>.GetFromPool();
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

                oldBonesList.Clear();
                for (int i = 0; i < bonesObjects.Count; i++)
                {
                    if (bonesObjects[i] is GameObject bonesObject)
                    {
                        oldBonesList.Add(bonesObject);
                        bonesObject = bonesObject.DeepCopy(CopyEffects: true, CopyID: false);

                        if (bonesObject.TryGetPart(out UD_Bones_LunarRegent lunarRegent)
                            && lunarRegent.BonesID.EqualsNoCase(BonesID))
                        {
                            MoonKing = bonesObject;
                            MoonKing?.AddOpinion<OpinionMollify>(The.Player);
                            The.Player.AddOpinion<OpinionMollify>(MoonKing);

                            if (MoonKing.TryGetPart(out Description description))
                                description.Short = "It was you.";

                            var attitudeSetup = Event.New($"{nameof(UD_Bones_BonesSaver.BonesName)}AttitudeSetup")
                                .SetParameter(nameof(MoonKing), MoonKing)
                                .SetParameter(nameof(The.Player), The.Player);

                            if (The.Player.FireEvent(attitudeSetup))
                            {
                                var brain = MoonKing.Brain;
                                brain?.PushGoal(new Kill(The.Player));
                                ApplyHostility(The.Player, brain, 0);
                            }

                            if (IsMad)
                            {
                                MoonKing.Render.Tile = Const.MAD_LUNAR_REGENT_TILE;
                                if (MoonKing.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                                {
                                    lunarRegentPart.IsMad = true;
                                }
                            }

                            if (MoonKing.Render is Render render)
                                render.Visible = true;

                            MoonKing.Energy.BaseValue = 0;
                        }

                        if (bonesObject != MoonKing)
                        {
                            if (bonesObject.NeedsFeverWarped())
                                bonesObject.RequirePart<UD_Bones_FeverWarped>();
                        }

                        if (bonesObject.CurrentCell is Cell oldBonesCell)
                            oldBonesCell.RemoveObject(bonesObject);

                        cell.AddObject(bonesObject);

                        /*
                        if (The.ActionManager is ActionManager actionManager)
                        {
                            if (bonesObject.GetStringProperty(ACTIVE_OBJECT_PROPERTY, $"{false}").EqualsNoCase($"{true}"))
                            {
                                if (!actionManager.ActionQueue.Contains(bonesObject))
                                    bonesObject.MakeActive();
                                bonesObject.ApplyActiveRegistrar();
                            }
                            if (bonesObject.GetStringProperty(ABILITY_OBJECT_PROPERTY, $"{false}").EqualsNoCase($"{true}")
                                && actionManager.ActionQueue.Contains(bonesObject)
                                && !actionManager.AbilityObjects.Contains(bonesObject))
                                actionManager.AbilityObjects.Add(bonesObject);
                        }
                        */

                        if (bonesObject == MoonKing)
                            MoonKing = bonesObject;

                        if (bonesObject.Energy is Statistic bonesEnergy)
                            bonesEnergy.BaseValue = 0;

                        // bonesObject.ApplyRegistrar();
                    }
                }
                foreach (var oldBones in oldBonesList)
                    oldBones.Obliterate();
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
