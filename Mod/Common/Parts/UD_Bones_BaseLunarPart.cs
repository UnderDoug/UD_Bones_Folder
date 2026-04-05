using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Rules;
using XRL.World.Anatomy;
using System.Linq;

namespace XRL.World.Parts
{
    [Serializable]
    public abstract class UD_Bones_BaseLunarPart : IScribedPart
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
            SetBonesID(The.Game?.GameID);
        }

        protected virtual void SetBonesID(string BonesID, bool Override)
        {
            if (Override
                || this.BonesID == null)
                this.BonesID = BonesID;

            LastBonesID = BonesID;
        }

        public void SetBonesID(string BonesID)
            => SetBonesID(BonesID, false)
            ;

        public override void FinalizeRead(SerializationReader Reader)
        {
            SetBonesID(BonesID);
            base.FinalizeRead(Reader);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == BeforeObjectCreatedEvent.ID
            || ID == BeforeTakeActionEvent.ID
            ;

        public override bool HandleEvent(BeforeObjectCreatedEvent E)
        {
            SetBonesID(The.Game?.GameID, true);
            if (E.Context == "Wish")
                Persists = true;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(BeforeTakeActionEvent E)
        {
            if (BonesID == The.Game.GameID
                && !Persists)
            {
                ParentObject.Obliterate();
                E.PreventAction = true;
                return true;
            }
            return base.HandleEvent(E);
        }
    }
}
