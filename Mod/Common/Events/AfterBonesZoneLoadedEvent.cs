using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    [GameEvent(Cascade = CASCADE_ALL, Cache = Cache.Pool)]
    public class AfterBonesZoneLoadedEvent : ModPooledEvent<AfterBonesZoneLoadedEvent>
    {
        public new static readonly int CascadeLevel = CASCADE_ALL;
        public static readonly string RegisteredEventID = nameof(AfterBonesZoneLoadedEvent);

        public string BonesID;
        public GameObject LunarRegent;
        public Zone BonesZone;

        public AfterBonesZoneLoadedEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            BonesID = null;
            LunarRegent = null;
            BonesZone = null;
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
            Zone BonesZone
            )
        {
            if (FromPool() is not AfterBonesZoneLoadedEvent E)
                return null;

            E.BonesID = BonesID;
            E.LunarRegent = LunarRegent;
            E.BonesZone = BonesZone;

            return E;
        }

        public static void Send(
            Zone Zone,
            string BonesID,
            GameObject LunarRegent,
            Zone BonesZone
            )
        {
            if (Zone == null
                || FromPool(
                    BonesID: BonesID,
                    LunarRegent: LunarRegent,
                    BonesZone: BonesZone) is not AfterBonesZoneLoadedEvent E)
                return;

            if (Zone.WantEvent(E.GetID(), E.GetCascadeLevel()))
                Zone.HandleEvent(E);
        }
    }
}
