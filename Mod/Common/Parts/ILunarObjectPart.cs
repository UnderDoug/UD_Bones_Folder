using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.Parts
{
    public interface ILunarObjectPart
    {
        public string BonesID { get; }

        public bool Persists { get; set; }

        public bool CanBeFragile { get; }
    }
}
