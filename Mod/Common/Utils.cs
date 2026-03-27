using System;
using System.Collections.Generic;
using System.Text;

using XRL;

namespace Bones.Mod
{
    public static class Utils
    {
        public const string MOD_ID = "UD_Bones_Folder";

        public static ModInfo ThisMod => ModManager.GetMod(MOD_ID);

        public static void Error(object Message)
            => ThisMod.Error(Message)
            ;

        public static void Warn(object Message)
            => ThisMod.Warn(Message)
            ;
    }
}
