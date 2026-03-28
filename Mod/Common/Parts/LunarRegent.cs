using System;
using System.Collections.Generic;
using System.Text;

using Bones.Mod;

using XRL.World.Effects;

namespace XRL.World.Parts
{
    public class LunarRegent :IScribedPart
    {
        public string BonesID;
        public bool Cremated;

        public string RegalTitle
        {
            get => ParentObject?.GetStringProperty(nameof(MoonKingFever.RegalTitle), GetRegalTitle(ParentObject));
            set => ParentObject?.SetStringProperty(nameof(MoonKingFever.RegalTitle), value);
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
            ParentObject.ApplyEffect(new MoonKingFever(RegalTitle));
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            ;

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (!Cremated
                && BonesManager.System != null
                && BonesManager.System.TryGetSaveBonesByID(BonesID, out var bonesInfo) is true)
            {
                bonesInfo.Cremate();
                Cremated = true;
                Utils.Log($"{nameof(LunarRegent)} did cremation!");
            }
            if (ParentObject != null
                && !ParentObject.IsPlayer()
                && !ParentObject.HasEffect<MoonKingFever>())
                Onset();

            return base.HandleEvent(E);
        }
    }
}
