using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;

using XRL;
using XRL.Collections;
using XRL.World;

namespace UD_Bones_Folder.Mod.Serialization.PseudoTypes
{
    [Serializable]
    public class LunarPartyPseudoAddresses : IComposite, IDisposable
    {
        [NonSerialized]
        public PseudoAddress LunarRegent;

        [NonSerialized]
        public HashSet<PseudoAddress> LunarCourtiers;

        public LunarPartyPseudoAddresses()
        { }

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.WriteComposite(LunarRegent);

            int count = LunarCourtiers != null
                ? LunarCourtiers.Count
                : -1
                ;
            Writer.WriteOptimized(count);

            foreach (var lunarCourtier in LunarCourtiers.IteratorSafe())
                Writer.WriteComposite(lunarCourtier);
        }

        public virtual void Read(SerializationReader Reader)
        {
            LunarRegent = Reader.ReadComposite<PseudoAddress>();

            int count = Reader.ReadOptimizedInt32();
            if (count >= 0)
            {
                LunarCourtiers = new(PseudoAddress.EqualityComparer);
                for (int i = 0; i < count; i++)
                    LunarCourtiers.Add(Reader.ReadComposite<PseudoAddress>());
            }
        }

        public bool TryRetrieveLunarParty(
            PseudoZone Zone,
            out GameObject LunarRegent,
            out HashSet<GameObject> LunarCourtiers
            )
        {
            LunarRegent = null;
            LunarCourtiers = null;

            if (this.LunarRegent.IsNullOrInvalid())
                return false;

            if (!this.LunarRegent.TryRetrieveObject(Zone, out LunarRegent))
                return false;

            TryRetrieveLunarCourtiers(Zone, out LunarCourtiers);
            return true;
        }

        public bool TryRetrieveLunarParty(PseudoZone Zone, out LunarParty LunarParty)
        {
            LunarParty = null;
            if (!TryRetrieveLunarParty(Zone, out GameObject LunarRegent, out HashSet<GameObject> LunarCourtiers))
                return false;

            LunarParty = new LunarParty
            {
                LunarRegent = LunarRegent,
                LunarCourtiers = LunarCourtiers,
            };
            return true;
        }

        public bool TryRetrieveLunarCourtiers(PseudoZone Zone, out HashSet<GameObject> LunarCourtiers)
        {
            LunarCourtiers = null;
            if (this.LunarCourtiers != null)
            {
                foreach (var lunarCourtier in this.LunarCourtiers)
                {
                    if (lunarCourtier.TryRetrieveObject(Zone, out GameObject storedLunarCourtier))
                    {
                        LunarCourtiers ??= new();
                        LunarCourtiers.Add(storedLunarCourtier);
                    }
                }
                return true;
            }
            return false;
        }

        public void Dispose()
        {
            LunarRegent = null;
            LunarCourtiers = null;
        }
    }
}
