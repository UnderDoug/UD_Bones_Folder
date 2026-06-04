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
    [Serializable]
    public class UD_Bones_Moderated : IScribedPart
    {
        public BadWord.SeverityLevel BadWordSeverityOption;
        public BadDisplayName BadWordName;
        public bool BadWordDesc;

        public string OriginalBaseName;
        public string OriginalAdjectives;
        public KeyValuePair<string, int> OriginalSizeAdjective;
        public string[] OriginalFactionAdjective;
        public string[] OriginalHonorifics;
        public string[] OriginalTitles;
        public string[] OriginalEpithets;

        public string OriginalShortDesc;

        [SerializeField]
        private int IsModerated;

        public UD_Bones_Moderated()
        {
            BadWordSeverityOption = Options.ModerationMinimumSeverityLevel;
        }

        public UD_Bones_Moderated(BadDisplayName BadWordName, bool BadWordDesc)
            : this()
        {
            this.BadWordName = BadWordName;
            this.BadWordDesc = BadWordDesc;
        }

        public override void Attach()
        {
            base.Attach();
        }

        public bool ModerateContent(GameObject ParentObject = null)
        {
            ParentObject ??= this.ParentObject;

            if (ParentObject == null)
                return false;

            if (IsModerated > 0)
            {
                Utils.Warn($"{nameof(ModerateContent)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("action")} already taken.");
                return false;
            }

            IsModerated = 0;

            if (BadWordName != null)
            {
                if (BadWordName.IsBase
                    && ParentObject != null)
                {
                    OriginalBaseName = ParentObject.BaseDisplayName;

                    string replacementName = ParentObject.GetBlueprint()?.DisplayName();
                    if (ParentObject.IsLunarRegent()
                        || replacementName.IsNullOrEmpty())
                        replacementName = NameMaker.MakeName(ParentObject);

                    ParentObject.DisplayName = replacementName;
                    IsModerated++;
                }

                if (BadWordName.IsAdjective
                    && ParentObject != null
                    && ParentObject.TryGetPart(out DisplayNameAdjectives adjectives))
                {
                    OriginalAdjectives = adjectives.Adjectives;
                    ParentObject?.RemovePart(adjectives);
                    IsModerated++;
                }

                if (BadWordName.IsSizeAdjective
                    && ParentObject != null
                    && ParentObject.TryGetPart(out SizeAdjective sizeAdjective))
                {
                    OriginalSizeAdjective = new(sizeAdjective.Adjective, sizeAdjective.OrderAdjust);
                    ParentObject?.RemovePart(sizeAdjective);
                    IsModerated++;
                }

                if (BadWordName.IsFactionAdjective
                    && ParentObject != null
                    && ParentObject.TryGetPart(out DisplayNameFactionAdjective factionAdjective))
                {
                    OriginalFactionAdjective = new string[3]
                    {
                        factionAdjective.Faction,
                        factionAdjective.FactionAdjective,
                        factionAdjective.NonFactionAdjective,
                    };
                    ParentObject?.RemovePart(factionAdjective);
                    IsModerated++;
                }

                if (BadWordName.IsHonorific
                    && ParentObject != null
                    && ParentObject.TryGetPart(out Honorifics honorifics))
                {
                    OriginalHonorifics = new string[2]
                    {
                        honorifics.HonorificList,
                        honorifics.HonorificOrder,
                    };

                    ParentObject.RemovePart(honorifics);

                    if (ParentObject?.RequirePart<Honorifics>() is Honorifics newHonorifics)
                    {
                        foreach (var honorific in OriginalHonorifics[0].CachedDoubleSemicolonExpansion().IteratorSafe())
                            newHonorifics.AddHonorific(NameMaker.MakeHonorific(ParentObject));

                        if (newHonorifics.HonorificList.IsNullOrEmpty())
                        {
                            foreach (var honorific in OriginalHonorifics[1].CachedNumericDictionaryExpansion().IteratorSafe())
                                newHonorifics.AddHonorific(NameMaker.MakeHonorific(ParentObject), honorific.Value);
                        }
                    }
                    IsModerated++;
                }

                if (BadWordName.IsTitle
                    && ParentObject != null
                    && ParentObject.TryGetPart(out Titles titles))
                {
                    OriginalTitles = new string[2]
                    {
                        titles.TitleList,
                        titles.TitleOrder,
                    };

                    ParentObject.RemovePart(titles);

                    if (ParentObject?.RequirePart<Titles>() is Titles newTitles)
                    {
                        foreach (var honorific in OriginalTitles[0].CachedDoubleSemicolonExpansion().IteratorSafe())
                            newTitles.AddTitle(NameMaker.MakeTitle(ParentObject));

                        if (newTitles.TitleList.IsNullOrEmpty())
                        {
                            foreach (var title in OriginalTitles[1].CachedNumericDictionaryExpansion().IteratorSafe())
                                newTitles.AddTitle(NameMaker.MakeTitle(ParentObject), title.Value);
                        }
                    }
                    IsModerated++;
                }

                if (BadWordName.IsEpithet
                    && ParentObject != null
                    && ParentObject.TryGetPart(out Epithets epithets))
                {
                    OriginalEpithets = new string[2]
                    {
                        epithets.EpithetList,
                        epithets.EpithetOrder,
                    };

                    ParentObject.RemovePart(epithets);

                    if (ParentObject?.RequirePart<Epithets>() is Epithets newEpithets)
                    {
                        foreach (var honorific in OriginalEpithets[0].CachedDoubleSemicolonExpansion().IteratorSafe())
                            newEpithets.AddEpithet(NameMaker.MakeEpithet(ParentObject));

                        if (newEpithets.EpithetList.IsNullOrEmpty())
                        {
                            foreach (var epithet in OriginalEpithets[1].CachedNumericDictionaryExpansion().IteratorSafe())
                                newEpithets.AddEpithet(NameMaker.MakeEpithet(ParentObject), epithet.Value);
                        }
                    }
                    IsModerated++;
                }
            }

            if (BadWordDesc
                && ParentObject != null
                && ParentObject.TryGetPart(out Description description))
            {
                OriginalShortDesc = description._Short;
                description._Short = ParentObject.GetBlueprint().GetPartParameter<string>(nameof(Description), nameof(Description._Short));
                IsModerated++;
            }

            return IsModerated > 0;
        }

        public override void Initialize()
        {
            base.Initialize();
            ModerateContent(ParentObject);            
        }

        public void RestoreModeratedContent(GameObject ParentObject = null)
        {
            ParentObject ??= this.ParentObject;

            if (ParentObject == null)
                return;

            if (IsModerated <= 0)
            {
                if (IsModerated < 0)
                    Utils.Error(
                        Context: $"{nameof(RestoreModeratedContent)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("too many action")}",
                        X: new InvalidOperationException($"More moderation actions were reversed than were originally taken."));
                return;
            }

            if (OriginalBaseName != null
                && ParentObject != null)
            {
                ParentObject.DisplayName = OriginalBaseName;
                IsModerated--;

                if (IsModerated < 0)
                    Utils.Error(
                        Context: $"{nameof(RestoreModeratedContent)} at {nameof(OriginalBaseName)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("too many action")}",
                        X: new InvalidOperationException($"More moderation actions were reversed than were originally taken."));
            }

            if (OriginalAdjectives != null
                && ParentObject != null)
            {
                var adjectives = ParentObject.RequirePart<DisplayNameAdjectives>();
                adjectives.Adjectives = OriginalAdjectives;
                IsModerated--;

                if (IsModerated < 0)
                    Utils.Error(
                        Context: $"{nameof(RestoreModeratedContent)} at {nameof(OriginalAdjectives)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("too many action")}",
                        X: new InvalidOperationException($"More moderation actions were reversed than were originally taken."));
            }

            if (OriginalSizeAdjective.Key != null
                && ParentObject != null)
            {
                var sizeAdjective = ParentObject.RequirePart<SizeAdjective>();
                sizeAdjective.Adjective = OriginalSizeAdjective.Key;
                sizeAdjective.OrderAdjust = OriginalSizeAdjective.Value;
                IsModerated--;

                if (IsModerated < 0)
                    Utils.Error(
                        Context: $"{nameof(RestoreModeratedContent)} at {nameof(OriginalSizeAdjective)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("too many action")}",
                        X: new InvalidOperationException($"More moderation actions were reversed than were originally taken."));
            }

            if (!OriginalFactionAdjective.IsNullOrEmpty()
                && ParentObject != null)
            {
                var factionAdjective = ParentObject.RequirePart<DisplayNameFactionAdjective>();
                factionAdjective.Faction = OriginalFactionAdjective[0];
                factionAdjective.FactionAdjective = OriginalFactionAdjective[1];
                factionAdjective.NonFactionAdjective = OriginalFactionAdjective[2];
                IsModerated--;

                if (IsModerated < 0)
                    Utils.Error(
                        Context: $"{nameof(RestoreModeratedContent)} at {nameof(OriginalFactionAdjective)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("too many action")}",
                        X: new InvalidOperationException($"More moderation actions were reversed than were originally taken."));
            }

            if (!OriginalHonorifics.IsNullOrEmpty()
                && ParentObject != null)
            {
                var honorifics = ParentObject.RequirePart<Honorifics>();
                honorifics.HonorificList = OriginalHonorifics[0];
                honorifics.HonorificOrder = OriginalHonorifics[1];
                IsModerated--;

                if (IsModerated < 0)
                    Utils.Error(
                        Context: $"{nameof(RestoreModeratedContent)} at {nameof(OriginalHonorifics)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("too many action")}",
                        X: new InvalidOperationException($"More moderation actions were reversed than were originally taken."));
            }

            if (!OriginalTitles.IsNullOrEmpty()
                && ParentObject != null)
            {
                var titles = ParentObject.RequirePart<Titles>();
                titles.TitleList = OriginalTitles[0];
                titles.TitleOrder = OriginalTitles[1];
                IsModerated--;

                if (IsModerated < 0)
                    Utils.Error(
                        Context: $"{nameof(RestoreModeratedContent)} at {nameof(OriginalTitles)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("too many action")}",
                        X: new InvalidOperationException($"More moderation actions were reversed than were originally taken."));
            }

            if (!OriginalEpithets.IsNullOrEmpty()
                && ParentObject != null)
            {
                var epithets = ParentObject.RequirePart<Epithets>();
                epithets.EpithetList = OriginalEpithets[0];
                epithets.EpithetOrder = OriginalEpithets[1];
                IsModerated--;

                if (IsModerated < 0)
                    Utils.Error(
                        Context: $"{nameof(RestoreModeratedContent)} at {nameof(OriginalEpithets)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("too many action")}",
                        X: new InvalidOperationException($"More moderation actions were reversed than were originally taken."));
            }

            if (OriginalShortDesc != null
                && ParentObject != null)
            {
                var description = ParentObject.RequirePart<Description>();
                description._Short = OriginalShortDesc;
                IsModerated--;

                if (IsModerated < 0)
                    Utils.Error(
                        Context: $"{nameof(RestoreModeratedContent)} at {nameof(OriginalShortDesc)} for {ParentObject?.DebugName}, {Math.Abs(IsModerated).Things("too many action")}",
                        X: new InvalidOperationException($"More moderation actions were reversed than were originally taken."));
            }
        }

        public override void Remove()
        {
            base.Remove();
            RestoreModeratedContent(ParentObject);
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            // Registrar.Register(GetShortDescriptionEvent.ID, EventOrder.VERY_LATE, Serialize: true);
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(BadWordSeverityOption), $"{(int)BadWordSeverityOption} ({BadWordSeverityOption})");

            string badWordName = BadWordName.DebugStrings().IteratorSafe().Aggregate("", Utils.NewLineDelimitedAggregator);
            if (badWordName.IsNullOrEmpty())
                badWordName = "null";
            E.AddEntry(this, nameof(BadWordName), badWordName);

            E.AddEntry(this, nameof(BadWordDesc), BadWordDesc);

            E.AddEntry(this, nameof(OriginalBaseName), OriginalBaseName ?? "none");
            E.AddEntry(this, nameof(OriginalAdjectives), OriginalAdjectives ?? "none");

            string originalSizeAdjective = "none";
            if (OriginalSizeAdjective.Key != null)
                originalSizeAdjective = $"{OriginalSizeAdjective.Key ?? "none"}: {OriginalSizeAdjective.Value}";
            E.AddEntry(this, nameof(OriginalSizeAdjective), originalSizeAdjective);

            string originalFactionAdjective = "none";
            if (!OriginalFactionAdjective.IsNullOrEmpty())
                originalFactionAdjective = $"{OriginalFactionAdjective[0] ?? "none"}, {OriginalFactionAdjective[1] ?? "none"}, {OriginalFactionAdjective[2] ?? "none"}";
            E.AddEntry(this, nameof(OriginalFactionAdjective), originalFactionAdjective);

            string originalHonorifics = "none";
            if (!OriginalHonorifics.IsNullOrEmpty())
                originalHonorifics = $"{OriginalHonorifics[0] ?? "none"}, {OriginalHonorifics[1] ?? "none"}";
            E.AddEntry(this, nameof(OriginalHonorifics), originalHonorifics);

            string originalTitles = "none";
            if (!OriginalTitles.IsNullOrEmpty())
                originalTitles = $"{OriginalTitles[0] ?? "none"}, {OriginalTitles[1] ?? "none"}";
            E.AddEntry(this, nameof(OriginalTitles), originalTitles);

            string originalEpithets = "none";
            if (!OriginalEpithets.IsNullOrEmpty())
                originalEpithets = $"{OriginalEpithets[0] ?? "none"}, {OriginalEpithets[1] ?? "none"}";
            E.AddEntry(this, nameof(OriginalEpithets), originalEpithets);

            E.AddEntry(this, nameof(OriginalShortDesc), OriginalShortDesc ?? "none");

            return base.HandleEvent(E);
        }
    }
}
