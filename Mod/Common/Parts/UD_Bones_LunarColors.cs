using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ConsoleLib.Console;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

using XRL.Core;
using XRL.Rules;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarColors : UD_Bones_BaseLunarPart
    {
        public static int BaseAnimationFrameDuration => 8;

        protected string TileColor;
        protected string DetailColor;

        protected int? _FrameOffset;
        public int FrameOffset => ParentObject?.BaseID ?? (_FrameOffset ??= Stat.RandomCosmetic(0, 6999));

        public bool IsMad => ParentObject?.GetPropertyOrTag("UD_BonesFolder_IsMad", $"{false}")?.EqualsNoCase($"{true}") is true;

        public int AnimFrameDuration = 8;

        private int? FrameCount;
        public int AnimationLengthInFrames => (FrameCount ??= Utils.YieldRainbowColors().Count()) * AnimFrameDuration;

        protected int LastFrame;

        public string GetTileColor()
            => $"&{TileColor}";

        public string GetDetailColor()
            => DetailColor;

        public override void Attach()
        {
            base.Attach();
            TileColor = Utils.GetRainbowColorAtIndex(ParentObject.BaseID);
            DetailColor = Utils.GetNextRainbowColor(TileColor);
        }

        public static bool TryGetLunarColorPair(
            GameObject Object,
            string CurrentTileColor,
            bool IsMad,
            out string TileColor,
            out string DetailColor,
            out int LastFrame,
            int AnimFrameDuration,
            int AnimationLengthInFrames
            )
        {
            TileColor = null;
            DetailColor = null;
            LastFrame = 0;

            if (Object == null)
                return false;

            if (CurrentTileColor.IsNullOrEmpty())
                return false;

            if (CurrentTileColor.Length > 1)
                CurrentTileColor = (ColorUtility.FindLastForeground(CurrentTileColor) ?? 'r').ToString();

            if (!Utils.IsRainbowColor(CurrentTileColor))
                CurrentTileColor = Utils.GetRainbowColorAtIndex(Object.BaseID);

            LastFrame = GetCurrentFrame(Object.BaseID, AnimationLengthInFrames);
            int segmentFrame = (int)Math.Floor(LastFrame / (double)AnimFrameDuration);
            if (segmentFrame > 0)
            {
                TileColor = Utils.GetNextRainbowColor(CurrentTileColor);
                DetailColor = Utils.GetNextRainbowColor(TileColor);
                LunarObjectColorChangedEvent.Send(Object, TileColor, DetailColor, IsMad, LastFrame);
            }
            return true;
        }

        public static int GetCurrentFrame(int? Offset = null, int? AnimationLengthInFrames = null)
            => (XRLCore.GetCurrentFrameAtFPS(60) + Offset ?? 0)
            % (AnimationLengthInFrames ?? (BaseAnimationFrameDuration * Utils.YieldRainbowColors().Count()))
            ;

        public override bool Render(RenderEvent E)
        {
            TryGetLunarColorPair(
                Object: ParentObject,
                IsMad: IsMad,
                CurrentTileColor: TileColor,
                TileColor: out TileColor,
                DetailColor: out DetailColor,
                LastFrame: out LastFrame,
                AnimFrameDuration: AnimFrameDuration,
                AnimationLengthInFrames: AnimationLengthInFrames);

            if (Options.EnableFlashingLightEffects
                && E.ColorsVisible
                && ParentObject.Render is Render render)
            {
                render.ColorString = GetTileColor();
                render.TileColor = GetTileColor();

                if (IsMad)
                    render.DetailColor = GetDetailColor();

            }
            return base.Render(E);
        }
    }
}
