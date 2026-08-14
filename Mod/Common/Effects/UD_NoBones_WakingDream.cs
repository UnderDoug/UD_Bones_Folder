using System;
using System.Collections.Generic;
using System.Text;

using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Effects
{
    public class UD_NoBones_WakingDream
        : UD_NoBones_EffectExtension<WakingDream>
        , IModEventHandler<BeforeCreateLunarRegentEvent>
    {
        public override GameObject Other => ParentEffect?.Dreamer;
        public override string Reason => "This end simply signifies a new beginning... or, perhaps, an old beginning, started anew...";
    }
}
