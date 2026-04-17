using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Rules;
using XRL.World.Anatomy;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;

using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Parts
{
    [Serializable]
    public abstract class UD_Bones_BaseLunarSubject : UD_Bones_BaseLunarPart
    {
        public string BakedLunarRegentNameStripped;
        public string BakedLunarRegentName;

        protected GameObjectReference LunarRegentReference = new();

        public int LunarRegenBaseID => LunarRegent?.BaseID ?? -1;
        public GameObject LunarRegent
            => LunarRegentReference.TryEnsureObject(out GameObject lunarRegent)
            ? lunarRegent
            : null
            ;

        public UD_Bones_BaseLunarSubject()
            : base()
        { }

        public UD_Bones_BaseLunarSubject(string BonesID)
            : base(BonesID)
        { }

        public override void Initialize()
        {
            base.Initialize();

        }

        public override void FinalizeRead(SerializationReader Reader)
        {
            base.FinalizeRead(Reader);

        }

        public virtual void SetLunarRegentReference(GameObject LunarRegent)
        {
            LunarRegentReference ??= new();
            LunarRegentReference.Set(LunarRegent);
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == TidyLunarObjectsEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(LunarObjectColorChangedEvent E)
            => base.HandleEvent(E);

        public override bool HandleEvent(TidyLunarObjectsEvent E)
        {
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            if (!E.Entries.ContainsKey(nameof(UD_Bones_BaseLunarSubject)))
            {
                E.AddEntry(nameof(UD_Bones_BaseLunarSubject), nameof(LunarRegent), LunarRegent != null ? "not null" : "null");
                E.AddEntry(nameof(UD_Bones_BaseLunarSubject), nameof(LunarRegenBaseID), LunarRegenBaseID);
                E.AddEntry(nameof(UD_Bones_BaseLunarSubject), nameof(BakedLunarRegentNameStripped), BakedLunarRegentNameStripped);
                E.AddEntry(nameof(UD_Bones_BaseLunarSubject), nameof(BakedLunarRegentName), BakedLunarRegentName);
            }
            return base.HandleEvent(E);
        }

        public override bool FireEvent(Event E)
        {

            return base.FireEvent(E);
        }
    }
}
