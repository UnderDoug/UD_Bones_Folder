using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public class AnnounceLunarRegentEvent : ModSingletonEvent<AnnounceLunarRegentEvent>
    {
        public new static readonly int CascadeLevel = CASCADE_ALL;
        public static readonly string RegisteredEventID = nameof(AnnounceLunarRegentEvent);

        public AnnounceLunarRegentEvent()
        {
        }

        public override int GetCascadeLevel()
            => CascadeLevel
            ;

        public virtual string GetRegisteredEventID()
            => RegisteredEventID
            ;

        public static void Send()
        {
            if (The.Player?.CurrentZone is not Zone zone
                || Instance is not AnnounceLunarRegentEvent E)
            {
                Utils.Error($"Attempted to send {nameof(AnnounceLunarRegentEvent)} for null Zone, Player, or event Instance", new InvalidOperationException("Must have a player object, zone it's in, and instance of event."));
                return;
            }

            if (zone.WantEvent(E.GetID(), E.GetCascadeLevel()))
                zone.HandleEvent(E);
        }
    }
}
