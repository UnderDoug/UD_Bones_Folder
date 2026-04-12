using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Platform.IO;

using Qud.UI;

using XRL;
using XRL.UI;
using XRL.World;

using static UD_Bones_Folder.Mod.Const;
using static XRL.World.Parts.UD_Bones_MoonKingAnnouncer;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasOptionFlagUpdate(Prefix = MOD_PREFIX)]
    public static class Options
    {
        // Debug Settings
        [OptionFlag] public static bool DebugEnableNoHoarding;
        [OptionFlag] public static bool DebugEnableNoExhuming;
        [OptionFlag] public static bool DebugEnablePickingBones;
        [OptionFlag] public static bool DebugEnableNoCremation;

        // General Settings
        [OptionFlag] public static bool EnableFlashingLightEffects;

        [OptionFlag] public static bool EnableOsseousAshDownloads;
        [OptionFlag] public static bool EnableOsseousAshUploads;

        public static string DefaultOsseousAshHandle => "{{black|A Mysterious Stranger}}";
        [OptionFlag] public static string OsseousAshHandle;
        public static async void SetOsseousAshHandle()
        {
            OsseousAshHandle = await Popup.AskStringAsync(
                Message: $"Enter a name to associate with your bones files in the {OSSEOUS_ASH}.\n\n" +
                $"Please note, this name is subject to server-side moderation (to be implemented).",
                Default: OsseousAshHandle ?? DefaultOsseousAshHandle);
        }

        public static string OsseousAshFileName => "OsseousAsh.guid";

        private static Guid _OsseousAshID;
        public static Guid OsseousAshID
        {
            get
            {
                if (_OsseousAshID == default
                    || _OsseousAshID == Guid.Empty)
                {
                    string currentPath = null;
                    string filePath = null;
                    foreach (string bonesPath in BonesManager.BonesPaths)
                    {
                        try
                        {
                            currentPath = bonesPath;
                            if (Directory.Exists(bonesPath))
                            {
                                filePath = Path.Combine(currentPath, OsseousAshFileName);

                                if (File.Exists(filePath))
                                    break;
                            }
                        }
                        catch (Exception x)
                        {
                            Utils.Error(currentPath, x);
                        }
                    }

                    if (currentPath.IsNullOrEmpty()
                        || filePath.IsNullOrEmpty())
                    {
                        currentPath = BonesManager.BonesSyncPath;
                    }

                    if (!Directory.Exists(currentPath))
                    {
                        try
                        {
                            Directory.CreateDirectory(currentPath);
                        }
                        catch (Exception x)
                        {
                            Utils.Error(x);
                            return _OsseousAshID;
                        }
                    }
                    if (!Directory.Exists(currentPath))
                    {
                        Utils.Error($"{nameof(OsseousAshID)}", new Exception($"Failed to find or create Bones directory {DataManager.SanitizePathForDisplay(currentPath)}"));
                        return _OsseousAshID;
                    }

                    if (filePath.IsNullOrEmpty())
                        Path.Combine(currentPath, OsseousAshFileName);

                    if (!File.Exists(filePath)
                        || File.ReadAllText(filePath) is not string readText
                        || readText.Split('\n') is not string[] readLines
                        || readLines.Length != 3
                        || readLines[2].IsNullOrEmpty()
                        || !Guid.TryParse(readLines[2], out Guid readGuid))
                    {
                        _OsseousAshID = Guid.NewGuid();

                        var sB = Event.NewStringBuilder("Wot u doin' peeping in here?")
                            .AppendLine()
                            .AppendLine().Append(_OsseousAshID);

                        File.WriteAllText(filePath, Event.FinalizeString(sB));
                    }
                    else
                        _OsseousAshID = readGuid;
                }
                return _OsseousAshID;
            }
        }

        [OptionFlag] public static bool EnableOsseousAshStartupPopup;

        [ModSensitiveCacheInit]
        public static async Task AskOnStartup()
        {
            if (EnableOsseousAshStartupPopup)
            {
                EnableOsseousAshStartupPopup = false;
                var choice = await Popup.NewPopupMessageAsync(
                    message: $"Thank you for your interest in the {Utils.ThisMod.DisplayTitle} mod!\n\n" +
                    $"We've detected that this is the first time you've launched the game with this mod installed and wanted to let you know about the {OSSEOUS_ASH} community bones cloud.\n\n" +
                    $"Participation in the project is entirely optional but could greatly enhance your experience with the Bones Folder:\n" +
                    $"\t Opting in to \"Uploads\" will upload any bones files saved while the option is enabled to the {OSSEOUS_ASH}, and, if you choose one, will associate a handle with the uploaded bones.\n" +
                    $"\t Opting in to \"Downloads\" will include bones files from the {OSSEOUS_ASH} when looking for bones files to load.\n\n" +
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
                    var selections = Popup.PickSeveral($"Opting in to {OSSEOUS_ASH}",
                        Sound: null,
                        Options: new List<string>
                        {
                            "Opt me in to {{red|downloads}}!",
                            "Opt me in to {{yellow|uploads}}! (you'll be asked if you want to provide a handle)",
                        },
                        Hotkeys: new List<char>
                        {
                            'D',
                            'U',
                        },
                        Icons: new List<IRenderable>
                        {
                            new FlippableRender(
                                Source: new Renderable(
                                    Tile: "Tiles2/status_flying.bmp",
                                    ColorString: "&R",
                                    TileColor: "&R"),
                                HFlip: false,
                                VFlip: true),
                            new Renderable(
                                Tile: "Tiles2/status_flying.bmp",
                                ColorString: "&W",
                                TileColor: "&W"),
                        },
                        AllowEscape: true);

                    if (selections.IsNullOrEmpty())
                    {
                        await Popup.ShowAsync("You haven't been opted in.\n\n" +
                            "If you'd like to change your mind at any time, there are options available in the options menu.");
                    }
                    else
                    {
                        foreach ((var selected, var _) in selections)
                        {
                            if (selected == 0)
                                EnableOsseousAshDownloads = true;

                            if (selected == 1)
                            {
                                EnableOsseousAshUploads = true;
                                SetOsseousAshHandle();
                            }
                        }
                    }
                }
            }
        }
    }
}
