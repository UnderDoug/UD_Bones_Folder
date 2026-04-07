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

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_FeverWarped : UD_Bones_BaseLunarPart
    {
        protected string TileColor;
        protected string DetailColor;

        protected bool TileOnly;

        protected string OriginalShortDesc;

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

            if (ParentObject.RequirePart<Cursed>() is Cursed cursed)
            {
                cursed.RevealInDescription = false;
            }

            if (ParentObject.TryGetPart(out Description description))
            {
                OriginalShortDesc = description._Short;
                description._Short = ProcessDescription(
                    Description: OriginalShortDesc
                        .StartReplace()
                        .AddObject(ParentObject)
                        .ToString())
                    .StartReplace()
                    .ToString();
            }

            var render = ParentObject.RequirePart<Render>();
            
            BonesManager.System.RequireAlternativeTileAndBlueprintForGameObject(
                BlueprintSpec: new Utils.BlueprintSpec(ParentObject),
                Blueprint: out string altBlueprint,
                Tile: out string altTile);

            if (!TileOnly)
                ParentObject.Blueprint = altBlueprint;

            render.Tile = altTile;

            if (Stat.RandomCosmetic(0, 5) == 0)
                render.HFlip = true;

            if (Stat.RandomCosmetic(0, 25) == 0)
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
            return Event.FinalizeString(sB);
        }

        public string GetAdjective()
            => "=LunarShader:fever warped="
                .StartReplace()
                .ToString()
            ;

        public static string ProcessDescription(string Description)
        {
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
            || ID == GetShortDescriptionEvent.ID
            || ID == EquippedEvent.ID
            || ID == UnequippedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (ParentObject.TryGetPart(out Description description))
                description._Short = ProcessDescription(
                        Description: OriginalShortDesc
                            .StartReplace()
                            .AddObject(ParentObject)
                            .ToString())
                        .StartReplace()
                        .ToString();

            E.Postfix.AppendRules(GetDescription());
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EquippedEvent E)
        {
            if (E.Item == ParentObject
                && ParentObject.IsEquippedProperly())
                StatShifter.SetStatShift("Willpower", Math.Clamp(ParentObject.GetTier() / 2, 1, 4));

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(UnequippedEvent E)
        {
            if (E.Item == ParentObject)
                StatShifter.RemoveStatShifts();

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            E.AddAdjective(GetAdjective());
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, "Adjective", GetAdjective());
            E.AddEntry(this, "Description", GetDescription());
            return base.HandleEvent(E);
        }

        public override bool Render(RenderEvent E)
        {
            if (UD_Bones_LunarRegent.CycleColors(ParentObject.Render, ref TileColor, ref DetailColor, Offset: ParentObject.BaseID))
                return base.Render(E);  //true;

            return base.Render(E);
        }
    }
}
