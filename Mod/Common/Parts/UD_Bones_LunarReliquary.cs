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

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarReliquary : UD_Bones_BaseLunarSubject
    {
        protected string TileColor;
        protected string DetailColor;

        public static string MissingLunarRegent => "some =LunarShader:Moon Sovran:*= lost to time";

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

        public Type AllyReasonType;

        private bool _DoneAllyship;
        public bool DoneAllyship
        {
            get => _DoneAllyship;
            protected set => _DoneAllyship = value;
        }

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
            ParentObject.SetStringProperty(Const.IS_MAD_PROP, $"{IsMad}");
            ParentObject.RequirePart<UD_Bones_LunarColors>();
        }

        public override IPart DeepCopy(GameObject Parent, Func<GameObject, GameObject> MapInv)
        {
            var part = base.DeepCopy(Parent, MapInv) as UD_Bones_LunarReliquary;
            part.BakedLunarRegentNameStripped = null;
            part.BakedLunarRegentName = null;
            part.LunarRegentReference = null;
            return part;
        }

        public bool PerformAllyship()
        {
            try
            {
                if (!DoneAllyship
                    && BonesID != The.Game.GameID
                    && ParentObject.Brain is Brain brain
                    && ParentObject.CurrentZone is Zone currentZone
                    && currentZone.TryFindLunarRegent(BonesID, out GameObject lunarRegent)
                    && Activator.CreateInstance(AllyReasonType) is IAllyReason allyReason)
                {
                    DoneAllyship = true;
                    brain.TakeAllegiance(lunarRegent, allyReason);
                    brain.SetPartyLeader(lunarRegent, Silent: true);
                    Utils.Log($"{ParentObject?.DebugName ?? "NO_COURTIER"} made follower of {LunarRegent?.DebugName ?? "NO_REGENT"} for reason {allyReason.GetType().Name}.");
                }
            }
            catch (Exception x)
            {
                Utils.Error($"Failed to create instance of {AllyReasonType?.Name ?? "NO_ALLY_TYPE"} for {ParentObject?.DebugName ?? "NO_COURTIER"}", x);
                DoneAllyship = true;
            }
            return DoneAllyship;
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
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (ParentObject.Render is Render render
                && render.DisplayName.Contains("@@")
                && !BakedLunarRegentNameStripped.IsNullOrEmpty()
                && LunarRegenBaseID >1)
            {
                BakedLunarRegentName = $"=LunarShader:{Grammar.MakePossessive(BakedLunarRegentNameStripped)}:{LunarRegenBaseID}=";
                render.DisplayName = render.DisplayName.Replace("@@LunarRegent@@", BakedLunarRegentName);
            }
            string baseReplacement;
            if (GameObject.Validate(LunarRegent))
            {
                baseReplacement = $"=subject.Refname's|LunarShader:{LunarRegenBaseID}= reliquary"
                    .StartReplace()
                    .AddObject(LunarRegent)
                    .ToString();
            }
            else
            {
                baseReplacement = E.GetPrimaryBase()
                    .StartReplace()
                    .ToString();
            }

            E.ReplacePrimaryBase(baseReplacement);

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

            if (LunarRegent != null)
                BakedLunarRegentNameStripped = LunarRegent.GetReferenceDisplayName(Stripped: true);

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
                foreach (var containedObject in ParentObject.Inventory.GetObjectsDirect())
                    if (containedObject.TryGetPart(out UD_Bones_FragileLunarObject fragileObject))
                        fragileObject.AttemptDamageAndRemove(Force: true);

            return base.FireEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(DoneAllyship), DoneAllyship);
            E.AddEntry(this, nameof(ParentObject.Brain.PartyLeader), ParentObject?.Brain?.PartyLeader?.DebugName ?? "NO_LEADER");
            return base.HandleEvent(E);
        }
    }
}
