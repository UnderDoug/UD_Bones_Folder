using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod
{
    public static class Const
    {
        public const string MOD_ID = "UD_Bones_Folder";
        public const string MOD_PREFIX = MOD_ID + "_";

        public const int MIN_SAVE_VERSION = 400;

        public const int SERIALIZATION_CHECK = 123457;
        public const int BONES_SPEC_POS = 111111;
        public const int BONES_ZONE_POS = 222222;
        public const int BONES_FINALIZE_POS = 999999;

        public const string OSSEOUS_ASH_ADDRESS = "osseousash.cloud";
        public const string OSSEOUS_ASH_UN = "bones@osseousash.cloud";
        public const string OSSEOUS_ASH_PW = "changed";

        public const string OSSEOUS_ASH = "{{black|Osseous Ash}}";
        public const string OSSEOUS_ASH_CHECK = "Wot u doin' peeping in here?";

        public const string BONES_WORLD = "BonesWorld";

        public const string CONTENT_TYPE_JSON = "application/json";

        public const string MAD_LUNAR_REGENT_TILE = "Creatures/mad_lunar_regent_human.png";
        public const string MOON_KING_FEVER_TILE = "Effects/lunar_regent_fever.png";

        public const string ANNOUNCER_WIDGET = "UD_Bones_Folder Lunar Announcer";

        public const string EQ_FRAME_COLORS = "EquipmentFrameColors";

        public const string IS_MAD_PROP = MOD_PREFIX + "IsMad";
        public const string LOADED_BONES_PROP = MOD_PREFIX + "Loaded_BonesID";

        public const string REPORT_LOADED_BONES_COMMAND = "Cmd_" + MOD_PREFIX + "ReportLoadedBonesID";

        public const string LUNAR_RELIQUARY_BLUEPRINT = "Lunar Reliquary";
    }
}
