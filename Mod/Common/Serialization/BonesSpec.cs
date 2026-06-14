using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Platform.IO;

using UD_Bones_Folder.Mod.Serialization.PseudoTypes;

using UnityEngine;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.Const;

using GameObject = XRL.World.GameObject;

namespace UD_Bones_Folder.Mod
{
    [JsonObject(MemberSerialization.OptOut)]
    [Serializable]
    public class BonesSpec : IComposite
    {
        [Serializable]
        public enum ApproxDepth
        {
            /// <summary>Strata 0-9</summary>
            Sky,
            /// <summary>Stratum 10</summary>
            Surface,
            /// <summary>Strata 11-20</summary>
            Shallow,
            /// <summary>Strata 21-40</summary>
            Deep,
            /// <summary>Strata 41-997</summary>
            Abyssal,
            /// <summary>Strata 998+</summary>
            CryoClone,
        }

        public static string MissingTerrainType => "Mystery";

        public string BonesID;

        public int Level;

        public string ZoneID;
        public int ZoneZ;
        public int ZoneTier;

        public string ZoneTerrainType;
        public int RegionTier;
        public string TerrainTravelClass;

        public BonesSpec()
        {
        }

        public BonesSpec(
            GameObject LunarRegent,
            Zone Zone
            )
            : this()
        {
            BonesID = The.Game?.GameID;

            if (LunarRegent != null)
                Level = LunarRegent.Level;

            if (Zone != null)
            {
                ZoneID = Zone.ZoneID;
                ZoneZ = Zone.GetZoneZ();
                ZoneTier = Zone.NewTier;

                if (Zone.GetTerrainObject() is GameObject zoneTerrain)
                {
                    ZoneTerrainType = zoneTerrain.GetTagOrStringProperty("Terrain", MissingTerrainType);

                    int.TryParse(zoneTerrain.GetTag("RegionTier", "1"), out RegionTier);

                    TerrainTravelClass = zoneTerrain.GetPart<TerrainTravel>()?.TravelClass ?? "none";
                }

                if (GetApproxDepth(ZoneZ) >= ApproxDepth.Abyssal)
                {
                    RegionTier = Tier.Constrain(RegionTier + 5);
                    ZoneTerrainType = "Underground";
                    TerrainTravelClass = "Underground";
                }
            }
        }

        public BonesSpec(
            GameObject LunarRegent,
            PseudoZone PseudoZone
            )
            : this()
        {
            BonesID = The.Game?.GameID;

            if (LunarRegent != null)
                Level = LunarRegent.Level;

            if (PseudoZone != null)
            {
                ZoneID = PseudoZone.ZoneID;
                ZoneZ = PseudoZone.ZoneRequest.Z;
                ZoneTier = PseudoZone.NewTier;

                ZoneTerrainType = PseudoZone.ZoneTerrainType;
                RegionTier = PseudoZone.RegionTier;
                TerrainTravelClass = PseudoZone.TerrainTravelClass;

                if (GetApproxDepth(ZoneZ) >= ApproxDepth.Abyssal)
                {
                    RegionTier = Tier.Constrain(RegionTier + 5);
                    ZoneTerrainType = "Underground";
                    TerrainTravelClass = "Underground";
                }
            }
        }

        public bool SameAs(BonesSpec Other)
            => Other != null
            && BonesID == Other.BonesID
            && Level != 0
            && Level == Other.Level
            && ZoneID == Other.ZoneID
            && ZoneZ == Other.ZoneZ
            && ZoneTier == Other.ZoneTier
            && ZoneTerrainType == Other.ZoneTerrainType
            && RegionTier == Other.RegionTier
            && TerrainTravelClass == Other.TerrainTravelClass
            ;

        public static BonesSpec GetPlayerSpec(Zone Zone)
            => new(The.Player, Zone)
            ;

        public static ApproxDepth GetApproxDepth(int ZoneZ)
            => ZoneZ switch
            {
                >= 998 => ApproxDepth.CryoClone,
                >= 41 => ApproxDepth.Abyssal,
                >= 21 => ApproxDepth.Deep,
                >= 11 => ApproxDepth.Shallow,
                10 => ApproxDepth.Surface,
                _ => ApproxDepth.Sky,
            };

        public ApproxDepth GetApproxDepth()
            => GetApproxDepth(ZoneZ);

        public static bool ZoneStrataWithinThreshold(int ZoneZ, int SpecZ)
            => GetApproxDepth(ZoneZ) == GetApproxDepth(SpecZ)
            ;

        public static bool IsWithinLevel(int Level, int SpecLevel)
        {
            if ((Level / (double)Math.Max(1.0, SpecLevel)) < 0.85)
                return false;

            if ((SpecLevel / (double)Math.Max(1.0, Level)) < 0.85)
                return false;

            return true;
        }

        public bool IsWithinSpec(BonesSpec PlayerSpec)
        {
            if (SameAs(PlayerSpec))
                return true;

            if (!IsWithinLevel(Level, PlayerSpec.Level))
                return false;

            if (!ZoneStrataWithinThreshold(ZoneZ, PlayerSpec.ZoneZ))
                return false;

            if (Math.Abs(ZoneTier - PlayerSpec.ZoneTier) > 5)
                return false;

            if (ZoneTerrainType != PlayerSpec.ZoneTerrainType)
                return false;

            if (RegionTier != PlayerSpec.RegionTier)
                return false;

            if (!TerrainTravelClass.IsNullOrEmpty()
                && !PlayerSpec.TerrainTravelClass.IsNullOrEmpty()
                && TerrainTravelClass != PlayerSpec.TerrainTravelClass)
                return false;

            return true;
        }

        public bool IsWithinSpec(Zone Zone)
            => IsWithinSpec(new BonesSpec(The.Player, Zone))
            ;
    }
}
