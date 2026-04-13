using System;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;

using Platform.IO;
using UnityEngine;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.Const;

using GameObject = XRL.World.GameObject;

namespace UD_Bones_Folder.Mod
{
    [Serializable]
    public class BonesSpec : IComposite
    {
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

            Level = LunarRegent.Level;

            ZoneID = Zone.ZoneID;
            ZoneZ = Zone.GetZoneZ();
            ZoneTier = Zone.NewTier;

            if (Zone.GetTerrainObject() is GameObject zoneTerrain)
            {
                ZoneTerrainType = zoneTerrain.GetTagOrStringProperty("Terrain", MissingTerrainType);

                int.TryParse(zoneTerrain.GetTag("RegionTier", "1"), out RegionTier);

                TerrainTravelClass = zoneTerrain.GetPart<TerrainTravel>()?.TravelClass ?? "none";
            }
        }

        public bool SameAs(BonesSpec Other)
            => Other != null
            && BonesID == Other.BonesID
            && Level == Other.Level
            && ZoneID == Other.ZoneID
            && ZoneZ == Other.ZoneZ
            && ZoneTier == Other.ZoneTier
            && ZoneTerrainType == Other.ZoneTerrainType
            && RegionTier == Other.RegionTier
            && TerrainTravelClass == Other.TerrainTravelClass
            ;

        public static async Task<BonesSpec> ReadBonesSpecAsync(SaveBonesInfo SaveBonesInfo)
        {
            string bonesPath = SaveBonesInfo.FullBonesPathSavGz;

            int versionNumber = -1;
            string versionString = "{unknown}";

            BonesSpec bonesSpec = null;
            try
            {
                if (!await File.ExistsAsync(bonesPath))
                {
                    bonesPath =SaveBonesInfo.FullBonesPathSav;
                    if (!await File.ExistsAsync(bonesPath))
                    {
                        Utils.Error($"No saved bones exist. ({SaveBonesInfo.DisplayDirectory})");
                        return bonesSpec;
                    }
                }

                SerializationReader reader = null;
                try
                {
                    reader = SerializationReader.Get();
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed to get reader", x);
                    return null;
                }

                var status = Loading.StartTask("Checking Bones Specifications");

                try
                {
                    using var stream = File.OpenRead(bonesPath);
                    var memory = reader.Stream;

                    if (stream.Length >= 2
                        && stream.ReadByte() == 31)
                        stream.ReadByte();

                    stream.Position = 0L;

                    using var gZipStream = new GZipStream(stream, CompressionMode.Decompress);
                    await gZipStream.CopyToAsync(memory);

                    memory.Position = 0L;

                    reader.Start();
                    if (reader.ReadInt32() != SERIALIZATION_CHECK)
                    {
                        versionString = "2.0.167.0 or prior";
                        throw new FatalDeserializationVersionException($"Bones file ({SaveBonesInfo.ID}) is the incorrect version.");
                    }

                    versionNumber = reader.FileVersion;
                    versionString = reader.ReadString();
                    try
                    {
                        if (versionNumber != XRLGame.SaveVersion)
                        {
                            string backupPath = bonesPath + $"_upgradebackup_{versionNumber}.gz";
                            if (!File.Exists(backupPath))
                            {
                                File.Copy(bonesPath, backupPath);
                                string cacheDBPath = Path.Combine(SaveBonesInfo.Directory, "Cache.db");
                                string cacheDBBackupPath = cacheDBPath + $"_upgradebackup_{versionNumber}.gz";
                                if (File.Exists(cacheDBPath)
                                    && !File.Exists(cacheDBBackupPath))
                                    File.Copy(cacheDBPath, cacheDBBackupPath);
                            }
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"bones upgrade backup: {x}");
                    }

                    if (reader.FileVersion < MIN_SAVE_VERSION)
                        throw new FatalDeserializationVersionException($"Bones file ({SaveBonesInfo.ID}) is the incorrect version.");

                    if (reader.FileVersion > XRLGame.SaveVersion)
                        throw new DeserializationVersionException($"Bones file ({SaveBonesInfo.ID}) is the incorrect version ({versionString}).");

                    try
                    {
                        if (reader.ReadInt32() != BONES_SPEC_POS)
                            throw new DeserializationException($"Bones file ({SaveBonesInfo.ID}) missing {nameof(BonesSpec)} val-check.");

                        bonesSpec = reader.ReadComposite<BonesSpec>();
                    }
                    catch (Exception x)
                    {
                        bonesSpec = null;
                        reader.Errors++;
                        reader.UnspoolTo(BONES_ZONE_POS, Prior: true);
                        Utils.Error($"Failed to read {nameof(BonesSpec)}, recovery will be attempted", x);
                    }

                    if (reader.Errors > 0)
                        BonesManager.DisplayLoadError(reader, "bones file", reader.Errors);
                }
                catch (Exception x)
                {
                    Utils.Error(x);
                    bonesSpec = null;
                }
                finally
                {
                    SerializationReader.Release(reader);
                    status.Dispose();
                }

            }
            catch (Exception x)
            {
                string message = $"That bones file appears to be corrupt, " +
                    $"you can try to restore the backup in your bones folder ({SaveBonesInfo.BonesBakDisplay}) " +
                    $"by removing the 'bak' file extension.";

                if (ModManager.TryGetStackMod(x, out var Mod, out var Frame))
                {
                    if (Frame.GetMethod() is MethodBase method)
                    {
                        string culpritMethod = method.DeclaringType?.FullName + "." + method.Name;
                        Mod.Error(culpritMethod + "::" + x);
                        message = $"That bones file is likely not loading because of a mod error from {Mod.DisplayTitleStripped} ({culpritMethod}), " +
                            $"make sure the correct mods are enabled or contact the mod author.";
                    }
                }
                else
                {
                    if (versionNumber < XRLGame.SaveVersion)
                        message = $"That bones file looks like it's from an older save format revision ({versionString}). Sorry!\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";
                    else
                    if (versionNumber > XRLGame.SaveVersion)
                        message = $"That bones file looks like it's from a newer save format revision ({versionString}).\n\n" +
                            $"In the future this process will more intelligently exclude mismatched bones.";

                    MetricsManager.LogException($"{nameof(BonesManager)}.{nameof(ReadBonesSpecAsync)}::", x, "serialization_error");
                }

                Utils.Error(message);

                throw;
            }

            return bonesSpec;
        }

        public static BonesSpec GetPlayerSpec(Zone Zone)
            => new(The.Player, Zone)
            ;

        public static bool ZoneStrataWithinThreshold(int SpecZ, int ZoneZ)
        {
            if (SpecZ.IsSurfaceZ()
                && ZoneZ.IsSurfaceZ())
                return true;

            if (ZoneZ.IsSubterranianZ() != SpecZ.IsSubterranianZ())
                return false;

            if (ZoneZ.IsAerialZ() != SpecZ.IsAerialZ())
                return false;

            int cappedZoneZ = Math.Min(ZoneZ, 20);
            int cappedSpecZ = Math.Min(SpecZ, 20);

            if (Math.Abs(cappedZoneZ - cappedSpecZ) > 5)
                return false;

            return true;
        }

        public bool IsWithinSpec(BonesSpec PlayerSpec)
        {
            if ((Level / (double)PlayerSpec.Level) < 0.9)
                return false;

            if ((PlayerSpec.Level / (double)Level) < 0.9)
                return false;

            if (!ZoneStrataWithinThreshold(ZoneZ, PlayerSpec.ZoneZ))
                return false;

            if (Math.Abs(ZoneTier - PlayerSpec.ZoneZ) > 5)
                return false;

            if (ZoneTerrainType != PlayerSpec.ZoneTerrainType)
                return false;

            if (RegionTier != PlayerSpec.RegionTier)
                return false;

            if (TerrainTravelClass != PlayerSpec.TerrainTravelClass)
                return false;

            return true;
        }

        public bool IsWithinSpec(Zone Zone)
            => IsWithinSpec(new BonesSpec(The.Player, Zone))
            ;
    }
}
