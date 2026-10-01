using System;
using System.Collections.Generic;
using System.Linq;

using XRL.Rules;
using XRL.UI;
using XRL.Wish;
using XRL.World.ZoneBuilders;

using UD_Bones_Folder.Mod;
using Options = UD_Bones_Folder.Mod.Options;
using XRL.Collections;
using ConsoleLib.Console;
using XRL.World.Effects;
using XRL.Core;
using Genkit;
using UD_Bones_Folder.Mod.Serialization;
using UD_Bones_Folder.Mod.Serialization.Delegates;
using UD_Bones_Folder.Mod.UI;

namespace XRL.World.WorldBuilders
{
    [HasGameBasedStaticCache]
    [HasWishCommand]
    [JoppaWorldBuilderExtension]
    public class UD_Bones_WorldBuilder : IJoppaWorldBuilderExtension
    {
        public static BonesManager BonesManager => BonesManager.System;

        public static JoppaWorldBuilder Builder;

        [GameBasedStaticCache(CreateInstance = false)]
        public static string BoneZoneID = null;

        public override void OnBeforeMutableInit(JoppaWorldBuilder Builder)
        {
            if (Options.EnableOsseousAshDownloads)
            {
                Loading.SetLoadingStatus($"Loading Bones...");
                foreach (var host in OsseousAsh.AllHosts(h => h.IsOnCooldown))
                {
                    string hostName = host.GetHostNameWithProtocol();
                    try
                    {
                        host.ManuallyClearStatusCheckTimer(ReasonForOverride: $"{hostName} was cleared attempting to open {nameof(BonesManagement)} UI");
                    }
                    catch (Exception x)
                    {
                        Utils.ErrorTimestamp($"Issue trying to clear timeout cooldown for {hostName}", x);
                    }
                    finally
                    {
                        if (host.IsOnCooldown)
                            Utils.WarnTimestamp($"Failed to clear timeout cooldown for {hostName}.");
                    }
                }
            }
            base.OnBeforeMutableInit(Builder);
        }

        public override void OnAfterBuild(JoppaWorldBuilder Builder)
        {
            UD_Bones_WorldBuilder.Builder ??= Builder;
            base.OnAfterBuild(Builder);
        }

        public override void OnAfterMutableInit(JoppaWorldBuilder Builder)
        {
            UD_Bones_WorldBuilder.Builder ??= Builder;
            Builder?.BuildStep("Diagnosing Moon King Fever", DiagnoseMoonKingFever);
            base.OnAfterMutableInit(Builder);
        }

        public void DiagnoseMoonKingFever(string WorldID)
        {
            if (WorldID != "JoppaWorld")
                return;

            BonesManager.ImmutableLocations = AllImmutableLocations();
        }

        public static IEnumerable<Location2D> YieldAllLocations(Predicate<Location2D> Where = null)
        {
            foreach (var parasangs in (Builder?.worldInfo?.terrainLocations?.Values).IteratorSafe())
                foreach (var parasang in parasangs.IteratorSafe())
                    foreach (var location in parasang.YieldParasangZoneLocations().IteratorSafe())
                        if (Where?.Invoke(location) is not false)
                            yield return location;
        }

        public LocationSet AllImmutableLocations()
            => new LocationSet(YieldAllLocations(l => Builder.mutableMap.GetMutable(l) == 0))
            ;
    }
}
