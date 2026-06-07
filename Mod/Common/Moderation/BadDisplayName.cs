using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Names;
using XRL.World;
using XRL.World.Parts;

namespace UD_Bones_Folder.Mod.Moderation
{
    [Serializable]
    public class BadDisplayName : IComposite
    {
        public bool IsBase;
        public bool IsAdjective;
        public bool IsSizeAdjective;
        public bool IsFactionAdjective;
        public bool IsHonorific;
        public bool IsTitle;
        public bool IsEpithet;

        public BadDisplayName()
        { }

        public bool Any()
            => IsBase
            || IsAdjective
            || IsSizeAdjective
            || IsFactionAdjective
            || IsHonorific
            || IsTitle
            || IsEpithet
            ;

        public void AlignWith(OriginalDisplayName OriginalDisplayName)
        {
            IsBase = !(OriginalDisplayName?.BaseName).IsNullOrEmpty();
            IsAdjective = !(OriginalDisplayName?.Adjectives).IsNullOrEmpty();
            IsSizeAdjective = !(OriginalDisplayName?.SizeAdjective ?? default).Key.IsNullOrEmpty();
            IsFactionAdjective = !(OriginalDisplayName?.FactionAdjective).IsNullOrEmpty();
            IsHonorific = !(OriginalDisplayName?.Honorifics).IsNullOrEmpty();
            IsTitle = !(OriginalDisplayName?.Titles).IsNullOrEmpty();
            IsEpithet = !(OriginalDisplayName?.Epithets).IsNullOrEmpty();
        }

        public bool ModerateBaseName(
            GameObject Object,
            ref int ModerationActions,
            ref OriginalDisplayName OriginalDisplayName
            )
        {
            if (IsBase
                && Object != null)
            {
                if (!GameObjectFactory.Factory.HasBlueprint(Object.Blueprint))
                {
                    Utils.Warn($"Attempted to Moderate the {nameof(GameObject.BaseDisplayName)} of an object whose {nameof(GameObject.Blueprint)} doesn't exist.");
                    return false;
                }
                else
                {
                    OriginalDisplayName ??= new();
                    OriginalDisplayName.BaseName = Object.BaseDisplayName;

                    string replacementName = Object.GetBlueprint()?.DisplayName();
                    if (Object.IsLunarRegent()
                        || replacementName.IsNullOrEmpty())
                        replacementName = NameMaker.MakeName(Object);

                    Object.DisplayName = replacementName;
                    ModerationActions++;
                    return true;
                }
            }
            return false;
        }

        public bool ModerateAdjectives(
            GameObject Object,
            ref int ModerationActions,
            ref OriginalDisplayName OriginalDisplayName
            )
        {
            if (IsAdjective
                && Object != null
                && Object.TryGetPart(out DisplayNameAdjectives adjectives))
            {
                OriginalDisplayName ??= new();
                OriginalDisplayName.Adjectives = adjectives.Adjectives;
                Object?.RemovePart(adjectives);
                ModerationActions++;
                return true;
            }
            return false;
        }

        public bool ModerateSizeAdjective(
            GameObject Object,
            ref int ModerationActions,
            ref OriginalDisplayName OriginalDisplayName
            )
        {
            if (IsSizeAdjective
                && Object != null
                && Object.TryGetPart(out SizeAdjective sizeAdjective))
            {
                OriginalDisplayName ??= new();
                OriginalDisplayName.SizeAdjective = new(sizeAdjective.Adjective, sizeAdjective.OrderAdjust);
                Object?.RemovePart(sizeAdjective);
                ModerationActions++;
                return true;
            }
            return false;
        }

        public bool ModerateFactionAdjective(
            GameObject Object,
            ref int ModerationActions,
            ref OriginalDisplayName OriginalDisplayName
            )
        {
            if (IsFactionAdjective
                && Object != null
                && Object.TryGetPart(out DisplayNameFactionAdjective factionAdjective))
            {
                OriginalDisplayName ??= new();
                OriginalDisplayName.FactionAdjective = new string[3]
                {
                    factionAdjective.Faction,
                    factionAdjective.FactionAdjective,
                    factionAdjective.NonFactionAdjective,
                };
                Object?.RemovePart(factionAdjective);
                ModerationActions++;
                return true;
            }
            return false;
        }

        public bool ModerateHonorifics(
            GameObject Object,
            ref int ModerationActions,
            ref OriginalDisplayName OriginalDisplayName
            )
        {
            if (IsHonorific
                && Object != null
                && Object.TryGetPart(out Honorifics honorifics))
            {
                OriginalDisplayName ??= new();
                OriginalDisplayName.Honorifics = new string[2]
                {
                    honorifics.HonorificList,
                    honorifics.HonorificOrder,
                };

                Object.RemovePart(honorifics);

                if (Object?.RequirePart<Honorifics>() is Honorifics newHonorifics)
                {
                    foreach (var _ in OriginalDisplayName.Honorifics[0].CachedDoubleSemicolonExpansion().IteratorSafe())
                        newHonorifics.AddHonorific(NameMaker.MakeHonorific(Object));

                    if (newHonorifics.HonorificList.IsNullOrEmpty())
                        foreach (var honorific in OriginalDisplayName.Honorifics[1].CachedNumericDictionaryExpansion().IteratorSafe())
                            newHonorifics.AddHonorific(NameMaker.MakeHonorific(Object), honorific.Value);
                }
                ModerationActions++;
                return true;
            }
            return false;
        }

        public bool ModerateTitles(
            GameObject Object,
            ref int ModerationActions,
            ref OriginalDisplayName OriginalDisplayName
            )
        {
            if (IsTitle
                && Object != null
                && Object.TryGetPart(out Titles titles))
            {
                OriginalDisplayName ??= new();
                OriginalDisplayName.Titles = new string[2]
                {
                    titles.TitleList,
                    titles.TitleOrder,
                };

                Object.RemovePart(titles);

                if (Object?.RequirePart<Titles>() is Titles newTitles)
                {
                    foreach (var _ in OriginalDisplayName.Titles[0].CachedDoubleSemicolonExpansion().IteratorSafe())
                        newTitles.AddTitle(NameMaker.MakeTitle(Object));

                    if (newTitles.TitleList.IsNullOrEmpty())
                        foreach (var title in OriginalDisplayName.Titles[1].CachedNumericDictionaryExpansion().IteratorSafe())
                            newTitles.AddTitle(NameMaker.MakeTitle(Object), title.Value);
                }
                ModerationActions++;
                return true;
            }
            return false;
        }

        public bool ModerateEpithets(
            GameObject Object,
            ref int ModerationActions,
            ref OriginalDisplayName OriginalDisplayName
            )
        {
            if (IsEpithet
                && Object != null
                && Object.TryGetPart(out Epithets epithets))
            {
                OriginalDisplayName ??= new();
                OriginalDisplayName.Epithets = new string[2]
                {
                    epithets.EpithetList,
                    epithets.EpithetOrder,
                };

                Object.RemovePart(epithets);

                if (Object?.RequirePart<Epithets>() is Epithets newEpithets)
                {
                    foreach (var _ in OriginalDisplayName.Epithets[0].CachedDoubleSemicolonExpansion().IteratorSafe())
                        newEpithets.AddEpithet(NameMaker.MakeEpithet(Object));

                    if (newEpithets.EpithetList.IsNullOrEmpty())
                    {
                        foreach (var epithet in OriginalDisplayName.Epithets[1].CachedNumericDictionaryExpansion().IteratorSafe())
                            newEpithets.AddEpithet(NameMaker.MakeEpithet(Object), epithet.Value);
                    }
                }
                ModerationActions++;
                return true;
            }
            return false;
        }

        public bool ModerateDisplayName(
            GameObject Object,
            ref int ModerationActions,
            out bool AnyFailed,
            out OriginalDisplayName OriginalDisplayName
            )
        {
            AnyFailed = false;
            OriginalDisplayName = null;

            if (Object == null)
                return false;

            var originalDisplayName = OriginalDisplayName;
            try
            {
                ModerateBaseName(Object, ref ModerationActions, ref originalDisplayName);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(ModerateDisplayName), nameof(ModerateBaseName)), x);
                AnyFailed = true;
            }

            try
            {
                ModerateAdjectives(Object, ref ModerationActions, ref originalDisplayName);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(ModerateDisplayName), nameof(ModerateAdjectives)), x);
                AnyFailed = true;
            }

            try
            {
                ModerateSizeAdjective(Object, ref ModerationActions, ref originalDisplayName);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(ModerateDisplayName), nameof(ModerateSizeAdjective)), x);
                AnyFailed = true;
            }

            try
            {
                ModerateFactionAdjective(Object, ref ModerationActions, ref originalDisplayName);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(ModerateDisplayName), nameof(ModerateFactionAdjective)), x);
                AnyFailed = true;
            }

            try
            {
                ModerateHonorifics(Object, ref ModerationActions, ref originalDisplayName);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(ModerateDisplayName), nameof(ModerateHonorifics)), x);
                AnyFailed = true;
            }

            try
            {
                ModerateTitles(Object, ref ModerationActions, ref originalDisplayName);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(ModerateDisplayName), nameof(ModerateTitles)), x);
                AnyFailed = true;
            }

            try
            {
                ModerateEpithets(Object, ref ModerationActions, ref originalDisplayName);
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(ModerateDisplayName), nameof(ModerateEpithets)), x);
                AnyFailed = true;
            }

            OriginalDisplayName = originalDisplayName;

            return true;
        }

        public void Clear()
        {
            IsBase = false;
            IsAdjective = false;
            IsSizeAdjective = false;
            IsFactionAdjective = false;
            IsHonorific = false;
            IsTitle = false;
            IsEpithet = false;
        }

        public IEnumerable<string> GetDebugLines()
        {
            yield return $"{nameof(IsBase)}: {IsBase}";
            yield return $"{nameof(IsAdjective)}: {IsAdjective}";
            yield return $"{nameof(IsSizeAdjective)}: {IsSizeAdjective}";
            yield return $"{nameof(IsFactionAdjective)}: {IsFactionAdjective}";
            yield return $"{nameof(IsHonorific)}: {IsHonorific}";
            yield return $"{nameof(IsTitle)}: {IsTitle}";
            yield return $"{nameof(IsEpithet)}: {IsEpithet}";
        }

        public string DebugString()
            => GetDebugLines()
                .IteratorSafe()
                .Aggregate((string)null, Utils.NewLineDelimitedAggregator)
            ;

        public static explicit operator bool(BadDisplayName Operand)
            => Operand?.Any() is true
            ;
    }
}
