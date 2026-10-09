using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using XRL;
using XRL.Collections;

using static UD_Bones_Folder.Mod.Host;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [HasModSensitiveStaticCache]
    [HasRegistersRoutes]
    public static class VersionRoutes
    {
        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        public static Dictionary<int, RouteSet> RoutesByVersion = new();

        [ModSensitiveStaticCache(CreateEmptyInstance = true)]
        private static List<MethodInfo> ValidMethods = new();

        private static bool _Init;
        public static bool Init
        {
            get => _Init;
            private set => _Init = value;
        }

        [ModSensitiveCacheInit]
        public static void InitializeRoutes()
        {
            if (Init)
                return;

            try
            {
                if (ModManager.GetMethodsWithAttribute(typeof(RegistersRoutesAttribute), typeof(HasRegistersRoutesAttribute)) is List<MethodInfo> methods)
                {
                    if (ValidMethods.IsNullOrEmpty())
                    {
                        ValidMethods ??= new();
                        foreach (var method in methods)
                        {
                            var methodMod = ModManager.GetMod(method.DeclaringType.Assembly);
                            string toUse = $"to be used by {nameof(RegistersRoutesAttribute)}";
                            if (!method.IsStatic)
                            {
                                MetricsManager.LogModWarning(methodMod, $"{method.Name} must be static {toUse}");
                                continue;
                            }

                            if (method.GetParameters() is not ParameterInfo[] paramInfos
                                || paramInfos.Length < 1)
                            {
                                if (!method.ReturnType.InheritsFrom(typeof(RouteSet)))
                                {
                                    MetricsManager.LogModWarning(methodMod, $"{method.Name} must have at least one parameter ({typeof(int).Name} or {nameof(RouteSet)}) or return {nameof(RouteSet)} {toUse}");
                                    continue;
                                }
                                paramInfos = new ParameterInfo[0];
                            }

                            Type param1 = null;
                            Type param2 = null;
                            bool haveRouteSet = false;
                            bool returnsRouteSet = method.ReturnType.InheritsFrom(typeof(RouteSet));
                            if (paramInfos.Length > 0)
                            {
                                param1 = paramInfos[0].ParameterType;
                                bool isInt = param1.InheritsFrom(typeof(int));
                                bool isRouteSet = param1.InheritsFrom(typeof(RouteSet));

                                if (isRouteSet)
                                    haveRouteSet = true;

                                if (!isInt
                                    && !isRouteSet)
                                {
                                    MetricsManager.LogModWarning(methodMod, $"{method.Name}'s first parameter must be either {typeof(int).Name} or {nameof(RouteSet)} {toUse}");
                                    continue;
                                }
                                if (paramInfos.Length < 2
                                    && !paramInfos[0].ParameterType.InheritsFrom(typeof(RouteSet))
                                    && !returnsRouteSet)
                                {
                                    MetricsManager.LogModWarning(methodMod, $"{method.Name} must return a {nameof(RouteSet)} if its only parameter isn't also a {nameof(RouteSet)} {toUse}");
                                    continue;
                                }
                            }

                            if (paramInfos.Length > 1)
                            {
                                param2 = paramInfos[1].ParameterType;
                                bool isInt = param2.InheritsFrom(typeof(int));
                                bool isRouteSet = param2.InheritsFrom(typeof(RouteSet));

                                if (isRouteSet)
                                    haveRouteSet = true;

                                if (!isInt
                                    && !isRouteSet)
                                {
                                    MetricsManager.LogModWarning(methodMod, $"{method.Name}'s second parameter must be either {typeof(int).Name} or {nameof(RouteSet)} {toUse}");
                                    continue;
                                }
                                if (!haveRouteSet
                                    && !returnsRouteSet)
                                {
                                    MetricsManager.LogModWarning(methodMod, $"{method.Name} must return a {nameof(RouteSet)} if neither of its first two parameters aren't a {nameof(RouteSet)} {toUse}");
                                    continue;
                                }
                                if (param1.InheritsFrom(param2)
                                    || param2.InheritsFrom(param1))
                                {
                                    MetricsManager.LogModWarning(methodMod, $"{method.Name}'s first two parameters cannot be the same {toUse}");
                                    continue;
                                }
                            }

                            ValidMethods.Add(method);
                        }
                    }
                    Utils.Info($"Registering Routes...");
                    for (int i = 0; i < 2; i++)
                    {
                        var routeSet = new RouteSet(DefaultUnsupported: false, Version: i);
                        foreach (var method in ValidMethods.IteratorSafe())
                        {
                            var paramTypes = method.GetParameters().IteratorSafe().Select(pi => pi.ParameterType);
                            using var paramValues = ScopeDisposedList<object>.GetFromPool();
                            bool haveInt = false;
                            bool haveRouteSet = false;
                            foreach (var paramType in paramTypes)
                            {
                                object value = null;
                                if (paramType.InheritsFrom(typeof(int))
                                    && !haveInt)
                                {
                                    value = Convert.ChangeType(i, paramType);
                                    haveInt = true;
                                }
                                else
                                if (paramType.InheritsFrom(typeof(RouteSet))
                                    && !haveRouteSet)
                                {
                                    haveRouteSet = true;
                                    value = Convert.ChangeType(routeSet, paramType);
                                }

                                paramValues.Add(value);
                            }
                            if (method.Invoke(null, paramValues.ToArray()) is RouteSet returnValue)
                                routeSet = returnValue;
                        }

                        routeSet.FillInUnsupported();
                        routeSet.LogRoutes(1);

                        RoutesByVersion ??= new();
                        RoutesByVersion[i] = routeSet;
                    }
                }
            }
            finally
            {
                Init = true;
            }
        }

        [RegistersRoutes]
        public static RouteSet RegisterRoutes(int Version, RouteSet RouteSet)
        {
            if (Version == 0)
            {
                // Status
                RouteSet.Status ??= new(WebMethods.GET, "status");

                // User
                RouteSet.CanUpload ??= new(WebMethods.GET, "canUp/:UserID");

                // Bones
                RouteSet.NewBonesInfo ??= new(WebMethods.POST, "Bones");
                RouteSet.NewBonesSavGz ??= new(WebMethods.PUT, "Bones/SavGz/:BonesID", "gz");

                RouteSet.BonesInfo ??= new(WebMethods.GET, "Bones/Info/:BonesID");
                RouteSet.BonesInfos ??= new(WebMethods.GET, "Bones/Infos");

                RouteSet.BonesSpec ??= new(WebMethods.GET, "Bones/Spec/:BonesID");
                RouteSet.BonesSpecs ??= new(WebMethods.GET, "Bones/Specs");

                RouteSet.UpdateBonesStats ??= new(WebMethods.PUT, "Bones/Stats/:BonesID/:UserID");

                RouteSet.LoadBones ??= new(WebMethods.GET, "Bones/SavGz/:BonesID", "gz");
                RouteSet.DownloadBones ??= new(WebMethods.POST, "Bones/Download/:BonesID/:UserID", "gz");

                RouteSet.CheckBonesID ??= new(WebMethods.GET, "Bones/ID/:BonesID");
                RouteSet.BonesIDs ??= new(WebMethods.GET, "Bones/IDs");

                RouteSet.DeleteBones ??= new(WebMethods.DELETE, "Bones/del/:BonesID");
                // RouteSet.WipeBones ??= new(WebMethods.DELETE, "del/Bones");

                // Report
                RouteSet.NewReport ??= new(WebMethods.POST, "Report");

                RouteSet.Reported ??= new(WebMethods.GET, "Report/Check/:BonesID/:UserID");
                // RouteSet.Report ??= new(WebMethods.GET, "ReportID/:ReportID");
                // RouteSet.Reports ??= new(WebMethods.GET, "Reports");

                RouteSet.ReportByBonesAndUser ??= new(WebMethods.GET, "Reports/:BonesID/:UserID");
                // RouteSet.ReportByBones ??= new(WebMethods.GET, "Reports/Bones/:BonesID");
                RouteSet.ReportByUser ??= new(WebMethods.GET, "Reports/OAID/:UserID");

                // RouteSet.DeleteReport ??= new(WebMethods.DELETE, "Report/del/:ReportID");
                RouteSet.DeleteReportByBonesAndUser ??= new(WebMethods.DELETE, "del/Reports/:BonesID/:UserID");
                // RouteSet.DeleteReportByBones ??= new(WebMethods.DELETE, "del/Report/Bones/:BonesID");
                // RouteSet.DeleteReportByUser ??= new(WebMethods.DELETE, "del/Report/OAID/:UserID");
                // RouteSet.WipeReports ??= new(WebMethods.DELETE, "del/Reports");
            }
            else
            if (Version == 1)
            {
                // Status
                RouteSet.Status ??= new(WebMethods.GET, "status");

                // User
                RouteSet.CanUpload ??= new(WebMethods.GET, "canUp/:UserID");

                RouteSet.NewUser ??= new(WebMethods.POST, "User/new");
                RouteSet.UpdateUser ??= new(WebMethods.POST, "User/Update/:UserID");

                RouteSet.Login ??= new(WebMethods.POST, "User/Login");

                RouteSet.User ??= new(WebMethods.GET, "User/:UserID");
                // RouteSet.Users ??= new(WebMethods.GET, "Users");
                // RouteSet.UserStatus ??= new(WebMethods.POST, "User/Status/:UserID");
                // RouteSet.UserHandle ??= new(WebMethods.POST, "User/Handle/:UserID");

                // Bones
                RouteSet.NewBonesInfo ??= new(WebMethods.POST, "Bones/new");
                RouteSet.NewBonesSavGz ??= new(WebMethods.PUT, "Bones/SavGz/:BonesID");

                RouteSet.BonesInfo ??= new(WebMethods.GET, "Bones/Info/:BonesID/:UserID");
                RouteSet.BonesInfos ??= new(WebMethods.GET, "Bones/Infos/:UserID");
                RouteSet.BonesPagedInfos ??= new(WebMethods.GET, "/Bones/Paged/Infos/:from-:to/:UserID");

                RouteSet.BonesSpec ??= new(WebMethods.GET, "Bones/Spec/:BonesID");
                RouteSet.BonesSpecs ??= new(WebMethods.GET, "Bones/Specs");

                RouteSet.UpdateBonesStats ??= new(WebMethods.PUT, "Bones/Stats/:Stat/:BonesID/:UserID");

                RouteSet.LoadBones ??= new(WebMethods.GET, "Bones/SavGz/:BonesID");
                RouteSet.DownloadBones ??= new(WebMethods.POST, "Bones/Download/:BonesID/:UserID");

                RouteSet.CheckBonesID ??= new(WebMethods.GET, "Bones/ID/:BonesID");
                RouteSet.BonesIDs ??= new(WebMethods.GET, "Bones/IDs");

                RouteSet.DeleteBones ??= new(WebMethods.DELETE, "Bones/del/:BonesID");
                // RouteSet.WipeBones ??= new(WebMethods.DELETE, "del/Bones");

                // Report
                RouteSet.NewReport ??= new(WebMethods.POST, "Report/new");

                // RouteSet.UpdateReport ??= new(WebMethods.PUT, "Report/:ReportID");

                RouteSet.Reported ??= new(WebMethods.GET, "Report/Check/:BonesID/:UserID");
                // RouteSet.Report ??= new(WebMethods.GET, "Report/:ReportID");
                RouteSet.Reports ??= new(WebMethods.GET, "Reports");

                RouteSet.ReportByBonesAndUser ??= new(WebMethods.GET, "Reports/:BonesID/:UserID");
                // RouteSet.ReportByBones ??= new(WebMethods.GET, "Reports/Bones/:BonesID");
                RouteSet.ReportByUser ??= new(WebMethods.GET, "Reports/User/:UserID");

                // RouteSet.DeleteReport ??= new(WebMethods.DELETE, "Report/del/:ReportID");
                RouteSet.DeleteReportByBonesAndUser ??= new(WebMethods.DELETE, "del/Reports/:BonesID/:UserID");
                // RouteSet.DeleteReportByBones ??= new(WebMethods.DELETE, "del/Report/Bones/:BonesID");
                // RouteSet.DeleteReportByUser ??= new(WebMethods.DELETE, "del/Report/User/:UserID");
                // RouteSet.WipeReports ??= new(WebMethods.DELETE, "del/Reports");
            }
            return RouteSet;
        }

        public static RouteSet GetFor(int Version)
            => RoutesByVersion.GetValue(Version)
            ?? new()
            ;
    }
}