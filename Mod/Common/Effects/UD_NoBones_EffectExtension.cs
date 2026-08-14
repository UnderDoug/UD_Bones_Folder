using System;
using System.Collections.Generic;
using System.Text;

using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Effects
{
    public abstract class UD_NoBones_EffectExtension<T> 
        : IEffectExtension<T>
        , IModEventHandler<BeforeCreateLunarRegentEvent>
        where T 
        : Effect
    {
        public virtual GameObject Other => null;
        public virtual string Reason => "Under an excluded effect";

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == BeforeCreateLunarRegentEvent.ID
            ;

        public virtual bool HandleEvent(BeforeCreateLunarRegentEvent E)
        {
            if (E.IsSource(Object)
                || E.IsSource(Other))
            {
                E.BlockCreation(Reason: Reason);
                return false;
            }
            return base.HandleEvent(E);
        }
    }
}
