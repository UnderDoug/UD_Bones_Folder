using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using XRL.Core;
using XRL.Collections;
using XRL.Language;
using XRL.Rules;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

using SerializeField = UnityEngine.SerializeField;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;
using UD_Bones_Folder.Mod.Moderation;
using XRL.Names;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_BlueprintSpec : IScribedPart
    {
        public BlueprintSpec BlueprintSpec;

        public UD_Bones_BlueprintSpec()
        { }

        public override void Attach()
        {
            base.Attach();
        }

        public override void Initialize()
        {
            base.Initialize();
            BlueprintSpec?.Dispose();
            BlueprintSpec = new BlueprintSpec(ParentObject);
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            // Registrar.Register(GetShortDescriptionEvent.ID, EventOrder.VERY_LATE, Serialize: true);
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            try
            {
                /*string blueprintSpecString = BlueprintSpec?.DebugString(ForInternals: true);
                if (blueprintSpecString.IsNullOrEmpty())
                    blueprintSpecString = "null";
                E.AddEntry(this, nameof(BlueprintSpec), blueprintSpecString);*/
                if (BlueprintSpec == null)
                    E.AddEntry(this, nameof(BlueprintSpec), "null");
                else
                {
                    foreach ((var key, var value) in BlueprintSpec.GetDebugPairs().IteratorSafe())
                    {
                        E.AddEntry(this, key, value);
                    }
                }
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(UD_Bones_BlueprintSpec)}.{nameof(GetDebugInternalsEvent)}", x);
                E.AddEntry(this, nameof(Exception), x.GetType().Name);
            }

            return base.HandleEvent(E);
        }
    }
}
