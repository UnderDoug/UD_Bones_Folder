using System;
using System.Collections.Generic;
using System.Text;

using XRL;
using XRL.Language;
using XRL.World.Effects;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

namespace UD_Bones_Folder.Mod
{
    [HasVariableReplacer]
    public static class Utils
    {
        public const string MOD_ID = "UD_Bones_Folder";

        public static ModInfo ThisMod => ModManager.GetMod(MOD_ID);

        public static string BothBonesLocations 
            => $"{DataManager.SanitizePathForDisplay(UD_Bones_BonesManager.BonesSyncPath)} -OR- " +
            $"{DataManager.SanitizePathForDisplay(UD_Bones_BonesManager.BonesSavePath)}"
            ;

        public static void Error(object Message)
            => ThisMod.Error(Message)
            ;

        public static void Error(object Context, Exception X)
            => Error($"{Context}: {X}")
            ;

        public static void Warn(object Message)
            => ThisMod.Warn(Message)
            ;

        public static void Info(object Message)
            => MetricsManager.LogModInfo(ThisMod, Message)
            ;

        public static void Log(object Message)
            => UnityEngine.Debug.Log(Message);

        [VariableObjectReplacer]
        public static string UD_RegalTitle(DelegateContext Context)
        {
            string output = UD_Bones_MoonKingFever.REGAL_TITLE;
            if (Context.Target.TryGetEffect(out UD_Bones_MoonKingFever moonKingFever))
                output = moonKingFever.RegalTitle.Color("rainbow");

            return output;
        }
    }
}
