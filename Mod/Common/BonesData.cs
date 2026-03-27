using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Genkit;

using XRL.Rules;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts;
using XRL.World.ZoneParts;

using static XRL.World.Cell;

namespace Bones.Mod
{
    public class BonesData
    {
        public string BonesID;
        public string ZoneID;
        public Zone BonesZone;
        public GameObject MoonKing;

        public BonesData()
        { }

        public BonesData(string BonesID, SerializationReader Reader)
            : this()
        {
            this.BonesID = BonesID;
            ZoneID = Reader.ReadOptimizedString();

            try
            {
                if (Reader.ReadBonesZone(ZoneID) is Zone loadedZone)
                {
                    BonesZone = loadedZone;
                    if (BonesZone.Z > 21)
                        BonesZone.Z = Stat.Random(16, 21);
                }
            }
            catch (Exception x)
            {
                Utils.Error(x);
            }
        }

        public bool Apply(Zone Zone)
        {
            if (BonesZone == null)
                return false;

            foreach (var zonePart in BonesZone.Parts ?? Enumerable.Empty<IZonePart>())
            {
                Zone.Parts ??= new();
                Zone.AddPart(zonePart, true);
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
                    if (bonesObject.TryGetIntProperty(BonesSaver.BonesName, out int result)
                        && result == 1)
                    {
                        MoonKing = Cloning.GenerateClone(
                            Original: bonesObject,
                            Cell: cell,
                            DuplicateGear: true,
                            BecomesCompanion: false,
                            Budded: false,
                            Context: BonesSaver.BonesName);

                        MoonKing.RestorePristineHealth(SkipEffects: true);
                    }
                    else
                        cell.AddObject(bonesObject.DeepCopy(true));

                    bonesObject?.Obliterate();
                }

            }
            if (MoonKing != null)
            {
                if (MoonKing.GetPartsDescendedFrom<IPlayerPart>() is IEnumerable<IPlayerPart> playerParts)
                    foreach (var playerPart in playerParts)
                        MoonKing.RemovePart(playerPart);

                MoonKing.ApplyEffect(new MoonKingFever());
            }
            return true;
        }

        public void Cremate()
            => BonesManager.System?.CremateMoonKing(BonesID)
            ;
    }
}
