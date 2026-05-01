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
using GameObject = XRL.World.GameObject;

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

        public static List<QudMenuItem> _OptInOptOut = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{y|Opt In}}",
                command = "accept",
                hotkey = "Accept",
            },
            new QudMenuItem
            {
                text = "{{y|Opt Out}}",
                command = "decline",
                hotkey = "N,V Negative"
            },
        };

        public static List<QudMenuItem> OptInOptOut
        {
            get
            {
                if (ControlManager.activeControllerType != ControlManager.InputDeviceType.Gamepad)
                    return _OptInOptOut;

                return new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = ControlManager.getCommandInputFormatted("Accept", XRL.UI.Options.ModernUI) + " {{y|Opt In}}",
                        command = "accept",
                        hotkey = "Accept",
                    },
                    new QudMenuItem
                    {
                        text = ControlManager.getCommandInputFormatted("V Negative", XRL.UI.Options.ModernUI) + " {{y|Opt Out}}",
                        command = "decline",
                        hotkey = "N,V Negative"
                    },
                };
            }
        }

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

        public static InventoryAction ReportBonesInventoryAction => new InventoryAction
        {
            Name = nameof(XRL.World.Parts.UD_Bones_ReportBones),
            Key = '.',
            Display = "report loaded bones",
            Command = REPORT_LOADED_BONES_COMMAND,
            Default = 30,
            Priority = 30,
            WorksAtDistance = true,
            AsMinEvent = true,
        };

        public static string ReportBonesTitle => "{{yellow|Report Loaded Bones}}";
        public static IRenderable ReportBonesIcon
            => new Renderable()
                .setTile("Items/sw_unfurled_scroll1.bmp")
                .setColorString("&K")
                .setTileColor("&K")
                .setDetailColor('y')
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
                                $"Participation in the project is entirely optional but could greatly enhance your experience with the Bones Folder mod:\n" +
                                $"{"\u0007".Colored("red")} Opting in to \"{OSSEOUS_ASH_DOWNLOADS}\" will include bones files from the {OSSEOUS_ASH} when looking for bones files to load.\n" +
                                $"{"\u0007".Colored("yellow")} Opting in to \"{OSSEOUS_ASH_UPLOADS}\" will upload any bones files saved while the option is enabled to the {OSSEOUS_ASH}, and, if you choose one, will associate a handle with the uploaded bones.\n\n" +
                                $"Irrespective of your choice, you won't be asked again, and you can always change your decision later in the options menu.\n\n" +
                                $"Please note: the {OSSEOUS_ASH} community bones cloud requires an internet connection to function.",
                            buttons: OptInOptOut,
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

        public static async Task ManageHostsOptionButtonAsync()
        {
            PickOptionDataSetAsync<KeyValuePair<FileLocationData, Host>, UIUtils.CascadableResult> options;
            do
            {
                options = new PickOptionDataSetAsync<KeyValuePair<FileLocationData, Host>, UIUtils.CascadableResult>
                {
                    new PickOptionDataAsync<KeyValuePair<FileLocationData, Host>, UIUtils.CascadableResult>
                    {
                        Text = "new host",
                        Icon = new Renderable(
                            Tile: "UI/sw_newchar.bmp",
                            ColorString: "&W",
                            TileColor: "&W",
                            DetailColor: 'y'),
                        Hotkey = 'n',
                        Callback = PerformNewHostAsync,
                    }
                };
                int offset = options.Count;
                using var exludeHotkeys = ScopeDisposedList<char>.GetFromPoolFilledWith(options.GetHotkeys());

                _Hosts = null;
                using var hostInfos = ScopeDisposedList<KeyValuePair<FileLocationData, Host>>.GetFromPoolFilledWith(AllHostsWithLocation());
                foreach (var hostInfo in hostInfos)
                {
                    options.Add(
                        Item: new PickOptionDataAsync<KeyValuePair<FileLocationData, Host>, UIUtils.CascadableResult>
                        {
                            Element = hostInfo,
                            Text = $"[{hostInfo.Key.Type.GetColoredString()}] {hostInfo.Value.FullDisplayName(IncludeAuth: true)}",
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
            while ((await UIUtils.PerformPickOptionAsync(
                OptionDataSet: options,
                Title: "{{yellow|Manage {{black|Osseous Ash}} Hosts}}",
                Intro: $"Use the options below to manage the hosts to/from which you'd like to upload/download bones files.",
                IntroIcon: new Renderable(
                    Tile: "Mutations/gas_generation.bmp",
                    ColorString: "&K",
                    TileColor: "&K",
                    DetailColor: 'y'),
                DefaultSelected: 0,
                OnBackCallback: () => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                OnEscapeCallback: () => Task.Run(() => UIUtils.CascadableResult.CancelSilent),
                FinalSelectedCallback: UIUtils.ShowEscancellepedAsync)).IsContinue());

            foreach (var hostCollection in Hosts)
                hostCollection.Write();
        }

        private static async Task<UIUtils.CascadableResult> PerformNewHostAsync(KeyValuePair<FileLocationData, Host> Element)
        {
            bool? confirmed = null;
            do
            {
                if ((await AskHostLocation()) is not FileLocationData chosenLocation)
                    break;

                string entered = await AskFullHostName(chosenLocation);

                string authToken = null;
                if (entered != null)
                    authToken = await AskHostAuthToken(null);

                int? timeout = null;
                if (entered != null
                    && authToken != null)
                    timeout = await AskHostTimeout(null);

                var newHost = !entered.IsNullOrEmpty()
                        && authToken != null
                        && timeout.HasValue
                    ? new Host(entered, authToken, timeout.GetValueOrDefault())
                    : null
                    ;

                confirmed = await ConfirmParsedHost(newHost);

                if (!confirmed.HasValue)
                    break;

                if (newHost == null)
                    break;

                if (confirmed.GetValueOrDefault())
                {
                    (Hosts.FirstOrDefault(s => s.LocationData == chosenLocation)
                        ?? new HostCollection(chosenLocation))
                    .WriteAddHost(newHost);
                    break;
                }
            }
            while (true);

            return confirmed.ToCascadableResult(Silent: true);
        }

        private static async Task<FileLocationData> AskHostLocation()
        {
            using var osseousAshFilePaths = ScopeDisposedList<FileLocationData>.GetFromPoolFilledWith(GetOsseousAshFileLocationData(Ensure: true));
            var hotkeys = new Rack<char>();
            var options = new PickOptionDataSetAsync<FileLocationData, FileLocationData>();

            foreach (var fileLocationData in osseousAshFilePaths)
            {
                options.Add(new PickOptionDataAsync<FileLocationData, FileLocationData>
                {
                    Element = fileLocationData,
                    Text = fileLocationData.TaggedDisplayName(),
                    Hotkey = fileLocationData.Type.ToString().ToLower()[0],
                    Callback = fLD => Task.Run(() => fLD),
                });
            }

            if (options.Count == 1)
                return await options[0].Invoke();

            string localType = FileLocationData.LocationType.Local.GetColoredString();
            string syncedType = FileLocationData.LocationType.Synced.GetColoredString();

            return await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: "{{yellow|New {{black|Osseous Ash}} Host}}",
                    Intro: $"Which location would you like to record your new host?\n\n" +
                        $"\u0007 [{localType}] only exists on this device.\n" +
                        $"\u0007 [{syncedType}] will be synced by the platform managing the game's installation (if there is one).\n\n",
                    IntroIcon: new Renderable(
                        Tile: "Mutations/gas_generation.bmp",
                        ColorString: "&K",
                        TileColor: "&K",
                        DetailColor: 'y'));
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

            return await Popup.AskStringAsync(Event.FinalizeString(sB), Default: "", ReturnNullForEscape: true, AllowColorize: false);
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

            return await Popup.AskStringAsync(Event.FinalizeString(sB), Default: "", ReturnNullForEscape: true, AllowColorize: false);
        }

        private static async Task<int?> AskHostTimeout(string PrependMessage)
        {
            var sB = Event.NewStringBuilder();
            sB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                .AppendLine().AppendLine();
            if (!PrependMessage.IsNullOrEmpty())
                sB.Append(PrependMessage)
                    .AppendLine().AppendLine();

            sB.Append($"Enter the amount of time in milliseconds (-1 for unlimited) to wait before cancelling most requests to this host.")
                .AppendLine().AppendLine();
            sB.Append("You can change/update this later if necessary.");

            return await Popup.AskNumberAsync(Event.FinalizeString(sB), Start: Host.DefaultHost.TimeoutMS, Min: -1);
        }

        private static async Task<bool?> ConfirmParsedHost(Host NewHost)
        {
            if (NewHost == null)
            {
                await ShowCancelledAddHost();
                return false;
            }

            var sB = Event.NewStringBuilder("The host you've entered was parsed in the following way:")
                .AppendLine()
                .AppendLine().AppendPair(nameof(NewHost.Name), NewHost.Name)
                .AppendLine().AppendPair(nameof(NewHost.Port), (NewHost.Port)?.ToString() ?? "")
                .AppendLine().AppendPair(nameof(NewHost.Encrypted), NewHost.Encrypted)
                .AppendLine().AppendPair(nameof(NewHost.TimeoutMS), NewHost.GetTimeoutString())
                .AppendLine().AppendPair(nameof(NewHost.AuthToken), NewHost.AuthToken)
                .AppendLine()
                .AppendLine().Append(NewHost.GetHostNameWithProtocol())
                .AppendLine().AppendPair("Server Status:", NewHost.ServerStatusString)
                .AppendLine()
                .AppendLine().Append("Is this correct?");

            switch (await Popup.ShowYesNoCancelAsync(Event.FinalizeString(sB)))
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

        private static async Task<UIUtils.CascadableResult> AskDoWhatWithHostAsync(
            Host Host,
            HostCollection HostCollection
            )
        {
            var locationData = HostCollection.LocationData;
            var pair = (Host, HostCollection);

            PickOptionDataSetAsync<(Host Host, HostCollection Hosts), UIUtils.CascadableResult> options;
            UIUtils.CascadableResult result;
            do
            {
                options = new PickOptionDataSetAsync<(Host Host, HostCollection Hosts), UIUtils.CascadableResult>
                    {
                        new PickOptionDataAsync<(Host Host, HostCollection Hosts), UIUtils.CascadableResult>
                        {
                            Element = pair,
                            Text = "modify",
                            Hotkey = 'm',
                            Callback = p => PerformModifyHostAsync(p.Host, p.Hosts)
                        },
                        new PickOptionDataAsync<(Host Host, HostCollection Hosts), UIUtils.CascadableResult>
                        {
                            Element = pair,
                            Text = "migrate",
                            Hotkey = 'M',
                            Callback = p => PerformMigrateHostAsync(p.Host, p.Hosts)
                        },
                        new PickOptionDataAsync<(Host Host, HostCollection Hosts), UIUtils.CascadableResult>
                        {
                            Element = pair,
                            Text = "delete",
                            Hotkey = 'd',
                            Callback = async delegate ((Host Host, HostCollection Hosts) p)
                            {
                                string message = $"Are you sure you want to delete {p.Host.GetHostNameWithProtocol()} from {p.Hosts.DisplayName()}?";
                                if ((await Popup.ShowYesNoCancelAsync(message)) == DialogResult.Yes)
                                    p.Hosts.WriteRemoveHost(p.Host);
                                return UIUtils.CascadableResult.BackSilent;
                            }
                        },
                    };

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: "{{yellow|Manage {{black|Osseous Ash}} Host}}",
                    Intro: $"What would you like to do with host {Host.GetHostNameWithProtocol()}, which is in the below location:\n\n" +
                        $"{HostCollection.TaggedDisplayName()}\n\n",
                    IntroIcon: new Renderable(
                        Tile: "Mutations/gas_generation.bmp",
                        ColorString: $"&K",
                        TileColor: $"&K",
                        DetailColor: HostCollection.LocationData.GetFileLocationDataTypeColor()[0]),
                    OnBackCallback: () => Task.Run(() => UIUtils.CascadableResult.Back),
                    OnEscapeCallback: () => Task.Run(() => UIUtils.CascadableResult.Cancel),
                    FinalSelectedCallback: UIUtils.ShowEscancellepedAsync);
            }
            while (result.IsContinue());

            if (result.IsCancel())
                return result;

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformModifyHostAsync(Host Host, HostCollection HostCollection)
        {
            var oldHost = Host.Clone();
            PickOptionDataSetAsync<Host, UIUtils.CascadableResult> options = new();
            UIUtils.CascadableResult result;
            do
            {
                options.Clear();
                options.Add(new PickOptionDataAsync<Host, UIUtils.CascadableResult>
                {
                    Element = Host,
                    Text = $"Change {nameof(Host.Name)}",
                    Hotkey = 'n',
                    Callback = async delegate (Host h)
                    {
                        h.Name = await Popup.AskStringAsync(
                            Message: $"Enter a new {nameof(Host.Name)}",
                            Default: h.Name,
                            ReturnNullForEscape: true,
                            AllowColorize: false);

                        return !h.Name.IsNullOrEmpty()
                            ? UIUtils.CascadableResult.Continue
                            : UIUtils.CascadableResult.Back
                            ;
                    }
                });
                options.Add(new PickOptionDataAsync<Host, UIUtils.CascadableResult>
                {
                    Element = Host,
                    Text = $"Change {nameof(Host.Port)}",
                    Hotkey = 'p',
                    Callback = async delegate (Host h)
                    {
                        h.Port = await Popup.AskNumberAsync(
                            Message: $"Enter a new {nameof(Host.Port)}",
                            Start: h.Port ?? 0,
                            Min: 0);

                        return h.Port == null
                                || h.Port >= 0
                            ? UIUtils.CascadableResult.Continue
                            : UIUtils.CascadableResult.Back
                            ;
                    }
                });
                options.Add(new PickOptionDataAsync<Host, UIUtils.CascadableResult>
                {
                    Element = Host,
                    Text = Host.Encrypted.GetCheckboxText(nameof(Host.Encrypted)),
                    Hotkey = 'e',
                    Callback = async delegate (Host h)
                    {
                        return await Host.FlipEncryptedAsync(h)
                            ? UIUtils.CascadableResult.Continue
                            : UIUtils.CascadableResult.Back
                            ;
                    },
                });
                options.Add(new PickOptionDataAsync<Host, UIUtils.CascadableResult>
                {
                    Element = Host,
                    Text = $"Change Timeout (ms)",
                    Hotkey = 'e',
                    Callback = async delegate (Host h)
                    {
                        var number = await Popup.AskNumberAsync(
                            Message: $"Enter a new Timeout (ms), or -1 for no/unlimited timeout.",
                            Start: h.TimeoutMS,
                            Min: -1);

                        if (!number.HasValue)
                            return UIUtils.CascadableResult.Back;

                        h.TimeoutMS = number.GetValueOrDefault();

                        return UIUtils.CascadableResult.Continue;
                    },
                });
                options.Add(new PickOptionDataAsync<Host, UIUtils.CascadableResult>
                {
                    Element = Host,
                    Text = "Change Auth Token",
                    Hotkey = 'a',
                    Callback = async delegate (Host h)
                    {
                        h.AuthToken = await Popup.AskStringAsync(
                            Message: "Enter a new Auth Token",
                            Default: h.AuthToken ?? "",
                            ReturnNullForEscape: true,
                            AllowColorize: false);

                        var returnValue = h.AuthToken != null
                            ? UIUtils.CascadableResult.Continue
                            : UIUtils.CascadableResult.Back
                            ;

                        if (h.AuthToken == "")
                            h.AuthToken = null;

                        return returnValue;
                    },
                });
                options.Add(new PickOptionDataAsync<Host, UIUtils.CascadableResult>
                {
                    Element = Host,
                    Text = Host.Enabled.GetCheckboxText(nameof(Host.Enabled)),
                    Hotkey = 't',
                    Callback = async delegate (Host h)
                    {
                        return await Host.FlipEnabledAsync(h)
                            ? UIUtils.CascadableResult.Continue
                            : UIUtils.CascadableResult.Back
                            ;
                    },
                });

                List<QudMenuItem> buttons = null;
                Dictionary<int, Func<Task<UIUtils.CascadableResult>>> buttonCallbacks = null;
                if (!Host.SameAs(oldHost))
                {
                    options.Add(new PickOptionDataAsync<Host, UIUtils.CascadableResult>
                    {
                        Element = Host,
                        Text = "revert changes",
                        Hotkey = 'u',
                        Callback = h => Task.Run(delegate ()
                        {
                            oldHost.CopyTo(ref h);
                            return UIUtils.CascadableResult.Continue;
                        }),
                    });

                    buttons = UIUtils.SaveButton;
                    buttonCallbacks = new()
                    {
                        { -3, () => PerformSaveHostAsync(Host, oldHost, HostCollection) }
                    };
                }

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: "{{yellow|Modify {{black|Osseous Ash}} Host}}",
                    Intro: $"Use the options below to modify host {Host.GetHostNameWithProtocol()}, which is in the below location:\n\n" +
                        $"{HostCollection.TaggedDisplayName()}\n\n",
                    IntroIcon: new Renderable(
                        Tile: "Mutations/gas_generation.bmp",
                        ColorString: $"&K",
                        TileColor: $"&K",
                        DetailColor: HostCollection.LocationData.GetFileLocationDataTypeColor()[0]),
                    AdditionalButtons: buttons,
                    ButtonCallbacks: buttonCallbacks,
                    OnBackCallback: () => Task.Run(() => UIUtils.CascadableResult.Back),
                    OnEscapeCallback: () => Task.Run(() => UIUtils.CascadableResult.Cancel),
                    FinalSelectedCallback: async delegate (PickOptionData<Host, Task<UIUtils.CascadableResult>> o, Task<UIUtils.CascadableResult> r)
                    {
                        var result = (await r?.AwaitResultIfNotIsCompletedSuccessfully());
                        if (result is not UIUtils.CascadableResult.Continue
                            && !(await AskSureLoseUnsavedAsync(Host, oldHost)))
                            r = Task.Run(() => UIUtils.CascadableResult.Continue);
                        else
                        if (result is not UIUtils.CascadableResult.Continue
                            && o?.Element == null)
                            oldHost.CopyTo(ref Host);

                        return await UIUtils.ShowEscancellepedAsync(o, r);
                    });
            }
            while (result.ToBool());

            if (result.IsCancel())
                return result;

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformSaveHostAsync(Host Host, Host OldHost, HostCollection HostCollection)
        {
            var result = await ConfirmModifiedHostAsync(OldHost, Host);

            if (result.IsCancel())
                return UIUtils.CascadableResult.Cancel;

            if (result.IsContinue())
            {
                HostCollection.Write();
                Host.CopyTo(ref OldHost);
                return UIUtils.CascadableResult.BackSilent;
            }
            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformMigrateHostAsync(Host Host, HostCollection HostCollection)
        {
            PickOptionDataSetAsync<HostCollection, UIUtils.CascadableResult> options = null;
            UIUtils.CascadableResult result;
            do
            {
                using var hostCollections = ScopeDisposedList<HostCollection>.GetFromPoolFilledWith(Hosts);
                hostCollections.Remove(HostCollection);

                options = new PickOptionDataSetAsync<HostCollection, UIUtils.CascadableResult>
                    {
                        new PickOptionDataAsync<HostCollection, UIUtils.CascadableResult>
                        {
                            Element = HostCollection,
                            Text = $"Leave in {HostCollection.TaggedDisplayName()}".Colored("black"),
                            Hotkey = 'x',
                            Callback = hc => Task.Run(() => UIUtils.CascadableResult.BackSilent),
                        }
                    };

                using var excludedHotkeys = ScopeDisposedList<char>.GetFromPoolFilledWith(options.GetHotkeys());
                foreach (var hostCollection in hostCollections)
                {
                    options.Add(new PickOptionDataAsync<HostCollection, UIUtils.CascadableResult>
                    {
                        Element = hostCollection,
                        Text = hostCollection.TaggedDisplayName(),
                        Hotkey = options.GetHotkeys().GetNextHotKey(Excluding: excludedHotkeys),
                        Callback = async delegate (HostCollection hc)
                        {
                            if ((await Popup.ShowYesNoCancelAsync(
                                Message: $"Confirm migration of {Host.GetHostNameWithProtocol()}\n\n" +
                                    $"From:\n" +
                                    $"{HostCollection.TaggedDisplayName()}\n\n" +
                                    $"To:\n" +
                                    $"{hostCollection.TaggedDisplayName()}"
                                )) == DialogResult.Yes)
                            {
                                HostCollection.WriteRemoveHost(Host);
                                hostCollection.WriteAddHost(Host);
                            }
                            return UIUtils.CascadableResult.BackSilent;
                        }
                    });
                }

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: "{{yellow|Migrate {{black|Osseous Ash}} Host}}",
                    Intro: $"Which location would you like to migrate host {Host.GetHostNameWithProtocol()} to?\n\n",
                    IntroIcon: new Renderable(
                        Tile: "Mutations/gas_generation.bmp",
                        ColorString: $"&K",
                        TileColor: $"&K",
                        DetailColor: HostCollection.LocationData.GetFileLocationDataTypeColor()[0]),
                    OnBackCallback: () => Task.Run(() => UIUtils.CascadableResult.Back),
                    OnEscapeCallback: () => Task.Run(() => UIUtils.CascadableResult.Cancel),
                    FinalSelectedCallback: UIUtils.ShowEscancellepedAsync);
            }
            while (result.IsContinue());

            if (result.IsCancel())
                return result;

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<bool> AskSureLoseUnsavedAsync(Host Host, Host OldHost)
            => Utils.LogReturn($"Host is null", Host is null)
            || Utils.LogReturn($"Host.SameAs(OldHost)", Host.SameAs(OldHost))
            || Utils.LogReturn($"Are you sure? DialogResult.Yes", (await Popup.ShowYesNoCancelAsync("Your unsaved changes will be lost.\n\nAre you sure?")) == DialogResult.Yes)
            ;

        private static async Task<UIUtils.CascadableResult> ConfirmModifiedHostAsync(Host OldHost, Host ModifiedHost)
        {
            if (ModifiedHost == null
                || OldHost == ModifiedHost)
            {
                await ShowCancelledModifyHost();
                return UIUtils.CascadableResult.Continue;
            }
            var sB = Event.NewStringBuilder($"You've made the following changes to host {OldHost.GetHostNameWithProtocol()}:");
            sB.AppendLine();

            bool any = false;
            if (OldHost.Name != ModifiedHost.Name)
            {
                any = true;
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.Name),
                    Value: $"\"{OldHost.Name}\" \u001a \"{ModifiedHost.Name}\"");
            }

            if (OldHost.Port != ModifiedHost.Port)
            {
                any = true;
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.Port),
                    Value: $"\"{(OldHost.Port)?.ToString() ?? ""}\" \u001a \"{(ModifiedHost.Port)?.ToString() ?? ""}\"");
            }

            if (OldHost.Encrypted != ModifiedHost.Encrypted)
            {
                any = true;
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.Encrypted),
                    Value: $"\"{OldHost.Encrypted}\" \u001a \"{ModifiedHost.Encrypted}\"");
            }

            if (OldHost.TimeoutMS != ModifiedHost.TimeoutMS)
            {
                any = true;
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.TimeoutMS),
                    Value: $"\"{OldHost.GetTimeoutString()}\" \u001a \"{ModifiedHost.GetTimeoutString()}\"");
            }

            if (OldHost.AuthToken != ModifiedHost.AuthToken)
            {
                any = true;
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.AuthToken),
                    Value: $"\"{OldHost.AuthToken}\" \u001a \"{ModifiedHost.AuthToken}\"");
            }

            if (OldHost.Enabled != ModifiedHost.Enabled)
            {
                any = true;
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.Enabled),
                    Value: $"\"{OldHost.Enabled}\" \u001a \"{ModifiedHost.Enabled}\"");
            }

            if (!any)
                sB.AppendLine().Append("No changes, this is an errored state to be in.");

            sB.AppendLine();
            sB.AppendLine().Append("New host:");
            sB.AppendLine().Append(ModifiedHost.GetHostNameWithProtocol())
                .AppendLine().AppendLine()
                .Append("Is this correct?");

            var confirmResult = await Popup.ShowYesNoCancelAsync(Event.FinalizeString(sB));

            switch (confirmResult)
            {
                case DialogResult.Yes:
                    return UIUtils.CascadableResult.Continue;
                case DialogResult.No:
                    return UIUtils.CascadableResult.BackSilent;
                case DialogResult.Cancel:
                default:
                    await ShowCancelledModifyHost();
                    return UIUtils.CascadableResult.CancelSilent;
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
                if (!host.GetServerStatus()
                    || host.GetSaveBonesInfos() is not IEnumerable<SaveBonesInfo> saveBonesInfosFromHost
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

        public static async Task<bool> TryReportBones(
            string BonesID,
            GameObject ReportedObject
            )
        {
            using var report = await AskForBonesReport(BonesID, ReportedObject);

            if (report == null)
                return false;

            try
            {
                bool any = false;
                foreach (var host in AllHosts(h => h.Enabled))
                {
                    try
                    {
                        if (await host.PostBonesReport(report))
                            any = true;
                        else
                            Utils.Warn($"Failed to upload bones report to {host}");
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to upload bones report to {host}", x);
                        continue;
                    }
                }
                if (any)
                    await Popup.ShowAsync("Thank you! Your report was successfully submitted to at least one host.");
                else
                    await Popup.ShowAsync("Unfortunately, none of your currently enabled hosts were able to receive your report.");

                return any;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(TryUploadBones)} failed to upload Bones with BonesID {BonesID}", x);
                return false;
            }
        }

        private static async Task<Report> AskForBonesReport(
            string BonesID,
            GameObject ReportedObject
            )
        {
            if (Report.ObjectReportDetails.FromGameObject(ReportedObject) is not Report.ObjectReportDetails reportObjectDetails)
            {
                await Popup.ShowAsync($"Something about {ReportedObject?.DebugName ?? "MISSING_OBJECT"} is making reporting from it unavailable.\n\n" +
                    $"Please consider making your report from another object loaded by the offending bones file.");
                return null;
            }

            PickOptionDataSetAsync<Report, UIUtils.CascadableResult> options = new();
            var report = new Report
            {
                OsseousAshID = Config.ID,
                BonesID = BonesID,
                ObjectDetails = reportObjectDetails,
                Description = "",
            };
            List<QudMenuItem> buttons = null;
            Dictionary<int, Func<Task<UIUtils.CascadableResult>>> buttonCallbacks = null;
            UIUtils.CascadableResult result;

            var sB = Event.NewStringBuilder();

            do
            {
                sB.Append("Please use the provided options to fill out your report about the bones that loaded the below object:")
                    .AppendLine()
                    .AppendLine().AppendBonesReportedObject(reportObjectDetails)
                    .AppendLine()
                    ;

                /*sB.AppendLine().Append("Once all the necessary information is present (missing highlighted ")
                    .AppendColored("red", "red")
                    .Append("), a submit button will appear.")
                    .AppendLine()
                    ;*/

                sB.AppendLine().Append("Below is what your current report contains:")
                    .AppendLine()
                    .AppendLine().AppendBonesReport(report, "{{W|Report}}")
                    //.AppendLine()
                    ;

                if (!report.IsValid)
                    sB.AppendLine().Append("Report is missing ")
                        .AppendColored("red", "necessary information")
                        .Append(", please add this info in order to proceed.")
                        ;

                string reportTypeString = report.Type.ToString();
                if (report.Type == Report.ReportTypes.None)
                    reportTypeString = reportTypeString.Colored("red");

                string enterDescriptionText = "Enter a Description";
                if (!report.Description.IsNullOrEmpty())
                    enterDescriptionText = "Modify Description";

                options.Add(new()
                {
                    Element = report,
                    Text = $"Report Type: {reportTypeString}",
                    Hotkey = 't',
                    Callback = element => Task.Run(async delegate ()
                    {
                        element.Type = await AskBonesReportType();
                        return UIUtils.CascadableResult.Continue;
                    }),
                });
                options.Add(new()
                {
                    Element = report,
                    Text = $"{report.IsSpecificObject.GetCheckboxText("For Specific Object")}",
                    Hotkey = 's',
                    Callback = element => Task.Run(delegate ()
                    {
                        if (element.IsSpecificObject)
                            element.ObjectDetails = null;
                        else
                            element.ObjectDetails = reportObjectDetails;
                        return UIUtils.CascadableResult.Continue;
                    }),
                });
                options.Add(new()
                {
                    Element = report,
                    Text = enterDescriptionText,
                    Hotkey = 'd',
                    Callback = element => Task.Run(async delegate ()
                    {
                        element.Description = (await Popup.AskStringAsync(
                                Message: "Please enter any additional details for your report that might assist in the review process:",
                                Default: element.Description ?? "",
                                ReturnNullForEscape: true,
                                AllowColorize: false))
                            ?? element.Description;

                        return UIUtils.CascadableResult.Continue;
                    }),
                });

                if (report.IsValid)
                {
                    buttons = new()
                    {
                        new QudMenuItem
                        {
                            text = PopupMessage.SubmitCancelButton[0].text,
                            command = "option:-3",
                            hotkey = PopupMessage.SubmitCancelButton[0].hotkey,
                        },
                    };
                    buttonCallbacks = new()
                    {
                        { -3, () => Task.Run(() => UIUtils.CascadableResult.BackSilent) }
                    };
                }

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: ReportBonesTitle,
                    Intro: sB.AppendLine().AppendLine().ToString(),
                    IntroIcon: ReportBonesIcon,
                    AdditionalButtons: buttons,
                    ButtonCallbacks: buttonCallbacks,
                    OnBackCallback: () => Task.Run(delegate ()
                    {
                        report.Dispose();
                        report = null;
                        return UIUtils.CascadableResult.Back;
                    }),
                    OnEscapeCallback: () => Task.Run(delegate ()
                    {
                        report.Dispose();
                        report = null;
                        return UIUtils.CascadableResult.Cancel;
                    }));

                sB.Clear();
                options.Clear();
            }
            while (result.IsContinue());

            Event.ResetTo(sB);

            if (report?.IsValid is not true)
            {
                if (!result.IsSilent())
                    await Popup.ShowAsync($"Report cancelled.");

                return null;
            }

            return report;
        }

        private static async Task<Report.ReportTypes> AskBonesReportType()
        {
            PickOptionDataSetAsync<Report.ReportTypes, Report.ReportTypes> options = new();

            var reportType = Report.ReportTypes.None;
            while (Enum.IsDefined(typeof(Report.ReportTypes), ++reportType))
            {
                string reportTypeText = reportType.ToString();
                if (reportType != Report.ReportTypes.Other)
                    reportTypeText = $"They are {reportType.ToString().ToLower()}";

                options.Add(new PickOptionDataAsync<Report.ReportTypes, Report.ReportTypes>
                {
                    Element = reportType,
                    Text = reportTypeText,
                    Hotkey = options.GetFirstAvailableHotkey(
                        new char[]
                        {
                            reportType.ToString().ToLower()[0],
                            reportType.ToString().ToUpper()[0],
                            'x',
                            'X'
                        }),
                    Callback = v => Task.Run(() => v),
                });
            }
            return await UIUtils.PerformPickOptionAsync(
                OptionDataSet: options,
                Title: ReportBonesTitle,
                Intro: $"What is it about these bones that you'd like to report?\n\n",
                IntroIcon: ReportBonesIcon,
                OnBackCallback: () => Task.Run(() => Report.ReportTypes.None),
                OnEscapeCallback: () => Task.Run(() => Report.ReportTypes.None));
        }
    }
}
