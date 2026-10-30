using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using XRL;
using XRL.Collections;

using static UD_Bones_Folder.Mod.OsseousAsh.Host;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [HasModSensitiveStaticCache]
    [HasRegistersRoutes]
    public static class VersionRoutes
    {
        public const int MIN_ROUTE_VERSION = 1;

        public const int MAX_ROUTE_VERSION = 1;

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
                            var methodMod = ModManager.GetMod(method.DeclaringType.Assembly) ?? Utils.ThisMod;
                            string toUse = $"to be used by {nameof(RegistersRoutesAttribute)}";
                            if (!method.IsStatic)
                            {
                                methodMod.Warn($"{method.Name} must be static {toUse}");
                                continue;
                            }

                            if (method.GetParameters() is not ParameterInfo[] paramInfos
                                || paramInfos.Length < 1)
                            {
                                if (!method.ReturnType.InheritsFrom(typeof(RouteSet)))
                                {
                                    methodMod.Warn($"{method.Name} must have at least one parameter ({typeof(int).Name} or {nameof(RouteSet)}) or return {nameof(RouteSet)} {toUse}");
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
                                    methodMod.Warn($"{method.Name}'s first parameter must be either {typeof(int).Name} or {nameof(RouteSet)} {toUse}");
                                    continue;
                                }
                                if (paramInfos.Length < 2
                                    && !paramInfos[0].ParameterType.InheritsFrom(typeof(RouteSet))
                                    && !returnsRouteSet)
                                {
                                    methodMod.Warn($"{method.Name} must return a {nameof(RouteSet)} if its only parameter isn't also a {nameof(RouteSet)} {toUse}");
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
                                    methodMod.Warn($"{method.Name}'s second parameter must be either {typeof(int).Name} or {nameof(RouteSet)} {toUse}");
                                    continue;
                                }
                                if (!haveRouteSet
                                    && !returnsRouteSet)
                                {
                                    methodMod.Warn($"{method.Name} must return a {nameof(RouteSet)} if neither of its first two parameters aren't a {nameof(RouteSet)} {toUse}");
                                    continue;
                                }
                                if (param1.InheritsFrom(param2)
                                    || param2.InheritsFrom(param1))
                                {
                                    methodMod.Warn($"{method.Name}'s first two parameters cannot be the same {toUse}");
                                    continue;
                                }
                            }

                            ValidMethods.Add(method);
                        }
                    }
                    Utils.Info($"Registering Routes...");
                    for (int i = MIN_ROUTE_VERSION; i <= MAX_ROUTE_VERSION; i++)
                    {
                        var routeSet = new RouteSet(DefaultUnsupported: false, ApiVersion: i);
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
            if (Version == 1)
            {
                // Status
                RouteSet.SupportRoute(nameof(RouteSet.Status), WebMethods.GET, "status");

                // User
                RouteSet.SupportRoute(nameof(RouteSet.CanUpload), WebMethods.GET, "users/:UserID/upload-eligibility");

                RouteSet.SupportRoute(nameof(RouteSet.NewUser), WebMethods.POST, "users");
                RouteSet.SupportRoute(nameof(RouteSet.UpdateUser), WebMethods.PUT, "users/:UserID");
                RouteSet.SupportRoute(nameof(RouteSet.UpdateUserHandle), WebMethods.PUT, "users/:UserID/handle");
                RouteSet.SupportRoute(nameof(RouteSet.UpdateUserPassword), WebMethods.PUT, "users/:UserID/password");

                RouteSet.SupportRoute(nameof(RouteSet.GetSession), WebMethods.GET, "users/:UserID/session");

                RouteSet.SupportRoute(nameof(RouteSet.User), WebMethods.GET, "users/:UserID");
                RouteSet.SupportRoute(nameof(RouteSet.Users), WebMethods.GET, "users");
                RouteSet.SupportRoute(nameof(RouteSet.UserStatus), WebMethods.GET, "users/:UserID/status");
                RouteSet.SupportRoute(nameof(RouteSet.UserHandle), WebMethods.GET, "users/:UserID/handle");

                RouteSet.SupportRoute(nameof(RouteSet.UserReports), WebMethods.GET, "users/:UserID/reports");
                RouteSet.SupportRoute(nameof(RouteSet.DeleteUserReports), WebMethods.DELETE, "users/:UserID/reports");

                // Bones
                RouteSet.SupportRoute(nameof(RouteSet.NewBonesInfo), WebMethods.POST, "bones");
                RouteSet.SupportRoute(nameof(RouteSet.NewBonesSavGz), WebMethods.PUT, "bones/:BonesID/bytes-gz", "gz");

                RouteSet.SupportRoute(nameof(RouteSet.LoadBones), WebMethods.GET, "bones/:BonesID/bytes-gz", "gz");

                RouteSet.SupportRoute(nameof(RouteSet.CheckBonesID), WebMethods.GET, "bones/:BonesID/confirmation");
                RouteSet.SupportRoute(nameof(RouteSet.BonesIDs), WebMethods.GET, "bones/ids");

                RouteSet.SupportRoute(nameof(RouteSet.DeleteBones), WebMethods.DELETE, "bones/:BonesID");
                RouteSet.SupportRoute(nameof(RouteSet.WipeBones), WebMethods.DELETE, "bones");

                RouteSet.SupportRoute(nameof(RouteSet.BonesReports), WebMethods.GET, "bones/:BonesID/reports");
                RouteSet.SupportRoute(nameof(RouteSet.DeleteBonesReports), WebMethods.DELETE, "bones/:BonesID/reports");

                RouteSet.SupportRoute(nameof(RouteSet.BonesInfos), WebMethods.GET, "bones/infos");
                RouteSet.SupportRoute(nameof(RouteSet.BonesInfo), WebMethods.GET, "bones/:BonesID/info");
                RouteSet.SupportRoute(nameof(RouteSet.BonesPagedInfos), WebMethods.GET, "bones/infos/paged/:from-:to");
                RouteSet.SupportRoute(nameof(RouteSet.BonesInfosByIDs), WebMethods.POST, "bones/infos");

                RouteSet.SupportRoute(nameof(RouteSet.BonesSpecs), WebMethods.GET, "bones/specs");
                RouteSet.SupportRoute(nameof(RouteSet.BonesSpec), WebMethods.GET, "bones/:BonesID/spec");
                RouteSet.SupportRoute(nameof(RouteSet.BonesIDsMatchingSpec), WebMethods.GET, "bones/IDs/spec", Queryable: true);

                RouteSet.SupportRoute(nameof(RouteSet.BonesStatSets), WebMethods.GET, "bones/:BonesID/stat-sets");
                RouteSet.SupportRoute(nameof(RouteSet.BonesStatSet), WebMethods.GET, "bones/:BonesID/stat-sets");

                RouteSet.SupportRoute(nameof(RouteSet.UpdateBonesStats), WebMethods.PUT, "bones/:BonesID/stat-sets/:Stat");

                RouteSet.SupportRoute(nameof(RouteSet.BonesModConfigs), WebMethods.GET, "bones/mod-configs");
                RouteSet.SupportRoute(nameof(RouteSet.BonesModConfig), WebMethods.GET, "bones/:BonesID/mod-configs");
                RouteSet.SupportRoute(nameof(RouteSet.BonesModConfigsMatchingSpec), WebMethods.GET, "bones/mod-configs/spec", Queryable: true);

                // Report
                RouteSet.SupportRoute(nameof(RouteSet.NewReport), WebMethods.POST, "reports");

                RouteSet.SupportRoute(nameof(RouteSet.UpdateReport), WebMethods.PUT, "reports/:ReportID");
                RouteSet.SupportRoute(nameof(RouteSet.UpdateBlocked), WebMethods.PUT, "reports/:BonesID/:UserID/blocked");

                RouteSet.SupportRoute(nameof(RouteSet.Reported), WebMethods.GET, "reports/:BonesID/:UserID/confirmation");
                RouteSet.SupportRoute(nameof(RouteSet.Blocked), WebMethods.GET, "reports/:BonesID/:UserID/blocked");
                RouteSet.SupportRoute(nameof(RouteSet.Report), WebMethods.GET, "reports/:ReportID");

                RouteSet.SupportRoute(nameof(RouteSet.ReportByBonesAndUser), WebMethods.GET, "reports/:BonesID/:UserID");
                RouteSet.SupportRoute(nameof(RouteSet.Reports), WebMethods.GET, "reports");

                RouteSet.SupportRoute(nameof(RouteSet.DeleteReport), WebMethods.DELETE, "reports/:ReportID");
                RouteSet.SupportRoute(nameof(RouteSet.DeleteReportByBonesAndUser), WebMethods.DELETE, "reports/:BonesID/:UserID");
                RouteSet.SupportRoute(nameof(RouteSet.WipeReports), WebMethods.DELETE, "reports");
            }
            return RouteSet;
        }

        public static RouteSet GetFor(int Version)
            => RoutesByVersion.GetValue(Version)
            ?? new()
            ;
    }
}