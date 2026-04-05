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

                foreach (var bonesObject in bonesObjects)
                {
                    if (bonesObject.TryGetPart(out UD_Bones_LunarRegent lunarRegent)
                        && lunarRegent.BonesID.EqualsNoCase(BonesID))
                    {
                        MoonKing = bonesObject;
                        MoonKing?.AddOpinion<OpinionMollify>(The.Player);
                        The.Player.AddOpinion<OpinionMollify>(MoonKing);

                        if (MoonKing.TryGetPart<Description>(out var description))
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

                            var sampleMask = GameObject.CreateSample("Lunar Regent Mask");
                            if (!MoonKing.HasPart<AnimatedMaterialGeneric>()
                                && sampleMask.TryGetPart(out AnimatedMaterialGeneric animatedMaterial))
                            {
                                sampleMask.RemovePart(animatedMaterial);
                                MoonKing.AddPart(animatedMaterial);
                            }
                            sampleMask?.Obliterate();
                        }

                        if (MoonKing.Render is Render render)
                            render.Visible = true;

                        MoonKing.Energy.BaseValue = 0;
                    }
                    var oldBonesObject = bonesObject;
                    /*
                    var bonesObjectCopy = bonesObject.DeepCopy(CopyEffects: true, CopyID: false);
                    if (oldBonesObject != bonesObjectCopy)
                        oldBonesObject?.Obliterate();
                    */
                    var bonesObjectCopy = bonesObject;

                    if (bonesObjectCopy.CurrentCell is Cell oldBonesCell)
                        oldBonesCell.RemoveObject(bonesObjectCopy);

                    bonesObjectCopy.ApplyRegistrar();

                    cell.AddObject(bonesObjectCopy);

                    if (The.ActionManager is ActionManager actionManager)
                    {
                        if (bonesObjectCopy.GetStringProperty(ACTIVE_OBJECT_PROPERTY, $"{false}").EqualsNoCase($"{true}"))
                        {
                            if (!actionManager.ActionQueue.Contains(bonesObjectCopy))
                                bonesObjectCopy.MakeActive();
                            bonesObjectCopy.ApplyActiveRegistrar();
                        }
                        if (bonesObjectCopy.GetStringProperty(ABILITY_OBJECT_PROPERTY, $"{false}").EqualsNoCase($"{true}")
                            && actionManager.ActionQueue.Contains(bonesObjectCopy)
                            && !actionManager.AbilityObjects.Contains(bonesObjectCopy))
                            actionManager.AbilityObjects.Add(bonesObjectCopy);
                    }

                    if (bonesObject == MoonKing)
                        MoonKing = bonesObjectCopy;

                    if (bonesObjectCopy.Energy is Statistic energyCopy)
                        energyCopy.BaseValue = 0;

                    if (bonesObjectCopy != MoonKing
                        && bonesObjectCopy?.GetTile() is string tile
                        && !tile.IsTile())
                        bonesObjectCopy.AddPart<UD_Bones_FeverWarped>();
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
