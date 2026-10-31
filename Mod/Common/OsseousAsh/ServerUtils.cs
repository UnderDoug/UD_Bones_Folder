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

namespace UD_Bones_Folder.Mod.OsseousAsh
{
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
                    { nameof(ReadHostSetsAsync), true },
                });
        #endregion

        public static string RedCrossIndentLabel => $"{Const.NBSP}{Const.NBSP}{Const.CROSS.Colored("red")}{Const.NBSP}";
        public static string GreenDFIndentLabel => $"{Const.NBSP}{Const.NBSP}{Const.DF.Colored("green")}{Const.NBSP}";
        public static string GreenTickIndentLabel => $"{Const.NBSP}{Const.NBSP}{Const.TICK.Colored("green")}{Const.NBSP}";
        public static bool EnableOsseousAshUploads => Options.EnableOsseousAshUploads;
        public static bool EnableOsseousAshDownloads => Options.EnableOsseousAshDownloads;

        private static Rack<HostSet> _Hosts;
        public static Rack<HostSet> Hosts => _Hosts ??= ReadHostSetsAsync()?.WaitResult();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        internal static List<DoWhatWithHostPickOptionsDelegateEntry> DoWhatWithHostPickOptionsDelegates;

        private static IEnumerable<Report> _ReportsCache;
        public static IEnumerable<Report> ReportsCache
        {
            get
            {
                if (_ReportsCache == null)
                {
                    var cachedReports = Hosts.CacheReports();
                    _ReportsCache = cachedReports;
                }

                return _ReportsCache;
            }
        }


        [BonesSourcesDelegate(IBonesSource.SourceType.Online)]
        public static IEnumerable<IBonesSource> OnlineBonesSources()
            => AllEnabledHosts()
            ;

        [ModSensitiveCacheInit]
        public static void EnsureHosts()
        {
            if (!VersionRoutes.Init)
                VersionRoutes.InitializeRoutes();

            _ = Hosts;
            Hosts?.BuildHosts(Ping: true);
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
            Debug.LogCaller(Indent: indent[1]);

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
            _ReportsCache = null;
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

        [ManageBonesPickOptionsDelegate]
        public static void OnlineBonesManagementOptions(SaveBonesInfo BonesInfo, ref CascadablePickOptionDataSetAsync<SaveBonesInfo> Options)
        {
            // View Stats (5)

            // Manage ModConfig (10)

            if (EnableOsseousAshUploads
                && BonesInfo.BonesSources.AnyHostsMissing(AllEnabledHosts()))
            {
                if (BonesInfo.BonesSources.AnyHostsMissing(AllEnabledHosts(h => !h.HasPending(BonesInfo))))
                {
                    Options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                    {
                        Text = $"Upload",
                        Priority = 12,
                        Icon = Configuration.UploadIcon,
                        Hotkey = Options.GetFirstAvailableHotkey(
                        new char[]
                        {
                            'u',
                            'U',
                            '^',
                        }),
                        Callback = ManageBonesUploadAsync,
                    });
                }
                else
                {
                    Options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                    {
                        Text = $"Upload ({"pending...".Colored("C")})",
                        Priority = 12,
                        Icon = Configuration.UploadIcon,
                        Hotkey = Options.GetFirstAvailableHotkey(
                        new char[]
                        {
                            'u',
                            'U',
                            '^',
                        }),
                        Callback = e => Task.Run(() => UIUtils.CascadableResult.Continue),
                    });
                }
            }

            if (BonesInfo.BonesSources.Any(bs => bs.IsOnline()))
            {
                if (EnableOsseousAshDownloads
                    && BonesInfo.BonesSources.All(bs => !bs.IsOnDisk()))
                {
                    Options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                    {
                        Text = $"Download",
                        Priority = 13,
                        Icon = Configuration.DownloadIcon,
                        Hotkey = Options.GetFirstAvailableHotkey(
                        new char[]
                        {
                            'd',
                            'D',
                            'v',
                            'V',
                        }),
                        Callback = ManageBonesDownloadAsync,
                    });
                }
            }

            // Block Bones (15)

            bool isBlocked = BonesInfo.IsBlocked;
            if (BonesInfo.BonesSources.Any(bs => bs.IsOnline()))
            {
                if (!isBlocked)
                {
                    Options.Add(new CascadablePickOptionDataAsync<SaveBonesInfo>
                    {
                        Text = "Report",
                        Priority = 17,
                        Icon = Report.ReportBonesIcon,
                        Hotkey = Options.GetFirstAvailableHotkey(
                        new char[]
                        {
                            'r',
                            'R',
                            'x',
                            'X',
                        }),
                        Callback = SaveBonesInfo.AskForBonesReportAsync,
                    });
                }
            }

            // Cremate (20)
        }

        private static string HostString(this Host Host)
            => Host?.ToString() ?? "MISSING_HOST"
            ;

        public static string ResponseInfoStatusNewLineDelimitedAggregator(string Accumulator, ResponseInfo Next)
            => Utils.NewLineDelimitedAggregator(Accumulator, $"{(Next.Success ? GreenDFIndentLabel : RedCrossIndentLabel)}{HostString(Next.Host)}{Next.AppendFailureMessage()}")
            ;

        public static async Task<UIUtils.CascadableResult> ManageBonesUploadAsync(SaveBonesInfo BonesInfo)
        {
            string catchFlag = "top";
            //Utils.Log(catchFlag);
            try
            {
                catchFlag = "try";
                //Utils.Log(catchFlag);

                if (await BonesInfo.GetBonesSavGzStreamAsync() is not BonesSavGz bonesSavGz)
                {
                    await Popup.ShowAsync($"Something whent wrong, {Grammar.MakePossessive(BonesInfo.Name.StartReplace().ToString())} bones appears to be missing save data.");
                    return UIUtils.CascadableResult.Continue;
                }

                if (!bonesSavGz.TryEnsureSavable())
                {
                    bonesSavGz.Dispose();
                    await Popup.ShowAsync($"Something whent wrong, {Grammar.MakePossessive(BonesInfo.Name.StartReplace().ToString())} bones appears to be missing save data.");
                    return UIUtils.CascadableResult.Continue;
                }

                catchFlag = "BonesManager.UploadBonesFileAsync";
                //Utils.Log(catchFlag);

                if (UploadBones(BonesInfo.JSON, bonesSavGz) is List<ResponseInfo> responseInfos)
                {
                    if (!responseInfos.Any(ri => ri.Success))
                    {
                        string failures = responseInfos.Aggregate(
                            seed: (string)null,
                            func: ResponseInfoStatusNewLineDelimitedAggregator)
                            ?? $"{RedCrossIndentLabel}No hosts";

                        await Popup.ShowAsync($"Something whent wrong uploading {Grammar.MakePossessive(BonesInfo.Name.StartReplace().ToString())} bones:\n{failures}");
                        return UIUtils.CascadableResult.Continue;
                    }

                    foreach (var responseInfo in responseInfos)
                        if (responseInfo.Host is Host host)
                            BonesInfo.BonesSources.AddDistinct(host);

                    catchFlag = "successFullHosts.Aggregate";
                    //Utils.Log(catchFlag);
                    string hostsString = responseInfos
                        .Where(ri => ri.Success)
                        .Aggregate(
                            seed: (string)null,
                            func: ResponseInfoStatusNewLineDelimitedAggregator);

                    catchFlag = "Popup.ShowAsync";
                    //Utils.Log(catchFlag);
                    await Popup.ShowAsync($"Uploaded {BonesInfo.Name.StartReplace()} to the following hosts:\n{hostsString}");
                    return UIUtils.CascadableResult.Continue;
                }
                catchFlag = "Fell Through Upload";

                // Utils.Log(catchFlag);
                await Popup.ShowAsync($"Something whent wrong, {Grammar.MakePossessive(BonesInfo.Name.StartReplace().ToString())} bones failed to upload.");
                return UIUtils.CascadableResult.Continue;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(UploadBones)} failed to upload bones to hosts at {nameof(catchFlag)} {catchFlag}", x);
                return UIUtils.CascadableResult.BackSilent;
            }
        }

        public static async Task<UIUtils.CascadableResult> ManageBonesDownloadAsync(SaveBonesInfo BonesInfo)
        {
            using var preferredSources = BonesManager.GetBonesSourcesFromPool(BonesSourceExtensions.IsPreferredAndWritable);

            string catchFlag = "top";
            //Utils.Log(catchFlag);
            try
            {
                catchFlag = "try";
                //Utils.Log(catchFlag);

                catchFlag = "BonesInfo.LoadBonesSavGz";
                //Utils.Log(catchFlag);
                using var bonesSavGz = await BonesInfo.LoadBonesSavGzAsync();

                catchFlag = "preferredSources.SaveBones";
                //Utils.Log(catchFlag);
                var results = preferredSources.SaveBones(BonesInfo.JSON, bonesSavGz);

                if (!results.IsNullOrEmpty())
                {
                    catchFlag = "BonesInfo.BonesSources.AddDistinct";
                    foreach ((var bonesSource, var result) in results)
                        if (result)
                            BonesInfo.BonesSources.AddDistinct(bonesSource);

                    catchFlag = "successString";
                    //Utils.Log(catchFlag);
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

                    catchFlag = "failureString";
                    //Utils.Log(catchFlag);
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

                    catchFlag = "Popup.ShowAsync => successString";
                    //Utils.Log(catchFlag);
                    await Popup.ShowAsync($"Downloaded {BonesInfo.Name.StartReplace()} to:\n{successString}");
                    if (!failureString.IsNullOrEmpty())
                    {
                        catchFlag = "Popup.ShowAsync => failureString";
                        //Utils.Log(catchFlag);
                        await Popup.ShowAsync($"Failed to downloaded {BonesInfo.Name.StartReplace()} to:\n{failureString}");
                    }

                    return UIUtils.CascadableResult.Continue;
                }

                // Utils.Log("Fell Through");
                await Popup.ShowAsync($"Something whent wrong, {BonesInfo.Name.StartReplace()} bones appears to be missing save data.");
                return UIUtils.CascadableResult.Continue;
            }
            catch (Exception x)
            {
                BonesManager.BonesHoardError(BonesInfo.ID, preferredSources, x, true);
                Utils.Error($"{nameof(ManageBonesDownloadAsync)} failed to write bones to file [{catchFlag}]", x);
                return UIUtils.CascadableResult.BackSilent;
            }
        }

        public static List<ResponseInfo> UploadBones(
            SaveBonesJSON BonesJSON,
            BonesSavGz BonesSavGz
            )
        {
            try
            {
                List<ResponseInfo> successfulHosts = null;
                foreach (var host in AllEnabledHosts().IteratorSafe())
                {
                    ResponseInfo responseInfo = default;
                    try
                    {
                        host.SaveBones(BonesJSON, BonesSavGz, out responseInfo);

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
        }

        public static bool TryUploadBones(
            SaveBonesJSON BonesJSON,
            BonesSavGz BonesSavGz
            )
            => UploadBones(BonesJSON, BonesSavGz) is List<ResponseInfo> responseInfos
            && responseInfos.Any(ri => ri.Success)
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
            var bonesJSON = BonesInfo.JSON.Clone();
            var bonesSpec = bonesJSON.BonesSpec;
            var bonesStats = bonesJSON.Stats;
            var modConfig = bonesJSON.EnabledMods;

            bonesJSON.BonesSpec = null;
            bonesJSON.Stats = null;
            bonesJSON.EnabledMods = null;

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
            var bonesJSON = BonesJSON.Clone();
            var bonesSpec = bonesJSON.BonesSpec;
            var bonesStats = bonesJSON.Stats;
            var modConfig = bonesJSON.EnabledMods;

            bonesJSON.BonesSpec = null;
            bonesJSON.Stats = null;
            bonesJSON.EnabledMods = null;

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

        public static bool AnyHostsMissing(this BonesSources BonesSources, IEnumerable<Host> Hosts)
            => BonesSources?.GetHosts()?.Any(bs => !Hosts.Any(h => bs.SameAs(h))) is true
            ;
    }
}
