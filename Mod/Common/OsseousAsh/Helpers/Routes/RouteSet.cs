using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

using UD_Bones_Folder.Mod.Serialization;

using XRL.Collections;
using XRL.World;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public class RouteSet : StringMap<Route>
    {
        public static Route Unsupported => Route.NotSupported();

        public int ApiVersion;

        #region Status

        public Route Status
        {
            get => GetValue(nameof(Status));
            set => SetValue(nameof(Status), value);
        }

        #endregion
        #region User

        public Route CanUpload
        {
            get => GetValue(nameof(CanUpload));
            set => SetValue(nameof(CanUpload), value);
        }

        public Route NewUser
        {
            get => GetValue(nameof(NewUser));
            set => SetValue(nameof(NewUser), value);
        }
        public Route UpdateUser
        {
            get => GetValue(nameof(UpdateUser));
            set => SetValue(nameof(UpdateUser), value);
        }
        public Route UpdateUserHandle
        {
            get => GetValue(nameof(UpdateUserHandle));
            set => SetValue(nameof(UpdateUserHandle), value);
        }
        public Route UpdateUserPassword
        {
            get => GetValue(nameof(UpdateUserPassword));
            set => SetValue(nameof(UpdateUserPassword), value);
        }

        public Route GetSession
        {
            get => GetValue(nameof(GetSession));
            set => SetValue(nameof(GetSession), value);
        }

        public Route User
        {
            get => GetValue(nameof(User));
            set => SetValue(nameof(User), value);
        }
        public Route Users
        {
            get => GetValue(nameof(Users));
            set => SetValue(nameof(Users), value);
        }
        public Route UserStatus
        {
            get => GetValue(nameof(UserStatus));
            set => SetValue(nameof(UserStatus), value);
        }
        public Route UserHandle
        {
            get => GetValue(nameof(UserHandle));
            set => SetValue(nameof(UserHandle), value);
        }

        public Route UserReports
        {
            get => GetValue(nameof(UserReports));
            set => SetValue(nameof(UserReports), value);
        }
        public Route DeleteUserReports
        {
            get => GetValue(nameof(DeleteUserReports));
            set => SetValue(nameof(DeleteUserReports), value);
        }

        #endregion
        #region Bones

        public Route NewBonesInfo
        {
            get => GetValue(nameof(NewBonesInfo));
            set => SetValue(nameof(NewBonesInfo), value);
        }
        public Route NewBonesSavGz
        {
            get => GetValue(nameof(NewBonesSavGz));
            set => SetValue(nameof(NewBonesSavGz), value);
        }

        public Route LoadBones
        {
            get => GetValue(nameof(LoadBones));
            set => SetValue(nameof(LoadBones), value);
        }

        public Route CheckBonesID
        {
            get => GetValue(nameof(CheckBonesID));
            set => SetValue(nameof(CheckBonesID), value);
        }
        public Route BonesIDs
        {
            get => GetValue(nameof(BonesIDs));
            set => SetValue(nameof(BonesIDs), value);
        }

        public Route DeleteBones
        {
            get => GetValue(nameof(DeleteBones));
            set => SetValue(nameof(DeleteBones), value);
        }
        public Route WipeBones
        {
            get => GetValue(nameof(WipeBones));
            set => SetValue(nameof(WipeBones), value);
        }

        public Route BonesInfos
        {
            get => GetValue(nameof(BonesInfos));
            set => SetValue(nameof(BonesInfos), value);
        }
        public Route BonesInfo
        {
            get => GetValue(nameof(BonesInfo));
            set => SetValue(nameof(BonesInfo), value);
        }
        /// <summary>
        /// Include an array of bones IDs in the body of the request
        /// </summary>
        public Route BonesInfosByIDs
        {
            get => GetValue(nameof(BonesInfosByIDs));
            set => SetValue(nameof(BonesInfosByIDs), value);
        }
        public Route BonesPagedInfos
        {
            get => GetValue(nameof(BonesPagedInfos));
            set => SetValue(nameof(BonesPagedInfos), value);
        }

        public Route BonesSpecs
        {
            get => GetValue(nameof(BonesSpecs));
            set => SetValue(nameof(BonesSpecs), value);
        }
        public Route BonesSpec
        {
            get => GetValue(nameof(BonesSpec));
            set => SetValue(nameof(BonesSpec), value);
        }
        public Route BonesIDsMatchingSpec
        {
            get => GetValue(nameof(BonesIDsMatchingSpec));
            set => SetValue(nameof(BonesIDsMatchingSpec), value);
        }

        public Route BonesStatSets
        {
            get => GetValue(nameof(BonesStatSets));
            set => SetValue(nameof(BonesStatSets), value);
        }
        public Route BonesStatSet
        {
            get => GetValue(nameof(BonesStatSet));
            set => SetValue(nameof(BonesStatSet), value);
        }

        public Route UpdateBonesStats
        {
            get => GetValue(nameof(UpdateBonesStats));
            set => SetValue(nameof(UpdateBonesStats), value);
        }

        public Route BonesModConfigs
        {
            get => GetValue(nameof(BonesModConfigs));
            set => SetValue(nameof(BonesModConfigs), value);
        }
        public Route BonesModConfig
        {
            get => GetValue(nameof(BonesModConfig));
            set => SetValue(nameof(BonesModConfig), value);
        }
        public Route BonesModConfigsMatchingSpec
        {
            get => GetValue(nameof(BonesModConfigsMatchingSpec));
            set => SetValue(nameof(BonesModConfigsMatchingSpec), value);
        }

        #endregion
        #region Report

        public Route NewReport
        {
            get => GetValue(nameof(NewReport));
            set => SetValue(nameof(NewReport), value);
        }

        public Route UpdateReport
        {
            get => GetValue(nameof(UpdateReport));
            set => SetValue(nameof(UpdateReport), value);
        }
        public Route UpdateBlocked
        {
            get => GetValue(nameof(UpdateBlocked));
            set => SetValue(nameof(UpdateBlocked), value);
        }

        public Route Reported
        {
            get => GetValue(nameof(Reported));
            set => SetValue(nameof(Reported), value);
        }
        public Route Blocked
        {
            get => GetValue(nameof(Blocked));
            set => SetValue(nameof(Blocked), value);
        }
        public Route Report
        {
            get => GetValue(nameof(Report));
            set => SetValue(nameof(Report), value);
        }
        public Route Reports
        {
            get => GetValue(nameof(Reports));
            set => SetValue(nameof(Reports), value);
        }

        public Route ReportByBonesAndUser
        {
            get => GetValue(nameof(ReportByBonesAndUser));
            set => SetValue(nameof(ReportByBonesAndUser), value);
        }
        public Route BonesReports
        {
            get => GetValue(nameof(BonesReports));
            set => SetValue(nameof(BonesReports), value);
        }

        public Route DeleteReport
        {
            get => GetValue(nameof(DeleteReport));
            set => SetValue(nameof(DeleteReport), value);
        }
        public Route DeleteReportByBonesAndUser
        {
            get => GetValue(nameof(DeleteReportByBonesAndUser));
            set => SetValue(nameof(DeleteReportByBonesAndUser), value);
        }
        public Route DeleteBonesReports
        {
            get => GetValue(nameof(DeleteBonesReports));
            set => SetValue(nameof(DeleteBonesReports), value);
        }
        public Route WipeReports
        {
            get => GetValue(nameof(WipeReports));
            set => SetValue(nameof(WipeReports), value);
        }

        #endregion

        public RouteSet()
            : this(DefaultUnsupported: true)
        { }

        public RouteSet(bool DefaultUnsupported)
            : base()
        {
            if (DefaultUnsupported)
                FillInUnsupported();
        }

        public RouteSet(bool DefaultUnsupported, int ApiVersion)
            : this(DefaultUnsupported)
        {
            this.ApiVersion = ApiVersion;
        }

        public RouteSet(int ApiVersion)
            : this()
        {
            this.ApiVersion = ApiVersion;
        }

        public RouteSet(RouteSet Source)
            : this()
        {
            CopyFrom(Source);
        }

        public virtual Route SetValue([AllowNull] string Key, Route Value)
        {
            if (Value == null)
                Remove(Key);
            else
                this[Key] = Value;

            return GetValue(Key);
        }

        public RouteSet Clone()
            => new(this)
            ;

        public void CopyFrom(RouteSet Source)
        {
            Clear();

            ApiVersion = Source.ApiVersion;

            foreach ((var name, var route) in this)
                this[name] = route;
        }

        public void FillInUnsupported()
        {
            using var filled = ScopeDisposedList<string>.GetFromPool();
            foreach (var property in typeof(RouteSet).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                try
                {
                    if (property.PropertyType.InheritsFrom(typeof(Route))
                        && property.DeclaringType == GetType()
                        && property.GetValue(this) == null)
                    {
                        property.SetValue(this, Unsupported);
                        filled.Add(property.Name);
                    }
                }
                catch (Exception x)
                {
                    Utils.Warn($"{nameof(RouteSet)}.{nameof(FillInUnsupported)} failed to {nameof(PropertyInfo.SetValue)} for property {property?.Name}", x);
                }
            }

            using var toFill = ScopeDisposedList<string>.GetFromPool();
            foreach ((var name, var route) in this)
            {
                if (filled.Contains(name))
                    continue;

                if (route == null)
                    toFill.Add(name);

                filled.Add(name);
            }

            foreach (var name in toFill)
                this[name] = Unsupported;
        }

        public Route SupportRoute(string Route, Host.WebMethods Method, string Value, string ContentType = "json", bool Queryable = false)
        {
            if (!ContainsKey(Route))
                this[Route] = new Route(Method, Value, ContentType, Queryable);

            var route = this[Route];

            if (!route.IsUnsupported)
                return route;

            route.Value = Value;

            if (OsseousAsh.Route.ContentTypes.ContainsKey(ContentType))
                route.ContentType = OsseousAsh.Route.ContentTypes[ContentType];
            else
                route.ContentType = ContentType;

            route.Method = Method;

            route.Queryable = Queryable;

            return route;
        }

        public void LogRoutes(int Indent = 0)
        {
            using var logged = ScopeDisposedList<string>.GetFromPool();
            Utils.Log($"{Indent.Indent()}Version {ApiVersion} Routes:");
            foreach (var property in typeof(RouteSet).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                try
                {
                    if (property.PropertyType.InheritsFrom(typeof(Route))
                        && property.DeclaringType == GetType())
                    {
                        Utils.Log($"{(Indent + 1).Indent()}{property.Name}: {property.GetValue(this)?.ToString() ?? "null"}");
                        logged.Add(property.Name);
                    }
                }
                catch (Exception x)
                {
                    Utils.Warn($"{nameof(RouteSet)}.{nameof(LogRoutes)} failed to {nameof(PropertyInfo.GetValue)} for property {property?.Name}", x);
                }
            }

            foreach ((var name, var route) in this)
            {
                if (logged.Contains(name))
                    continue;

                Utils.Log($"{(Indent + 1).Indent()}{name}: {route?.ToString() ?? "null"}");
                logged.Add(name);
            }
        }

        public override void Clear()
        {
            base.Clear();
            ApiVersion = 0;
        }
    }
}
