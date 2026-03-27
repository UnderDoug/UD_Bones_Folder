using System;
using System.Collections.Generic;
using System.Text;

using ConsoleLib.Console;

using XRL.UI;
using XRL.World.Effects;
using XRL.World.ZoneBuilders;

namespace XRL.World.Parts
{
    public class MoonKingAnnouncer : IScribedPart
    {
        public string Title;
        public string Message;
        public IRenderable Renderable;

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
            this.Renderable = Renderable;
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == EndTurnEvent.ID
            ;

        public override bool HandleEvent(EndTurnEvent E)
        {
            if (ParentObject.CurrentZone == The.Player.CurrentZone
                && !Title.IsNullOrEmpty()
                && !Message.IsNullOrEmpty()
                && Renderable != null)
            {
                Popup.ShowSpace(
                    Message: Message,
                    Title: Title,
                    AfterRender: new Renderable(Renderable),
                    PopupID: $"{nameof(BonesZoneBuilder)}::{BonesSaver.BonesName}");

                ParentObject.Obliterate();
            }
            return base.HandleEvent(E);
        }
    }
}
