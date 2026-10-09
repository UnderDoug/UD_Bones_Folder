using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Platform.IO;

using ConsoleLib.Console;

using Qud.UI;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.World;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using static UD_Bones_Folder.Mod.Const;
using static XRL.World.Parts.UD_Bones_LunarRegentAnnouncer;
using UD_Bones_Folder.Mod.UI;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [JsonObject(MemberSerialization.OptOut)]
    [HasModSensitiveStaticCache]
    [Serializable]
    public class Configuration
    {
        public static string OsseousAshDirectoryName => "OsseousAsh";

        public static string ConfigFileName => $"{nameof(Configuration)}.json";

        public static string LocalPath => DataManager.SavePath(OsseousAshDirectoryName);
        public static string SyncedPath => DataManager.SyncedPath(OsseousAshDirectoryName);

        public static FileLocationData LocalFileLocation => FileLocationData.NewLocal(LocalPath);
        public static FileLocationData SyncedFileLocation => FileLocationData.NewSynced(SyncedPath);

        private static Configuration _Instance;
        public static Configuration Instance => _Instance ??= ReadOrNewAsync()?.WaitResult();

        public static string DefaultOsseousAshHandle => "{{K|A Mysterious Stranger}}";

        public const string OSSEOUS_ASH_UPLOADS = "{{yellow|uploads}}";
        public const string OSSEOUS_ASH_DOWNLOADS = "{{red|downloads}}";

        public static IRenderable BlackAshCloudIcon = new Renderable(
            Tile: "Mutations/gas_generation.bmp",
            ColorString: "&K",
            TileColor: "&K",
            DetailColor: 'y')
            ;

        public static IRenderable DownloadIcon = new Renderable(
            Tile: "Abilities/tile_supressive_fire.png",
            ColorString: "&y",
            TileColor: "&y",
            DetailColor: 'R')
            ;

        public static IRenderable UploadIcon = new FlippableRender(
            Source: new Renderable(
                Tile: "Abilities/tile_supressive_fire.png",
                ColorString: "&y",
                TileColor: "&y",
                DetailColor: 'W'),
            HFlip: false,
            VFlip: true)
            ;

        public static bool WantToAsk
            => Options.EnableOsseousAshStartupPopup
            && (Instance?.AskAtStartup is true)
            ;

        public Guid ID;
        public string Handle;

        [JsonIgnore]
        private bool _AskAtStartup;
            
        public bool AskAtStartup
        {
            get => _AskAtStartup;
            set
            {
                WriteAskAtStartup(value);
            }
        }

        [JsonIgnore]
        private bool _EnableDownloads;
            
        public bool EnableDownloads
        {
            get => _EnableDownloads;
            set
            {
                WriteEnableDownloads(value);
            }
        }

        [JsonIgnore]
        private bool _EnableUploads;
            
        public bool EnableUploads
        {
            get => _EnableUploads;
            set
            {
                WriteEnableUploads(value);
            }
        }

        public int? CustomPermyriadChance;

        [JsonIgnore]
        private HashSet<string> LockedMembers = new();

        [JsonIgnore]
        public FileLocationData LocationData;

        public Configuration()
        { }

        [ModSensitiveCacheInit]
        public static void EnsureConfiguration()
        {
            if (!VersionRoutes.Init)
                VersionRoutes.InitializeRoutes();

            _ = Instance;
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

        public static async Task<Configuration> ReadFromFileAsync(FileLocationData FileLocationData, string FileName)
            => await FileLocationData?.ReadFromFileAsync<Configuration>(FileName)
            ;

        public static async Task<Configuration> ReadAsync()
        {
            if (TryFindBestOsseousAshPath(out var fileLocationData, ConfigFileName))
                return await fileLocationData.ReadFromFileAsync<Configuration>(ConfigFileName);

            return null;
        }

        public static async Task<Configuration> ReadOrNewAsync()
        {
            if (TryFindBestOsseousAshPath(out var fileLocationData, ConfigFileName))
            {
                if ((await ReadFromFileAsync(fileLocationData, ConfigFileName)) is not Configuration configJSON)
                {
                    configJSON = new Configuration
                    {
                        ID = Guid.NewGuid(),
                        Handle = DefaultOsseousAshHandle,
                        AskAtStartup = true,
                        EnableDownloads = false,
                        EnableUploads = false,
                        CustomPermyriadChance = Options.DefaultPermyriadChance,
                        LocationData = fileLocationData,
                    };
                    Options.EnableOsseousAshStartupPopup = true;
                    Options.EnableOsseousAshDownloads = false;
                    Options.EnableOsseousAshUploads = false;
                }
                else
                {
                    Options.EnableOsseousAshStartupPopup = configJSON.AskAtStartup;
                    Options.EnableOsseousAshDownloads = configJSON.EnableDownloads;
                    Options.EnableOsseousAshUploads = configJSON.EnableUploads;
                }
                return configJSON;
            }
            return null;
        }

        public bool WriteUpdateField<T>(
            ref T Field,
            T Value,
            Func<T, T, bool> Predicate,
            Action<T, T> PostProc = null,
            Action<T, T> Catch = null,
            Action<T, T> AndFinally = null,
            string LockMember = null
            )
        {
            var fieldName = (new { Field })
                .GetType()
                .GetProperties()[0].Name;

            LockedMembers ??= new();

            if (!LockMember.IsNullOrEmpty()
                && LockedMembers.Contains(LockMember))
                return false;

            if (!LockMember.IsNullOrEmpty())
                LockedMembers.Add(LockMember);

            T originalFieldValue = Field;
            try
            {
                if (Predicate(Field, Value))
                {
                    Field = Value;
                    Write();

                    PostProc?.Invoke(originalFieldValue, Value);
                    return true;
                }
                return false;
            }
            catch (Exception x)
            {
                try
                {
                    Catch?.Invoke(originalFieldValue, Value);
                }
                catch (Exception xX)
                {
                    Utils.Error($"Failed to revert {fieldName} to {nameof(originalFieldValue)} and perform passed {nameof(Catch)} {nameof(Action)}", xX);
                }
                Utils.Error($"Failed to {nameof(WriteUpdateField)} {fieldName}", x);
                return false;
            }
            finally
            {
                if (!LockMember.IsNullOrEmpty())
                    LockedMembers.Remove(LockMember);

                AndFinally?.Invoke(originalFieldValue, Value);
            }
        }

        public void WriteToFile(FileLocationData FileLocationData, string FileName)
            => FileLocationData.Write(FileName, this, Formatting.Indented)
            ;

        public void Write()
        {
            if (LocationData != null
                || TryFindBestOsseousAshPath(out LocationData, ConfigFileName))
                WriteToFile(LocationData, ConfigFileName);
            else
                Utils.Error(
                    Context: $"Failed to {nameof(Write)} to {ConfigFileName}",
                    X: new NullReferenceException($"{nameof(LocationData)} must not be null"));
        }

        public void WriteAskAtStartup(bool Value, bool Propagate = true)
        {
            WriteUpdateField(
                Field: ref _AskAtStartup,
                Value: Value,
                Predicate: (f, v) => f != v,
                PostProc: (f, v)
                    => XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshStartupPopup)}",
                        Value: Options.EnableOsseousAshStartupPopup = v));
        }

        public void WriteEnableDownloads(bool Value, bool Propagate = true)
        {
            WriteUpdateField(
                Field: ref _EnableDownloads,
                Value: Value,
                Predicate: (f, v) => f != v,
                PostProc: (f, v)
                    => XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshDownloads)}",
                        Value: Options.EnableOsseousAshDownloads = v));
        }

        public void WriteEnableUploads(bool Value, bool Propagate = true)
        {
            WriteUpdateField(
                Field: ref _EnableUploads,
                Value: Value,
                Predicate: (f, v) => f != v,
                PostProc: (f, v)
                    => XRL.UI.Options.SetOption(
                        ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshUploads)}",
                        Value: Options.EnableOsseousAshUploads = v));
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
                this.ID = ID;
                Write();
            }
        }

        public void WriteCustomPermyriadChance(int CustomPermyriadChance)
        {
            if (this.CustomPermyriadChance != CustomPermyriadChance)
            {
                XRL.UI.Options.SetOption(
                    ID: $"{MOD_PREFIX}{nameof(Options.CustomPermyriadChance)}",
                    Value: (this.CustomPermyriadChance = CustomPermyriadChance).ToString());
                Write();
            }
        }

        public static IEnumerable<Host> AllHosts(Predicate<Host> Where)
        {
            foreach (var hostsCollection in ServerUtils.Hosts.IteratorSafe())
                foreach (var host in hostsCollection.IteratorSafe())
                    if (Where?.Invoke(host) is not false)
                        yield return host;
        }

        public static IEnumerable<Host> AllHosts()
            => AllHosts(null).IteratorSafe()
            ;

        public static IEnumerable<Host> AllEnabledHosts()
            => AllHosts(h => h.Enabled).IteratorSafe()
            ;

        public static async Task PerformAskAsync()
        {
            EnsureConfiguration();
            await AskOnStartup();
        }

        public static async Task AskOnStartup()
        {
            if (GameManager.AwakeComplete
                || MainMenuBones.AllowPromptAboutLinux)
            {
                if (WantToAsk)
                {
                    var sB = Event.NewStringBuilder();
                    Dictionary<int, Func<Task<UIUtils.CascadableResult>>> buttonCallbacks = null;
                    try
                    {
                        using var options = new CascadablePickOptionDataSetAsync<object>(UIUtils.CascadableResult.Continue, UIUtils.CascadableResult.CancelSilent);
                        var result = UIUtils.CascadableResult.Continue;

                        bool originalEnableDownloads = Options.EnableOsseousAshDownloads;
                        bool originalEnableUploads = Options.EnableOsseousAshUploads;
                        bool enableDownloads = originalEnableDownloads;
                        bool enableUploads = originalEnableUploads;
                        int defaultSelected = 0;
                        do
                        {
                            sB.Clear();
                            options.ClearItems(Dispose: true);

                            sB.Append("Welcome to the ").Append(Utils.ThisMod?.DisplayTitle ?? "Bones(alpha)").Append(" mod!")
                            .AppendLine()
                            .AppendLine().Append("We've detected that this is the first time you've launched the game with this mod ")
                                .Append("installed and wanted to let you know about the ").Append(OSSEOUS_ASH).Append(" community bones cloud.")
                            .AppendLine()
                            .AppendLine().Append("Participation in the project is entirely optional but could greatly ")
                                .Append("enhance your experience with the Bones Folder mod:")
                            .AppendLine()
                            .AppendLine().AppendBullet("red").Append("Opting in to ").AppendQuote(OSSEOUS_ASH_DOWNLOADS)
                                .Append(" will include bones files from the ").Append(OSSEOUS_ASH).Append(" when looking for bones files to load.")
                            .AppendLine().AppendBullet("yellow").Append("Opting in to ").AppendQuote(OSSEOUS_ASH_UPLOADS)
                                .Append(" will upload to the ").Append(OSSEOUS_ASH).Append(", any bones files saved while the option is enabled, ")
                                .Append("and, if you choose one, will associate a handle with the uploaded bones.")
                            .AppendLine()
                            .AppendLine().Append("Irrespective of your choice, you won't be asked again, ")
                                .Append("and you can always change your decision later in the options menu.")
                            .AppendLine()
                            .AppendLine().Append("Please note: the ").Append(OSSEOUS_ASH).Append(" community bones cloud ")
                                .Append("requires an internet connection to function.")
                            .AppendLine()
                            .AppendLine().AppendColored("w", "This feature is currently in closed beta, and typically requires being whitelisted to participate.")
                            .AppendLine()
                            .AppendLine().Append("Feel free to opt-in now so that, when testing goes public, you're already opted in, ")
                                .Append("but be aware that you'll be unable to communicate with the server until then.")
                            .AppendLineEnd();

                            options.Add(new CascadablePickOptionDataAsync<object>
                            {
                                Element = null,
                                Text = enableDownloads.GetCheckboxText($"Opt in to {OSSEOUS_ASH} {OSSEOUS_ASH_DOWNLOADS}"),
                                Icon = DownloadIcon,
                                Hotkey = 'd',
                                Callback = e => Task.Run(delegate ()
                                {
                                    defaultSelected = options.IndexOf(options.FirstOrDefault(o => o.Hotkey == 'd'));
                                    enableDownloads = !enableDownloads;
                                    return UIUtils.CascadableResult.Continue;
                                }),
                            });
                            options.Add(new CascadablePickOptionDataAsync<object>
                            {
                                Element = null,
                                Text = enableUploads.GetCheckboxText($"Opt in to {OSSEOUS_ASH} {OSSEOUS_ASH_UPLOADS}"),
                                Icon = UploadIcon,
                                Hotkey = 'u',
                                Callback = e => Task.Run(delegate ()
                                {
                                    defaultSelected = options.IndexOf(options.FirstOrDefault(o => o.Hotkey == 'u'));
                                    enableUploads = !enableUploads;
                                    return UIUtils.CascadableResult.Continue;
                                }),
                            });
                            options.Add(new CascadablePickOptionDataAsync<object>
                            {
                                Element = null,
                                Text = $"Manage {OSSEOUS_ASH} Handle",
                                Icon = new Renderable(
                                    Tile: "Items/sw_credit_wedge.bmp",
                                    ColorString: "&y",
                                    TileColor: "&y",
                                    DetailColor: 'K'),
                                Hotkey = 'h',
                                Callback = async delegate (object e)
                                {
                                    defaultSelected = options.IndexOf(options.FirstOrDefault(o => o.Hotkey == 'u'));
                                    await Options.ManageOsseousAshHandleAsync();
                                    return UIUtils.CascadableResult.Continue;
                                },
                            });

                            List<QudMenuItem> buttons = null;
                            buttonCallbacks?.Clear();
                            buttonCallbacks = null;
                            if ((originalEnableDownloads != enableDownloads)
                                || (originalEnableUploads != enableUploads))
                            {
                                buttons = UIUtils.ConfirmButton;
                                buttonCallbacks = new()
                            {
                                { -4, () => Task.Run(() => UIUtils.CascadableResult.BackSilent) }
                            };
                            }

                            result = await UIUtils.PerformPickOptionAsync(
                                OptionDataSet: options,
                                Title: $"New Bones Folder Mod Installation Detected!".Colored("yellow"),
                                Intro: sB.ToString(),
                                IntroIcon: BlackAshCloudIcon,
                                AdditionalButtons: buttons,
                                NoBackButton: true,
                                DefaultSelected: Math.Clamp(defaultSelected, 0, options.Count - 1),
                                ButtonCallbacks: buttonCallbacks,
                                FinalSelectedCallback: async delegate (PickOptionData<object, Task<UIUtils.CascadableResult>> o, Task<UIUtils.CascadableResult> r)
                                {
                                    var result = (await r?.AwaitResultIfNotIsCompletedSuccessfully());
                                    if (result.IsBack())
                                    {
                                        await PerformOptInAsync(
                                            enableDownloads: enableDownloads,
                                            originalEnableDownloads: originalEnableDownloads,
                                            enableUploads: enableUploads,
                                            originalEnableUploads: originalEnableUploads);
                                    }
                                    else
                                    if (result.IsCancel())
                                    {
                                        await Popup.ShowAsync(Event.FinalizeString(
                                            SB: Event.NewStringBuilder()
                                                .Append("You haven't been opted in.")
                                                .AppendLine()
                                                .AppendLine().Append("If you'd like to change your mind at any time, ")
                                                .Append("there are options available in the options menu."))
                                            );
                                    }
                                    return await UIUtils.ShowEscancellepedAsync(o, r);
                                });

                        }
                        while (result.IsContinue());

                        Options.EnableOsseousAshStartupPopup = false;
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to ask player about Osseous Ash", x);
                        Options.EnableOsseousAshStartupPopup = true;
                    }
                    finally
                    {
                        Event.ResetTo(sB);
                    }
                }
            }
        }

        private static async Task PerformOptInAsync(
            bool enableDownloads,
            bool originalEnableDownloads,
            bool enableUploads,
            bool originalEnableUploads
            )
        {
            if (enableDownloads != originalEnableDownloads)
            {
                XRL.UI.Options.SetOption(
                    ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshDownloads)}",
                    Value: Options.EnableOsseousAshDownloads = enableDownloads);
            }

            if (enableUploads != originalEnableUploads)
            {
                XRL.UI.Options.SetOption(
                    ID: $"{MOD_PREFIX}{nameof(Options.EnableOsseousAshUploads)}",
                    Value: Options.EnableOsseousAshUploads = enableUploads);
            }

            if (Options.EnableOsseousAshDownloads
                || Options.EnableOsseousAshUploads)
            {
                if (ServerUtils.Hosts.FirstWithHostMatching(h => h.SameAs(Host.DefaultHost, IgnoreDisabled: true)) is HostSet defaultHostCollection
                    && defaultHostCollection.FirstOrDefault(h => h.SameAs(Host.DefaultHost, IgnoreDisabled: true)) is Host defaultHost)
                {
                    defaultHost.Enabled = false;
                    defaultHostCollection.Write();
                }
            }

            if (ServerUtils.Hosts.FirstHostMatching(h => h.Enabled) is null)
            {
                var sB = Event.NewStringBuilder()
                    .Append("It appears that none of your hosts are currently enabled.")
                    .AppendLine()
                    .AppendLine().Append("You have ").Append(ServerUtils.Hosts.TotalCount().Things("host")).Append(" currently saved.")
                    .AppendLine()
                    .AppendLine().Append("There is a button in the options menu to configure them.")
                    .AppendLine()
                    .AppendLine().Append("Alternatively, would you like to configure them now?");

                if ((await Popup.NewPopupMessageAsync(
                    message: sB.ToString(),
                    buttons: PopupMessage.YesNoButton,
                    DefaultSelected: 1,
                    title: $"Configure {OSSEOUS_ASH} Hosts?",
                    afterRender: new FlippableRender(
                        Source: new Renderable(
                            Tile: "Abilities/tile_supressive_fire.png",
                            ColorString: "&y",
                            TileColor: "&y",
                            DetailColor: 'W'),
                        HFlip: false,
                        VFlip: true))).command == "Yes")
                {
                    await Host.ManageHostsOptionButtonWithSelectionAsync(null);
                }
            }
        }
    }
}
