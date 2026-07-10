using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Rules;
using XRL.World.Anatomy;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;

using UD_Bones_Folder.Mod.Events;
using UD_Bones_Folder.Mod.Parts;

namespace XRL.World.Parts
{
    [Serializable]
    public abstract class UD_Bones_BaseLunarPart
        : IScribedPart
        , ILunarObjectPart
        , IPseudoZoneEventHandler
        , IModEventHandler<TidyLunarObjectsEvent>
        , IModEventHandler<LunarObjectColorChangedEvent>
    {
        [Serializable]
        public class NoInfluenceSet : IComposite
        {
            public string Name;
            public string DisplayName;
            public List<string> Exclusions;
            public Dictionary<string, string> Messages;

            public string GetFor(string Type)
                => Type != null
                    && !Messages.IsNullOrEmpty()
                    && Messages.TryGetValue(Type, out string message)
                ? message
                : null
                ;

            public bool IsExcluded(string Type)
                => Type != null
                && !Exclusions.IsNullOrEmpty()
                && Exclusions.Contains(Type)
                ;
        }

        public virtual NoInfluenceSet NoInfluence { get; } = null;

        protected string _BonesID;
        public string BonesID
        {
            get
            {
                if (ParentObject != null)
                    _BonesID = ParentObject.GetStringProperty(nameof(BonesID), _BonesID);
                return _BonesID;
            }
            protected set
            {
                _BonesID = value;
                ParentObject?.SetStringProperty(nameof(BonesID), _BonesID, true);
            }
        }

        protected string _LastBonesID;
        public string LastBonesID
        {
            get
            {
                if (ParentObject != null)
                    _LastBonesID = ParentObject.GetStringProperty(nameof(LastBonesID), _LastBonesID);
                return _LastBonesID;
            }
            protected set
            {
                _LastBonesID = value;
                ParentObject?.SetStringProperty(nameof(LastBonesID), _LastBonesID, true);
            }
        }

        public bool Persists;
        bool ILunarObjectPart.Persists {
            get => Persists;
            set => Persists = value;
        }

        public virtual bool CanBeFragile => true;

        public UD_Bones_BaseLunarPart()
            : base()
        {
        }

        public UD_Bones_BaseLunarPart(string BonesID)
            : this()
        {
            SetBonesIDInternal(BonesID, true);
        }

        public override void Write(GameObject Basis, SerializationWriter Writer)
        {
            base.Write(Basis, Writer);
            Writer.WriteOptimized(_BonesID);
            Writer.WriteOptimized(_LastBonesID);
        }

        public override void Read(GameObject Basis, SerializationReader Reader)
        {
            base.Read(Basis, Reader);
            _BonesID = Reader.ReadOptimizedString();
            _LastBonesID = Reader.ReadOptimizedString();
        }

        public override void FinalizeRead(SerializationReader Reader)
        {
            base.FinalizeRead(Reader);
            SetBonesIDInternal(BonesID);
        }

        public override void Initialize()
        {
            base.Initialize();
            if (The.Game?.GameID is string gameID)
                SetBonesIDInternal(gameID);

            // SetBonesIDInternal(The.Game?.GameID, Override: true);
        }

        protected virtual void SetBonesIDInternal(string BonesID, bool Override)
        {
            if (Override
                || this.BonesID == null)
                this.BonesID = BonesID;

            LastBonesID = BonesID;
        }

        protected virtual void SetBonesIDInternal(string BonesID)
            => SetBonesIDInternal(BonesID, false);

        public T SetBonesIDTyped<T>(string BonesID)
            where T : UD_Bones_BaseLunarPart
        {
            SetBonesIDInternal(BonesID, false);
            return (T)this;
        }

        public void SetBonesID(string BonesID)
            => SetBonesIDInternal(BonesID, false)
            ;

        public T OverrideBonesIDTyped<T>(string BonesID)
            where T : UD_Bones_BaseLunarPart
        {
            SetBonesIDInternal(BonesID, true);
            return (T)this;
        }

        public void OverrideBonesID(string BonesID)
            => SetBonesIDInternal(BonesID, true)
            ;

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register("CanBeInfluenced");
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == BeforeObjectCreatedEvent.ID
            || ID == TidyLunarObjectsEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(BeforeObjectCreatedEvent E)
        {
            if (E.Context == "Wish")
                Persists = true;

            SetBonesIDInternal(The.Game?.GameID, true);
            return base.HandleEvent(E);
        }

        public virtual bool HandleEvent(TidyLunarObjectsEvent E)
        {
            var parentObject = ParentObject;
            if (GameObject.Validate(ref parentObject))
            {
                //Utils.Log($"{0.Indent()}{nameof(UD_Bones_BaseLunarPart)} as {GetType().Name} - {nameof(TidyLunarObjectsEvent)}({nameof(GameObject)}: {parentObject?.DebugName ?? "NO_OBJECT"}, {nameof(E.Context)}: {E.Context})");
                bool bonesIDMatches = BonesID == E.BonesID
                    || E.BonesID == null;

                bool bonesMatchAndNotPersist = bonesIDMatches
                    && !Persists;

                if (bonesMatchAndNotPersist
                    || E.Force)
                {
                    // Utils.Log($"{1.Indent()}: {nameof(bonesMatchAndNotPersist)}: {bonesMatchAndNotPersist}, {nameof(E.Force)}: {E.Force}");
                    try
                    {
                        parentObject.RemovePart(this);
                        parentObject.Obliterate();
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"{GetType().Name}, during {nameof(TidyLunarObjectsEvent)}", x);
                    }
                    return true;
                }
            }
            return base.HandleEvent(E);
        }

        public virtual bool HandleEvent(BeforePseudoZoneLoadedEvent E)
            => base.HandleEvent(E);

        public virtual bool HandleEvent(AfterPseudoZoneLoadedEvent E)
            => base.HandleEvent(E);

        public virtual bool HandleEvent(AfterBonesZoneLoadedEvent E)
            => base.HandleEvent(E);

        public virtual bool HandleEvent(LunarObjectColorChangedEvent E)
            => base.HandleEvent(E);

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            if (!E.Entries.ContainsKey(nameof(UD_Bones_BaseLunarPart)))
            {
                E.AddEntry(nameof(UD_Bones_BaseLunarPart), nameof(BonesID), BonesID);
                E.AddEntry(nameof(UD_Bones_BaseLunarPart), nameof(LastBonesID), LastBonesID);
                E.AddEntry(nameof(UD_Bones_BaseLunarPart), nameof(Persists), Persists);
            }
            return base.HandleEvent(E);
        }

        public override bool FireEvent(Event E)
        {
            if (E.ID == "CanBeInfluenced"
                && NoInfluence != null)
            {
                string influenceType = E.GetStringParameter("Type", "default");
                if (!NoInfluence.IsExcluded(influenceType)
                    && NoInfluence.GetFor(influenceType) is string influenceMessage)
                {
                    E.SetParameter(
                        Name: "Message",
                        Value: influenceMessage
                            .StartReplace()
                            .AddObject(ParentObject)
                            .ToString()
                            .Replace("@@DisplayName@@", NoInfluence.DisplayName));
                    return false;
                }
            }
            return base.FireEvent(E);
        }
    }
}
