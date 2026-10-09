using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

using Newtonsoft.Json;

using UD_Bones_Folder.Mod.Serialization;

using XRL.World;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public class RouteSet
    {
        public static Route Unsupported => Route.NotSupported();

        public int Version;

        #region Status

        public Route Status;

        #endregion
        #region User

        public Route CanUpload;

        public Route NewUser;
        public Route UpdateUser;

        public Route Login;

        public Route User;
        public Route Users;
        public Route UserStatus;
        public Route UserHandle;

        #endregion
        #region Bones

        public Route NewBonesInfo;
        public Route NewBonesSavGz;

        public Route BonesInfo;
        public Route BonesInfos;
        public Route BonesPagedInfos;

        public Route BonesSpec;
        public Route BonesSpecs;

        public Route UpdateBonesStats;

        public Route LoadBones;
        public Route DownloadBones;

        public Route CheckBonesID;
        public Route BonesIDs;

        public Route DeleteBones;
        public Route WipeBones;

        #endregion
        #region Report

        public Route NewReport;

        public Route UpdateReport;

        public Route Reported;
        public Route Report;
        public Route Reports;

        public Route ReportByBonesAndUser;
        public Route ReportByBones;
        public Route ReportByUser;

        public Route DeleteReport;
        public Route DeleteReportByBonesAndUser;
        public Route DeleteReportByUser;
        public Route DeleteReportByBones;
        public Route WipeReports;

        #endregion

        public RouteSet()
            : this(DefaultUnsupported: true)
        { }

        public RouteSet(bool DefaultUnsupported)
            : base()
        {
            if (DefaultUnsupported)
            {
                Status = Unsupported;

                CanUpload = Unsupported;

                NewUser = Unsupported;
                UpdateUser = Unsupported;

                Login = Unsupported;

                User = Unsupported;
                Users = Unsupported;
                UserStatus = Unsupported;
                UserHandle = Unsupported;

                NewBonesInfo = Unsupported;
                NewBonesSavGz = Unsupported;

                BonesInfo = Unsupported;
                BonesInfos = Unsupported;
                BonesPagedInfos = Unsupported;

                BonesSpec = Unsupported;
                BonesSpecs = Unsupported;

                UpdateBonesStats = Unsupported;

                LoadBones = Unsupported;
                DownloadBones = Unsupported;

                CheckBonesID = Unsupported;
                BonesIDs = Unsupported;

                DeleteBones = Unsupported;
                WipeBones = Unsupported;

                NewReport = Unsupported;

                UpdateReport = Unsupported;

                Reported = Unsupported;
                Report = Unsupported;
                Reports = Unsupported;

                ReportByBonesAndUser = Unsupported;
                ReportByBones = Unsupported;
                ReportByUser = Unsupported;

                DeleteReport = Unsupported;
                DeleteReportByBonesAndUser = Unsupported;
                DeleteReportByUser = Unsupported;
                DeleteReportByBones = Unsupported;
                WipeReports = Unsupported;
            }
        }

        public RouteSet(bool DefaultUnsupported, int Version)
            : this(DefaultUnsupported)
        {
            this.Version = Version;
        }

        public RouteSet(int Version)
            : this()
        {
            this.Version = Version;
        }

        public RouteSet(RouteSet Source)
            : this()
        {
            CopyFrom(Source);
        }

        public RouteSet Clone()
            => new(this)
            ;

        public void CopyFrom(RouteSet Source)
        {
            Version = Source.Version;

            foreach (var field in typeof(RouteSet).GetFields())
            {
                try
                {
                    if (field.FieldType.InheritsFrom(typeof(Route))
                        && field.GetValue(Source) is Route fieldValue
                        && fieldValue.Clone() is Route newRoute)
                    {
                        field.SetValue(this, newRoute);
                    }
                }
                catch (Exception x)
                {
                    Utils.Warn($"{nameof(RouteSet)}.{nameof(CopyFrom)} failed to {nameof(FieldInfo.SetValue)} for field {field?.Name}", x);
                }
            }
        }

        public void FillInUnsupported()
        {
            foreach (var field in typeof(RouteSet).GetFields())
            {
                try
                {
                    if (field.FieldType.InheritsFrom(typeof(Route))
                        && field.GetValue(this) == null)
                    {
                        field.SetValue(this, Unsupported);
                    }
                }
                catch (Exception x)
                {
                    Utils.Warn($"{nameof(RouteSet)}.{nameof(FillInUnsupported)} failed to {nameof(FieldInfo.SetValue)} for field {field?.Name}", x);
                }
            }
        }

        public void LogRoutes(int Indent = 0)
        {
            Utils.Log($"{Indent.Indent()}Version {Version} Routes:");
            foreach (var field in typeof(RouteSet).GetFields())
            {
                try
                {
                    if (field.FieldType.InheritsFrom(typeof(Route)))
                        Utils.Log($"{(Indent + 1).Indent()}{field.Name}: {field.GetValue(this)?.ToString() ?? "null"}");
                }
                catch (Exception x)
                {
                    Utils.Warn($"{nameof(RouteSet)}.{nameof(FillInUnsupported)} failed to {nameof(FieldInfo.GetValue)} for field {field?.Name}", x);
                }
            }
        }
    }
}
