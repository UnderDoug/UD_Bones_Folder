using System;

using Qud.API;

using XRL.World.Effects;
using XRL.World.Parts;

using UD_Bones_Folder.Mod;
using static UD_Bones_Folder.Mod.BonesManager;
using XRL.UI;
using Qud.UI;
using Platform.IO;
using Kobold;

namespace XRL.World.ZoneBuilders
{
    public class BonesZoneBuilder
    {
        protected bool Alerted;

        public string SaveBonesInfoID;

        public string ZoneID;

        private SaveBonesInfo _SaveBonesInfo;
        public SaveBonesInfo SaveBonesInfo => _SaveBonesInfo ??= BonesManager.System?.GetSavedBonesByID(SaveBonesInfoID);

        public BonesZoneBuilder()
        { }

        public bool BuildZone(Zone Z)
        {
            if (SaveBonesInfo is not SaveBonesInfo saveBonesInfo)
            {
                Utils.Error(
                    Context: $"{nameof(BonesZoneBuilder)}.{nameof(BuildZone)}", 
                    X: new InvalidOperationException(
                        $"Missing ${nameof(UD_Bones_Folder.Mod.SaveBonesInfo)}." +
                        $"Has the Bones folder been tampered with?\n" +
                        Utils.BothBonesLocations)
                    );
                return true;
            }

            if (saveBonesInfo.WasCremated)
            {
                Utils.Warn($"{nameof(SaveBonesInfo)} was cremated. Aborting Bones Zone build and allowing zone to build normally.");
                return true;
            }

            string gameID = The.Game?.GameID;
            if (saveBonesInfo.Pending != gameID)
            {
                string pending = saveBonesInfo.Pending.EqualsNoCase($"{false}") ? "none (this is an error)" : saveBonesInfo.Pending;
                Utils.Warn($"Loading mismatched {nameof(UD_Bones_Folder.Mod.SaveBonesInfo)} for this {nameof(SaveGameInfo)}: " +
                    $"expected {pending}, got {gameID}. " +
                    $"Zone may be nonsensically placed.");
            }

            if (saveBonesInfo.Encountered > 0)
            {
                Utils.Warn($"Loading bones previously encountered {saveBonesInfo.Encountered.Things("time")}. " +
                    $"Bones may have been loaded into a rebuilt Zone, or game may have crashed without saving.");
            }

            BonesData bonesData = null;
            try
            {
                bonesData = BonesData.GetFromSaveBonesInfo(ZoneID, saveBonesInfo);
            }
            catch (FatalDeserializationVersionException fX)
            {
                if (!Alerted)
                {
                    Popup.ShowYesNo(
                        Message: $"This bones file has encountered a fatal deserialization exception on the basis of the save version and will likely never load.\n\n" +
                        $"If you would like to try and recover it, select {PopupMessage.YesNoButton[1].text}, force the zone to load when asked, and then please contact {Utils.AuthorOnPlatforms} with a copy of the bones file: {DataManager.SanitizePathForDisplay(saveBonesInfo.Directory)}.\n\n" +
                        $"Alternatively, it is recommended that you cremate this bones file, so that it doesn't continue to create issues.\n\n" +
                        $"Would you like to cremate this bones file?",
                        callback: (result) =>
                        {
                            if (result == DialogResult.Yes)
                                saveBonesInfo.Cremate();
                        });
                    Alerted = true;
                }
                throw fX;
            }
            catch (DeserializationVersionException x)
            {
                if (!Alerted)
                {
                    Popup.ShowYesNo(
                        Message: $"This bones file has encountered a deserialization version exception on the basis of the save version and will likely not load this run.\n\n" +
                        $"This bones is for game version {saveBonesInfo.Version}, and will likely load in an appropriately versioned run. If you would like to keep this bones so that it might appear in run on that version of the game, select {PopupMessage.YesNoButton[1].text}, and then force the zone to load when asked.\n\n" +
                        $"Alternatively, if you're unlikely to switch versions of the game, it is recommended that you cremate this bones file, so that it doesn't continue to create issues.\n\n" +
                        $"Would you like to cremate this bones file?",
                        callback: (result) =>
                        {
                            if (result == DialogResult.Yes)
                                saveBonesInfo.Cremate();
                        });
                    Alerted = true;
                }
                throw x;
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(BonesZoneBuilder), nameof(BuildZone)), x);
                bonesData = null;
            }

            if (bonesData == null)
                return false;

            if (Z.GetZoneProperty(nameof(BonesData.BonesID), null) is string existingBonesID)
            {
                if (existingBonesID != bonesData.BonesID)
                    Utils.Warn($"Loading {nameof(UD_Bones_Folder.Mod.SaveBonesInfo)} for zone that has already loaded a different bones: " +
                        $"{nameof(existingBonesID)} {existingBonesID}, {nameof(bonesData)}.{nameof(bonesData.BonesID)} {bonesData.BonesID}. " +
                        $"Zone may have errors.");
                else
                    Utils.Warn($"{nameof(UD_Bones_Folder.Mod.SaveBonesInfo)} for zone that has already loaded this bones: " +
                        $"{nameof(existingBonesID)} {existingBonesID}, {nameof(bonesData)}.{nameof(bonesData.BonesID)} {bonesData.BonesID}. " +
                        $"Zone may have errors.");
            }

            if (bonesData.Apply(Z, out var MoonKing, saveBonesInfo.IsMad) is true)
            {
                Z.GetCell(0, 0).AddObject(Const.ANNOUNCER_WIDGET, Context: $"{nameof(UD_Bones_MoonKingAnnouncer.BonesID)}::{bonesData.BonesID}");

                Z.SetZoneProperty(nameof(bonesData.BonesID), bonesData.BonesID);
                try
                {
                    SaveBonesInfo.IncrementEncountered(saveBonesInfo);
                }
                catch (Exception x)
                {
                    Utils.Error($"Failed to increment {nameof(SaveBonesInfo)}.{nameof(SaveBonesInfo.Encountered)}", x);
                }
                return true;
            }
            return false;
        }
    }
}
