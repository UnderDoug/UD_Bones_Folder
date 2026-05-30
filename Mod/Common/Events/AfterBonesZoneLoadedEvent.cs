using System;
using System.Collections.Generic;
using System.Text;

using UD_Bones_Folder.Mod.Serialization.PseudoTypes;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public class AfterBonesZoneLoadedEvent : ModPooledEvent<AfterBonesZoneLoadedEvent>
    {
        public new static readonly int CascadeLevel = CASCADE_ALL;
        public static readonly string RegisteredEventID = nameof(AfterBonesZoneLoadedEvent);

        public string BonesID;
        public GameObject LunarRegent;
        public Zone BonesZone;
        public PseudoZone BonesPseudoZone;

        public string Context;

        public AfterBonesZoneLoadedEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            BonesID = null;
            LunarRegent = null;
            BonesZone = null;
            BonesPseudoZone = null;
            Context = null;
        }

        public override int GetCascadeLevel()
            => CascadeLevel
            ;

        public virtual string GetRegisteredEventID()
            => RegisteredEventID
            ;

        public static AfterBonesZoneLoadedEvent FromPool(
            string BonesID,
            GameObject LunarRegent,
            Zone BonesZone,
            string Context = null
            )
        {
            if (FromPool() is not AfterBonesZoneLoadedEvent E)
                return null;

            E.BonesID = BonesID;
            E.LunarRegent = LunarRegent;
            E.BonesZone = BonesZone;
            E.Context = Context;

            return E;
        }

        public static AfterBonesZoneLoadedEvent FromPool(
            string BonesID,
            GameObject LunarRegent,
            PseudoZone BonesPseudoZone,
            string Context = null
            )
        {
            if (FromPool() is not AfterBonesZoneLoadedEvent E)
                return null;

            E.BonesID = BonesID;
            E.LunarRegent = LunarRegent;
            E.BonesPseudoZone = BonesPseudoZone;
            E.Context = Context;

            return E;
        }

        public static void Send(
            Zone Zone,
            string BonesID,
            GameObject LunarRegent,
            Zone BonesZone,
            string Context = null
            )
        {
            if (Zone == null
                || FromPool(
                    BonesID: BonesID,
                    LunarRegent: LunarRegent,
                    BonesZone: BonesZone,
                    Context: Context) is not AfterBonesZoneLoadedEvent E)
                return;
                        
            if (The.Player?.WantEvent(E.GetID(), E.GetCascadeLevel()) is true)
                The.Player.HandleEvent(E);

            if (Zone.WantEvent(E.GetID(), E.GetCascadeLevel()))
                Zone.HandleEvent(E);
        }

        public static void Send(
            Zone Zone,
            string BonesID,
            GameObject LunarRegent,
            PseudoZone BonesPseudoZone,
            string Context = null
            )
        {
            if (Zone == null
                || FromPool(
                    BonesID: BonesID,
                    LunarRegent: LunarRegent,
                    BonesPseudoZone: BonesPseudoZone,
                    Context: Context) is not AfterBonesZoneLoadedEvent E)
                return;

            if (The.Player?.WantEvent(E.GetID(), E.GetCascadeLevel()) is true)
                The.Player.HandleEvent(E);

            if (Zone.WantEvent(E.GetID(), E.GetCascadeLevel()))
                Zone.HandleEvent(E);
        }
    }
}
