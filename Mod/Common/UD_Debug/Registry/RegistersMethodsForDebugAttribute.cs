using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.Logging
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RegistersMethodsForDebugAttribute : Attribute
    {
    }
}
