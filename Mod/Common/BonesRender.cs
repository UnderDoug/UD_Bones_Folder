using System;

using ConsoleLib.Console;

using XRL.Core;
using XRL.World;
using XRL.World.Parts;
namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class BonesRender : IRenderable, IComposite, IDisposable
    {
        public string Tile;

        public string RenderString = " ";

        public string ColorString = "";

        public string TileColor;

        public char DetailColor;

        public bool VFlip;

        public bool HFlip;

        public bool IsMad;

        private string LastMadTileColor;

        public virtual bool WantFieldReflection => false;

        public BonesRender()
        {
        }

        public BonesRender(
            string Tile,
            string RenderString = " ",
            string ColorString = "",
            string TileColor = null,
            char DetailColor = '\0',
            bool HFlip = true,
            bool VFlip = false,
            bool IsMad = false
            )
            : this()
        {
            SetTile(Tile);
            SetRenderString(RenderString);
            SetColorString(ColorString);
            SetTileColor(TileColor);
            SetDetailColor(DetailColor);
            SetHFlip(HFlip);
            SetVFlip(VFlip);
            SetIsMad(IsMad);
        }

        public BonesRender(
            IRenderable Source,
            string Tile = null,
            string RenderString = null,
            string ColorString = null,
            string TileColor = null,
            char? DetailColor = null,
            bool? HFlip = null,
            bool? VFlip = null,
            bool? IsMad = null
            )
            : this()
        {
            SetTile(Tile ?? Source.getTile());
            SetRenderString(RenderString ?? Source.getRenderString());
            SetColorString(ColorString ?? Source.getColorString());
            SetTileColor(TileColor ?? Source.getTileColor());
            SetDetailColor(DetailColor ?? Source.getDetailColor());
            SetHFlip(HFlip ?? Source.getHFlip());
            SetVFlip(VFlip ?? Source.getVFlip());
            SetIsMad(IsMad ?? this.IsMad);
        }

        public BonesRender(SaveBonesJSON BonesJSON)
            : this(
                  Tile: BonesJSON.CharIcon,
                  RenderString: "@",
                  ColorString: $"&{BonesJSON.FColor}",
                  TileColor: $"&{BonesJSON.FColor}",
                  DetailColor: BonesJSON.DColor,
                  HFlip: true,
                  VFlip: false,
                  IsMad: BonesJSON.IsCharIconSwapped())
        { }

        public BonesRender(SaveBonesInfo BonesInfo)
            : this(BonesInfo.GetBonesJSON())
        {
            SetIsMad(BonesInfo.IsMad);
        }

        public BonesRender(
            GameObjectBlueprint Blueprint,
            bool HFlip = true,
            bool VFlip = false,
            bool IsMad = false
            )
        {
            Set(Blueprint, HFlip, VFlip, IsMad);
        }

        public virtual void Copy(IRenderable Source)
        {
            SetTile(Source.getTile());
            SetRenderString(Source.getRenderString());
            SetColorString(Source.getColorString());
            SetTileColor(Source.getTileColor());
            SetDetailColor(Source.getDetailColor());
            SetHFlip(Source.getHFlip());
            SetVFlip(Source.getVFlip());
        }

        public virtual void Copy(IRenderable Source, bool IsMad)
        {
            Copy(Source);
            this.IsMad = IsMad;
        }

        public static BonesRender UITile(string TilePath)
            => UITile(TilePath, 'y', 'W')
            ;

        public static BonesRender UITile(
            string TilePath,
            char ForegroundColorCode = 'y',
            char DetailColorCode = 'w',
            string NoTileAlt = " ",
            char NoTileColor = '\0',
            bool HFlip = true,
            bool VFlip = false
            )
            => new BonesRender(
                Tile: TilePath,
                RenderString: NoTileAlt,
                ColorString: $"&{((NoTileColor != 0) ? NoTileColor : ForegroundColorCode)}",
                TileColor: $"&{ForegroundColorCode}",
                DetailColor: DetailColorCode,
                HFlip: HFlip,
                VFlip: VFlip)
            ;

        private static char ColorCodeFromString(string Color)
        {
            if (string.IsNullOrEmpty(Color))
                return '\0';

            if (Color.Length == 1)
                return Color[0];

            return Color[1];
        }

        public static BonesRender UITile(
            string TilePath,
            string ForegroundColorCode = "y",
            string DetailColorCode = "w",
            string NoTileAlt = " ",
            string NoTileColor = null,
            bool HFlip = true,
            bool VFlip = false
            )
        {
            char colorCode = ColorCodeFromString(NoTileColor);
            if (colorCode == '\0')
                colorCode = ColorCodeFromString(ForegroundColorCode);

            string colorString = $"&{colorCode}";
            return new BonesRender(
                Tile: TilePath,
                RenderString: NoTileAlt,
                ColorString: colorString,
                TileColor: $"&{ColorCodeFromString(ForegroundColorCode)}",
                DetailColor: ColorCodeFromString(DetailColorCode),
                HFlip: HFlip,
                VFlip: VFlip);
        }

        public virtual BonesRender Set(
            GameObjectBlueprint Blueprint,
            bool HFlip = true,
            bool VFlip = false,
            bool IsMad = false
            )
        {
            if (Blueprint?.GetPart("Render") is GamePartBlueprint blueprintRender)
            {
                SetIsMad(IsMad);
                SetTile(blueprintRender.GetParameterString("Tile"));
                SetRenderString(blueprintRender.GetParameterString("RenderString", " "));
                SetColorString(blueprintRender.GetParameterString("ColorString", ""));
                SetTileColor(blueprintRender.GetParameterString("TileColor"));
                SetDetailColor(blueprintRender.GetParameterString("DetailColor", "\0")[0]);
            }
            else
            {
                SetIsMad(true);
                SetTile(null);
                SetRenderString(null);
                SetColorString("&y");
                SetTileColor("&y");
                SetDetailColor("\0");
            }
            SetHFlip(HFlip);
            SetVFlip(VFlip);
            return this;
        }

        public virtual BonesRender SetTile(string Tile)
        {
            this.Tile = Tile;
            return this;
        }

        public virtual string GetTile()
            => Tile
            ;

        string IRenderable.getTile()
            => GetTile()
            ;

        public virtual BonesRender SetRenderString(string RenderString)
        {
            this.RenderString = RenderString;
            return this;
        }

        public virtual string GetRenderString()
            => RenderString
            ;

        string IRenderable.getRenderString()
            => GetRenderString()
            ;

        public virtual BonesRender SetColorString(string ColorString)
        {
            this.ColorString = ColorString;
            return this;
        }

        public virtual string GetColorString()
            => IsMad
            ? $"&{LastMadTileColor = Utils.GetRainbowColorForFPS()}"
            : ColorString
            ;

        string IRenderable.getColorString()
            => GetColorString()
            ;

        public virtual BonesRender SetTileColor(string TileColor)
        {
            this.TileColor = TileColor;
            return this;
        }

        public virtual string GetTileColor()
            => IsMad
            ? $"&{LastMadTileColor = Utils.GetRainbowColorForFPS()}"
            : TileColor
            ;

        string IRenderable.getTileColor()
            => GetTileColor()
            ;

        public virtual BonesRender SetDetailColor(char DetailColor)
        {
            this.DetailColor = DetailColor;
            return this;
        }

        public virtual BonesRender SetDetailColor(string DetailColor)
            => SetDetailColor(!DetailColor.IsNullOrEmpty() ? DetailColor[0] : '\0')
            ;

        public virtual char GetDetailColor()
            => IsMad
            ? Utils.GetNextRainbowColor(LastMadTileColor ?? ColorCodeFromString(GetTileColor()).ToString())[0]
            : DetailColor
            ;

        char IRenderable.getDetailColor()
            => GetDetailColor()
            ;

        public virtual BonesRender SetHFlip(bool HFlip)
        {
            this.HFlip = HFlip;
            return this;
        }

        public virtual bool GetHFlip()
            => HFlip
            ;

        bool IRenderable.getHFlip()
            => GetHFlip()
            ;

        public virtual BonesRender SetVFlip(bool VFlip)
        {
            this.VFlip = VFlip;
            return this;
        }

        public virtual bool GetVFlip()
            => VFlip
            ;

        bool IRenderable.getVFlip()
            => GetVFlip()
            ;

        public virtual char GetForegroundColor()
            => ColorUtility.FindLastForeground(ResolveColorString())
            ?? 'y'
            ;

        public virtual char GetBackgroundColor()
            => ColorUtility.FindLastBackground(ResolveColorString())
            ?? 'k'
            ;

        public virtual BonesRender SetIsMad(bool IsMad)
        {
            this.IsMad = IsMad;
            return this;
        }

        public virtual bool GetIsMad()
            => IsMad
            ;

        public string ResolveColorString()
        {
            if (Globals.RenderMode == RenderModeType.Tiles)
            {
                string tileColor = GetTileColor();
                if (!tileColor.IsNullOrEmpty())
                    return tileColor;
            }
            return GetColorString();
        }

        public virtual ColorChars GetColorChars()
            => new ColorChars
            {
                background = GetBackgroundColor(),
                foreground = GetForegroundColor(),
                detail = GetDetailColor(),
            };

        ColorChars IRenderable.getColorChars()
            => GetColorChars()
            ;

        public virtual string GetSpriteName()
        {
            if (GetTile() != null)
                return Tile;

            if (GetRenderString().Length == 0)
                return $"Text/{32}.bmp";

            return $"Text/{(int)GetRenderString()[0]}.bmp";
        }

        public virtual void Write(SerializationWriter Writer)
        {
            Writer.WriteOptimized(Tile);
            Writer.WriteOptimized(RenderString);
            Writer.WriteOptimized(ColorString);
            Writer.WriteOptimized(TileColor);
            Writer.Write(DetailColor);
            Writer.Write(HFlip);
            Writer.Write(VFlip);
            Writer.Write(IsMad);
        }

        public virtual void Read(SerializationReader Reader)
        {
            Tile = Reader.ReadOptimizedString();
            RenderString = Reader.ReadOptimizedString();
            ColorString = Reader.ReadOptimizedString();
            TileColor = Reader.ReadOptimizedString();
            DetailColor = Reader.ReadChar();
            HFlip = Reader.ReadBoolean();
            VFlip = Reader.ReadBoolean();
            IsMad = Reader.ReadBoolean();
        }

        public void Dispose()
        {
        }

        public static explicit operator Renderable(BonesRender BonesRender)
            => new(BonesRender);

        public static explicit operator UD_Bones_MoonKingAnnouncer.FlippableRender(BonesRender BonesRender)
            => new(BonesRender, BonesRender.HFlip);

        public static explicit operator BonesRender(Renderable Renderable)
            => new(Renderable, HFlip: false);

        public static explicit operator BonesRender(UD_Bones_MoonKingAnnouncer.FlippableRender FlippableRender)
            => new(FlippableRender, HFlip: FlippableRender.HFlip);
    }
}
