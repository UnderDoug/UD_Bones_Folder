using System;

using ConsoleLib.Console;

using XRL.UI;
using XRL.World.ZoneBuilders;

using UD_Bones_Folder.Mod;
using Options = UD_Bones_Folder.Mod.Options;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_MoonKingAnnouncer : UD_Bones_BaseLunarPart
    {
        public bool Cremated;

        public string Title;
        public string Message;

        public GameObject MoonKing => ParentObject.CurrentZone.GetFirstObject(go => go.GetStringProperty(UD_Bones_BonesSaver.BonesName) == BonesID);

        public UD_Bones_MoonKingAnnouncer()
            : base()
        { }

        public void Announce()
        {
            if (!GameObject.Validate(ParentObject))
                return;

            if (ParentObject.CurrentZone == The.Player.CurrentZone
                && !Title.IsNullOrEmpty()
                && !Message.IsNullOrEmpty()
                && MoonKing != null)
            {
                Popup.ShowSpace(
                    Message: Message,
                    Title: Title,
                    AfterRender: new (MoonKing.RenderForUI()),
                    PopupID: $"{nameof(BonesZoneBuilder)}::{UD_Bones_BonesSaver.BonesName}");

                ParentObject.Obliterate();
            }
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == ZoneActivatedEvent.ID
            || ID == EndTurnEvent.ID
            ;

        public override bool HandleEvent(ZoneActivatedEvent E)
        {
            if (!Options.DebugEnableNoCremation
                && !Cremated
                && BonesManager.System != null
                && BonesManager.System.TryGetSaveBonesByID(BonesID, out var bonesInfo))
            {
                bonesInfo.Cremate();
                Cremated = true;
                if (MoonKing?.GetPart<UD_Bones_LunarRegent>() is UD_Bones_LunarRegent lunarRegentPart)
                    lunarRegentPart.Cremated = true;
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EndTurnEvent E)
        {
            Announce();
            return base.HandleEvent(E);
        }
    }
}
