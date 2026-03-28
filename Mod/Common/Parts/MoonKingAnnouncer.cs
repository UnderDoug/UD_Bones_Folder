using System;

using Bones.Mod;

using ConsoleLib.Console;

using XRL.UI;
using XRL.World.ZoneBuilders;

namespace XRL.World.Parts
{
    [Serializable]
    public class MoonKingAnnouncer : IScribedPart
    {
        public string BonesID;
        public bool Cremated;

        public string Title;
        public string Message;

        public MoonKingAnnouncer()
            : base()
        { }

        public void Announce()
        {
            if (!GameObject.Validate(ParentObject))
                return;

            if (ParentObject.CurrentZone == The.Player.CurrentZone
                && !Title.IsNullOrEmpty()
                && !Message.IsNullOrEmpty()
                && ParentObject.CurrentZone.GetFirstObject(go => go.GetStringProperty(BonesSaver.BonesName) == BonesID) is GameObject moonKing)
            {
                Popup.ShowSpace(
                    Message: Message,
                    Title: Title,
                    AfterRender: new (moonKing.RenderForUI()),
                    PopupID: $"{nameof(BonesZoneBuilder)}::{BonesSaver.BonesName}");

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
            if (!Cremated
                && BonesManager.System != null
                && BonesManager.System.TryGetSaveBonesByID(BonesID, out var bonesInfo) is true)
            {
                bonesInfo.Cremate();
                Cremated = true;
                Utils.Log($"{nameof(MoonKingAnnouncer)} did cremation!");
            }
            // Announce();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EndTurnEvent E)
        {
            Announce();
            return base.HandleEvent(E);
        }
    }
}
