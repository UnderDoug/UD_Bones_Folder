using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.Coalescence
{
    [Serializable]
    public enum CoalesceMethod : int
    {
        First,
        Second,
        Greater,
        Lesser,
        Combine,
        Difference,
        TypeDefined,
    }
}
