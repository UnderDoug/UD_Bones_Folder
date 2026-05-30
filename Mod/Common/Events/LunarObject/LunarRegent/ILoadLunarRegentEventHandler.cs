using System;
using System.Collections.Generic;
using System.Text;

using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public interface ILoadLunarRegentEventHandler
        : IModEventHandler<EarlyBeforeLoadLunarRegentEvent>
        , IModEventHandler<BeforeLoadLunarRegentEvent>
        , IModEventHandler<LoadLunarRegentEvent>
        , IModEventHandler<AfterLoadedLunarRegentEvent>
    {
    }
}
