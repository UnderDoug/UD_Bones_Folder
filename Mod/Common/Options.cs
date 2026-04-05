using XRL;

using static UD_Bones_Folder.Mod.Const;

namespace UD_Bones_Folder.Mod
{
    [HasModSensitiveStaticCache]
    [HasOptionFlagUpdate(Prefix = MOD_PREFIX)]
    public static class Options
    {
        // Debug Settings
        [OptionFlag] public static bool DebugEnableNoHoarding;
        [OptionFlag] public static bool DebugEnableNoExhuming;
        [OptionFlag] public static bool DebugEnablePickingBones;
        [OptionFlag] public static bool DebugEnableNoCremation;

        // General Settings
        [OptionFlag] public static bool EnableFlashingLightEffects;
    }
}
