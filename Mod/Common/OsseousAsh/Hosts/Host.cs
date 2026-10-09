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

using UD_Bones_Folder.Mod.UI;
using UD_Bones_Folder.Mod.OsseousAsh;
using UD_Bones_Folder.Mod.Serialization;

using static UD_Bones_Folder.Mod.Const;
using static UD_Bones_Folder.Mod.OsseousAsh.HostSet;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [JsonObject(MemberSerialization.OptIn)]
    [Serializable]
    public class Host : IComposite, IEquatable<Host>, IDisposable
    {
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
            Version = DEFAULT_VERSION,
            Enabled = false,
        };

        public const int DEFAULT_VERSION = 1;
            
        public const int DEFAULT_TIMEOUT = 3000;

        public static long SeverStatusCheckInterval => 300000;

        private static int CurrentHostID = 0;

        public static Dictionary<string, Dictionary<string, PendingSavGz>> PendingSavGzCache = new();

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

        [JsonIgnore]
        private int? _HostID;
        [JsonIgnore]
        private int HostID => _HostID ??= CurrentHostID++;

        /// <summary>
        /// Name of the host, excluding protocol and any port number.
        /// </summary>
        [JsonProperty]
        public string Name;

        /// <summary>
        /// Optional port number for host.
        /// </summary>
        [JsonProperty]
        public int? Port;

        /// <summary>
        /// Determines http or https.
        /// </summary>
        [JsonProperty]
        public bool Encrypted;

        /// <summary>
        /// The number of milliseconds to wait for a response.
        /// </summary>
        [JsonProperty]
        public int TimeoutMS = DEFAULT_TIMEOUT;

        /// <summary>
        /// Current unused but will allow for effectively (optionally) password protecting a sever which will requiring this be assigned and sent along with the request.
        /// </summary>
        [JsonProperty]
        public string AuthToken;

        /// <summary>
        /// The API version this host expects to connect to.
        /// </summary>
        [JsonProperty]
        public int Version;

        /// <summary>
        /// The Routes made available by the API this host will connect to.
        /// </summary>
        [JsonIgnore]
        public RouteSet Routes => VersionRoutes.GetFor(Version);

        /// <summary>
        /// Indicates whether or not this host should be utilised.
        /// </summary>
        [JsonProperty]
        public bool Enabled;

        [JsonIgnore]
        private bool IsBuilt;

        [JsonIgnore]
        private string BuiltString => !IsBuilt
            ? " Unbuilt"
            : null
            ;

        [JsonIgnore]
        private bool? WrittenEnabled;

        [JsonIgnore]
        public bool IsOnCooldown => IsBuilt
            && !Enabled
            && WrittenEnabled.HasValue
            && WrittenEnabled.GetValueOrDefault()
            ;

        [JsonIgnore]
        public bool IsRunning
            => !VersionRoutes.Init
            && GetServerStatus()
            ;

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
        private IEnumerable<Report> _ReportsCache;
        [JsonIgnore]
        public IEnumerable<Report> ReportsCache => _ReportsCache ??= GetBonesReports();

        [JsonIgnore]
        private string PendingSavGzCacheKey;

        [JsonIgnore]
        public Dictionary<string, PendingSavGz> PendingSavGzs
        {
            get
            {
                if (!PendingSavGzCache.ContainsKey(PendingSavGzCacheKey))
                    PendingSavGzCache[PendingSavGzCacheKey] = new();

                return PendingSavGzCache[PendingSavGzCacheKey];
            }
        }

        public Host()
        {
            Enabled = true;
            PendingSavGzCacheKey = Guid.NewGuid().ToString();
            TimeoutMS = DEFAULT_TIMEOUT;
        }

        public Host(
            string Name,
            int? Port = null,
            bool Encrypted = false,
            string AuthToken = null,
            int Version = DEFAULT_VERSION,
            bool Enabled = true,
            int TimeoutMS = DEFAULT_TIMEOUT
            )
            : this()
        {
            this.Name = Name;
            this.Port = Port;
            this.Encrypted = Encrypted;
            this.AuthToken = AuthToken;
            this.Version = Version;
            this.Enabled = Enabled;
            this.TimeoutMS = TimeoutMS;
        }

        public Host(
            string HostName,
            string AuthToken = null,
            int TimeoutMS = DEFAULT_TIMEOUT
            )
            : this()
        {
            Parse(HostName, out Name, out Port, out Encrypted, out Version);
            if (!AuthToken.IsNullOrEmpty())
                this.AuthToken = AuthToken;

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

            Destination.PendingSavGzCacheKey = Source.PendingSavGzCacheKey;

            Destination.Name = Source.Name;
            Destination.Port = Source.Port;
            Destination.Encrypted = Source.Encrypted;
            Destination.TimeoutMS = Source.TimeoutMS;
            Destination.AuthToken = Source.AuthToken;
            Destination.Enabled = Source.Enabled;

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
        }

        public void Build(bool Ping = false)
        {
            IsBuilt = true;
            if (Ping)
            {
                _ = IsRunning;
                ClearReportsCache();
            }
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

        public string statusGetRoute()
        {
            string uRI = CombineHostRoute(IncludeVersion: false, status);
            if (Version > 0
                && Routes?.Status?.ParseValue() is string statusRoute)
                uRI = CombineHostRoute(IncludeVersion: false, statusRoute);
            return uRI;
        }

        public string canUpGetRoute(Guid UserID)
            => CombineHostRoute(canUp, UserID.ToString())
            ;

        public string BonesIDGetRoute(string BonesID = null)
            => CombineHostRoute(ID, BonesID)
            ;

        public string BonesInfoGetRoute(string BonesID = null)
            => CombineHostRoute(Info, BonesID)
            ;

        public string BonesSavGzGetRoute(string BonesID = null)
            => CombineHostRoute(SavGz, BonesID)
            ;

        public string BonesInfosGetRoute()
            => CombineHostRoute(Infos)
            ;

        public string BonesPostRoute()
            => CombineHostRoute(Bones)
            ;

        public string BonesSavGzPutRoute(string BonesID = null)
            => CombineHostRoute(SavGz, BonesID)
            ;

        public string BonesReportPostRoute()
            => CombineHostRoute(Report)
            ;

        public string BonesReportsGetRoute()
            => CombineHostRoute(Reports)
            ;

        public string BonesReportCheckGetRoute(string BonesID, Guid UserID)
            => CombineHostRoute(ReportCheck, BonesID, UserID.ToString())
            ;

        public string BonesStatsPutRoute(string BonesID, Guid UserID)
            => CombineHostRoute(Stats, BonesID, UserID.ToString())
            ;

        public string BonesDownloadPostRoute(string BonesID, Guid UserID)
            => CombineHostRoute(Download, BonesID, UserID.ToString())
            ;

        public string DisplayName()
            => NameWithPort(Name, Port)
            ;

        private static HttpWebRequest CreateWebRequest(
            string URI,
            string ContentType,
            WebMethods Method,
            int? Timeout = null,
            Action<System.IO.StreamWriter> Proc = null
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
                    using var streamWriter = new System.IO.StreamWriter(httpReq.GetRequestStream());
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
            Action<System.IO.StreamWriter> Proc,
            out string URI,
            params RouteParam[] Parameters
            )
        {
            URI = CombineHostRoute(IncludeVersion: IncludeVersion, Route.ParseValue(Parameters));
            return CreateWebRequest(
                URI: URI,
                ContentType: Route.ContentType,
                Method: Route.Method,
                Timeout: Timeout,
                Proc: Proc);
        }

        private HttpWebRequest CreateWebRequest(
            Route Route,
            bool IncludeVersion,
            int? Timeout,
            out string URI,
            params RouteParam[] Parameters
            )
            => CreateWebRequest(
                Route: Route,
                IncludeVersion: IncludeVersion,
                Timeout: Timeout,
                Proc: null,
                URI: out URI,
                Parameters: Parameters)
            ;

        private HttpWebRequest CreateWebRequest(
            Route Route,
            Action<System.IO.StreamWriter> Proc,
            out string URI,
            params RouteParam[] Parameters
            )
            => CreateWebRequest(
                Route: Route,
                IncludeVersion: true,
                Timeout: GetTimeout(),
                Proc: Proc,
                URI: out URI,
                Parameters: Parameters)
            ;

        private HttpWebRequest CreateWebRequest(
            Route Route,
            out string URI,
            params RouteParam[] Parameters
            )
            => CreateWebRequest(
                Route: Route,
                Proc: null,
                URI: out URI,
                Parameters: Parameters)
            ;

        private HttpWebRequest CreatePostJSON(
            string URI,
            int? Timeout = null,
            Action<System.IO.StreamWriter> Proc = null
            )
            => CreateWebRequest(
                URI: URI,
                ContentType: "json",
                Method: WebMethods.POST,
                Timeout: Timeout,
                Proc: Proc)
            ;

        private HttpWebRequest CreatePutJSON(
            string URI,
            int? Timeout = null,
            Action<System.IO.StreamWriter> Proc = null
            )
            => CreateWebRequest(
                URI: URI,
                ContentType: "json",
                Method: WebMethods.PUT,
                Timeout: Timeout,
                Proc: Proc)
            ;

        private HttpWebRequest CreatePutGz(
            string URI,
            int? Timeout = null,
            Action<System.IO.StreamWriter> Proc = null
            )
            => CreateWebRequest(
                URI: URI,
                ContentType: "gz",
                Method: WebMethods.PUT,
                Timeout: Timeout,
                Proc: Proc)
            ;

        private HttpWebRequest CreateGetJSON(
            string URI,
            int? Timeout = null,
            Action<System.IO.StreamWriter> Proc = null
            )
            => CreateWebRequest(
                URI: URI,
                ContentType: "json",
                Method: WebMethods.GET,
                Timeout: Timeout,
                Proc: Proc)
            ;

        private HttpWebRequest CreateGetGz(
            string URI,
            int? Timeout = null,
            Action<System.IO.StreamWriter> Proc = null
            )
            => CreateWebRequest(
                URI: URI,
                ContentType: "gz",
                Method: WebMethods.GET,
                Timeout: Timeout,
                Proc: Proc)
            ;

        #region Get Server Status

        public bool GetServerStatus(bool RethrowForLinuxPrompt = false)
        {
            if (!Enabled)
                return false;

            //Utils.Log($"{1.Indent()}{nameof(GetServerStatus)}, {nameof(RethrowForLinuxPrompt)}: {RethrowForLinuxPrompt}");

            string uRI = null; // statusGetRoute();

            HttpWebRequest httpReq = null;
            int? timeout = GetTimeout();
            if (timeout.HasValue)
                timeout = (int)(timeout.GetValueOrDefault() * 0.5);

            try
            {
                httpReq = CreateWebRequest(Routes.Status, IncludeVersion: false, Timeout: timeout, out uRI); // CreateGetJSON(uRI, timeout);
            }
            catch (Exception x)
            {
                if (RethrowForLinuxPrompt
                    && (x is NotSupportedException
                        || x?.InnerException is NotSupportedException)
                    && (x.Message?.Contains("The URI prefix is not recognized") is true))
                    throw x;

                Utils.Error($"Creating Server Status HttpWebRequest for {uRI}", x);
                return false;
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    if (httpRes.StatusCode == HttpStatusCode.OK
                        && JObject.Parse(result) is JObject jObject
                        && jObject["status"].ToObject<string>() is string statusString
                        && statusString == "Running")
                    {
                        //Utils.Log($"{2.Indent()}return true");
                        return true;
                    }
                    else
                        Utils.Warn($"{nameof(ServerUtils.GetBonesInfos)} got no status from {ToString()}" +
                            $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, timeout))
                    return false;

                if (x.Is404Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.NotFound} ({(int)HttpStatusCode.NotFound}) for {nameof(GetServerStatus)} via route {uRI}");
                    return false;
                }
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed receiving GET response for {uRI}", x);
            }

            return false;
        }

        public bool GetCanUp()
        {
            if (!IsRunning)
                return false;

            if (UserID == Guid.Empty)
                return false;

            string uRI = null;
            HttpWebRequest httpReq = null;
            try
            {
                httpReq = CreateWebRequest(Routes.CanUpload, out uRI, new RouteParam(nameof(UserID), UserID)); // CreateGetJSON(uRI, timeout);
            }
            catch (Exception x)
            {
                Utils.Error($"Creating GET canUp HttpWebRequest for {uRI}", x);
                return false;
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    if (httpRes.StatusCode == HttpStatusCode.OK
                        && JsonConvert.DeserializeObject<bool>(result) is bool parsedResult
                        && parsedResult)
                    {
                        return true;
                    }
                    else
                        Utils.Warn($"{nameof(ServerUtils.GetBonesInfos)} got no status from {ToString()}" +
                            $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return false;

                if (x.Is404Exception())
                {
                    Utils.Warn($"{nameof(Host)} {Name} got {HttpStatusCode.NotFound} ({(int)HttpStatusCode.NotFound}) for {nameof(GetCanUp)} via route {uRI}");
                    return false;
                }
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed receiving GET response for {uRI}", x);
            }
            return false;
        }

        #endregion
        #region Upload Saves

        public async Task<Guid> PostBonesInfoAsync(
            SaveBonesJSON SaveBonesJSON,
            PendingSavGz PendingSavGz,
            bool Silent = false
            )
        {
            if (!IsRunning)
            {
                if (!Silent)
                    Utils.Warn($"{ToString()} is not running");

                return Guid.Empty;
            }

            if (SaveBonesJSON == null)
            {
                if (!Silent)
                    Utils.Warn($"{nameof(SaveBonesJSON)} is null");

                return Guid.Empty;
            }

            if (!PendingSavGz.CheckValid(IgnoreToken: true, Silent: false).Success)
                return Guid.Empty;

            if (GetSaveBonesInfo(PendingSavGz.BonesID) is SaveBonesInfo existingBonesInfo)
            {
                if (!Silent)
                    Utils.Warn($"{ToString()} already has a bones with the following ID {{{existingBonesInfo.ID}}}");

                return Guid.Empty;
            }    

            string uRI = null;
            HttpWebRequest httpReq = null;
            try
            {
                using (var tempSaveBonesJSON = SaveBonesJSON.Clone())
                {
                    tempSaveBonesJSON.FileLocationType = FileLocationData.LocationType.Online;

                    if (tempSaveBonesJSON.IsCharIconSwapped())
                        tempSaveBonesJSON.HotSwapCharIcon();

                    var bonesSpec = tempSaveBonesJSON.BonesSpec;
                    tempSaveBonesJSON.BonesSpec = null;

                    httpReq = CreateWebRequest(
                        Route: Routes.NewBonesInfo,
                        Proc: async delegate (System.IO.StreamWriter streamWriter)
                        {
                            using var record = new Record(
                                UserID: UserID,
                                BonesID: PendingSavGz.BonesID,
                                SaveBonesJSON: tempSaveBonesJSON,
                                BonesSpec: bonesSpec,
                                SavGz: PendingSavGz.SavGz)
                            ;
                            await streamWriter.WriteAsync(JsonConvert.SerializeObject(record));
                        },
                        URI: out uRI);
                }
            }
            catch (Exception x)
            {
                Utils.Error($"Creating POST HttpWebRequest for {uRI}", x);
                return Guid.Empty;
            }

            if (httpReq == null)
            {
                if (!Silent)
                    Utils.Warn($"{nameof(httpReq)} for {ToString()} is null");
                return Guid.Empty;
            }

            try
            {
                var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.Created)
                    {
                        if (JObject.Parse(result) is JObject jObject
                            && jObject["success"].ToObject<Guid>() is Guid parsedToken)
                        {
                            Utils.Info($"Successfully created new BonesInfo on server at \"{ToString()}\"" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})" +
                                $" - {nameof(parsedToken)}: {parsedToken}");

                            return parsedToken;
                        }
                    }
                    else
                    {
                        Utils.Warn($"{nameof(TryUploadBonesAsync)} received response from server at \"{ToString()}\": {httpRes.StatusCode} ({(int)httpRes.StatusCode}) " +
                            $"instead of expected {HttpStatusCode.Created} ({(int)HttpStatusCode.Created})");
                    }
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return Guid.Empty;
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed receiving POST response for {uRI}", x);
                return Guid.Empty;
            }
            return Guid.Empty;
        }

        private TimerCallback GetPendingSavGzTimerCallback(PendingSavGz PendingSavGz)
            => delegate (object state)
            {
                if (state is not string bonesID
                    || bonesID.IsNullOrEmpty())
                {
                    Utils.Error($"{nameof(Timer)} passed invalid {nameof(PendingSavGz.BonesID)} ({state?.ToString() ?? "NO_STATE"}).");
                    return;
                }

                if (PendingSavGzs.TryGetValue(bonesID, out var pendingSavGz))
                    pendingSavGz.RetryUpload().Wait();
                else
                    Utils.Warn($"{nameof(Timer)} passed {nameof(PendingSavGz.BonesID)} not found in {nameof(PendingSavGzs)} ({bonesID ?? "NO_BONES_ID"}).");

            };

        private Timer GetPendingSavGzTimer(PendingSavGz PendingSavGz)
        {
            string bonesID = PendingSavGz.BonesID;
            Utils.InfoTimestamp($"Starting timer to reattempt upload of {nameof(PendingSavGz)} {{{bonesID}}} to {ToString()}. {nameof(PendingSavGzs)}: {PendingSavGzs.Count}");

            return new(GetPendingSavGzTimerCallback(PendingSavGz),
                state: bonesID,
                dueTime: 45000, // every 45 seconds
                period: 45000);
        }

        public async Task<ResponseInfo> PutBonesSavGz(PendingSavGz PendingSavGz, bool Silent = false)
        {
            var responseInfo = new ResponseInfo
            {
                Host = this,
                Success = false,
            };

            if (PendingSavGz == null)
            {
                responseInfo.Message = $"{nameof(PendingSavGz)} is null";

                if (!Silent)
                    Utils.Warn(responseInfo.Message);
                return responseInfo;
            }

            responseInfo = PendingSavGz.CheckValid(Silent: Silent);
            if (!responseInfo.Success)
                return responseInfo;

            bool setUpTimer = PendingSavGz.Timer == null;
            try
            {
                if (!IsRunning)
                {
                    responseInfo.StatusCode = HttpStatusCode.InternalServerError;
                    responseInfo.Success = false;
                    responseInfo.Message = $"{ToString()} is not running";
                    if (!Silent)
                        Utils.Warn(responseInfo.Message);

                    return responseInfo;
                }

                string uRI = null;
                HttpWebRequest httpReq = null;
                try
                {
                    httpReq = CreateWebRequest(
                        Route: Routes.NewBonesSavGz,
                        Proc: async delegate (System.IO.StreamWriter streamWriter)
                        {
                            using (var savGzStream = new System.IO.MemoryStream(PendingSavGz.SavGz))
                            {
                                await savGzStream.CopyToAsync(streamWriter.BaseStream);
                            }
                        },
                        URI: out uRI,
                        new RouteParam(nameof(PendingSavGz.BonesID), PendingSavGz.BonesID));

                    httpReq.Headers.AppendWebHeader(HttpRequestHeader.Authorization, $"basic {PendingSavGz.Token}");
                }
                catch (Exception x)
                {
                    responseInfo.Success = false;
                    responseInfo.Message = x.Message;

                    Utils.Error($"Creating PUT HttpWebRequest for {uRI}", x);
                    return responseInfo;
                }

                if (httpReq == null)
                {
                    responseInfo.Success = false;
                    responseInfo.Message = $"{nameof(httpReq)} for {ToString()} is null";

                    if (!Silent)
                        Utils.Warn(responseInfo.Message);

                    return responseInfo;
                }

                try
                {
                    var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = await streamReader.ReadToEndAsync();
                        responseInfo.StatusCode = httpRes.StatusCode;
                        if (httpRes.StatusCode == HttpStatusCode.Created)
                        {
                            if (JObject.Parse(result) is JObject jObject
                                && jObject["success"].ToObject<bool>())
                            {
                                responseInfo.Message = $"Successfully added \".sav.gz\" blob to Bones on server at \"{ToString()}\"";
                                Utils.Info($"{responseInfo.Message} - {httpRes.StatusCode} ({(int)httpRes.StatusCode})\n{jObject}");

                                setUpTimer = false;

                                PendingSavGz.Success = true;
                                responseInfo.Success = true;
                                if (PendingSavGz.Timer == null)
                                    PendingSavGz.Dispose();

                                return responseInfo;
                            }
                            else
                            {
                                responseInfo.Message = $"{nameof(TryUploadBonesAsync)} received expected response from server at \"{ToString()}\": " +
                                    $"{httpRes.StatusCode} ({(int)httpRes.StatusCode}), but {nameof(result)} was malformed";

                                if (!Silent)
                                    Utils.Warn($"{responseInfo.Message}\n{JObject.Parse(result)?.ToString() ?? result?.ToString() ?? $"{nameof(result)} null"}");

                                return responseInfo;
                            }
                        }
                        else
                        {
                            responseInfo.Message = $"{nameof(TryUploadBonesAsync)} received response from server at \"{ToString()}\": " +
                                $"{httpRes.StatusCode} ({(int)httpRes.StatusCode}) instead of expected {HttpStatusCode.Created} ({(int)HttpStatusCode.Created})";

                            Utils.Warn(responseInfo.Message);
                        }
                    }

                    responseInfo.Message = $"{nameof(TryUploadBonesAsync)} received response from server at \"{ToString()}\": " +
                        $"{httpRes.StatusCode} ({(int)httpRes.StatusCode}), but somehow fell through without an error.";

                    if (!Silent)
                        Utils.Warn(responseInfo.Message);

                    return responseInfo;
                }
                catch (WebException x)
                {
                    if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    {
                        setUpTimer = false;

                        PendingSavGz.ForceDispose = true;
                        if (PendingSavGz.Timer == null)
                            PendingSavGz.Dispose();

                        responseInfo.Success = false;
                        responseInfo.Message = x.Message;

                        return responseInfo;
                    }
                    throw x;
                }
                catch (Exception x)
                {
                    responseInfo.Success = false;
                    responseInfo.Message = x.Message;

                    Utils.ErrorOnce($"Failed receiving PUT response for {uRI} ({x.GetType().ToStringWithGenerics()})", x);
                    return responseInfo;
                }
            }
            finally
            {
                if (setUpTimer)
                {
                    string bonesID = PendingSavGz.BonesID;

                    if (PendingSavGz != null
                        && !PendingSavGzs.TryGetValue(bonesID, out var pendingSavGz))
                    {
                        PendingSavGzs[bonesID] = pendingSavGz = PendingSavGz;
                        pendingSavGz.Timer = GetPendingSavGzTimer(pendingSavGz);
                    }
                }
            }
        }

        public async Task<ResponseInfo> TryUploadBonesAsync(SaveBonesJSON SaveBonesJSON, PendingSavGz PendingSavGz)
        {
            var responseInfo = new ResponseInfo
            {
                Host = this,
                Success = false,
            };

            if (!IsRunning)
            {
                responseInfo.StatusCode = HttpStatusCode.InternalServerError;
                responseInfo.Message = $"Server is not running";
                return responseInfo;
            }

            try
            {
                try
                {
                    PendingSavGz.Token = await PostBonesInfoAsync(SaveBonesJSON, PendingSavGz);
                    PendingSavGz.UploadTime = DateTime.UtcNow;
                }
                catch (Exception x)
                {
                    PendingSavGz.Token = Guid.Empty;
                    Utils.Warn($"{nameof(Host)}.{nameof(TryUploadBonesAsync)}", x);

                    responseInfo.Message = x.Message;
                }

                if (PendingSavGz.Token.IsEmptyOrDefault())
                {
                    responseInfo.Success = false;
                    responseInfo.Message ??= $"Missing or invalid auth token";
                    return responseInfo;
                }

                try
                {
                    return await PutBonesSavGz(PendingSavGz);
                }
                catch (Exception x)
                {
                    responseInfo.Message = x.Message;

                    return responseInfo;
                }
            }
            catch (Exception x)
            {
                responseInfo.Message = x.Message;

                Utils.Error($"{nameof(TryUploadBonesAsync)} failed to upload Bones with BonesID {PendingSavGz.BonesID}", x);
                return responseInfo;
            }
        }

        #endregion
        #region Post Bones Report

        public async Task<bool> PostBonesReport(Report Report, bool ClearReportsCache = true)
        {
            if (!IsRunning
                || Report == null
                || Report.BonesID.IsNullOrEmpty())
                return false;

            string uRI = null;
            HttpWebRequest httpReq = null;
            try
            {
                httpReq = CreateWebRequest(
                    Route: Routes.NewReport,
                    Proc: async delegate (System.IO.StreamWriter streamWriter)
                    {
                        await streamWriter.WriteAsync(JsonConvert.SerializeObject(Report));
                    },
                    URI: out uRI);
            }
            catch (Exception x)
            {
                Utils.Error($"Creating POST HttpWebRequest for {uRI}", x);
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
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
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return false;
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed receiving POST response for {uRI}", x);
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
                || UserID.IsEmptyOrDefault())
                return false;

            string uRI = null;
            HttpWebRequest httpReq = null;
            try
            {
                httpReq = CreateWebRequest(
                    Route: Routes.Reported,
                    URI: out uRI,
                    new RouteParam(nameof(BonesID), BonesID),
                    new RouteParam(nameof(UserID), UserID));
            }
            catch (Exception x)
            {
                Utils.Error($"Creating GET HttpWebRequest for {uRI}", x);
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
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
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return false;
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed receiving GET response for {uRI}", x);
                return false;
            }
            return false;
        }

        public IEnumerable<Report> GetBonesReports()
        {
            //Utils.Log($"{1.Indent()}{nameof(GetBonesReports)}");
            if (!IsRunning)
                return null;

            string uRI = null;
            HttpWebRequest httpReq = null;
            try
            {
                httpReq = CreateWebRequest(
                    Route: Routes.Reports,
                    URI: out uRI);
            }
            catch (Exception x)
            {
                Utils.Error($"Creating GET HttpWebRequest for {uRI}", x);
            }

            if (httpReq == null)
                return null;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    if (httpRes.StatusCode == HttpStatusCode.OK)
                    {
                        try
                        {
                            if (JObject.Parse(result) is JObject jObject
                                && jObject["success"].ToObject<bool>() is bool success
                                && jObject["data"] is JToken data)
                            {
                                var reports = new List<Report>();
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
                                    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");

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
                        Utils.Warn($"{nameof(GetBonesReports)} received response from server at \"{ToString()}\": {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
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
                Utils.Error($"Failed receiving GET response for {uRI}", x);
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
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
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
        #region Get Bones Info(s)

        private SaveBonesJSON[] GetSaveBonesJSONs()
        {
            //Utils.Log($"{3.Indent()}GetSaveBonesJSONs");
            if (!IsRunning)
                return null;

            string uRI = null;
            HttpWebRequest httpReq = null;
            try
            {
                httpReq = CreateWebRequest(
                    Route: Routes.BonesInfos,
                    URI: out uRI,
                    new RouteParam(nameof(UserID), UserID));
            }
            catch (Exception x)
            {
                Utils.Error($"Creating GET HttpWebRequest for {uRI}", x);
                return null;
            }

            if (httpReq == null)
                return null;

            try
            {
                var httpRes = (HttpWebResponse)httpReq.GetResponse();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    if (httpRes.StatusCode == HttpStatusCode.OK
                        && JObject.Parse(result) is JObject jObject
                        && jObject["success"].ToObject<bool>())
                    {
                        try
                        {
                            var saveBonesJSONs = JsonConvert.DeserializeObject<SaveBonesJSON[]>(jObject["data"].ToString());
                            foreach (var saveBonesJSON in saveBonesJSONs)
                            {
                                saveBonesJSON.Downloaded = true;
                                saveBonesJSON.FileLocationType = FileLocationData.LocationType.Online;
                            }

                            return saveBonesJSONs;
                        }
                        catch (Exception x)
                        {
                            Utils.Error($"{nameof(ServerUtils.GetBonesInfos)} failed to deserialize {nameof(jObject)}[\"data\"] to {typeof(SaveBonesJSON[]).Name}", x);
                            return null;
                        }
                    }
                    else
                    if (httpRes.StatusCode == HttpStatusCode.NoContent)
                    {
                        //Utils.Log($"{nameof(GetBonesInfos)} got no SaveBonesInfo from {ToString()}" +
                        //    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                    }
                    else
                    {
                        Utils.Warn($"{nameof(ServerUtils.GetBonesInfos)} got no SaveBonesInfo from {ToString()}" +
                            $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                    }
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return null;
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed receiving GET response for {uRI}", x);
            }
            return null;
        }

        public IEnumerable<SaveBonesInfo> GetSaveBonesInfos()
        {
            if (GetSaveBonesJSONs() is not SaveBonesJSON[] saveBonesJSONs)
                yield break;

            using var saveBonesInfos = ScopeDisposedList<SaveBonesInfo>.GetFromPool();
            foreach (var saveBonesJSON in saveBonesJSONs)
            {
                if (saveBonesInfos.Any(bonesInfo => bonesInfo.ID == saveBonesJSON.ID))
                    continue;

                var saveBonesInfo = SaveBonesJSON.InfoFromJson(
                    SaveBonesJSON: saveBonesJSON,
                    FileLocationData: FileLocationData.NewOnline(this),
                    SaveSize: saveBonesJSON.Size);

                yield return saveBonesInfo;

                saveBonesInfos.Add(saveBonesInfo);
            }

            //Utils.Log($"{nameof(GetBonesInfos)} got {saveBonesInfos.Count} Bones Info from {ToString()}");
        }

        private SaveBonesJSON GetSaveBonesJSON(string BonesID)
        {
            if (!IsRunning)
                return null;

            string uRI = null;
            HttpWebRequest httpReq = null;
            try
            {
                httpReq = CreateWebRequest(
                    Route: Routes.BonesInfo,
                    URI: out uRI,
                    new RouteParam(nameof(BonesID), BonesID),
                    new RouteParam(nameof(UserID), UserID));
            }
            catch (Exception x)
            {
                Utils.Error($"Creating GET HttpWebRequest for {uRI}", x);
                return null;
            }

            if (httpReq == null)
                return null;

            try
            {
                using (var httpRes = (HttpWebResponse)httpReq.GetResponse())
                {
                    using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = streamReader.ReadToEnd();
                        if (httpRes.StatusCode == HttpStatusCode.OK
                            && JObject.Parse(result) is JObject jObject
                            && jObject["success"].ToObject<bool>())
                        {
                            try
                            {
                                return JsonConvert.DeserializeObject<SaveBonesJSON>(jObject["BonesInfo"].ToString());
                            }
                            catch (Exception x)
                            {
                                Utils.Error($"{nameof(GetSaveBonesJSON)} failed to deserialize {nameof(jObject)}[\"BonesInfo\"] to {nameof(SaveBonesJSON)}", x);
                                return null;
                            }
                        }
                        else
                        if (httpRes.StatusCode == HttpStatusCode.NoContent)
                        {
                            //Utils.Log($"{nameof(GetSaveBonesJSON)} got no SaveBonesJSON from {ToString()}" +
                            //    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                        }
                        else
                        {
                            Utils.Warn($"{nameof(GetSaveBonesJSON)} got no SaveBonesJSON from {ToString()}" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                        }
                    }
                }
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return null;
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error($"Failed receiving GET response for {uRI}", x);
            }
            return null;
        }

        public SaveBonesInfo GetSaveBonesInfo(string BonesID)
            => GetSaveBonesJSON(BonesID)?.InfoFromJson(
                FileLocationData: FileLocationData.NewOnline(this),
                SaveSize: 0L)
            ;

        #endregion
        #region Get Bones SavGz

        public async Task<byte[]> GetBonesSavGzAsync(string BonesID)
        {
            if (!IsRunning)
                return null;

            if (BonesID.IsNullOrEmpty())
                return null;

            string uRI = null;
            HttpWebRequest httpReq = null;
            try
            {
                httpReq = CreateWebRequest(
                    Route: Routes.LoadBones,
                    URI: out uRI,
                    new RouteParam(nameof(BonesID), BonesID));
            }
            catch (Exception x)
            {
                Utils.Error($"Creating GET HttpWebRequest for {uRI}", x);
                return null;
            }

            if (httpReq == null)
                return null;

            try
            {
                var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                {
                    try
                    {
                        if ((await streamReader.ReadAllBytesAsync()) is byte[] rawBuffer)
                        {
                            Utils.Log($"{nameof(GetBonesSavGzAsync)} got a SaveBonesSavGz from {ToString()} ({Buffer.ByteLength(rawBuffer).Things(typeof(byte).Name)})" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                            return rawBuffer;
                        }
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"{nameof(GetBonesSavGzAsync)} failed to get a Bones SavGz from {ToString()}" +
                            $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})", x);
                        return null;
                    }
                }
                Utils.Warn($"{nameof(GetBonesSavGzAsync)} failed to get a Bones SavGz from {ToString()}" +
                    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
            }
            catch (WebException x)
            {
                if (HandleTimeoutWebException(x, uRI, GetTimeout()))
                    return null;
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(GetBonesSavGzAsync)} failed to get a Bones SavGz from {ToString()}", x);
            }
            return null;
        }

        #endregion
        #region Post Downlaod Bones File

        /*public async Task<bool> PostDownloadBones(string BonesID, FileLocationData LocationData)
        {
            if (!IsRunning)
                return false;

            *//*if (Enabled)        // remove this to start testing again.
                return false;*//*

            if (BonesID.IsNullOrEmpty()
                || Config is null
                || Config.ID.IsEmptyOrDefault()
                || LocationData is null
                || LocationData.Type >= FileLocationData.LocationType.Mod)
                return false;

            string uRI = BonesDownloadPostRoute(BonesID, Config.ID);
            HttpWebRequest httpReq = null;
            int? timeout = GetTimeout();
            try
            {
                httpReq = CreatePostJSON(uRI, timeout);
            }
            catch (Exception x)
            {
                Utils.Error($"Creating POST HttpWebRequest for {uRI}", x);
                return false;
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    if (httpRes.StatusCode == HttpStatusCode.OK
                        && JObject.Parse(result) is JObject jObject
                        && jObject["success"].ToObject<bool>())
                    {
                        string catchFlag = "top";
                        bool restoreBackup = false;
                        string jsonFileName = BonesManager.GetInfoFileName(BonesManager.BonesFileName);
                        string saveFileName = BonesManager.GetSaveFileName(BonesManager.BonesFileName);
                        string jsonFilePath = LocationData.WithFileName(Path.Combine(BonesID, jsonFileName));
                        string saveFilePath = LocationData.WithFileName(Path.Combine(BonesID, saveFileName));
                        try
                        {
                            catchFlag = "DeserializeObject<SaveBonesJSON>";
                            if (JsonConvert.DeserializeObject<SaveBonesJSON>(jObject?[nameof(SaveBonesJSON)]?.ToString()) is SaveBonesJSON saveBonesJSON
                                && !(catchFlag = "Encoding.UTF8.GetString").IsNullOrEmpty()
                                && Encoding.UTF8.GetString(jObject?[nameof(SavGz)]?.ToObject<byte[]>()) is string savGzString
                                && !(catchFlag = "Convert.FromBase64String").IsNullOrEmpty()
                                && Convert.FromBase64String(savGzString) is byte[] savGz)
                            {
                                catchFlag = "File.WriteAllText";
                                saveBonesJSON.FileLocationType = LocationData.Type;
                                await File.WriteAllTextAsync(jsonFilePath, JsonConvert.SerializeObject(saveBonesJSON, Formatting.Indented));
                                if (File.Exists(saveFilePath))
                                {
                                    catchFlag = "File.Copy";
                                    await File.CopyAsync(saveFilePath, $"{saveFilePath}.bak", overwrite: true);
                                    restoreBackup = true;
                                }
                                catchFlag = "File.WriteAllBytes";
                                await File.WriteAllBytesAsync(saveFilePath, savGz);

                                MemoryHelper.GCCollect();

                                catchFlag = "The.Game.CheckSave";
                                The.Game.CheckSave(saveFilePath);
                                return true;
                            }
                            return false;
                        }
                        catch (Exception x)
                        {
                            BonesManager.BonesHoardError(saveFilePath, x, restoreBackup);
                            Utils.Error($"{nameof(PostDownloadBones)} failed to write reponse to files [{catchFlag}]", x);
                            return false;
                        }
                    }
                    else
                    if (httpRes.StatusCode == HttpStatusCode.NoContent)
                    {
                        //Utils.Log($"{nameof(GetBonesInfos)} got no SaveBonesInfo from {uRI}" +
                        //    $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
                    }
                    else
                    {
                        Utils.Warn($"{nameof(GetBonesInfos)} got no bones file from {uRI}" +
                            $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})");
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
                Utils.Error($"{nameof(GetBonesSavGzAsync)} failed to get a bones file from {uRI}", x);
            }
            return false;
        }*/

        #endregion
        #region Put Bones Stats

        public async Task<bool> PutBonesStats(
            string BonesID,
            string Stat
            )
        {
            if (!IsRunning
                || BonesID.IsNullOrEmpty()
                || Stat.IsNullOrEmpty()
                || !BonesStats.StatNames.Contains(Stat)
                || UserID.IsEmptyOrDefault())
                return false;

            string uRI = null;
            HttpWebRequest httpReq = null;
            int? timeout = GetTimeout();
            try
            {
                httpReq = CreateWebRequest(
                    Route: Routes.UpdateBonesStats,
                    URI: out uRI,
                    new RouteParam(nameof(Stat), Stat),
                    new RouteParam(nameof(BonesID), BonesID),
                    new RouteParam(nameof(UserID), UserID));
            }
            catch (Exception x)
            {
                Utils.Error($"Creating PUT HttpWebRequest for {uRI}", x);
                return false;
            }

            if (httpReq == null)
                return false;

            try
            {
                var httpRes = (HttpWebResponse)await httpReq.GetResponseAsync();
                using (var streamReader = new System.IO.StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.OK)
                    {
                        if (JObject.Parse(result) is JObject jObject
                            && jObject["success"].ToObject<bool>())
                        {
                            Utils.Info($"Successfully updated stats to BonesRecord on server at \"{ToString()}\"" +
                                $" - {httpRes.StatusCode} ({(int)httpRes.StatusCode})\n{jObject}");
                            return true;
                        }
                    }
                    else
                    {
                        Utils.Warn($"{nameof(TryUploadBonesAsync)} received response from server at \"{ToString()}\": {httpRes.StatusCode} ({(int)httpRes.StatusCode}) " +
                            $"instead of expected {HttpStatusCode.OK} ({(int)HttpStatusCode.OK})");
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
                Utils.Error($"Failed receiving PUT response for {uRI}", x);
                return false;
            }
            Utils.Warn($"Fell through sending/receiving PUT for {uRI}");
            return false;
        }

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
                using var hostLocationPairs = ScopeDisposedList<HostLocationPair>.GetFromPoolFilledWith(AllHostsWithLocation());
                foreach (var hostLocationPair in hostLocationPairs)
                {
                    try
                    {
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
                            Text = $"[{locationData.Type.GetColoredString()}] {host.FullDisplayName(IncludeAuth: true)}",
                            Icon = new Renderable(
                                Tile: "Mutations/gas_generation.bmp",
                                ColorString: "&K",
                                TileColor: "&K",
                                DetailColor: FileLocationData.GetFileLocationDataTypeColor(locationData.Type)[0]),
                            Hotkey = options.GetHotkeys().GetNextHotKey(Excluding: exludeHotkeys),
                            Callback = e => AskDoWhatWithHostAsync(new (ServerUtils.Hosts.FirstOrDefault(h => h.LocationData?.SameAs(e.FileLocationData) is true), e.Host))
                        });
                    }
                    catch (Exception x)
                    {
                        Utils.Warn($"{nameof(ManageHostsOptionButtonWithSelectionAsync)} failed to add {nameof(hostLocationPair)}: \"{hostLocationPair}\" to {nameof(options)}", x);
                    }
                }

                if (SelectImmediately != null)
                {
                    if (options.FirstOrDefault(SelectImmediately.Invoke) is CascadablePickOptionDataAsync<HostSet.HostLocationPair> selectedImmediately)
                    {
                        if (selectedImmediately.Callback?.Invoke(selectedImmediately?.Element) is Task<UIUtils.CascadableResult> resultTask)
                            result = await UIUtils.ShowEscancellepedAsync(selectedImmediately, resultTask);
                    }
                    SelectImmediately = null;
                }
                else
                {
                    result = await UIUtils.PerformPickOptionAsync(
                        OptionDataSet: options,
                        Title: $"Manage {OSSEOUS_ASH} Host".Colored("yellow"),
                        Intro: $"Use the options below to manage the hosts to/from which you'd like to upload/download bones files.",
                        IntroIcon: Configuration.BlackAshCloudIcon,
                        DefaultSelected: 0);
                }

                if (newHostLocationPair?.IsValid() is false)
                {
                    Utils.Log($"!{nameof(newHostLocationPair)}{nameof(newHostLocationPair.IsValid)}; {nameof(Dispose)}");
                    newHostLocationPair.Dispose();
                }
            }
            while (result.IsContinue());

            foreach (var hostCollection in ServerUtils.Hosts)
                hostCollection.Write();
        }

        private static async Task<UIUtils.CascadableResult> PerformNewHostAsync(HostSet.HostLocationPair HostConfig)
        {
            if ((await AskHostLocationAsync()) is not FileLocationData chosenLocation)
                return UIUtils.CascadableResult.CancelSilent;

            HostConfig.FileLocationData = chosenLocation;

            var sB = Event.NewStringBuilder();
            using var options = new CascadablePickOptionDataSetAsync<HostSet.HostLocationPair>(HostConfig);
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

        private static async Task<UIUtils.CascadableResult> PerformBasicNewHostAsync(HostSet.HostLocationPair Element)
        {
            bool? confirmed = null;
            do
            {
                try
                {
                    string entered = await AskFullHostNameAsync(Element.FileLocationData);

                    string authToken = null;
                    if (entered != null)
                        authToken = await AskHostAuthTokenAsync(null);

                    int? timeout = null;
                    if (entered != null
                        && authToken != null)
                        timeout = await AskHostTimeoutAsync(null);

                    if (!entered.IsNullOrEmpty()
                        && authToken != null
                        && timeout.HasValue
                        && TryParse(entered, out Element.Host))
                    {
                        Element.Host.TimeoutMS = timeout ?? DEFAULT_TIMEOUT;
                        Element.Host.AuthToken = authToken.IsNullOrEmpty() ? null : authToken;

                        if (Element.Host.Version < 0)
                        {
                            Element.Host.Version = (await AskHostVersionAsync(null)) ?? DEFAULT_VERSION;
                        }
                    }

                    confirmed = await ConfirmParsedHost(Element.Host);

                    if (!confirmed.HasValue)
                    {
                        Element.Host.Dispose(DisposePendingSavGz: true);
                        break;
                    }

                    if (Element.Host == null)
                        break;

                    if (confirmed.GetValueOrDefault())
                    {
                        if (ServerUtils.Hosts.FirstOrDefault(s => s.LocationData?.SameAs(Element.FileLocationData) is true) is not HostSet hostSet)
                            hostSet = new HostSet(Element.FileLocationData);

                        hostSet.WriteAddHost(Element.Host);
                        break;
                    }
                    Element.Host.Dispose(DisposePendingSavGz: true);
                }
                finally
                {
                    Element.Dispose();
                }
            }
            while (true);

            return confirmed.ToCascadableResult(Silent: true);
        }

        private static async Task<UIUtils.CascadableResult> PerformAdvancedNewHostAsync(HostSet.HostLocationPair Element)
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

                string localType = FileLocationData.LocationType.Local.GetColoredString();
                string syncedType = FileLocationData.LocationType.Synced.GetColoredString();

                return await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: $"New {OSSEOUS_ASH} Host".Colored("yellow"),
                    Intro: $"Which location would you like to record your new host?\n\n" +
                        $"\u0007 [{localType}] only exists on this device.\n" +
                        $"\u0007 [{syncedType}] will be synced by the platform managing the game's installation (if there is one).\n\n",
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

            using var tB = TextBuilder.Get();
            tB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                .AppendLine().AppendLine();

            tB.Append("Adding to location:")
                .AppendLine().Append("[").Append(ChosenLocation.Type.GetColoredString()).Append("] ")
                .Append(ChosenLocation.SanitiseForDisplay())
                .AppendLine().AppendLine();

            tB.Append($"Enter the full host name of the host you'd like to add, including the protocol and, if applicable, the port number.")
                .AppendLine().AppendLine();
            tB.Append("Examples:")
                .AppendLine().Append("https://osseousash.cloud/")
                .AppendLine().Append("http://localhost:8000/api/")
                .AppendLine().Append("http://192.168.0.25:8080/api/v1/")
                .AppendLine().AppendLine();
            tB.Append("You'll be asked to confirm the parsed result.");

            return await Popup.AskStringAsync(tB.ToString(), Default: "", ReturnNullForEscape: true, AllowColorize: false);
        }

        private static async Task<string> AskHostAuthTokenAsync(string PrependMessage)
        {
            using var tB = TextBuilder.Get();
            tB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                .AppendLine().AppendLine();
            if (!PrependMessage.IsNullOrEmpty())
                tB.Append(PrependMessage)
                    .AppendLine().AppendLine();

            tB.Append($"If the new host requires authentication of some kind, enter the required key below.")
                .AppendLine().AppendLine();
            tB.Append("You can change/update this later if necessary.");

            return await Popup.AskStringAsync(tB.ToString(), Default: "", ReturnNullForEscape: true, AllowColorize: false);
        }

        private static async Task<int?> AskHostTimeoutAsync(string PrependMessage)
        {
            using var tB = TextBuilder.Get();
            tB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                .AppendLine().AppendLine();
            if (!PrependMessage.IsNullOrEmpty())
                tB.Append(PrependMessage)
                    .AppendLine().AppendLine();

            tB.Append($"Enter the amount of time in milliseconds (-1 for unlimited) to wait before cancelling most requests to this host.")
                .AppendLine().AppendLine();
            tB.Append("You can change/update this later if necessary.");

            return await Popup.AskNumberAsync(tB.ToString(), Start: DefaultHost.TimeoutMS, Min: -1);
        }

        private static async Task<int?> AskHostVersionAsync(string PrependMessage)
        {
            using var tB = TextBuilder.Get();
            tB.AppendColored("yellow", $"New {OSSEOUS_ASH} Host")
                .AppendLine().AppendLine();
            if (!PrependMessage.IsNullOrEmpty())
                tB.Append(PrependMessage)
                    .AppendLine().AppendLine();

            tB.Append($"Enter the API version used by this host.")
                .AppendLine().Append("Some hosts will report their version number if you go to [host name]/api/status is a web browser.")
                .AppendLine()
                .AppendLine().Append("You can change/update this later if necessary.");

            return await Popup.AskNumberAsync(tB.ToString(), Start: DefaultHost.Version, Min: 0);
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
                .AppendLine().AppendPair(nameof(NewHost.AuthToken), NewHost.AuthToken)
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
        {
            await Popup.ShowAsync(
                Message: Event.FinalizeString(
                    SB: Event.NewStringBuilder("Addition of new host cancelled."))
                );
        }

        private static async Task<UIUtils.CascadableResult> AskDoWhatWithHostAsync(HostSetHostPair HostParentPair)
        {
            Utils.Log($"{nameof(AskDoWhatWithHostAsync)}");

            var Host = HostParentPair.Host;
            var HostCollection = HostParentPair.HostSet;

            var locationData = HostCollection.LocationData;
            using var options = new CascadablePickOptionDataSetAsync<HostSetHostPair>(HostParentPair, UIUtils.CascadableResult.Back, UIUtils.CascadableResult.Cancel);
            UIUtils.CascadableResult result;
            using var tB = TextBuilder.Get();
            do
            {
                tB.Clear();
                options.ClearItems(Dispose: true);

                tB.Append($"What would you like to do with host ").Append(Host.GetHostNameWithProtocol()).Append(", ")
                    .Append("which is in the below location:")
                    .AppendLine()
                    .AppendLine().Append(HostCollection.TaggedDisplayName())
                    .AppendLine()
                    .AppendLine().AppendPair("Server Status", Host.ServerStatusString);
                if (Host.IsOnCooldown)
                    tB.AppendLine().AppendColored("C", "Host status checks on cooldown.");
                tB.AppendLineEnd();

                Utils.Log($"if {nameof(Host)}.{nameof(Host.IsOnCooldown)}");
                if (Host.IsOnCooldown)
                {
                    Utils.Log($"add clear timeout cooldown");
                    options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                    {
                        Text = "clear timeout cooldown",
                        Hotkey = 'c',
                        Callback = PerformClearTimeoutAsync,
                    });
                }
                Utils.Log($"add modify");
                options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                {
                    Text = "modify",
                    Hotkey = 'm',
                    Callback = PerformModifyHostAsync
                });
                Utils.Log($"add migrate");
                options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                {
                    Text = "migrate",
                    Hotkey = 'M',
                    Callback = PerformMigrateHostAsync
                });
                Utils.Log($"add delete");
                options.Add(new CascadablePickOptionDataAsync<HostSetHostPair>
                {
                    Text = "delete",
                    Hotkey = 'd',
                    Callback = async delegate (HostSetHostPair p)
                    {
                        string message = $"Are you sure you want to delete {p.Host.GetHostNameWithProtocol()} from {p.HostSet.DisplayName()}?";
                        if ((await Popup.ShowYesNoCancelAsync(message)) == DialogResult.Yes)
                        {
                            p.HostSet.WriteRemoveHost(p.Host, Dispose: true);
                            return UIUtils.CascadableResult.BackSilent;
                        }
                        return UIUtils.CascadableResult.Continue;
                    }
                });

                Utils.Log($"result {nameof(UIUtils.PerformPickOptionAsync)}");
                result = await UIUtils.PerformPickOptionAsync(
                    OptionDataSet: options,
                    Title: $"Manage {OSSEOUS_ASH} Host".Colored("yellow"),
                    Intro: tB.ToString(),
                    IntroIcon: HostCollection.GetAshCloudIcon());
            }
            while (result.IsContinue());

            if (result.IsCancel())
                return result;

            return UIUtils.CascadableResult.Continue;
        }

        private static async Task<UIUtils.CascadableResult> PerformClearTimeoutAsync(HostSetHostPair HostParentPair)
        {
            string hostName = HostParentPair.Host.GetHostNameWithProtocol();
            string message = $"Are you sure you want clear the timeout cooldown for {hostName}?";
            if ((await Popup.ShowYesNoCancelAsync(message)) == DialogResult.Yes)
                HostParentPair.Host.ManuallyClearStatusCheckTimer(ReasonForOverride: $"{hostName} was cleared by player via options menu");

            message = $"Timeout cooldown for {hostName} cleared.";
            if (HostParentPair.Host.IsOnCooldown)
                message = $"Failed to clear timeout cooldown for {hostName}.";

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
                Message: $"Enter a new {nameof(Mod.Host.Port)}:",
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

        private static async Task<UIUtils.CascadableResult> PerformModifyAuthTokenAsync(Host Host)
        {
            Host.AuthToken = await Popup.AskStringAsync(
                Message: "Enter a new Auth Token:",
                Default: Host.AuthToken ?? "",
                ReturnNullForEscape: true,
                AllowColorize: false);

            var returnValue = Host.AuthToken != null
                ? UIUtils.CascadableResult.Continue
                : UIUtils.CascadableResult.Back
                ;

            if (Host.AuthToken == "")
                Host.AuthToken = null;

            return returnValue;
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
                    Hotkey = 'n',
                    Callback = PerformModifyNameAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = $"Change {nameof(host.Port)}: {(host.Port?.ToString() ?? "none")?.Colored("rules")}",
                    Hotkey = 'p',
                    Callback = PerformModifyPortAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = host.Encrypted.GetCheckboxText(nameof(host.Encrypted)),
                    Hotkey = 'e',
                    Callback = PerformToggleEncryptedAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = $"Change Timeout (ms): {host.GetTimeoutString()?.Colored("rules")}",
                    Hotkey = 't',
                    Callback = PerformModifyTimeoutAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = $"Change Auth Token: {(host.AuthToken.IsNullOrEmpty() ? "none" : "********")?.Colored("rules")}",
                    Hotkey = 'a',
                    Callback = PerformModifyAuthTokenAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = $"Change API {nameof(host.Version)}: {host.Version} ({host.GetAPIVersionStub()})",
                    Hotkey = 'v',
                    Callback = PerformModifyVersionAsync,
                });
                options.Add(new CascadablePickOptionDataAsync<Host>
                {
                    Text = host.GetEnabledCheckbox(),
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

        public void Dispose(bool DisposePendingSavGz)
        {
            Name = null;
            Port = null;
            Encrypted = false;
            AuthToken = null;
            Enabled = false;
            WrittenEnabled = false;
            ClearStatusCheckTimer(ref StatusCheckTimer);
            if (DisposePendingSavGz)
            {
                foreach (var pendingSavGz in (PendingSavGzs?.Values).IteratorSafe())
                    pendingSavGz.Dispose();

                if (PendingSavGzCache.ContainsKey(PendingSavGzCacheKey))
                    PendingSavGzCache.Remove(PendingSavGzCacheKey);
            }
            PendingSavGzCacheKey = null;
        }

        public void Dispose()
            => Dispose(DisposePendingSavGz: false)
            ;

        public static implicit operator string(Host Host)
            => Host.ToString()
            ;
    }
}
