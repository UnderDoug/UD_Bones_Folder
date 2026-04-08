using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Core;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarRegent : UD_Bones_BaseLunarPart
    {
        public bool Cremated;

        public string RegalTitle => GetRegalTitle();

        public bool IsMad;

        protected string TileColor;
        protected string DetailColor;

        private double LastColorFrame;

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

        public override bool Render(RenderEvent E)
        {
            //CycleColors(ParentObject.Render, ref TileColor, ref DetailColor, ref CycleCounter, FPS_MODULO, IsMad, ParentObject.BaseID);
            return base.Render(E);
        }

        public override void TurnTick(long TimeTick, int Amount)
        {
            CycleColors(ParentObject.Render, ref TileColor, ref DetailColor, ref LastColorFrame, Utils.FPS_MODULO, IsMad, ParentObject.BaseID);
            base.TurnTick(TimeTick, Amount);
        }

        public static bool CycleColors(
            Render Render,
            ref string TileColor,
            ref string DetailColor,
            ref double LastFrameCache,
            double Threshold,
            bool IsMad = true,
            int Offset = 0
            )
        {
            if (LastFrameCache <= 0)
                LastFrameCache = Utils.CurrentFrame;

            if (Utils.CurrentFrame - LastFrameCache > Threshold)
            {
                TileColor = null;
                DetailColor = null;
                LastFrameCache = Utils.CurrentFrame;
            }
            TileColor ??= Utils.GetRainbowColorAtIndex(Utils.GetFPSModuloOrRandom(Offset));
            DetailColor ??= Utils.GetNextRainbowColor(TileColor);

            if (Options.EnableFlashingLightEffects)
            {
                Render.ColorString = $"&{TileColor}";
                Render.TileColor = $"&{TileColor}";

                if (IsMad)
                    Render.DetailColor = $"{DetailColor}";

                return true;
            }
            return false;
        }
    }
}
