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
using XRL.World.AI.GoalHandlers;
using XRL.World.AI;
using UD_Bones_Folder.Mod.Serialization.PseudoTypes;
using UD_Bones_Folder.Mod.Parts;
using XRL.Collections;
using XRL.Rules;
using UD_Bones_Folder.Mod.UI;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarRegent
        : UD_Bones_BaseLunarPart
        , IFragileObjectHolderPart
        , ILoadLunarRegentEventHandler
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

        public string BakedShortDesc;

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

        public override bool CanBeFragile => false;

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
            // ParentObject.SetStringProperty(Const.IS_MAD_PROP, $"{IsMad}");

            if (BonesID != The.Game.GameID
                && !BakedShortDesc.IsNullOrEmpty())
            {
                if (ParentObject.TryGetPart(out Description description)
                    && description._Short == BakedShortDesc)
                    DoneDescription = false;
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            ParentObject.SetStringProperty(Const.IS_MAD_PROP, $"{IsMad}");
            var bonesColors = ParentObject.RequirePart<UD_Bones_LunarColors>().OverrideBonesIDTyped<UD_Bones_LunarColors>(BonesID);
            bonesColors.Persists = true;

            if (ParentObject.TryGetPart(out Description description))
                BakedShortDesc = description._Short;
            else
                BakedShortDesc = "It was you.";
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

        public static void ApplyHostility(GameObject Actor, Brain Brain, int Depth)
        {
            if (Actor == null)
                return;
            if (Depth >= 100)
                return;

            Brain.AddOpinion<OpinionInscrutable>(Actor);

            ApplyHostility(Actor.PartyLeader, Brain, Depth + 1);

            if (Actor.TryGetEffect<Dominated>(out var Effect))
                ApplyHostility(Effect.Dominator, Brain, Depth + 1);
        }

        public void SetBakedShortDesc(
            bool IsYou,
            string WhoItWas,
            string Prefix = null,
            string Postfix = null,
            string FullOverride = null
            )
        {
            if (ParentObject == null)
                return;

            if (ParentObject.TryGetPart(out Description description))
            {
                if (FullOverride == null)
                {
                    if (IsYou)
                        WhoItWas = $"you";

                    BakedShortDesc = $"It was {WhoItWas}.";

                    AdjustBakedShortDesc(Prefix, Postfix, Bake: false);
                }
                else
                    BakedShortDesc = FullOverride;

                description._Short = BakedShortDesc;
                DoneDescription = false;

            }
        }

        public void AdjustBakedShortDesc(
            string Prefix = null,
            string Postfix = null,
            bool Bake = true
            )
        {
            if (ParentObject == null)
                return;

            if (ParentObject.TryGetPart(out Description description))
            {
                if (!Prefix.IsNullOrEmpty())
                    BakedShortDesc = $"{Prefix}{BakedShortDesc}";

                if (!Postfix.IsNullOrEmpty())
                    BakedShortDesc = $"{BakedShortDesc}{Postfix}";

                if (Bake)
                    description._Short = BakedShortDesc;
            }

            if (!Prefix.IsNullOrEmpty()
                || !Postfix.IsNullOrEmpty())
                DoneDescription = false;
        }

        public static bool ObjectShouldBeHeldOnto(GameObject Item)
            => Item.GetBlueprint() is GameObjectBlueprint itemBlueprint
            && (itemBlueprint.InheritsFromSafe("Grenade")
                || itemBlueprint.InheritsFromSafe("Tonic")
                || itemBlueprint.InheritsFromSafe("Projectile")
                || itemBlueprint.InheritsFromSafe("Energy Cell")
                || itemBlueprint.InheritsFromSafe("BaseThrownWeapon"))
            ;

        public static bool IsAbleToBeMadeFragile(GameObject Object, GameObject LunarReliquary)
        {
            if (Object == LunarReliquary)
                return false;

            if (Object.HasPart(p => p is UD_Bones_FragileLunarObject))
                return false;

            if (!Object.CanBeLunarFragile())
                return false;

            if (Object.GetTagOrStringProperty(Const.NOT_FRAGILE_PROPTAG, $"{false}").EqualsNoCase($"{true}"))
                return false;

            return true;
        }

        public static bool MakeInventoryFragile(
            GameObject LunarCreature,
            GameObject LunarReliquary = null,
            string Context = null
            )
        {
            if (LunarCreature == null)
                return false;

            string bonesID = LunarCreature.GetBonesID();

            // Utils.Log($"{nameof(MakeInventoryFragile)} for {LunarCreature?.DebugName ?? "NO_OBJECT"}");

            var creatureBelongings = LunarCreature.GetInventoryEquipmentAndCybernetics().IteratorSafe();
            using var lunarCreatureInventoryList = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(creatureBelongings);

            if (LunarReliquary != null)
            {
                if (!LunarCreature.ReceiveObject(LunarReliquary, Context: Context))
                {
                    Utils.Error($"Failed to give {LunarCreature?.DebugName?.Strip() ?? "NO_LUNAR_CREATURE"} =subject.possessive= {nameof(LunarReliquary)}"
                        .StartReplace()
                        .AddObject(LunarCreature)
                        );
                    LunarReliquary?.Obliterate();
                    LunarReliquary = null;
                }
            }

            bool isNotFragileObjectAndNotReliquary(GameObject gameObject)
                => IsAbleToBeMadeFragile(gameObject, LunarReliquary)
                ;

            string seededMethod = $"{nameof(MakeInventoryFragile)}::{LunarCreature.BaseID}";

            using var objectsToDestroy = ScopeDisposedList<GameObject>.GetFromPool();
            while (LunarCreature.GetInventoryEquipmentAndCybernetics(isNotFragileObjectAndNotReliquary).FirstOrDefault() is GameObject item)
            {
                //Utils.Log($"{1.Indent()}{nameof(MakeInventoryFragile)}::{item?.DebugName ?? "NO_ITEM"}, {nameof(item.Count)} {item?.Count ?? -1}");
                var objectBlueprint = item.GetBlueprint();

                item.PerformActionRecursively(delegate (GameObject go)
                {
                    var fragilePart = item.RequirePart<UD_Bones_FragileLunarObject>()
                        .OverrideBonesIDTyped<UD_Bones_FragileLunarObject>(bonesID);

                    fragilePart.WantsRemoveOnDamage = true;
                    if (!LunarCreature.IsLunarRegent())
                        fragilePart.Persists = true;
                });

                var fragilePart = item.RequirePart<UD_Bones_FragileLunarObject>()
                    .OverrideBonesIDTyped<UD_Bones_FragileLunarObject>(bonesID);
                fragilePart.WantsRemoveOnDamage = true;

                if (item.Equipped != null
                    || item.Implantee != null)
                    continue;

                if (LunarReliquary != null)
                {
                    if (ObjectShouldBeHeldOnto(item))
                    {
                        if (item.Count <= 1)
                            continue;

                        int high = (int)Math.Ceiling(item.Count * 0.5);
                        int count = BonesModeModule.GetNAdvantage(seededMethod, 0, high, 2, item.BaseID);
                        item = item.SplitStack(count, LunarCreature, NoRemove: true);
                    }
                    LunarCreature.Inventory?.RemoveObject(item);
                    LunarReliquary.ReceiveObject(item, Context: Context);
                    continue;
                }
                if (LunarCreature.IsLunarRegent()
                    && Context == PseudoZone.RECLAIM_CONTEXT)
                {
                    fragilePart.WantsToBeDropped = true;
                }
                else
                if (LunarCreature.IsLunarCourtier()
                    && Context == PseudoZone.RECLAIM_CONTEXT)
                {
                    objectsToDestroy.Add(item);
                }
            }
            foreach (var objectToDestroy in objectsToDestroy.IteratorSafe())
            {
                LunarCreature.Inventory.RemoveObject(objectToDestroy);
                objectToDestroy?.Obliterate();
            }

            foreach (var projectile in LunarCreature.GetInventory().IteratorSafe())
                if (projectile.GetBlueprint().InheritsFromSafe("Projectile"))
                    projectile.RemovePart<UD_Bones_FragileLunarObject>();

            foreach (var projectile in (LunarReliquary?.GetInventory()).IteratorSafe())
                if (projectile.GetBlueprint().InheritsFromSafe("Projectile"))
                    projectile.RemovePart<UD_Bones_FragileLunarObject>();

            //Utils.Log($"{nameof(MakeInventoryFragile)} Finished");
            return true;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(LoadLunarRegentEvent.ID, EventOrder.EXTREMELY_EARLY);
            base.Register(Object, Registrar);
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
                && !BakedShortDesc.IsNullOrEmpty()
                && ParentObject.TryGetPart(out Description description)
                //&& description._Short == ShortDesc
                )
            {
                DoneDescription = true;
                description._Short = BakedShortDesc.StartReplace().ToString();
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

        public virtual bool HandleEvent(EarlyBeforeLoadLunarRegentEvent E)
            => base.HandleEvent(E)
            ;

        public virtual bool HandleEvent(BeforeLoadLunarRegentEvent E)
            => base.HandleEvent(E)
            ;

        public virtual bool HandleEvent(LoadLunarRegentEvent E)
        {
            //Utils.Log($"{nameof(UD_Bones_LunarRegent)}: {nameof(HandleEvent)}({nameof(LoadLunarRegentEvent)})");
            if (ParentObject == E.LunarObject)
            {
                string catchFlag = $"Top";
                try
                {
                    LocationData = E.BonesInfo.FileLocationData;

                    foreach (var part in BonesManager.PartsLunarRegentsShouldNotHave.IteratorSafe())
                        E.LunarObject.RemovePart(part);

                    var playerParts = E.LunarObject.GetPartsDescendedFrom<IPlayerPart>();
                    foreach (var part in playerParts.IteratorSafe())
                        E.LunarObject.RemovePart(part);

                    E.LunarObject.SetStringProperty("OriginalPlayerBody", null, RemoveIfNull: true);

                    if (E.IsMad)
                        IsMad = true;

                    catchFlag = $"{nameof(GameObjectFactory.Factory.HasBlueprint)}";
                    if (!GameObjectFactory.Factory.HasBlueprint(E.LunarObject.Blueprint))
                    {
                        E.LunarObject.Blueprint = "Lunar Regent";
                        IsMad = true;
                    }

                    catchFlag = $"{nameof(IsMad)}?";
                    if (IsMad)
                    {
                        catchFlag = $"{nameof(IsMad)}: true";
                        E.LunarObject.Render.Tile = Const.MAD_LUNAR_REGENT_TILE;
                        IsMad = true;
                    }
                    else
                        catchFlag = $"{nameof(IsMad)}: false";

                    catchFlag = nameof(Description);
                    SetBakedShortDesc(IsYou: E.IsYou, WhoItWas: E.BonesInfo?.OsseousAshHandle ?? OsseousAsh.DefaultOsseousAshHandle);

                    if (E.CheckContext(PseudoZone.RECLAIM_CONTEXT)
                        && The.Player is GameObject player)
                    {
                        E.LunarObject.AddOpinion<OpinionMollify>(player);
                        player.AddOpinion<OpinionMollify>(E.LunarObject);

                        catchFlag = $"{nameof(BonesManager.BonesFileName)}AttitudeSetup";
                        var attitudeSetup = Event.New($"{nameof(BonesManager.BonesFileName)}AttitudeSetup")
                            .SetParameter(nameof(E.LunarObject), E.LunarObject)
                            .SetParameter(nameof(The.Player), player);

                        catchFlag = nameof(GameObject.FireEvent);
                        if (player.FireEvent(attitudeSetup))
                        {
                            var brain = E.LunarObject.Brain;
                            brain?.PushGoal(new Kill(player));
                            ApplyHostility(player, brain, 0);
                        }

                        MakeInventoryFragile(E.LunarObject, UD_Bones_LunarReliquary.Create(E.BonesInfo.ID), E.Context);
                    }

                    if (E.LunarObject.Render is Render render)
                        render.Visible = true;
                }
                catch (Exception x)
                {
                    Utils.Error($"{nameof(LoadLunarRegentEvent)}, ({catchFlag}): {E.LunarObject?.DebugName ?? "MISSING_OBJECT"}", x);
                }
            }

            return base.HandleEvent(E);
        }

        public virtual bool HandleEvent(AfterLoadedLunarRegentEvent E)
            => base.HandleEvent(E)
            ;

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
