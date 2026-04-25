using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Platform.IO;

using Qud.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using static UD_Bones_Folder.Mod.Const;
using static XRL.World.Parts.UD_Bones_MoonKingAnnouncer;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasCallAfterGameLoaded]
    [HasVariableReplacer]
    public static partial class OsseousAsh
    {
        public enum WebMethods
        {
            POST,
            GET,
            PUT,
            DELETE,
        }

        public static Dictionary<WebMethods, string> Methods = new()
        {
            { WebMethods.POST, nameof(WebMethods.POST) },
            { WebMethods.GET, nameof(WebMethods.GET) },
            { WebMethods.PUT, nameof(WebMethods.PUT) },
            { WebMethods.DELETE, nameof(WebMethods.DELETE) },
        };

        public static StringMap<string> ContentTypes = new()
        {
            { "json", "application/json" },
            { "octet-stream", "application/octet-stream" },
            { "gzip", "application/octet-stream" },
            { "gz", "application/octet-stream" },
            { "bytearray", "application/octet-stream" },
            { "byte[]", "application/octet-stream" },
            { "bytes", "application/octet-stream" },
        };

        public static string LocalPath => DataManager.SavePath(OsseousAshDirectoryName);
        public static string SyncedPath => DataManager.SyncedPath(OsseousAshDirectoryName);

        public static FileLocationData LocalFileLocation => FileLocationData.NewLocal(LocalPath);
        public static FileLocationData SyncedFileLocation => FileLocationData.NewSync(SyncedPath);

        public static HashSet<Host> Hosts = new HashSet<Host>
        {
            new Host
            {
                Name = "localhost",
                Port = 8000,
                Encrypted = false,
            },
            new Host
            {
                Name = "osseousash.cloud",
                Port = null,
                Encrypted = true
            }
        };

        //public static string Host => "http://localhost:8000/";
        //public static string Host => "http://osseousash.cloud/";

        public static string OsseousAshDirectoryName => "OsseousAsh";

        public static string OsseousAshFileName => $"{OsseousAshDirectoryName}.json";

        public static string DefaultOsseousAshHandle => "{{K|A Mysterious Stranger}}";

        public const string OSSEOUS_ASH_UPLOADS = "{{yellow|uploads}}";
        public const string OSSEOUS_ASH_DOWNLOADS = "{{red|downloads}}";


        private static Configuration _Config;
        public static Configuration Config => _Config ??= Configuration.ReadOrNew()?.WaitResult();

        public static bool WantToAsk
            => Options.EnableOsseousAshStartupPopup
            && (Config?.AskAtStartup is true)
            ;

        [ModSensitiveCacheInit]
        public static void EnsureConfiguration()
        {
            _ = Config;
            if (Options.EnableOsseousAshDownloads)
            {
                BonesManager.ClearHasSaveBones();
                _ = BonesManager.HasSaveBones();
            }
        }

        public static bool TryFindBestOsseousAshPath(out string FilePath)
        {
            string currentPath = null;
            FilePath = null;
            foreach (var osseousAshPath in GetOsseousAshPaths())
            {
                try
                {
                    currentPath = osseousAshPath;
                    if (!currentPath.DirectoryExistsSafe())
                    {
                        FilePath = Path.Combine(osseousAshPath, OsseousAshFileName);

                        if (File.Exists(FilePath))
                            break;

                        FilePath = null;
                    }
                }
                catch (Exception x)
                {
                    Utils.Error(currentPath, x);
                }
            }

            if (FilePath.IsNullOrEmpty())
                currentPath = SyncedFileLocation;

            if (!currentPath.DirectoryExistsSafe())
            {
                try
                {
                    Directory.CreateDirectory(currentPath);
                }
                catch (Exception x)
                {
                    Utils.Error(x);
                    return false;
                }
            }
            if (!currentPath.DirectoryExistsSafe())
                return false;

            if (FilePath.IsNullOrEmpty())
                FilePath = Path.Combine(currentPath, OsseousAshFileName);

            return true;
        }

        public static IEnumerable<string> GetOsseousAshPaths(bool Ensure = false)
        {
            if (Ensure)
            {
                yield return SyncedFileLocation;
                yield return LocalFileLocation;
            }
            else
            {
                if (SyncedFileLocation.Path is string syncPath)
                    yield return syncPath;

                if (LocalFileLocation.Path is string localPath)
                    yield return localPath;
            }
        }

        public static async Task PerformAskAsync()
        {
            EnsureConfiguration();
            await AskOnStartup();
        }

        [CallAfterGameLoaded]
        public static void PerformAsk()
        {
            PerformAskAsync().Wait();
        }

        public static async Task AskOnStartup()
        {
            if (GameManager.AwakeComplete)
            {
                if (WantToAsk)
                {
                    Options.EnableOsseousAshStartupPopup = false;
                    try
                    {
                        var choice = await Popup.NewPopupMessageAsync(
                            message: $"Welcome to the {Utils.ThisMod?.DisplayTitle ?? "Bones(alpha)"} mod!\n\n" +
                                $"We've detected that this is the first time you've launched the game with this mod installed and wanted to let you know about the {OSSEOUS_ASH} community bones cloud.\n\n" +
                                $"Participation in the project is entirely optional but could greatly enhance your experience with the Bones Folder:\n" +
                                $"{"\u0007".Colored("red")} Opting in to \"{OSSEOUS_ASH_DOWNLOADS}\" will include bones files from the {OSSEOUS_ASH} when looking for bones files to load.\n" +
                                $"{"\u0007".Colored("yellow")} Opting in to \"{OSSEOUS_ASH_UPLOADS}\" will upload any bones files saved while the option is enabled to the {OSSEOUS_ASH}, and, if you choose one, will associate a handle with the uploaded bones.\n\n" +
                                $"Irrespective of your choice, you won't be asked again, and you can always change your decision later in the options menu.",
                            buttons: new List<QudMenuItem>
                            {
                                new QudMenuItem
                                {
                                    text = "Opt In",
                                    hotkey = ControlManager.getCommandInputDescription("Accept"),
                                    command = "accept",
                                },
                                new QudMenuItem
                                {
                                    text = "Opt Out",
                                    hotkey = ControlManager.getCommandInputDescription("CmdDelete"),
                                    command = "decline",
                                },
                            },
                            DefaultSelected: 1,
                            title: "{{yellow|New Bones Folder Mod Installation Detected!}}",
                            afterRender: new Renderable(
                                Tile: "Items/sw_bones_1.bmp,Items/sw_bones_2.bmp,Items/sw_bones_3.bmp,Items/sw_bones_4.bmp,Items/sw_bones_5.bmp,Items/sw_bones_6.bmp,Items/sw_bones_7.bmp,Items/sw_bones_8.bmp".CachedCommaExpansion().GetRandomElementCosmetic(),
                                ColorString: "&y",
                                TileColor: "&y",
                                DetailColor: 'K'));

                        if (choice.command == "accept")
                        {
                            bool any = false;
                            if ((await Popup.NewPopupMessageAsync(
                                message: $"Opting in to \"{OSSEOUS_ASH_DOWNLOADS}\" will include bones files from the {OSSEOUS_ASH} when looking for bones files to load.",
                                buttons: PopupMessage.YesNoButton,
                                DefaultSelected: 1,
                                title: $"Opt in to {OSSEOUS_ASH} {OSSEOUS_ASH_DOWNLOADS}?",
                                afterRender: new Renderable(
                                    Tile: "Abilities/tile_supressive_fire.png",
                                    ColorString: "&y",
                                    TileColor: "&y",
                                    DetailColor: 'R'))
                                ).command == "Yes")
                            {
                                any = true;
                                XRL.UI.Options.SetOption(
                                    ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshDownloads)}",
                                    Value: Options.EnableOsseousAshDownloads = true);
                            }

                            if ((await Popup.NewPopupMessageAsync(
                                message: $"Opting in to \"{OSSEOUS_ASH_UPLOADS}\" will upload any bones files saved while the option is enabled to the {OSSEOUS_ASH}, " +
                                    "and, if you choose one, will associate a handle with the uploaded bones.",
                                buttons: PopupMessage.YesNoButton,
                                DefaultSelected: 1,
                                title: $"Opt in to {OSSEOUS_ASH} {OSSEOUS_ASH_UPLOADS}?",
                                afterRender: new FlippableRender(
                                    Source: new Renderable(
                                        Tile: "Abilities/tile_supressive_fire.png",
                                        ColorString: "&y",
                                        TileColor: "&y",
                                        DetailColor: 'W'),
                                    HFlip: false,
                                    VFlip: true))
                                ).command == "Yes")
                            {
                                any = true;

                                XRL.UI.Options.SetOption(
                                    ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshUploads)}",
                                    Value: Options.EnableOsseousAshUploads = true);
                                await Options.ManageOsseousAshHandle();
                            }

                            if (any)
                                return;

                            await Popup.ShowAsync("You haven't been opted in.\n\n" +
                                "If you'd like to change your mind at any time, there are options available in the options menu.");
                        }

                        XRL.UI.Options.SetOption(
                            ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshUploads)}",
                            Value: Options.EnableOsseousAshDownloads = true);

                        XRL.UI.Options.SetOption(
                            ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshUploads)}",
                            Value: Options.EnableOsseousAshUploads = false);
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to ask player about Osseous Ash", x);
                        Options.EnableOsseousAshStartupPopup = true;
                    }
                }
            }
        }

        public static async Task<bool> TryUploadBones(
            string BonesID,
            SaveBonesJSON SaveBonesJSON,
            byte[] SavGz
            )
        {
            try
            {
                bool any = false;
                foreach (var host in Hosts)
                {
                    try
                    {
                        if (await host.TryUploadBonesAsync(BonesID, SaveBonesJSON, SavGz))
                            any = true;
                        else
                            Utils.Warn($"Failed to upload bones to {host}");
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to upload bones to {host}", x);
                        continue;
                    }
                }
                return any;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(TryUploadBones)} failed to upload Bones with BonesID {BonesID}", x);
                return false;
            }
        }

        public static List<SaveBonesInfo> GetBonesInfos()
        {
            List<SaveBonesInfo> saveBonesInfos = null;
            foreach (var host in Hosts)
            {
                if (host.GetSaveBonesInfos() is not IEnumerable<SaveBonesInfo> saveBonesInfosFromHost
                    || saveBonesInfosFromHost.IsNullOrEmpty())
                    continue;

                foreach (var hostedSaveBonesInfo in saveBonesInfosFromHost)
                {
                    if (saveBonesInfos.Any(info => info.ID == hostedSaveBonesInfo.ID))
                        continue;

                    saveBonesInfos.Add(hostedSaveBonesInfo);
                }
            }
            return saveBonesInfos;
        }
    }
}
