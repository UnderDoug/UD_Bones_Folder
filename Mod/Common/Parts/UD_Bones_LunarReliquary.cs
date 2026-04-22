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
    public class UD_Bones_LunarReliquary : UD_Bones_FragileLunarObject
    {
        protected string TileColor;
        protected string DetailColor;

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

        public override void Attach()
        {
            base.Attach();
            ParentObject.RequirePart<UD_Bones_LunarColors>();
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register("AfterContentsTaken");
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
                    .Replace("@@LunarRegent@@", BakedLunarRegentName ?? MissingLunarRegent)
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
            {
                if (ParentObject.Inventory.GetObjectCount() > 0)
                {
                    using var containedObjects = ScopeDisposedList<GameObject>.GetFromPoolFilledWith(ParentObject.Inventory.GetObjects());
                    foreach (var containedObject in containedObjects)
                        if (containedObject.TryGetPart(out UD_Bones_FragileLunarObject fragileObject))
                            fragileObject.AttemptDamage(Force: true, Remove: fragileObject.WantsRemoveOnDamage);
                }
                IsProtected = false;
                AttemptDamage(Force: true, Remove: WantsRemoveOnDamage);
            }

            return base.FireEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            return base.HandleEvent(E);
        }
    }
}
