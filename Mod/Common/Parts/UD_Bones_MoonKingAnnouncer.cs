using System;

using ConsoleLib.Console;

using XRL.UI;
using XRL.World.ZoneBuilders;

using UD_Bones_Folder.Mod;
using Options = UD_Bones_Folder.Mod.Options;
using XRL.Core;
using Qud.UI;

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

        public string MadTitle;
        public string MadMessage;

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
            string Message)
            : this()
        {
            SetBonesIDInternal(BonesID, true);
            this.Title = Title;
            this.Message = Message;
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
                try
                {
                    var render = new BonesRender(MoonKing.RenderForUI("SaveBonesInfo", true));

                    string message;

                    string title;

                    if (!MoonKing.IsMad())
                    {
                        message = Message;
                        title = Title;
                    }
                    else
                    {
                        title = MadTitle;
                        message = MadMessage;
                    }

                    title = title
                        .StartReplace()
                        .AddObject(MoonKing)
                        .AddObject(The.Player)
                        .ToString();

                    message = message
                        .StartReplace()
                        .AddObject(MoonKing)
                        .AddObject(The.Player)
                        .ToString();

                    SoundManager.PlayUISound("Sounds/UI/ui_notification", 1f, Combat: false, Interface: true);
                    string popupMessage = Markup.Transform(message);
                    if (UIManager.UseNewPopups)
                    {
                        Popup.WaitNewPopupMessage(
                            message: popupMessage,
                            contextTitle: title,
                            afterRender: new FlippableRender(render, false),
                            PopupID: $"{nameof(BonesZoneBuilder)}::{UD_Bones_BonesSaver.BonesName}");
                    }
                    $"{title} {message}".StartReplace().AddObject(MoonKing).EmitMessage(AlwaysVisible: true);
                }
                finally
                {
                    ParentObject?.Obliterate();
                }
            }
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == AfterObjectCreatedEvent.ID
            || ID == ZoneActivatedEvent.ID
            || ID == EndTurnEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            if (E.Context.StartsWith($"{nameof(BonesID)}::"))
            {
                Utils.Log($"{nameof(UD_Bones_MoonKingAnnouncer)}.{nameof(AfterObjectCreatedEvent)}({nameof(E.Context)}: {E.Context})");
                OverrideBonesID<UD_Bones_MoonKingAnnouncer>(E.Context.Split("::")[1]);
            }
            return base.HandleEvent(E);
        }

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
