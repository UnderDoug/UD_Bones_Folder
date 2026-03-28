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
        public Renderable Renderable;

        public MoonKingAnnouncer()
            : base()
        { }

        public MoonKingAnnouncer(
            string Title,
            string Message,
            IRenderable Renderable
            )
            : base()
        {
            this.Title = Title;
            this.Message = Message;
            this.Renderable = new (Renderable);
        }

        public void Announce()
        {
            if (!GameObject.Validate(ParentObject))
                return;

            if (ParentObject.CurrentZone == The.Player.CurrentZone
                && !Title.IsNullOrEmpty()
                && !Message.IsNullOrEmpty()
                && Renderable != null)
            {
                Popup.ShowSpace(
                    Message: Message,
                    Title: Title,
                    AfterRender: new (Renderable),
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
