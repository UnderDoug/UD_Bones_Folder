using System;

using XRL.Core;
using XRL.World.Parts;
using XRL.World.AI.GoalHandlers;

using UD_Bones_Folder.Mod;
using System.Collections.Generic;

namespace XRL.World.Effects
{
    [Serializable]
    public class UD_Bones_MoonKingFever : IScribedEffect
    {
        public const int MAX_DIST = 9999;

        public const string REGAL_TITLE = "Moon Regent";

        public static string[] MoonKingColors = new string[7] { "r", "R", "W", "G", "B", "b", "m", };

        public static Dictionary<string, string> NoInfluenceMessages => new()
        {
            {
                "Beguiling",
                "=subject.T= knows there is only one =LunarShader:Moon King=. =subject.Subjective= also knows it's =subject.objective=!"
            },
            {
                "Persuasion_Proselytize",
                "Are you sure you don't want to join =subject.t= instead? Well... there can only be one!"
            },
            {
                "LoveTonicApplicator",
                "The tonic failed to cure =subject.t= of =subject.possessive= @@DisplayName@@!"
            },
            {
                "default",
                "=subject.T's= @@DisplayName@@ makes =subject.objective= insensible to your blandishments!"
            },
        };

        private int OriginalMaxKillDistance;

        private bool AlreadyPreacher;

        public string RegalTitle => UD_Bones_LunarRegent.GetRegalTitle(Object);

        private string RenderTileColor = "r";
        private int RenderTileColorCounter = 0;

        private int EffectCycleCounter;
        private int NameCycleCounter;

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

        public void SetDisplayName()
        {
            DisplayName = GetDisplayName();
        }

        public string GetDisplayName()
            => $"=LunarShader:{RegalTitle ?? REGAL_TITLE}:{(Object?.BaseID)?.ToString() ?? "*"}= {"fever".Colored("r")}"
                .StartReplace()
                .ToString()
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
            Registrar.Register("CanBeInfluenced");
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int cascade)
            => base.WantEvent(ID, cascade)
            || ID == PreferTargetEvent.ID
            || ID == GetFeelingEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool WantTurnTick()
            => true;

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
                        || !killGoal.Target.IsPlayer())
                    {
                        if (FocusOnUsurper(The.Player))
                            return true;
                    }
                }
            }
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
                else
                if (E.ID == "CanBeInfluenced")
                {
                    string influenceType = E.GetStringParameter("Type", "default");
                    if (influenceType != nameof(Parts.Mutation.Domination)
                        && NoInfluenceMessages.TryGetValue(influenceType, out string influenceMessage))
                    {
                        //Utils.Log($"CanBeInfluenced, Type: {influenceType}");
                        E.SetParameter("Message", influenceMessage.StartReplace().AddObject(Object).ToString().Replace("@@DisplayName@@", GetDisplayName()));
                        return false;
                    }
                }
            }
            return base.FireEvent(E);
        }

        public override bool Render(RenderEvent E)
        {
            int frameTarget = (int)Utils.FPS_MODULO;
            bool counterOverride = NameCycleCounter++ > frameTarget;
            if (Utils.GetFPSModuloOrRandom(Object.BaseID) == 0
                || counterOverride)
            {
                NameCycleCounter = Math.Max(0, NameCycleCounter - frameTarget);
                SetDisplayName();
            }

            E.RenderEffectIndicator(
                renderString: "@",
                tile: Const.MOON_KING_FEVER_TILE,
                colorString: $"&{RenderTileColor}",
                detailColor: Utils.GetNextRainbowColor(RenderTileColor).ToString(),
                frameHint: (Object.BaseID % 5) +1,
                durationHint: 10);

            frameTarget = (Object.BaseID % 5) + 1 + 10;
            counterOverride = EffectCycleCounter++ > frameTarget;
            if (Utils.CurrentFrame % 60 == frameTarget
                || counterOverride)
            {
                RenderTileColor = Utils.GetRainbowColorAtIndex(RenderTileColorCounter++);
                EffectCycleCounter = Math.Max(0, EffectCycleCounter - frameTarget);
            }

            return base.Render(E);
        }

        public override void TurnTick(long TimeTick, int Amount)
        {
            base.TurnTick(TimeTick, Amount);
        }
    }
}
