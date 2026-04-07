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

        public override void Attach()
        {
            base.Attach();
            ParentObject.SetStringProperty(nameof(UD_Bones_FeverWarped), null, true);

            if (ParentObject.TryGetPart(out Description description))
                description._Short = ProcessDescription(description._Short).StartReplace().ToString();

            var render = ParentObject.RequirePart<Render>();

            BonesManager.System.RequireAlternativeTileAndBlueprintForGameObject(
                GameObject: ParentObject,
                Blueprint: out ParentObject.Blueprint,
                Tile: out render.Tile);

            if (Stat.RandomCosmetic(0, 5) == 0)
                render.HFlip = true;

            if (Stat.RandomCosmetic(0, 25) == 0)
                render.VFlip = true;
        }

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
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            E.Postfix.AppendRules(GetDescription());
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
            if (UD_Bones_LunarRegent.CycleColors(ParentObject.Render, ref TileColor, ref DetailColor))
                return base.Render(E);  //true;

            return base.Render(E);
        }
    }
}
