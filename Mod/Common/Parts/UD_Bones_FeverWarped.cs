using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;


using XRL.Core;
using XRL.Collections;
using XRL.Language;
using XRL.Rules;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

using SerializeField = UnityEngine.SerializeField;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;
using UD_Bones_Folder.Mod.Moderation;
using XRL.Names;

namespace XRL.World.Parts
{
    [HasVariableReplacer]
    [Serializable]
    public class UD_Bones_FeverWarped : UD_Bones_BaseLunarPart
    {
        [Serializable]
        [Flags]
        public enum WarpFlags
        {
            None = 0,
            Blueprint = 1,
            Tile = 2,
            Total = 3,
        }

        public static string TileOnlyProp => $"{nameof(UD_Bones_FeverWarped)}::{nameof(TileOnly)}";

        protected string TileColor;
        protected string DetailColor;

        public WarpFlags Flags;

        private bool TileOnly
        {
            get => Flags == WarpFlags.Tile;
            set => Flags = WarpFlags.Tile;
        }

        public bool CosmeticOnly
        {
            get => Flags == WarpFlags.None;
            set => Flags = WarpFlags.None;
        }


        [SerializeField]
        private string _OriginalShortDesc;
        public string OriginalShortDesc
        {
            get => _OriginalShortDesc;
            protected set => _OriginalShortDesc = value;
        }

        private string DisplayNameCache;
        private string AdjectiveCache;

        public bool HasBadWord;
        public BadWord.SeverityLevel BadWordSeverityOption;
        public BadDisplayName BadWordName;
        public bool BadWordDesc;

        public UD_Bones_FeverWarped()
        {
        }

        public UD_Bones_FeverWarped(WarpFlags Flags)
            : this()
        {
            this.Flags = Flags;
        }

        public UD_Bones_FeverWarped(bool TileOnly)
            : this()
        {
            this.TileOnly = TileOnly;
        }

        public static UD_Bones_FeverWarped NewCosmeticOnly()
            => new UD_Bones_FeverWarped
            {
                CosmeticOnly = true,
            };

        public override void Attach()
        {
            base.Attach();
        }

        public override void Initialize()
        {
            base.Initialize();
            ParentObject.SetStringProperty(nameof(UD_Bones_FeverWarped), null, true);
            ParentObject.SetStringProperty($"{nameof(UD_Bones_FeverWarped)}::{nameof(TileOnly)}", null, true);
            ParentObject.SetStringProperty($"{nameof(UD_Bones_FeverWarped)}::OriginalBlueprint", ParentObject.Blueprint, true);
            ParentObject.SetStringProperty(Const.IS_MAD_PROP, $"{true}");

            var bonesColors = ParentObject.RequirePart<UD_Bones_LunarColors>()
                .OverrideBonesIDTyped<UD_Bones_LunarColors>(BonesID);
            bonesColors.Persists = true;

            if (ParentObject.IsEquipment())
            {
                if (!ParentObject.HasPart<Cursed>())
                {
                    // this was originally FAFO, but was maybe a little harsh.
                    ParentObject.AddPart<Cursed>().RevealInDescription = true;
                }
            }

            if (!CosmeticOnly
                && ParentObject.TryRequireBlueprintSpec(out BlueprintSpec blueprintSpec)
                && BonesManager.System.RequireReplacementEntryForGameObject(blueprintSpec) is ReplacementEntry replacementEntry)
                replacementEntry.ApplyTo(ParentObject, Flags.HasFlag(WarpFlags.Tile), Flags.HasFlag(WarpFlags.Blueprint));

            if (ParentObject.TryGetPart(out UD_Bones_Moderated moderatedPart))
                moderatedPart.ModerateContent(ParentObject, Context: nameof(FeverWarped));

            if (ParentObject.TryGetPart(out Description description))
            {
                if (!description._Short.IsNullOrEmpty())
                    OriginalShortDesc = description._Short;
                else
                    OriginalShortDesc = $"{ParentObject.IndefiniteArticleDescriptiveCategory(true, "wretched")}, warped into an unfamiliar configuration.";

                string bakedDescription = OriginalShortDesc
                    .StartReplace()
                    .AddObject(ParentObject)
                    .ToString();

                description._Short = FeverWarpText(bakedDescription);
            }
        }

        public static UD_Bones_FeverWarped NewWithBonesID(
            string BonesID,
            WarpFlags Flags,
            bool Persists = true
            )
            => new UD_Bones_FeverWarped
            {
                Flags = Flags,
                Persists = Persists,
            }
            .OverrideBonesIDTyped< UD_Bones_FeverWarped>(BonesID)
            ;

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
            AdjectiveCache ??= UD_Bones_LunarColors.ApplyAnimatedLunarShader("fever warped", TileColor);

            return AdjectiveCache;
        }

        public static int GetWillpowerMalus(int Tier)
            => -Math.Clamp(Math.Clamp(Tier, 1, 8) / 2, 1, 4)
            ;

        public static string FeverWarpText(string Description, bool DoShaderReplace = true)
        {
            if (Description.IsNullOrEmpty())
                return Description;

            Description = Description.Strip();
            //Utils.Log(Description);

            using var corruptions = ScopeDisposedList<string>.GetFromPool();
            while (corruptions.IsNullOrEmpty()
                || corruptions.Aggregate(0, (a, n) => a + n.Length) <= Description.Length)
            {
                string corruption = TextFilters.GenerateCrypticWord();
                if (corruption.Length < 8)
                    corruptions.Add(corruption);
            }

            int corruptionsLength = corruptions.Aggregate(0, (a, n) => a + n.Length);
            int diff = corruptionsLength - Description.Length;
            int startDiff = (int)Math.Ceiling(diff / 2.0);
            int endDiff = (int)Math.Floor(diff / 2.0);

            int startAt = 0;
            if (corruptions.Aggregate(0, (a, n) => a + n.Length) > Description.Length)
                startAt = Stat.RandomCosmetic(0, diff);

            //Utils.Log($"{1.Indent()}{nameof(diff)}: {diff}, {nameof(startDiff)}: {startDiff}, {nameof(endDiff)}: {endDiff}");

            string firstCorruption = null;
            if (startDiff > 0)
                firstCorruption = corruptions.TakeAt(0);

            string lastCorruption = null;
            if (endDiff > 0
                && corruptions.Count > 0)
                lastCorruption = corruptions.TakeAt(corruptions.Count - 1);

            //Utils.Log($"{1.Indent()}{nameof(firstCorruption)}: {firstCorruption?.Length ?? -1}, {nameof(lastCorruption)}: {lastCorruption?.Length ?? -1}");

            if (!firstCorruption.IsNullOrEmpty()
                && startDiff > 0
                && startDiff < firstCorruption.Length)
                corruptions.Insert(0, firstCorruption[startDiff..]);

            if (!lastCorruption.IsNullOrEmpty()
                && endDiff > 0
                && endDiff < lastCorruption.Length)
                corruptions.Add(lastCorruption[endDiff..]);

            corruptionsLength = corruptions.Aggregate(0, (a, n) => a + n.Length);

            /*Utils.Log($"{1.Indent()}{nameof(corruptionsLength)}: {corruptionsLength}, " +
                $"{Utils.CallChain(nameof(Description), nameof(Description.Length))}: {Description.Length}");*/

            int originalStart = startAt;
            int moduloOffset = Stat.RandomCosmetic(0, 6999);

            using var descriptions = ScopeDisposedList<string>.GetFromPool();
            int startPos = 0;
            foreach (string corruption in corruptions)
            {
                int endPos = Math.Min(startPos + corruption.Length, Description.Length);

                if (startPos < endPos)
                    descriptions.Add(Description[startPos..endPos]);

                startPos = endPos;
            }

            // Utils.Log($"{1.Indent()}{descriptions.Aggregate("", (a, n) => a + (!a.IsNullOrEmpty() ? "|" : null) + n)}");

            int modOffset = Stat.RandomCosmetic(0, 6999);

            string text = descriptions.Count.Aggregate(
                seed: "",
                func: delegate (string text, int i)
                {
                    string next = ((i + moduloOffset) % 2 == 0)
                        ? descriptions[i]
                        : corruptions[i];
                    return text + next;
                });

            using var fragments = ScopeDisposedList<string>.GetFromPool();
            int colorOffset = Stat.RandomCosmetic(1, 3);
            if (50.in100())
                colorOffset *= -1;

            startPos = 0;
            for (int i = 0; i < descriptions.Count; i++)
            {
                if (descriptions[i] is string description)
                {
                    if (i > 0)
                        startPos = Math.Clamp(startPos, 0, Description.Length);

                    int endPos = Math.Clamp(startPos + description.Length + colorOffset, 0, Description.Length);

                    if (startPos < endPos)
                        fragments.Add(text[startPos..endPos]);

                    if (i == descriptions.Count - 1
                        && endPos < Description.Length)
                        fragments.Add(text[endPos..]);

                    startPos = endPos;
                }
            }

            using var lunarColors = UD_Bones_LunarColors.BorrowScopeDisposedColorsFromPool();
            text = fragments.Count.Aggregate(
                seed: "",
                func: delegate (string text, int i)
                {
                    int index = (int)Math.Floor(i / 2.0);
                    if ((i + moduloOffset) % 2 == 0)
                        return text + fragments[i];
                    return text + $"=LunarShader:{fragments[i]}:{index.NegSafeModulo(lunarColors.Count)}=";
                });

            if (DoShaderReplace)
                text = text
                    .StartReplace()
                    .ToString();

            return text;
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
            if (Options.EnableFlashingLightEffects
                || (E.LastFrame + ParentObject.BaseID).NegSafeModulo(UD_Bones_LunarColors.BaseAnimationLengthInFrames) == 0)
            {
                DisplayNameCache = null;
                AdjectiveCache = null;
            }
            TileColor = E.TileColor;
            DetailColor = E.DetailColor;
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, "Adjective", GetAdjective());
            E.AddEntry(this, "Description", GetDescription());

            using var flagStrings = ScopeDisposedList<string>.GetFromPool();
            if (Flags == WarpFlags.None)
                flagStrings.Add($"{WarpFlags.None} ({(int)WarpFlags.None})");
            if (Flags.HasFlag(WarpFlags.Blueprint))
                flagStrings.Add($"{WarpFlags.Blueprint} ({(int)WarpFlags.Blueprint})");
            if (Flags.HasFlag(WarpFlags.Tile))
                flagStrings.Add($"{WarpFlags.Tile} ({(int)WarpFlags.Tile})");
            if (Flags.HasFlag(WarpFlags.Total))
                flagStrings.Add($"{WarpFlags.Total} ({(int)WarpFlags.Total})");
            E.AddEntry(this, $"{nameof(Flags)} ({(int)Flags})", flagStrings.Aggregate("", Utils.NewLineDelimitedAggregator));

            E.AddEntry(this, nameof(TileOnly), TileOnly);
            E.AddEntry(this, nameof(CosmeticOnly), CosmeticOnly);

            return base.HandleEvent(E);
        }

        [VariableReplacer(Keys = new string[] { "FeverWarped" })]
        public static string FeverWarped(DelegateContext Context)
            => Context.Parameters.Aggregate("", (a, n) => a + (!a.IsNullOrEmpty() ? ":" : null) + n).FeverWarped();

        [VariablePostProcessor(Keys = new string[] { "FeverWarped" })]
        public static void FeverWarpedPost(DelegateContext Context)
        {
            var oldValue = Context.Value.ToString();
            Context.Value.Clear();
            Context.Value.Append(oldValue.FeverWarped());
        }
    }
}
