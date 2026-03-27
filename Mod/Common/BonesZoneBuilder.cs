using System;
using System.Collections.Generic;
using System.Text;

using Bones.Mod;

using ConsoleLib.Console;

using Qud.API;

using XRL.UI;
using XRL.World.Effects;
using XRL.World.Parts;

namespace XRL.World.ZoneBuilders
{
    public class BonesZoneBuilder
    {
        public SaveBonesInfo SavedBonesInfo;

        public string ZoneID;

        public BonesZoneBuilder()
        { }

        public bool BuildZone(Zone Z)
        {
            if (BonesData.GetFromSavedBonesInfo(ZoneID, SavedBonesInfo) is BonesData bonesData
                && bonesData.Apply(Z, out var MoonKing) is true)
            {
                string regalTitle = MoonKingFever.REGAL_TITLE;

                if (MoonKing.TryGetEffect(out MoonKingFever moonKingFever))
                    regalTitle = moonKingFever.RegalTitle;

                Z.GetCell(0, 0)
                    ?.AddObject("Widget")
                    ?.AddPart(new MoonKingAnnouncer(
                        Title: $"A {regalTitle} persists!",
                        Message: $"=subject.Subjective= will tolerate neither pretenders nor would-be-usurpers!"
                            .StartReplace()
                            .AddObject(MoonKing)
                            .ToString(),
                        Renderable: MoonKing.RenderForUI())
                    );

                bonesData.Cremate();
                return true;
            }
            return false;
        }
    }
}
