using System;
using System.Collections.Generic;
using System.Text;

using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Parts
{
    public abstract class UD_NoBones_PartExtension<T> 
        : IPartExtension<T>
        , IModEventHandler<BeforeCreateLunarRegentEvent>
        where T 
        : IPart
    {
        public virtual GameObject Other => null;
        public virtual string Reason => "Has an excluded Part";

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == BeforeCreateLunarRegentEvent.ID
            ;

        public virtual bool HandleEvent(BeforeCreateLunarRegentEvent E)
        {
            if (E.IsSource(ParentObject)
                || E.IsSource(Other))
            {
                E.BlockCreation(Reason: Reason);
                return false;
            }
            return base.HandleEvent(E);
        }
    }
}
