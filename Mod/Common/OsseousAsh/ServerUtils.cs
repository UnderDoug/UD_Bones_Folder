using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

using File = Platform.IO.File;

using XRL;

using XRL.UI;
using XRL.World;
using UD_Bones_Folder.Mod.UI;
using Qud.UI;

using XRL.Collections;
using XRL.World.Text;
using UD_Bones_Folder.Mod.BonesSystem;
using System.Net;
using System.Reflection;
using static UD_Bones_Folder.Mod.OsseousAsh.HostSet;
using XRL.Language;
using UD_Bones_Folder.Mod.Serialization;
using UD_Bones_Folder.Mod.BonesSystem.BonesSource;
using UD_Bones_Folder.Mod.Logging;
using System.Threading;
using System.Globalization;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [HasRegisterMethodForDebug]
    [HasModSensitiveStaticCache]
    [HasCallAfterGameLoaded]
    [HasReturnsBonesSourcesDelegate]
    [HasManageBonesPickOptionsDelegate]
    public static class ServerUtils
    {
        #region Debug Registration
        [RegistersMethodsForDebug]
        public static void doDebugRegistry(DebugMethodRegistry Registry)
            => Registry.RegisterEach(
                Type: typeof(ServerUtils),
                MethodNameValues: new()
                {
                    { nameof(EnsureHosts), false },
                    { nameof(ReadHostSetsAsync), false },
                });
        #endregion

        public static Thread WebThread;
        public static bool ThreadStarted;

        private static SynchronizationContext _WebSynchronizationContext;
        public static SynchronizationContext WebContext => _WebSynchronizationContext;

        public static readonly object _PendingUploadCacheLock = new();
        // [ThreadStatic]
        public static Dictionary<string, Dictionary<string, PendingUpload>> PendingUploadCache = new();

        public static readonly object _WebTasksLock = new();
        public static Queue<PendingWebTask<ActionResult>> WebTasks = new();

        public static readonly object _TasksLock = new();
        public static Queue<Task> Tasks = new();

        public static readonly object _ActionsLock = new();
        public static Queue<Action> Actions = new();

        public static string RedCrossIndentLabel => $"{Const.NBSP}{Const.NBSP}{Const.CROSS.Colored("red")}{Const.NBSP}";
        public static string GreenDFIndentLabel => $"{Const.NBSP}{Const.NBSP}{Const.DF.Colored("green")}{Const.NBSP}";
        public static string GreenTickIndentLabel => $"{Const.NBSP}{Const.NBSP}{Const.TICK.Colored("green")}{Const.NBSP}";
        public static bool EnableOsseousAshUploads => Options.EnableOsseousAshUploads;
        public static bool EnableOsseousAshDownloads => Options.EnableOsseousAshDownloads;

        private static Rack<HostSet> _Hosts;
        public static Rack<HostSet> Hosts => _Hosts ??= ReadHostSetsAsync()?.WaitResult();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        internal static List<DoWhatWithHostPickOptionsDelegateEntry> DoWhatWithHostPickOptionsDelegates;

        private static readonly object _ReportsCacheLock = new();
        private static IEnumerable<Report> _ReportsCache;
        public static IEnumerable<Report> ReportsCache
        {
            get
            {
                lock (_ReportsCacheLock)
                {
                    if (_ReportsCache == null)
                    {
                        var cachedReports = Hosts.CacheReports();
                        _ReportsCache = cachedReports;
                    }

                    return _ReportsCache;
                }
            }
        }

        [BonesSourcesDelegate(IBonesSource.SourceType.Online)]
        public static IEnumerable<IBonesSource> OnlineBonesSources()
            => AllEnabledHosts()
            ;

        [ModSensitiveCacheInit]
        public static Thread StartWebThread()
        {
            UnityEngine.Application.quitting += Quitting;
            Utils.Info("Starting web thread...");
            WebThread = new (WebThreadStart)
            {
                Name = "UD_Bones_Folder Web Thread",
                CurrentCulture = CultureInfo.InvariantCulture,
                Priority = ThreadPriority.BelowNormal,
            };
            WebThread.Start();
            ThreadStarted = true;
            Utils.Info("Web thread started!");
            return WebThread;
        }

        public static async void WebThreadStart()
        {
            try
            {
                var synchronizationContext = new SynchronizationContext();
                SynchronizationContext.SetSynchronizationContext(synchronizationContext);
                _WebSynchronizationContext = synchronizationContext;

                bool skipActions = false;
                while (true)
                {
                    // Something?

                    if (!skipActions)
                    {
                        skipActions = true;
                        bool processWebAction = false;

                        PendingWebTask<ActionResult> action = null;
                        lock (_WebTasksLock)
                        {
                            WebTasks ??= new();
                            processWebAction = WebTasks.TryDequeue(out action);
                        }

                        if (processWebAction)
                        {
                            await action.CompleteAsync();
                            continue;
                        }
                    }
                    skipActions = false;

                    bool processTask = false;

                    Task task = null;
                    lock (_TasksLock)
                    {
                        Tasks ??= new();
                        processTask = Tasks.TryDequeue(out task);
                    }

                    if (processTask)
                    {
                        await task;
                        continue;
                    }

                    lock (_ActionsLock)
                    {
                        Actions ??= new();
                        if (Actions.TryDequeue(out var action))
                        {
                            action?.Invoke();
                        }
                    }
                }
            }
            catch (ThreadInterruptedException)
            {
                Utils.Info("Web thread shut down...");
            }
            catch (Exception x)
            {
                Utils.Error(x);
            }
        }

        public static void Quitting()
        {
            foreach ((var _, var pendingUploadCache) in PendingUploadCache.IteratorSafe())
                foreach ((var _, var pendingUpload) in pendingUploadCache)
                    pendingUpload.Dispose();
        }

        [ModSensitiveCacheInit]
        public static void EnsureHosts()
        {
            if (!VersionRoutes.Init)
                VersionRoutes.InitializeRoutes();

            _ = Hosts;
            Hosts?.BuildHosts(Ping: true);

            using var indent = new Indent();
            Debug.LogTypeMethod(Indent: indent[1]);

            Debug.Log($"Test-Serializing {nameof(Host)}s...", Indent: indent[2]);
            foreach (var hostSet in Hosts)
            {
                foreach (var host in hostSet)
                {
                    try
                    {
                        using (var hostJSON = host.ToJSON())
                        {
                            Debug.LogJson($"{nameof(Host)}[{host}])", hostJSON, Indent: indent[3]);
                        }
                    }
                    catch
                    { }
                }
            }

            _ = ReportsCache;

            if (Options.EnableOsseousAshDownloads)
            {
                BonesManager.ClearHasSaveBones();
                _ = BonesManager.HasSaveBones();
            }
        }

        [ModSensitiveCacheInit]
        private static void CacheDoWhatWithHostPickOptionDataDelegates()
        {
            DoWhatWithHostPickOptionsDelegates = new();

            foreach (var delegateMethod in ModManager.GetMethodsWithAttribute(typeof(DoWhatWithHostPickOptionsDelegateAttribute), typeof(HasDoWhatWithHostPickOptionsDelegateAttribute)))
            {
                var attribute = delegateMethod.GetCustomAttribute<DoWhatWithHostPickOptionsDelegateAttribute>();

                var modInfo = ModManager.GetMod(delegateMethod?.DeclaringType?.Assembly)
                    ?? Utils.ThisMod;

                if (DoWhatWithHostPickOptionsDelegateEntry.TryCreate(
                    DelegateMethod: delegateMethod,
                    DelegateEntry: out DoWhatWithHostPickOptionsDelegateEntry delegateEntry,
                    ModInfo: modInfo))
                    DoWhatWithHostPickOptionsDelegates.Add(delegateEntry);
            }
        }

        internal static void InvokeDoWhatWithHostPickOptionsDelegates(HostSetHostPair HostParentPair, ref CascadablePickOptionDataSetAsync<HostSetHostPair> Options)
        {
            foreach (var doWhatWithHostPickOptionsDelegateEntry in DoWhatWithHostPickOptionsDelegates.IteratorSafe())
                doWhatWithHostPickOptionsDelegateEntry.ProcessDoWhatWithHostPickOptions.Invoke(HostParentPair, ref Options);
        }

        public static async Task<Rack<HostSet>> ReadHostSetsAsync()
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(Indent: indent[1]);

            Rack<HostSet> hosts = null;
            foreach (var fileLocationData in Configuration.GetOsseousAshFileLocationData(Ensure: true).IteratorSafe())
            {
                try
                {
                    hosts ??= new();
                    if (fileLocationData.FileExists(HostsFileName)
                        && (await HostSet.ReadFromFileAsync(fileLocationData, HostsFileName)) is HostSet loadedHostCollection)
                    {
                        Debug.CheckYeh(fileLocationData.SanitizeForDisplay(), "Read Successfully", Indent: indent[1]);
                        loadedHostCollection.LocationData = fileLocationData;
                        hosts.Add(loadedHostCollection);
                    }
                    else
                    {
                        Debug.CheckNah(fileLocationData.SanitizeForDisplay(), "Failed Read, Creating New", Indent: indent[1]);
                        var newHostCollection = new HostSet(fileLocationData);
                        newHostCollection.Write();
                        hosts.Add(newHostCollection);
                    }
                }
                catch (Exception x)
                {
                    Utils.Error($"Finding {nameof(HostSet)}s", x);
                }
            }
            if (hosts.All(hc => hc.IsNullOrEmpty())
                && (await HostSet.ReadOrNewAsync()) is HostSet defaultHosts)
            {
                hosts ??= new();
                hosts.Add(defaultHosts);
            }
            if (hosts.IsNullOrEmpty()
                || hosts.All(hc => hc.IsNullOrEmpty()))
                return null;

            return hosts;
        }

        // This is a bit busted.
        public static void ReadHostsTo(ref Rack<HostSet> Hosts)
        {
            Hosts ??= new();
            if (ReadHostSetsAsync().WaitResult() is not Rack<HostSet> readHosts)
                return;

            using var hostsToRemove = ScopeDisposedList<Host>.GetFromPool();
            using var alreadyCopiedHosts = ScopeDisposedList<Host>.GetFromPool();
            foreach (var readHostCollection in readHosts)
            {
                // if the read host doesn't have an existing equivalent, add it.
                if (Hosts.FirstOrDefault(hc => hc.LocationData?.SameAs(readHostCollection.LocationData) is true) is not HostSet existingHostCollection)
                    Hosts.Add(readHostCollection);
                else
                {
                    // reset the lists.
                    hostsToRemove.Clear();
                    alreadyCopiedHosts.Clear();

                    // check each existing host for an equivalent read one.
                    //  - if read, copy the read one to the existing one, flag it as read.
                    //  - else, flag existing for removal.
                    foreach (var existingHost in existingHostCollection)
                    {
                        bool isEqualHost(Host h)
                            => h.SameAs(existingHost, IgnoreDisabled: true)
                            && !alreadyCopiedHosts.Contains(h)
                            ;

                        if (readHostCollection.FirstOrDefault(isEqualHost) is not Host readHost)
                            hostsToRemove.Add(existingHost);
                        else
                        {
                            readHost.CopyTo(existingHost);
                            alreadyCopiedHosts.Add(readHost);
                        }
                    }

                    // remove flagged existing.
                    foreach (var hostToRemove in hostsToRemove)
                    {
                        existingHostCollection.Remove(hostToRemove);
                        hostToRemove?.Dispose();
                    }

                    // add read that haven't been copied.
                    foreach (var readHost in readHostCollection)
                    {
                        if (alreadyCopiedHosts.Contains(readHost))
                            continue;

                        existingHostCollection.Add(readHost);
                    }

                    // dispose all the already copied hosts
                    existingHostCollection.DisposeClear(h => alreadyCopiedHosts.Contains(h), DisposePendingSavGz: false);
                }
            }
            readHosts.Clear();
        }

        public static void ClearLocalReportsCache()
        {
            //Utils.Log($"ClearReportsCache - {nameof(GameManager)}.{nameof(GameManager.AwakeComplete)}: {GameManager.AwakeComplete}");
            lock (_ReportsCacheLock)
            {
                _ReportsCache = null;
            }
        }

        public static IEnumerable<Host> AllHosts(Predicate<Host> Where)
        {
            foreach (var hostsCollection in Hosts.IteratorSafe())
                foreach (var host in hostsCollection.IteratorSafe())
                    if (Where?.Invoke(host) is not false)
                        yield return host;
        }

        public static IEnumerable<Host> AllHosts()
            => AllHosts(null).IteratorSafe()
            ;

        public static IEnumerable<Host> AllEnabledHosts(Predicate<Host> Where = null)
            => AllHosts(h => h.Enabled && (Where?.Invoke(h) is not false)).IteratorSafe()
            ;

        public static IEnumerable<HostLocationPair> AllHostsWithLocation(Predicate<HostLocationPair> Where)
        {
            foreach (var hostSet in Hosts.IteratorSafe())
            {
                foreach (var host in hostSet.IteratorSafe())
                {
                    var element = new HostLocationPair(host, hostSet?.LocationData);
                    if (Where?.Invoke(element) is false)
                    {
                        element.Dispose();
                        continue;
                    }

                    yield return element;
                }
            }
        }

        public static IEnumerable<HostLocationPair> AllHostsWithLocation()
            => AllHostsWithLocation(null)
            ;

        public static bool TryGetHostSet(this Host Host, out HostSet HostSet)
        {
            HostSet = null;
            if (AllHostsWithLocation(p => p.Host == Host)?.FirstOrDefault() is not HostLocationPair pair
                || Hosts.FirstOrDefault(hs => hs.LocationData?.SameAs(pair.FileLocationData) is true) is not HostSet hostSet)
                return false;

            HostSet = hostSet;
            return true;
        }

        [ManageBonesPickOptionsDelegate]
        [RegisterMethodForDebug(false)]
        public static void OnlineBonesManagementOptions(SaveBonesInfo BonesInfo, ref CascadablePickOptionDataSetAsync<SaveBonesInfo> Options)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesInfo), BonesInfo.GetNullString()),
                    Debug.Arg(nameof(Options), Options.GetNullString()),
                });

            // View Stats (5)

            // Manage ModConfig (10)

            var anyHostsMissing = BonesInfo.BonesSources.AnyHostsMissing(AllEnabledHosts());

            Debug.Log($"Add Upload?", Indent: indent[2]);
            Debug.YehNah(nameof(EnableOsseousAshUploads), Good: EnableOsseousAshUploads, Indent: indent[3]);
            Debug.YehNah(nameof(anyHostsMissing), Good: anyHostsMissing, Indent: indent[3]);
            if (EnableOsseousAshUploads
                && anyHostsMissing)
            {
                var pendingHosts = AllEnabledHosts(h => h.HasPending(BonesInfo));
                int numHostsPending = pendingHosts.Count();
                string pendingString = numHostsPending > 0 ? $" ({$"{numHostsPending} pending...".Colored("C")})" : null;
                Debug.YehNah(nameof(numHostsPending), numHostsPending, Good: numHostsPending > 0, Indent: indent[4]);
                Options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                {
                    Text = $"Upload{pendingString}",
                    Priority = 12,
                    Icon = Configuration.UploadIcon,
                    Hotkey = Options.GetFirstAvailableHotkey('u', 'U', '^'),
                    Callback = ManageBonesUploadAsync,
                });
            }

            Debug.Log($"Add Download?", Indent: indent[2]);
            var anySourcesOnline = BonesInfo.BonesSources.AnyOnline();
            var allSourcesNotOnDisk = !BonesInfo.BonesSources.AnyOnDisk();
            Debug.YehNah(nameof(EnableOsseousAshDownloads), Good: EnableOsseousAshDownloads, Indent: indent[3]);
            Debug.YehNah(nameof(anySourcesOnline), Good: anySourcesOnline, Indent: indent[3]);
            Debug.YehNah(nameof(allSourcesNotOnDisk), Good: allSourcesNotOnDisk, Indent: indent[3]);
            if (EnableOsseousAshDownloads
                && anySourcesOnline
                && allSourcesNotOnDisk)
            {
                Options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                {
                    Text = $"Download",
                    Priority = 13,
                    Icon = Configuration.DownloadIcon,
                    Hotkey = Options.GetFirstAvailableHotkey('d', 'D', 'v', 'V'),
                    Callback = ManageBonesDownloadAsync,
                });
            }

            // Block Bones (15)

            Debug.Log($"Add Report?", Indent: indent[2]);
            bool isBlocked = BonesInfo.IsBlocked;
            Debug.YehNah(nameof(anySourcesOnline), Good: anySourcesOnline, Indent: indent[3]);
            Debug.YehNah($"!{nameof(isBlocked)}", Good: !isBlocked, Indent: indent[3]);
            if (anySourcesOnline
                && !isBlocked)
            {
                Options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                {
                    Text = "Report",
                    Priority = 17,
                    Icon = Report.ReportBonesIcon,
                    Hotkey = Options.GetFirstAvailableHotkey('r', 'R', 'x', 'X'),
                    Callback = SaveBonesInfo.AskForBonesReportAsync,
                });
            }

            // Cremate (20)
        }

        private static string HostString(this Host Host)
            => Host?.ToString() ?? "MISSING_HOST"
            ;

        public static string ResponseInfoStatusNewLineDelimitedAggregator(string Accumulator, ResponseInfo Next)
            => Utils.NewLineDelimitedAggregator(Accumulator, $"{(Next.Success ? GreenDFIndentLabel : RedCrossIndentLabel)}{HostString(Next.Host)}{Next.AppendFailureMessage()}")
            ;

        [RegisterAsyncMethodForDebug(true)]
        public static async Task<UIUtils.CascadableResult> ManageBonesUploadAsync(SaveBonesInfo BonesInfo)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesInfo), BonesInfo.GetNullString()),
                });

            string catchFlag = "top";
            //Utils.Log(catchFlag);
            try
            {
                catchFlag = "BonesInfo.GetBonesSavGzStreamAsync";
                if (await BonesInfo.GetBonesSavGzStreamAsync() is not BonesSavGz bonesSavGz)
                {
                    Debug.CheckNah(catchFlag, Indent: indent[1]);
                    await UIUtils.ShowBummerAsync($"Something whent wrong, {Grammar.MakePossessive(BonesInfo.Name.StartReplace().ToString())} bones appears to be missing save data.");
                    return UIUtils.CascadableResult.Continue;
                }
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "bonesSavGz.TryEnsureSavable";
                if (!bonesSavGz.TryEnsureSavable())
                {
                    bonesSavGz.Dispose();
                    Debug.CheckNah(catchFlag, Indent: indent[1]);
                    await UIUtils.ShowBummerAsync($"Something whent wrong, {Grammar.MakePossessive(BonesInfo.Name.StartReplace().ToString())} bones appears to be missing save data.");
                    return UIUtils.CascadableResult.Continue;
                }
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "UploadBonesAsync";
                if (await UploadBonesAsync(BonesInfo.JSON, bonesSavGz) is not List<ResponseInfo> responseInfos)
                {
                    Debug.CheckNah(catchFlag, Indent: indent[1]);
                    await UIUtils.ShowBummerAsync($"Something whent wrong, {Grammar.MakePossessive(BonesInfo.Name.StartReplace().ToString())} bones failed to upload.");
                    return UIUtils.CascadableResult.Continue;
                }
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "responseInfos.Any => Success";
                if (!responseInfos.Any(ri => ri.Success))
                {
                    string failures = responseInfos
                        ?.Aggregate(
                            seed: (string)null,
                            func: ResponseInfoStatusNewLineDelimitedAggregator)
                        ?? $"{RedCrossIndentLabel}No hosts";

                    Debug.CheckNah(catchFlag, Indent: indent[1]);
                    await UIUtils.ShowBummerAsync($"Something whent wrong uploading {Grammar.MakePossessive(BonesInfo.Name.StartReplace().ToString())} bones:\n{failures}");
                    return UIUtils.CascadableResult.Continue;
                }
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "BonesSources.AddDistinctRange";
                BonesInfo.BonesSources.AddDistinctRange(responseInfos.Where(ri => ri.Host != null).Select(ri => ri.Host));
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "responseInfos.Aggregate";
                string hostsString = responseInfos
                    .Where(ri => ri.Success)
                    .Aggregate(
                        seed: (string)null,
                        func: ResponseInfoStatusNewLineDelimitedAggregator);
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "Popup.NewPopupMessageAsync";
                Debug.CheckYeh(catchFlag, Indent: indent[2]);
                await Popup.NewPopupMessageAsync($"Uploaded {BonesInfo.Name.StartReplace()} to the following hosts:\n{hostsString}", PopupMessage.SingleButton);

                Debug.CheckYeh($"{nameof(ManageBonesUploadAsync)} => {UIUtils.CascadableResult.Continue}", Indent: indent[1]);
                return UIUtils.CascadableResult.Continue;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(ManageBonesUploadAsync)} failed to {nameof(UploadBones)} to hosts [{catchFlag}]", x);
                return UIUtils.CascadableResult.BackSilent;
            }
        }

        [RegisterMethodForDebug(true)]
        public static async Task<UIUtils.CascadableResult> ManageBonesDownloadAsync(SaveBonesInfo BonesInfo)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesInfo), BonesInfo.GetNullString()),
                });

            using var preferredSources = BonesManager.GetBonesSourcesFromPool(BonesSourceExtensions.IsPreferredAndWritable);

            string catchFlag = "top";
            //Utils.Log(catchFlag);
            try
            {
                catchFlag = "BonesInfo.LoadBonesSavGz";
                using var bonesSavGz = await BonesInfo.LoadBonesSavGzAsync();
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "preferredSources.SaveBones";
                var results = preferredSources.SaveBones(BonesInfo.JSON, bonesSavGz);
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "results.IsNullOrEmpty";
                if (results.IsNullOrEmpty())
                {
                    Debug.CheckNah(catchFlag, Indent: indent[1]);
                    await UIUtils.ShowBummerAsync($"Something whent wrong, {BonesInfo.Name.StartReplace()} bones appears to be missing save data.");
                    return UIUtils.CascadableResult.Continue;
                }
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "BonesInfo.BonesSources.AddDistinct";
                foreach ((var bonesSource, var result) in results)
                    if (result)
                        BonesInfo.BonesSources.AddDistinct(bonesSource);
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "successString";
                var successString = results.Aggregate(
                    seed: (string)null,
                    func: (a, n) =>
                    {
                        if (!n.Value)
                            return a;

                        if (!a.IsNullOrEmpty())
                            a += "\n";

                        string extraInfo = n.Value.Type < ActionResult.ResultType.Success
                        ? $" ({n.Value})"
                        : null
                        ;

                        return a + $"{GreenTickIndentLabel}{n.Key?.DisplayName ?? "MISSING_SOURCE"}{extraInfo}";
                    });
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "failureString";
                var failureString = results.Aggregate(
                    seed: (string)null,
                    func: (a, n) =>
                    {
                        if (n.Value)
                            return a;

                        if (!a.IsNullOrEmpty())
                            a += "\n";
                        return a + $"{RedCrossIndentLabel}{n.Key?.DisplayName ?? "MISSING_SOURCE"} ({n.Value})";
                    });
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                catchFlag = "Popup.NewPopupMessageAsync => successString";
                await Popup.NewPopupMessageAsync($"Downloaded {BonesInfo.Name.StartReplace()} to:\n{successString}");
                if (!failureString.IsNullOrEmpty())
                {
                    catchFlag = "UIUtils.ShowBummerAsync => failureString";
                    Debug.YehNah(catchFlag, Indent: indent[2]);
                    await UIUtils.ShowBummerAsync($"Failed to downloaded {BonesInfo.Name.StartReplace()} to:\n{failureString}");
                }
                Debug.CheckYeh(catchFlag, Indent: indent[2]);

                Debug.CheckYeh($"{nameof(ManageBonesDownloadAsync)} => {UIUtils.CascadableResult.Continue}", Indent: indent[1]);
                return UIUtils.CascadableResult.Continue;
            }
            catch (Exception x)
            {
                BonesManager.HoardBonesError(BonesInfo.ID, preferredSources, x, true);
                Utils.Error($"{nameof(ManageBonesDownloadAsync)} failed to write bones to file [{catchFlag}]", x);
                return UIUtils.CascadableResult.BackSilent;
            }
        }

        [RegisterAsyncMethodForDebug(true)]
        public static async Task<List<ResponseInfo>> UploadBonesAsync(
            SaveBonesJSON BonesJSON,
            BonesSavGz BonesSavGz
            )
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(BonesJSON), BonesJSON.GetNullString()),
                    Debug.Arg(nameof(BonesSavGz), BonesSavGz.GetNullString()),
                });

            var originalContext = The.CurrentContext;
            try
            {
                await WebContext;

                Debug.Log($"Getting hostsWithoutThisBones...", Indent: indent[2]);
                var hostsWithoutThisBones = AllEnabledHosts(h => !h.HasPending(BonesJSON.ID) && !h.HasBones(BonesJSON.ID));

                Debug.Log($"await BonesSources.SelectBonesSourcesAsync...", Indent: indent[2]);
                using var hostSources = await BonesSources.SelectBonesSourcesFromPoolAsync(hostsWithoutThisBones, true, false, "Upload");

                if (hostSources == null)
                {
                    Debug.CheckNah($"{nameof(hostSources)} is null", Indent: indent[2]);
                    return null;
                }
                Debug.CheckYeh($"{nameof(hostSources)} not null", Indent: indent[2]);

                if (hostSources.Count < 1)
                {
                    Debug.CheckNah($"{nameof(hostSources)} is empty", Indent: indent[2]);
                    Popup.NewPopupMessageAsync($"Upload of {BonesJSON.Name}'s bones cancelled!", PopupMessage.AcceptButton).WaitResult();
                    return null;
                }
                Debug.CheckYeh($"{nameof(hostSources)} not empty", Indent: indent[2]);

                List<ResponseInfo> successfulHosts = null;
                Debug.Log($"Saving to each host...", Indent: indent[2]);
                foreach (var host in hostSources.Select(bs => bs as Host))
                {
                    ResponseInfo responseInfo = default;
                    try
                    {
                        bool saved = await host.SaveBonesAsync(BonesJSON, BonesSavGz, ri => responseInfo = ri);
                        /*bool saved = false;
                        responseInfo = await Task.Run(() =>
                        {
                            saved = host.SaveBones(BonesJSON, BonesSavGz, out var responseInfo);
                            return responseInfo;
                        });*/

                        Debug.YehNah($"{nameof(responseInfo)}", responseInfo.Message ?? "NO_RESPONSE", Good: !responseInfo.Message.IsNullOrEmpty(), Indent: indent[3]);
                        Debug.YehNah($"{host} {nameof(saved)}?", Good: saved, Indent: indent[3]);
                        successfulHosts ??= new();
                        successfulHosts.Add(responseInfo);
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to upload bones to {host}; {responseInfo.Message ?? "NO_RESPONSE"}", x);
                        continue;
                    }
                }
                return successfulHosts;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(UploadBones)} failed to upload Bones with BonesID {BonesJSON?.ID ?? "NO_BONES_ID"}", x);
                return null;
            }
            finally
            {
                await originalContext;
            }
        }

        public static async Task<bool> TryUploadBonesAsync(
            SaveBonesJSON BonesJSON,
            BonesSavGz BonesSavGz
            )
            => (await UploadBonesAsync(BonesJSON, BonesSavGz)) is List<ResponseInfo> responseInfos
            && responseInfos.Any(ri => ri.Success)
            ;

        public static List<ResponseInfo> UploadBones(
            SaveBonesJSON BonesJSON,
            BonesSavGz BonesSavGz
            )
            => UploadBonesAsync(BonesJSON, BonesSavGz).WaitResult()
            ;

        public static bool TryUploadBones(
            SaveBonesJSON BonesJSON,
            BonesSavGz BonesSavGz
            )
            => TryUploadBonesAsync(BonesJSON, BonesSavGz).WaitResult()
            ;

        public static async Task<bool> TryReportBonesAsync(
            Report Report,
            bool Silent = false,
            bool ClearReportsCache = true
            )
        {
            if (Report == null)
                return false;

            // Report.SeverityLevelOption = Options.ModerationMinimumSeverityLevel;

            try
            {
                bool any = false;
                foreach (var host in AllEnabledHosts().IteratorSafe())
                {
                    try
                    {
                        if (await host.PostBonesReport(Report, ClearReportsCache))
                            any = true;
                        else
                            Utils.Warn($"Failed to upload Bones Report to {host}");
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to upload Bones Report to {host}", x);
                        continue;
                    }
                }
                if (!Silent)
                {
                    if (any)
                        await Popup.ShowAsync("Thank you! Your report was successfully submitted to at least one host.");
                    else
                        await Popup.ShowAsync("Unfortunately, none of your currently enabled hosts were able to receive your report.");
                }
                return any;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(TryUploadBones)} failed to upload Bones Report for BonesID {Report.BonesID}", x);
                return false;
            }
        }

        public static async Task<bool> TryReportBonesAsync(
            string BonesID,
            GameObject ReportedObject,
            bool Silent = false,
            bool ClearReportsCache = true
            )
        {
            using var report = await AskForBonesReportAsync(BonesID, ReportedObject);

            return report != null
                && await TryReportBonesAsync(report, Silent, ClearReportsCache)
                ;
        }

        public static bool IsBonesReported(string BonesID)
        {
            if (BonesID.IsNullOrEmpty())
                return false;

            try
            {
                return !Hosts
                    .GetReportsMatchingSpec(BonesID: BonesID)
                    .IsNullOrEmpty();
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(IsBonesReported)} failed to check any hosts for Bones Reports with BonesID {BonesID}", x);
                return false;
            }
        }

        public static bool IsBonesBlocked(string BonesID)
        {
            if (BonesID.IsNullOrEmpty()
                || Configuration.Instance == null)
                return false;

            try
            {
                foreach (var host in AllEnabledHosts())
                {
                    if (host.IsBonesBlocked(BonesID))
                        return true;

                    /*try
                    {
                        if (host.ReportsCache.IsNullOrEmpty())
                            continue;

                        if (host.ReportsCache.Any(r => r.BonesID == BonesID && r.UserID == Configuration.Instance.ID && r.Blocked))
                            return true;
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to check {host} for blocked Bones Reports for BonesID {BonesID}", x);
                        continue;
                    }*/
                }
                return false;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(IsBonesBlocked)} failed to check any hosts for blocked Bones Reports with BonesID {BonesID}", x);
                return false;
            }
        }

        private static async Task<Report> AskForBonesReportAsync(
            string BonesID,
            GameObject ReportedObject
            )
        {
            Report.ObjectReportDetails reportObjectDetails = null;
            if (ReportedObject != null)
            {
                reportObjectDetails = Report.ObjectReportDetails.FromGameObject(ReportedObject);
                if (reportObjectDetails == null)
                {
                    await Popup.ShowAsync($"Something about {ReportedObject?.DebugName ?? "MISSING_OBJECT"} is making reporting from it unavailable.\n\n" +
                        $"Please consider making your report from another object loaded by the offending bones file.");
                    return null;
                }
            }

            var report = new Report
            {
                UserID = Configuration.Instance.ID,
                BonesID = BonesID,
                ObjectDetails = reportObjectDetails,
                Description = "",
            };
            using var options = new CascadablePickOptionDataSetAsync<Report>(report);
            List<QudMenuItem> buttons = null;
            Dictionary<int, Func<Task<UIUtils.CascadableResult>>> buttonCallbacks = null;
            UIUtils.CascadableResult result;

            using var tB = TextBuilder.Get();
            int defaultSelection = 0;
            do
            {
                tB.Clear();
                options.ClearItems(Dispose: true);

                tB.Append("Please use the provided options to fill out your report about ");
                if (reportObjectDetails != null)
                {
                    tB.Append("the bones that loaded the below object:")
                        .AppendLine()
                        .AppendLine().AppendBonesReportedObject(reportObjectDetails)
                        ;
                }
                else
                    tB.Append("these bones.");

                tB.AppendLine()
                    .AppendLine().Append("Below is what your current report contains:")
                    .AppendLine()
                    .AppendLine().AppendBonesReport(report, "{{W|Report}}")
                    ;

                if (!report.IsValid)
                {
                    tB.AppendLine()
                        .AppendLine().Append("Report is missing ")
                        .AppendColored("red", "necessary information")
                        .Append(", please add this info in order to proceed.")
                        ;
                }

                string reportTypeString = report.Type.ToString();
                if (report.Type == Report.ReportTypes.None)
                    reportTypeString = reportTypeString.Colored("red");

                string enterDescriptionText = "Enter a Description";
                if (!report.Description.IsNullOrEmpty())
                    enterDescriptionText = "Modify Description";

                options.Add(new CascadablePickOptionDataAsync<Report>
                {
                    Text = $"{report.Blocked.GetCheckboxText("Block This Bones File")}",
                    Hotkey = 'b',
                    Callback = element => Task.Run(delegate ()
                    {
                        report.Blocked = !report.Blocked;
                        return UIUtils.CascadableResult.Continue;
                    }),
                });

                options.Add(new CascadablePickOptionDataAsync<Report>
                {
                    Text = $"Report Type: {reportTypeString}",
                    Hotkey = 't',
                    Callback = element => Task.Run(async delegate ()
                    {
                        element.Type = await AskBonesReportTypeAsync();
                        return UIUtils.CascadableResult.Continue;
                    }),
                });
                if (reportObjectDetails != null)
                {
                    options.Add(new CascadablePickOptionDataAsync<Report>
                    {
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
                }
                options.Add(new CascadablePickOptionDataAsync<Report>
                {
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
                    Title: Report.ReportBonesTitle,
                    Intro: tB.AppendLineEnd().ToString(),
                    IntroIcon: Report.ReportBonesIcon,
                    AdditionalButtons: buttons,
                    DefaultSelected: defaultSelection,
                    LastSelectedCallback: c => defaultSelection = Math.Clamp(c, 0, options.Count - 1),
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
            }
            while (result.IsContinue());

            if (report?.IsValid is not true)
            {
                if (!result.IsSilent())
                    await Popup.ShowAsync($"Report cancelled.");

                return null;
            }

            return report;
        }

        private static async Task<Report.ReportTypes> AskBonesReportTypeAsync()
        {
            using var options = new PickOptionDataSetAsync<Report.ReportTypes, Report.ReportTypes>();

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
                Title: Report.ReportBonesTitle,
                Intro: $"What is it about these bones that you'd like to report?\n\n",
                IntroIcon: Report.ReportBonesIcon,
                OnBackCallback: () => Task.Run(() => Report.ReportTypes.None),
                OnEscapeCallback: () => Task.Run(() => Report.ReportTypes.None));
        }

        public static BonesRecord GetBonesRecord(this SaveBonesInfo BonesInfo)
        {
            var bonesJSON = BonesInfo.JSON.CloneForUpload(
                out var bonesSpec,
                out var bonesStats,
                out var modConfig
                );

            return new BonesRecord
            {
                UserID = Configuration.Instance.ID,
                BonesID = BonesInfo.BonesID,
                SaveBonesJSON = bonesJSON,
                BonesSpec = bonesSpec,
                BonesStats = bonesStats,
                ModConfig = modConfig,
                Size = (int)bonesJSON.Size,
            };
        }

        public static BonesRecord GetBonesRecord(this SaveBonesJSON BonesJSON)
        {
            var bonesJSON = BonesJSON.CloneForUpload(
                out var bonesSpec,
                out var bonesStats,
                out var modConfig
                );

            return new BonesRecord
            {
                UserID = Configuration.Instance.ID,
                BonesID = BonesJSON.BonesID,
                SaveBonesJSON = bonesJSON,
                BonesSpec = bonesSpec,
                BonesStats = bonesStats,
                ModConfig = modConfig,
                Size = (int)bonesJSON.Size,
            };
        }

        public static string DisplayString(this HttpStatusCode StatusCode)
                => $"{StatusCode} ({(int)StatusCode})"
                ;

        public static IEnumerable<Host> GetHosts(this BonesSources BonesSources)
            => (BonesSources
                ?.Where(bs => bs is Host)
                ?.Select(bs => bs as Host))
            ?.IteratorSafe()
            ;

        public static bool AnySameAs(this IEnumerable<Host> Source, IEnumerable<Host> Hosts)
            => Source?.Any(th => Hosts?.Any(h => th.SameAs(h)) is true) is true
            ;

        public static bool NoneSameAs(this IEnumerable<Host> Source, IEnumerable<Host> Hosts)
            => !Source.AnySameAs(Hosts)
            ;

        public static bool AnyNotSameAs(this IEnumerable<Host> Source, IEnumerable<Host> Hosts)
            => !Utils.EitherNullOrEmpty(Source, Hosts, out bool areEqual)
            ? Source.Any(sh => !Hosts.Any(h => sh.SameAs(h)))
            : !areEqual
            ;

        public static bool AnyHostsMissing(this BonesSources BonesSources, IEnumerable<Host> Hosts)
        {
            if (BonesSources?.GetHosts() is not IEnumerable<Host> bonesSourcesHosts)
                return false;

            return bonesSourcesHosts.IsNullOrEmpty()
                ? !Hosts.IsNullOrEmpty()
                : bonesSourcesHosts.AnyNotSameAs(Hosts)
                ;
        }
    }
}
