using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public abstract class ILunarObjectEvent<T> : ModPooledEvent<T>
        where T : ILunarObjectEvent<T>, new()
    {
        public new static readonly int CascadeLevel = CASCADE_ALL;
        public static readonly string RegisteredEventID = typeof(T).Name;

        public SaveBonesInfo BonesInfo;
        public GameObject LunarObject;

        public bool IsMad => BonesInfo?.IsMad ?? true;
        public bool IsYou => BonesInfo?.IsYou ?? true;

        public string Context;

        protected Event StringyEvent;

        public ILunarObjectEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            BonesInfo = null;
            LunarObject = null;
            StringyEvent = null;
            Context = null;
        }

        public override int GetCascadeLevel()
            => CascadeLevel
            ;

        public virtual string GetRegisteredEventID()
            => RegisteredEventID
            ;

        public static Event GetStringyEvent(
            ILunarObjectEvent<T> ForEvent,
            params KeyValuePair<string, object>[] EventParams
            )
        {
            Event @event;
            if (ForEvent == null)
                @event = Event.New(RegisteredEventID);
            else
                @event = Event.New(ForEvent.GetRegisteredEventID());

            if (!EventParams.IsNullOrEmpty())
                foreach ((var name, var value) in EventParams)
                    @event.SetParameter(name, value);

            return @event;
        }

        protected virtual Event GetStringyEvent()
            => GetStringyEvent(
                ForEvent: this,
                EventParams: new KeyValuePair<string, object>[3]
                {
                    new(nameof(BonesInfo), BonesInfo),
                    new(nameof(LunarObject), LunarObject),
                    new(nameof(Context), Context),
                })
            ;

        protected virtual bool UpdateStringyEvent()
        {
            if (StringyEvent == null)
                return false;

            StringyEvent.SetParameter(nameof(BonesInfo), BonesInfo);
            StringyEvent.SetParameter(nameof(LunarObject), LunarObject);
            StringyEvent.SetParameter(nameof(Context), Context);

            return true;
        }

        protected virtual bool UpdateFromStringyEvent()
        {
            if (StringyEvent == null)
                return false;

            BonesInfo = StringyEvent.GetParameter<SaveBonesInfo>(nameof(BonesInfo));
            LunarObject = StringyEvent.GetGameObjectParameter(nameof(LunarObject));
            Context = StringyEvent.GetStringParameter(nameof(Context));

            return true;
        }

        protected static T FromPool(
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            string Context
            )
        {
            if (FromPool() is not T E)
                return null;

            E.BonesInfo = BonesInfo;
            E.LunarObject = LunarObject;
            E.Context = Context;

            E.StringyEvent = E.GetStringyEvent();

            return E;
        }

        protected bool ValidateGameObject(
            ref GameObject Object,
            out bool WantsMin,
            out bool WantsStr
            )
        {
            WantsMin = false;
            WantsStr = false;
            if (GameObject.Validate(ref Object))
            {
                WantsMin = Object.WantEvent(GetID(), GetCascadeLevel());
                WantsStr = Object.HasRegisteredEvent(GetRegisteredEventID());
                return true;
            }
            return false;
        }

        protected bool ValidateGameObject(
            GameObject Object,
            out bool WantsMin,
            out bool WantsStr
            )
        {
            WantsMin = false;
            WantsStr = false;
            if (Object != null)
            {
                WantsMin = Object.WantEvent(GetID(), GetCascadeLevel());
                WantsStr = Object.HasRegisteredEvent(GetRegisteredEventID());
                return true;
            }
            return false;
        }

        protected virtual bool ProcessForGame()
        {
            if (The.Game?.WantEvent(GetID(), GetCascadeLevel()) is true)
            {
                if (!The.Game.HandleEvent(this as T))
                    return false;

                UpdateStringyEvent();
            }
            return true;
        }

        protected virtual bool ProcessForZone(Zone Zone)
        {

            if (Zone?.HasObjectWithRegisteredEvent(GetRegisteredEventID()) is true)
            {
                if (!Zone.FireEvent(StringyEvent))
                    return false;

                UpdateFromStringyEvent();
            }

            if (Zone?.WantEvent(GetID(), GetCascadeLevel()) is true)
            {
                if (!Zone.HandleEvent((T)this))
                    return false;

                UpdateStringyEvent();
            }

            return true;
        }

        protected bool ProcessForGameObjectInternal(GameObject GameObject, bool WantsMin, bool WantsStr)
        {
            if (WantsStr)
            {
                if (!GameObject.FireEvent(StringyEvent))
                    return false;

                UpdateFromStringyEvent();
            }

            if (WantsMin)
            {
                if (!GameObject.HandleEvent((T)this))
                    return false;

                UpdateStringyEvent();
            }

            return true;
        }

        protected virtual bool ProcessForGameObject(ref GameObject GameObject)
        {
            if (ValidateGameObject(ref GameObject, out bool wantsMin, out bool wantsStr))
                return ProcessForGameObjectInternal(GameObject, wantsMin, wantsStr);

            return true;
        }

        protected virtual bool ProcessForGameObject(GameObject GameObject)
        {
            if (ValidateGameObject(GameObject, out bool wantsMin, out bool wantsStr))
                return ProcessForGameObjectInternal(GameObject, wantsMin, wantsStr);

            return true;
        }

        protected static T Process(
            GameObject Player,
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            out bool Success,
            string Context = null
            )
        {
            Success = false;
            if (FromPool(
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                Context: Context) is not T E)
                return null;
            
            Success = true;

            if (Success)
                Success = E.ProcessForGame();

            if (Success)
                Success = Player?.IsPlayer() is true
                    ? E.ProcessForGameObject(ref Player)
                    : E.ProcessForGameObject(Player)
                    ;

            if (Success)
                Success = E.ProcessForGameObject(E.LunarObject);

            return E;
        }

        public static bool Check(
            GameObject Player,
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            string Context = null
            )
            => Process(
                Player: Player,
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                Success: out bool success,
                Context: Context) != null
            && success
            ;

        public static void Send(
            GameObject Player,
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            string Context = null
            )
            => Check(
                Player: Player,
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                Context: Context)
            ;

        public static bool Check(
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            string Context = null
            )
            => Process(
                Player: null,
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                Success: out bool success,
                Context: Context) != null
            && success
            ;

        public static void Send(
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            string Context = null
            )
            => Check(
                BonesInfo: BonesInfo,
                LunarObject: LunarObject,
                Context: Context)
            ;

        public static GameObject GetFor(
            GameObject Player,
            SaveBonesInfo BonesInfo,
            GameObject LunarObject,
            string Context = null
            )
            => The.Game == null
                || Process(
                    Player: Player,
                    BonesInfo: BonesInfo,
                    LunarObject: LunarObject,
                    Success: out bool success,
                    Context: Context) is not T E
                || !success
            ? null
            : E.LunarObject
            ;

        public bool CheckContext(string Value)
        {
            if (Context.IsNullOrEmpty())
                return Value.IsNullOrEmpty();

            return !Value.IsNullOrEmpty()
                && Context.Contains(Value);
        }

        public bool CheckContextNot(string Value)
        {
            if (Context.IsNullOrEmpty())
                return !Value.IsNullOrEmpty();

            return Value.IsNullOrEmpty()
                || !Context.Contains(Value);
        }

        public bool CheckContextAny(params string[] Values)
        {
            if (Values.IsNullOrEmpty())
                return CheckContext(null);

            foreach (var value in Values)
                if (CheckContext(value))
                    return true;

            return false;
        }

        public bool CheckContextNotAny(params string[] Values)
        {
            if (Values.IsNullOrEmpty())
                return CheckContextNot(null);

            foreach (var value in Values)
                if (CheckContext(value))
                    return false;

            return true;
        }
    }
}
