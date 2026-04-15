using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Newtonsoft.Json;

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
    public static class OsseousAsh
    {
        [JsonObject(MemberSerialization.OptOut)]
        [Serializable]
        public class OsseousAshJSON
        {
            /*
            [JsonIgnore]
            public Guid ID => Guid.TryParse(StringGuid, out Guid iD) ? iD : Guid.Empty;
            public string StringGuid;*/
            [JsonProperty]
            public Guid ID;
            public string Handle;

            [JsonIgnore]
            private bool _AskAtStartup;
            [JsonProperty]
            public bool AskAtStartup
            {
                get => _AskAtStartup;
                set
                {
                    WriteAskAtStartup(value);
                }
            }

            [JsonIgnore]
            private HashSet<string> LockedMembers = new();

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
                if (TryFindBestOsseousAshPath(out string filePath))
                    return await ReadFromFile(filePath);

                return null;
            }

            public static async Task<OsseousAshJSON> ReadOrNew()
            {
                if (TryFindBestOsseousAshPath(out string filePath))
                {
                    if (!(await File.ExistsAsync(filePath))
                        || await ReadFromFile(filePath) is not OsseousAshJSON osseousAshJSON)
                    {
                        //var iD = Guid.NewGuid();
                        osseousAshJSON = new OsseousAshJSON
                        {
                            AskAtStartup = true,
                            Handle = DefaultOsseousAshHandle,
                            //ID = iD.ToString(),
                            ID = Guid.NewGuid(),
                        };
                        Options.EnableOsseousAshStartupPopup = true;
                        osseousAshJSON.WriteToFile(filePath);
                        return osseousAshJSON;
                    }
                    Options.EnableOsseousAshStartupPopup = osseousAshJSON.AskAtStartup;
                    return osseousAshJSON;
                }
                return null;
            }

            public void WriteToFile(string FilePath)
            {
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(this, Formatting.Indented));
            }

            public void Write()
            {
                if (TryFindBestOsseousAshPath(out string filePath))
                    WriteToFile(filePath);
            }

            public void WriteAskAtStartup(bool Value, bool Propagate = true)
            {
                LockedMembers ??= new();
                if (!LockedMembers.Contains(nameof(AskAtStartup))
                    && _AskAtStartup != Value)
                {
                    LockedMembers.Add(nameof(AskAtStartup));
                    try
                    {
                        _AskAtStartup = Value;

                        Write();

                        XRL.UI.Options.SetOption(
                            ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshStartupPopup)}",
                            Value: Options.EnableOsseousAshStartupPopup = Value);
                    }
                    finally
                    {
                        LockedMembers.Remove(nameof(AskAtStartup));
                    }
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
                    //this.ID = ID.ToString();
                    this.ID = ID;
                    Write();
                }
            }
        }

        public static string LocalPath => DataManager.SavePath(OsseousAshDirectoryName);
        public static string SyncedPath => DataManager.SyncedPath(OsseousAshDirectoryName);

        public static DirectoryInfo LocalPathInfo => DirectoryInfo.NewOnline(LocalPath);
        public static DirectoryInfo SyncedPathInfo => DirectoryInfo.NewOnline(SyncedPath);

        public static string OsseousAshDirectoryName => "OsseousAsh";

        public static string OsseousAshFileName => $"{OsseousAshDirectoryName}.json";

        public static string DefaultOsseousAshHandle => "{{K|A Mysterious Stranger}}";

        public const string OSSEOUS_ASH_UPLOADS = "{{yellow|uploads}}";
        public const string OSSEOUS_ASH_DOWNLOADS = "{{red|downloads}}";


        private static OsseousAshJSON _Config;
        public static OsseousAshJSON Config
        {
            get
            {
                if (_Config == null)
                {
                    var osseousAshTask = OsseousAshJSON.ReadOrNew();
                    osseousAshTask.Wait();
                    _Config = osseousAshTask.Result;
                }
                return _Config;
            }
        }

        public static bool WantToAsk
            => Options.EnableOsseousAshStartupPopup
            && (Config?.AskAtStartup is true)
            ;

        [ModSensitiveCacheInit]
        public static void EnsureOsseousAshJSON()
        {
            _ = Config;
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
                currentPath = SyncedPathInfo;

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
                yield return SyncedPathInfo;
                yield return LocalPathInfo;
            }
            else
            {
                if (SyncedPathInfo.Path is string syncPath)
                    yield return syncPath;

                if (LocalPathInfo.Path is string localPath)
                    yield return localPath;
            }
        }

        public static async Task PerformAskAsync()
        {
            EnsureOsseousAshJSON();
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

        /// <summary>
        /// Returns an OsseousAsh Handle, or the word "you" unless no 2PP is specified.
        /// </summary>
        /// <remarks>
        /// Utilises the following params; paired params indicate those are ordered according to the pair; unordered otherwise:
        /// <list type="bullet">
        ///     <item>"No2PP"[<see cref="string"/>]: disable second person perspective (case insensitive).</item>
        ///     <item>OsseousAshID[<see cref="Guid"/>]: string Guid (parsed) to check against for being the current player.</item>
        ///     <item>"Handle"[<see cref="string"/>]:OsseaousAshHandle[<see cref="string"/>]: literal <see cref="string"/> "Handle" (case insensitive) and then the <see cref="string"/> result to output if the OsseousAsh ID doesn't match this player.</item>
        /// </list>
        /// </remarks>
        /// <param name="Context">Context parameter provided by the <see cref="XRL.World.Text.ReplaceBuilder"/></param>
        /// <returns>The string "you" or the player's OsseousAsh Handle (if "no2pp"), or the specified OsseousAsh Handle if a specified OsseousAsh ID doesn't match the player's.</returns>
        [VariableReplacer(Keys = new string[] { "OsseousAshHandle" }, Capitalization = false)]
        public static string OsseousAshHandle(DelegateContext Context)
        {
            if (Config == null)
                return "you";

            if (Context.Parameters is not List<string> parameters
                || parameters.IsNullOrEmpty())
                return Config.Handle ?? DefaultOsseousAshHandle;

            string you = "you";
            if (parameters.Any(p => p.EqualsNoCase("No2PP")))
                you = Config.Handle ?? DefaultOsseousAshHandle;

            foreach (var param in parameters)
            {
                if (Guid.TryParse(param, out Guid OAID))
                {
                    if (Config.ID == OAID)
                        return you;
                }
            }
            
            for (int i = 0; i < parameters.Count; i++)
            {
                if (parameters[i].EqualsNoCase("handle")
                    && (i + 1) < parameters.Count)
                {
                    return parameters[i + 1];
                }
            }

            return you;
        }
    }
}
