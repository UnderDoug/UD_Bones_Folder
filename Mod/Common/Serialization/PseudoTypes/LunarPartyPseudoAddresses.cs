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
        public CompositeSet<PseudoAddress> LunarCourtiers;

        public LunarPartyPseudoAddresses()
        { }

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.WriteComposite(LunarRegent);
            Writer.WriteComposite(LunarCourtiers);
        }

        public virtual void Read(SerializationReader Reader)
        {
            LunarRegent = Reader.ReadComposite<PseudoAddress>();
            LunarCourtiers = Reader.ReadCompositeSet<PseudoAddress>();
        }

        public bool TryRetrieveLunarParty(
            PseudoZone Zone,
            out GameObject LunarRegent,
            out CoalescibleSet<GameObject> LunarCourtiers
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
            if (!TryRetrieveLunarParty(Zone, out GameObject LunarRegent, out CoalescibleSet<GameObject> LunarCourtiers))
                return false;

            LunarParty = new LunarParty
            {
                LunarRegent = LunarRegent,
                LunarCourtiers = LunarCourtiers,
            };
            return true;
        }

        public bool TryRetrieveLunarCourtiers(PseudoZone Zone, out CoalescibleSet<GameObject> LunarCourtiers)
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
