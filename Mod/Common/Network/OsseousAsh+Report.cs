using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using Platform.IO;

using UnityEngine;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.Const;

using GameObject = XRL.World.GameObject;

namespace UD_Bones_Folder.Mod
{
    public static partial class OsseousAsh
    {
        [JsonObject(MemberSerialization.OptOut)]
        [Serializable]
        public class Report : IDisposable
        {
            [JsonObject(MemberSerialization.OptOut)]
            [Serializable]
            public class ObjectReportDetails
            {
                public string Blueprint;
                public string DisplayName;
                public int SerializedBaseID;
                public bool IsTheLunarRegent;
                public bool IsLunarRegent;

                public ObjectReportDetails()
                { }

                public ObjectReportDetails(
                    string Blueprint,
                    string DisplayName,
                    int SerializedBaseID,
                    bool IsTheLunarRegent,
                    bool IsLunarRegent
                ) : this()
                {
                    this.Blueprint = Blueprint;
                    this.DisplayName = DisplayName;
                    this.SerializedBaseID = SerializedBaseID;
                    this.IsTheLunarRegent = IsTheLunarRegent;
                    this.IsLunarRegent = IsLunarRegent;
                }

                public static ObjectReportDetails FromGameObject(GameObject ReportedObject)
                {
                    if (!ReportedObject.TryGetPart(out UD_Bones_ReportBones reportPart))
                        return null;

                    bool isTheLunarRegent = false;
                    bool isLunarRegent = false;
                    if (ReportedObject.TryGetPart(out UD_Bones_LunarRegent lunarRegentPart))
                    {
                        isLunarRegent = true;
                        isTheLunarRegent = lunarRegentPart.BonesID == reportPart.LoadedBonesID;
                    }
                    return new ObjectReportDetails
                    {
                        Blueprint = ReportedObject.Blueprint,
                        DisplayName = ReportedObject.GetReferenceDisplayName(),
                        SerializedBaseID = reportPart.SerializedBaseID,
                        IsTheLunarRegent = isTheLunarRegent,
                        IsLunarRegent = isLunarRegent,
                    };
                }
            }

            public enum ReportTypes
            {
                None,
                Offensive,
                Griefing,
                Broken,
                Other,
            }

            public enum ReportActionTypes
            {
                None,
                Waiting,
                NonIssue,
                Deleted,
                Other,
            }

            public int ID;

            public Guid OsseousAshID;

            public string BonesID;

            public bool Blocked;

            [JsonConverter(typeof(StringEnumConverter))]
            public ReportTypes Type;

            public ObjectReportDetails ObjectDetails;

            public string Description;

            [JsonConverter(typeof(StringEnumConverter))]
            public ReportActionTypes Actioned;

            [JsonIgnore]
            public bool IsSpecificObject => ObjectDetails != null;

            [JsonIgnore]
            public bool IsValid
                => !BonesID.IsNullOrEmpty()
                && Type > ReportTypes.None
                && Description != null
                ;

            public Report()
            {
                Blocked = true;
            }

            public Report(
                Guid OsseousAshID,
                string BonesID,
                ReportTypes Type,
                ObjectReportDetails ObjectDetails,
                string Description
            ) : this()
            {
                this.OsseousAshID = OsseousAshID;
                this.BonesID = BonesID;
                this.Type = Type;

                this.ObjectDetails = ObjectDetails;

                this.Description = Description;
            }

            public void Dispose()
            {
                ID = 0;
                BonesID = null;
                Blocked = true;
                Type = ReportTypes.None;
                ObjectDetails = null;
                Description = null;
                Actioned = ReportActionTypes.None;
            }
        }
    }
}
