using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Rules;
using XRL.World.Anatomy;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;

using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Parts
{
    [Serializable]
    public abstract class UD_Bones_BaseLunarPart
        : IScribedPart
        , IModEventHandler<LunarObjectColorChangedEvent>
    {
        protected string _BonesID;
        public string BonesID
        {
            get
            {
                if (ParentObject != null)
                    _BonesID = ParentObject.GetStringProperty(nameof(BonesID), _BonesID);
                return _BonesID;
            }
            protected set
            {
                _BonesID = value;
                ParentObject?.SetStringProperty(nameof(BonesID), _BonesID, true);
            }
        }

        protected string _LastBonesID;
        public string LastBonesID
        {
            get
            {
                if (ParentObject != null)
                    _LastBonesID = ParentObject.GetStringProperty(nameof(LastBonesID), _LastBonesID);
                return _LastBonesID;
            }
            protected set
            {
                _LastBonesID = value;
                ParentObject?.SetStringProperty(nameof(LastBonesID), _LastBonesID, true);
            }
        }

        public bool Persists;

        public override void Attach()
        {
            base.Attach();
            SetBonesID<UD_Bones_BaseLunarPart>(The.Game?.GameID);
        }

        protected virtual void SetBonesIDInternal(string BonesID, bool Override)
        {
            if (Override
                || this.BonesID == null)
                this.BonesID = BonesID;

            LastBonesID = BonesID;
        }

        protected virtual void SetBonesIDInternal(string BonesID)
            => SetBonesIDInternal(BonesID, false);

        public T SetBonesID<T>(string BonesID)
            where T : UD_Bones_BaseLunarPart
        {
            SetBonesIDInternal(BonesID, false);
            return (T)this;
        }

        public T OverrideBonesID<T>(string BonesID)
            where T : UD_Bones_BaseLunarPart
        {
            SetBonesIDInternal(BonesID, true);
            return (T)this;
        }

        public override void FinalizeRead(SerializationReader Reader)
        {
            SetBonesIDInternal(BonesID);
            base.FinalizeRead(Reader);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == BeforeObjectCreatedEvent.ID
            || ID == BeforeTakeActionEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public virtual bool HandleEvent(LunarObjectColorChangedEvent E)
            => base.HandleEvent(E);

        public override bool HandleEvent(BeforeObjectCreatedEvent E)
        {
            if (E.Context == "Wish")
                Persists = true;

            SetBonesIDInternal(The.Game?.GameID, true);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(BeforeTakeActionEvent E)
        {
            if (BonesID == The.Game.GameID
                && !Persists)
            {
                ParentObject.Obliterate();
                return true;
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(nameof(UD_Bones_BaseLunarPart), nameof(BonesID), BonesID);
            E.AddEntry(nameof(UD_Bones_BaseLunarPart), nameof(LastBonesID), LastBonesID);
            E.AddEntry(nameof(UD_Bones_BaseLunarPart), nameof(Persists), Persists);
            return base.HandleEvent(E);
        }
    }
}
