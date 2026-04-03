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
    public class UD_Bones_LunarFace : UD_Bones_BaseLunarPart
    {
        public bool TryBeWorn()
        {
            if (ParentObject.Equipped == null
                && ParentObject.Holder is GameObject holder
                && !holder.IsPlayer()
                && holder.TryGetPart(out UD_Bones_LunarRegent lunarRegent))
            {
                if (holder.FindEquippedItem(go => go.Blueprint == ParentObject.Blueprint) == null)
                {
                    if (holder.Body.LoopPart(ParentObject?.GetPart<Armor>()?.WornOn ?? "Face") is IEnumerable<BodyPart> bodyParts)
                    {
                        if (!bodyParts.IsNullOrEmpty())
                        {
                            foreach (var bodyPart in bodyParts)
                            {
                                if (bodyPart.TryUnequip()
                                    && bodyPart.Equip(ParentObject))
                                    return true;
                            }
                            if (lunarRegent.BonesID == BonesID
                                && bodyParts.First() is BodyPart firstPart
                                && firstPart.ForceUnequip()
                                && firstPart.Equip(ParentObject))
                                return true;
                        }
                    }
                }
            }
            return false;
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDisplayNameEvent.ID
            || ID == BeforeBeginTakeActionEvent.ID
            || ID == EquippedEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            E.ReplacePrimaryBase(
                desc: E.GetPrimaryBase()
                    .StartReplace()
                    .AddObject(ParentObject.Equipped ?? The.Player)
                    .ToString()
                );
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(BeforeBeginTakeActionEvent E)
        {
            if (TryBeWorn())
            {
                E.PreventAction = true;
                return true;
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EquippedEvent E)
        {
            if (E.Actor.TryGetPart(out UD_Bones_LunarRegent lunarRegent))
            {
                if (LastBonesID != lunarRegent.BonesID)
                    SetBonesID(lunarRegent.BonesID);
            }
            else
            if (E.Actor.IsPlayer())
                SetBonesID(The.Game?.GameID);

            return base.HandleEvent(E);
        }
    }
}
