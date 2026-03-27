using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.AI.Pathfinding;

using Bones.Mod;
using XRL.Core;
using XRL.World.Parts;
using XRL.World.AI.GoalHandlers;

namespace XRL.World.Effects
{
    public class MoonKingFever : IScribedEffect
    {
        public const int MAX_DIST = 9999;

        private int OriginalMaxKillDistance;

        private bool AlreadyUninfluencable;

        private bool AlreadyPreacher;

        public MoonKingFever()
        {
            DisplayName = "{{rainbow|Moon King}} {{r|fever}}";
            Duration = 1;
        }

        public override int GetEffectType()
            => TYPE_NEGATIVE
            ^ TYPE_MENTAL
            ^ TYPE_DIMENSIONAL
            ^ TYPE_DISEASE
            ;

        public override bool Apply(GameObject Object)
        {
            if (Object.HasEffect<MoonKingFever>())
                return false;

            if (!Object.FireEvent(Event.New($"Apply{nameof(MoonKingFever)}")))
                return false;

            ApplyChanges();
            return base.Apply(Object);
        }

        public override void Remove(GameObject Object)
        {
            UnapplyChanges();
            base.Remove(Object);
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
                noInfluence.Messages = "Beguiling::=subject.T= knows there is only one {{rainbow|Moon King}}. =subject.Subjective= also knows it's =subject.objective=!;;" +
                    "Persuasion_Proselytize::Are you sure you don't want to join =subject.t= instead? Well... there can only be one!;;" +
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
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetFeelingEvent E)
        {
            if (E.Target.IsPlayer())
            {
                E.Feeling = -100;
                return false;
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (!Object.Brain.HasGoal(nameof(Kill))
                || !Object.Target.IsPlayer())
            {

                Object.AddOpinion<OpinionMoonKingJealous>(The.Player);
                Object.Target = The.Player;

                if (!AlreadyPreacher
                    && Object.TryGetPart(out Preacher preacher))
                {
                    preacher.PreacherHomily(true);
                }

                AIHelpBroadcastEvent.Send(Object, The.Player);
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
            if (Duration > 0)
            {
                int frame = XRLCore.CurrentFrame % 60;
                if (frame > 5
                    && frame < 10)
                {
                    E.RenderString = "@";
                    E.ColorString = Crayons.GetRandomColorAll();
                }
            }
            return true;
        }
    }
}
