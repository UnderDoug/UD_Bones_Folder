using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using ConsoleLib.Console;

using XRL;
using XRL.World;

namespace UD_Bones_Folder.Mod
{
    public static class Extensions
    {
        public static SaveBonesJSON CreateSaveBonesJSON(this XRLGame Game, IDeathEvent E, GameObject MoonKing)
        {
            Utils.Log($"start of CreateSaveBonesJSON.");
            var render = The.Player.RenderForUI("SaveBonesInfo", true);
            Utils.Log($"got render.");
            var timeSpan = TimeSpan.FromTicks(Game._walltime);
            var zone = MoonKing.CurrentZone;
            Utils.Log($"got zone.");
            string zoneID = zone.ZoneID;
            Utils.Log($"about to return new SaveBonesJSON.");
            return new SaveBonesJSON
            {
                SaveVersion = 400,
                GameVersion = Game.GetType().Assembly.GetName().Version.ToString(),
                ID = Game.GameID,
                Name = MoonKing.GetReferenceDisplayName(),
                Level = MoonKing.Statistics["Level"].Value,
                GenoSubType = $"{MoonKing.genotypeEntry.DisplayName} {MoonKing.subtypeEntry.DisplayName}",
                GameMode = Game.GetStringGameState("GameMode", "Classic"),
                CharIcon = render.Tile,
                FColor = render.GetForegroundColorChar(),
                DColor = render.GetDetailColorChar(),

                Location = LoreGenerator.GenerateLandmarkDirectionsTo(zoneID),
                InGameTime = $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}",
                Turn = Game.Turns,
                SaveTime = $"{DateTime.Now.ToLongDateString()} at {DateTime.Now.ToLongTimeString()}",
                ModsEnabled = ModManager.GetRunningMods().ToList(),

                ModVersion = Utils.ThisMod.Manifest.Version.ToString(),

                ZoneID = zoneID,
                DeathReason = E.Reason,
                GenotypeName = MoonKing.GetGenotype(),
                SubtypeName = MoonKing.GetSubtype(),

                ZoneTerrainType = zone.Z > 10 ? "underground" : zone.GetTerrainObject().Blueprint,
                ZoneTier = zone.Tier,
                ZoneRegion = zone.GetRegion(),
            };
        }
    }
}
