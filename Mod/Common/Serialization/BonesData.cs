using System;

using Kobold;

using XRL;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.Effects;
using XRL.World.Parts;

using static XRL.World.Cell;

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

        public bool Apply(Zone Zone, out GameObject MoonKing, out bool IsMad)
        {
            MoonKing = null;
            IsMad = false;
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
                    if (bonesObject.GetStringProperty(UD_Bones_BonesSaver.BonesName, "").EqualsNoCase(BonesID))
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

                        if (!SpriteManager.TryGetTextureInfo(MoonKing.Render.Tile, out _))
                        {
                            IsMad = true;
                            MoonKing.Render.Tile = Const.MAD_LUNAR_REGENT_TILE;
                        }

                        if (!GenotypeFactory.GenotypesByName.ContainsKey(MoonKing.GetGenotype())
                            || !SubtypeFactory.SubtypesByName.ContainsKey(MoonKing.GetGenotype()))
                            IsMad = true;

                        if (MoonKing.Render is Render render)
                            render.Visible = true;

                        MoonKing.Energy.BaseValue = 0;
                    }
                    var bonesObjectCopy = cell.AddObject(bonesObject.DeepCopy(CopyEffects: true, CopyID: false));

                    if (bonesObjectCopy.Energy is Statistic energyCopy)
                        energyCopy.BaseValue = 0;

                    bonesObject?.Obliterate();
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
