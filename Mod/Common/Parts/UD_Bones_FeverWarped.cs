using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Rules;
using XRL.Core;
using XRL.Language;
using XRL.Collections;
using System.Linq;
using UD_Bones_Folder.Mod.Events;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_FeverWarped : UD_Bones_BaseLunarPart
    {
        protected string TileColor;
        protected string DetailColor;

        protected bool TileOnly;

        protected string OriginalShortDesc;

        private string DisplayNameCache;
        private string AdjectiveCache;

        public UD_Bones_FeverWarped()
        {
        }

        public UD_Bones_FeverWarped(bool TileOnly)
            : this()
        {
            this.TileOnly = TileOnly;
        }

        public override void Attach()
        {
            base.Attach();

            ParentObject.SetStringProperty(nameof(UD_Bones_FeverWarped), null, true);
            ParentObject.SetStringProperty($"{nameof(UD_Bones_FeverWarped)}::TileOnly", null, true);
            ParentObject.SetStringProperty($"{nameof(UD_Bones_FeverWarped)}::OriginalBlueprint", ParentObject.Blueprint, true);
            ParentObject.SetStringProperty("UD_Bones_Folder_IsMad", $"{true}");

            ParentObject.RequirePart<UD_Bones_LunarColors>();

            if (ParentObject.IsEquipment())
            {
                if (ParentObject.RequirePart<Cursed>() is Cursed cursed)
                {
                    // this was originally FAFO, but was maybe a little harsh.
                    cursed.RevealInDescription = true;
                }
            }

            if (ParentObject.TryGetPart(out Description description))
            {
                OriginalShortDesc = description._Short;

                string bakedDescription = OriginalShortDesc
                    .StartReplace()
                    .AddObject(ParentObject)
                    .ToString();

                description._Short = FeverWarpText(bakedDescription);
            }

            var render = ParentObject.RequirePart<Render>();
            
            BonesManager.System.RequireAlternativeTileAndBlueprintForGameObject(
                BlueprintSpec: new Utils.BlueprintSpec(ParentObject),
                Blueprint: out string altBlueprint,
                Tile: out string altTile);

            if (!TileOnly)
                ParentObject.Blueprint = altBlueprint;

            render.Tile = altTile;

            string flipSeed = ParentObject.GetStringProperty($"{nameof(UD_Bones_FeverWarped)}::OriginalBlueprint");

            if (Stat.SeededRandom($"{flipSeed}:{nameof(render.HFlip)}", 0, 5) == 0)
                render.HFlip = true;

            if (Stat.SeededRandom($"{flipSeed}:{nameof(render.VFlip)}", 0, 25) == 0)
                render.VFlip = true;
        }

        public bool IsTileOnly()
            => TileOnly
            ;

        public string GetDescription()
        {
            var sB = Event.NewStringBuilder();
            sB.Append(GetAdjective().Capitalize()).Append(": ")
                .Append(ParentObject.ThisTheseDescriptiveCategory()).Append(" has been warped by the process of arriving in this world.");

            if (ParentObject.IsEquipment())
            sB.AppendLine()
                .AppendColored("R", $"{GetWillpowerMalus(ParentObject?.GetTier() ?? 1).Signed()} Willpower");

            return Event.FinalizeString(sB);
        }

        public string GetAdjective()
        {
            AdjectiveCache ??= $"fever warped"
                .StartReplace()
                .ToString();

            return AdjectiveCache;
        }

        public static int GetWillpowerMalus(int Tier)
            => -Math.Clamp(Math.Clamp(Tier, 1, 8) / 2, 1, 4)
            ;

        public static string FeverWarpText(string Description, bool DoReplace = true)
        {
            Description = Description.Strip();

            using var corruptions = ScopeDisposedList<string>.GetFromPool();
            while (corruptions.IsNullOrEmpty() || corruptions.Aggregate(0, (a,n) => a + n.Length) < Description.Length)
                corruptions.Add(TextFilters.GenerateCrypticWord());

            int corruptionsLength = corruptions.Aggregate(0, (a, n) => a + n.Length);
            int startAt = 0;
            int diff = corruptionsLength - Description.Length;
            if (corruptions.Aggregate(0, (a, n) => a + n.Length) > Description.Length)
                startAt = Stat.RandomCosmetic(0, diff);
            int originalStart = startAt;
            int moduloOffset = Stat.RandomCosmetic(0, 6999);
            string output = Description;

            for (int i = 0; i < corruptions.Count; i++)
            {
                if (corruptions[i] is string corruption)
                {
                    if (startAt >= output.Length)
                        break;

                    if ((i + moduloOffset) % 2 == 0)
                    {
                        startAt += corruption.Length;
                        continue;
                    }
                    int corruptionEnd = Math.Min(startAt + corruption.Length, output.Length - 1);
                    string beforeCorruption = output[..startAt];
                    string afterCorruption = output[corruptionEnd..];
                    output = $"{beforeCorruption}{corruption}{afterCorruption}";
                    startAt = corruptionEnd;
                }
            }

            startAt = Stat.RandomCosmetic(0, diff / 2);

            if (startAt == originalStart)
                startAt += 1;

            for (int i = 0; i < corruptions.Count; i++)
            {
                if (corruptions[i] is string corruption)
                {
                    if (startAt >= output.Length)
                        break;

                    if ((i + moduloOffset) % 2 == 0)
                    {
                        startAt += corruption.Length;
                        continue;
                    }
                    int corruptionEnd = Math.Min(startAt + corruption.Length, output.Length - 1);
                    string beforeCorruption = output[..startAt];
                    string afterCorruption = output[corruptionEnd..];
                    string lunarCorruption = $"=LunarShader:{output[beforeCorruption.Length..^afterCorruption.Length]}=";
                    output = $"{beforeCorruption}{lunarCorruption}{afterCorruption}";
                    startAt += lunarCorruption.Length;
                }
            }

            if (DoReplace)
                output = output
                    .StartReplace()
                    .ToString();

            return output;
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(GetShortDescriptionEvent.ID, EventOrder.VERY_LATE, Serialize: true);
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDisplayNameEvent.ID
            || ID == EquippedEvent.ID
            || ID == UnequippedEvent.ID
            || ID == ImplantedEvent.ID
            || ID == UnimplantedEvent.ID
            || ID == LunarObjectColorChangedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (ParentObject.TryGetPart(out Description description))
            {
                string bakedDescription = OriginalShortDesc
                    .StartReplace()
                    .AddObject(ParentObject)
                    .ToString();

                description._Short = FeverWarpText(bakedDescription);
            }

            E.Postfix.AppendRules(GetDescription());
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            E.AddAdjective(GetAdjective());

            DisplayNameCache ??= FeverWarpText(E.GetPrimaryBase());
            E.ReplacePrimaryBase(DisplayNameCache);

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EquippedEvent E)
        {
            if (E.Item == ParentObject
                && ParentObject.IsEquippedProperly())
                StatShifter.SetStatShift(E.Actor, "Willpower", GetWillpowerMalus(ParentObject.GetTier()));

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(UnequippedEvent E)
        {
            if (E.Item == ParentObject)
                StatShifter.RemoveStatShifts(E.Actor);

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(ImplantedEvent E)
        {
            if (E.Item == ParentObject
                && ParentObject.IsEquippedProperly())
                StatShifter.SetStatShift(E.Implantee, "Willpower", GetWillpowerMalus(ParentObject.GetTier()));

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(UnimplantedEvent E)
        {
            if (E.Item == ParentObject)
                StatShifter.RemoveStatShifts(E.Implantee);

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(LunarObjectColorChangedEvent E)
        {
            DisplayNameCache = null;
            AdjectiveCache = null;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, "Adjective", GetAdjective());
            E.AddEntry(this, "Description", GetDescription());
            return base.HandleEvent(E);
        }
    }
}
