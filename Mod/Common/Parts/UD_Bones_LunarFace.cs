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
            Utils.Log($"{Utils.CallChain(nameof(UD_Bones_LunarFace), nameof(TryBeWorn))}");
            if (ParentObject == null)
                return false;

            if (ParentObject.Equipped == null
                && ParentObject.Holder is GameObject holder
                && !holder.IsPlayer()
                && holder.TryGetPart(out UD_Bones_LunarRegent lunarRegent))
            {
                if (holder.FindEquippedItem(go => go.Blueprint == ParentObject.Blueprint) == null)
                {
                    Utils.Log($"{1.Indent()}{holder.DebugName} is {nameof(lunarRegent)} lacking {ParentObject.Blueprint}");
                    string slot = ParentObject?.GetPart<Armor>()?.WornOn ?? "Face";
                    if (holder.Body.LoopPart(slot) is IEnumerable<BodyPart> bodyParts)
                    {
                        Utils.Log($"{2.Indent()}{slot} {nameof(bodyParts)}: {bodyParts.Count()}");
                        if (!bodyParts.IsNullOrEmpty())
                        {
                            foreach (var bodyPart in bodyParts)
                            {
                                if (bodyPart.TryUnequip()
                                    && bodyPart.Equip(ParentObject))
                                {
                                    Utils.Log($"{3.Indent()}Equipped on {bodyPart}");
                                    return true;
                                }
                            }
                            if (lunarRegent.BonesID == BonesID
                                && bodyParts.First() is BodyPart firstPart
                                && firstPart.ForceUnequip()
                                && firstPart.Equip(ParentObject))
                                Utils.Log($"{3.Indent()}Force equipped on {firstPart}");
                        }
                    }
                    if (ParentObject.Equipped == null)
                        Utils.Log($"{2.Indent()}No {slot} {nameof(bodyParts)}");
                    return true;
                }
                else
                    Utils.Log($"{1.Indent()}{holder.DebugName} is {nameof(lunarRegent)} with {ParentObject.Blueprint}");
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
