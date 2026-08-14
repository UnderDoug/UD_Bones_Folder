using System;
using System.Collections.Generic;
using System.Text;

using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Effects
{
    public class UD_NoBones_Dominated
        : UD_NoBones_EffectExtension<Dominated>
        , IModEventHandler<BeforeCreateLunarRegentEvent>
    {
        public override GameObject Other => ParentEffect?.Dominator;
        public override string Reason => "This end is that of someone else... linger not lest you're convinced it's yours...";
    }
}
