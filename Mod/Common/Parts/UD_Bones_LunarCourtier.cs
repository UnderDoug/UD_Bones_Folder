using System;
using System.Collections.Generic;
using System.Text;

using XRL.Core;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

using SerializeField = UnityEngine.SerializeField;
using XRL.Rules;
using XRL.World.AI;
using XRL.World.Parts.Mutation;
using XRL.World.Parts.Skill;
using XRL.Language;
using XRL.Collections;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarCourtier : UD_Bones_BaseLunarSubject
    {
        public struct Appointment
        {
            public bool IsPlural;
            public string Male;
            public string Female;
            public string Neutral;

            public readonly Appointment GetPlural()
                => new Appointment
                {
                    IsPlural = true,
                    Male = Male.Pluralize(),
                    Female = Female.Pluralize(),
                    Neutral = Neutral.Pluralize(),
                };

            public readonly string GetFor(GameObject Courtier, string Adjective = null)
            {
                if (Courtier?.IsPlural is true
                    && !IsPlural)
                    return GetPlural().GetFor(Courtier, Adjective);

                string appointment = Neutral;
                if (Courtier != null)
                {
                    if (Courtier?.GetGender() is Gender courtierGender)
                    {
                        if (courtierGender.Name.ToLower().Contains("male"))
                            appointment = Male;
                        else
                        if (courtierGender.Name.ToLower().Contains("female"))
                            appointment = Female;
                    }
                }
                if (!Adjective.IsNullOrEmpty())
                    appointment = $"{Adjective} {appointment}";
                return appointment;
            }

            public override readonly string ToString()
                => $"{nameof(Male)}: {Male}, {nameof(Female)}: {Female}, {nameof(Neutral)}: {Neutral}"
                ;
        }

        public static List<Appointment> LunarApointments => new()
        {
            new Appointment
            {
                Male = "Baron",
                Female = "Baroness",
                Neutral = "Barum",
            },
            new Appointment
            {
                Male = "Baronet",
                Female = "Baronetess",
                Neutral = "Baronetum",
            },
            new Appointment
            {
                Male = "Comte",
                Female = "Comtesse",
                Neutral = "Comtum",
            },
            new Appointment
            {
                Male = "Consort",
                Female = "Consort",
                Neutral = "Consort",
            },
            new Appointment
            {
                Male = "Count",
                Female = "Countess",
                Neutral = "Countum",
            },
            new Appointment
            {
                Male = "Duke",
                Female = "Duchess",
                Neutral = "Dukum",
            },
            new Appointment
            {
                Male = "Grand Duke",
                Female = "Grand Duchess",
                Neutral = "Grand Dukum",
            },
            new Appointment
            {
                Male = "Lord",
                Female = "Lady",
                Neutral = "Laird",
            },
            new Appointment
            {
                Male = "Marquess",
                Female = "Marchioness",
                Neutral = "Marquem",
            },
            new Appointment
            {
                Male = "Prince",
                Female = "Princess",
                Neutral = "Princum",
            },
            new Appointment
            {
                Male = "Vicomte",
                Female = "Vicomtesse",
                Neutral = "Vicomtum",
            },
            new Appointment
            {
                Male = "Viscount",
                Female = "Viscountess",
                Neutral = "Viscountum",
            },
            new Appointment
            {
                Male = "Jester",
                Female = "Jester",
                Neutral = "Jester",
            },
        };

        protected string TileColor;
        protected string DetailColor;

        public string AppointmentAdjective => (ParentObject?.BaseID ?? Stat.RandomCosmetic(0, 7000)) % 4 > 0
            ? "Moon"
            : "Lunar"
            ;

        protected string AdjectiveCache;

        private int AppointmentIndex => (ParentObject?.BaseID ?? Stat.RandomCosmetic(0, 7000)) % LunarApointments.Count;
        public Appointment LunarAppointment => LunarApointments[AppointmentIndex];

        private string _BakedLunarAppointment;
        public string BakedLunarAppointment 
            => ParentObject != null 
            ? _BakedLunarAppointment ??= LunarAppointment.GetFor(ParentObject, AppointmentAdjective)
            : LunarAppointment.GetFor(null, AppointmentAdjective)
            ;

        public Type AllyReasonType;

        private bool _DoneAllyship;
        public bool DoneAllyship
        {
            get => _DoneAllyship;
            protected set => _DoneAllyship = value;
        }

        public override NoInfluenceSet NoInfluence => new NoInfluenceSet
        {
            Name = nameof(UD_Bones_LunarRegent),
            DisplayName = LunarRegent != null
                ? $"dedication to =subject.refname="
                    .StartReplace()
                    .AddObject(LunarRegent)
                    .ToString()
                : MissingLunarRegent,
            Exclusions = new List<string>
            {
                nameof(Domination),
            },
            Messages = new Dictionary<string, string>
            {
                {
                    nameof(Beguiling),
                    "=subject.T= knows there is only one =LunarShader:Moon King=. =subject.Subjective= also knows it's not =object.objective=!"
                },
                {
                    nameof(Persuasion_Proselytize),
                    "Are you sure you don't want to join us instead? Well... there can only be one!"
                },
                {
                    nameof(LoveTonicApplicator),
                    "The tonic failed to cure =subject.t= of =subject.possessive= @@DisplayName@@!"
                },
                {
                    "default",
                    "=subject.T's= @@DisplayName@@ makes =subject.objective= insensible to your blandishments!"
                },
            }
        };

        public override void FinalizeRead(SerializationReader Reader)
        {
            base.FinalizeRead(Reader);
            if (AllyReasonType == null
                || AllyReasonType.InheritsFrom(typeof(IAllyReason)))
                AllyReasonType = typeof(AllyProselytize);
        }

        public override void Attach()
        {
            base.Attach();
            var colorsPart = ParentObject.RequirePart<UD_Bones_LunarColors>();
            colorsPart.Persists = true;
            PerformAllyship();
        }

        public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
        {
            var part = base.DeepCopy(Parent, MapInv) as UD_Bones_LunarCourtier;
            part.DoneAllyship = false;
            part.AdjectiveCache = null;
            part._BakedLunarAppointment = null;
            return part;
        }

        public bool PerformAllyship(
            GameObject LunarRegent = null,
            bool Force = false,
            bool Initial = false
            )
        {
            if (!Force
                && !Initial)
            {
                if (!DoneAllyship)
                    return DoneAllyship;

                if (BonesID == The.Game.GameID)
                    return DoneAllyship;

                if (LunarRegent == null
                    && ParentObject.CurrentZone is Zone currentZone
                    && !currentZone.TryFindLunarRegent(BonesID, out LunarRegent))
                    return DoneAllyship;
            }
            try
            {
                if (ParentObject.Brain is Brain brain
                    && Activator.CreateInstance(AllyReasonType) is IAllyReason allyReason)
                {
                    DoneAllyship = true;

                    var courtierParty = brain.PartyMembers;

                    if (Initial)
                        brain.Allegiance?.Clear();

                    SetLunarRegentReference(LunarRegent);
                    brain.TakeAllegiance(LunarRegent, allyReason);
                    brain.SetPartyLeader(LunarRegent, Silent: true);

                    if (!courtierParty.Values.IsNullOrEmpty())
                    {
                        using var partyMembers = ScopeDisposedList<PartyMember>.GetFromPoolFilledWith(courtierParty.Values);
                        foreach (var partyMember in partyMembers)
                        {
                            if (partyMember.Reference?.Object is not GameObject partyMemberObject
                                || partyMemberObject == LunarRegent
                                || partyMemberObject.Brain is not Brain partyMemberBrain)
                                continue;

                            partyMemberBrain.SetPartyLeader(ParentObject, Silent: true);
                        }
                    }

                    string parentObjectName = ParentObject?.DebugName ?? "NO_COURTIER";
                    string lunarRegentName = LunarRegent?.DebugName ?? "NO_REGENT";
                    string allyReasonName = allyReason.GetType().Name;
                    Utils.Log($"{parentObjectName} made follower of {lunarRegentName} for reason {allyReasonName}.");
                }
            }
            catch (Exception x)
            {
                string allyTypeName = AllyReasonType?.Name ?? "NO_ALLY_TYPE";
                string parentObjectName = ParentObject?.DebugName ?? "NO_COURTIER";
                Utils.Error($"Failed to create instance of {allyTypeName} for {parentObjectName}", x);
                DoneAllyship = true;
            }
            return DoneAllyship;
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public string GetAdjective()
            => AdjectiveCache ??= UD_Bones_LunarColors.ApplyAnimatedLunarShader("lunar courtier", TileColor);

        public string GetDescription()
        {
            string lunarRegents = !BakedLunarRegentName.IsNullOrEmpty() 
                ? Grammar.MakePossessive(BakedLunarRegentName)
                : Grammar.MakePossessive(MissingLunarRegent)
                ;

            string possessive = "their";

            if (LunarRegent != null)
                possessive = "=object.possessive=";

            var sB = Event.NewStringBuilder();
            sB.Append(GetAdjective().Capitalize()).Append(": ")
                .Append(ParentObject.ThisTheseDescriptiveCategory()).Append(" =subject.verb:are= a member of ")
                .Append(lunarRegents).Append(" =LunarShader:Moon Court:").Append(ParentObject.BaseID)
                .Append("= and respects only ").Append(possessive).Append(" authority!");

            var rB = Event.FinalizeString(sB)
                .StartReplace()
                .AddObject(ParentObject);

            if (LunarRegent != null)
                rB.AddObject(LunarRegent);

            return rB.ToString();
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(TidyLunarObjectsEvent.ID, EventOrder.EXTREMELY_EARLY, true);
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDisplayNameEvent.ID
            || ID == GetShortDescriptionEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == ZoneActivatedEvent.ID
            || ID == LunarObjectColorChangedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            string appointment = $"=LunarShader:{BakedLunarAppointment}:{ParentObject.BaseID}="
                .StartReplace()
                .ToString();
            int orderAdjustment = DescriptionBuilder.ORDER_ADJUST_LATE;
            switch (ParentObject.BaseID % 3)
            {
                case 0:
                    orderAdjustment = 0;
                    E.AddHonorific(appointment, orderAdjustment);
                    break;
                case 1:
                    E.AddTitle(appointment, orderAdjustment);
                    break;
                case 2:
                default:
                    E.AddEpithet(appointment, orderAdjustment);
                    break;
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            E.Postfix.AppendRules(GetDescription());
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(ZoneActivatedEvent E)
        {

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(LunarObjectColorChangedEvent E)
        {
            if (Options.EnableFlashingLightEffects
                || (E.LastFrame + ParentObject.BaseID).NegSafeModulo(UD_Bones_LunarColors.BaseAnimationLengthInFrames) == 0)
            {
                AdjectiveCache = null;
            }
            TileColor = E.TileColor;
            DetailColor = E.DetailColor;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(TidyLunarObjectsEvent E)
        {
            if (E.Context == "Wish")
            {
                bool bonesIDMatches = BonesID == E.BonesID
                    || E.BonesID == null;

                bool bonesMatchAndPersists = bonesIDMatches
                    && Persists;

                if (bonesMatchAndPersists)
                {
                    PerformAllyship(The.Player, Force: true, Initial: true);
                    foreach (var lunarPart in ParentObject.GetPartsDescendedFrom<UD_Bones_BaseLunarPart>())
                    {
                        if (lunarPart != this)
                        {
                            if (lunarPart.BonesID == E.BonesID
                                || E.BonesID == null)
                            {
                                ParentObject.RemovePart(lunarPart);
                            }
                        }
                    }
                    ParentObject.RemovePart(this);
                    return true;
                }
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(LunarAppointment), LunarAppointment.GetFor(ParentObject, AppointmentAdjective));
            E.AddEntry(this, nameof(BakedLunarAppointment), BakedLunarAppointment);
            E.AddEntry(this, nameof(DoneAllyship), DoneAllyship);
            E.AddEntry(this, nameof(ParentObject.Brain.PartyLeader), ParentObject?.Brain?.PartyLeader?.DebugName ?? "NO_LEADER");
            return base.HandleEvent(E);
        }
    }
}
