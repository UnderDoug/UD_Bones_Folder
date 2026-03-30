using System.Collections.Generic;

using ColorUtility = ConsoleLib.Console.ColorUtility;

using Kobold;

using Qud.API;
using Qud.UI;

using UnityEngine;
using UnityEngine.UI;

using XRL;
using XRL.UI;
using XRL.UI.Framework;

namespace UD_Bones_Folder.Mod.UI
{
    public class BonesManagementRow : MonoBehaviour, IFrameworkControl
    {
        public ImageTinyFrame ImageTinyFrame;

        public List<UITextSkin> TextSkins;

        public Image background;

        public GameObject ModsDiffer;

        public FrameworkContext DeleteButton;

        private bool? WasSelected;

        private FrameworkContext _Context;
        public FrameworkContext Context => _Context ??= GetComponent<FrameworkContext>();

        public bool Invalid;

        void IFrameworkControl.setData(FrameworkDataElement data)
            => setData(data);

        public bool setData(FrameworkDataElement data)
        {
            if (data is not BonesInfoData bonesData)
            {
                Invalid = true;
                return false;
            }

            Invalid = false;

            DeleteButton ??= new();
            DeleteButton.context ??= new NavigationContext();
            DeleteButton.context.parentContext = Context.context;

            var bonesInfo = bonesData.BonesInfo;
            var bonesJSON = bonesInfo?.GetBonesJSON();
            ImageTinyFrame ??= new();
            string tile = "Text/32.bmp";
            if (bonesJSON != null)
            {
                if (SpriteManager.HasTextureInfo(bonesJSON.CharIcon))
                    tile = bonesJSON.CharIcon;

                ImageTinyFrame.sprite = SpriteManager.GetUnitySprite(tile);
                ImageTinyFrame.unselectedBorderColor = The.Color.Black;
                ImageTinyFrame.selectedBorderColor = The.Color.Yellow;
                ImageTinyFrame.unselectedForegroundColor = The.Color.Black;
                ImageTinyFrame.unselectedDetailColor = The.Color.Black;

                ImageTinyFrame.selectedForegroundColor = The.Color.Gray;
                if (ColorUtility.ColorMap.TryGetValue(bonesJSON.FColor, out var value))
                    ImageTinyFrame.selectedForegroundColor = value;

                ImageTinyFrame.selectedDetailColor = The.Color.DarkBlack;
                if (ColorUtility.ColorMap.TryGetValue(bonesJSON.DColor, out var value2))
                    ImageTinyFrame.selectedDetailColor = value2;
            }
            else
            {
                ImageTinyFrame.sprite = SpriteManager.GetUnitySprite(tile);
                ImageTinyFrame.unselectedBorderColor = The.Color.Black;
                ImageTinyFrame.selectedBorderColor = The.Color.Yellow;
                ImageTinyFrame.unselectedForegroundColor = Color.clear;
                ImageTinyFrame.unselectedDetailColor = Color.clear;
                ImageTinyFrame.selectedForegroundColor = Color.clear;
                ImageTinyFrame.selectedDetailColor = Color.clear;
            }

            if (ImageTinyFrame.ThreeColor)
                ImageTinyFrame.ThreeColor.SetHFlip(Value: true);

            ImageTinyFrame.Sync(force: true);
            TextSkins[0].SetText($"{bonesInfo.Name}::{bonesInfo.Description}".WithColor("W"));
            TextSkins[1].SetText($"{"Location:".WithColor("C")} {bonesInfo.Info}");
            TextSkins[2].SetText($"{"Last saved:".WithColor("C")} {bonesInfo.SaveTime}");
            TextSkins[3].SetText($"{bonesInfo.Size} {{{bonesInfo.ID}}}".WithColor("K"));
            ModsDiffer.SetActive(bonesInfo.DifferentMods());
            WasSelected = null;
            Update();

            return true;
        }

        public void Update()
        {
            if (Invalid)
                return;

            bool isActive = Context?.context?.IsActive() is true;
            if (isActive != WasSelected)
            {
                WasSelected = isActive;
                DeleteButton?.gameObject?.SetActive(isActive);

                var darkCyan = The.Color.DarkCyan;
                darkCyan.a = isActive ? 0.25f : 0f;
                background.color = darkCyan;
                bool first = true;
                foreach (UITextSkin textSkin in TextSkins)
                {
                    if (isActive)
                    {
                        textSkin.color = The.Color.Gray;
                        textSkin.StripFormatting = false;
                    }
                    else
                    {
                        textSkin.color = first ? The.Color.DarkCyan : The.Color.Black;
                        textSkin.StripFormatting = true;
                    }
                    textSkin.Apply();
                    first = false;
                }
            }
            if (isActive
                && ControlManager.GetButtonDown("CmdDelete"))
                DeleteButton?.context.commandHandlers["CmdDelete"]();
        }

        public void handleDelete()
            => DeleteButton?.context?.buttonHandlers[InputButtonTypes.AcceptButton]()
            ;

        public NavigationContext GetNavigationContext()
            => null;

    }
}
