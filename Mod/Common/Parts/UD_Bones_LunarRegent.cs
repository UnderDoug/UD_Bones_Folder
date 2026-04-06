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

        public string RegalTitle => GetRegalTitle();

        private static bool OriginalEnableFlashingLightEffects = Options.EnableFlashingLightEffects;

        public static string GetRegalTitle(GameObject LunarRegent)
        {
            string regalTerm = "Regent";
            if (LunarRegent?.GetGender() is Gender regentGender)
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

        public string GetRegalTitle()
            => GetRegalTitle(ParentObject)
            ;

        public void Onset()
        {
            ParentObject.ApplyEffect(new UD_Bones_MoonKingFever());
        }

        public override bool WantTurnTick()
            => true;

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDisplayNameEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            E.AddHonorific(GetRegalTitle().Colored(Utils.GetAnimatedRainbowShaderForFrame()));
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

        public override void TurnTick(long TimeTick, int Amount)
        {
            HandleFlashingLightsOption(ParentObject);
            base.TurnTick(TimeTick, Amount);
        }

        public static void HandleFlashingLightsOption(GameObject GameObject)
        {
            if (Options.EnableFlashingLightEffects != OriginalEnableFlashingLightEffects)
            {
                OriginalEnableFlashingLightEffects = Options.EnableFlashingLightEffects;
                if (GameObject.TryGetPart(out AnimatedMaterialGeneric animatedMaterial))
                {
                    if (GameObject.GetBlueprint() is GameObjectBlueprint parentModel)
                    {
                        string partName = nameof(AnimatedMaterialGeneric);
                        string paramName = nameof(AnimatedMaterialGeneric.AnimationLength);
                        string prop = $"{partName}.{paramName}";
                        if (Options.EnableFlashingLightEffects)
                        {
                            if (GameObject.TryGetIntProperty(prop, out int animationLength))
                            {
                                GameObject.SetIntProperty(prop, 0, true);
                                animatedMaterial.AnimationLength = animationLength;
                            }
                        }
                        else
                        {
                            GameObject.SetIntProperty(prop, animatedMaterial.AnimationLength);
                            animatedMaterial.AnimationLength = 0;
                        }
                    }
                }
            }
        }
    }
}
