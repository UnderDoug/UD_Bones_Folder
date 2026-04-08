using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Rules;
using XRL.World.Anatomy;
using System.Linq;
using XRL.Core;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarFace : UD_Bones_BaseLunarPart
    {
        protected string TileColor;
        protected string DetailColor;

        private double LastColorFrame;

        public bool TryBeWorn()
        {
            if (ParentObject == null)
                return false;

            if (ParentObject.Holder != null
                && ParentObject.Holder.IsPlayer())
                return false;

            if (ParentObject.Equipped == null
                && ParentObject.Holder is GameObject holder
                && !holder.IsPlayer()
                && holder.TryGetPart(out UD_Bones_LunarRegent lunarRegent))
            {
                //Utils.Log($"{Utils.CallChain(nameof(UD_Bones_LunarFace), nameof(TryBeWorn))}");
                if (holder.FindEquippedItem(go => go.Blueprint == ParentObject.Blueprint) == null)
                {
                    //Utils.Log($"{1.Indent()}{holder.DebugName} is {nameof(lunarRegent)} lacking {ParentObject.Blueprint}");
                    string slot = ParentObject?.GetPart<Armor>()?.WornOn ?? "Face";
                    if (holder.Body.LoopPart(slot) is IEnumerable<BodyPart> bodyParts)
                    {
                        //Utils.Log($"{2.Indent()}{slot} {nameof(bodyParts)}: {bodyParts.Count()}");
                        if (!bodyParts.IsNullOrEmpty()
                            && !holder.AutoEquip(ParentObject, Forced: true))
                        {
                            foreach (var bodyPart in bodyParts)
                            {
                                if (bodyPart.TryUnequip()
                                    && bodyPart.Equip(ParentObject))
                                {
                                    //Utils.Log($"{3.Indent()}Equipped on {bodyPart}");
                                    return true;
                                }
                            }
                            if (lunarRegent.BonesID == BonesID
                                && bodyParts.First() is BodyPart firstPart
                                && firstPart.ForceUnequip())
                            {
                                if (firstPart.Equip(ParentObject))
                                {
                                    //Utils.Log($"{3.Indent()}Force equipped on {firstPart}");
                                }
                            }
                        }
                    }/*
                    else
                        Utils.Log($"{2.Indent()}No {slot} {nameof(bodyParts)}");

                    if (ParentObject.Equipped == null)
                        Utils.Log($"{2.Indent()}Failed to equip {ParentObject.DebugName}");
                    */
                    return true;
                }/*
                else
                    Utils.Log($"{1.Indent()}{holder.DebugName} is {nameof(lunarRegent)} with {ParentObject.Blueprint}");*/
            }
            return false;
        }

        public override bool WantTurnTick()
            => true;

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDisplayNameEvent.ID
            || ID == BeforeBeginTakeActionEvent.ID
            || ID == EquippedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            E.ReplacePrimaryBase(
                desc: E.GetPrimaryBase()
                    .StartReplace()
                    .AddObject(ParentObject?.Equipped ?? The.Player)
                    .ToString()
                );
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(BeforeBeginTakeActionEvent E)
        {
            TryBeWorn();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EquippedEvent E)
        {
            if (E.Actor.TryGetPart(out UD_Bones_LunarRegent lunarRegent))
            {
                if (LastBonesID != lunarRegent.BonesID)
                    SetBonesID<UD_Bones_LunarFace>(lunarRegent.BonesID);
            }
            else
            if (E.Actor.IsPlayer())
                SetBonesID<UD_Bones_LunarFace>(The.Game?.GameID);

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(TileColor), TileColor);
            E.AddEntry(this, nameof(DetailColor), DetailColor);
            E.AddEntry(this, "EquipmentFrameColors", ParentObject.GetEquipmentFrameColors("----"));
            return base.HandleEvent(E);
        }

        public override bool Render(RenderEvent E)
        {
            if (UD_Bones_LunarRegent.CycleColors(ParentObject.Render, ref TileColor, ref DetailColor, ref LastColorFrame, Utils.FPS_MODULO, Offset: ParentObject.BaseID))
            {
                if (Utils.GetAnimatedRainbowShaderEquipmentFrameColors(TileColor) is string equipmentFrame)
                    ParentObject?.SetEquipmentFrameColors(equipmentFrame);
            }
            return base.Render(E);
        }
    }
}
