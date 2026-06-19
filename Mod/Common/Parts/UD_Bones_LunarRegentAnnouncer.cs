using System;
using ConsoleLib.Console;

using Qud.UI;

using XRL.Core;
using XRL.UI;
using XRL.World.ZoneBuilders;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

using Options = UD_Bones_Folder.Mod.Options;
using SerializeField = UnityEngine.SerializeField;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarRegentAnnouncer
        : UD_Bones_BaseLunarSubject
        , IModEventHandler<AnnounceLunarRegentEvent>
    {
        [Serializable]
        public class FlippableRender : Renderable
        {
            public bool HFlip;
            public bool VFlip;
            public FlippableRender(IRenderable Source, bool HFlip, bool VFlip = false)
                : base(Source)
            {
                this.HFlip = HFlip;
                this.VFlip = VFlip;
            }

            public override bool getHFlip()
                => HFlip
                ;

            public override bool getVFlip()
                => VFlip
                ;
        }

        public const string GAME_ID_PROPERTY = SerializationExtensions.GAME_ID_PROPERTY;
        public const string ACTIVE_OBJECT_PROPERTY = SerializationExtensions.ACTIVE_OBJECT_PROPERTY;
        public const string ABILITY_OBJECT_PROPERTY = SerializationExtensions.ABILITY_OBJECT_PROPERTY;

        public string Title;
        public string Message;

        public string MadTitle;
        public string MadMessage;

        public bool IsMad;

        [SerializeField]
        private bool LoggedNotFound;

        public override GameObject LunarRegent => ParentObject?.CurrentZone?.GetFirstObject(go => go.GetPart<UD_Bones_LunarRegent>()?.BonesID == BonesID);

        public UD_Bones_LunarRegentAnnouncer()
            : base()
        {
            Persists = true;
        }

        public UD_Bones_LunarRegentAnnouncer(
            string BonesID,
            string Title,
            string Message)
            : this()
        {
            SetBonesIDInternal(BonesID, true);
            this.Title = Title;
            this.Message = Message;
        }

        public bool Announce()
        {
            if (!GameObject.Validate(ParentObject))
                return false;

            if (LunarRegent == null)
            {
                if (!LoggedNotFound)
                {
                    LoggedNotFound = true;
                    Utils.Error($"Failed to find Lunar Regent with BonesID {BonesID}.");
                }
            }

            if (ParentObject.CurrentZone == The.Player.CurrentZone
                && !Title.IsNullOrEmpty()
                && !Message.IsNullOrEmpty()
                && LunarRegent != null)
            {
                try
                {
                    var render = new BonesRender(LunarRegent.RenderForUI("SaveBonesInfo", true));

                    string message;

                    string title;

                    if (!LunarRegent.IsMad())
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
                        .AddObject(LunarRegent)
                        .AddObject(The.Player)
                        .ToString();

                    message = message
                        .StartReplace()
                        .AddObject(LunarRegent)
                        .AddObject(The.Player)
                        .ToString();

                    if (UIManager.UseNewPopups)
                    {
                        SoundManager.PlayUISound("Sounds/UI/ui_notification", 1f, Combat: false, Interface: true);
                        Popup.WaitNewPopupMessage(
                            message: Markup.Transform(message),
                            contextTitle: title,
                            afterRender: new FlippableRender(render, false),
                            PopupID: $"{nameof(BonesZoneBuilder)}::{BonesManager.BonesFileName}");
                    }
                    $"{title} {message}".StartReplace().AddObject(LunarRegent).EmitMessage(AlwaysVisible: true);
                    return true;
                }
                finally
                {
                    ParentObject?.Obliterate();
                }
            }
            return false;
        }

        public static bool IsAnnouncerWidget(GameObject Object)
            => (Object.Blueprint == Const.ANNOUNCER_WIDGET
                || Object.GetBlueprint().InheritsFromSafe(Const.ANNOUNCER_WIDGET))
            && Object.HasPart<UD_Bones_LunarRegentAnnouncer>()
            ;

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == AfterObjectCreatedEvent.ID
            || ID == ZoneActivatedEvent.ID
            || ID == AnnounceLunarRegentEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == EndTurnEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(AfterObjectCreatedEvent E)
        {
            if (E.Context.StartsWith($"{nameof(BonesID)}::"))
            {
                OverrideBonesIDTyped<UD_Bones_LunarRegentAnnouncer>(E.Context.Split("::")[1]);
                // Utils.Log($"{nameof(UD_Bones_LunarRegentAnnouncer)}.{nameof(AfterObjectCreatedEvent)}({nameof(E.Context)}: {E.Context}): {nameof(BonesID)}: {BonesID}");
            }
            return base.HandleEvent(E);
        }

        public virtual bool HandleEvent(AnnounceLunarRegentEvent E)
        {
            if (Announce())
                return true;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(ZoneActivatedEvent E)
        {
            if (Announce())
                return true;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EndTurnEvent E)
        {
            if (Announce())
                return true;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (Announce())
                return true;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(Title), Title);
            E.AddEntry(this, nameof(Message), Message);
            return base.HandleEvent(E);
        }
    }
}
