using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_FragileRoyalObject : IScribedPart
    {
        public string BonesID;

        public override bool WantTurnTick()
            => true;

        public override void TurnTick(long TimeTick, int Amount)
        {
            if (ParentObject != null)
            {
                if (ParentObject.InInventory is not GameObject holder
                    || !holder.TryGetPart(out UD_Bones_LunarRegent lunarRegent)
                    || lunarRegent.BonesID != BonesID)
                {
                    if (ParentObject.HasEffect<Broken>()
                        || ParentObject.ForceApplyEffect(new Broken()))
                        ParentObject.RemovePart(this);
                }
            }
            base.TurnTick(TimeTick, Amount);
        }
    }
}
