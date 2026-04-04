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

            if (bonesData.Apply(Z, out var MoonKing, out bool IsMad) is true)
            {
                string regalTitle = UD_Bones_MoonKingFever.REGAL_TITLE;

                if (MoonKing.TryGetEffect(out UD_Bones_MoonKingFever moonKingFever))
                    regalTitle = moonKingFever.RegalTitle.Colored("rainbow");

                string announcement = $"=subject.Subjective= will tolerate neither pretenders nor would-be-usurpers!";
                string madAnnouncement = $"=subject.Subjective==subject.verb:'ve:afterpronoun= =subject.verb:come:afterpronoun= " +
                    $"from a world vastly different from this one, and will tolerate neither pretenders nor would-be-usurpers!";

                Z.GetCell(0, 0)
                    ?.AddObject("Widget")
                    ?.AddPart(
                        P: new UD_Bones_MoonKingAnnouncer(
                            BonesID: bonesData.BonesID,
                            Title: $"A {(IsMad ? "mad " : null)}{regalTitle} persists!",
                            Message: (!IsMad ? announcement : madAnnouncement)
                                .StartReplace()
                                .AddObject(MoonKing)
                                .ToString())
                        );
                return true;
            }
            return false;
        }
    }
}
