using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

using Genkit;

using XRL;
using XRL.Collections;
using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization.PseudoTypes
{
    [Serializable]
    public class PseudoAddress : IComposite
    {
        public class PseudoAddressEqualityComparer : IEqualityComparer<PseudoAddress>
        {
            public bool Equals(PseudoAddress x, PseudoAddress y)
            {
                if (x == null
                    || y == null)
                    return (x == null) == (y == null);

                if (x == y)
                    return true;

                return x.SameAs(y);
            }

            public int GetHashCode(PseudoAddress obj)
                => (obj?.ZoneID?.GetHashCode() ?? 0)
                ^ (obj?.X ?? 0).GetHashCode()
                ^ (obj?.Y ?? 0).GetHashCode()
                ^ (obj?.I ?? 0).GetHashCode()
                ;
        }

        public static PseudoAddressEqualityComparer EqualityComparer = new();

        public string ZoneID;
        public int X;
        public int Y;
        public int I;

        public PseudoAddress()
        { }

        public PseudoAddress(string ZoneID, int X, int Y, int I)
            : this()
        {
            this.ZoneID = ZoneID;
            this.X = X;
            this.Y = Y;
            this.I = I;
        }

        public PseudoAddress(GameObject GameObject)
            : this(
                ZoneID: GameObject?.CurrentZone?.ZoneID,
                X: GameObject?.CurrentCell?.X ?? -1,
                Y: GameObject?.CurrentCell?.Y ?? -1,
                I: GameObject?.CurrentCell?.Objects?.IndexOf(GameObject) ?? -1)
        { }

        public PseudoAddress(Cell Cell, GameObject GameObject, int I)
            : this(
                ZoneID: GameObject?.CurrentZone?.ZoneID,
                X: Cell?.X ?? -1,
                Y: Cell?.Y ?? -1,
                I: I)
        { }

        public PseudoAddress(PseudoCell Cell, GameObject GameObject, int I)
            : this(
                ZoneID: GameObject?.CurrentZone?.ZoneID,
                X: Cell?.X ?? -1,
                Y: Cell?.Y ?? -1,
                I: I)
        { }

        public PseudoAddress(PseudoCell Cell, int I)
            : this(
                ZoneID: Cell?.ParentZone?.ZoneID,
                X: Cell?.X ?? -1,
                Y: Cell?.Y ?? -1,
                I: I)
        { }


        public override string ToString()
            => $"{ZoneID}@{X},{Y}:{I}"
            ;

        public bool SameAs(PseudoAddress Other)
            => Other != null
            && ZoneID == Other.ZoneID
            && X == Other.X
            && Y == Other.Y
            && I == Other.I
            ;

        public Location2D GetLocation()
            => Location2D.Get(X, Y)
            ;

        public bool TryRetrieveObject(PseudoZone Zone, out GameObject Object)
        {
            Object = null;

            if (this.IsNullOrInvalid())
                return false;

            if (ZoneID != Zone.ZoneID)
                return false;

            if (Zone?.Map.IsNullOrEmpty() is not false)
                return false;

            if (Zone.Map.Length > X)
                return false;

            if (Zone.Map[X] is not PseudoCell[] column
                || column.Length > Y)
                return false;

            if (column[Y] is not PseudoCell pseudoCell)
                return false;

            if (pseudoCell.Objects is not Rack<PseudoGameObject> objectRack
                || objectRack.Count > I)
                return false;

            return (Object = objectRack[I].GameObject) != null;
        }
    }

    public static class PseudoAddressExtensions
    {
        public static bool IsNullOrInvalid([NotNullWhen(false)] this PseudoAddress PseudoAddress, bool RequireZoneID = false)
            => PseudoAddress == null
            || (RequireZoneID && PseudoAddress.ZoneID.IsNullOrEmpty())
            || PseudoAddress.X < 0
            || PseudoAddress.Y < 0
            || PseudoAddress.I < 0
            ;
    }
}
