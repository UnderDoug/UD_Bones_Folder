using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using ConsoleLib.Console;

using XRL;
using XRL.UI;
using XRL.World;
using XRL.World.Parts;

using static UD_Bones_Folder.Mod.Const;

using GameObject = XRL.World.GameObject;

namespace UD_Bones_Folder.Mod.OsseousAsh
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

        public static InventoryAction InventoryAction => new InventoryAction
        {
            Name = nameof(UD_Bones_ReportBones),
            Key = 'Z',
            Display = "report loaded bones",
            Command = REPORT_LOADED_BONES_COMMAND,
            Default = -100,
            Priority = -100,
            WorksAtDistance = true,
            AsMinEvent = true,
        };

        public static string ReportBonesTitle => "{{yellow|Report Loaded Bones}}";
        public static IRenderable ReportBonesIcon
            => new Renderable()
                .setTile("Items/sw_unfurled_scroll1.bmp")
                .setColorString("&K")
                .setTileColor("&K")
                .setDetailColor('y')
            ;

        public int ID;

        public Guid UserID;

        // [JsonConverter(typeof(StringEnumConverter))]
        // public BadWord.SeverityLevel SeverityLevelOption = Options.ModerationMinimumSeverityLevel;

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
            this.UserID = OsseousAshID;
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
