using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Rules;
using XRL.World.Anatomy;
using System.Linq;
using XRL.Core;
using UD_Bones_Folder.Mod.Events;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

namespace XRL.World.Parts
{
    [HasVariableReplacer]
    [Serializable]
    public class UD_Bones_LunarFace : UD_Bones_BaseLunarPart
    {
        protected string TileColor;
        protected string DetailColor;
        public override bool CanBeFragile => false;

        public UD_Bones_LunarFace()
            : base()
        { }

        public static GameObject CreateNew(out UD_Bones_LunarFace LunarFace, string BonesID = null)
        {
            var maskObject = GameObject.Create("Lunar Face");
            LunarFace = maskObject.GetPart<UD_Bones_LunarFace>();
            if (!BonesID.IsNullOrEmpty())
                LunarFace?.OverrideBonesID(BonesID);
            return maskObject;
        }

        public static GameObject CreateNew(string BonesID = null)
            => CreateNew(out _, BonesID)
            ;

        public static bool TryMatchSize(GameObject Holder, GameObject LunarFace)
        {
            if (Holder == null
                || LunarFace.GetPart<UD_Bones_LunarFace>() is not UD_Bones_LunarFace lunarFacePart)
                return false;

            if (Holder.IsGiganticCreature != LunarFace.IsGiganticEquipment)
            {
                var newMask = CreateNew(out var newLunarFacePart, LunarFace.GetBonesID());
                if (Holder.IsGiganticCreature)
                    newMask.IsGiganticEquipment = true;

                newMask.RemovePart(newLunarFacePart);
                newMask.AddPart(lunarFacePart);
                ReplaceInContextEvent.Send(LunarFace, newMask);
            }
            return true;
        }

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
                TryMatchSize(holder, ParentObject);
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

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDisplayNameEvent.ID
            || ID == BeforeBeginTakeActionEvent.ID
            || ID == TookEvent.ID
            || ID == EquippedEvent.ID
            || ID == GetIntrinsicValueEvent.ID
            || ID == LunarObjectColorChangedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            E.ReplacePrimaryBase(
                desc: E.GetPrimaryBase()
                    .StartReplace()
                    .AddObject(ParentObject?.Equipped ?? The.Player, "observer")
                    .ToString()
                );
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(BeforeBeginTakeActionEvent E)
        {
            TryBeWorn();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(TookEvent E)
        {
            if (E.Item == ParentObject
                && E.Actor != null)
                TryMatchSize(E.Actor, ParentObject);

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EquippedEvent E)
        {
            if (E.Actor.TryGetPart(out UD_Bones_LunarRegent lunarRegent))
            {
                if (LastBonesID != lunarRegent.BonesID)
                    SetBonesIDTyped<UD_Bones_LunarFace>(lunarRegent.BonesID);
            }
            else
            if (E.Actor.IsPlayer())
                SetBonesIDTyped<UD_Bones_LunarFace>(The.Game?.GameID);

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetIntrinsicValueEvent E)
        {
            E.Value *= -1;
            return true;
        }

        public override bool HandleEvent(LunarObjectColorChangedEvent E)
        {
            if (UD_Bones_LunarColors.GetAnimatedLunarShaderEquipmentFrameColors(E.TileColor) is string equipmentFrame)
                ParentObject?.SetEquipmentFrameColors(equipmentFrame);

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(TileColor), TileColor);
            E.AddEntry(this, nameof(DetailColor), DetailColor);
            E.AddEntry(this, Const.EQ_FRAME_COLORS, ParentObject.GetEquipmentFrameColors("----"));
            return base.HandleEvent(E);
        }
    }
}
