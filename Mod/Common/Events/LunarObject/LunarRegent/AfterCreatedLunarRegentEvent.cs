using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod.Events
{
    public class AfterCreatedLunarRegentEvent : ILunarObjectEvent<AfterCreatedLunarRegentEvent>
    {
        public AfterCreatedLunarRegentEvent()
        { }


        public static void Send(
            GameObject Player,
            GameObject LunarRegent,
            string Context = null
            )
            => Check(
                Player: Player,
                BonesInfo: null,
                LunarObject: LunarRegent,
                Context: Context)
            ;
    }
}
