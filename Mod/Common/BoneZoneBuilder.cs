using System;
using System.Collections.Generic;
using System.Text;

using Bones.Mod;

namespace XRL.World.ZoneBuilders
{
    public class BoneZoneBuilder
    {
        public BonesData BonesData;

        public BoneZoneBuilder()
        { }

        public bool BuildZone(Zone Z)
        {
            if (BonesData?.Apply(Z) is true)
            {
                BonesData.Cremate();
                return true;
            }
            return false;
        }
    }
}
