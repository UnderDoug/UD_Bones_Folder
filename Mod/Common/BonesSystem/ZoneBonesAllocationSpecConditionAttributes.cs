using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.BonesSystem
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class HasZoneBonesAllocationSpecConditionAttribute : Attribute
    {
        public HasZoneBonesAllocationSpecConditionAttribute()
        { }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
    public class ZoneBonesAllocationSpecConditionAttribute : Attribute
    {
        public ZoneBonesAllocation.AllocationTypes WhenTrue;

        public ZoneBonesAllocationSpecConditionAttribute()
        { }

        public ZoneBonesAllocationSpecConditionAttribute(ZoneBonesAllocation.AllocationTypes WhenTrue)
            : this()
        {
            this.WhenTrue = WhenTrue;
        }
    }
}
