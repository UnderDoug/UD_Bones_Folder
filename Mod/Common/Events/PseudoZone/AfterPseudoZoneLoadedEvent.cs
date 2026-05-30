using System;
using System.Collections.Generic;
using System.Text;

using UD_Bones_Folder.Mod.Serialization.PseudoTypes;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public class AfterPseudoZoneLoadedEvent : IPseudoZoneEvent<AfterPseudoZoneLoadedEvent>
    {
        public GameObject LunarRegent;

        public AfterPseudoZoneLoadedEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            LunarRegent = null;
        }

        public static AfterPseudoZoneLoadedEvent FromPool(
            string BonesID,
            GameObject LunarRegent,
            PseudoZone PseudoZone,
            string Context = null
            )
        {
            if (FromPool(
                BonesID: BonesID,
                PseudoZone: PseudoZone,
                Context: Context) is not AfterPseudoZoneLoadedEvent E)
                return null;

            E.LunarRegent = LunarRegent;
            E.UpdateStringyEvent();

            return E;
        }

        public override Event GetStringyEvent()
            => GetStringyEvent(this)
                .AddParameter(nameof(LunarRegent), LunarRegent)
            ;

        protected override bool UpdateStringyEvent()
        {
            if (!base.UpdateStringyEvent())
                return false;

            StringyEvent.SetParameter(nameof(LunarRegent), LunarRegent);

            return true;
        }

        protected override bool UpdateFromStringyEvent()
        {
            if (!base.UpdateFromStringyEvent())
                return false;

            LunarRegent = StringyEvent.GetGameObjectParameter(nameof(LunarRegent));

            return true;
        }

        protected override bool Check(Zone Zone)
        {
            if (base.Check(Zone)
                && ProcessForGameObject(LunarRegent))
            {
                AfterBonesZoneLoadedEvent.Send(Zone, BonesID, LunarRegent, PseudoZone);
                return true;
            }
            return false;
        }

        protected override void Send(Zone Zone, out bool Proceed)
        {
            base.Send(Zone, out Proceed);

            if (Proceed)
                Proceed = ProcessForGameObject(LunarRegent);
        }

        public static void Send(
            Zone Zone,
            string BonesID,
            GameObject LunarRegent,
            PseudoZone PseudoZone,
            string Context = null
            )
        {
            if (FromPool(
                BonesID: BonesID,
                PseudoZone: PseudoZone,
                LunarRegent: LunarRegent,
                Context: Context) is not AfterPseudoZoneLoadedEvent E)
                return;

            E.Send(Zone, out bool proceed);

            if (proceed)
                AfterBonesZoneLoadedEvent.Send(Zone, E.BonesID, E.LunarRegent, E.PseudoZone);
        }
    }
}
