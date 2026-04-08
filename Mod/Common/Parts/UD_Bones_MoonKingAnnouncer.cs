using System;

using ConsoleLib.Console;

using XRL.UI;
using XRL.World.ZoneBuilders;

using UD_Bones_Folder.Mod;
using Options = UD_Bones_Folder.Mod.Options;
using XRL.Core;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_MoonKingAnnouncer : UD_Bones_BaseLunarPart
    {
        [Serializable]
        public class FlippableRender : Renderable
        {
            public bool HFlip;
            public FlippableRender(IRenderable Source, bool HFlip)
                : base(Source)
            {
                this.HFlip = HFlip;
            }

            public override bool getHFlip()
                => HFlip
                ;
        }

        public const string GAME_ID_PROPERTY = SerializationExtensions.GAME_ID_PROPERTY;
        public const string ACTIVE_OBJECT_PROPERTY = SerializationExtensions.ACTIVE_OBJECT_PROPERTY;
        public const string ABILITY_OBJECT_PROPERTY = SerializationExtensions.ABILITY_OBJECT_PROPERTY;

        public bool Cremated;

        public string Title;
        public string Message;

        public bool IsMad;

        public GameObject MoonKing => ParentObject.CurrentZone.GetFirstObject(go => go.GetPart<UD_Bones_LunarRegent>()?.BonesID == BonesID);

        public UD_Bones_MoonKingAnnouncer()
            : base()
        {
            Persists = true;
        }

        public UD_Bones_MoonKingAnnouncer(
            string BonesID,
            string Title,
            string Message,
            bool IsMad)
            : this()
        {
            SetBonesIDInternal(BonesID, true);
            this.Title = Title;
            this.Message = Message;
            this.IsMad = IsMad;
        }

        public override void Attach()
        {
            base.Attach();
            ParentObject.Render.DisplayName = "[Moon King Announcer]";
        }

        public void Announce()
        {
            if (!GameObject.Validate(ParentObject))
                return;

            if (MoonKing == null)
                Utils.Error($"Failed to find moon king with BonesID {BonesID}.");

            if (ParentObject.CurrentZone == The.Player.CurrentZone
                && !Title.IsNullOrEmpty()
                && !Message.IsNullOrEmpty()
                && MoonKing != null)
            {
                var render = new BonesRender(MoonKing.RenderForUI("SaveBonesInfo", true));

                string message = Message
                        .StartReplace()
                        .AddObject(MoonKing)
                        .AddObject(The.Player)
                        .ToString();

                if (IsMad)
                    message = UD_Bones_FeverWarped.FeverWarpText(message);

                Popup.ShowSpace(
                    Message: message,
                    Title: Title
                        .StartReplace()
                        .AddObject(MoonKing)
                        .AddObject(The.Player)
                        .ToString(),
                    AfterRender: new FlippableRender(render, false),
                    PopupID: $"{nameof(BonesZoneBuilder)}::{UD_Bones_BonesSaver.BonesName}");

                ParentObject.Obliterate();
            }
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == ZoneActivatedEvent.ID
            || ID == EndTurnEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(ZoneActivatedEvent E)
        {
            if (!Options.DebugEnableNoCremation
                && !Cremated
                && BonesManager.System != null
                && BonesManager.System.TryGetSaveBonesByID(BonesID, out var bonesInfo))
            {
                // bonesInfo.Cremate();
                Cremated = true;
                if (MoonKing?.GetPart<UD_Bones_LunarRegent>() is UD_Bones_LunarRegent lunarRegentPart)
                    lunarRegentPart.Cremated = true;
            }
            // ActivateObjects();
            // Announce();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EndTurnEvent E)
        {
            Announce();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(Cremated), Cremated);
            E.AddEntry(this, nameof(Title), Title);
            E.AddEntry(this, nameof(Message), Message);
            return base.HandleEvent(E);
        }
    }
}
