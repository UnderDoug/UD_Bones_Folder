using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

using XRL.Core;
using XRL.World.Effects;
using XRL.World.Parts.Mutation;
using XRL.World.Parts.Skill;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarRegent : UD_Bones_BaseLunarPart
    {
        public static int TitleHonorificOrderAdjust = 49;
        public static int TitleEpithetOrderAdjust = -600;

        public FileLocationData LocationData;

        public bool Cremated;

        public string RegalTitle => GetRegalTitle();

        protected bool? _IsMad;
        public bool IsMad
        {
            get
            {
                if (ParentObject != null)
                    _IsMad = ParentObject.GetPropertyOrTag(Const.IS_MAD_PROP, $"{_IsMad.GetValueOrDefault()}").EqualsNoCase($"{true}");
                return _IsMad.GetValueOrDefault();
            }
            set
            {
                _IsMad = value;
                ParentObject?.SetStringProperty(Const.IS_MAD_PROP, $"{_IsMad.GetValueOrDefault()}");
            }
        }

        public string OriginalShortDesc;

        private bool _DoneDescription;
        public bool DoneDescription
        {
            get => _DoneDescription;
            protected set => _DoneDescription = value;
        }

        private List<string> Reclaimed;

        private NoInfluenceSet _NoInfluence;
        public override NoInfluenceSet NoInfluence => _NoInfluence
            ?? (!Broken
                ? GetDefaultNoInfluenceSet()
                : GetBrokenNoInfluenceSet())
            ;

        private string BakedBrokenTitle;

        protected bool? _Broken;
        public bool Broken
        {
            get
            {
                if (ParentObject != null)
                    _Broken = ParentObject.GetPropertyOrTag(Const.IS_BROKEN_PROP, $"{_Broken.GetValueOrDefault()}").EqualsNoCase($"{true}");
                return _Broken.GetValueOrDefault();
            }
            set
            {
                if (_Broken != value)
                {
                    if (value)
                    {
                        BakedBrokenTitle = $"=LunarShader:{RegalTitle}:{ParentObject.BaseID}=".StartReplace().ToString();

                        if (IsMad)
                            BakedBrokenTitle = $"Mad {BakedBrokenTitle}";

                        BakedBrokenTitle = $"{nameof(Broken).ToLower()} {BakedBrokenTitle.FeverWarped()}";

                        BakedBrokenTitle = BakedBrokenTitle.StartReplace().ToString();
                    }
                    else
                        BakedBrokenTitle = null;

                    _NoInfluence = null;
                    _Broken = value;
                    ParentObject?.SetStringProperty(Const.IS_BROKEN_PROP, $"{_Broken.GetValueOrDefault()}");
                }
            }
        }

        private Version? MigrateFrom = null;

        public UD_Bones_LunarRegent()
            : base()
        { }

        public UD_Bones_LunarRegent(string BonesID)
            : base(BonesID)
        { }

        public override void Write(GameObject Basis, SerializationWriter Writer)
        {
            base.Write(Basis, Writer);
            if (!_IsMad.HasValue)
                Writer.WriteOptimized(-1);
            else
                Writer.WriteOptimized(_IsMad.GetValueOrDefault() ? 1 : 0);

            Writer.Write(_DoneDescription);
            Writer.Write(Reclaimed);
            Writer.WriteOptimized(BakedBrokenTitle);
        }

        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            var modVersion = Reader.ModVersions[Utils.ThisMod.ID];
            if (modVersion < new Version("0.0.3.5"))
                MigrateFrom = modVersion;

            base.Read(Basis, Reader);

            if (modVersion >= new Version("0.0.3.5"))
            {
                _IsMad = Reader.ReadOptimizedInt32() switch
                {
                    >= 1 => true,
                    >= 0 => false,
                    _ => null,
                };
                _DoneDescription = Reader.ReadBoolean();
                Reclaimed = Reader.ReadList<string>();
                BakedBrokenTitle = Reader.ReadOptimizedString();
            }
        }

        public override void FinalizeRead(SerializationReader Reader)
        {
            base.FinalizeRead(Reader);

        }

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

        public SaveBonesInfo GetSourceSaveBonesInfo(bool AggregateLocations = true)
        {
            if (AggregateLocations)
                return BonesManager.System.GetSavedBonesByID(BonesID);

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

        public NoInfluenceSet GetDefaultNoInfluenceSet()
            => new NoInfluenceSet
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

        public NoInfluenceSet GetBrokenNoInfluenceSet()
            => new NoInfluenceSet
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
                        "=subject.T's= @@DisplayName@@ has subsided, but their resolve is no weaker for it!"
                    },
                    {
                        nameof(Persuasion_Proselytize),
                        "=subject.T= knows with certainty that =subject.substantivePossessive= is no longer that path!"
                    },
                    {
                        nameof(LoveTonicApplicator),
                        "The tonic failed to overcome =subject.t's= innoculation from breaking =subject.possessive= @@DisplayName@@!"
                    },
                    {
                        "default",
                        "=subject.T's= newly redirected resolve makes =subject.objective= insensible to your blandishments!"
                    },
                }
            };

        public void Onset()
        {
            if (!Broken)
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

        public void IncrementBroken()
        {
            if (GetSourceSaveBonesInfo() is SaveBonesInfo saveBonesInfo)
                saveBonesInfo.IncrementBroken();
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == KilledPlayerEvent.ID
            || ID == AfterDieEvent.ID
            || ID == TakeOnRoleEvent.ID
            || ID == GetDisplayNameEvent.ID
            || ID == GetShortDescriptionEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == AfterBonesZoneLoadedEvent.ID
            || ID == AfterGameLoadedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(KilledPlayerEvent E)
        {
            if (E.Killer == ParentObject
                && !Broken)
                IncrementReclaimed();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AfterDieEvent E)
        {
            if (E.Dying == ParentObject
                && !Broken)
                IncrementDefeated();
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(TakeOnRoleEvent E)
        {
            if (E.Object == ParentObject)
            {
                if (!Broken)
                {
                    Broken = true;
                    IncrementBroken();
                }
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (!E.WithoutTitles)
            {
                bool isBroken = Broken
                && !BakedBrokenTitle.IsNullOrEmpty();

                string title = !isBroken
                    ? $"=LunarShader:{RegalTitle}:{ParentObject.BaseID}=".StartReplace().ToString()
                    : BakedBrokenTitle
                    ;

                if (!isBroken)
                {
                    int titleHonAdjust = TitleHonorificOrderAdjust;
                    E.AddHonorific(title, titleHonAdjust);

                    if (IsMad)
                        E.AddHonorific("Mad", --titleHonAdjust);
                }
                else
                    E.AddEpithet(title, TitleEpithetOrderAdjust);
            }
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
        public override bool HandleEvent(AfterGameLoadedEvent E)
        {
            if (MigrateFrom != null
                && ParentObject is GameObject lunarRegent)
            {
                // Do migration code here if necessary.
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
