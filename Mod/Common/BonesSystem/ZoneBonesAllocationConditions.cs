using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.World;
using XRL.World.WorldBuilders;

namespace UD_Bones_Folder.Mod.BonesSystem
{

    [HasZoneBonesAllocationSpecCondition]
    public static class ZoneBonesAllocationConditions
    {
        public static BonesManager BonesManager => BonesManager.System;

        public static ITombAnchorSystem TombAnchorSystem = new CatacombsAnchorSystem();

        [ZoneBonesAllocationSpecCondition(WhenTrue = ZoneBonesAllocation.AllocationTypes.None)]
        public static bool IsStartingZone(Zone Z)
        {
            if (Z == null)
                return false;

            if (!The.Game.GetStringGameState("RuinedJoppa").EqualsNoCase("Yes")
                && Z.ZoneID == JoppaWorldBuilder.ID_JOPPA)
                return true;

            if (BonesManager.StartingLocation?.ZoneID == Z.ZoneID)
                return true;

            return false;
        }

        [ZoneBonesAllocationSpecCondition(WhenTrue = ZoneBonesAllocation.AllocationTypes.Party)]
        public static bool IsHistoricSiteSurfaceZone(Zone Z)
        {
            if (Z == null)
                return false;

            if (Z.GetTerrainObject().Blueprint.EqualsNoCase("SultanRegionSurface")
                && Z.HasZoneProperty("HistoricSite"))
                return true;

            return false;
        }

        [ZoneBonesAllocationSpecCondition(WhenTrue = ZoneBonesAllocation.AllocationTypes.Party)]
        public static bool IsHollowTreeZone(Zone Z)
        {
            if (Z == null
                || Z.ZoneID == null)
                return false;

            if (Z.ZoneID == The.Game?.GetStringGameState("HollowTreeZoneId"))
                return true;

            return false;
        }

        [ZoneBonesAllocationSpecCondition(WhenTrue = ZoneBonesAllocation.AllocationTypes.Party)]
        public static bool IsStiltGroundsZone(Zone Z)
        {
            if (Z == null
                || Z.ZoneID == null)
                return false;

            if (Z.GetTerrainObject()?.Blueprint == "TerrainSixDayStilt"
                && Z.Z == 10)
                return true;

            return false;
        }

        [ZoneBonesAllocationSpecCondition(WhenTrue = ZoneBonesAllocation.AllocationTypes.Bubble)]
        public static bool IsNotMutableZone(Zone Z)
        {
            if (Z == null)
                return false;

            if (!BonesManager.MutableLocations.IsNullOrEmpty()
                && !BonesManager.MutableLocations.Contains(Z.ResolvedLocation))
                return true;

            return false;
        }
    }
}
