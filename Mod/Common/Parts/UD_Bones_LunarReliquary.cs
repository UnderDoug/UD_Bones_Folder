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
using UD_Bones_Folder.Mod.Parts;
using System.Linq;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarReliquary
        : UD_Bones_FragileLunarObject
        , IFragileObjectHolderPart
    {
        protected string TileColor;
        protected string DetailColor;

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

        public UD_Bones_LunarReliquary()
            : base()
        { }

        public static GameObject Create(string BonesID, bool IsDownloaded)
        {
            if (GameObject.CreateUnmodified(Const.LUNAR_RELIQUARY_BLUEPRINT) is not GameObject lunarReliquary)
                return null;

            if (!lunarReliquary.TryGetPart(out UD_Bones_LunarReliquary reliquaryPart))
            {
                Utils.Warn($"{nameof(UD_Bones_LunarReliquary)}.{nameof(Create)} resulted in {nameof(lunarReliquary)} with no {nameof(UD_Bones_LunarReliquary)} Part. Aborting.");
                lunarReliquary?.Obliterate();
                return null;
            }

            reliquaryPart.OverrideBonesID(BonesID ?? The.Game?.GameID);

            lunarReliquary.MakeReportable(BonesID);
            lunarReliquary.TryModerate(IsDownloaded);
            lunarReliquary.TryFeverWarp(BonesID);

            return lunarReliquary;
        }

        public static GameObject Create(SaveBonesInfo BonesInfo)
            => Create(BonesInfo?.ID, BonesInfo?.IsDownloaded is true)
            ;

        public static GameObject Create(bool IsDownloaded = false)
            => Create(null, IsDownloaded)
            ;

        public override void Attach()
        {
            base.Attach();
        }

        public override void Initialize()
        {
            base.Initialize();
            ParentObject.RequirePart<UD_Bones_LunarColors>().OverrideBonesID(BonesID);
        }

        public static bool IsFragileAndShouldBeDamaged(GameObject Object, GameObject TriggeringObject)
        {
            if (Object == TriggeringObject)
                return false;

            if (!Object.TryGetPart(out UD_Bones_FragileLunarObject fragilePart))
                return false;

            if (!fragilePart.WantsRemoveOnDamage)
                return !fragilePart.Triggered;

            return true;
        }


        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register("AfterContentsTaken");
            Registrar.Register(Const.LUNAR_RELIQUARY_TRIGGERED);
            Registrar.Register(GetDisplayNameEvent.ID, EventOrder.EXTREMELY_LATE);
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            // || ID == GetShortDescriptionEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == ZoneActivatedEvent.ID
            || ID == LunarObjectColorChangedEvent.ID
            || ID == AfterBonesZoneLoadedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            string baseReplacement = E.GetPrimaryBase();

            if (baseReplacement.Contains("@@LunarRegent@@"))
            {
                E.ReplacePrimaryBase(baseReplacement
                    .Replace("@@LunarRegent@@", Grammar.MakePossessive(BakedLunarRegentName ?? MissingLunarRegent))
                    .StartReplace()
                    .ToString());
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            // E.Postfix.AppendRules(GetDescription());
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(ZoneActivatedEvent E)
        {
            // _LunarRegent = null;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (ParentObject.Render is Render lunarRender
                && !lunarRender.Visible)
                lunarRender.Visible = true;

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(AfterBonesZoneLoadedEvent E)
        {
            if (E.LunarRegent == null
                || E.LunarRegent.HasPart<UD_Bones_FeverWarped>()
                && !ParentObject.HasPart<UD_Bones_FeverWarped>())
            {
                ParentObject.AddPart(UD_Bones_FeverWarped.NewCosmeticOnly());
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(LunarObjectColorChangedEvent E)
        {
            TileColor = E.TileColor;
            DetailColor = E.DetailColor;
            return base.HandleEvent(E);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "AfterContentsTaken")
                ParentObject.FireEvent(Event.New(Const.LUNAR_RELIQUARY_TRIGGERED, "Source", E.ID));
            else
            if (E.ID == Const.LUNAR_RELIQUARY_TRIGGERED)
            {
                string source = $"{nameof(UD_Bones_LunarReliquary)}.Triggered";
                string addtlSource = E.GetStringParameter("Source");
                if (!addtlSource.IsNullOrEmpty())
                    source += $" ({addtlSource})";

                if (ParentObject.Inventory is Inventory inventory)
                {
                    var triggeringObject = E.GetGameObjectParameter("TriggeringObject");
                    GameObject lastObject = null;
                    while (inventory.Objects.FirstOrDefault(go => IsFragileAndShouldBeDamaged(go, triggeringObject)) is GameObject containedObject)
                    {
                        if (containedObject == lastObject)
                        {
                            Utils.Log($"{nameof(Event)}.{E.ID} ({nameof(IsFragileAndShouldBeDamaged)}) - Repeat Object: {containedObject?.DebugName ?? "NO_OBJECT"}");
                            break;
                        }

                        if (containedObject.TryGetPart(out UD_Bones_FragileLunarObject fragileObject))
                            fragileObject.AttemptDamage(
                                Force: true,
                                Cascade: false,
                                Source: source);

                        lastObject = containedObject;
                    }
                }
                /*if (ParentObject.Inventory.GetObjectCount() > 0)
                {
                    using var containedObjects = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(ParentObject.Inventory.GetObjects());
                    foreach (var containedObject in containedObjects)
                    {
                        if (E.GetGameObjectParameter("TriggeringObject") == containedObject)
                            continue;

                        if (containedObject.TryGetPart(out UD_Bones_FragileLunarObject fragileObject))
                            fragileObject.AttemptDamage(
                                Force: true,
                                Remove: fragileObject.WantsRemoveOnDamage,
                                Cascade: false,
                                Source: source);
                    }
                }*/
                IsProtected = false;
                AttemptDamage(
                    Force: true,
                    Remove: WantsRemoveOnDamage,
                    Cascade: false,
                    Source: source);
            }
            return base.FireEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            return base.HandleEvent(E);
        }
    }
}
