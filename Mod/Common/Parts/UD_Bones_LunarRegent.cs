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
using XRL.Collections;
using XRL.Rules;

using UD_Bones_Folder.Mod.UI;
using UD_Bones_Folder.Mod.Serialization.PseudoTypes;
using UD_Bones_Folder.Mod.Parts;

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

        private Version? MigrateFrom = null;

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
                        BakedBrokenTitle = $"=LunarShader:{RegalTitle}:{ParentObject.BaseID}="
                            .StartReplace()
                            .ToString();

                        BakedBrokenTitle = BakedBrokenTitle.FeverWarped();

                        if (IsMad)
                            BakedBrokenTitle = $"Mad {BakedBrokenTitle}";

                        BakedBrokenTitle = $"{nameof(Broken).ToLower()} {BakedBrokenTitle}";

                        BakedBrokenTitle = BakedBrokenTitle
                            .StartReplace()
                            .ToString();
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

        private bool WantsThreePointLanding;

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
                return BonesManager.GetSavedBonesByIDDirect(BonesID);

            if (LocationData is not null)
            {
                if (LocationData.Host is OsseousAsh.Host storedHost)
                {
                    if (OsseousAsh.Hosts?.FirstOrDefault(hc => hc.Any(h => h.SameAs(storedHost, true))) is OsseousAsh.HostSet hostCollection
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

                if (BonesManager.TryGetSaveBonesByID(BonesID, out var saveBonesInfo, bonesInfo => LocationData.SameAs(bonesInfo.FileLocationData)))
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

                UD_Bones_FragileLunarObject fragilePart = null;
                item.PerformActionRecursively(delegate (GameObject go)
                {
                    var recursiveFragilePart = item.RequirePart<UD_Bones_FragileLunarObject>()
                        .OverrideBonesIDTyped<UD_Bones_FragileLunarObject>(bonesID);

                    if (item == go)
                        fragilePart = recursiveFragilePart;

                    recursiveFragilePart.WantsRemoveOnDamage = true;
                    if (!LunarCreature.IsLunarRegent())
                        recursiveFragilePart.Persists = true;
                });

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
                    && LoadLunarRegentEvent.CheckContext(Context, PseudoZone.RECLAIM_CONTEXT))
                {
                    if (fragilePart != null)
                        fragilePart.WantsToBeDropped = true;
                }
                else
                if (LunarCreature.IsLunarCourtier()
                    && LoadLunarCourtierEvent.CheckContext(Context, PseudoZone.RECLAIM_CONTEXT))
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

        public bool TryPerformThreePointLanding()
        {
            if (!WantsThreePointLanding)
                return false;

            Event.PinCurrentPool();
            try
            {
                Event.ResetPool();
                WantsThreePointLanding = false;
                int forceLevel = 4;
                if (currentCell.GetLocalAdjacentCells() is List<Cell> adjacentCells)
                {
                    if (adjacentCells.All(c => c.IsSolidFor(ParentObject)))
                    {
                        currentCell.Clear(Combat: true, alsoExclude: go => go == ParentObject || go.ConsiderSolidFor(ParentObject));
                        Physics.ApplyExplosion(currentCell, 15000, Local: true, Show: true, Owner: ParentObject, Neutron: true, DamageModifier: 0f, WhatExploded: ParentObject);
                        forceLevel = 25;
                    }
                }
                StunningForce.Concussion(StartCell: currentCell, ParentObject: ParentObject, Level: forceLevel, Distance: 1, Stun: false, Damage: false);
            }
            finally
            {
                Event.ResetToPin();
            }
            return true;
        }

        public static bool IsNonGeneratedFaction(Faction faction)
        {
            if (faction.Name.StartsWith("SultanCult"))
                return false;

            if (faction.Name.StartsWith("villagers of "))
                return faction.Name.EndsWith("Ezra")
                    || faction.Name.EndsWith("Joppa");

            return true;
        }

        public static IEnumerable<Faction> GetSuperlativeOrderedFactions(
            GameObject LunarRegent,
            bool Positive,
            Predicate<Faction> Where = null
            )
        {
            Dictionary<string, float> playerRep = null;
            if (!LunarRegent.TryGetStringProperty($"{Const.MOD_PREFIX}{nameof(The.Game.PlayerReputation.ReputationValues)}", out string storedPlayerRepString))
                playerRep = The.Game?.PlayerReputation?.ReputationValues;
            else
            {
                foreach (var pairString in storedPlayerRepString.Split(";;"))
                {
                    if (pairString.Split("::") is not string[] splitPair
                        || splitPair.Length < 2)
                    {
                        Utils.Warn($"Bad dictionary expansion part '{pairString}'");
                        continue;
                    }

                    if (!float.TryParse(splitPair[1], out float value))
                    {
                        Utils.Warn($"Invalid dictionary expansion value for key '{splitPair[0]}': {splitPair[1]} is not {typeof(float).Name}");
                        continue;
                    }

                    playerRep ??= new();
                    if (playerRep.ContainsKey(splitPair[0]))
                    {
                        Utils.Warn($"Duplicate dictionary expansion entry '{splitPair[0]}'");
                        continue;
                    }

                    playerRep[splitPair[0]] = value;
                }
            }

            if (playerRep.IsNullOrEmpty())
                yield break;

            var orderedFactions = Positive
                ? playerRep.OrderByDescending(rep => rep.Value)
                : playerRep.OrderBy(rep => rep.Value)
                ;

            foreach ((var factionName, var _) in orderedFactions)
            {
                if (Factions.GetIfExists(factionName) is Faction faction
                    && Where?.Invoke(faction) is not false
                    && faction.Visible)
                {
                    yield return faction;
                }
            }
        }

        public static ScopeDisposedList<Faction> RentMostLovedFactions(GameObject LunarRegent)
            => ScopeDisposedList<Faction>.GetFromPoolFilledWith(GetSuperlativeOrderedFactions(LunarRegent, Positive: true, Where: IsNonGeneratedFaction))
            ;

        public static ScopeDisposedList<Faction> RentMostHatedFactions(GameObject LunarRegent)
            => ScopeDisposedList<Faction>.GetFromPoolFilledWith(GetSuperlativeOrderedFactions(LunarRegent, Positive: false, Where: IsNonGeneratedFaction))
            ;

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(LoadLunarRegentEvent.ID, EventOrder.EXTREMELY_EARLY);
            Registrar.Register($"Apply{nameof(UD_Bones_MoonKingFever)}");
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
            || ID == AfterPseudoZoneLoadedEvent.ID
            || ID == ZoneActivatedEvent.ID
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

                bool forSave = E.Context == nameof(SaveBonesJSON);
                string shaderParam = !forSave
                    ? ParentObject.BaseID.ToString()
                    : "*"
                    ;

                string title = !isBroken
                    ? $"=LunarShader:{RegalTitle}:{shaderParam}="
                    : BakedBrokenTitle
                    ;

                if (!isBroken
                    && !forSave)
                    title = title.StartReplace().ToString();

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
                && ParentObject.TryGetPart(out Description description))
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
            if (ParentObject == E.LunarObject
                && E.LunarObject is GameObject lunarRegent)
            {
                string catchFlag = $"Top";
                try
                {
                    LocationData = E.BonesInfo.FileLocationData;

                    foreach (var part in BonesManager.PartsLunarRegentsShouldNotHave.IteratorSafe())
                        lunarRegent.RemovePart(part);

                    var playerParts = lunarRegent.GetPartsDescendedFrom<IPlayerPart>();
                    foreach (var part in playerParts.IteratorSafe())
                        lunarRegent.RemovePart(part);

                    lunarRegent.SetStringProperty("OriginalPlayerBody", null, RemoveIfNull: true);

                    if (E.IsMad)
                        IsMad = true;

                    catchFlag = $"{nameof(GameObjectFactory.Factory.HasBlueprint)}";
                    if (!GameObjectFactory.Factory.HasBlueprint(lunarRegent.Blueprint))
                    {
                        lunarRegent.Blueprint = "Lunar Regent";
                        IsMad = true;
                    }

                    catchFlag = $"{nameof(IsMad)}?";
                    if (IsMad)
                    {
                        catchFlag = $"{nameof(IsMad)}: true";
                        lunarRegent.Render.Tile = Const.MAD_LUNAR_REGENT_TILE;
                        IsMad = true;
                    }
                    else
                        catchFlag = $"{nameof(IsMad)}: false";

                    catchFlag = nameof(Description);
                    SetBakedShortDesc(IsYou: E.IsYou, WhoItWas: E.BonesInfo?.OsseousAshHandle ?? OsseousAsh.DefaultOsseousAshHandle);

                    if (lunarRegent.AddPart<GivesRep>() is GivesRep givesRep)
                    {
                        using var mostLovedFactions = RentMostLovedFactions(lunarRegent);
                        using var mostHatedFactions = RentMostHatedFactions(lunarRegent);

                        int reasonCount = 3;

                        if (E.CheckContext(PseudoZone.RECLAIM_CONTEXT)
                            || IsMad)
                        {
                            if (Math.Abs(lunarRegent.GetSeededRandom(nameof(GivesRep)).Next()) % 2 == 0)
                            {
                                givesRep.relatedFactions.Add(
                                    item: new FriendorFoe
                                    {
                                        faction = IsMad ? "Entropic" : "Strangers",
                                        status = "love",
                                        reason = IsMad ? "reasons that should be somewhat obvious" : "being strange and mysterious"
                                    });
                                reasonCount--;
                            }
                        }

                        while (reasonCount > 0)
                        {
                            bool positive = Math.Abs(lunarRegent.GetSeededRandom(nameof(GivesRep)).Next()) % 5 < 2;
                            Faction faction = null;

                            bool noLoved = mostLovedFactions.IsNullOrEmpty();
                            bool noHated = mostHatedFactions.IsNullOrEmpty();

                            if (positive
                                && faction == null)
                            {
                                if (!noLoved)
                                    faction = mostLovedFactions.TakeAt(0);
                                else
                                    positive = false;
                            }

                            if (!positive
                                || faction == null)
                            {
                                if (!noHated)
                                    faction = mostHatedFactions.TakeAt(0);
                                else
                                if (!noLoved)
                                {
                                    faction = mostLovedFactions.TakeAt(0);
                                    positive = true;
                                }
                            }

                            faction ??= Factions.GetRandomFaction(givesRep.relatedFactions.Select(f => f.faction).ToArray());

                            if (faction == null)
                                break;

                            string status = positive
                                ? "love"
                                : "hate"
                                ;

                            if (givesRep.relatedFactions.Count > 0)
                                status = positive
                                    ? "friend"
                                    : "dislike"
                                    ;

                            string reason = positive ? GenerateFriendOrFoe.getLikeReason() : GenerateFriendOrFoe.getHateReason();
                            if (Math.Abs(lunarRegent.GetSeededRandom(nameof(GivesRep)).Next()) % 2 == 0)
                                reason = $"allegedly {reason}";
                            else
                                reason = $"{reason}; allegedly";

                            givesRep.relatedFactions.Add(
                                item: new FriendorFoe
                                {
                                    faction = faction.Name,
                                    status = status,
                                    reason = reason,
                                });

                            reasonCount--;
                        }
                    }

                    if (E.CheckContext(PseudoZone.RECLAIM_CONTEXT)
                        && The.Player is GameObject player)
                    {
                        lunarRegent.AddOpinion<OpinionMollify>(player);
                        player.AddOpinion<OpinionMollify>(lunarRegent);

                        catchFlag = $"{nameof(BonesManager.BonesFileName)}AttitudeSetup";
                        var attitudeSetup = Event.New($"{nameof(BonesManager.BonesFileName)}AttitudeSetup")
                            .SetParameter(nameof(E.LunarObject), lunarRegent)
                            .SetParameter(nameof(The.Player), player);

                        catchFlag = nameof(GameObject.FireEvent);
                        if (player.FireEvent(attitudeSetup))
                        {
                            var brain = lunarRegent.Brain;
                            brain?.PushGoal(new Kill(player));
                            ApplyHostility(player, brain, 0);
                        }

                        MakeInventoryFragile(lunarRegent, UD_Bones_LunarReliquary.Create(E.BonesInfo, lunarRegent), E.Context);

                        Onset();
                    }

                    if (lunarRegent.Render is Render render)
                        render.Visible = true;
                }
                catch (Exception x)
                {
                    Utils.Error($"{nameof(LoadLunarRegentEvent)}, ({catchFlag}): {lunarRegent?.DebugName ?? "MISSING_OBJECT"}", x);
                }
            }

            return base.HandleEvent(E);
        }

        public virtual bool HandleEvent(AfterLoadedLunarRegentEvent E)
            => base.HandleEvent(E)
            ;

        public override bool HandleEvent(AfterPseudoZoneLoadedEvent E)
        {
            if (E.LunarRegent == ParentObject
                && ParentObject.CurrentCell is not null)
            {
                if (E.CheckContext(PseudoZone.RECLAIM_CONTEXT))
                {
                    WantsThreePointLanding = true;
                }
            }

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(ZoneActivatedEvent E)
        {
            if (WantsThreePointLanding
                && !TryPerformThreePointLanding())
                WantsThreePointLanding = false;

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

        public override bool FireEvent(Event E)
        {
            if (E.ID == $"Apply{nameof(UD_Bones_MoonKingFever)}"
                && Broken)
                return false;

            return base.FireEvent(E);
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
