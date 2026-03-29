using System;

using XRL.Core;
using XRL.World.Parts;
using XRL.World.AI.GoalHandlers;

using UD_Bones_Folder.Mod;

namespace XRL.World.Effects
{
    [Serializable]
    public class UD_Bones_MoonKingFever : IScribedEffect
    {
        public const int MAX_DIST = 9999;

        public const string REGAL_TITLE = "{{rainbow|Moon Regent}}";

        public static string[] MoonKingColors = new string[7] { "r", "R", "W", "G", "B", "b", "m", };

        private int OriginalMaxKillDistance;

        private bool AlreadyUninfluencable;

        private bool AlreadyPreacher;

        public string RegalTitle;

        public UD_Bones_MoonKingFever()
        {
            RegalTitle = REGAL_TITLE;
            SetDisplayName();
            Duration = 999;
        }

        public UD_Bones_MoonKingFever(string RegalTitle)
            : this()
        {
            if (!RegalTitle.IsNullOrEmpty())
                this.RegalTitle = RegalTitle;
            SetDisplayName();
        }

        public override int GetEffectType()
            => TYPE_NEGATIVE
            ^ TYPE_MENTAL
            ^ TYPE_DIMENSIONAL
            ^ TYPE_DISEASE
            ;

        public override bool Apply(GameObject Object)
        {
            if (Object.HasEffect<UD_Bones_MoonKingFever>())
                return false;

            if (!Object.FireEvent(Event.New($"Apply{nameof(UD_Bones_MoonKingFever)}")))
                return false;

            RegalTitle = Object.GetStringProperty(nameof(RegalTitle), RegalTitle);

            SetDisplayName();

            ApplyChanges();
            return base.Apply(Object);
        }

        public override void Remove(GameObject Object)
        {
            UnapplyChanges();
            base.Remove(Object);
        }

        public void SetDisplayName()
        {
            DisplayName = $"{(RegalTitle ?? REGAL_TITLE).Color("rainbow")} {"fever".Color("r")}";
        }

        private void ApplyChanges()
        {
            if (Object.Brain != null)
            {
                OriginalMaxKillDistance = Object.Brain.MaxKillRadius;
                Object.Brain.MaxKillRadius = MAX_DIST;
            }
            AlreadyUninfluencable = Object.HasPart<CannotBeInfluenced>();
            if (!AlreadyUninfluencable)
            {
                var noInfluence = Object.AddPart<CannotBeInfluenced>();
                noInfluence.Messages = 
                    $"Beguiling::=subject.T= knows there is only one {{rainbow|Moon King}}. =subject.Subjective= also knows it's =subject.objective=!;;" +
                    $"Persuasion_Proselytize::Are you sure you don't want to join =subject.t= instead? Well... there can only be one!;;" +
                    $"LoveTonicApplicator::The tonic failed to cure =subject.t= of =subject.possessive= {DisplayName}!;;" +
                    $"default::=subject.T's= {DisplayName} makes =subject.objective= insensible to your blandishments!";
            }
            AlreadyPreacher = Object.HasPart<Preacher>();
            if (!AlreadyPreacher)
            {
                var preacher = Object.AddPart<Preacher>();
                preacher.Book = "UD_Bones_MoonKingFever";
            }
        }
        private void UnapplyChanges()
        {
            if (OriginalMaxKillDistance != 0
                && Object.Brain != null)
            {
                Object.Brain.MaxKillRadius = OriginalMaxKillDistance;
                OriginalMaxKillDistance = 0;
            }
            if (!AlreadyUninfluencable)
                Object.RemovePart<CannotBeInfluenced>();

            if (!AlreadyPreacher)
                Object.RemovePart<Preacher>();
        }

        public bool FocusOnUsurper(GameObject Usurper)
        {
            if (Object?.Brain == null)
                return false;

            if (Usurper == null)
                return false;

            if (Object.Target == Usurper)
                return true;

            Object.AddOpinion<OpinionMoonKingJealous>(Usurper);
            Object.Brain.WantToKill(
                Subject: Usurper,
                Because: ($"=subject.subjective= =subject.verb:think= =subject.subjective==subject.verb:'re:afterpronoun= the {REGAL_TITLE} " +
                    $"but =subject.subjective==subject.verb:'re:afterpronoun= not me!")
                        .StartReplace()
                        .AddObject(Usurper)
                        .ToString()
                );

            if (!AlreadyPreacher
                && Object.TryGetPart(out Preacher preacher))
                preacher.PreacherHomily(Dialog: false);

            AIHelpBroadcastEvent.Send(Object, Usurper);
            return true;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register("AfterDeepCopyWithoutEffects");
            Registrar.Register("BeforeDeepCopyWithoutEffects");
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int cascade)
            => base.WantEvent(ID, cascade)
            || ID == PreferTargetEvent.ID
            || ID == GetFeelingEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            ;

        public override bool HandleEvent(PreferTargetEvent E)
        {
            if (E.Target1.IsPlayer()
                || E.Target2.IsPlayer())
            { 
                E.Result = E.Target1.IsPlayer().CompareTo(E.Target2.IsPlayer());
                return true;
            }
            if (E.Target1.HasEffect<UD_Bones_MoonKingFever>()
                || E.Target2.HasEffect<UD_Bones_MoonKingFever>())
            { 
                E.Result = E.Target1.HasEffect<UD_Bones_MoonKingFever>().CompareTo(E.Target2.HasEffect<UD_Bones_MoonKingFever>());
                return true;
            }
            if (E.Target1.HasPart<UD_Bones_LunarRegent>()
                || E.Target2.HasPart<UD_Bones_LunarRegent>())
            { 
                E.Result = E.Target1.HasPart<UD_Bones_LunarRegent>().CompareTo(E.Target2.HasPart<UD_Bones_LunarRegent>());
                return true;
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetFeelingEvent E)
        {
            if (E.Target.IsPlayer()
                || E.Target.HasEffect<UD_Bones_MoonKingFever>()
                || E.Target.HasPart<UD_Bones_LunarRegent>())
            {
                E.Feeling = -100;
                return false;
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (Object?.Brain != null)
            {
                if (Object.Brain.FindGoal(nameof(Kill)) is not Kill killGoal
                    || !killGoal.Target.IsPlayer())
                {
                    if (FocusOnUsurper(The.Player))
                        return true;
                }
            }
            return base.HandleEvent(E);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "BeforeDeepCopyWithoutEffects")
                UnapplyChanges();
            else
            if (E.ID == "AfterDeepCopyWithoutEffects")
                ApplyChanges();

            return base.FireEvent(E);
        }

        public override bool Render(RenderEvent E)
        {
            if (Object?.Render?.Visible is not false)
            {
                int frame = XRLCore.CurrentFrame % 60;
                if (frame > 5
                    && frame < 10)
                {
                    E.RenderString = "@";
                    E.ColorString = Crayons.GetRandomColorAll();
                }
                return true;
            }
            return Render(E);
        }
    }
}
