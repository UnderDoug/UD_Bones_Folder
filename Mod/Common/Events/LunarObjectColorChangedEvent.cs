using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    [GameEvent(Cascade = CASCADE_ALL, Cache = Cache.Pool)]
    public class LunarObjectColorChangedEvent : ModPooledEvent<LunarObjectColorChangedEvent>
    {
        public new static readonly int CascadeLevel = CASCADE_ALL;
        public static readonly string RegisteredEventID = nameof(LunarObjectColorChangedEvent);

        public GameObject LunarObject;

        public string TileColor;
        public string DetailColor;
        public bool IsMad;

        public int LastFrame;

        public LunarObjectColorChangedEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            LunarObject = null;
            TileColor = null;
            DetailColor = null;
            IsMad = false;
            LastFrame = 0;
        }

        public override int GetCascadeLevel()
            => CascadeLevel
            ;

        public virtual string GetRegisteredEventID()
            => RegisteredEventID
            ;

        public static LunarObjectColorChangedEvent FromPool(
            GameObject LunarObject,
            string TileColor,
            string DetailColor,
            bool IsMad,
            int LastFrame
            )
        {
            if (FromPool() is not LunarObjectColorChangedEvent E
                || LunarObject == null)
                return null;

            E.LunarObject = LunarObject;
            E.TileColor = TileColor;
            E.DetailColor = DetailColor;
            E.IsMad = IsMad;
            E.LastFrame = LastFrame;

            return E;
        }

        public static void Send(
            GameObject LunarObject,
            string TileColor,
            string DetailColor,
            bool IsMad,
            int LastFrame
            )
        {
            if (FromPool(
                LunarObject: LunarObject,
                TileColor: TileColor,
                DetailColor: DetailColor,
                IsMad: IsMad,
                LastFrame: LastFrame) is not LunarObjectColorChangedEvent E
                || !GameObject.Validate(ref LunarObject))
                return;

            if (LunarObject.WantEvent(E.GetID(), E.GetCascadeLevel()))
                LunarObject.HandleEvent(E);
        }
    }
}
