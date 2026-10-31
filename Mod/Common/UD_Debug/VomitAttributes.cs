using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.Logging
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class NoVomitAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public class DoVomitAttribute : Attribute
    {
        public string Label { get; set; }

        public bool ValueOnly { get; set; }

        public DoVomitAttribute()
            : base()
        { }

        public DoVomitAttribute(string Label)
            : this()
        {
            this.Label = Label;
        }
    }
}
