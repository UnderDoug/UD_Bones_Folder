using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public class TidyLunarObjectsEvent : ModPooledEvent<TidyLunarObjectsEvent>
    {
        public new static readonly int CascadeLevel = CASCADE_ALL;
        public static readonly string RegisteredEventID = nameof(TidyLunarObjectsEvent);

        public string BonesID;
        public bool Force;
        public string Context;

        public TidyLunarObjectsEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            BonesID = null;
            Force = false;
            Context = null;
        }

        public override int GetCascadeLevel()
            => CascadeLevel
            ;

        public virtual string GetRegisteredEventID()
            => RegisteredEventID
            ;

        public static TidyLunarObjectsEvent FromPool(
            string BonesID = null,
            bool Force = false,
            string Context = null
            )
        {
            if (FromPool() is not TidyLunarObjectsEvent E)
                return null;

            E.BonesID = BonesID;
            E.Force = Force;
            E.Context = Context;

            return E;
        }

        public static void Send(
            Zone Zone,
            string BonesID = null,
            bool Force = false,
            string Context = null
            )
        {
            if (Zone == null
                || FromPool(
                    BonesID: BonesID,
                    Force: Force,
                    Context: Context) is not TidyLunarObjectsEvent E)
                return;

            if (Zone.WantEvent(E.GetID(), E.GetCascadeLevel()))
                Zone.HandleEvent(E);
        }

        public static void Send(
            Zone Zone,
            string Context = null
            )
            => Send(Zone, null, false, Context);

        public static void SendGameID(
            Zone Zone,
            string Context = null
            )
            => Send(Zone, The.Game?.GameID, false, Context);

        public static void Send(
            GameObject Object,
            string BonesID = null,
            bool Force = false,
            string Context = null
            )
            => Send(Object?.CurrentZone, BonesID, Force, Context);

        public static void Send(
            GameObject Object,
            string Context = null
            )
            => Send(Object?.CurrentZone, null, false, Context);

        public static void SendGameID(
            GameObject Object,
            string Context = null
            )
            => Send(Object?.CurrentZone, The.Game?.GameID, false, Context);

        public static void Send(string Context = null)
            => Send(The.Player, null, false, Context);

        public static void SendGameID(string Context = null)
            => Send(The.Player, The.Game?.GameID, false, Context);
    }
}
