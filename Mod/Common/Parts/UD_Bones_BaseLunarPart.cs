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
        public string BonesID = null;

        public string LastBonesID = null;

        public override void Attach()
        {
            base.Attach();
            SetBonesID(The.Game?.GameID);
        }

        protected virtual void SetBonesID(string BonesID, bool Override = false)
        {
            if (Override
                || this.BonesID == null)
                this.BonesID = BonesID;

            LastBonesID = BonesID;
        }

        public override void FinalizeRead(SerializationReader Reader)
        {
            SetBonesID(BonesID);
            base.FinalizeRead(Reader);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == BeforeObjectCreatedEvent.ID
            ;

        public override bool HandleEvent(BeforeObjectCreatedEvent E)
        {
            SetBonesID(The.Game?.GameID, true);
            return base.HandleEvent(E);
        }
    }
}
