using System;
using System.Collections.Generic;
using System.Text;

using XRL.Core;
using XRL.World.Effects;

using UD_Bones_Folder.Mod;
using UD_Bones_Folder.Mod.Events;

using SerializeField = UnityEngine.SerializeField;

namespace XRL.World.Parts
{
    [Serializable]
    public class UD_Bones_LunarRegent : UD_Bones_BaseLunarPart
    {
        public bool Cremated;

        public string RegalTitle => GetRegalTitle();

        protected bool? _IsMad;
        public bool IsMad
        {
            get
            {
                if (ParentObject != null)
                    _IsMad = ParentObject.GetStringProperty(Const.IS_MAD_PROP, $"{_IsMad.GetValueOrDefault()}").EqualsNoCase($"{true}");
                return _IsMad.GetValueOrDefault();
            }
            set
            {
                _IsMad = value;
                ParentObject?.SetStringProperty(Const.IS_MAD_PROP, _IsMad.GetValueOrDefault() ? $"{true}" : null, true);
            }
        }

        public string OriginalShortDesc;

        [SerializeField]
        private bool _DoneDescription;
        public bool DoneDescription
        {
            get => _DoneDescription;
            protected set => _DoneDescription = value;
        }

        public static string GetRegalTitle(GameObject LunarRegent)
        {
            string regalTerm = "Regent";
            if (LunarRegent?.GetGender() is Gender regentGender)
            {
                if (regentGender.Name.ToLower().Contains("male"))
                    regalTerm = "King";

                if (regentGender.Name.ToLower().Contains("female"))
                    regalTerm = "Queen";

                if (regentGender.Plural)
                    regalTerm = regalTerm.Pluralize();
            }
            return $"Moon {regalTerm}";
        }

        public string GetRegalTitle()
            => GetRegalTitle(ParentObject)
            ;

        public void Onset()
        {
            ParentObject.ApplyEffect(new UD_Bones_MoonKingFever());
        }

        public override void Attach()
        {
            base.Attach();
            ParentObject.SetStringProperty(Const.IS_MAD_PROP, $"{IsMad}");

            if (BonesID != The.Game.GameID
                && !OriginalShortDesc.IsNullOrEmpty())
            {
                if (ParentObject.TryGetPart(out Description description)
                    && description._Short == OriginalShortDesc)
                    DoneDescription = false;
            }
        }

        public override void Initialize()
        {
            base.Initialize();

            ParentObject.SetStringProperty(Const.IS_MAD_PROP, $"{IsMad}");
            var bonesColors = ParentObject.RequirePart<UD_Bones_LunarColors>()
                .OverrideBonesID<UD_Bones_LunarColors>(BonesID);
            bonesColors.Persists = true;

            if (ParentObject.TryGetPart(out Description description))
                OriginalShortDesc = description._Short;
            else
                OriginalShortDesc = "It was you.";
        }

        public override bool WantEvent(int ID, int Cascade)
            => base.WantEvent(ID, Cascade)
            || ID == GetDisplayNameEvent.ID
            || ID == GetShortDescriptionEvent.ID
            || ID == EarlyBeforeBeginTakeActionEvent.ID
            || ID == LunarObjectColorChangedEvent.ID
            || ID == GetDebugInternalsEvent.ID
            ;

        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            E.AddHonorific($"=LunarShader:{GetRegalTitle()}:{ParentObject.BaseID}=".StartReplace().ToString());
            if (IsMad)
                E.AddHonorific("mad", DescriptionBuilder.ORDER_ADJUST_SLIGHTLY_EARLY);
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            if (!DoneDescription
                && BonesID != The.Game?.GameID
                && !OriginalShortDesc.IsNullOrEmpty()
                && ParentObject.TryGetPart(out Description description)
                && description._Short == OriginalShortDesc)
            {
                DoneDescription = true;
                description._Short = OriginalShortDesc.StartReplace().ToString();
            }
            return base.HandleEvent(E);
        }

        public override bool HandleEvent(EarlyBeforeBeginTakeActionEvent E)
        {
            if (!Cremated
                && BonesManager.System != null
                && BonesManager.System.TryGetSaveBonesByID(BonesID, out var bonesInfo))
            {
                // bonesInfo.Cremate();
                Cremated = true;
            }
            if (ParentObject != null
                && !ParentObject.IsPlayer()
                && !ParentObject.HasEffect<UD_Bones_MoonKingFever>())
                Onset();

            if (ParentObject.Render is Render lunarRender
                && !lunarRender.Visible)
                lunarRender.Visible = true;

            return base.HandleEvent(E);
        }

        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            E.AddEntry(this, nameof(Cremated), Cremated);
            E.AddEntry(this, nameof(RegalTitle), RegalTitle);
            E.AddEntry(this, nameof(DoneDescription), DoneDescription);
            return base.HandleEvent(E);
        }
    }
}
