using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [AttributeUsage(validOn: AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HasRegistersRoutesAttribute : Attribute
    {
    }

    [AttributeUsage(validOn: AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RegistersRoutesAttribute : Attribute
    {
        public int? Version = null;
    }
}
