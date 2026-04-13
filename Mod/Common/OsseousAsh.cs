using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Platform.IO;

using Qud.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;

using static UD_Bones_Folder.Mod.Const;
using static XRL.World.Parts.UD_Bones_MoonKingAnnouncer;

namespace UD_Bones_Folder.Mod
{
    public static class OsseousAsh
    {
        [Serializable]
        public class OsseousAshJSON
        {
            public Guid ID => Guid.TryParse(GUID, out Guid iD) ? iD : Guid.Empty;
            public string GUID;
            public string Handle;
            public bool AskAtStartup;

            public static async Task<OsseousAshJSON> ReadFromFile(string FilePath)
            {
                OsseousAshJSON osseousAshJSON;
                try
                {
                    osseousAshJSON = await File.ReadAllJsonAsync<OsseousAshJSON>(FilePath);
                }
                catch (Exception x)
                {
                    Utils.Error($"Loading OsseousAsh {FilePath}", x);
                    osseousAshJSON = null;
                }
                return osseousAshJSON;
            }

            public static async Task<OsseousAshJSON> Read()
            {
                if (TryFindBestBonesPath(out string filePath))
                    return await ReadFromFile(filePath);

                return null;
            }

            public static async Task<OsseousAshJSON> ReadOrNew()
            {
                if (TryFindBestBonesPath(out string filePath))
                {
                    if (!(await File.ExistsAsync(filePath))
                        || await ReadFromFile(filePath) is not OsseousAshJSON osseousAshJSON)
                    {
                        var iD = Guid.NewGuid();
                        osseousAshJSON = new OsseousAshJSON
                        {
                            AskAtStartup = true,
                            Handle = DefaultOsseousAshHandle,
                            GUID = iD.ToString(),
                        };
                        Options.DoOsseousAshStartupPopup = true;
                        osseousAshJSON.WriteToFile(filePath);
                        return osseousAshJSON;
                    }
                    Options.DoOsseousAshStartupPopup = osseousAshJSON.AskAtStartup;
                    return osseousAshJSON;
                }
                return null;
            }

            public void WriteToFile(string FilePath)
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, prettyPrint: true));
            }

            public void Write()
            {
                if (TryFindBestBonesPath(out string filePath))
                    WriteToFile(filePath);
            }

            public void WriteAskAtStartup(bool AskAtStartup)
            {
                if (this.AskAtStartup != AskAtStartup)
                {
                    this.AskAtStartup = AskAtStartup;
                    XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshStartupPopup)}",
                        Value: Options.EnableOsseousAshStartupPopup = AskAtStartup);
                    Write();
                }
            }

            public void WriteHandle(string Handle)
            {
                if (this.Handle != Handle)
                {
                    XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.OsseousAshHandle)}",
                        Value: this.Handle = Handle);
                    Write();
                }
            }

            public void WriteID(Guid ID)
            {
                if (this.ID != ID)
                {
                    this.GUID = ID.ToString();
                    Write();
                }
            }
        }

        public static string Path => Platform.IO.Path.Combine(BonesManager.BonesSyncPath, OsseousAshDirectoryName);

        public static DirectoryInfo PathInfo => DirectoryInfo.NewOnline(Path);

        public static string OsseousAshDirectoryName => "OsseousAsh";

        public static string OsseousAshFileName => $"{OsseousAshDirectoryName}.guid";

        public static string DefaultOsseousAshHandle => "{{K|A Mysterious Stranger}}";

        public const string OSSEOUS_ASH_UPLOADS = "{{yellow|uploads}}";
        public const string OSSEOUS_ASH_DOWNLOADS = "{{red|downloads}}";

        public static OsseousAshJSON Config;

        public static bool WantToAsk
            => Options.DoOsseousAshStartupPopup
            && (EnsureOsseousAshJSON()?.AskAtStartup is true)
            ;

        public static OsseousAshJSON EnsureOsseousAshJSON()
        {
            if (Config == null)
            {
                var osseousAshTask = OsseousAshJSON.ReadOrNew();
                osseousAshTask.Wait();
                Config = osseousAshTask.Result;
            }
            return Config;
        }

        public static bool TryFindBestBonesPath(out string FilePath)
        {
            string currentPath = null;
            FilePath = null;
            foreach (var bonesPath in BonesManager.GetBonesPaths(NonRemoteOnly: true))
            {
                try
                {
                    currentPath = bonesPath;
                    if (Directory.Exists(bonesPath))
                    {
                        FilePath = Platform.IO.Path.Combine(bonesPath, OsseousAshFileName);

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
                currentPath = BonesManager.BonesSyncPath;

            if (!Directory.Exists(currentPath))
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
            if (!Directory.Exists(currentPath))
                return false;

            if (FilePath.IsNullOrEmpty())
                FilePath = Platform.IO.Path.Combine(currentPath, OsseousAshFileName);

            return true;
        }

        public static async Task PerformAskAsync()
        {
            EnsureOsseousAshJSON();
            await AskOnStartup();
        }

        public static async Task AskOnStartup()
        {
            if (GameManager.AwakeComplete)
            {
                if (WantToAsk)
                {
                    if (EnsureOsseousAshJSON() != null)
                        Config.WriteAskAtStartup(false);
                    else
                        Options.DoOsseousAshStartupPopup = false;

                    var choice = await Popup.NewPopupMessageAsync(
                        message: $"Welcome to the {Utils.ThisMod.DisplayTitle} mod!\n\n" +
                            $"We've detected that this is the first time you've launched the game with this mod installed and wanted to let you know about the {OSSEOUS_ASH} community bones cloud.\n\n" +
                            $"Participation in the project is entirely optional but could greatly enhance your experience with the Bones Folder:\n" +
                            $"{"\u0007".Colored("red")} Opting in to \"{OSSEOUS_ASH_DOWNLOADS}\" will include bones files from the {OSSEOUS_ASH} when looking for bones files to load.\n" +
                            $"{"\u0007".Colored("yellow")} Opting in to \"{OSSEOUS_ASH_UPLOADS}\" will upload any bones files saved while the option is enabled to the {OSSEOUS_ASH}, and, if you choose one, will associate a handle with the uploaded bones.\n\n" +
                            $"Irrespective of your choice, you won't be asked again (there's an option to be asked again), and you can always change your decision later in the options menu.",
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
                            afterRender: new FlippableRender(
                                Source: new Renderable(
                                    Tile: "Tiles2/status_flying.bmp",
                                    ColorString: "&R",
                                    TileColor: "&R"),
                                HFlip: false,
                                VFlip: true))
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
                            title: $"Opt in to {OSSEOUS_ASH} {OSSEOUS_ASH_DOWNLOADS}?",
                            afterRender: new Renderable(
                                Tile: "Tiles2/status_flying.bmp",
                                ColorString: "&W",
                                TileColor: "&W"))
                            ).command == "Yes")
                        {
                            any = true;

                            XRL.UI.Options.SetOption(
                                ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshUploads)}",
                                Value: Options.EnableOsseousAshUploads = true);
                            await Options.SetOsseousAshHandle("{{yellow|Last question, I promise!}}");
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
            }
        }
    }
}
