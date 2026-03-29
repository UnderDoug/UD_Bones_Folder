using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod
{
    public static class Extensions
    {
        public static string Color(this string String, string Color)
            => !Color.IsNullOrEmpty()
            ? "{{" + $"{Color}|{String}" + "}}"
            : String
            ;
    }
}
