using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using ConsoleLib.Console;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

using XRL.Collections;
using XRL.Core;
using XRL.Rules;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

namespace XRL.World.Parts
{
    [HasModSensitiveStaticCache]
    [HasVariableReplacer]
    [Serializable]
    public class UD_Bones_LunarColors : UD_Bones_BaseLunarPart
    {
        public ref struct ColorAnimationEvent
        {
            public int? Offset;
            public int? KeyframeOfLastFrame;
            public string CurrentTileColor;
            public string CurrentDetailColor;
            public int? FrameDuration;
            public int? LengthInFrames;

            public readonly int GetOffset()
                => Offset
                ?? 0
                ;

            public readonly int GetKeyframeOfLastFrame()
                => KeyframeOfLastFrame
                ?? -1
                ;

            public readonly int GetAnimFrameDuration()
                => FrameDuration
                ?? BaseAnimationFrameDuration
                ;

            public readonly int GetAnimationLengthInFrames()
                => LengthInFrames
                ?? BaseAnimationLengthInFrames
                ;
        }

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        public static Dictionary<string, string> EquipmentFrameByTileColor = new();

        public static int BaseAnimationFrameDuration => 8;
        public static int BaseAnimationLengthInFrames
        {
            get
            {
                using var colors = BorrowScopeDisposedColorsFromPool();
                return colors.Count;
            }
        }

        protected string TileColor;
        protected string DetailColor;

        protected int? _FrameOffset;
        public int FrameOffset => ParentObject?.BaseID ?? (_FrameOffset ??= Stat.RandomCosmetic(0, 6999));

        public bool IsMad => ParentObject?.GetPropertyOrTag(Const.IS_MAD_PROP, $"{false}")?.EqualsNoCase($"{true}") is true;

        public int AnimationFrameDuration = 8;

        private int? FrameCount;
        public int AnimationLengthInFrames
        {
            get
            {
                if (FrameCount == null)
                {
                    using var colors = BorrowScopeDisposedColorsFromPool();
                    FrameCount = colors.Count;
                }
                return (FrameCount ?? 1) * AnimationFrameDuration;
            }
        }

        protected int KeyframeOfLastFrame;

        public UD_Bones_LunarColors()
            : base()
        {
        }

        public UD_Bones_LunarColors(string BonesID)
            : base(BonesID)
        {
        }


        public string GetTileColor()
            => $"&{TileColor}";

        public string GetDetailColor()
            => DetailColor;

        public override void Attach()
        {
            base.Attach();
            TileColor = GetLunarColorAtIndex(FrameOffset);
            DetailColor = GetNextLunarColor(TileColor);
        }

        public static ScopeDisposedList<string> BorrowScopeDisposedColorsFromPool()
        {
            var list = ScopeDisposedList<string>.GetFromPool();
            list.Add("r");
            list.Add("R");
            list.Add("W");
            list.Add("G");
            list.Add("B");
            list.Add("b");
            list.Add("m");
            return list;
        }

        public static string GetLunarColorAtIndex(int Offset)
        {
            using var colors = BorrowScopeDisposedColorsFromPool();
            return colors[Offset.NegSafeModulo(colors.Count)];
        }
        public static int GetLunarColorIndex(string Color)
        {
            if (!IsLunarColor(Color))
                return 0;

            using var colors = BorrowScopeDisposedColorsFromPool();
            return colors.IndexOf(Color);
        }
        public static bool IsLunarColor(string Color)
        {
            using var colors = BorrowScopeDisposedColorsFromPool();
            return colors.Contains(Color);
        }

        public static string GetLunarColorAtOffsetFrom(string Color, int Offset = 0)
        {
            if (!IsLunarColor(Color))
                return Color;

            return GetLunarColorAtIndex(GetLunarColorIndex(Color) + Offset);
        }

        public static string GetNextLunarColor(string Color)
            => GetLunarColorAtOffsetFrom(Color, 1);

        public static string GetPrevLunarColor(string Color)
            => GetLunarColorAtOffsetFrom(Color, -1);

        public static string GetAnimatedLunarShader(int Offset = 0, string Style = "sequence")
        {
            using var rainbowColors = BorrowScopeDisposedColorsFromPool();

            string output = null;
            for (int i = 0; i < rainbowColors.Count; i++)
                output = Utils.DelimitedAggregator(output, GetLunarColorAtIndex(Offset + i), "-");

            if (output.IsNullOrEmpty())
                return "rainbow";

            return $"{output} {Style}";
        }

        public static string GetAnimatedLunarShaderFor(string TileColor, string Style = "sequence")
            => GetAnimatedLunarShader(GetLunarColorIndex(TileColor), Style);

        public static string ApplyAnimatedLunarShader(string Text, int Offset = 0, string Style = "sequence")
            => Text.Colored(GetAnimatedLunarShader(Offset, Style));

        public static string ApplyAnimatedLunarShader(string Text, string TileColor, string Style = "sequence")
            => ApplyAnimatedLunarShader(Text, GetLunarColorIndex(TileColor), Style);

        public static string GetAnimatedLunarShaderEquipmentFrameColors(int TileColorIndex)
            => GetLunarColorAtIndex(TileColorIndex - 2)
            + GetLunarColorAtIndex(TileColorIndex - 1)
            + GetLunarColorAtIndex(TileColorIndex + 2)
            + GetLunarColorAtIndex(TileColorIndex + 3)
            ;

        public static string GetAnimatedLunarShaderEquipmentFrameColors(string Color)
        {
            if (!Color.IsNullOrEmpty()
                && Color.Length > 1)
                Color = $"{ColorUtility.FindLastForeground(Color) ?? 'W'}";

            if (!IsLunarColor(Color))
                Color = "W";

            using var colors = BorrowScopeDisposedColorsFromPool();
            return GetAnimatedLunarShaderEquipmentFrameColors(colors.IndexOf(Color));
        }

        public static string GetAnimatedLunarShaderEquipmentFrameColors(Renderable Renderable)
            => GetAnimatedLunarShaderEquipmentFrameColors($"{Renderable.GetForegroundColor()}")
            ;

        public static string GetAnimatedLunarShaderEquipmentFrameColors(Render Render)
            => GetAnimatedLunarShaderEquipmentFrameColors(Render.GetTileForegroundColor())
            ;

        public static string GetAnimatedLunarShaderEquipmentFrameColors(RenderEvent RenderEvent)
            => GetAnimatedLunarShaderEquipmentFrameColors($"{RenderEvent.GetForegroundColorChar()}")
            ;

        public static string GetAnimatedLunarShaderEquipmentFrameColors(BonesRender BonesRender)
            => GetAnimatedLunarShaderEquipmentFrameColors($"{BonesRender.GetForegroundColor()}")
            ;

        public static string GetAnimatedLunarShaderEquipmentFrameColors(IRenderable IRenderable)
        {
            if (IRenderable is Renderable renderable)
                return GetAnimatedLunarShaderEquipmentFrameColors(renderable);

            if (IRenderable is Render render)
                return GetAnimatedLunarShaderEquipmentFrameColors(render);

            if (IRenderable is RenderEvent renderEvent)
                return GetAnimatedLunarShaderEquipmentFrameColors(renderEvent);

            if (IRenderable is BonesRender bonesRender)
                return GetAnimatedLunarShaderEquipmentFrameColors(bonesRender);

            return GetAnimatedLunarShaderEquipmentFrameColors($"{IRenderable.getTileColor() ?? IRenderable.getColorString()}");
        }
            

        public static bool TryGetLunarColorPair(
            ref ColorAnimationEvent E,
            out string TileColor,
            out string DetailColor,
            out int Frame,
            out int Keyframe
            )
        {
            TileColor = E.CurrentTileColor;
            DetailColor = E.CurrentDetailColor;
            Keyframe = GetCurrentAnimationKeyframe(E.Offset, out Frame, E.FrameDuration, E.LengthInFrames);

            if (TileColor.IsNullOrEmpty()
                || E.CurrentTileColor.Length > 1
                || !IsLunarColor(E.CurrentTileColor)
                || E.KeyframeOfLastFrame != Keyframe)
            {
                TileColor = GetLunarColorAtIndex(Keyframe);
                DetailColor = GetLunarColorAtIndex(Keyframe + 1);
                return true;
            }
            return false;
        }

        public static int GetCurrentAnimationFrame(
            int? Offset = null,
            int? AnimationFrameDuration = null,
            int? AnimationLengthInFrames = null
            )
        {
            int offset = Offset ?? 0;
            int animationFrameDuration = AnimationFrameDuration ?? BaseAnimationFrameDuration;
            using var colors = BorrowScopeDisposedColorsFromPool();
            int animationLengthInFrames = AnimationLengthInFrames ?? (animationFrameDuration * colors.Count);
            int frameWithOffset = XRLCore.GetCurrentFrameAtFPS(60) + offset;
            return frameWithOffset % animationLengthInFrames;
        }

        public static int GetCurrentAnimationKeyframe(
            int? Offset,
            out int Frame,
            int? AnimationFrameDuration = null,
            int? AnimationLengthInFrames = null
            )
        {
            double frameDuration = (AnimationFrameDuration ?? BaseAnimationFrameDuration);
            Frame = GetCurrentAnimationFrame(Offset, AnimationFrameDuration, AnimationLengthInFrames);
            return (int)Math.Floor(Frame / frameDuration);
        }

        public static int GetCurrentAnimationKeyframe(
            int? Offset = null,
            int? AnimationFrameDuration = null,
            int? AnimationLengthInFrames = null
            )
            => GetCurrentAnimationKeyframe(Offset, out _, AnimationFrameDuration, AnimationLengthInFrames);

        [VariablePostProcessor(Keys = new string[] { "LunarShader" })]
        public static void LunarShaderPost(DelegateContext Context)
        {
            if (!Context.Value.IsNullOrEmpty())
            {
                int offset = 0;
                if (!Context.Parameters.IsNullOrEmpty())
                {
                    if (Context.Parameters[0] == "*")
                        offset = Stat.RandomCosmetic(0, 6999);
                    else
                        int.TryParse(Context.Parameters[1], out offset);
                }
                var e = new ColorAnimationEvent
                {
                    Offset = offset,
                };
                TryGetLunarColorPair(
                    E: ref e,
                    TileColor: out _,
                    DetailColor: out _,
                    Frame: out _,
                    Keyframe: out int keyframe);

                var oldValue = Context.Value.ToString();
                Context.Value.Clear();
                Context.Value.Append(ApplyAnimatedLunarShader(oldValue, keyframe));
            }
        }

        [VariableReplacer(Keys = new string[] { "LunarShader" })]
        public static string LunarShader(DelegateContext Context)
        {
            if (!Context.Parameters.IsNullOrEmpty()
                && Context.Parameters[0] is string text
                && !text.IsNullOrEmpty())
            {
                int offset = 0;
                if (Context.Parameters.Count > 1)
                {
                    if (Context.Parameters[1] == "*")
                        offset = Stat.RandomCosmetic(0, 6999);
                    else
                        int.TryParse(Context.Parameters[1], out offset);
                }
                var e = new ColorAnimationEvent
                {
                    Offset = offset,
                };
                TryGetLunarColorPair(
                    E: ref e,
                    TileColor: out _,
                    DetailColor: out _,
                    Frame: out _,
                    Keyframe: out int keyframe);

                return ApplyAnimatedLunarShader(text, keyframe);
            }
            return null;
        }

        public override bool Render(RenderEvent E)
        {
            var animationEvent = new ColorAnimationEvent
            {
                Offset = FrameOffset,
                KeyframeOfLastFrame = KeyframeOfLastFrame,
                CurrentTileColor = TileColor,
                CurrentDetailColor = DetailColor,
                FrameDuration = AnimationFrameDuration,
                LengthInFrames = AnimationLengthInFrames,
            };
            if (TryGetLunarColorPair(
                E: ref animationEvent,
                TileColor: out TileColor,
                DetailColor: out DetailColor,
                Frame: out _,
                Keyframe: out KeyframeOfLastFrame))
                LunarObjectColorChangedEvent.Send(ParentObject, TileColor, DetailColor, IsMad, KeyframeOfLastFrame);

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

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(TileColor), TileColor);
            E.AddEntry(this, nameof(DetailColor), DetailColor);
            E.AddEntry(this, nameof(FrameOffset), FrameOffset);
            E.AddEntry(this, nameof(AnimationFrameDuration), AnimationFrameDuration);
            E.AddEntry(this, nameof(AnimationLengthInFrames), AnimationLengthInFrames);
            E.AddEntry(this, nameof(KeyframeOfLastFrame), KeyframeOfLastFrame);
            return base.HandleEvent(E);
        }
    }
}
