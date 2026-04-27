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
using System;
using Event = XRL.UI.Framework.Event;

namespace UD_Bones_Folder.Mod.UI
{
    public class BonesManagementRow : MonoBehaviour, IFrameworkControl
    {
        public static ImageTinyFrame _imageTinyFrame;
        public static ImageTinyFrame imageTinyFrame => _imageTinyFrame ??= Instantiate(SaveManagement.instance?.savesScroller?.selectionPrefab?.GetComponent<SaveManagementRow>()?.imageTinyFrame);

        public static Dictionary<InputButtonTypes, Action> ButtonHandler => new()
        {
            { InputButtonTypes.AcceptButton, Event.Helpers.Handle(BonesManagement.instance.HandleDelete) },
        };
        public static Dictionary<string, Action> CommandHandlers => new()
        {
            { "CmdDelete", Event.Helpers.Handle(BonesManagement.instance.HandleDelete) },
        };

        public ImageTinyFrame ImageTinyFrame;

        public List<UITextSkin> TextSkins;

        public Image background;

        public GameObject ModsDiffer;

        public FrameworkContext DeleteButton;

        private bool? WasSelected;

        private FrameworkContext _Context;
        public FrameworkContext Context => _Context ??= GetComponent<FrameworkContext>();

        public bool Invalid;

        public void setData(FrameworkDataElement data)
        {
            if (data is not BonesInfoData bonesData)
            {
                Invalid = true;
                //Utils.Log($"{1.Indent()}{nameof(BonesManagementRow)}.{nameof(setData)} {nameof(Invalid)}: {Invalid} ({nameof(data)})");
                return;
            }

            Invalid = false;
            //Utils.Log($"{1.Indent()}{nameof(BonesManagementRow)}.{nameof(setData)} {nameof(Invalid)}: {Invalid}");

            DeleteButton ??= Instantiate(SaveManagement.instance?.savesScroller?.selectionPrefab?.GetComponent<SaveManagementRow>()?.deleteButton);
            DeleteButton.RequireContext<NavigationContext>().parentContext = Context.context;

            var bonesInfo = bonesData.BonesInfo;
            var bonesJSON = bonesInfo?.GetBonesJSON();
            if (Instantiate(BonesManagementRow.imageTinyFrame) is not ImageTinyFrame imageTinyFrame)
            {
                Invalid = true;
                //Utils.Log($"{1.Indent()}{nameof(BonesManagementRow)}.{nameof(setData)} {nameof(Invalid)}: {Invalid} ({nameof(ImageTinyFrame)})");
                return;
            }

            ImageTinyFrame ??= imageTinyFrame;
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

            if (TextSkins.IsNullOrEmpty())
            {
                TextSkins = new();
                if (SaveManagement.instance?.savesScroller?.selectionPrefab?.GetComponent<SaveManagementRow>()?.TextSkins is not List<UITextSkin> textSkins)
                {
                    Invalid = true;
                    //Utils.Log($"{1.Indent()}{nameof(BonesManagementRow)}.{nameof(setData)} {nameof(Invalid)}: {Invalid} ({nameof(SaveManagement)}.{nameof(TextSkins)})");
                    return;
                }
                foreach (var textSkin in textSkins)
                {
                    TextSkins.Add(Instantiate(textSkin));
                }
            }
            if (TextSkins.IsNullOrEmpty())
            {
                Invalid = true;
                //Utils.Log($"{1.Indent()}{nameof(BonesManagementRow)}.{nameof(setData)} {nameof(Invalid)}: {Invalid} ({nameof(TextSkins)})");
                return;
            }

            TextSkins[0].SetText($"{bonesInfo.Name}::{bonesInfo.Description}".Colored("W"));
            TextSkins[1].SetText($"{"Location:".Colored("C")} {bonesInfo.Info}");
            TextSkins[2].SetText($"{"Last saved:".Colored("C")} {bonesInfo.SaveTime}");
            TextSkins[3].SetText($"{bonesInfo.Size} {{{bonesInfo.ID}}}".Colored("K"));
            ModsDiffer ??= Instantiate(SaveManagement.instance?.savesScroller?.selectionPrefab?.GetComponent<SaveManagementRow>()?.modsDiffer);
            ModsDiffer.transform.SetParent(transform, worldPositionStays: false);
            ModsDiffer.SetActive(value: bonesInfo.DifferentMods());
            ModsDiffer.PrintComponents();
            WasSelected = null;
            Update();
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
                background ??= Instantiate(SaveManagement.instance?.savesScroller?.selectionPrefab?.GetComponent<SaveManagementRow>()?.background);
                background.color = darkCyan;
                bool first = true;

                if (TextSkins.IsNullOrEmpty())
                {
                    TextSkins = new();
                    if (SaveManagement.instance?.savesScroller?.selectionPrefab?.GetComponent<SaveManagementRow>()?.TextSkins is not List<UITextSkin> textSkins)
                    {
                        Invalid = true;
                        //Utils.Log($"{1.Indent()}{nameof(BonesManagementRow)}.{nameof(Update)} {nameof(Invalid)}: {Invalid} ({nameof(SaveManagement)}.{nameof(TextSkins)})");
                        return;
                    }
                    foreach (var textSkin in textSkins)
                    {
                        TextSkins.Add(Instantiate(textSkin));
                    }
                }

                if (TextSkins.IsNullOrEmpty())
                {
                    Invalid = true;
                    //Utils.Log($"{1.Indent()}{nameof(BonesManagementRow)}.{nameof(Update)} {nameof(Invalid)}: {Invalid} ({nameof(TextSkins)}2)");
                    return;
                }

                if (Invalid)
                    return;

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
