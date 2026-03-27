using System;
using System.Collections.Generic;
using System.Text;

namespace Bones.Mod
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
