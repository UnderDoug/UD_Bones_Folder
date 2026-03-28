using System;

using XRL;
using XRL.World;
using XRL.World.AI;
using XRL.World.AI.GoalHandlers;
using XRL.World.Parts;

using static XRL.World.Cell;

namespace Bones.Mod
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

        public static BonesData GetFromSavedBonesInfo(string ZoneID, SaveBonesInfo SavedBonesInfo)
            => BonesManager?.ExhumeMoonKing(ZoneID, SavedBonesInfo)
            ;

        public bool Apply(Zone Zone, out GameObject MoonKing)
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
                    if (bonesObject.GetStringProperty(BonesSaver.BonesName, "false").EqualsNoCase("true"))
                    {
                        MoonKing = bonesObject;
                        MoonKing?.AddOpinion<OpinionMollify>(The.Player);
                        The.Player.AddOpinion<OpinionMollify>(MoonKing);

                        if (MoonKing.TryGetPart<Description>(out var description))
                            description.Short = "It was you.";

                        if (The.Player.FireEvent(Event.New($"{nameof(BonesSaver.BonesName)}AttitudeSetup")
                                .SetParameter(nameof(MoonKing), MoonKing)
                                .SetParameter(nameof(The.Player), The.Player))
                            )
                        {
                            var brain = MoonKing.Brain;
                            brain?.PushGoal(new Kill(The.Player));
                            BonesManager.ApplyHostility(The.Player, brain, 0);
                        }

                        if (MoonKing.Render is Render render)
                            render.Visible = true;

                        MoonKing.Energy.BaseValue = 0;


                    }
                    cell.AddObject(bonesObject.DeepCopy(CopyEffects: true, CopyID: false));

                    bonesObject?.Obliterate();
                }
            }
            return MoonKing != null;
        }

        public void Cremate()
            => BonesManager?.CremateMoonKing(BonesID)
            ;
    }
}
