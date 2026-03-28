using System;

using Qud.API;

using XRL.World.Effects;
using XRL.World.Parts;

using Bones.Mod;

namespace XRL.World.ZoneBuilders
{
    public class BonesZoneBuilder
    {
        public string SaveBonesInfoID;

        public string ZoneID;

        private SaveBonesInfo _SaveBonesInfo;
        public SaveBonesInfo SaveBonesInfo => _SaveBonesInfo ??= BonesManager.System?.GetSavedBonesByID(SaveBonesInfoID);

        public BonesZoneBuilder()
        { }

        public bool BuildZone(Zone Z)
        {
            if (SaveBonesInfo is not SaveBonesInfo savedBones)
            {
                Utils.Error(
                    Context: $"{nameof(BonesZoneBuilder)}.{nameof(BuildZone)}", 
                    X: new InvalidOperationException(
                        $"Missing ${nameof(Bones.Mod.SaveBonesInfo)}." +
                        $"Has the Bones folder been tampered with?\n" +
                        Utils.BothBonesLocations)
                    );
                return true;
            }

            string gameID = The.Game?.GameID;
            if (savedBones.Pending != gameID)
            {
                string pending = savedBones.Pending.EqualsNoCase($"{false}") ? "none (this is an error)" : savedBones.Pending;
                Utils.Warn($"Loading mismatched {nameof(Bones.Mod.SaveBonesInfo)} for this {nameof(SaveGameInfo)}: " +
                    $"expected {pending}, got {gameID}. " +
                    $"Zone may be nonsensically placed.");
            }

            if (BonesData.GetFromSavedBonesInfo(ZoneID, savedBones) is BonesData bonesData
                && bonesData.Apply(Z, out var MoonKing) is true)
            {
                string regalTitle = MoonKingFever.REGAL_TITLE;

                if (MoonKing.TryGetEffect(out MoonKingFever moonKingFever))
                    regalTitle = moonKingFever.RegalTitle.Color("rainbow");

                Z.GetCell(0, 0)
                    ?.AddObject("Widget")
                    ?.AddPart(
                        P: new MoonKingAnnouncer()
                        {
                            Title = $"A {regalTitle} persists!",
                            Message = $"=subject.Subjective= will tolerate neither pretenders nor would-be-usurpers!"
                                .StartReplace()
                                .AddObject(MoonKing)
                                .ToString(),
                            Renderable = new(MoonKing.RenderForUI()),
                            BonesID = bonesData.BonesID,
                        }
                    );

                // bonesData.Cremate();
                return true;
            }

            return false;
        }
    }
}
