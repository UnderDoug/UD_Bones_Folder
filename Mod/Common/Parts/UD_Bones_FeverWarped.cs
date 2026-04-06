using System;
using System.Collections.Generic;
using System.Text;

using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using XRL.Rules;
using XRL.Core;
using XRL.Language;

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
            if (ParentObject.TryGetPart(out Description description))
                description._Short = TextFilters.CrypticMachine(description._Short);

            var render = ParentObject.RequirePart<Render>();

            BonesManager.System.RequireAlternativeTileAndBlueprintForGameObject(
                GameObject: ParentObject,
                Blueprint: out ParentObject.Blueprint,
                Tile: out render.Tile);
        }

        public string GetDescription()
        {
            var sB = Event.NewStringBuilder();
            sB.AppendColored(Utils.GetAnimatedRainbowShaderForFrame(), "Fever warped").Append(": ")
                .Append(ParentObject.ThisTheseDescriptiveCategory()).Append(" has been warped by the process of arriving in this world.");
            return Event.FinalizeString(sB);
        }

        public string GetAdjective()
            => "fever warped".Colored(Utils.GetAnimatedRainbowShaderForFrame());

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
            if (UD_Bones_LunarRegent.CycleColors(E, ref TileColor, ref DetailColor))
                return true;
            Utils.Log($"{Utils.CallChain(nameof(UD_Bones_FeverWarped), nameof(Render))}");
            return Render(E);
        }
    }
}
