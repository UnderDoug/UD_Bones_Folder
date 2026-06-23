using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod
{
    public static class FlagsHelper
    {
        public static bool IsSet<T>(T Flags, T Flag)
            where T : struct
            => ((int)(object)Flags & (int)(object)Flag) != 0
            ;

        public static void Set<T>(ref T Flags, T Flag)
            where T : struct
            => Flags = (T)(object)((int)(object)Flags | (int)(object)Flag)
            ;

        public static void Unset<T>(ref T Flags, T Flag)
            where T : struct
            => Flags = (T)(object)((int)(object)Flags & (~(int)(object)Flag))
            ;
    }
}
