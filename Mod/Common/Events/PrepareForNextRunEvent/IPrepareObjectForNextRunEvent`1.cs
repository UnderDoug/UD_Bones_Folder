using System;
using System.Collections.Generic;
using System.Text;

using UD_Bones_Folder.Mod.Logging;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    [HasRegisterMethodForDebug]
    public abstract class IPrepareObjectForNextRunEvent<T> : ModSingletonEvent<T> where T : IPrepareObjectForNextRunEvent<T>, new()
    {
        public new static readonly int CascadeLevel = CASCADE_ALL;
        public static readonly string RegisteredEventID = typeof(T).Name;

        public GameObject Object;
        public Zone Zone;
        public string GameID;
        public GameObject LunarRegent;

        public IPrepareObjectForNextRunEvent()
            : base()
        { }

        public override void Reset()
        {
            base.Reset();
            Object = null;
            Zone = null;
            GameID = null;
            LunarRegent = null;
        }

        public override int GetCascadeLevel()
            => CascadeLevel
            ;

        public virtual string GetRegisteredEventID()
            => RegisteredEventID
            ;

        [RegisterMethodForDebug(false)]
        public static void Send(
            GameObject Object,
            Zone Zone,
            string GameID,
            GameObject LunarRegent
            )
        {
            /*using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Object), Object?.DebugName ?? "MISSING_OBJECT"),
                    Debug.Arg(nameof(Zone), Zone?.ZoneID ?? "MISSING_ZONE"),
                    Debug.Arg(nameof(GameID), GameID),
                    Debug.Arg(nameof(LunarRegent), LunarRegent?.DebugName ?? "MISSING_LUNAR_REGENT"),
                });*/

            if (Object == null
                || Instance is not T E)
            {
                var x = new InvalidOperationException($"Must have an {nameof(Object)} and event {nameof(Instance)}");
                Utils.Error($"Attempted to send {typeof(T).Name} for null {nameof(Object)} or event {nameof(Instance)}", x);
                return;
            }

            E.Object = Object;
            E.Zone = Zone;
            E.GameID = GameID;
            E.LunarRegent = LunarRegent;

            if (Object.WantEvent(E.GetID(), E.GetCascadeLevel()))
                Object.HandleEvent(E);

            Instance?.Reset();
        }
    }
}
