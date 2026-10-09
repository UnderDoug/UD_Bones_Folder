using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

using XRL;

using XRL.UI;
using XRL.World;
using UD_Bones_Folder.Mod.UI;
using Qud.UI;

using static UD_Bones_Folder.Mod.Host;
using XRL.Collections;
using XRL.World.Text;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [HasModSensitiveStaticCache]
    [HasCallAfterGameLoaded]
    public static class ServerUtils
    {
        private static Rack<HostSet> _Hosts;
        public static Rack<HostSet> Hosts => _Hosts ??= ReadHostCollections()?.WaitResult();

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

        public static async Task<Rack<HostSet>> ReadHostCollections()
        {
            Rack<HostSet> hosts = null;
            foreach (var fileLocationData in Configuration.GetOsseousAshFileLocationData(Ensure: true).IteratorSafe())
            {
                try
                {
                    hosts ??= new();
                    if (fileLocationData.FileExists(HostSet.HostsFileName)
                        && (await HostSet.ReadFromFileAsync(fileLocationData, HostSet.HostsFileName)) is HostSet loadedHostCollection)
                    {
                        loadedHostCollection.LocationData = fileLocationData;
                        hosts.Add(loadedHostCollection);
                    }
                    else
                    {
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
            if (ReadHostCollections().WaitResult() is not Rack<HostSet> readHosts)
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

        public static async Task<List<ResponseInfo>> UploadBonesAsync(
            string BonesID,
            SaveBonesJSON SaveBonesJSON,
            byte[] SavGz
            )
        {
            try
            {
                List<ResponseInfo> successfulHosts = null;
                foreach (var host in Configuration.AllEnabledHosts().IteratorSafe())
                {
                    try
                    {
                        var responseInfo = await host.TryUploadBonesAsync(
                            SaveBonesJSON: SaveBonesJSON,
                            PendingSavGz: new PendingSavGz
                            {
                                ParentHost = host,
                                BonesID = BonesID,
                                SavGz = SavGz,
                            });

                        successfulHosts ??= new();
                        successfulHosts.Add(responseInfo);
                    }
                    catch (Exception x)
                    {
                        Utils.Error($"Failed to upload bones to {host}", x);
                        continue;
                    }
                }
                return successfulHosts;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(TryUploadBonesAsync)} failed to upload Bones with BonesID {BonesID}", x);
                return null;
            }
        }

        public static async Task<bool> TryUploadBonesAsync(
            string BonesID,
            SaveBonesJSON SaveBonesJSON,
            byte[] SavGz
            )
            => (await UploadBonesAsync(BonesID, SaveBonesJSON, SavGz)) is List<ResponseInfo> responseInfos
            && responseInfos.Any(ri => ri.Success)
            ;

        public static List<SaveBonesInfo> GetBonesInfos()
        {
            //Utils.Log($"{1.Indent()}GetBonesInfos");
            List<SaveBonesInfo> saveBonesInfos = null;
            bool any = false;
            foreach (var host in Configuration.AllHosts(h => h.Enabled).IteratorSafe())
            {
                any = true;
                //Utils.Log($"{1.Indent()}{host} - GetSaveBonesInfos");
                if (host.GetSaveBonesInfos() is not IEnumerable<SaveBonesInfo> saveBonesInfosFromHost
                    || saveBonesInfosFromHost.IsNullOrEmpty())
                    continue;

                foreach (var hostedSaveBonesInfo in saveBonesInfosFromHost)
                {
                    if (saveBonesInfos?.FirstOrDefault(b => b.ID == hostedSaveBonesInfo.ID) is SaveBonesInfo existingInfo
                        && existingInfo.FileLocationData != null)
                    {
                        _ = existingInfo.FileLocationData;
                        _ = hostedSaveBonesInfo.FileLocationData;
                        existingInfo.FileLocationDataSet.UnionWith(hostedSaveBonesInfo.FileLocationDataSet);
                        continue;
                    }
                    saveBonesInfos ??= new();
                    saveBonesInfos.Add(hostedSaveBonesInfo);
                }
            }

            if (BonesManager.System is BonesManager system)
                system.IgnoreCache = any;

            return saveBonesInfos;
        }

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
                foreach (var host in Configuration.AllHosts(h => h.Enabled).IteratorSafe())
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
                Utils.Error($"{nameof(TryUploadBonesAsync)} failed to upload Bones Report for BonesID {Report.BonesID}", x);
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
                foreach (var host in Configuration.AllEnabledHosts())
                {
                    try
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
                    }
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
    }
}
