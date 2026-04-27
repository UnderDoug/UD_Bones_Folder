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

using UD_Bones_Folder.Mod.UI;

using UnityEngine;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using static UD_Bones_Folder.Mod.Const;
using static XRL.World.Parts.UD_Bones_MoonKingAnnouncer;

using Event = XRL.World.Event;

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
        public static FileLocationData SyncedFileLocation => FileLocationData.NewSynced(SyncedPath);

        /*public static HashSet<Host> Hosts = new HashSet<Host>
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
        };*/

        //public static string Host => "http://localhost:8000/";
        //public static string Host => "http://osseousash.cloud/";

        public static string OsseousAshDirectoryName => "OsseousAsh";

        public static string ConfigFileName => $"{nameof(Configuration)}.json";
        public static string HostsFileName => $"{nameof(Hosts)}.json";

        public static string DefaultOsseousAshHandle => "{{K|A Mysterious Stranger}}";

        public const string OSSEOUS_ASH_UPLOADS = "{{yellow|uploads}}";
        public const string OSSEOUS_ASH_DOWNLOADS = "{{red|downloads}}";

        private static Configuration _Config;
        public static Configuration Config => _Config ??= Configuration.ReadOrNewAsync()?.WaitResult();

        private static Rack<HostCollection> _Hosts;
        public static Rack<HostCollection> Hosts => _Hosts ??= FindHostCollections()?.WaitResult();

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

        [ModSensitiveCacheInit]
        public static void EnsureHosts()
        {
            _ = Hosts;
        }

        public static bool TryFindBestOsseousAshPath(out FileLocationData FileLocationData, string FileName)
        {
            FileLocationData currentLocationData = null;
            FileLocationData = null;
            bool hasFile = false;
            foreach (var osseousAshLocationData in GetOsseousAshFileLocationData())
            {
                if (osseousAshLocationData.Type >= FileLocationData.LocationType.Mod)
                    continue;

                try
                {
                    currentLocationData = osseousAshLocationData;
                    if (currentLocationData?.Exists() is true)
                    {
                        FileLocationData = osseousAshLocationData;

                        if (FileLocationData.FileExists(FileName))
                        {
                            hasFile = true;
                            break;
                        }

                        FileLocationData = null;
                    }
                }
                catch (Exception x)
                {
                    Utils.Error(currentLocationData, x);
                }
            }

            if (!hasFile)
                currentLocationData = SyncedFileLocation;

            if ((currentLocationData?.EnsureExists()).IsNullOrEmpty())
                    return false;

            if (FileLocationData == null)
                FileLocationData = currentLocationData;

            return true;
        }

        public static IEnumerable<FileLocationData> GetOsseousAshFileLocationData(bool Ensure = false)
        {
            if (Ensure)
            {
                if (!SyncedFileLocation.EnsureExists().IsNullOrEmpty())
                    yield return SyncedFileLocation;

                if (!LocalFileLocation.EnsureExists().IsNullOrEmpty())
                    yield return LocalFileLocation;
            }
            else
            {
                yield return SyncedFileLocation;
                yield return LocalFileLocation;
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

        public static async Task<Rack<HostCollection>> FindHostCollections()
        {
            Rack<HostCollection> hosts = null;
            foreach (var fileLocationData in GetOsseousAshFileLocationData(Ensure: true))
            {
                try
                {
                    hosts ??= new();
                    if (fileLocationData.FileExists(HostsFileName)
                        && await HostCollection.ReadFromFileAsync(fileLocationData, HostsFileName) is HostCollection loadedHostCollection)
                    {
                        loadedHostCollection.LocationData = fileLocationData;
                        if (loadedHostCollection != null)
                            hosts.Add(loadedHostCollection);
                    }
                    else
                    {
                        var newHostCollection = new HostCollection(fileLocationData);
                        newHostCollection.Write();
                        hosts.Add(newHostCollection);
                    }
                }
                catch (Exception x)
                {
                    Utils.Error($"Finding {nameof(HostCollection).Pluralize()}", x);
                }
            }
            if (hosts.All(hc => hc.IsNullOrEmpty())
                && await HostCollection.ReadOrNewAsync() is HostCollection defaultHosts)
            {
                hosts ??= new();
                hosts.Add(defaultHosts);
            }
            if (hosts.IsNullOrEmpty()
                || hosts.All(hc => hc.IsNullOrEmpty()))
                return null;

            return hosts;
        }

        public static IEnumerable<Host> AllHosts(Predicate<Host> Where)
        {
            foreach (var hostsCollection in Hosts ?? Enumerable.Empty<HostCollection>())
                foreach (var host in hostsCollection ?? Enumerable.Empty<Host>())
                    if (Where?.Invoke(host) is not false)
                        yield return host;
        }

        public static IEnumerable<Host> AllHosts()
            => AllHosts(null)
            ;

        public static IEnumerable<KeyValuePair<FileLocationData, Host>> AllHostsWithLocation(Predicate<KeyValuePair<FileLocationData, Host>> Where)
        {
            foreach (var hostCollection in Hosts ?? Enumerable.Empty<HostCollection>())
            {
                foreach (var host in hostCollection ?? Enumerable.Empty<Host>())
                {
                    var element = new KeyValuePair<FileLocationData, Host>(hostCollection.LocationData, host);
                    if (Where?.Invoke(element) is not false)
                        yield return element;
                }
            }
        }

        public static IEnumerable<KeyValuePair<FileLocationData, Host>> AllHostsWithLocation()
            => AllHostsWithLocation(null)
            ;

        #region Option Handling

        public static async Task ManageHostsOptionButton()
        {
            PickOptionDataSetAsync<KeyValuePair<FileLocationData, Host>, bool?> options;
            do
            {
                _Hosts = null;
                using var hostInfos = ScopeDisposedList<KeyValuePair<FileLocationData, Host>>.GetFromPoolFilledWith(AllHostsWithLocation());
                options = new PickOptionDataSetAsync<KeyValuePair<FileLocationData, Host>, bool?>
                {
                    new PickOptionDataAsync<KeyValuePair<FileLocationData, Host>, bool?>
                    {
                        Text = "new host",
                        Icon = new Renderable(Tile: "UI/sw_newchar.bmp", ColorString: "&W", TileColor: "&W", DetailColor: 'y'),
                        Hotkey = 'n',
                        Callback = PerformNewHostAsync,
                    }
                };
                int offset = options.Count;
                using var exludeHotkeys = ScopeDisposedList<char>.GetFromPoolFilledWith(options.GetHotkeys());
                foreach (var hostInfo in hostInfos)
                {
                    options.Add(
                        Item: new PickOptionDataAsync<KeyValuePair<FileLocationData, Host>, bool?>
                        {
                            Element = hostInfo,
                            Text = $"[{hostInfo.Key.Type.GetColoredString()}] {hostInfo.Value.DisplayName()}",
                            Icon = new Renderable(
                                Tile: "Mutations/gas_generation.bmp",
                                ColorString: "&K",
                                TileColor: "&K",
                                DetailColor: FileLocationData.GetFileLocationDataTypeColor(hostInfo.Key.Type)[0]),
                            Hotkey = options.GetHotkeys().GetNextHotKey(Excluding: exludeHotkeys),
                            Callback = e => AskDoWhatWithHostAsync(e.Value, Hosts.FirstOrDefault(h => h.LocationData == e.Key))
                        });
                }
            }
            while (await UIUtils.PerformPickOptionAsync(
                OptionDataSet: options,
                BreakWhen: v => v is not true,
                Title: "{{yellow|Manage {{black|Osseous Ash}} Hosts}}",
                Intro: $"Use the options below to manage the hosts to/from which you'd like to upload/download bones files.",
                IntroIcon: new Renderable(
                    Tile: "Mutations/gas_generation.bmp",
                    ColorString: "&K",
                    TileColor: "&K",
                    DetailColor: 'y'),
                DefaultSelected: 1,
                AllowEscape: true,
                ValueOnEscape: null,
                FinalSelectedCallback: UIUtils.ShowEscancellepedAsync) is not null);
        }

        private static async Task<bool?> PerformNewHostAsync(KeyValuePair<FileLocationData, Host> Element)
        {
            while (true)
            {
                if ((await AskHostLocation()) is not FileLocationData chosenLocation)
                    break;

                string entered = await AskFullHostName(chosenLocation);

                string authToken = null;
                if (entered != null)
                    authToken = await AskHostAuthToken(null);

                var newHost = !entered.IsNullOrEmpty()
                    ? new Host(entered, authToken)
                    : null
                    ;

                bool? confirmed = await ConfirmParsedHost(newHost);

                if (!confirmed.HasValue)
                    break;

                if (confirmed.GetValueOrDefault())
                {
                    (Hosts.FirstOrDefault(s => s.LocationData == chosenLocation)
                        ?? new HostCollection(chosenLocation))
                    .WriteAddHost(newHost);
                    break;
                }
            }
            return true;
        }

        private static async Task<FileLocationData> AskHostLocation()
        {
            using var osseousAshFilePaths = ScopeDisposedList<FileLocationData>.GetFromPoolFilledWith(GetOsseousAshFileLocationData(Ensure: true));
            var options = new Rack<string>();
            var hotkeys = new Rack<char>();

            foreach (var filePath in osseousAshFilePaths)
            {
                options.Add($"[{filePath.Type.GetColoredString()}] {filePath.SanitiseForDisplay()}");
                hotkeys.Add(filePath.Type.ToString().ToLower()[0]);
            }

            string localType = FileLocationData.LocationType.Local.GetColoredString();
            string syncedType = FileLocationData.LocationType.Synced.GetColoredString();

            int choice = await Popup.PickOptionAsync(
                    Title: "{{yellow|New {{black|Osseous Ash}} Host}}",
                    Intro: $"Which location would you like to record your new host?\n\n" +
                        $"Locations marked [{localType}] only exist on this device, whereas ones marked [{syncedType}] will be synced by the platform managing the game's installation.",
                    Options: options,
                    Hotkeys: hotkeys,
                    IntroIcon: new Renderable(
                        Tile: "Mutations/gas_generation.bmp",
                        ColorString: "&K",
                        TileColor: "&K",
                        DetailColor: 'y'),
                    DefaultSelected: 1,
                    AllowEscape: true);

            return osseousAshFilePaths.ElementAtOrDefault(choice);
        }

        private static async Task<string> AskFullHostName(FileLocationData ChosenLocation)
        {
            if (ChosenLocation == null)
                return null;

            var sB = Event.NewStringBuilder();
            sB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                .AppendLine().AppendLine();

            sB.Append("Adding to location:")
                .AppendLine().Append("[").Append(ChosenLocation.Type.GetColoredString()).Append("] ")
                .Append(ChosenLocation.SanitiseForDisplay())
                .AppendLine().AppendLine();

            sB.Append($"Enter the full host name of the host you'd like to add, including the protocol and, if applicable, the port number.")
                .AppendLine().AppendLine();
            sB.Append("Examples:")
                .AppendLine().Append("https://osseousash.cloud/")
                .AppendLine().Append("http://localhost:8000/")
                .AppendLine().AppendLine();
            sB.Append("You'll be asked to confirm the parsed result.");

            return await Popup.AskStringAsync(Event.FinalizeString(sB), Default: "", ReturnNullForEscape: true);
        }

        private static async Task<string> AskHostAuthToken(string PrependMessage)
        {
            var sB = Event.NewStringBuilder();
            sB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                .AppendLine().AppendLine();
            if (!PrependMessage.IsNullOrEmpty())
                sB.Append(PrependMessage)
                    .AppendLine().AppendLine();

            sB.Append($"If the new host requires authentication of some kind, enter the required key below.")
                .AppendLine().AppendLine();
            sB.Append("You can change/update this later if necessary.");

            return await Popup.AskStringAsync(Event.FinalizeString(sB), Default: "", ReturnNullForEscape: true);
        }

        private static async Task<bool?> ConfirmParsedHost(Host NewHost)
        {
            if (NewHost == null)
            {
                await ShowCancelledAddHost();
                return true;
            }

            var confirmResult = await Popup.ShowYesNoCancelAsync(
                    Message: Event.FinalizeString(
                        SB: Event.NewStringBuilder("The host you've entered was parsed in the following way:")
                            .AppendLine()
                            .AppendLine().AppendPair(nameof(NewHost.Name), NewHost.Name)
                            .AppendLine().AppendPair(nameof(NewHost.Port), (NewHost.Port)?.ToString() ?? "")
                            .AppendLine().AppendPair(nameof(NewHost.Encrypted), NewHost.Encrypted)
                            .AppendLine().AppendPair(nameof(NewHost.AuthToken), NewHost.AuthToken)
                            .AppendLine()
                            .AppendLine().Append(NewHost.GetHostNameWithProtocol())
                            .AppendLine()
                            .AppendLine().Append("Is this correct?"))
                    );

            switch (confirmResult)
            {
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    return false;
                case DialogResult.Cancel:
                default:
                    await ShowCancelledAddHost();
                    return null;
            }
        }

        private static async Task ShowCancelledAddHost()
        {
            await Popup.ShowAsync(
                Message: Event.FinalizeString(
                    SB: Event.NewStringBuilder("Addition of new host cancelled."))
                );
        }

        private static async Task<bool?> AskDoWhatWithHostAsync(
            Host Host,
            HostCollection HostCollection
            )
        {
            var locationData = HostCollection.LocationData;
            var pair = (Host, HostCollection);

            PickOptionDataSetAsync<(Host Host, HostCollection Hosts), bool?> options;
            bool? result;
            do
            {
                options = new PickOptionDataSetAsync<(Host Host, HostCollection Hosts), bool?>
                    {
                        new PickOptionDataAsync<(Host Host, HostCollection Hosts), bool?>
                        {
                            Element = pair,
                            Text = "modify",
                            Hotkey = 'm',
                            Callback = p => PerformModifyHostAsync(p.Host, p.Hosts)
                        },
                        new PickOptionDataAsync<(Host Host, HostCollection Hosts), bool?>
                        {
                            Element = pair,
                            Text = "migrate",
                            Hotkey = 'M',
                            Callback = p => PerformMigrateHostAsync(p.Host, p.Hosts)
                        },
                        new PickOptionDataAsync<(Host Host, HostCollection Hosts), bool?>
                        {
                            Element = pair,
                            Text = "delete",
                            Hotkey = 'd',
                            Callback = async delegate ((Host Host, HostCollection Hosts) p)
                            {
                                string message = $"Are you sure you want to delete {p.Host.DisplayName()} from {p.Hosts.DisplayName()}?";
                                if ((await Popup.ShowYesNoCancelAsync(message)) == DialogResult.Yes)
                                    p.Hosts.WriteRemoveHost(p.Host);
                                return true;
                            }
                        },
                    };

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    BreakWhen: v => v is not true,
                    Title: "{{yellow|Manage {{black|Osseous Ash}} Host}}",
                    Intro: $"What would you like to do with host {Host.DisplayName()} which is in the below location:\n" +
                        $"{HostCollection.TaggedDisplayName()}\n\n",
                    IntroIcon: new Renderable(
                        Tile: "Mutations/gas_generation.bmp",
                        ColorString: $"&K",
                        TileColor: $"&K",
                        DetailColor: HostCollection.LocationData.GetFileLocationDataTypeColor()[0]),
                    Buttons: UIUtils.BackButton,
                    ValueOnBack: false,
                    AllowEscape: true,
                    ValueOnEscape: null,
                    FinalSelectedCallback: UIUtils.ShowEscancellepedAsync);
            }
            while (result is true);

            if (result is null)
                return null;

            return true;
        }

        private static async Task<bool?> PerformModifyHostAsync(Host Host, HostCollection HostCollection)
        {
            var oldHost = Host.Clone();
            PickOptionDataSetAsync<Host, bool?> options = null;
            bool? result;
            do
            {
                options = new PickOptionDataSetAsync<Host, bool?>
                {
                    new PickOptionDataAsync<Host, bool?>
                    {
                        Element = Host,
                        Text = "Change Name",
                        Hotkey = 'n',
                        Callback = async delegate (Host h)
                        {
                            h.Name = await Popup.AskStringAsync(
                                Message: "Enter a new Name",
                                Default: h.Name,
                                ReturnNullForEscape: true);

                            return !h.Name.IsNullOrEmpty();
                        }
                    },
                    new PickOptionDataAsync<Host, bool?>
                    {
                        Element = Host,
                        Text = "Change Port",
                        Hotkey = 'p',
                        Callback = async delegate (Host h)
                        {
                            h.Port = await Popup.AskNumberAsync(
                                Message: "Enter a new Port",
                                Start: h.Port ?? 0,
                                Min: 0);

                            return h.Port == null
                                || h.Port >= 0;
                        }
                    },
                    new PickOptionDataAsync<Host, bool?>
                    {
                        Element = Host,
                        Text = Host.Encrypted.GetCheckboxText(nameof(Host.Encrypted)),
                        Hotkey = 'e',
                        Callback = Host.FlipEncryptedAsync,
                    },
                    new PickOptionDataAsync<Host, bool?>
                    {
                        Element = Host,
                        Text = "Change Auth Token",
                        Hotkey = 'a',
                        Callback = async delegate (Host h)
                        {
                            h.AuthToken = await Popup.AskStringAsync(
                                Message: "Enter a new Auth Token",
                                Default: h.AuthToken ?? "",
                                ReturnNullForEscape: true);

                            bool returnValue = h.AuthToken != null;

                            if (h.AuthToken == "")
                                h.AuthToken = null;

                            return returnValue;
                        },
                    },
                    new PickOptionDataAsync<Host, bool?>
                    {
                        Element = Host,
                        Text = Host.Enabled.GetCheckboxText(nameof(Host.Enabled)),
                        Hotkey = 't',
                        Callback = Host.FlipEnabledAsync,
                    },
                    new PickOptionDataAsync<Host, bool?>
                    {
                        Element = Host,
                        Text = "return",
                        Hotkey = 'r',
                        Callback = async delegate (Host h)
                        {
                            if (await AskSureLoseUnsavedAsync(h, oldHost))
                            {
                                oldHost.CopyTo(ref h);
                                return false;
                            }
                            return true;
                        },
                    },
                };

                if (Host != oldHost)
                {
                    options.Insert(
                        Index: options.FirstIndexOrDefault(o => o.Text.EqualsNoCase("return")),
                        Item: new PickOptionDataAsync<Host, bool?>
                        {
                            Element = Host,
                            Text = "undo changes",
                            Hotkey = 'u',
                            Callback = h => Task.Run<bool?>(delegate ()
                            {
                                oldHost.CopyTo(ref h);
                                return true;
                            }),
                        });

                    options.Add(new PickOptionDataAsync<Host, bool?>
                    {
                        Element = Host,
                        Text = "save && return",
                        Hotkey = 's',
                        Callback = async delegate (Host h)
                        {
                            var result = await ConfirmModifiedHostAsync(oldHost, h);

                            if (result is null)
                                return true;

                            if (result is true)
                            {
                                HostCollection.Write();
                                oldHost = h.Clone();
                                return false;
                            }
                            return true;
                        }
                    });
                }

                int defaultIndex = options.FirstIndexOrDefault(o
                    => (Host == oldHost
                         && o.Text.EqualsNoCase("return"))
                    || o.Text.EqualsNoCase("save & return"));

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    BreakWhen: v => v is not true,
                    Title: "{{yellow|Modify {{black|Osseous Ash}} Host}}",
                    Intro: $"Use the options below to modify host {Host.DisplayName()} which is in the below location:\n" +
                        $"{HostCollection.TaggedDisplayName()}\n\n",
                    IntroIcon: new Renderable(
                        Tile: "Mutations/gas_generation.bmp",
                        ColorString: $"&K",
                        TileColor: $"&K",
                        DetailColor: HostCollection.LocationData.GetFileLocationDataTypeColor()[0]),
                    DefaultSelected: defaultIndex,
                    AllowEscape: true,
                    ValueOnEscape: null,
                    FinalSelectedCallback: async delegate (PickOptionData<Host, Task<bool?>> o, Task<bool?> r)
                    {
                        if ((await r.AwaitResultIfNotIsCompletedSuccessfully()) is not true
                            && await AskSureLoseUnsavedAsync(o.Element, oldHost))
                            r = Task.Run<bool?>(() => true);

                        return await UIUtils.ShowEscancellepedAsync(o, r);
                    });

                options?.Clear();
                options = null;
            }
            while (result is true);

            if (result is null)
                return null;

            return true;
        }

        private static async Task<bool?> PerformMigrateHostAsync(Host Host, HostCollection HostCollection)
        {
            PickOptionDataSetAsync<HostCollection, bool?> options = null;
            bool? result;
            do
            {
                using var hostCollections = ScopeDisposedList<HostCollection>.GetFromPoolFilledWith(Hosts);
                hostCollections.Remove(HostCollection);

                options = new PickOptionDataSetAsync<HostCollection, bool?>
                    {
                        new PickOptionDataAsync<HostCollection, bool?>
                        {
                            Element = HostCollection,
                            Text = $"Leave in {HostCollection.TaggedDisplayName()}".Colored("black"),
                            Hotkey = 'x',
                            Callback = hc => Task.Run<bool?>(() => false), // false breaks and returns but doesn't exit.
                        }
                    };

                using var excludedHotkeys = ScopeDisposedList<char>.GetFromPoolFilledWith(options.GetHotkeys());
                foreach (var hostCollection in hostCollections)
                {
                    options.Add(new PickOptionDataAsync<HostCollection, bool?>
                    {
                        Element = hostCollection,
                        Text = hostCollection.TaggedDisplayName(),
                        Hotkey = options.GetHotkeys().GetNextHotKey(Excluding: excludedHotkeys),
                        Callback = async delegate (HostCollection hc)
                        {
                            if ((await Popup.ShowYesNoCancelAsync(
                                Message: $"Confirm migration of {Host.DisplayName()}\n\n" +
                                    $"From:\n" +
                                    $"{HostCollection.TaggedDisplayName()}\n\n" +
                                    $"To:\n" +
                                    $"{hostCollection.TaggedDisplayName()}"
                                )) == DialogResult.Yes)
                            {
                                HostCollection.WriteRemoveHost(Host);
                                hostCollection.WriteAddHost(Host);
                            }
                            return false;
                        }
                    });
                }

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    BreakWhen: v => v is not true,
                    Title: "{{yellow|Migrate {{black|Osseous Ash}} Host}}",
                    Intro: $"Which location would you like to migrate host {Host.DisplayName()} to?\n\n",
                    IntroIcon: new Renderable(
                        Tile: "Mutations/gas_generation.bmp",
                        ColorString: $"&K",
                        TileColor: $"&K",
                        DetailColor: HostCollection.LocationData.GetFileLocationDataTypeColor()[0]),
                    Buttons: UIUtils.BackButton,
                    ValueOnBack: false,
                    AllowEscape: true,
                    ValueOnEscape: null,
                    FinalSelectedCallback: UIUtils.ShowEscancellepedAsync);
            }
            while (result is true);

            if (result is null)
                return null;

            return true;
        }

        private static async Task<bool> AskSureLoseUnsavedAsync(Host Host, Host OldHost)
            => Host == OldHost
            || (await Popup.ShowYesNoCancelAsync("Your unsaved changes will be lost.\n\nAre you sure?")) == DialogResult.Yes;

        private static async Task<bool?> ConfirmModifiedHostAsync(Host OldHost, Host ModifiedHost)
        {
            if (ModifiedHost == null
                || OldHost == ModifiedHost)
            {
                await ShowCancelledModifyHost();
                return true;
            }
            var sB = Event.NewStringBuilder($"You've made the following changes to host {OldHost.DisplayName()}:");
            if (OldHost.Name != ModifiedHost.Name)
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.Name),
                    Value: $"\"{OldHost.Name}\" -> \"{ModifiedHost.Name}\"");

            if (OldHost.Port != ModifiedHost.Port)
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.Port),
                    Value: $"\"{(OldHost.Port)?.ToString() ?? ""}\" -> \"{(ModifiedHost.Port)?.ToString() ?? ""}\"");

            if (OldHost.Encrypted != ModifiedHost.Encrypted)
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.Encrypted),
                    Value: $"\"{OldHost.Encrypted}\" -> \"{ModifiedHost.Encrypted}\"");

            if (OldHost.AuthToken != ModifiedHost.AuthToken)
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.AuthToken),
                    Value: $"\"{OldHost.AuthToken}\" -> \"{ModifiedHost.AuthToken}\"");

            sB.AppendLine().AppendLine();
            sB.AppendLine().Append(ModifiedHost.GetHostNameWithProtocol())
                .AppendLine().AppendLine()
                .Append("Is this correct?");

            var confirmResult = await Popup.ShowYesNoCancelAsync(Event.FinalizeString(sB));

            switch (confirmResult)
            {
                case DialogResult.Yes:
                    return true;
                case DialogResult.No:
                    return false;
                case DialogResult.Cancel:
                default:
                    await ShowCancelledModifyHost();
                    return null;
            }
        }

        private static async Task ShowCancelledModifyHost()
        {
            await Popup.ShowAsync(
                Message: Event.FinalizeString(
                    SB: Event.NewStringBuilder("Modification of host cancelled."))
                );
        }

        #endregion

        public static async Task<bool> TryUploadBones(
            string BonesID,
            SaveBonesJSON SaveBonesJSON,
            byte[] SavGz
            )
        {
            try
            {
                bool any = false;
                foreach (var host in AllHosts(h => h.Enabled))
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
            foreach (var host in AllHosts(h => h.Enabled))
            {
                if (host.GetSaveBonesInfos() is not IEnumerable<SaveBonesInfo> saveBonesInfosFromHost
                    || saveBonesInfosFromHost.IsNullOrEmpty())
                    continue;

                foreach (var hostedSaveBonesInfo in saveBonesInfosFromHost)
                {
                    if ((saveBonesInfos?.Any(info => info.ID == hostedSaveBonesInfo.ID)) is true)
                        continue;

                    saveBonesInfos ??= new();
                    saveBonesInfos.Add(hostedSaveBonesInfo);
                }
            }
            return saveBonesInfos;
        }
    }
}
