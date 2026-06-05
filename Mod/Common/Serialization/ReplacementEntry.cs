using System;
using System.Collections.Generic;
using System.Text;

using XRL.Rules;
using XRL.World;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class ReplacementEntry : IComposite
    {
        [Serializable]
        public class EqualityComparer : EqualityComparer<ReplacementEntry>, IComposite
        {
            public virtual bool WantFieldReflection => false;

            protected bool NameOnly;

            public EqualityComparer()
            {
                NameOnly = true;
            }

            public EqualityComparer(bool NameOnly)
                : this()
            {
                this.NameOnly = NameOnly;
            }

            public virtual void Write(SerializationWriter Writer)
            {
                Writer.Write(NameOnly);
            }

            public virtual void Read(SerializationReader Reader)
            {
                NameOnly = Reader.ReadBoolean();
            }

            public override bool Equals(ReplacementEntry x, ReplacementEntry y)
            {
                if (x is null
                    || y is null)
                    return (x is null) == (y is null);

                if (NameOnly)
                    return x.Name == y.Name;

                return x.Name == y.Name
                    && x.Blueprint == y.Blueprint
                    && x.Tile == y.Tile
                    && x.HFlip == y.HFlip
                    && x.VFlip == y.VFlip
                    ;
            }

            public override int GetHashCode(ReplacementEntry obj)
            {
                if (obj == null)
                    return 0;

                if (NameOnly)
                    return obj.Name?.GetHashCode()
                        ?? 0
                        ;

                return (obj.Name?.GetHashCode() ?? 0)
                    ^ (obj.Blueprint?.GetHashCode() ?? 0)
                    ^ (obj.Tile?.GetHashCode() ?? 0)
                    ;
            }
        }

        public static EqualityComparer DefaultEqualityComparer => new(NameOnly: true);

        public string Name;
        public string Blueprint;
        public string Tile;
        public bool HFlip;
        public bool VFlip;

        public ReplacementEntry()
        { }

        public ReplacementEntry(
            string Name,
            string Blueprint,
            string Tile,
            bool HFlip,
            bool VFlip
            )
            : this()
        {
            this.Name = Name;
            this.Blueprint = Blueprint;
            this.Tile = Tile;
            this.HFlip = HFlip;
            this.VFlip = VFlip;
        }

        public bool SameAs(ReplacementEntry Other)
            => Name == Other.Name
            ;

        protected static bool GetHFlipFor(string Name)
            => Stat.GetSeededRandomGenerator($"{nameof(BlueprintSpec)}::{Name}::{nameof(HFlip)}").Next(0, 7000) % 5 == 0
            ;

        protected static bool GetVFlipFor(string Name)
            => Stat.GetSeededRandomGenerator($"{nameof(BlueprintSpec)}::{Name}::{nameof(HFlip)}").Next(0, 7000) % 25 == 0
            ;

        public static ReplacementEntry CreateDefaultFor(string Name)
            => new ReplacementEntry
            {
                Name = Name,
                Blueprint = "PhysicalObject",
                Tile = "Creatures/sw_mimic.bmp",
                HFlip = GetHFlipFor(Name),
                VFlip = GetVFlipFor(Name),
            }
            ;

        public static ReplacementEntry CreateFor(
            string Name,
            string Blueprint,
            string Tile
            )
            => new ReplacementEntry
            {
                Name = Name,
                Blueprint = Blueprint,
                Tile = Tile,
                HFlip = GetHFlipFor(Name),
                VFlip = GetVFlipFor(Name),
            }
            ;

        public void ApplyTo(
            GameObject GameObject,
            bool Tile = true,
            bool Blueprint = true
            )
        {
            if (GameObject == null)
                return;

            if (Blueprint)
                GameObject.Blueprint = this.Blueprint;

            if (GameObject.Render is Render render)
            {
                render.Tile = this.Tile;

                render.HFlip = HFlip;
                render.VFlip = VFlip;
            }
        }
    }
}
