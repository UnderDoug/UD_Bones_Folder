using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public class RouteParam
    {
        public string Key;
        public string Value;

        public RouteParam(string Key, object Value)
        {
            this.Key = Key;
            this.Value = Value?.ToString();
        }

        public void Deconstruct(out string Key, out string Value)
        {
            Key = this.Key;
            Value = this.Value;
        }
    }
}
