using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarRegent : UD_Bones_BaseLunarPart
    {
        public bool Cremated;

        public string RegalTitle
        {
            get => ParentObject?.GetStringProperty(nameof(UD_Bones_MoonKingFever.RegalTitle), GetRegalTitle(ParentObject));
            set => ParentObject?.SetStringProperty(nameof(UD_Bones_MoonKingFever.RegalTitle), value);
        }

        public static string GetRegalTitle(GameObject LunarRegent)
        {
            if (LunarRegent == null)
                return null;

            string regalTerm = "Regent";
            if (LunarRegent.GetGender() is Gender regentGender)
            {
                switch (regentGender.Name)
                {
                    case "Female":
                    case "female":
                        regalTerm = "Queen";
                        break;
                    case "Male":
                    case "male":
                        regalTerm = "King";
                        break;
                    default:
                        break;
                }
                if (regentGender.Plural)
                    regalTerm = regalTerm.Pluralize();
            }
            return $"Moon {regalTerm}";
        }

        public void Onset()
        {
            ParentObject.ApplyEffect(new UD_Bones_MoonKingFever(RegalTitle));
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            ;

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (!Cremated
                && BonesManager.System != null
                && BonesManager.System.TryGetSaveBonesByID(BonesID, out var bonesInfo))
            {
                bonesInfo.Cremate();
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
    }
}
