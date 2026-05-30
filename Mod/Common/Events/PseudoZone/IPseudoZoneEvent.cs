using System;
using System.Collections.Generic;
using System.Text;

using UD_Bones_Folder.Mod.Serialization.PseudoTypes;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public abstract class IPseudoZoneEvent<T> : ModPooledEvent<T>
        where T : IPseudoZoneEvent<T>, new()
    {
        public new static readonly int CascadeLevel = CASCADE_ALL;
        public static readonly string RegisteredEventID = typeof(T).Name;

        public string BonesID;
        public PseudoZone PseudoZone;

        public string Context;

        protected Event StringyEvent;

        public IPseudoZoneEvent()
        {
        }

        public override void Reset()
        {
            base.Reset();
            BonesID = null;
            PseudoZone = null;
            Context = null;
            StringyEvent = null;
        }

        public override int GetCascadeLevel()
            => CascadeLevel
            ;

        public virtual string GetRegisteredEventID()
            => RegisteredEventID
            ;

        public static Event GetStringyEvent(IPseudoZoneEvent<T> ForEvent)
            => ForEvent == null
            ? Event.New(RegisteredEventID)
            : Event.New(ForEvent.GetRegisteredEventID(),
                nameof(ForEvent.BonesID), ForEvent?.BonesID,
                nameof(ForEvent.PseudoZone), ForEvent?.PseudoZone,
                nameof(ForEvent.Context), ForEvent?.Context)
            ;

        public virtual Event GetStringyEvent()
            => GetStringyEvent(this);

        protected virtual bool UpdateStringyEvent()
        {
            if (StringyEvent == null)
                return false;

            StringyEvent.SetParameter(nameof(BonesID), BonesID);
            StringyEvent.SetParameter(nameof(PseudoZone), PseudoZone);
            StringyEvent.SetParameter(nameof(Context), Context);

            return true;
        }

        protected virtual bool UpdateFromStringyEvent()
        {
            if (StringyEvent == null)
                return false;

            BonesID = StringyEvent.GetStringParameter(nameof(BonesID));
            PseudoZone = StringyEvent.GetParameter<PseudoZone>(nameof(PseudoZone));
            Context = StringyEvent.GetStringParameter(nameof(Context));

            return true;
        }

        public static T FromPool(
            string BonesID,
            PseudoZone PseudoZone,
            string Context = null
            )
        {
            if (FromPool() is not T E)
                return null;

            E.BonesID = BonesID;
            E.PseudoZone = PseudoZone;
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
                WantsMin = Object?.WantEvent(GetID(), GetCascadeLevel()) is true;
                WantsStr = Object?.HasRegisteredEvent(GetRegisteredEventID()) is true;
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
            if (GameObject.Validate(Object))
            {
                WantsMin = Object?.WantEvent(GetID(), GetCascadeLevel()) is true;
                WantsStr = Object?.HasRegisteredEvent(GetRegisteredEventID()) is true;
                return true;
            }
            return false;
        }

        protected virtual bool ProcessForGame()
        {
            if (The.Game?.WantEvent(GetID(), GetCascadeLevel()) is true)
            {
                if (!The.Game.HandleEvent(this))
                    return false;

                UpdateStringyEvent();
            }
            return true;
        }

        protected virtual bool ProcessForZone(Zone Zone)
        {
            if (Zone?.WantEvent(GetID(), GetCascadeLevel()) is true)
            {
                if (!Zone.HandleEvent(this))
                    return false;

                UpdateStringyEvent();
            }

            if (Zone?.HasObjectWithRegisteredEvent(GetRegisteredEventID()) is true)
            {
                if (!Zone.FireEvent(StringyEvent))
                    return false;

                UpdateFromStringyEvent();
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
                if (!GameObject.HandleEvent(this))
                    return false;

                UpdateStringyEvent();
            }

            return true;
        }

        protected virtual bool ProcessForGameObject(GameObject GameObject)
        {
            if (ValidateGameObject(GameObject, out bool wantsMin, out bool wantsStr))
                return ProcessForGameObjectInternal(GameObject, wantsMin, wantsStr);

            return true;
        }

        protected virtual bool ProcessForGameObject(ref GameObject GameObject)
        {
            if (ValidateGameObject(ref GameObject, out bool wantsMin, out bool wantsStr))
                return ProcessForGameObjectInternal(GameObject, wantsMin, wantsStr);

            return true;
        }

        protected virtual bool Check(Zone Zone)
            => ProcessForGame()
            && ProcessForZone(Zone)
            && (The.Player is not GameObject player
                || ProcessForGameObject(player))
            ;

        public static bool Check(
            Zone Zone,
            string BonesID,
            PseudoZone PseudoZone,
            string Context = null
            )
        {
            if (FromPool(
                BonesID: BonesID,
                PseudoZone: PseudoZone,
                Context: Context) is not T E)
                return false;

            return E.Check(Zone);
        }

        protected virtual void Send(Zone Zone, out bool Proceed)
        {
            Proceed = true;
            if (Proceed)
                Proceed = ProcessForGame();

            if (Proceed)
                Proceed = ProcessForZone(Zone);

            if (Proceed)
            {
                if (The.Player is GameObject player)
                    Proceed = ProcessForGameObject(player);
            }
        }

        public static void Send(
            Zone Zone,
            string BonesID,
            PseudoZone PseudoZone,
            string Context = null
            )
        {
            if (FromPool(
                BonesID: BonesID,
                PseudoZone: PseudoZone,
                Context: Context) is not T E)
                return;

            E.Send(Zone, out _);
        }

        protected virtual PseudoZone GetFor(Zone Zone)
        {
            bool proceed = true;
            if (proceed)
                proceed = ProcessForGame();

            if (proceed)
                proceed = ProcessForZone(Zone);

            if (proceed)
            {
                if (The.Player is GameObject player)
                    proceed = ProcessForGameObject(player);
            }

            if (!proceed)
                return null;

            return PseudoZone;
        }

        public static PseudoZone GetFor(
            Zone Zone,
            string BonesID,
            PseudoZone PseudoZone,
            string Context = null
            )
        {
            if (The.Game is not XRLGame game)
                return null;

            if (FromPool(
                BonesID: BonesID ?? game.GameID,
                PseudoZone: PseudoZone,
                Context: Context) is not T E)
                return null;

            return E.GetFor(Zone);
        }
    }
}
