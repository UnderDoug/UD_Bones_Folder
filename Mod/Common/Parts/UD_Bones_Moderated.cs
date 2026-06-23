using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using XRL.Core;
using XRL.Collections;
using XRL.Language;
using XRL.Names;
using XRL.Rules;
using XRL.World.Effects;
using XRL.World.Text.Attributes;
using XRL.World.Text.Delegates;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;
using UD_Bones_Folder.Mod.Moderation;

using static UD_Bones_Folder.Mod.Moderation.DisplayNameData;

using SerializeField = UnityEngine.SerializeField;
using System.Diagnostics;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_Moderated : IScribedPart
    {
        public static IBadWord.SeverityLevel OptionSeverity => Options.ModerationMinimumSeverityLevel;

        public BadDisplayName BadWordName;
        public bool BadWordDesc;

        public DisplayNameData OriginalDisplayName;
        public DisplayNameData ModeratedDisplayName;

        public string OriginalShortDesc;
        public string ModeratedShortDesc;

        [SerializeField]
        private int ModerationActions;

        public IBadWord.SeverityLevel? NameModerationProcessed;
        public IBadWord.SeverityLevel? DescModerationProcessed;

        public bool IsModerationPaused;

        public UD_Bones_Moderated()
        {
        }

        public UD_Bones_Moderated(BadDisplayName BadWordName, bool BadWordDesc)
            : this()
        {
            this.BadWordName = BadWordName;
            this.BadWordDesc = BadWordDesc;
        }

        // Every time it's added, including during deserialization
        public override void Attach()
        {
            base.Attach();
        }

        // Once when first added
        public override void Initialize()
        {
            base.Initialize();
            // ModerateContent(ParentObject);            
        }

        public override void Remove()
        {
            base.Remove();
            RestoreModeratedContent(ParentObject, ClearAfter: true);
            Clear();
        }

        public IBadWord.SeverityLevel? ModerateDisplayName(GameObject Object = null)
        {
            Object ??= ParentObject;

            if (Object == null)
                return null;

            if (NameModerationProcessed == OptionSeverity)
                return OptionSeverity;

            if (!(bool)BadWordName)
                return OptionSeverity;

            if (BadWordName.ModerateDisplayName(
                Object: Object,
                ModerationActions: ref ModerationActions,
                AnyFailed: out bool anyFailed,
                AnyModerated: out bool anyModerated,
                OriginalDisplayName: out OriginalDisplayName,
                PreconfiguredModeratedDisplayName: ModeratedDisplayName))
            {
                if (anyFailed)
                    return null;

                if (anyModerated)
                {
                    ModeratedDisplayName?.Clear();
                    ModeratedDisplayName = GetFrom(Object, BadWordName, OriginalDisplayName);
                }
            }

            return OptionSeverity;
        }

        public bool TryModerateDisplayName(GameObject Object = null)
        {
            if (NameModerationProcessed == OptionSeverity)
            {
                NameModerationProcessed = RestoreModeratedDisplayName(Object, ClearAfter: true);

                if (NameModerationProcessed != null)
                    return false;
            }

            try
            {
                if (!Object.HasBadDisplayName(out BadWordName, OptionSeverity))
                    return true;

                ModerateDisplayName(Object);

                return true;
            }
            finally
            {
                NameModerationProcessed = OptionSeverity;
            }
        }

        public IBadWord.SeverityLevel? ModerateDescription(GameObject Object = null)
        {
            Object ??= ParentObject;

            if (Object == null)
                return null;

            if (DescModerationProcessed == OptionSeverity)
                return OptionSeverity;

            if (!BadWordDesc)
                return OptionSeverity;

            if (!Object.TryGetPart(out Description description))
                return OptionSeverity;

            if (!GameObjectFactory.Factory.HasBlueprint(Object.Blueprint))
            {
                if (!Object.FeverWarp(Object.GetBonesID(Default: null)))
                {
                    Utils.Warn($"Attempted to Moderate the {nameof(Description)} of an object whose {nameof(GameObject.Blueprint)} doesn't exist.");
                    return null;
                }
            }

            OriginalShortDesc = description._Short;
            ModeratedShortDesc ??= Object.GetBlueprint().GetPartParameter<string>(nameof(Description), nameof(Description.Short));
            description._Short = ModeratedShortDesc;
            ModerationActions++;
            return OptionSeverity;
        }

        public bool TryModerateDescription(GameObject Object = null)
        {
            if (DescModerationProcessed == OptionSeverity)
            {
                DescModerationProcessed = RestoreModeratedDescription(Object, ClearAfter: true);

                if (DescModerationProcessed != null)
                    return false;
            }

            try
            {
                if (!Object.HasBadDescription(out BadWordDesc, OptionSeverity))
                    return true;

                ModerateDescription(Object);

                return true;
            }
            finally
            {
                DescModerationProcessed = OptionSeverity;
            }
        }

        public bool ModerateContent(GameObject Object = null, string Context = null, bool Silent = true)
        {
            Object ??= ParentObject;

            if (NameModerationProcessed != OptionSeverity
                || DescModerationProcessed != OptionSeverity)
            {
                if (Object == null)
                    return true;

                var sw = Stopwatch.StartNew();
                try
                {
                    if (NameModerationProcessed != null
                        && DescModerationProcessed != null)
                        RestoreModeratedContent(Object, ClearAfter: true);

                    if (!Object.HasBadWord(out BadWordName, out BadWordDesc, OptionSeverity))
                    {
                        NameModerationProcessed = OptionSeverity;
                        DescModerationProcessed = OptionSeverity;
                        return true;
                    }

                    NameModerationProcessed = ModerateDisplayName(Object);
                    DescModerationProcessed = ModerateDescription(Object);

                    return true;
                }
                finally
                {
                    if (!Silent)
                    {
                        string contextString = $"{nameof(ModerateContent)} for {Object?.DebugName?.Strip() ?? "NO_OBJECT"}";
                        if (!Context.IsNullOrEmpty())
                            contextString = $"{Context}: {contextString}";

                        Utils.Log($"{contextString} took {sw.Elapsed.ValueUnits()} to complete.");
                    }
                    sw.Stop();
                }
            }
            return false;
        }

        public IBadWord.SeverityLevel? RestoreModeratedDisplayName(GameObject Object = null, bool ClearAfter = false)
        {
            Object ??= ParentObject;

            if (Object == null)
                return OptionSeverity;

            if (NameModerationProcessed == null)
                return null;

            if (!(bool)BadWordName)
                return null;

            if (OriginalDisplayName == null)
                return null;

            try
            {
                if (!ClearAfter)
                    ModeratedDisplayName ??= GetFrom(Object, OriginalDisplayName);

                OriginalDisplayName.RestoreDisplayName(
                    Object: Object,
                    ModerationActions: ref ModerationActions,
                    AnyFailed: out bool anyFailed,
                    ClearAfter: ClearAfter);
                
                if (ClearAfter)
                {
                    if (!anyFailed)
                    {
                        ModeratedDisplayName?.Clear();
                        BadWordName?.Clear();
                    }
                    else
                        BadWordName.AlignWith(OriginalDisplayName);
                }

                return anyFailed
                    ? NameModerationProcessed
                    : null
                    ;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(RestoreModeratedContent)} at {nameof(OriginalShortDesc)} for {Object?.DebugName ?? "NO_OBJECT"}", x);
                return NameModerationProcessed;
            }
        }

        public IBadWord.SeverityLevel? RestoreModeratedDescription(GameObject Object = null, bool ClearAfter = false)
        {
            Object ??= ParentObject;

            if (Object == null)
                return OptionSeverity;

            if (DescModerationProcessed == null)
                return null;

            if (!BadWordDesc)
                return null;

            if (OriginalShortDesc.IsNullOrEmpty())
                return null;

            if (!Object.TryGetPart(out Description description))
                return null;

            try
            {
                ModeratedShortDesc ??= description._Short;
                description._Short = OriginalShortDesc;
                ModerationActions--;

                if (ModerationActions < 0)
                    Utils.Error(GetErrorText(nameof(OriginalShortDesc), Object, ModerationActions), GetInvalidModerationOperationException());

                if (ClearAfter)
                {
                    OriginalShortDesc = null;
                    ModeratedShortDesc = null;
                    BadWordDesc = false;
                }
                return OptionSeverity;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(RestoreModeratedContent)} at {nameof(OriginalShortDesc)} for {Object?.DebugName ?? "NO_OBJECT"}", x);
                return DescModerationProcessed;
            }
        }

        public bool RestoreModeratedContent(GameObject Object = null, bool ClearAfter = false)
        {
            Object ??= ParentObject;

            try
            {
                if (Object == null)
                    return true;

                if (ModerationActions <= 0)
                {
                    if (ModerationActions < 0)
                        Utils.Error(
                            Context: GetErrorText(nameof(RestoreModeratedContent), Object, ModerationActions),
                            X: GetInvalidModerationOperationException());
                    return false;
                }

                RestoreModeratedDisplayName(Object, ClearAfter: ClearAfter);
                RestoreModeratedDescription(Object, ClearAfter: ClearAfter);

                NameModerationProcessed = null;
                DescModerationProcessed = null;

                return true;
            }
            finally
            {
                ModerationActions = 0;
            }
        }

        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            Registrar.Register(GetDisplayNameEvent.ID, EventOrder.EXTREMELY_EARLY, Serialize: true);
            Registrar.Register(GetShortDescriptionEvent.ID, EventOrder.EXTREMELY_EARLY, Serialize: true);
            base.Register(Object, Registrar);
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (E.Object == ParentObject
                && !IsModerationPaused
                && E.Context != BadWord.DisplayNameContext)
                ModerateContent(ParentObject, nameof(GetDisplayNameEvent));
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (E.Object == ParentObject
                && !IsModerationPaused)
                ModerateContent(ParentObject, nameof(GetShortDescriptionEvent));
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            try
            {
                E.AddEntry(
                    Part: this,
                    Name: nameof(OptionSeverity),
                    Value: $"{(int)OptionSeverity} ({OptionSeverity})");

                E.AddEntry(
                    Part: this,
                    Name: nameof(NameModerationProcessed),
                    Value: $"{(int)NameModerationProcessed} ({NameModerationProcessed})");

                E.AddEntry(
                    Part: this,
                    Name: nameof(DescModerationProcessed),
                    Value: $"{(int)DescModerationProcessed} ({DescModerationProcessed})");

                string badWordName = BadWordName?.DebugString();
                if (badWordName.IsNullOrEmpty())
                    badWordName = $"{(bool)BadWordName}";
                E.AddEntry(this, nameof(BadWordName), badWordName);
                E.AddEntry(this, nameof(BadWordDesc), BadWordDesc);
                
                string originalDisplayName = OriginalDisplayName?.DebugString();
                if (originalDisplayName.IsNullOrEmpty())
                    originalDisplayName = "null";
                E.AddEntry(this, nameof(OriginalDisplayName), originalDisplayName);

                E.AddEntry(this, nameof(OriginalShortDesc), OriginalShortDesc ?? "null");
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(UD_Bones_Moderated)}.{nameof(GetDebugInternalsEvent)}", x);
                E.AddEntry(this, nameof(Exception), x.GetType().Name);
            }
            return base.HandleEvent(E);
        }

        public void Clear()
        {
            BadWordName = null;
            BadWordDesc = false;

            OriginalDisplayName = null;
            OriginalShortDesc = null;

            ModerationActions = 0;

            NameModerationProcessed = null;
            DescModerationProcessed = null;
        }
    }
}
