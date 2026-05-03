using System;

using XRL.Core;
using XRL.World.Parts;
using XRL.World.AI.GoalHandlers;

using UD_Bones_Folder.Mod;
using System.Collections.Generic;
using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Effects
{
    [Serializable]
    public class UD_Bones_MoonKingFever
        : IScribedEffect
        , IModEventHandler<AfterBonesZoneLoadedEvent>
        , IModEventHandler<LunarObjectColorChangedEvent>
    {
        public const int MAX_DIST = 9999;

        public const string REGAL_TITLE = "Moon King";

        public static string[] MoonKingColors = new string[7] { "r", "R", "W", "G", "B", "b", "m", };

        private int OriginalMaxKillDistance;

        private bool AlreadyPreacher;

        public string RegalTitle => UD_Bones_LunarRegent.GetRegalTitle(Object);

        private string TileColor = "r";
        private string DetailColor = "R";

        public UD_Bones_MoonKingFever()
        {
            SetDisplayName();
            Duration = DURATION_INDEFINITE;
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

            SetDisplayName();

            ApplyChanges();
            return base.Apply(Object);
        }

        public override void Remove(GameObject Object)
        {
            UnapplyChanges();
            base.Remove(Object);
        }

        public void SetDisplayName(string TileColor = "r")
        {
            DisplayName = GetDisplayName(TileColor);
        }

        public string GetDisplayName(string TileColor = "r")
            => $"{UD_Bones_LunarColors.ApplyAnimatedLunarShader(RegalTitle ?? REGAL_TITLE, TileColor)} {"fever".Colored("r")}"
            ;

        private void ApplyChanges()
        {
            if (Object?.IsPlayer() is not true)
            {
                if (Object.Brain != null)
                {
                    OriginalMaxKillDistance = Object.Brain.MaxKillRadius;
                    Object.Brain.MaxKillRadius = MAX_DIST;
                }
                AlreadyPreacher = Object.HasPart<Preacher>();
                if (!AlreadyPreacher)
                {
                    var preacher = Object.AddPart<Preacher>();
                    preacher.Book = "UD_Bones_MoonKingFever";
                    preacher.Prefix = "=subject.T= =verb:proclaim= {{W|\'";
                    preacher.Postfix = "\'}}";
                }
            }
        }
        private void UnapplyChanges()
        {
            if (Object?.IsPlayer() is not true)
            {
                if (OriginalMaxKillDistance != 0
                && Object.Brain != null)
                {
                    Object.Brain.MaxKillRadius = OriginalMaxKillDistance;
                    OriginalMaxKillDistance = 0;
                }

                if (!AlreadyPreacher)
                    Object.RemovePart<Preacher>();
            }
        }

        public bool FocusOnUsurper(GameObject Usurper)
        {
            if (Object?.Brain == null)
                return false;

            if (Object.IsPlayer())
                return false;

            if (Usurper == null)
                return false;

            if (Object.Target == Usurper)
                return true;

            Object.AddOpinion<OpinionMoonKingJealous>(Usurper);
            Object.Brain.WantToKill(
                Subject: Usurper,
                Because: ($"=subject.subjective= =subject.verb:think= =subject.subjective==subject.verb:'re:afterpronoun= the {UD_Bones_LunarRegent.GetRegalTitle(Usurper)} " +
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
            || ID == AfterBonesZoneLoadedEvent.ID
            || ID == PreferTargetEvent.ID
            || ID == GetFeelingEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == LunarObjectColorChangedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public virtual bool HandleEvent(AfterBonesZoneLoadedEvent E)
        {
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(PreferTargetEvent E)
        {
            if (Object?.IsPlayer() is not true)
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
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetFeelingEvent E)
        {
            if (Object?.IsPlayer() is not true)
            {
                if (E.Target.IsPlayer()
                    || E.Target.HasEffect<UD_Bones_MoonKingFever>()
                    || E.Target.HasPart<UD_Bones_LunarRegent>())
                {
                    E.Feeling = -100;
                    return false;
                }
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (Object?.IsPlayer() is not true)
            {
                if (Object?.Brain != null)
                {
                    if (Object.Brain.FindGoal(nameof(Kill)) is not Kill killGoal
                        || killGoal?.Target?.IsPlayer() is false)
                    {
                        if (FocusOnUsurper(The.Player))
                            return true;
                    }
                }
            }
            return base.HandleEvent(E);
        }

        public virtual bool HandleEvent(LunarObjectColorChangedEvent E)
        {
            SetDisplayName(E.TileColor);
            TileColor = E.TileColor;
            DetailColor = E.DetailColor;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(OriginalMaxKillDistance), OriginalMaxKillDistance);
            E.AddEntry(this, nameof(AlreadyPreacher), AlreadyPreacher);
            E.AddEntry(this, nameof(Duration), Duration >= DURATION_INDEFINITE ? "\u00EC" : Duration.ToString());
            return base.HandleEvent(E);
        }

        public override bool FireEvent(Event E)
        {
            if (Object?.IsPlayer() is not true)
            {
                if (E.ID == "BeforeDeepCopyWithoutEffects")
                    UnapplyChanges();
                else
                if (E.ID == "AfterDeepCopyWithoutEffects")
                    ApplyChanges();
            }
            return base.FireEvent(E);
        }

        public override bool Render(RenderEvent E)
        {
            E.RenderEffectIndicator(
                renderString: "@",
                tile: Const.MOON_KING_FEVER_TILE,
                colorString: $"&{TileColor}",
                detailColor: DetailColor,
                frameHint: (Object.BaseID % 5) + 1,
                durationHint: 10);

            return base.Render(E);
        }
    }
}
