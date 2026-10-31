using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Platform.IO;
using StreamWriter = System.IO.StreamWriter;
using StreamReader = System.IO.StreamReader;

using ConsoleLib.Console;

using Qud.UI;

using XRL;
using XRL.Collections;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;
using XRL.World.Parts;
using XRL.World.Text;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using Event = XRL.World.Event;
using Stream = System.IO.Stream;

using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.UI;
using UD_Bones_Folder.Mod.Serialization;

using static UD_Bones_Folder.Mod.Const;
using static UD_Bones_Folder.Mod.OsseousAsh.HostSet;
using System.Runtime.CompilerServices;
using UD_Bones_Folder.Mod.BonesSystem.BonesSource;
using UD_Bones_Folder.Mod.Logging;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [HasModSensitiveStaticCache]
    [JsonObject(MemberSerialization.OptIn
        , ItemReferenceLoopHandling = ReferenceLoopHandling.Ignore
        )]
    [Serializable]
    public class Host : BaseBonesSource, IComposite, IEquatable<Host>, IVomitable<Host>, IDisposable
    {
        #region Debug Registration
        [RegistersMethodsForDebug]
        public static void doDebugRegistry(DebugMethodRegistry Registry)
            => Registry.RegisterEach(
                Type: typeof(Host),
                MethodNameValues: new()
                {
                    { nameof(GetServerStatusAsync), true },
                    { nameof(GetCanUpAsync), true },
                    { nameof(GetSessionAsync), true },
                    { nameof(GetUserAsync), true },
                    { nameof(CheckRegisterUserAsync), true },
                });
        #endregion

        [Serializable]
        public enum WebMethods
        {
            Unsupported,
            POST,
            GET,
            PUT,
            DELETE,
        }

        [Serializable]
        public enum ConnectionLevels
        {
            None,
            Disabled,
            NotResponding,
            DownloadOnly,
            Running,
        }

        public static Host DefaultHost => new Host
        {
            Name = "osseousash.cloud",
            Port = null,
            Encrypted = true,
            TimeoutMS = DEFAULT_TIMEOUT,
            MaxUploadAttempts = DEFAULT_MAX_UP_ATTEMPTS,
            Version = DEFAULT_VERSION,
            Enabled = false,
        };

        public const int DEFAULT_VERSION = 1;
        public const int DEFAULT_TIMEOUT = 3000;
        public const int DEFAULT_MAX_UP_ATTEMPTS = 5;
        public const int DEFAULT_MAX_SAV_GZ_TOKEN_LIFE = 180000;

        public const string JWT_HEADER = "jwt_token_header";
        public const string OBFS_PASSWORD = "********";
        public static long SeverStatusCheckInterval => 300000;

        private static int CurrentHostID = 0;

        public static Dictionary<string, Dictionary<string, PendingUpload>> PendingUploadCache = new();

        public static string status => nameof(status);
        public static string canUp => nameof(canUp);
        public static string Bones => nameof(Bones);
        public static string ID => CombineRoute(Bones, nameof(ID));
        public static string IDs => CombineRoute(Bones, nameof(IDs));
        public static string Info => CombineRoute(Bones, nameof(Info));
        public static string Infos => CombineRoute(Bones, nameof(Infos));
        public static string Spec => CombineRoute(Bones, nameof(Spec));
        public static string Specs => CombineRoute(Bones, nameof(Specs));
        public static string SavGz => CombineRoute(Bones, nameof(SavGz));
        public static string Stats => CombineRoute(Bones, nameof(Stats));
        public static string Download => CombineRoute(Bones, nameof(Download));
        public static string Report => nameof(Report);
        public static string Reports => nameof(Reports);
        public static string ReportCheck => CombineRoute(Report, "Check");

        public static Guid UserID => Configuration.Instance?.ID ?? Guid.Empty;

        [NoVomit]
        public bool OptIn => true;

        [JsonIgnore]
        private int? _HostID;
        [DoVomit]
        [JsonIgnore]
        private int HostID => _HostID ??= CurrentHostID++;

        /// <summary>
        /// Name of the host, excluding protocol and any port number.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public string Name;

        /// <summary>
        /// Optional port number for host.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public int? Port;

        /// <summary>
        /// Determines http or https.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public bool Encrypted;

        /// <summary>
        /// The number of milliseconds to wait for a response.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public int TimeoutMS = DEFAULT_TIMEOUT;

        /// <summary>
        /// The maximum number number of times an upload should be attempted before being considered to have failed.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public int MaxUploadAttempts = DEFAULT_MAX_UP_ATTEMPTS;

        /// <summary>
        /// The API version this host expects to connect to.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public int Version;

        /// <summary>
        /// The Routes made available by the API this host will connect to.
        /// </summary>
        [DoVomit]
        [JsonIgnore]
        public RouteSet Routes => VersionRoutes.GetFor(Version);

        /// <summary>
        /// Indicates whether or not this host should be utilised.
        /// </summary>
        [DoVomit]
        [JsonProperty]
        public bool Enabled;

        [DoVomit(nameof(JwtToken))]
        [JsonProperty(nameof(JwtToken))]
        private string _JwtToken;
        /// <summary>
        /// The JwtToken holding the current authentication session with this host. 
        /// </summary>
        /// <remarks>
        /// Default behaviour refreshes the token every time it's used in order to reduce the frequency at which a new session needs to be established.
        /// </remarks>
        [JsonIgnore]
        public string JwtToken
        {
            get => _JwtToken;
            set
            {
                bool write = IsBuilt && _JwtToken != value;
                _JwtToken = value;
                if (write
                    && this.TryGetHostSet(out var hostSet))
                    hostSet.Write();
            }
        }

        /// <summary>
        /// An object representation of the User as it appears on the server.
        /// </summary>
        [DoVomit]
        //[JsonIgnore]
        [JsonConverter(typeof(User.JsonConverter))]
        [JsonProperty]
        public User User;

        [DoVomit]
        [JsonIgnore]
        private bool IsBuilt;

        [DoVomit]
        [JsonIgnore]
        public bool HasSession => !JwtToken.IsNullOrEmpty();

        [JsonIgnore]
        private string BuiltString => !IsBuilt
            ? " Unbuilt"
            : null
            ;

        [DoVomit]
        [JsonIgnore]
        private bool? WrittenEnabled;

        [DoVomit]
        [JsonIgnore]
        public bool IsOnCooldown => IsBuilt
            && !Enabled
            && WrittenEnabled.HasValue
            && WrittenEnabled.GetValueOrDefault()
            ;

        [DoVomit]
        [JsonIgnore]
        public bool IsRunning
            => VersionRoutes.Init
            && GetServerStatus()
            ;

        [DoVomit]
        [JsonIgnore]
        public bool CanUp => GetCanUp();

        [JsonIgnore]
        public ConnectionLevels ConnectionLevel
        {
            get
            {
                if (!Enabled)
                    return ConnectionLevels.Disabled;

                if (!IsRunning)
                    return ConnectionLevels.NotResponding;

                if (!CanUp)
                    return ConnectionLevels.DownloadOnly;

                return ConnectionLevels.Running;
            }
        }

        [JsonIgnore]
        public string ConnectionColor => GetConnectionColor(ConnectionLevel);

        [JsonIgnore]
        public string ServerStatusString
        {
            get
            {
                var connectionLevel = ConnectionLevel;
                return GetConnectionString(connectionLevel).Colored(GetConnectionColor(connectionLevel));
            }
        }

        [JsonIgnore]
        private Timer StatusCheckTimer;
        [JsonIgnore]
        private TimerCallback StatusCheckCallback => delegate (object state)
        {
            if (!IsBuilt)
                return;

            if (!WrittenEnabled.HasValue)
            {
                Utils.InfoTimestamp($"[{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)}: " +
                    $"{GetHostNameWithProtocol()} lacks a {nameof(WrittenEnabled)} value - Clearing timer");
                ClearStatusCheckTimer(ref StatusCheckTimer, Indent: 1);
                return;
            }

            if (!Enabled
                && WrittenEnabled.GetValueOrDefault())
            {
                Enabled = true;
                try
                {
                    if (IsRunning)
                    {
                        WrittenEnabled = null;
                        // ClearReportsCache();
                        Utils.InfoTimestamp($"[{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)}: " +
                            $"Connection re-established to {GetHostNameWithProtocol()} - Re-enabled");
                        ClearStatusCheckTimer(ref StatusCheckTimer, Indent: 1);
                    }
                    else
                        Enabled = false;
                }
                catch (Exception x)
                {
                    Utils.ErrorTimestamp($"[{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)} Checking Status", x);
                    Enabled = false;
                }
            }
            else
            {
                Utils.InfoTimestamp($"[{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)}: " +
                    $"{GetHostNameWithProtocol()} is already enabled, or {nameof(WrittenEnabled)} is false - Clearing timer");
                ClearStatusCheckTimer(ref StatusCheckTimer, Indent: 1);
            }
        };

        [JsonIgnore]
        private List<string> BonesIDCache;

        [JsonIgnore]
        private Timer BonesIDCacheTimer;

        [JsonIgnore]
        public string AuthToken;

        [JsonIgnore]
        private IEnumerable<Report> _ReportsCache;
        [JsonIgnore]
        public IEnumerable<Report> ReportsCache => _ReportsCache ??= GetBonesReports();

        [JsonIgnore]
        private string PendingUploadCacheKey;

        [JsonIgnore]
        public Dictionary<string, PendingUpload> PendingUploads
        {
            get
            {
                if (!PendingUploadCache.ContainsKey(PendingUploadCacheKey))
                    PendingUploadCache[PendingUploadCacheKey] = new();

                return PendingUploadCache[PendingUploadCacheKey];
            }
        }

        public sealed override IBonesSource.SourceType Type => IBonesSource.SourceType.Online;

        public override string SourceID => NameWithPort(Name, Port);

        public override string DisplayName => $"{Type.Tag()} {SourceID}";

        public override bool CanLoad
            => Options.EnableOsseousAshDownloads
            && IsRunning
            ;

        public override bool CanSave
            => Options.EnableOsseousAshUploads
            && IsRunning
            && CanUp
            ;

        public override string BlurbDisplayName => $"{SourceID.Colored(IBonesSource.SourceType.Online.GetColor())}";
        public override string BlurbSavedVerb => "uploaded to";

        public Host()
            // : base()
        {
            Enabled = true;
            PendingUploadCacheKey = Guid.NewGuid().ToString();
            TimeoutMS = DEFAULT_TIMEOUT;
        }

        public Host(
            string Name,
            int? Port = null,
            bool Encrypted = false,
            string JwtToken = null,
            int Version = DEFAULT_VERSION,
            bool Enabled = true,
            int TimeoutMS = DEFAULT_TIMEOUT
            )
            : this()
        {
            this.Name = Name;
            this.Port = Port;
            this.Encrypted = Encrypted;
            this.JwtToken = JwtToken;
            this.Version = Version;
            this.Enabled = Enabled;
            this.TimeoutMS = TimeoutMS;
        }

        public Host(
            string HostName,
            string JwtToken = null,
            int TimeoutMS = DEFAULT_TIMEOUT
            )
            : this()
        {
            Parse(HostName, out Name, out Port, out Encrypted, out Version);
            if (!JwtToken.IsNullOrEmpty())
                this.JwtToken = JwtToken;

            this.TimeoutMS = TimeoutMS;
        }

        #region Timeout Handling

        private static void ClearStatusCheckTimer(ref Timer StatusCheckTimer, int Indent = 0)
        {
            if (StatusCheckTimer is not null)
            {
                if (Indent == 0)
                    Utils.Info($"Cleared {nameof(StatusCheckTimer)}");
                else
                    Utils.Log($"{Indent.Indent()}Cleared {nameof(StatusCheckTimer)}");
            }

            StatusCheckTimer?.Dispose();
            StatusCheckTimer = null;
        }

        public void ManuallyClearStatusCheckTimer(string ReasonForOverride = "manually overridden")
        {
            if (!WrittenEnabled.HasValue)
            {
                Utils.InfoTimestamp($"[{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)}: {GetHostNameWithProtocol()} lacks a {nameof(WrittenEnabled)} value - Clearing timer");
                ClearStatusCheckTimer(ref StatusCheckTimer, Indent: 1);
                return;
            }

            if (!Enabled
                && WrittenEnabled.GetValueOrDefault())
            {
                Enabled = true;
                try
                {
                    if (IsRunning)
                    {
                        WrittenEnabled = null;
                        Utils.InfoTimestamp($"[{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)}: {ReasonForOverride}");
                        ClearStatusCheckTimer(ref StatusCheckTimer, Indent: 1);
                    }
                    else
                        Enabled = false;
                }
                catch (Exception x)
                {
                    Utils.ErrorTimestamp($"[{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)} Checking Status", x);
                    Enabled = false;
                }
            }
            else
            {
                Utils.InfoTimestamp($"[{nameof(HostID)}: {HostID}] {nameof(StatusCheckCallback)}: " +
                    $"{GetHostNameWithProtocol()} is already enabled, or {nameof(WrittenEnabled)} is false - Clearing timer");
                ClearStatusCheckTimer(ref StatusCheckTimer, Indent: 1);
            }
        }

        private static void SetupStatusCheckTimer(ref Timer StatusCheckTimer, Host Host, int Indent = 0)
        {
            if (!Host.IsBuilt)
            {
                ClearStatusCheckTimer(ref StatusCheckTimer, Indent);
                return;
            }

            if (StatusCheckTimer is null)
            {
                Host.WrittenEnabled = Host.Enabled;
                Host.Enabled = false;
                StatusCheckTimer = new Timer(Host.StatusCheckCallback, null, SeverStatusCheckInterval, SeverStatusCheckInterval);

                string successMessage = $"Timer for {Host.GetHostNameWithProtocol()} set up successfully.";
                if (Indent == 0)
                    Utils.InfoTimestamp($"[{nameof(HostID)}: {Host.HostID}] {nameof(SetupStatusCheckTimer)}: {successMessage}");
                else
                    Utils.Log($"{Indent.Indent()}{successMessage}");
            }
            else
            {
                string failureMessage = $"Timer for {Host.GetHostNameWithProtocol()} already exists.";
                if (Indent == 0)
                    Utils.InfoTimestamp($"[{nameof(HostID)}: {Host.HostID}] {nameof(SetupStatusCheckTimer)}: {failureMessage}");
                /*else
                    Utils.Log($"{Indent.Indent()}{failureMessage}");*/
            }
        }

        private bool HandleTimeoutWebException(WebException X, string URI, int? Timeout)
        {
            if (X.Status == WebExceptionStatus.Timeout)
            {
                string inner = null;
                if (X.InnerException is Exception iX)
                    inner = $" Inner: {iX}";
                Utils.InfoTimestamp($"[{nameof(HostID)}: {HostID}{BuiltString}] Timed out getting status from {URI} ({GetTimeoutString(Timeout)}).{inner}");
                if (IsBuilt)
                    SetupStatusCheckTimer(ref StatusCheckTimer, this, Indent: 1);

                return true;
            }
            return false;
        }

        private static void SetUpBonesIDCacheTimer(ref Timer BonesIDCacheTimer, ref List<string> BonesIDCache, TimerCallback TimerCallback)
        {
            ClearBonesIDCacheTimer(ref BonesIDCacheTimer);
            BonesIDCacheTimer = new Timer(
                callback: TimerCallback,
                state: BonesIDCache,
                dueTime: 300000,
                period: 300000);
        }

        private static void ClearBonesIDCache(ref List<string> BonesIDCache)
        {
            BonesIDCache ??= new();
            BonesIDCache.Clear();
        }

        private static void ClearBonesIDCacheTimer(ref Timer BonesIDCacheTimer, int Indent = 0)
        {
            BonesIDCacheTimer?.Dispose();
            BonesIDCacheTimer = null;
        }

        public void ClearBonesIDCache()
            => ClearBonesIDCache(ref BonesIDCache)
            ;

        #endregion

        public void ClearReportsCache()
        {
            // OsseousAsh.ClearReportsCache();
            _ReportsCache = null;
        }

        public static void Parse(string HostName, out string Name, out int? Port, out bool Encrypted, out int Version)
        {
            if (HostName.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(HostName));

            Port = null;
            Encrypted = false;
            Version = DEFAULT_VERSION;

            Name = HostName;
            if (Name.StartsWith("http"))
            {
                Name = Name[4..];
                Encrypted = Name[0] == 's';
                if (Encrypted)
                    Name = Name[1..];
            }
            if (Name.StartsWith(":"))
                Name = Name[1..];

            while (Name.StartsWith('/'))
                Name = Name[1..];

            string baseRoute = null;
            if (Name.TrySplitOut(":", out Name, out string port))
            {
                if (port.IndexOf("/") is int slashIndex
                    && slashIndex >= 0)
                {
                    baseRoute = port[^slashIndex..];
                    port = port[..slashIndex];
                }

                if (!port.IsNullOrEmpty())
                {
                    if (int.TryParse(port, out int portInt))
                        Port = portInt;
                    else
                        Utils.Warn($"Failed to parse {nameof(port)} {port} to {typeof(int).Name}");
                }
            }
            if (baseRoute == null
                && Name.TrySplitOut("/", out Name, out baseRoute))
            {
                if (baseRoute.Split("/") is string[] splitRout)
                {
                    if (splitRout.Any(s => s.EqualsNoCase("api")))
                    {
                        Version = -1;
                        foreach (var routePart in splitRout)
                        {
                            if (routePart.StartsWith("v")
                                && routePart.Length > 1)
                            {
                                if (int.TryParse(routePart[1..], out Version))
                                    break;
                                else
                                    Utils.Warn($"Failed to parse {nameof(routePart)} {routePart} to {typeof(int).Name} for {nameof(Version)}");
                            }
                        }
                    }
                }
                else
                if (baseRoute.Contains("api"))
                {
                    baseRoute = baseRoute.Replace("api", "");
                    if (baseRoute.StartsWith("v")
                        && baseRoute.Length > 1)
                    {
                        if (!int.TryParse(baseRoute[1..], out Version))
                        {
                            Version = -1;
                            Utils.Warn($"Failed to parse {nameof(baseRoute)}[1..] {baseRoute[1..]} to {typeof(int).Name} for {nameof(Version)}");
                        }
                    }
                }
            }
            if (Name.EndsWith("/"))
                Name = Name[..^1];
        }

        public static bool TryParse(string HostName, out Host Host)
        {
            Host = null;
            try
            {
                Parse(HostName, out string name, out int? port, out bool encrypted, out int version);
                Host = new Host
                {
                    Name = name,
                    Port = port,
                    Encrypted = encrypted,
                    Version = version,
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void HotSwapEnabledWhile(Action Action)
        {
            bool enabled = Enabled;
            if (WrittenEnabled.HasValue)
                Enabled = WrittenEnabled.GetValueOrDefault();

            Action?.Invoke();

            Enabled = enabled;
        }

        public async Task HotSwapEnabledWhileAsync(Task Task)
        {
            bool enabled = Enabled;
            if (WrittenEnabled.HasValue)
                Enabled = WrittenEnabled.GetValueOrDefault();

            await Task;

            Enabled = enabled;
        }

        public void HotSwapEnabled(ref Dictionary<Host, bool?> Enableds)
        {
            Enableds ??= new();
            if (!Enableds.TryGetValue(this, out bool? enabled))
                enabled = (Enableds[this] = null);

            if (enabled.HasValue)
            {
                Enabled = enabled.GetValueOrDefault();
                Enableds[this] = null;
                return;
            }

            Enableds[this] = Enabled;
            if (WrittenEnabled.HasValue)
                Enabled = WrittenEnabled.GetValueOrDefault();
        }

        public string GetEnabledCheckbox()
            => WrittenEnabled.HasValue
            ? WrittenEnabled.GetValueOrDefault().GetCheckboxText(nameof(Enabled), "K")
            : Enabled.GetCheckboxText(nameof(Enabled))
            ;

        public bool GetEnabledValueForMenu()
            => WrittenEnabled
            ?? Enabled
            ;

        public static Host Clone(Host Host, bool? SetBuiltTo = null)
        {
            if (Host == null)
                throw new ArgumentNullException(nameof(Host));

            var newHost = new Host();

            Host.CopyTo(ref newHost, SetBuiltTo);
            return newHost;
        }

        public Host Clone(bool? SetBuiltTo = null)
            => Clone(this, SetBuiltTo)
            ;

        public static void CopyTo(
            Host Source,
            ref Host Destination,
            bool? SetBuiltTo = null,
            bool DisposeSource = false
            )
        {
            if (Source == null)
                return;

            if (Destination == null)
            {
                Destination = Source.Clone();
                return;
            }

            Destination.PendingUploadCacheKey = Source.PendingUploadCacheKey;

            Destination.Name = Source.Name;
            Destination.Port = Source.Port;
            Destination.Encrypted = Source.Encrypted;
            Destination.TimeoutMS = Source.TimeoutMS;
            Destination.Enabled = Source.Enabled;
            Destination.JwtToken = Source.JwtToken;
            Destination.User = Source.User?.Clone();

            Destination.IsBuilt = Source.IsBuilt;

            if (SetBuiltTo.HasValue)
            {
                if (SetBuiltTo.GetValueOrDefault())
                    Destination.Build(Ping: false);
                else
                    Destination.Unbuild();
            }

            if (Destination.WrittenEnabled.HasValue)
                Destination.WrittenEnabled = Source.Enabled;

            if (Source.StatusCheckTimer is not null)
            {
                Destination.Enabled = Source.WrittenEnabled.GetValueOrDefault();
                ClearStatusCheckTimer(ref Destination.StatusCheckTimer);
                SetupStatusCheckTimer(ref Destination.StatusCheckTimer, Destination);
            }

            Destination.AuthToken = Source.AuthToken;

            if (DisposeSource)
                Source.Dispose();
        }

        public void CopyTo(
            ref Host Destination,
            bool? SetBuiltTo = null,
            bool ThenDispose = false
            )
            => CopyTo(this, ref Destination, SetBuiltTo, ThenDispose)
            ;

        public void CopyTo(
            Host Destination,
            bool? SetBuiltTo = null,
            bool ThenDispose = false
            )
            => CopyTo(this, ref Destination, SetBuiltTo, ThenDispose)
            ;

        public void Unbuild()
        {
            IsBuilt = false;
            ClearStatusCheckTimer(ref StatusCheckTimer);
            ClearBonesIDCacheTimer(ref BonesIDCacheTimer);
            ClearBonesIDCache(ref BonesIDCache);
        }

        public void Build(bool Ping = false)
        {
            IsBuilt = true;
            if (Ping)
            {
                _ = IsRunning;
                ClearReportsCache();
            }
            ClearBonesIDCache(ref BonesIDCache);
            SetUpBonesIDCacheTimer(
                BonesIDCacheTimer: ref BonesIDCacheTimer,
                BonesIDCache: ref BonesIDCache,
                TimerCallback: delegate (object state)
                {
                    if (state is List<string> bonesIDCache)
                    {
                        ClearBonesIDCache(ref bonesIDCache);
                        BonesIDCache = bonesIDCache;
                    }
                });
        }

        public int? GetTimeout()
            => TimeoutMS >= 0
            ? TimeoutMS
            : null
            ;

        public static string GetTimeoutString(int? Timeout)
            => Timeout is int timeout
            ? $"{timeout} ms"
            : $"\u00ec ms"
            ;

        public string GetTimeoutString()
            => GetTimeoutString(GetTimeout())
            ;

        public static string GetConnectionColor(ConnectionLevels ConnectionLevel)
        => ConnectionLevel switch
        {
            ConnectionLevels.Running => "green",
            ConnectionLevels.DownloadOnly => "yellow",
            ConnectionLevels.NotResponding => "red",
            ConnectionLevels.Disabled => "black",
            _ => "M",
        }
        ;

        public string GetConnectionColor()
            => GetConnectionColor(ConnectionLevel)
            ;

        public static string GetConnectionString(ConnectionLevels ConnectionLevel)
            => ConnectionLevel switch
            {
                ConnectionLevels.Running => "Running",
                ConnectionLevels.DownloadOnly => "Download Only",
                ConnectionLevels.NotResponding => "Not Responding",
                ConnectionLevels.Disabled => "Disabled",
                _ => "Error",
            }
            ;

        public string GetConnectionString()
            => GetConnectionString(ConnectionLevel)
            ;

        public static string GetConnectionSymbol(ConnectionLevels ConnectionLevel)
            => DF.Colored(GetConnectionColor(ConnectionLevel)) // DF ☼ | \u0017 ↨
            ;

        public string GetEnableDisableUIText()
        {
            string notColor = "K";
            string color = "W";

            string enabledText = "Enabled";
            string disabledText = "Disabled";

            if (Enabled)
            {
                enabledText = enabledText.Colored(color);
                disabledText = disabledText.Colored(notColor);
            }
            else
            {
                enabledText = enabledText.Colored(notColor);
                disabledText = disabledText.Colored(color);
            }
            return $"Toggle: {enabledText}/{disabledText}";
        }

        public static string TrimSlashes(string Path)
        {
            if (Path?.StartsWith('/') is true)
                Path = Path[1..];

            if (Path?.EndsWith('/') is true)
                Path = Path[..^1];

            return Path;
        }

        public static string SlashTrimmedSlashDelimitedAggregator(string Accumulator, string Next)
            => Utils.DelimitedNotNullAggregator(Accumulator, TrimSlashes(Next), "/")
            ;

        public static string CombineRoute(params string[] Routes)
            => Routes?.Aggregate("", SlashTrimmedSlashDelimitedAggregator)
            ;

        string CombineHostRoute(bool IncludeVersion, params string[] Route)
            => CombineRoute(GetHostNameWithProtocol(IncludeAPI: false), GetAPIVersionStub(IncludeVersion), CombineRoute(Route))
            ;

        string CombineHostRoute(params string[] Route)
            => CombineHostRoute(true, Route)
            ;

        public static string NameWithPort(string HostName, int? Port = null)
        {
            string nameWithPort = HostName ?? "MISSING_NAME";

            if (nameWithPort.EndsWith("/"))
                nameWithPort = nameWithPort[..^1];

            if (Port.HasValue)
                nameWithPort += $":{Port}";

            return nameWithPort;
        }

        public static string GetProtocol(
            string HostName = null,
            bool Encrypted = false,
            bool TrailingSlash = true,
            bool IncludeAPI = true,
            int? Version = null
            )
        {
            string protocol = Encrypted
                ? "https://"
                : "http://"
                ;

            string trailingSlash = TrailingSlash
                ? "/"
                : null
                ;

            string version = null;
            if (Version != null)
            {

                version = Version > 0
                    ? GetAPIVersionStub(IncludeAPI, Version)
                    : null
                    ;

                if (!TrailingSlash)
                    version = $" ({version ?? 0.ToString()})";
            }

            return $"{protocol}{TrimSlashes(HostName)}{trailingSlash}{version}";
        }

        public string GetHostNameWithProtocol(bool Encrypted, bool TrailingSlash = true, bool IncludeAPI = true)
            => GetProtocol(NameWithPort(Name, Port), Encrypted, TrailingSlash, IncludeAPI, Version > 0 ? Version : null)
            ;

        public string GetHostNameWithProtocol(bool TrailingSlash = true, bool IncludeAPI = true)
            => GetHostNameWithProtocol(Encrypted, TrailingSlash, IncludeAPI)
            ;

        public static string GetAPIVersionStub(bool IncludeAPI = true, int? Version = DEFAULT_VERSION)
            => IncludeAPI
            ? CombineRoute("api", Version != null ? $"v{Version}" : null)
            : null
            ;

        public string GetAPIVersionStub(bool IncludeVersion = true)
            => GetAPIVersionStub(true, IncludeVersion ? Version : null)
            ;

        public string FullDisplayName(bool IncludeAuth = false)
            => $"{GetEnabledCheckbox()} "
            + $"{GetConnectionSymbol(ConnectionLevel)} " // \u000f ☼ | \u0017 ↨
            + GetHostNameWithProtocol()
            + (AuthToken.IsNullOrEmpty() || !IncludeAuth ? null : " (Auth)")
            ;

        private static HttpWebRequest CreateWebRequest(
            string URI,
            string ContentType,
            WebMethods Method,
            int? Timeout = null,
            Action<StreamWriter> Proc = null
            )
        {
            if (!MainMenuBones.HasSeenMainMenu)
                return null;

            try
            {
                // throw new NotSupportedException("The URI prefix is not recognized");
                // the above is the error that linux users get.
                var httpReq = (HttpWebRequest)WebRequest.Create(URI);
                httpReq.ContentType = Route.ContentTypes.GetValue(ContentType) ?? ContentType;
                httpReq.Method = Method.ToString();

                if (Timeout.HasValue)
                    httpReq.Timeout = Timeout.GetValueOrDefault();

                if (Proc != null)
                {
                    using var streamWriter = new StreamWriter(httpReq.GetRequestStream());
                    Proc.Invoke(streamWriter);
                }
                return httpReq;
            }
            catch (Exception x)
            {
                if (MainMenuBones.AllowPromptAboutLinux
                    && (x is NotSupportedException
                        || x?.InnerException is NotSupportedException)
                    && (x.Message?.Contains("The URI prefix is not recognized") is true))
                    throw x;
            }
            return null;
        }

        private HttpWebRequest CreateWebRequest(
            Route Route,
            bool IncludeVersion,
            int? Timeout,
            bool RequireSession,
            Action<StreamWriter> Proc,
            out string URI,
            RouteParam[] RouteParams,
            RouteQuery RouteQuery,
            [CallerMemberName] string CallingMethod = ""
            )
        {
            URI = CombineHostRoute(IncludeVersion: IncludeVersion, Route.ParseValue(RouteParams, RouteQuery));

            using var indent = new Indent();
            Debug.LogMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Route), Route),
                    Debug.Arg(nameof(IncludeVersion), IncludeVersion),
                    Debug.Arg(nameof(Timeout), Timeout?.ToString() ?? "null"),
                    Debug.Arg(nameof(RequireSession), RequireSession),
                    Debug.Arg($"out {nameof(URI)}", URI),
                    Debug.Arg(nameof(RouteParams), (RouteParams?.Count())?.ToString() ?? "null"),
                    Debug.Arg(nameof(RouteQuery), (RouteQuery?.Count())?.ToString() ?? "null"),
                    Debug.Arg(nameof(CallingMethod), CallingMethod),
                });

            var httpReq = CreateWebRequest(
                URI: URI,
                ContentType: Route.ContentType,
                Method: Route.Method,
                Timeout: Timeout,
                Proc: Proc);

            if (RequireSession
                && JwtToken.IsNullOrEmpty()
                && GetSessionAsync().WaitResult() is string sessionToken)
                JwtToken = sessionToken;

            if (!JwtToken.IsNullOrEmpty())
                httpReq.Headers.Add(JWT_HEADER, JwtToken);

            return httpReq;
        }

        private HttpWebRequest CreateWebRequest(
            Route Route,
            bool IncludeVersion,
            int? Timeout,
            out string URI,
            bool RequireSession = true,
            RouteParam[] RouteParams = null,
            [CallerMemberName] string CallingMethod = ""
            )
            => CreateWebRequest(
                Route: Route,
                IncludeVersion: IncludeVersion,
                Timeout: Timeout,
                RequireSession: RequireSession,
                Proc: null,
                URI: out URI,
                RouteParams: RouteParams,
                RouteQuery: null,
                CallingMethod: CallingMethod)
            ;

        private HttpWebRequest CreateWebRequest(
            Route Route,
            Action<StreamWriter> Proc,
            out string URI,
            bool RequireSession,
            RouteParam[] RouteParams = null,
            [CallerMemberName] string CallingMethod = ""
            )
            => CreateWebRequest(
                Route: Route,
                IncludeVersion: true,
                Timeout: GetTimeout(),
                RequireSession: RequireSession,
                Proc: Proc,
                URI: out URI,
                RouteParams: RouteParams,
                RouteQuery: null,
                CallingMethod: CallingMethod)
            ;

        private HttpWebRequest CreateWebRequest(
            Route Route,
            out string URI,
            bool RequireSession,
            RouteParam[] RouteParams = null,
            [CallerMemberName] string CallingMethod = ""
            )
            => CreateWebRequest(
                Route: Route,
                RequireSession: RequireSession,
                Proc: null,
                URI: out URI,
                RouteParams: RouteParams,
                CallingMethod: CallingMethod)
            ;

        private HttpWebRequest CreateWebRequest(
            Route Route,
            Action<StreamWriter> Proc,
            out string URI,
            RouteParam[] RouteParams = null,
            [CallerMemberName] string CallingMethod = ""
            )
            => CreateWebRequest(
                Route: Route,
                Proc: Proc,
                URI: out URI,
                RequireSession: true,
                RouteParams: RouteParams,
                CallingMethod: CallingMethod)
            ;

        private HttpWebRequest CreateWebRequest(
            Route Route,
            out string URI,
            RouteParam[] RouteParams = null,
            [CallerMemberName] string CallingMethod = ""
            )
            => CreateWebRequest(
                Route: Route,
                Proc: null,
                URI: out URI,
                RouteParams: RouteParams,
                CallingMethod: CallingMethod)
            ;

        public bool HasPending(SaveBonesInfo BonesInfo)
            => BonesInfo != null
            && PendingUploads?.ContainsKey(BonesInfo.ID) is true
            ;

        private bool TryUpdateJwtToken(
            HttpWebResponse HttpRes,
            [CallerMemberName] string CallingMethod = ""
            )
        {
            if (HttpRes == null)
            {
                JwtToken = null;
                return true;
            }
            try
            {
                if (HttpRes?.Headers?.Get(JWT_HEADER) is string jwtToken
                    && !jwtToken.IsNullOrEmpty())
                    JwtToken = jwtToken;

                return true;
            }
            catch (Exception x)
            {
                Utils.Warn($"{CallingMethod} encountered issue updating {nameof(JwtToken)}", x);
            }
            return false;
        }

        private async Task<bool> TryGetResultAndStatusAsync(
            HttpWebRequest HttpReq,
            Route Route,
            string URI,
            Action<HttpStatusCode> StatusCodeCallback,
            Action<string> ResultCallback
            )
        {
            try
            {
                // Utils.Log($"{3.Indent()}{nameof(GetEnumerableFromWebRequestAsync)}<{typeof(T).Name}>");
                var httpRes = (HttpWebResponse)HttpReq.GetResponse();
                string result = null;
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    // Utils.Log($"{3.Indent()}{nameof(streamReader.ReadToEndAsync)}");
                    result = await streamReader.ReadToEndAsync();
                }
                StatusCodeCallback?.Invoke(httpRes.StatusCode);
                ResultCallback?.Invoke(result);

                TryUpdateJwtToken(httpRes);

                return true;
            }
            catch (WebException x)
            {
                if (!HandleTimeoutWebException(x, URI, GetTimeout()))
                    throw x;

                TryUpdateJwtToken(null);
                return false;
            }
            catch (Exception x)
            {
                Route.ErrorFailed("receiving", nameof(HttpWebResponse), URI, x);
                TryUpdateJwtToken(null);
            }
            return false;
        }

        #region Server Status

        public async Task<bool> GetServerStatusAsync(
            bool RethrowForLinuxPrompt = false,
            [CallerMemberName] string CallingMethod = ""
            )
        {
            if (!Enabled)
                return false;

            string methodName = nameof(GetServerStatusAsync);

            using var indent = new Indent();
            Debug.LogCaller(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(RethrowForLinuxPrompt), RethrowForLinuxPrompt),
                    Debug.Arg(nameof(CallingMethod), CallingMethod),
                });

            string uRI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.Status;
            int? timeout = GetTimeout();
            if (timeout.HasValue)
                timeout = (int)(timeout.GetValueOrDefault() * 0.5);

            try
            {
                httpReq = CreateWebRequest(
                    Route: route,
                    IncludeVersion: false,
                    Timeout: timeout,
                    URI: out uRI,
                    RequireSession: false);
            }
            catch (Exception x)
            {
                if (RethrowForLinuxPrompt
                    && (x is NotSupportedException
                        || x?.InnerException is NotSupportedException)
                    && (x.Message?.Contains("The URI prefix is not recognized") is true))
                    throw x;

                route.ErrorFailed("creating", nameof(HttpWebRequest), uRI, x);
                return false;
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                TryUpdateJwtToken(httpRes);
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.OK)
                    {
                        if (JObject.Parse(result) is JObject jObject
                            && jObject["status"].ToObject<string>() is string statusString)
                        {
                            bool isRunning = statusString == "Running";

                            if (!isRunning)
                                Utils.Warn($"{ToString()} returned status \"{statusString}\"");

                            Debug.Log($"return true", Indent: indent[2]);
                            return isRunning;
                        }
                        else
                            Utils.Warn($"{methodName} received unexpected data from {ToString()}" +
                                $" - {httpRes.StatusCode.DisplayString()} - {result}");
                    }
                    else
                        Utils.Warn($"{methodName} received unexpected status code from {ToString()}" +
                            $" - {httpRes.StatusCode.DisplayString()}");
                }
            }
            catch (WebException x)
            {
                if (!HandleTimeoutWebException(x, uRI, timeout))
                    throw x;

                if (x.Is404Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.NotFound.DisplayString()} for {methodName} via route {uRI}");
                    return false;
                }
                if (x.Is403Exception())
                {
                    TryUpdateJwtToken(null);
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.Forbidden.DisplayString()} for {methodName} via route {uRI}");
                    return false;
                }
            }
            catch (Exception x)
            {
                route.ErrorFailed("receiving", nameof(HttpWebResponse), uRI, x);
            }

            Debug.Log($"return false", Indent: indent[2]);
            return false;
        }

        public bool GetServerStatus(
            bool RethrowForLinuxPrompt = false,
            [CallerMemberName] string CallingMethod = ""
            )
            => GetServerStatusAsync(RethrowForLinuxPrompt, CallingMethod).WaitResult()
            ;

        public async Task<bool> GetCanUpAsync(
            [CallerMemberName] string CallingMethod = ""
            )
        {
            if (!IsRunning
                || UserID == Guid.Empty)
                return false;

            using var indent = new Indent();
            Debug.LogCaller(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(CallingMethod), CallingMethod)
                });

            string methodName = nameof(GetCanUpAsync);

            string uRI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.CanUpload;
            try
            {
                httpReq = CreateWebRequest(
                    Route: route,
                    URI: out uRI,
                    RequireSession: false,
                    RouteParams: new RouteParam[]
                    {
                        new(nameof(UserID), UserID)
                    });
            }
            catch (Exception x)
            {
                route.ErrorFailed($"creating", nameof(HttpWebRequest), uRI, x);
                return false;
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                TryUpdateJwtToken(httpRes);
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.OK)
                    {
                        if (JsonConvert.DeserializeObject<bool>(result) is bool parsedResult)
                            return parsedResult;
                        else
                            Utils.Warn($"{methodName} received unexpected data from {ToString()} - {httpRes.StatusCode.DisplayString()} - {result}");
                    }
                    else
                        Utils.Warn($"{methodName} received unexpected status code from {ToString()} - {httpRes.StatusCode.DisplayString()}");
                }
            }
            catch (WebException x)
            {
                if (!HandleTimeoutWebException(x, uRI, GetTimeout()))
                    throw x;

                if (x.Is404Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.NotFound.DisplayString()} for {methodName} via route {uRI}");
                    return false;
                }
                if (x.Is403Exception())
                {
                    TryUpdateJwtToken(null);
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.Forbidden.DisplayString()} for {methodName} via route {uRI}");
                    return false;
                }
                if (x.Is400Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.BadRequest.DisplayString()} for {methodName} via route {uRI}");
                    return false;
                }
            }
            catch (Exception x)
            {
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
            }
            return false;
        }

        public bool GetCanUp(
            [CallerMemberName] string CallingMethod = ""
            )
            => GetCanUpAsync(CallingMethod: CallingMethod).WaitResult()
            ;

        #endregion
        #region User

        public string ConsumeAuthToken()
        {
            var authToken = AuthToken;
            AuthToken = null;
            return authToken;
        }

        public async Task<string> AskWhatPasswordAsync(
            string Purpose,
            string Message,
            bool IncludeNote = true,
            bool ConfirmEntered = true,
            bool PushView = false
            )
        {
            using var title = TextBuilder.Get()
                .AppendColored("yellow", $"{Purpose} {OSSEOUS_ASH} User");

            using var message = TextBuilder.Get()
                .Append(Message);
            if (IncludeNote)
                message.AppendLine()
                .AppendLine().Append("Please note: default host behaviour does not typically require a password.");

            if (await UIUtils.AskStringAsync(
                    Message: message.ToString(),
                    Title: title.ToString(),
                    ReturnNullForEscape: true,
                    EscapeNonMarkupFormatting: false,
                    pushView: PushView
                ) is string passwordString)
            {
                if (passwordString == null)
                    return null;

                if (passwordString == string.Empty)
                {
                    message.Clear()
                        .Append("You have entered an empty password.")
                        .AppendLine()
                        .AppendLine().Append("Is this correct?")
                        .AppendLine().Append("Select \"cancel\" to cancel ").Append(Purpose.ToLower()).Append(", otherwise \"no\" to enter something else.");

                    var confirmEmpty = await Popup.NewPopupMessageAsync(
                        message: message.ToString(),
                        buttons: PopupMessage.YesNoCancelButton,
                        title: title.ToString(),
                        pushView: PushView);

                    using var indent = new Indent();
                    Debug.Log($"{nameof(confirmEmpty)}: {confirmEmpty.command}", Indent: indent[1]);

                    return confirmEmpty.command switch
                    {
                        "No" => await AskWhatPasswordAsync(Purpose, Message, IncludeNote, PushView: PushView),
                        "Yes" => string.Empty,
                        _ => null,
                    };
                }

                string confirmText = passwordString;
                bool isConsole = CapabilityManager.CurrentPlatformClassification() == CapabilityManager.PlatformClassification.Console;

                string typeToConfirmText = !isConsole
                    ? $"Type {confirmText.Colored("W")} to confirm."
                    : $"{confirmText.Colored("W")}"
                    ;

                string defaultValue = !isConsole
                    ? string.Empty
                    : null
                    ;

                if (!ConfirmEntered
                    || (await UIUtils.AskStringAsync(
                            Message: $"Is this correct?\n\n{typeToConfirmText}",
                            Title: title.ToString(),
                            Default: defaultValue,
                            pushView: PushView,
                            WantsSpecificPrompt: confirmText)
                        ) == confirmText)
                    return passwordString;
            }

            return null;
        }

        public async Task<bool> CheckRegisterUserAsync(
            string Password = null,
            bool UpdatePassword = false,
            bool TryGetSession = true,
            bool PushView = false
            )
        {
            if (!IsRunning
                || UserID == Guid.Empty)
                return false;

            bool update = false;
            using (var existingUser = await GetUserAsync(CatchBadRequest: true, PushView: PushView))
            {
                if (existingUser != null)
                {
                    existingUser.CopyTo(ref User);
                    if (!UpdatePassword)
                        return true;

                    update = true;
                }
            }

            using var indent = new Indent();
            Debug.LogCaller(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Password), Password != null),
                    Debug.Arg(nameof(UpdatePassword), UpdatePassword),
                    Debug.Arg(nameof(TryGetSession), TryGetSession),
                    Debug.Arg(nameof(PushView), PushView),
                });

            string callingMethod = nameof(CheckRegisterUserAsync);

            string uRI = null;
            HttpWebRequest httpReq = null;
            var route = !update ? Routes.NewUser : Routes.UpdateUserPassword;
            using var user = new User(Configuration.Instance, Password);
            try
            {
                Debug.Log($"{nameof(User)}:", Indent: indent[2]);
                Debug.LogJson(user, Indent: indent[2]);
                httpReq = CreateWebRequest(
                    Route: route,
                    Proc: async streamWriter => await streamWriter.WriteAsync(JsonConvert.SerializeObject(new { User = user })),
                    URI: out uRI,
                    RequireSession: false,
                    RouteParams: !update ? null : new RouteParam[] { new(nameof(UserID), UserID) });

            }
            catch (Exception x)
            {
                route.ErrorFailed($"creating", nameof(HttpWebRequest), uRI, x);
                return false;
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                TryUpdateJwtToken(httpRes);
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.OK
                        || httpRes.StatusCode == HttpStatusCode.Created)
                    {
                        if (JsonConvert.DeserializeObject<User>(result) is User parsedResult)
                        {
                            parsedResult.CopyTo(ref User, SanitizePassword: true);
                            parsedResult.Dispose();

                            /*if (TryGetSession
                                && JwtToken == null
                                && await GetSessionAsync(PushView) is string sessionToken)
                                JwtToken = sessionToken;*/

                            return true;
                        }
                        else
                        {
                            Utils.Warn($"{callingMethod} received unexpected data from {ToString()}" +
                                $" - {httpRes.StatusCode.DisplayString()} - {result}");
                        }
                    }
                    else
                    {
                        Utils.Warn($"{callingMethod} received unexpected status code from {ToString()}" +
                            $" - {httpRes.StatusCode.DisplayString()}");
                    }
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return false;

                if (x.Is404Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.NotFound.DisplayString()} for {callingMethod} via route {uRI}");
                    return false;
                }
                if (x.Is403Exception())
                {
                    TryUpdateJwtToken(null);
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.Forbidden.DisplayString()} for {callingMethod} via route {uRI}");
                    return false;
                }
                if (x.Is400Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.BadRequest.DisplayString()} for {callingMethod} via route {uRI}");
                    return false;
                }
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
            }
            catch (Exception x)
            {
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
            }
            finally
            {
                Debug.Log("Attempted to register User:", Indent: indent[2]);
                if (user != null)
                    Debug.LogJson(user, Indent: indent[2]);
                else
                    Debug.Log("none", Indent: indent[3]);
            }
            return false;
        }

        public async Task<User> GetUserAsync(bool CatchBadRequest = false, bool PushView = false)
        {
            if (!IsRunning
                || UserID == Guid.Empty)
                return null;

            if (JwtToken.IsNullOrEmpty())
            {
                if (await GetSessionAsync(PushView: PushView) is not string sessionToken)
                    return null;

                JwtToken = sessionToken;
            }

            using var indent = new Indent();
            Debug.LogCaller(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(CatchBadRequest), CatchBadRequest),
                    Debug.Arg(nameof(PushView), PushView),
                });

            string callingMethod = nameof(GetUserAsync);

            string uRI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.User;
            try
            {
                httpReq = CreateWebRequest(route, out uRI, new RouteParam[] { new(nameof(UserID), UserID) });
            }
            catch (Exception x)
            {
                route.ErrorFailed("creating", nameof(HttpWebRequest), uRI, x);
                return null;
            }

            if (httpReq == null)
                return null;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                TryUpdateJwtToken(httpRes);
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.OK
                        && JsonConvert.DeserializeObject<User>(result) is User parsedResult)
                    {
                        return parsedResult;
                    }
                    else
                        Utils.Warn($"{callingMethod} got no status from {ToString()}" +
                            $" - {httpRes.StatusCode.DisplayString()}");
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return null;

                if (x.Is404Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.NotFound.DisplayString()} for {callingMethod} via route {uRI}");
                    return null;
                }
                if (x.Is403Exception())
                {
                    TryUpdateJwtToken(null);
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.Forbidden.DisplayString()} for {callingMethod} via route {uRI}");
                    return null;
                }
                if (CatchBadRequest
                    && x.Is400Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.BadRequest.DisplayString()} for {callingMethod} via route {uRI}");
                    return null;
                }
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
            }
            catch (Exception x)
            {
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
            }
            return null;
        }

        public async Task<string> GetSessionAsync(bool PushView = false)
        {
            if (!IsRunning)
                return null;

            if (!JwtToken.IsNullOrEmpty())
                return JwtToken;

            using var indent = new Indent();
            Debug.LogCaller(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(PushView), PushView),
                });

            var password = User?.Password
                ?? await AskWhatPasswordAsync(
                    Purpose: "Authenticate",
                    Message: $"Enter your password if you have one to authenticate with this host.",
                    IncludeNote: false,
                    ConfirmEntered: false,
                    PushView: PushView);

            string callingMethod = nameof(GetSessionAsync);

            Debug.YehNah($"have {nameof(password)}", password != null, Indent: indent[2]);
            if (password == null)
            {
                Utils.Warn($"{callingMethod} was not provided any credentials with which to authenticate {ToString()}.");
                return null;
            }

            string uRI = null;
            var route = Routes.GetSession;
            HttpWebRequest httpReq = null;
            try
            {
                httpReq = CreateWebRequest(
                    Route: route,
                    URI: out uRI,
                    RequireSession: false,
                    RouteParams: new RouteParam[]
                    {
                        new(nameof(UserID), UserID)
                    });

                httpReq.Headers.AppendWebHeader(HttpRequestHeader.Authorization, $"basic {password}");
            }
            catch (Exception x)
            {
                route.ErrorFailed($"creating", nameof(HttpWebRequest), uRI, x);
                return null;
            }

            if (httpReq == null)
                return null;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    Debug.Log(nameof(StreamReader), nameof(streamReader.ReadToEndAsync), Indent: indent[2]);
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.OK)
                    {
                        Debug.Log(nameof(httpRes.StatusCode), httpRes.StatusCode, Indent: indent[2]);
                        if (JObject.Parse(result) is JObject jObject)
                        {
                            Debug.Log(nameof(jObject), jObject.ToString(), Indent: indent[2]);
                            try
                            {
                                if (jObject["user"].ToObject<User>() is not User user)
                                {
                                    Utils.Warn($"{callingMethod} failed to deserialize {nameof(jObject)}[\"user\"] to {typeof(User)}");
                                    return null;
                                }
                                Debug.LogJson(nameof(user), user, Indent: indent[2]);
                                if (user.ID != UserID)
                                {
                                    Utils.Warn($"{callingMethod} deserialized {typeof(User)} has incorrect ID:");
                                    Utils.Log($"{1.Indent()}{{{user.ID}}} - Got");
                                    Utils.Log($"{1.Indent()}{{{UserID}}} - Expected");
                                    return null;
                                }
                                Debug.CheckYeh($"ID Matches!", Indent: indent[2]);
                                user.CopyTo(ref User, SanitizePassword: true);
                                user.Dispose();

                                // -- This is just debug stuff to force it to crash-out under controlled circumstances
                                Vomit(Indent: indent[2]);
                                Debug.CheckYeh(nameof(User), (User?.ID)?.ToString() ?? "MISSING_USER", Indent: indent[2]);
                                Debug.LogJson(User, Indent: indent[2]);

                                string fileName = "User.json";
                                var localFileLoc = Configuration.GetOsseousAshFileLocationData(Ensure: true).FirstOrDefault(f => f.Type == FileLocationData.LocationType.Local);
                                Debug.Log($"Writing User to {localFileLoc.WithFileName(fileName)}", Indent: indent[2]);
                                localFileLoc?.Write(fileName, User, Formatting.Indented); // `User` is serialised and written to disk just fine on its own

                                Formatting? formatting = Formatting.Indented;
                                string formattingString = $"Formatting{(formatting.HasValue ? $" ({formatting})" : null)}";
                                string without = $"with{(formatting.HasValue ? null : "out")}";

                                Debug.LogJson(nameof(SuperUser), new SuperUser(User), formatting.GetValueOrDefault(), Indent: indent[2]); // Checking if this works.
                                Debug.Log($"Made it past {nameof(SuperUser)} {without} {formattingString}.", Indent: indent[2]);

                                Debug.LogJson(nameof(Host), this, formatting.GetValueOrDefault(), Indent: indent[2]); // Crashes here trying to serialize `User` when it's a member of `Host`
                                Debug.Log($"Made it past {nameof(Host)} {without} {formattingString}.", Indent: indent[2]);
                                // -- Remove above this when the issue is resolved.
                            }
                            catch (Exception x)
                            {
                                Utils.Error($"{callingMethod} failed to deserialize {nameof(jObject)}[\"user\"] to {typeof(User)}", x);
                            }

                            try
                            {
                                if (jObject["token"].ToString() is not string token)
                                {
                                    Utils.Warn($"{callingMethod} failed to deserialize {nameof(jObject)}[\"token\"] to {typeof(User)}");
                                    return null;
                                }
                                return token;
                            }
                            catch (Exception x)
                            {
                                Utils.Error($"{callingMethod} failed to deserialize {nameof(jObject)}[\"token\"] to {typeof(User)}", x);
                            }
                        }
                        else
                        {
                            Utils.Warn($"{callingMethod} received unexpected data from {ToString()}" +
                                $" - {httpRes.StatusCode.DisplayString()} - {result}");
                        }
                    }
                    else
                    {
                        Utils.Warn($"{callingMethod} received unexpected status code from {ToString()}" +
                            $" - {httpRes.StatusCode.DisplayString()}");
                    }
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return null;

                if (x.Is404Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.NotFound.DisplayString()} for {callingMethod} via route {uRI}");
                    return null;
                }
                if (x.Is403Exception())
                {
                    TryUpdateJwtToken(null);
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.Forbidden.DisplayString()} for {callingMethod} via route {uRI}");
                    return null;
                }
                if (x.Is400Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.BadRequest.DisplayString()} for {callingMethod} via route {uRI}");
                    return null;
                }
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
            }
            catch (Exception x)
            {
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
            }
            return null;
        }

        public async Task<bool> UpdateUserHandleAsync()
        {
            if (!IsRunning
                || UserID == Guid.Empty
                || !await CheckRegisterUserAsync())
                return false;

            string uRI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.UpdateUserHandle;
            try
            {
                User?.Dispose();
                User = new User(Configuration.Instance);
                httpReq = CreateWebRequest(
                    Route: route,
                    Proc: async delegate (StreamWriter streamWriter)
                    {
                        await streamWriter.WriteAsync(JsonConvert.SerializeObject(new { User }));
                    },
                    URI: out uRI,
                    RouteParams: new RouteParam[]
                    { 
                        new(nameof(UserID), UserID)
                    });

                using var title = TextBuilder.Get()
                    .AppendColored("yellow", $"Update {OSSEOUS_ASH} Handle");

                using var message = TextBuilder.Get()
                    .Append("The host you're updating your handle with may require a password. If you know for sure you have one, please enter it now.");

                if (await UIUtils.AskStringAsync(
                    Message: message.ToString(),
                    Title: title.ToString(),
                    ReturnNullForEscape: true,
                    EscapeNonMarkupFormatting: false) is string passwordString
                    && !passwordString.IsNullOrEmpty())
                    httpReq.Headers.AppendWebHeader(HttpRequestHeader.Authorization, $"basic {passwordString}");
            }
            catch (Exception x)
            {
                route.ErrorFailed($"creating", nameof(HttpWebRequest), uRI, x);
                return false;
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                TryUpdateJwtToken(httpRes);
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    if (httpRes.StatusCode == HttpStatusCode.OK
                        && JsonConvert.DeserializeObject<bool>(result) is bool parsedResult
                        && parsedResult)
                    {
                        return parsedResult;
                    }
                    else
                        Utils.Warn($"{nameof(UpdateUserHandleAsync)} got no status from {ToString()}" +
                            $" - {httpRes.StatusCode.DisplayString()}");
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return false;

                if (x.Is404Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.NotFound.DisplayString()} for {nameof(UpdateUserHandleAsync)} via route {uRI}");
                    return false;
                }
                if (x.Is403Exception())
                {
                    TryUpdateJwtToken(null);
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.Forbidden.DisplayString()} for {nameof(UpdateUserHandleAsync)} via route {uRI}");
                    return false;
                }
                if (x.Is400Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.BadRequest.DisplayString()} for {nameof(UpdateUserHandleAsync)} via route {uRI}");
                    return false;
                }
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
            }
            catch (Exception x)
            {
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
            }
            return false;
        }

        #endregion
        #region Post Bones Report

        public async Task<bool> PostBonesReport(Report Report, bool ClearReportsCache = true)
        {
            if (!IsRunning
                || Report == null
                || Report.BonesID.IsNullOrEmpty()
                || !await CheckRegisterUserAsync())
                return false;

            string uRI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.NewReport;
            try
            {
                httpReq = CreateWebRequest(
                    Route: route,
                    Proc: async delegate (StreamWriter streamWriter)
                    {
                        await streamWriter.WriteAsync(JsonConvert.SerializeObject(Report));
                    },
                    URI: out uRI);
            }
            catch (Exception x)
            {
                route.ErrorFailed($"creating", nameof(HttpWebRequest), uRI, x);
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                TryUpdateJwtToken(httpRes);
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.Created)
                    {
                        if (JObject.Parse(result) is JObject jObject
                            && jObject["success"].ToObject<bool>() is bool success)
                        {
                            Utils.Info($"Successfully reported {Report.BonesID} on server at \"{ToString()}\"" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");

                            if (ClearReportsCache)
                                this.ClearReportsCache();

                            return success;
                        }
                    }
                    else
                    {
                        Utils.Warn($"{nameof(PostBonesReport)} received response from server at \"{ToString()}\": {httpRes.StatusCode} ({(int)httpRes.StatusCode}) " +
                            $"instead of expected {HttpStatusCode.Created} ({(int)HttpStatusCode.Created})");
                    }
                }
            }
            catch (WebException x)
            {
                if (!HandleTimeoutWebException(x, uRI, GetTimeout()))
                    throw x;

                return false;
            }
            catch (Exception x)
            {
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
                return false;
            }
            return false;
        }

        #endregion
        #region Check Bones Report

        public async Task<bool> GetBonesReportBlockedCheck(string BonesID)
        {
            if (!IsRunning
                || BonesID.IsNullOrEmpty()
                || UserID.IsEmptyOrDefault()
                || !await CheckRegisterUserAsync())
                return false;

            string uRI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.Reported;
            try
            {
                httpReq = CreateWebRequest(route, out uRI, new RouteParam[] { new(nameof(BonesID), BonesID), new(nameof(UserID), UserID) });
            }
            catch (Exception x)
            {
                route.ErrorFailed($"creating", nameof(HttpWebRequest), uRI, x);
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                TryUpdateJwtToken(httpRes);
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.OK)
                    {
                        if (JObject.Parse(result) is JObject jObject
                            && jObject["success"].ToObject<bool>() is bool success
                            && jObject["blocked"].ToObject<bool>() is bool blocked)
                        {
                            int reportCount = jObject["reports"]?.ToObject<int>() ?? 0;
                            Utils.Info($"{BonesID} has {reportCount.Things(nameof(OsseousAsh.Report))} from {UserID} on server at \"{ToString()}\", " +
                                $"at least 1 indicating {nameof(blocked)}" +
                                $" - {httpRes.StatusCode.DisplayString()}");

                            return blocked;
                        }
                    }
                    else
                    {
                        Utils.Warn($"{nameof(GetBonesReportBlockedCheck)} received response from server at \"{ToString()}\": {httpRes.StatusCode.DisplayString()}");
                    }
                }
            }
            catch (WebException x)
            {
                if (!HandleTimeoutWebException(x, uRI, GetTimeout()))
                    throw x;

                if (x.Is403Exception())
                {
                    TryUpdateJwtToken(null);
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.Forbidden.DisplayString()} for {nameof(GetBonesReportBlockedCheck)} via route {uRI}");
                    return false;
                }
                return false;
            }
            catch (Exception x)
            {
                route.ErrorFailed($"receiving", nameof(HttpWebResponse), uRI, x);
                return false;
            }
            return false;
        }

        public IEnumerable<Report> GetBonesReports()
        {
            //Utils.Log($"{1.Indent()}{nameof(GetBonesReports)}");
            if (!IsRunning)
                return null;

            if (!CheckRegisterUserAsync().WaitResult())
                return null;

            string uRI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.UserReports;
            try
            {
                httpReq = CreateWebRequest(route, out uRI, new RouteParam[] { new(nameof(UserID), UserID) });
            }
            catch (Exception x)
            {
                route.ErrorFailed("creating", nameof(HttpWebRequest), uRI, x);
            }

            if (httpReq == null)
                return null;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                TryUpdateJwtToken(httpRes);
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    if (httpRes.StatusCode == HttpStatusCode.OK)
                    {
                        try
                        {
                            var reports = new List<Report>();
                            if (JObject.Parse(result) is JObject jObject)
                            {
                                if (jObject["success"].ToObject<bool>() is bool success
                                    && jObject["data"] is JToken data)
                                {
                                    int counter = 0;
                                    while (true)
                                    {
                                        try
                                        {
                                            if (data?[counter++]?.ToObject<Report>() is Report report)
                                            {
                                                if (report.UserID == UserID)
                                                    reports.Add(report);
                                            }
                                            else
                                                break;
                                        }
                                        catch (ArgumentOutOfRangeException)
                                        {
                                            break;
                                        }
                                        catch (Exception x)
                                        {
                                            Utils.Error($"Deserializing {nameof(GetBonesReports)} response into {nameof(IEnumerable<Report>)}", x);
                                            break;
                                        }
                                    }
                                    Utils.Info($"Found {reports.Count.Things(nameof(OsseousAsh.Report))} by player {{{UserID}}} on server at {ToString()}" +
                                        $" - {httpRes.StatusCode.DisplayString()}");

                                    return reports;
                                }
                                else
                                {
                                    if (jObject.ToObject<Report[]>() is Report[] reportsArray)
                                    {
                                        reports.AddRange(reportsArray);

                                        Utils.Info($"Found {reports.Count.Things(nameof(OsseousAsh.Report))} by player {{{UserID}}} on server at {ToString()}" +
                                            $" - {httpRes.StatusCode.DisplayString()}");
                                        return reports;
                                    }
                                }
                            }
                            else
                            if (JsonConvert.DeserializeObject<Report[]>(result) is Report[] reportsArray)
                            {
                                reports.AddRange(reportsArray);

                                Utils.Info($"Found {reports.Count.Things(nameof(OsseousAsh.Report))} by player {{{UserID}}} on server at {ToString()}" +
                                    $" - {httpRes.StatusCode.DisplayString()}");
                                return reports;
                            }
                        }
                        catch (Exception x)
                        {
                            Utils.Error($"Reading {nameof(GetBonesReports)} response", x);
                            return null;
                        }
                    }
                    else
                    if (httpRes.StatusCode > HttpStatusCode.NoContent
                        || httpRes.StatusCode < HttpStatusCode.OK)
                    {
                        Utils.Warn($"{nameof(GetBonesReports)} received response from server at \"{ToString()}\": {httpRes.StatusCode.DisplayString()}");
                    }
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return Enumerable.Empty<Report>();
                throw x;
            }
            catch (Exception x)
            {
                route.ErrorFailed("receiving", nameof(HttpWebResponse), uRI, x);
                return Enumerable.Empty<Report>();
            }
            return Enumerable.Empty<Report>();
        }

        /*public async Task<IEnumerable<string>> GetBonesReportsForUser()
        {
            if (!Enabled
                || !IsRunning
                || (Config?.ID ?? Guid.Empty).IsEmptyOrDefault())
                return Enumerable.Empty<string>();

            string uRI = BonesReportUserReportsRoute(Config.ID);

            HttpWebRequest httpReq = null;
            int? timeout = GetTimeout();
            try
            {
                httpReq = CreateGetJSON(uRI, timeout);
            }
            catch (Exception x)
            {
                Utils.Error($"Creating GET HttpWebRequest for {uRI}", x);
            }

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.OK)
                    {
                        if (JObject.Parse(result) is JObject jObject
                            && jObject["success"].ToObject<bool>() is bool success
                            && jObject["blocked"].ToObject<bool>() is bool blocked)
                        {
                            int reportCount = jObject["reports"]?.ToObject<int>() ?? 0;
                            Utils.Info($"{OAID} has {reportCount.Things(nameof(OsseousAsh.Report))} from {Config.ID} on server at \"{ToString()}\", " +
                                $"at least 1 indicating {nameof(blocked)}" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");

                            return blocked;
                        }
                    }
                    else
                    {
                        Utils.Warn($"{nameof(GetBonesReportBlockedCheck)} received response from server at \"{ToString()}\": {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                    }
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, timeout))
                    return false;
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed receiving GET response for {uRI}", x);
                return false;
            }
            return false;
        }*/

        #endregion

        public bool SameAs(Host Other, bool IgnoreDisabled = false)
            => Other is not null
            && Name == Other.Name
            && Port == Other.Port
            && Encrypted == Other.Encrypted
            && TimeoutMS == Other.TimeoutMS
            && AuthToken == Other.AuthToken
            && (IgnoreDisabled
                || GetEnabledValueForMenu() == Other.GetEnabledValueForMenu())
            ;

        public override string ToString()
            => $"{GetHostNameWithProtocol(TrailingSlash: true)}"
            ;

        public override bool Equals(object obj)
            => obj is Host hostObj
            ? Equals(hostObj)
            : Equals(this, obj)
            ;

        public override int GetHashCode()
            => Name.GetHashCode()
            ^ (Port?.GetHashCode() ?? 0)
            ^ Encrypted.GetHashCode()
            ^ (AuthToken?.GetHashCode() ?? 0)
            ^ Enabled.GetHashCode()
            ;

        public bool Equals(Host other)
            => SameAs(other)
            ;

        public static Task<bool> FlipEncryptedAsync(Host Host)
            => Task.Run(() => (Host.Encrypted = !Host.Encrypted) || true) // always true, but flip it first.
            ;

        public static Task<bool> FlipEnabledAsync(Host Host)
            => Task.Run(delegate ()
            {
                if (Host.WrittenEnabled.HasValue)
                    return (Host.WrittenEnabled = !Host.WrittenEnabled.GetValueOrDefault()).GetValueOrDefault()
                        || true; // always true, but flip it first.

                return (Host.Enabled = !Host.Enabled) || true; // always true, but flip it first.
            })
            ;

        #region Option Handling

        public static async Task ManageHostsOptionButtonWithSelectionAsync(
            Predicate<PickOptionData<HostLocationPair, Task<UIUtils.CascadableResult>>> SelectImmediately
            )
        {
            using var indent = new Indent();
            Debug.LogCaller(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(SelectImmediately), (SelectImmediately != null ? "not " : null) + "null"),
                });

            using var options = new CascadablePickOptionDataSetAsync<HostLocationPair>();
            var result = UIUtils.CascadableResult.Continue;
            HostLocationPair newHostLocationPair = null;
            do
            {
                options.ClearItems(Dispose: true);
                newHostLocationPair?.Dispose();
                newHostLocationPair = new();

                options.Add(new CascadablePickOptionDataAsync<HostLocationPair>
                {
                    Element = newHostLocationPair,
                    Text = "new host",
                    Icon = new Renderable(
                        Tile: "UI/sw_newchar.bmp",
                        ColorString: "&W",
                        TileColor: "&W",
                        DetailColor: 'y'),
                    Hotkey = 'n',
                    Callback = PerformNewHostAsync,
                });

                int offset = options.Count;
                using var exludeHotkeys = ScopeDisposedList<char>.GetFromPoolFilledWith(options.GetHotkeys());

                // ReadHostsTo(ref _Hosts);
                Debug.Log($"{nameof(ServerUtils.AllHostsWithLocation)}", Indent: indent[2]);
                using var hostLocationPairs = ScopeDisposedList<HostLocationPair>.GetFromPoolFilledWith(ServerUtils.AllHostsWithLocation());
                foreach (var hostLocationPair in hostLocationPairs)
                {
                    try
                    {
                        Debug.Log($"Pair: {hostLocationPair}", Indent: indent[3]);
                        if (!hostLocationPair.IsValid())
                        {
                            Utils.Warn($"{nameof(ManageHostsOptionButtonWithSelectionAsync)} got invalid {nameof(hostLocationPair)}: {hostLocationPair}");
                            continue;
                        }
                        var host = hostLocationPair.Host;
                        var locationData = hostLocationPair.FileLocationData;
                        options.Add(new CascadablePickOptionDataAsync<HostLocationPair>
                        {
                            Element = hostLocationPair,
                            Text = $"{locationData.Tag()} {host.FullDisplayName(IncludeAuth: true)}",
                            Icon = new Renderable(
                                Tile: "Mutations/gas_generation.bmp",
                                ColorString: "&K",
                                TileColor: "&K",
                                DetailColor: FileLocationData.GetFileLocationDataTypeColor(locationData.Type)[0]),
                            Hotkey = options.GetHotkeys().GetNextHotKey(Excluding: exludeHotkeys),
                            Callback = async delegate (HostLocationPair p)
                            {
                                var hostSet = ServerUtils.Hosts.FirstOrDefault(hs => hs.LocationData?.SameAs(p.FileLocationData) is true);
                                var host = p.Host;
                                return await AskDoWhatWithHostAsync(new(hostSet, host));
                            }
                        });
                    }
                    catch (Exception x)
                    {
                        Utils.Warn($"{nameof(ManageHostsOptionButtonWithSelectionAsync)} failed to add {nameof(hostLocationPair)}: \"{hostLocationPair}\" to {nameof(options)}", x);
                    }
                }

                if (SelectImmediately != null)
                {
                    Debug.Log($"{nameof(SelectImmediately)} not null", Indent: indent[3]);
                    if (options.FirstOrDefault(SelectImmediately.Invoke) is CascadablePickOptionDataAsync<HostSet.HostLocationPair> selectedImmediately)
                    {
                        if (selectedImmediately.Callback?.Invoke(selectedImmediately?.Element) is Task<UIUtils.CascadableResult> resultTask)
                            result = await UIUtils.ShowEscancellepedAsync(selectedImmediately, resultTask);
                    }
                    SelectImmediately = null;
                }
                else
                {
                    Debug.Log($"{nameof(SelectImmediately)} is null", Indent: indent[3]);

                    result = await UIUtils.PerformPickOptionAsync(
                        OptionDataSet: options,
                        Title: $"Manage {OSSEOUS_ASH} Host".Colored("yellow"),
                        Intro: $"Use the options below to manage the hosts to/from which you'd like to upload/download bones files.",
                        IntroIcon: Configuration.BlackAshCloudIcon,
                        DefaultSelected: 0);
                }

                if (newHostLocationPair?.IsValid() is false)
                {
                    Debug.Log($"!{nameof(newHostLocationPair)}.{nameof(newHostLocationPair.IsValid)}; {nameof(Dispose)}", Indent: indent[2]);
                    newHostLocationPair.Dispose();
                }
            }
            while (result.IsContinue());

            Debug.Log($"foreach hostCollection in ServerUtils.Hosts", Indent: indent[2]);
            foreach (var hostSet in ServerUtils.Hosts)
            {
                Debug.Log($"Write: {hostSet.LocationData}", Indent: indent[3]);
                hostSet.Write();
            }

            Debug.CheckYeh($"{nameof(ManageHostsOptionButtonWithSelectionAsync)} Finished", Indent: indent[2]);
        }

        private static async Task<UIUtils.CascadableResult> PerformNewHostAsync(HostLocationPair HostConfig)
        {
            if ((await AskHostLocationAsync()) is not FileLocationData chosenLocation)
                return UIUtils.CascadableResult.CancelSilent;

            HostConfig.FileLocationData = chosenLocation;

            var sB = Event.NewStringBuilder();
            using var options = new CascadablePickOptionDataSetAsync<HostLocationPair>(HostConfig);
            var result = UIUtils.CascadableResult.Continue;
            do
            {
                sB.Clear();
                options.ClearItems();

                sB.Append($"Which setup method would you like to use?")
                    .AppendLine()
                    .AppendLine().AppendColored("Y", "Basic").Append(" will allow you to enter the address and will attempt to parse the bulk of a config file from it, asking some follow up questions for details it can't parse.")
                    .AppendLine()
                    .AppendLine().AppendColored("W", "Advanced").Append(" will let you individually control each element of the eventual config.")
                    .AppendLineEnd();

                options.Add(new CascadablePickOptionDataAsync<HostSet.HostLocationPair>
                {
                    Text = "Basic Setup".Colored("Y"),
                    Icon = Configuration.DownloadIcon,
                    Hotkey = 'b',
                    Callback = PerformBasicNewHostAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<HostSet.HostLocationPair>
                {
                    Text = "Advanced Setup".Colored("W"),
                    Icon = Configuration.UploadIcon,
                    Hotkey = 'a',
                    Callback = PerformAdvancedNewHostAsync,
                });

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: $"New {OSSEOUS_ASH} Host".Colored("yellow"),
                    Intro: $"Which setup method would you like to use?\n\nBasic will allow you to enter the address and will attempt to parse the bulk of a config file from it.\n\nAdvanced will let you individually control each element of the eventual config.",
                    IntroIcon: Configuration.BlackAshCloudIcon,
                    DefaultSelected: 0);
            }
            while (result.IsContinue());

            return result;
        }

        private static async Task<UIUtils.CascadableResult> PerformBasicNewHostAsync(HostLocationPair Element)
        {
            bool? confirmed = null;
            do
            {
                try
                {
                    string entered = await AskFullHostNameAsync(Element.FileLocationData);

                    int? timeout = null;
                    if (entered != null)
                        timeout = await AskHostTimeoutAsync(null);

                    int? maxAttempts = null;
                    if (entered != null
                        && timeout.HasValue)
                        maxAttempts = await AskHostMaxUpAttemptsAsync(null);

                    if (!entered.IsNullOrEmpty()
                        && timeout.HasValue
                        && maxAttempts.HasValue
                        && TryParse(entered, out Element.Host))
                    {
                        Element.Host.TimeoutMS = timeout ?? DEFAULT_TIMEOUT;
                        Element.Host.MaxUploadAttempts = maxAttempts ?? DEFAULT_MAX_UP_ATTEMPTS;

                        if (Element.Host.Version < 0)
                        {
                            Element.Host.Version = (await AskHostVersionAsync(null)) ?? DEFAULT_VERSION;
                        }
                    }

                    confirmed = await ConfirmParsedHost(Element.Host);

                    if (!confirmed.HasValue)
                    {
                        Element.Host.Dispose(DisposePending: true);
                        break;
                    }

                    if (Element.Host == null)
                        break;

                    if (confirmed.GetValueOrDefault())
                    {
                        if (ServerUtils.Hosts.FirstOrDefault(s => s.LocationData?.SameAs(Element.FileLocationData) is true) is not HostSet hostSet)
                            hostSet = new HostSet(Element.FileLocationData);

                        hostSet.WriteAddHost(Element.Host);
                        if (await Element.Host.AskWhatPasswordAsync(
                            Purpose: "Register",
                            Message: "This is your first time using this host after setting it up. If you would like to set a password for use with this host, please enter it now.",
                            IncludeNote: true) is string newPassword)
                            await Element.Host.CheckRegisterUserAsync(newPassword, true);
                        break;
                    }
                    Element.Host.Dispose(DisposePending: true);
                }
                finally
                {
                    Element.Dispose();
                }
            }
            while (true);

            return confirmed.ToCascadableResult(Silent: true);
        }

        private static async Task<UIUtils.CascadableResult> PerformAdvancedNewHostAsync(HostLocationPair Element)
        {
            await Popup.ShowAsync("{{W|Advanced}} is not currently available.");
            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<FileLocationData> AskHostLocationAsync()
        {
            using var osseousAshFilePaths = ScopeDisposedList<FileLocationData>.GetFromPoolFilledWith(Configuration.GetOsseousAshFileLocationData(Ensure: true));
            using var options = new PickOptionDataSetAsync<FileLocationData, FileLocationData>();

            try
            {
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

                return await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: $"New {OSSEOUS_ASH} Host".Colored("yellow"),
                    Intro: $"Which location would you like to record your new host?\n\n" +
                        $"\u0007 [{FileLocationData.LocalTypeString}] only exists on this device.\n" +
                        $"\u0007 [{FileLocationData.SyncedTypeString}] will be synced by the platform managing the game's installation (if there is one).\n\n",
                    IntroIcon: Configuration.BlackAshCloudIcon);
            }
            finally
            {
                options.ClearItems(Dispose: true);
            }
        }

        private static async Task<string> AskFullHostNameAsync(FileLocationData ChosenLocation)
        {
            if (ChosenLocation == null)
                return null;

            using var title = TextBuilder.Get()
                .AppendColored("yellow", $"New {OSSEOUS_ASH} Host");

            using var message = TextBuilder.Get()
                .Append("Adding to location:")
                .AppendLine().Append(ChosenLocation.Tag()).Append(" ")
                .Append(ChosenLocation.SanitizeForDisplay())
                .AppendLine().AppendLine();

            message.Append($"Enter the full host name of the host you'd like to add, including the protocol and, if applicable, the port number.")
                .AppendLine().AppendLine();
            message.Append("Examples:")
                .AppendLine().Append("https://osseousash.cloud/")
                .AppendLine().Append("http://localhost:8000/api/")
                .AppendLine().Append("http://192.168.0.25:8080/api/v1/")
                .AppendLine().AppendLine();
            message.Append("You'll be asked to confirm the parsed result.");

            return await UIUtils.AskStringAsync(
                Message: message.ToString(),
                Title: title.ToString(),
                Default: "",
                ReturnNullForEscape: true,
                AllowColorize: false);
        }

        private static async Task<int?> AskHostTimeoutAsync(string PrependMessage)
        {
            using var title = TextBuilder.Get()
                .AppendColored("yellow", $"New {OSSEOUS_ASH} Host");

            using var message = TextBuilder.Get();
            if (!PrependMessage.IsNullOrEmpty())
                message.Append(PrependMessage)
                    .AppendLine().AppendLine();

            message.Append($"Enter the amount of time in milliseconds (-1 for unlimited) to wait before cancelling most requests to this host.")
                .AppendLine().AppendLine();
            message.Append("You can change/update this later if necessary.");

            return await UIUtils.AskNumberAsync(
                Message: message.ToString(),
                Title: title.ToString(),
                Start: DefaultHost.TimeoutMS,
                Min: -1);
        }

        private static async Task<int?> AskHostMaxUpAttemptsAsync(string PrependMessage)
        {
            using var title = TextBuilder.Get()
                .AppendColored("yellow", $"New {OSSEOUS_ASH} Host");

            using var message = TextBuilder.Get();
            if (!PrependMessage.IsNullOrEmpty())
                message.Append(PrependMessage)
                    .AppendLine().AppendLine();

            message.Append($"Enter the maximum number of times this host should attempt to upload a bones file (including the initial attempt).")
                .AppendLine().Append("This refers specifically to the json/info component of a bones file. A failed .sav.gz upload will re-attempt until the 3 minute window has passed.")
                .AppendLine()
                .AppendLine().Append("You can change/update this later if necessary.");

            return await UIUtils.AskNumberAsync(
                Message: message.ToString(),
                Title: title.ToString(),
                Start: DefaultHost.MaxUploadAttempts,
                Min: 1);
        }

        private static async Task<int?> AskHostVersionAsync(string PrependMessage)
        {
            using var title = TextBuilder.Get()
                .AppendColored("yellow", $"New {OSSEOUS_ASH} Host");

            using var message = TextBuilder.Get();
            if (!PrependMessage.IsNullOrEmpty())
                message.Append(PrependMessage)
                    .AppendLine().AppendLine();

            message.Append($"Enter the API version used by this host.")
                .AppendLine().Append("Some hosts will report their version number if you go to [host name]/api/status in a web browser.")
                .AppendLine()
                .AppendLine().Append("You can change/update this later if necessary.");

            return await UIUtils.AskNumberAsync(
                Message: message.ToString(),
                Title: title.ToString(),
                Start: DefaultHost.Version,
                Min: 0);
        }

        private static async Task<bool?> ConfirmParsedHost(Host NewHost)
        {
            if (NewHost == null)
            {
                await ShowCancelledAddHost();
                return false;
            }

            using var tB = TextBuilder.Get("The host you've entered was parsed in the following way:")
                .AppendLine()
                .AppendLine().AppendPair(nameof(NewHost.Name), NewHost.Name)
                .AppendLine().AppendPair(nameof(NewHost.Port), (NewHost.Port)?.ToString() ?? "")
                .AppendLine().AppendPair(nameof(NewHost.Encrypted), NewHost.Encrypted)
                .AppendLine().AppendPair(nameof(NewHost.TimeoutMS), NewHost.GetTimeoutString())
                .AppendLine().AppendPair(nameof(NewHost.Version), $"{NewHost.Version}{(NewHost.GetAPIVersionStub() is string apiStub ? $" ({apiStub})" : null)}")
                .AppendLine()
                .AppendLine().Append(NewHost.GetHostNameWithProtocol())
                .AppendLine().AppendPair("Server Status", NewHost.ServerStatusString)
                .AppendLine()
                .AppendLine().Append("Is this correct?");

            switch (await Popup.ShowYesNoCancelAsync(tB.ToString()))
            {
                case DialogResult.Yes:
                    return false;
                case DialogResult.No:
                    return true;
                case DialogResult.Cancel:
                default:
                    await ShowCancelledAddHost();
                    return null;
            }
        }

        private static async Task ShowCancelledAddHost()
            => await Popup.ShowAsync("Addition of new host cancelled.")
            ;

        private static async Task<UIUtils.CascadableResult> AskDoWhatWithHostAsync(HostSetHostPair HostParentPair)
        {
            using var indent = new Indent();
            Debug.LogCaller(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(HostParentPair), HostParentPair),
                });

            var host = HostParentPair.Host;
            var hostSet = HostParentPair.HostSet;

            var locationData = hostSet.LocationData;
            var options = new CascadablePickOptionDataSetAsync<HostSetHostPair>(HostParentPair, UIUtils.CascadableResult.Back, UIUtils.CascadableResult.Cancel);
            UIUtils.CascadableResult result;
            using var tB = TextBuilder.Get();
            try
            {
                do
                {
                    int priority = 1;
                    tB.Clear();
                    options.ClearItems(Dispose: true);

                    tB.Append($"What would you like to do with host ").Append(host.GetHostNameWithProtocol()).Append(", ")
                        .Append("which is in the below location:")
                        .AppendLine()
                        .AppendLine().Append(hostSet.TaggedDisplayName())
                        .AppendLine()
                        .AppendLine().AppendPair("Server Status", host.ServerStatusString);
                    if (host.IsOnCooldown)
                        tB.AppendLine().AppendColored("C", "Host status checks on cooldown.");
                    tB.AppendLine()
                        .AppendLine().AppendPair("Registered as", host.User?.Handle ?? "[not yet registered]");
                    if (host.User != null)
                        tB.AppendLine().AppendPair(nameof(host.User.Status), host.User.Status)
                            .AppendLine().AppendPair(nameof(host.User.Access), host.User.Access);
                    tB.AppendLineEnd();

                    // Utils.Log($"if {nameof(host)}.{nameof(host.IsOnCooldown)}");
                    int coolDownPriority = priority++ * 5;
                    if (host.IsOnCooldown)
                    {
                        // Utils.Log($"add clear timeout cooldown");
                        options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                        {
                            Text = "clear timeout cooldown",
                            Priority = coolDownPriority, // 5
                            Hotkey = 'c',
                            Callback = PerformClearTimeoutAsync,
                        });
                    }
                    // Utils.Log($"add modify");
                    options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                    {
                        Text = "modify",
                        Priority = priority++ * 5, // 10
                        Hotkey = 'm',
                        Callback = PerformModifyHostAsync
                    });
                    // Utils.Log($"add migrate");
                    options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                    {
                        Text = "migrate",
                        Priority = priority++ * 5, // 15
                        Hotkey = 'M',
                        Callback = PerformMigrateHostAsync
                    });
                    // Utils.Log($"add delete");
                    options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                    {
                        Text = "delete",
                        Priority = priority++ * 5, // 20
                        Hotkey = 'd',
                        Callback = PerformDeleteHostAsync,
                    });
                    options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                    {
                        Text = $"manage registration: {(host.User != null ? host.User.Handle : "unregistered")?.Colored("rules")}",
                        Priority = priority++ * 5, // 25
                        Hotkey = 'r',
                        Callback = async delegate (HostSetHostPair p)
                        {
                            if (p.Host.IsOnCooldown
                                || !p.Host.IsRunning)
                            {
                                await Popup.ShowAsync($"Connection to {p.Host} is {(p.Host.IsOnCooldown ? "temporarily" : "currently")} unavailable.");
                                return UIUtils.CascadableResult.Continue;
                            }
                            return await PerformManageRegistrationAsync(p);
                        },
                    });

                    bool hostCanEndSessionOrIsEnabled = host.HasSession
                        || (!host.IsOnCooldown
                            && host.IsRunning
                            && host.User != null);

                    coolDownPriority = priority++ * 5;
                    if (hostCanEndSessionOrIsEnabled)
                    {
                        options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                        {
                            Text = !host.HasSession ? $"start session" : $"end session",
                            Priority = coolDownPriority, // 30
                            Hotkey = 's',
                            Callback = async delegate (HostSetHostPair p)
                            {
                                if (p.Host.HasSession)
                                {
                                    using var message = TextBuilder.Get($"Are you sure you want to end your session?")
                                        .AppendLine()
                                        .AppendLine().Append("You will be prompted to start a new session the next time a connection is attempted with this host.")
                                        .AppendLine()
                                        .AppendLine().Append("If you would like to stop future connections to this host, you may need to disable it first via '")
                                            .AppendColored("W", "[m]").Append(" modify'.");

                                    switch (await Popup.ShowYesNoCancelAsync(message.ToString()))
                                    {
                                        case DialogResult.No:
                                            return UIUtils.CascadableResult.Continue;
                                        case DialogResult.Cancel:
                                            return UIUtils.CascadableResult.BackSilent;
                                        default:
                                            break;
                                    }
                                }
                                p.Host.JwtToken = !p.Host.HasSession
                                    ? await p.Host.GetSessionAsync(PushView: true)
                                    : null
                                    ;
                                return UIUtils.CascadableResult.Continue;
                            },
                        });
                    }
                    // Utils.Log($"call delegate");
                    ServerUtils.InvokeDoWhatWithHostPickOptionsDelegates(HostParentPair, ref options);

                    // Utils.Log($"result {nameof(UIUtils.PerformPickOptionAsync)}");
                    result = await UIUtils.PerformPickOptionAsync(
                        OptionDataSet: options,
                        Title: $"Manage {OSSEOUS_ASH} Host".Colored("yellow"),
                        Intro: tB.ToString(),
                        IntroIcon: hostSet.GetAshCloudIcon());
                }
                while (result.IsContinue());
            }
            finally
            {
                options.Dispose();
            }

            if (result.IsCancel())
                return result;

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformClearTimeoutAsync(HostSetHostPair HostParentPair)
        {
            var host = HostParentPair.Host;
            string hostName = host.GetHostNameWithProtocol();
            string message = $"Are you sure you want clear the timeout cooldown for {hostName}?";
            if ((await Popup.ShowYesNoCancelAsync(message)) == DialogResult.Yes)
                host.ManuallyClearStatusCheckTimer(ReasonForOverride: $"{hostName} was cleared by player via options menu");

            message = $"Timeout cooldown for {hostName} cleared.";
            if (HostParentPair.Host.IsOnCooldown)
                message = $"Failed to clear timeout cooldown for {hostName}.";
            else
            {
                if (await host.GetUserAsync(CatchBadRequest: true) is not User user)
                    await PerformManageRegistrationAsync(HostParentPair);
                else
                    user.CopyTo(ref host.User);
            }

            await Popup.ShowAsync(message);

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformModifyNameAsync(Host Host)
        {
            Host.Name = await Popup.AskStringAsync(
                Message: $"Enter a new {nameof(Host.Name)}:",
                Default: Host.Name,
                ReturnNullForEscape: true,
                AllowColorize: false);

            return !Host.Name.IsNullOrEmpty()
                ? UIUtils.CascadableResult.Continue
                : UIUtils.CascadableResult.Back
                ;
        }

        private static async Task<UIUtils.CascadableResult> PerformModifyPortAsync(Host Host)
        {
            Host.Port = await Popup.AskNumberAsync(
                Message: $"Enter a new {nameof(Port)}:",
                Start: Host.Port ?? 0,
                Min: 0);

            return Host.Port == null
                    || Host.Port >= 0
                ? UIUtils.CascadableResult.Continue
                : UIUtils.CascadableResult.Back
                ;
        }

        private static async Task<UIUtils.CascadableResult> PerformToggleEncryptedAsync(Host Host)
            => await FlipEncryptedAsync(Host)
            ? UIUtils.CascadableResult.Continue
            : UIUtils.CascadableResult.Back
            ;

        private static async Task<UIUtils.CascadableResult> PerformModifyTimeoutAsync(Host Host)
        {
            var number = await Popup.AskNumberAsync(
                Message: $"Enter a new Timeout (ms), or -1 for no/unlimited timeout:",
                Start: Host.TimeoutMS,
                Min: -1);

            if (!number.HasValue)
                return UIUtils.CascadableResult.Back;

            Host.TimeoutMS = number.GetValueOrDefault();

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformModifyMaxUpAttemptsAsync(Host Host)
        {
            var number = await Popup.AskNumberAsync(
                Message: $"Enter a new Maximum Upload Attempts:",
                Start: Host.MaxUploadAttempts,
                Min: 1);

            if (!number.HasValue)
                return UIUtils.CascadableResult.Back;

            Host.TimeoutMS = number.GetValueOrDefault();

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformModifyVersionAsync(Host Host)
        {
            var number = await Popup.AskNumberAsync(
                Message: $"Enter a new API Version:",
                Start: Host.Version,
                Min: 0);

            if (!number.HasValue)
                return UIUtils.CascadableResult.Back;

            Host.Version = number.GetValueOrDefault();

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformToggleEnabledAsync(Host Host)
            => await FlipEnabledAsync(Host)
            ? UIUtils.CascadableResult.Continue
            : UIUtils.CascadableResult.Back
            ;

        private static async Task<UIUtils.CascadableResult> PerformModifyHostAsync(HostSetHostPair Pair)
        {
            if (Pair?.IsValid() is not true)
            {
                await Popup.ShowAsync($"Something whent wrong with {Pair?.Host?.GetHostNameWithProtocol() ?? "MISSING_HOST"} in {Pair?.HostSet?.DisplayName() ?? "MISSING_SET"}");
                return UIUtils.CascadableResult.Continue;
            }

            var hostSet = Pair.HostSet;
            var host = Pair.Host;
            using var oldHost = host.Clone();
            host.Unbuild();
            UIUtils.CascadableResult result;
            using var tB = TextBuilder.Get();
            Dictionary<int, Func<Task<UIUtils.CascadableResult>>> buttonCallbacks = null;
            bool wasRunningBefore = host.IsRunning;
            using var options = new CascadablePickOptionDataSetAsync<Host>(host, UIUtils.CascadableResult.Back, UIUtils.CascadableResult.Cancel);
            do
            {
                int priority = 1;
                tB.Clear();
                options.ClearItems(Dispose: true);

                tB.Append($"Use the options below to modify host ").Append(host.GetHostNameWithProtocol()).Append(", ")
                    .Append("which is in the below location:")
                    .AppendLine()
                    .AppendLine().Append(hostSet.TaggedDisplayName())
                    .AppendLine()
                    .AppendLine().AppendPair("Server Status", host.ServerStatusString)
                    .AppendLineEnd();

                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = $"Change {nameof(host.Name)}: {host.Name?.Colored("rules")}",
                    Priority = priority++ * 5, // 5
                    Hotkey = 'n',
                    Callback = PerformModifyNameAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = $"Change {nameof(host.Port)}: {(host.Port?.ToString() ?? "none")?.Colored("rules")}",
                    Priority = priority++ * 5, // 10
                    Hotkey = 'p',
                    Callback = PerformModifyPortAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = host.Encrypted.GetCheckboxText(nameof(host.Encrypted)),
                    Priority = priority++ * 5, // 15
                    Hotkey = 'e',
                    Callback = PerformToggleEncryptedAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = $"Change Timeout (ms): {host.GetTimeoutString()?.Colored("rules")}",
                    Priority = priority++ * 5, // 20
                    Hotkey = 't',
                    Callback = PerformModifyTimeoutAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = $"Change Max Upload Attempts: {host.MaxUploadAttempts.ToString().Colored("rules")}",
                    Priority = priority++ * 5, // 25
                    Hotkey = 'm',
                    Callback = PerformModifyMaxUpAttemptsAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = $"Change API {nameof(host.Version)}: {host.Version.ToString().Colored("rules")} ({host.GetAPIVersionStub()})",
                    Priority = priority++ * 5, // 30
                    Hotkey = 'v',
                    Callback = PerformModifyVersionAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = host.GetEnabledCheckbox(),
                    Priority = priority++ * 5, // 35
                    Hotkey = 'E',
                    Callback = PerformToggleEnabledAsync,
                });

                List<QudMenuItem> buttons = null;
                buttonCallbacks?.Clear();
                buttonCallbacks = null;

                if (!host.SameAs(oldHost))
                {
                    options.Add(new CascadablePickOptionDataAsync<Host>
                    {
                        Text = "revert changes"?.Colored("w"),
                        Priority = priority++ * 5, // 45
                        Hotkey = 'u',
                        Icon = new Renderable(
                            Tile: "UI/sw_lastchar.bmp",
                            ColorString: $"&W",
                            TileColor: $"&W",
                            DetailColor: 'w'),
                        Callback = h => Task.Run(delegate ()
                        {
                            oldHost.CopyTo(ref h, SetBuiltTo: true);
                            return UIUtils.CascadableResult.Continue;
                        }),
                    });

                    buttons = UIUtils.SaveButton;
                    buttonCallbacks = new()
                    {
                        { -3, () => PerformSaveHostAsync(host, oldHost, hostSet) }
                    };
                }

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: $"Modify {OSSEOUS_ASH} Host".Colored("yellow"),
                    Intro: tB.ToString(),
                    IntroIcon: hostSet.GetAshCloudIcon(),
                    AdditionalButtons: buttons,
                    ButtonCallbacks: buttonCallbacks,
                    FinalSelectedCallback: async delegate (PickOptionData<Host, Task<UIUtils.CascadableResult>> o, Task<UIUtils.CascadableResult> r)
                    {
                        var result = (await r?.AwaitResultIfNotIsCompletedSuccessfully());
                        if (result is not UIUtils.CascadableResult.Continue
                            && !(await AskSureLoseUnsavedAsync(host, oldHost)))
                            r = Task.Run(() => UIUtils.CascadableResult.Continue);
                        else
                        if (result is not UIUtils.CascadableResult.Continue
                            && o?.Element == null)
                            oldHost.CopyTo(ref host, SetBuiltTo: true, ThenDispose: true);

                        return await UIUtils.ShowEscancellepedAsync(o, r);
                    });
            }
            while (result.ToBool());

            host.Build(Ping: true);

            if (wasRunningBefore != host.IsRunning)
                ServerUtils.ClearLocalReportsCache();

            if (result.IsCancel())
                return result;

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformSaveHostAsync(Host Host, Host OldHost, HostSet HostCollection)
        {
            var result = await ConfirmModifiedHostAsync(OldHost, Host);

            if (result.IsCancel())
                return UIUtils.CascadableResult.Cancel;

            if (result.IsContinue())
            {
                HostCollection.Write();
                Host.CopyTo(ref OldHost, SetBuiltTo: true);
                return UIUtils.CascadableResult.BackSilent;
            }
            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformMigrateHostAsync(HostSetHostPair Pair)
        {
            if (Pair?.IsValid() is not true)
            {
                await Popup.ShowAsync($"Something whent wrong with {Pair?.Host?.GetHostNameWithProtocol() ?? "MISSING_HOST"} in {Pair?.HostSet?.DisplayName() ?? "MISSING_SET"}");
                return UIUtils.CascadableResult.Continue;
            }

            var originHostSet = Pair.HostSet;
            var host = Pair.Host;
            using var options = new CascadablePickOptionDataSetAsync<HostSet>(UIUtils.CascadableResult.Back, UIUtils.CascadableResult.Cancel);
            UIUtils.CascadableResult result;
            do
            {
                using var hostSets = ScopeDisposedList<HostSet>.GetFromPoolFilledWith(ServerUtils.Hosts);
                hostSets.Remove(originHostSet);

                options.ClearItems(Dispose: true);
                options.Add(new CascadablePickOptionDataAsync<HostSet>
                {
                    Element = originHostSet,
                    Text = $"Leave in {originHostSet.TaggedDisplayName()}".Colored("black"),
                    Hotkey = 'x',
                    Callback = hc => Task.Run(() => UIUtils.CascadableResult.BackSilent),
                });

                using var excludedHotkeys = ScopeDisposedList<char>.GetFromPoolFilledWith(options.GetHotkeys());
                foreach (var destinationHostSet in hostSets)
                {
                    options.Add(new CascadablePickOptionDataAsync<HostSet>
                    {
                        Element = destinationHostSet,
                        Text = destinationHostSet.TaggedDisplayName(),
                        Hotkey = options.GetHotkeys().GetNextHotKey(Excluding: excludedHotkeys),
                        Callback = async delegate (HostSet hs)
                        {
                            if (host == null)
                                Utils.Log($"{nameof(host)} is null");
                            if (originHostSet == null)
                                Utils.Log($"{nameof(originHostSet)} is null");
                            if (hs == null)
                                Utils.Log($"{nameof(hs)} is null");

                            if ((await Popup.ShowYesNoCancelAsync(
                                Message: $"Confirm migration of {host.GetHostNameWithProtocol()}\n\n" +
                                    $"From:\n" +
                                    $"{originHostSet.TaggedDisplayName()}\n\n" +
                                    $"To:\n" +
                                    $"{hs.TaggedDisplayName()}"
                                )) == DialogResult.Yes)
                            {
                                hs.WriteRemoveHost(host, Dispose: false);
                                hs.WriteAddHost(host);
                            }
                            return UIUtils.CascadableResult.BackSilent;
                        }
                    });
                }

                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: $"Migrate {OSSEOUS_ASH} Host".Colored("yellow"),
                    Intro: $"Which location would you like to migrate host {host.GetHostNameWithProtocol()} to?\n\n",
                    IntroIcon: originHostSet.GetAshCloudIcon());
            }
            while (result.IsContinue());

            if (result.IsCancel())
                return result;

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformManageRegistrationAsync(HostSetHostPair Pair)
        {
            var host = Pair.Host;

            string purpose = "Register";
            string message = "You aren't currently registered with this host.\nBefore registering, if you would like to set a password for use with this host, please enter it below:";

            var user = await host.GetUserAsync(CatchBadRequest: true);
            user?.CopyTo(ref host.User);
            user?.Dispose();

            bool update = false;
            if (host.User != null)
            {
                purpose = "Update";
                message = "Please enter your new password below:";
                update = true;
            }

            if (await host.AskWhatPasswordAsync(
                Purpose: purpose,
                Message: message,
                IncludeNote: true) is string newPassword)
            {
                if (newPassword == null)
                    return UIUtils.CascadableResult.Continue;

                if (await host.CheckRegisterUserAsync(newPassword, update))
                    return UIUtils.CascadableResult.Continue;
            }

            return UIUtils.CascadableResult.Back;
        }

        private static async Task<UIUtils.CascadableResult> PerformDeleteHostAsync(HostSetHostPair Pair)
        {
            if (Pair?.IsValid() is not true)
            {
                await Popup.ShowAsync($"Something whent wrong with {Pair?.Host?.GetHostNameWithProtocol() ?? "MISSING_HOST"} in {Pair?.HostSet?.DisplayName() ?? "MISSING_SET"}");
                return UIUtils.CascadableResult.Continue;
            }

            var hostSet = Pair.HostSet;
            var host = Pair.Host;

            string message = $"Are you sure you want to delete {host.GetHostNameWithProtocol()} from {hostSet.DisplayName()}?";
            if ((await Popup.ShowYesNoCancelAsync(message)) != DialogResult.Yes)
                return UIUtils.CascadableResult.Continue;

            hostSet.WriteRemoveHost(host, Dispose: true);
            return UIUtils.CascadableResult.BackSilent;
        }

        private static async Task<bool> AskSureLoseUnsavedAsync(Host Host, Host OldHost)
            => Host is null // Utils.LogReturn($"Host is null", Host is null)
            || Host.SameAs(OldHost) // Utils.LogReturn($"Host.SameAs(OldHost)", Host.SameAs(OldHost))
            || (await Popup.ShowYesNoCancelAsync("Your unsaved changes will be lost.\n\nAre you sure?")) == DialogResult.Yes
            //|| Utils.LogReturn($"Are you sure? DialogResult.Yes", (await Popup.ShowYesNoCancelAsync("Your unsaved changes will be lost.\n\nAre you sure?")) == DialogResult.Yes)
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
                    Value: $"{OldHost.Encrypted.GetCheckbox()} \u001a {ModifiedHost.Encrypted.GetCheckbox()}");
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

            if (OldHost.GetEnabledValueForMenu() != ModifiedHost.GetEnabledValueForMenu())
            {
                any = true;
                sB.AppendLine().AppendPair(
                    Key: nameof(ModifiedHost.Enabled),
                    Value: $"{OldHost.GetEnabledCheckbox()} \u001a {ModifiedHost.GetEnabledCheckbox()}");
            }

            if (!any)
                sB.AppendLine().Append("No changes, this is an errored state to be in.");

            sB.AppendLine();
            sB.AppendLine().Append("New host:");
            sB.AppendLine().Append(ModifiedHost.GetHostNameWithProtocol())
                .AppendLine()
                .AppendLine().AppendPair("Server Status", ModifiedHost.ServerStatusString)
                .AppendLine()
                .AppendLine().Append("Is this correct?");

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

        public void Dispose(bool DisposePending)
        {
            Name = null;
            Port = null;
            Encrypted = false;
            AuthToken = null;
            Version = 0;
            Enabled = false;
            JwtToken = null;
            WrittenEnabled = false;
            ClearStatusCheckTimer(ref StatusCheckTimer);
            ClearBonesIDCacheTimer(ref BonesIDCacheTimer);
            BonesIDCache?.Clear();
            BonesIDCache = null;
            if (DisposePending)
            {
                foreach (var pendingUpload in (PendingUploads?.Values).IteratorSafe())
                    pendingUpload.Dispose();

                if (PendingUploadCache.ContainsKey(PendingUploadCacheKey))
                    PendingUploadCache.Remove(PendingUploadCacheKey);
            }
            PendingUploadCacheKey = null;
            AuthToken = null;
            User?.Dispose();
            User = null;
        }

        public void Dispose()
            => Dispose(DisposePending: false)
            ;

        #region BaseBonesSource

        public override bool HasInfo(string BonesID)
            => ListBonesIDs().Contains(BonesID)
            ;

        public override bool HasSavGz(string BonesID)
            => ListBonesIDs().Contains(BonesID)
            ;

        public override bool IsBonesBlocked(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()} blocking {nameof(SaveBonesInfo)} is not currently supported.", new NotImplementedException());
            return false;
        }

        public override async Task<SaveBonesInfo> LoadInfoAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()} loading {nameof(SaveBonesInfo)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => (SaveBonesInfo)null);
        }

        public override async Task<IEnumerable<SaveBonesInfo>> LoadInfosAsync(IEnumerable<string> BonesIDs = null)
            => (await GetEnumerableFromRouteAsync<SaveBonesJSON>(
                Route: Routes.BonesInfosByIDs,
                ProcBody: async delegate (StreamWriter sw)
                {
                    if (BonesIDs != null)
                        await sw.WriteJsonObjectAsync(new { BonesIDs = BonesIDs.ToArray() });
                },
                ProcEach: sbj => sbj.MarkAsOnline(this),
                Parameters: new RouteParam[1]
                {
                    new(nameof(UserID), UserID),
                })
                ).Select(sbj => SaveBonesJSON.CreateInfoFrom(SaveBonesJSON: sbj, BonesSource: this))
            ;

        public override async Task<BonesSpec> LoadSpecAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()} loading {nameof(BonesSpec)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => (BonesSpec)null);
        }

        public override async Task<ModRecordCollection> LoadModConfigAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()} loading {nameof(ModRecordCollection)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => (ModRecordCollection)null);
        }

        public override async Task<BonesStats> LoadStatsAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()} loading {nameof(BonesStats)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => (BonesStats)null);
        }

        public async Task<byte[]> GetBonesSavGzAsync(string BonesID)
        {
            if (!IsRunning)
                return null;

            if (BonesID.IsNullOrEmpty())
                return null;

            if (!await CheckRegisterUserAsync())
                return null;

            string uRI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.LoadBones;
            try
            {
                httpReq = CreateWebRequest(route, out uRI, new RouteParam[] { new(nameof(BonesID), BonesID) });
            }
            catch (Exception x)
            {
                route.ErrorFailed("creating", nameof(HttpWebRequest), uRI, x);
                return null;
            }

            if (httpReq == null)
                return null;

            try
            {
                var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                TryUpdateJwtToken(httpRes);
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    try
                    {
                        if ((await streamReader.ReadAllBytesAsync()) is byte[] rawBuffer)
                        {
                            string sizeString = Buffer.ByteLength(rawBuffer).Things(typeof(byte).Name);
                            Utils.Log($"{nameof(GetBonesSavGzAsync)} got a {typeof(byte[]).ToStringWithGenerics()} from {ToString()} ({sizeString})" +
                                $" - {httpRes.StatusCode.DisplayString()}");
                            return rawBuffer;
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"{nameof(GetBonesSavGzAsync)} failed to get a Bones SavGz from {ToString()}" +
                            $" - {httpRes.StatusCode.DisplayString()}", x);
                        return null;
                    }
                }
                Utils.Warn($"{nameof(GetBonesSavGzAsync)} failed to get a Bones SavGz from {ToString()}" +
                    $" - {httpRes.StatusCode.DisplayString()}");
            }
            catch (WebException x)
            {
                if (!HandleTimeoutWebException(x, uRI, GetTimeout()))
                    throw x;

                if (x.Is403Exception())
                {
                    TryUpdateJwtToken(null);
                    throw x;
                }
            }
            catch (Exception x)
            {
                route.ErrorFailed("receiving", nameof(HttpWebResponse), uRI, x);
            }
            return null;
        }

        public override async Task<BonesSavGz> LoadSavGzAsync(string BonesID)
        {
            if (await GetBonesSavGzAsync(BonesID) is byte[] savGz)
                return new(BonesID, savGz);

            return null;
        }

        public override async Task<ActionResult> IncrementStatAsync(string BonesID, string StatName)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()}.{nameof(IncrementStat)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => ActionResult.NewFailed().SetMessage("Unsupported"));
        }

        public HttpWebRequest CreatePostBonesInfoRequest(
            PendingUpload PendingUpload,
            out string URI,
            bool Silent = false
            )
        {
            URI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.NewBonesInfo;
            try
            {
                httpReq = CreateWebRequest(
                    Route: route,
                    Proc: async sw => await sw.WriteJsonObjectAsync(PendingUpload.BonesRecord),
                    URI: out URI);
            }
            catch (Exception x)
            {
                route.ErrorFailed($"creating", nameof(HttpWebRequest), URI, x);
                return null;
            }

            if (httpReq == null)
            {
                if (!Silent)
                    route.WarnFailed($"creating", nameof(HttpWebRequest), URI);
            }
            return httpReq;
        }

        public async Task<HttpWebResponse> GetPostBonesInfoResponseAsync(
            HttpWebRequest HttpReq,
            string URI
            )
        {
            HttpWebResponse httpRes = null;
            string warnMessage = null;
            try
            {
                httpRes = (HttpWebResponse)await HttpReq.GetResponseAsync();
                TryUpdateJwtToken(httpRes);
            }
            catch (WebException x)
            {
                if (!HandleTimeoutWebException(x, URI, GetTimeout()))
                {
                    warnMessage = $"; Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during resposne from {ToString()}";
                    throw x;
                }
                if (x.Is403Exception())
                {
                    warnMessage = $"; Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during resposne from {ToString()}";
                    TryUpdateJwtToken(null);
                    throw x;
                }
            }
            catch (Exception x)
            {
                warnMessage ??= $"; Caught {x.GetType().ToStringWithGenerics()} during resposne from {ToString()}";
                Routes.NewBonesInfo.ErrorFailed($"receiving", nameof(HttpWebResponse), URI, x, warnMessage);
            }
            return httpRes;
        }

        public HttpWebRequest CreatePutBonesSavGzRequest(
            PendingUpload PendingUpload,
            out string URI,
            bool Silent = false
            )
        {
            URI = null;
            HttpWebRequest httpReq = null;
            var route = Routes.NewBonesSavGz;
            try
            {
                httpReq = CreateWebRequest(
                    Route: route,
                    Proc: async delegate (StreamWriter streamWriter)
                    {
                        using (var savGzStream = new System.IO.MemoryStream(PendingUpload.SavGz))
                        {
                            await savGzStream.CopyToAsync(streamWriter.BaseStream);
                        }
                    },
                    URI: out URI,
                    RouteParams: new RouteParam[]
                    {
                        new(nameof(PendingUpload.BonesID), PendingUpload.BonesID)
                    });

                PendingUpload.AppendAuthHeader(httpReq);
            }
            catch (Exception x)
            {
                route.ErrorFailed($"creating", nameof(HttpWebRequest), URI, x);
                return null;
            }

            if (httpReq == null)
            {
                if (!Silent)
                    route.WarnFailed($"creating", nameof(HttpWebRequest), URI);
            }
            return httpReq;
        }

        public async Task<HttpWebResponse> GetPutBonesSavGzResponseAsync(
            HttpWebRequest HttpReq,
            string URI,
            bool Rethrow = false
            )
        {
            HttpWebResponse httpRes = null;
            string warnMessage = null;
            try
            {
                httpRes = (HttpWebResponse)await HttpReq.GetResponseAsync();
                TryUpdateJwtToken(httpRes);
            }
            catch (WebException x)
            {
                if (!HandleTimeoutWebException(x, URI, GetTimeout()))
                {
                    warnMessage = $"; Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during response from {ToString()}";
                    throw x;
                }
                if (x.Is403Exception())
                {
                    warnMessage = $"; Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during resposne from {ToString()}";
                    TryUpdateJwtToken(null);
                    throw x;
                }
            }
            catch (Exception x)
            {
                warnMessage ??= $"; Caught {x.GetType().ToStringWithGenerics()} during response from {ToString()}";
                Routes.NewBonesSavGz.ErrorFailed($"receiving", nameof(HttpWebResponse), URI, x, warnMessage);
                if (Rethrow)
                    throw x;
            }
            return httpRes;
        }

        public override async Task<ActionResult> SaveInfoAsync(SaveBonesJSON BonesJSON)
            => await SaveBonesAsync(BonesJSON, LoadSavGz(BonesJSON.BonesID))
            ;

        public override async Task<ActionResult> SaveSavGzAsync(BonesSavGz BonesSavGz)
            => await SaveBonesAsync(LoadInfo(BonesSavGz.BonesID).JSON, BonesSavGz)
            ;

        public virtual bool SaveBones(SaveBonesJSON BonesJSON, BonesSavGz BonesSavGz, out ResponseInfo ResponseInfo)
        {
            ResponseInfo = new ResponseInfo
            {
                Host = this,
                StatusCode = HttpStatusCode.InternalServerError,
                Success = false,
                Message = "Host is not running",
            };
            if (!IsRunning)
                return false;

            string bonesID = BonesJSON.BonesID;
            if (PendingUploads.TryGetValue(bonesID, out var pendingUpload))
            {
                if (pendingUpload.BonesRecord.SaveBonesJSON.SaveTimeValue >= BonesJSON.SaveTimeValue)
                {
                    ResponseInfo.StatusCode = HttpStatusCode.NotModified;
                    ResponseInfo.Message = "Host already has newer bones with this ID";
                    return true;
                }

                pendingUpload.ForceDispose = true;
                pendingUpload.Dispose();
            }

            PendingUploads[bonesID] = new PendingUpload
            {
                ParentHost = this,
                BonesID = bonesID,
                BonesRecord = BonesJSON.GetBonesRecord(),
                BonesSavGz = BonesSavGz.Clone(),
            };

            ResponseInfo = PendingUploads[bonesID].Attempt();
            return true;
        }

        public override async Task<ActionResult> SaveBonesAsync(SaveBonesJSON BonesJSON, BonesSavGz BonesSavGz)
            => await Task.Run(() =>
            {
                var saved = SaveBones(BonesJSON, BonesSavGz, out var responseInfo);
                var result = ActionResult.NewFailed();
                if (PendingUploads.ContainsKey(BonesJSON.ID))
                    return result.SetPending()
                        .SetMessage($"Bones upload pending, {responseInfo.Message}");

                if (saved)
                    return result.SetSuccess()
                        .SetMessage("Bones uploaded");

                return result
                    .SetMessage($"{responseInfo.StatusCode.DisplayString()} - {responseInfo.Message}");
            })
            ;

        public override async Task<ActionResult> BlockBonesAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()}.{nameof(BlockBones)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => ActionResult.NewFailed().SetMessage("Unsupported"));
        }

        public override async Task<ActionResult> UnblockBonesAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()}.{nameof(UnblockBones)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => ActionResult.NewFailed().SetMessage("Unsupported"));
        }

        public override async Task<ActionResult> DeleteInfoAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()}.{nameof(DeleteInfo)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => ActionResult.NewFailed().SetMessage("Unsupported"));
        }

        public override async Task<ActionResult> DeleteSavGzAsync(string BonesID)
        {
            Utils.WarnOnce($"{GetType().ToStringWithGenerics()}.{nameof(DeleteSavGz)} is not currently supported.", new NotImplementedException());
            return await Task.Run(() => ActionResult.NewFailed().SetMessage("Unsupported"));
        }

        private async Task<IEnumerable<T>> GetEnumerableFromWebRequestAsync<T>(
            Route Route,
            HttpWebRequest HttpReq,
            string URI,
            Func<T, T> ProcEach = null,
            [CallerMemberName] string CallingMethod = ""
            )
        {
            HttpStatusCode statusCode = default;
            string result = null;
            if (!await TryGetResultAndStatusAsync(
                HttpReq: HttpReq,
                Route: Route,
                URI: URI,
                StatusCodeCallback: sc => statusCode = sc,
                ResultCallback: r => result = r))
                return Enumerable.Empty<T>();

            // bool noContent = false;
            if (statusCode == HttpStatusCode.OK)
            {
                if (JObject.Parse(result) is JObject jObject)
                {
                    try
                    {
                        var output = jObject.ToObject<T[]>().IteratorSafe();

                        // Utils.Log($"{4.Indent()}Have a {nameof(result)}");
                        if (ProcEach == null)
                            return output;

                        return output.Select(ProcEach);
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"{CallingMethod} failed to deserialize {nameof(jObject)} to {typeof(T[])}", x);
                    }
                }
                else
                {
                    Utils.Warn($"{CallingMethod} received unexpected data from {ToString()}" +
                        $" - {statusCode.DisplayString()} - {result}");
                }
            }
            else
            if (statusCode == HttpStatusCode.NoContent)
            {
                // Utils.Log($"{4.Indent()}No Content");
                // noContent = true;
            }
            else
            {
                Utils.Warn($"{CallingMethod} received unexpected status code from {ToString()}" +
                    $" - {statusCode.DisplayString()}");
            }
            /*if (!noContent)
                Utils.Log($"{4.Indent()}Failed");*/
            return Enumerable.Empty<T>();
        }

        private async Task<IEnumerable<T>> GetEnumerableFromRouteAsync<T>(
            Route Route,
            Action<StreamWriter> ProcBody = null,
            Func<T, T> ProcEach = null,
            RouteParam[] Parameters = null,
            [CallerMemberName] string CallingMethod = ""
            )
        {
            // Utils.Log($"{3.Indent()}{nameof(GetEnumerableFromRouteAsync)}<{typeof(T).Name}>");
            if (!IsRunning)
                return Enumerable.Empty<T>();

            if (!await CheckRegisterUserAsync())
                return Enumerable.Empty<T>();

            string uRI = null;
            HttpWebRequest httpReq = null;
            try
            {
                // Utils.Log($"{3.Indent()}{nameof(CreateWebRequest)}");
                httpReq = CreateWebRequest(
                    Route: Route,
                    Proc: ProcBody,
                    URI: out uRI,
                    RouteParams: Parameters);
            }
            catch (Exception x)
            {
                Route.ErrorFailed("creating", nameof(HttpWebRequest), uRI, x);
                return Enumerable.Empty<T>();
            }

            if (httpReq == null)
                return Enumerable.Empty<T>();

            try
            {
                return await GetEnumerableFromWebRequestAsync(
                    Route: Route,
                    HttpReq: httpReq,
                    URI: uRI,
                    ProcEach: ProcEach,
                    CallingMethod: CallingMethod);
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(GetEnumerableFromRouteAsync)} failed calling {nameof(GetEnumerableFromWebRequestAsync)}", x);
            }
            return Enumerable.Empty<T>();
        }

        private async Task<IEnumerable<string>> GetBonesIDsAsync()
            => await GetEnumerableFromRouteAsync<string>(Routes.BonesIDs)
            ;

        public override async Task<IEnumerable<string>> ListBonesIDsAsync()
        {
            BonesIDCache ??= new();
            if (BonesIDCache.IsNullOrEmpty())
            {
                try
                {
                    BonesIDCache.AddRange(await GetBonesIDsAsync());
                }
                catch (Exception x)
                {
                    Utils.Warn($"{nameof(ListBonesIDs)} failed to {nameof(GetBonesIDsAsync)}", x);
                    return Enumerable.Empty<string>();
                }
            }
            return BonesIDCache;
        }

        private async Task<IEnumerable<SaveBonesJSON>> GetSaveBonesJSONsAsync()
        {
            ClearBonesIDCache(ref BonesIDCache);
            var bonesJSONs = await GetEnumerableFromRouteAsync<SaveBonesJSON>(
                Route: Routes.BonesInfos,
                Parameters: new RouteParam[1]
                {
                    new(nameof(UserID), UserID),
                });

            BonesIDCache.AddRange(bonesJSONs.Select(b => b.ID).Distinct());
            return bonesJSONs;
        }

        public async Task<IEnumerable<SaveBonesInfo>> GetSaveBonesInfosAsync()
        {
            var saveBonesInfos = new List<SaveBonesInfo>();
            // Utils.Log($"{2.Indent()}{nameof(GetSaveBonesInfosAsync)}");
            var bonesJSONs = (await GetSaveBonesJSONsAsync()).IteratorSafe();
            foreach (var bonesJSON in bonesJSONs)
            {
                if (saveBonesInfos.Any(bonesInfo => bonesInfo.ID == bonesJSON.ID))
                    continue;

                saveBonesInfos.Add(SaveBonesJSON.CreateInfoFrom(bonesJSON, this));
            }

            return saveBonesInfos;
        }

        public override async Task<IEnumerable<SaveBonesInfo>> ListBonesInfosAsync()
            => await GetSaveBonesInfosAsync()
            ;

        private async Task<IEnumerable<BonesSpec>> GetBonesSpecsAsync()
            => await GetEnumerableFromRouteAsync<BonesSpec>(Routes.BonesSpecs)
            ;

        public override async Task<IEnumerable<BonesSpec>> ListBonesSpecsAsync()
            => await GetBonesSpecsAsync()
            ;

        private async Task<IEnumerable<ModRecordCollection>> GetModConfigsAsync()
            => await GetEnumerableFromRouteAsync<ModRecordCollection>(Routes.BonesModConfigs)
            ;

        public override async Task<IEnumerable<ModRecordCollection>> ListModConfigsAsync()
            => await GetModConfigsAsync()
            ;

        public override async Task<IEnumerable<string>> GetBonesIDsMatchingSpecAsync(BonesSpec PlayerSpec)
            => await GetEnumerableFromRouteAsync<string>(
                Route: Routes.BonesIDsMatchingSpec,
                ProcBody: async sw => await sw.WriteJsonObjectAsync(new { PlayerSpec }))
            ;

        public override async Task<IEnumerable<ModRecordCollection>> GetModRecordsMatchingSpecAsync(BonesSpec PlayerSpec)
            => await GetEnumerableFromRouteAsync<ModRecordCollection>(
                Route: Routes.BonesModConfigsMatchingSpec,
                ProcBody: async sw => await sw.WriteJsonObjectAsync(new { PlayerSpec }))
            ;

        #endregion

        public Host Vomit(bool ForceName = false, Indent Indent = null, [CallerMemberName] string CallingMethod = "", params object[] Parameters)
            => (Host)IVomitable.Vomit(
                Object: this,
                ForceName: ForceName,
                Indent: Indent,
                CallingMethod: CallingMethod,
                Parameters: Parameters)
            ;

        public static implicit operator string(Host Host)
            => Host.ToString()
            ;
    }
}
