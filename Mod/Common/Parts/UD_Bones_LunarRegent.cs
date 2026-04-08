using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Core;
using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarRegent : UD_Bones_BaseLunarPart
    {
        public bool Cremated;

        public string RegalTitle => GetRegalTitle();

        public bool IsMad;

        public static string GetRegalTitle(GameObject LunarRegent)
        {
            string regalTerm = "Regent";
            if (LunarRegent?.GetGender() is Gender regentGender)
            {
                if (regentGender.Name.ToLower().Contains("male"))
                    regalTerm = "King";

                if (regentGender.Name.ToLower().Contains("female"))
                    regalTerm = "Queen";

                if (regentGender.Plural)
                    regalTerm = regalTerm.Pluralize();
            }
            return $"Moon {regalTerm}";
        }

        public string GetRegalTitle()
            => GetRegalTitle(ParentObject)
            ;

        public void Onset()
        {
            ParentObject.ApplyEffect(new UD_Bones_MoonKingFever());
        }

        public override void Attach()
        {
            base.Attach();
            ParentObject.SetStringProperty("UD_Bones_Folder_IsMad", $"{true}");
            ParentObject.RequirePart<UD_Bones_LunarColors>();
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDisplayNameEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == LunarObjectColorChangedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            E.AddHonorific($"=LunarShader:{GetRegalTitle()}:{ParentObject.BaseID}=".StartReplace().ToString());
            if (IsMad)
                E.AddHonorific("mad", DescriptionBuilder.ORDER_ADJUST_SLIGHTLY_EARLY);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (!Cremated
                && BonesManager.System != null
                && BonesManager.System.TryGetSaveBonesByID(BonesID, out var bonesInfo))
            {
                // bonesInfo.Cremate();
                Cremated = true;
            }
            if (ParentObject != null
                && !ParentObject.IsPlayer()
                && !ParentObject.HasEffect<UD_Bones_MoonKingFever>())
                Onset();

            if (ParentObject.Render is Render lunarRender
                && !lunarRender.Visible)
                lunarRender.Visible = true;

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(Cremated), Cremated);
            E.AddEntry(this, nameof(RegalTitle), RegalTitle);
            return base.HandleEvent(E);
        }
    }
}
