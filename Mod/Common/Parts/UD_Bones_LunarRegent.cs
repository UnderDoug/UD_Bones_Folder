using System;
using System.Collections.Generic;
using System.Text;

using XRL.Core;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

using SerializeField = UnityEngine.SerializeField;
using XRL.World.Parts.Mutation;
using XRL.World.Parts.Skill;
using System.Linq;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarRegent : UD_Bones_BaseLunarPart
    {
        public FileLocationData LocationData;

        public bool Cremated;

        public string RegalTitle => GetRegalTitle();

        protected bool? _IsMad;
        public bool IsMad
        {
            get
            {
                if (ParentObject != null)
                    _IsMad = ParentObject.GetStringProperty(Const.IS_MAD_PROP, $"{_IsMad.GetValueOrDefault()}").EqualsNoCase($"{true}");
                return _IsMad.GetValueOrDefault();
            }
            set
            {
                _IsMad = value;
                ParentObject?.SetStringProperty(Const.IS_MAD_PROP, _IsMad.GetValueOrDefault() ? $"{true}" : null, true);
            }
        }

        public string OriginalShortDesc;

        [SerializeField]
        private bool _DoneDescription;
        public bool DoneDescription
        {
            get => _DoneDescription;
            protected set => _DoneDescription = value;
        }

        [SerializeField]
        private List<string> Reclaimed;

        public override NoInfluenceSet NoInfluence => new NoInfluenceSet
        {
            Name = nameof(UD_Bones_LunarRegent),
            DisplayName = ParentObject?.GetEffect<UD_Bones_MoonKingFever>()?.GetDisplayName()
                ?? new UD_Bones_MoonKingFever().GetDisplayName(ParentObject?.Render?.TileColor),
            Exclusions = new List<string>
            {
                nameof(Domination),
            },
            Messages = new Dictionary<string, string>
            {
                {
                    nameof(Beguiling),
                    "=subject.T= knows there is only one =LunarShader:Moon King=. =subject.Subjective= also knows it's =subject.objective=!"
                },
                {
                    nameof(Persuasion_Proselytize),
                    "Are you sure you don't want to join =subject.t= instead? Well... there can only be one!"
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

        public static string GetRegalTitle(GameObject LunarRegent)
        {
            string regalTerm = "Sovran";
            if (LunarRegent?.GetGender() is Gender regentGender)
            {
                if (regentGender.Name.ToLower().Contains("male"))
                    regalTerm = "King";

                if (regentGender.Name.ToLower().Contains("female"))
                    regalTerm = "Queen";

                if (regentGender.Plural)
                    regalTerm = regalTerm.Pluralize();
            }
            return $"Moon {regalTerm}";
        }

        public SaveBonesInfo GetSourceSaveBonesInfo()
        {
            if (LocationData is not null)
            {
                if (LocationData.Host is OsseousAsh.Host storedHost)
                {
                    if (OsseousAsh.Hosts?.FirstOrDefault(hc => hc.Any(h => h.SameAs(storedHost, true))) is OsseousAsh.HostCollection hostCollection
                        && hostCollection.FirstOrDefault(h => h.SameAs(storedHost, true)) is OsseousAsh.Host actualHost)
                    {
                        if (actualHost.Enabled)
                            return actualHost.GetSaveBonesInfo(BonesID);
                        
                        Utils.Warn($"{actualHost} is disabled and can't have bones retreived from it.");
                        return null;
                    }
                    else
                        Utils.Warn($"{storedHost} no longer exists and can't have bones retreived from it.");
                }

                if (BonesManager.System is BonesManager system
                    && system.TryGetSaveBonesByID(BonesID, out var saveBonesInfo, bonesInfo => bonesInfo.FileLocationData?.SameAs(LocationData) is true))
                    return saveBonesInfo;
            }

            Utils.Warn($"Couldn't find bones info in {LocationData?.ToString() ?? "MISSING_LOCATION_DATA"}");
            return null;
        }

        public string GetRegalTitle()
            => GetRegalTitle(ParentObject)
            ;

        public void Onset()
        {
            ParentObject.ApplyEffect(new UD_Bones_MoonKingFever());
        }

        public override void Attach()
        {
            base.Attach();
            ParentObject.SetStringProperty(Const.IS_MAD_PROP, $"{IsMad}");

            if (BonesID != The.Game.GameID
                && !OriginalShortDesc.IsNullOrEmpty())
            {
                if (ParentObject.TryGetPart(out Description description)
                    && description._Short == OriginalShortDesc)
                    DoneDescription = false;
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            ParentObject.SetStringProperty(Const.IS_MAD_PROP, $"{IsMad}");
            var bonesColors = ParentObject.RequirePart<UD_Bones_LunarColors>()
                .OverrideBonesID<UD_Bones_LunarColors>(BonesID);
            bonesColors.Persists = true;

            if (ParentObject.TryGetPart(out Description description))
                OriginalShortDesc = description._Short;
            else
                OriginalShortDesc = "It was you.";
        }

        public void IncrementReclaimed()
        {
            Reclaimed ??= new();
            if (!Reclaimed.Contains(The.Game.GameID)
                && GetSourceSaveBonesInfo() is SaveBonesInfo saveBonesInfo)
            {
                saveBonesInfo.IncrementReclaimed();
                Reclaimed.Add(The.Game.GameID);
            }
        }

        public void IncrementDefeated()
        {
            if (GetSourceSaveBonesInfo() is SaveBonesInfo saveBonesInfo)
                saveBonesInfo.IncrementDefeated();
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == KilledPlayerEvent.ID
            || ID == AfterDieEvent.ID
            || ID == GetDisplayNameEvent.ID
            || ID == GetShortDescriptionEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == AfterBonesZoneLoadedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(KilledPlayerEvent E)
        {
            if (E.Killer == ParentObject)
                IncrementReclaimed();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AfterDieEvent E)
        {
            if (E.Dying == ParentObject)
                IncrementDefeated();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            E.AddHonorific($"=LunarShader:{GetRegalTitle()}:{ParentObject.BaseID}=".StartReplace().ToString());
            if (IsMad)
                E.AddHonorific("mad", DescriptionBuilder.ORDER_ADJUST_SLIGHTLY_EARLY);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (!DoneDescription
                && BonesID != The.Game?.GameID
                && !OriginalShortDesc.IsNullOrEmpty()
                && ParentObject.TryGetPart(out Description description)
                && description._Short == OriginalShortDesc)
            {
                DoneDescription = true;
                description._Short = OriginalShortDesc.StartReplace().ToString();
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (!Cremated
                && BonesManager.System != null
                && BonesManager.System.TryGetSaveBonesByID(BonesID, out var bonesInfo))
            {
                // bonesInfo.Cremate();
                Cremated = true;
            }
            if (ParentObject != null
                && !ParentObject.IsPlayer()
                && !ParentObject.HasEffect<UD_Bones_MoonKingFever>())
                Onset();

            if (ParentObject.Render is Render lunarRender
                && !lunarRender.Visible)
                lunarRender.Visible = true;

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AfterBonesZoneLoadedEvent E)
        {
            if (ParentObject.CurrentCell is Cell currentCell
                // && currentCell.GetObjectCountWithPart(nameof(Gas)) > 0
                )
            {
                int forceLevel = 4;
                if (currentCell.GetLocalAdjacentCells() is List<Cell> adjacentCells)
                {
                    if (adjacentCells.All(c => c.IsSolidFor(ParentObject)))
                    {
                        currentCell.Clear(Combat: true, alsoExclude: go => go == ParentObject);
                        /*Physics.ApplyExplosion(currentCell, 15000, Local: true, Show: true, Owner: ParentObject, Neutron: true, DamageModifier: 0f, WhatExploded: ParentObject);
                        forceLevel = 25;*/
                    }
                }
                Physics.ApplyExplosion(currentCell, 15000, Local: true, Show: true, Owner: ParentObject, Neutron: true, DamageModifier: 0f, WhatExploded: ParentObject);
                forceLevel = 25;
                StunningForce.Concussion(StartCell: currentCell, ParentObject: ParentObject, Level: forceLevel, Distance: 1, Stun: false, Damage: false);
            }

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(Cremated), Cremated);
            E.AddEntry(this, nameof(RegalTitle), RegalTitle);
            E.AddEntry(this, nameof(DoneDescription), DoneDescription);
            return base.HandleEvent(E);
        }
    }
}
