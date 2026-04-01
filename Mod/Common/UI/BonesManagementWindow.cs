using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Qud.UI;

using UnityEngine.UI;

using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.UI;
using XRL.Collections;
using XRL.UI;
using XRL.UI.Framework;
using XRL.World;

using Event = XRL.UI.Framework.Event;

namespace UD_Bones_Folder.Mod.UI
{
    [UIView("MainMenu:UD_BonesFolderManagement",
        NavCategory = "Chargen",
        UICanvas = "SaveManagement",
        UICanvasHost = 1)]
    public class BonesManagementWindow : EmbarkBuilderModuleWindowPrefabBase<BonesManagementModule, FrameworkScroller>
    {
        // don't remove this. It's what allows the first call to UpdateControls() to actually update the controls.
        public EmbarkBuilderModuleWindowDescriptor windowDescriptor;

        public TaskCompletionSource<SaveBonesInfo> CompletionSource;

        private bool SelectFirst = true;

        public bool WasInScroller;

        public bool TrySetupModuleData()
        {
            if (module?.data == null)
            {
                module?.setData(module?.DefaultData);
                return true;
            }
            return false;
        }
        public override void BeforeShow(EmbarkBuilderModuleWindowDescriptor descriptor)
        {
            if (descriptor != null)
                windowDescriptor = descriptor;

            // TrySetupModuleData();

            prefabComponent.onSelected.RemoveAllListeners();
            prefabComponent.onSelected.AddListener(SelectedInfo);

            GetOverlayWindow().nextButton.gameObject.SetActive(value: false);

            UpdateControls();

            if (prefabComponent.choices.Count == 0)
            {
                CompletionSource.TrySetResult(null);
                Exit();
                return;
            }

            if (SelectFirst)
            {
                Utils.Log($"{nameof(BonesManagementWindow)}.{nameof(Show)}, do SelectFirst");
                SelectFirst = false;
                prefabComponent.scrollContext.selectedPosition = 0;
            }
            else
            if (prefabComponent.scrollContext.selectedPosition >= prefabComponent.choices.Count)
            {
                Utils.Log($"{nameof(BonesManagementWindow)}.{nameof(Show)}, do not SelectFirst");
                prefabComponent.scrollContext.selectedPosition = Math.Max(prefabComponent.choices.Count - 1, 0);
            }
            
            EnableNavContext();

            base.BeforeShow(descriptor);
        }

        public override void Hide()
        {
            base.Hide();
            DisableNavContext();
            gameObject.SetActive(value: false);
        }

        public override UIBreadcrumb GetBreadcrumb()
            => new()
            {
                Id = GetType().FullName,
                Title = "Manage Bones",
                IconPath = "Items/sw_bones_1.bmp,Items/sw_bones_2.bmp,Items/sw_bones_3.bmp,Items/sw_bones_4.bmp,Items/sw_bones_5.bmp,Items/sw_bones_6.bmp,Items/sw_bones_7.bmp,Items/sw_bones_8.bmp".CachedCommaExpansion().GetRandomElementCosmetic(),
                HFlip = false,
                IconDetailColor = The.Color.White,
                IconForegroundColor = The.Color.Black,
            };

        public void EnableNavContext()
        {
            /*
            Utils.Log($"{nameof(BonesManagementWindow)}.{nameof(EnableNavContext)}");
            if (prefabComponent.GetNavigationContext() is NavigationContext navContext)
            {
                navContext.disabled = false;
                navContext.ActivateAndEnable();
            }
            */
        }

        public bool IsInsideActiveContext(NavigationContext NavigationContext)
        {
            if (NavigationController.instance is not NavigationController navController)
                return false;

            if (navController.activeContext is not NavigationContext activeContext)
                return false;

            return activeContext.IsInside(NavigationContext);
        }

        public void DisableNavContext(bool deactivate = true)
        {
            /*
            if (deactivate
                && prefabComponent.GetNavigationContext() is NavigationContext navContext
                && IsInsideActiveContext(navContext))
                {
                    NavigationController.instance.activeContext = null;
                    navContext.disabled = true;
                }
            */
        }

        public void SelectedInfo(FrameworkDataElement data)
        {
            if (data is BonesInfoData bonesData)
                CompletionSource?.TrySetResult(bonesData.BonesInfo);
        }

        public async Task BonesMenu()
        {
            SelectFirst = true;
            while (true)
            {
                Utils.Log($"{nameof(BonesManagementWindow)}.{nameof(BonesMenu)}");
                CompletionSource?.TrySetCanceled();
                Utils.Log($"{nameof(BonesManagementWindow)}.{nameof(BonesMenu)} CompletionSource?.TrySetCanceled();");
                CompletionSource = new TaskCompletionSource<SaveBonesInfo>();
                Utils.Log($"{nameof(BonesManagementWindow)}.{nameof(BonesMenu)} CompletionSource = new TaskCompletionSource<SaveBonesInfo>();");

                await The.UiContext;
                Utils.Log($"{nameof(BonesManagementWindow)}.{nameof(BonesMenu)} await The.UiContext;");
                ControlManager.ResetInput();
                Show();
                var bonesInfo = await CompletionSource.Task;
                //DisableNavContext();

                await The.UiContext;

                if (bonesInfo == null)
                {
                    break;
                }
                try
                {
                    if (bonesInfo.GetBonesJSON() is SaveBonesJSON json)
                    {
                        if (await bonesInfo.TryRestoreModsAsync())
                        {
                            Hide();
                            return;
                        }
                    }
                }
                catch (Exception x)
                {
                    Utils.Error("Bones Menu", x);
                }
            }
            Hide();
            return;
        }

        public void Exit()
        {
            MetricsManager.LogEditorInfo("Exiting bones screen");
            CompletionSource?.TrySetResult(null);
            ControlManager.ResetInput();
        }

        public async void HandleDelete()
        {
            if (!IsInsideActiveContext(prefabComponent.GetNavigationContext()))
                return;

            if (prefabComponent.choices[prefabComponent.selectedPosition] is not BonesInfoData bonesData
                || bonesData.BonesInfo is not SaveBonesInfo bonesInfo)
                return;

            List<QudMenuItem> buttons = PopupMessage.AcceptCancelButtonWithoutHotkey;
            if (CapabilityManager.CurrentPlatformClassification() != CapabilityManager.PlatformClassification.PC)
                buttons = PopupMessage.AcceptCancelButton;

            string title = $"Delete {bonesInfo.Name}".WithColor("R");
            if ((await Popup.NewPopupMessageAsync(
                    message: $"Are you sure you want to delete the save game for {bonesInfo.Name}?",
                    buttons: buttons,
                    title: title,
                    DefaultSelected: 1)
                ).command != "Cancel")
            {
                DisableNavContext();

                bonesInfo.Cremate();

                EnableNavContext();

                await Popup.NewPopupMessageAsync("Bones Deleted!", PopupMessage.AcceptButton);

                UpdateControls();

                if (prefabComponent.choices.Count == 0)
                    Exit();
                else
                    Show();
            }
        }

        public override void HandleMenuOption(MenuOption menuOption)
        {
            if (menuOption.Id == "delete")
                return;

            base.HandleMenuOption(menuOption);
        }

        public override IEnumerable<MenuOption> GetKeyMenuBar()
        {
            yield return new MenuOption
            {
                Id = "delete",
                KeyDescription = ControlManager.getCommandInputDescription("CmdDelete"),
                Description = "delete"
            };
        }

        public void Update()
        {
            /*
            if (prefabComponent.GetNavigationContext().parentContext.IsActive()
                && IsInsideActiveContext(prefabComponent.GetNavigationContext()) != WasInScroller)
                UpdateControls();
            */
        }

        public void ControllerChanged()
            => UpdateControls()
            ;

        public static List<BonesInfoData> SaveBonesInfosToUIElements(IEnumerable<SaveBonesInfo> SaveGameInfoList)
            => SaveGameInfoList.Aggregate(
                seed: new List<BonesInfoData>(),
                func: delegate (List<BonesInfoData> accumulator, SaveBonesInfo next)
                {
                    string tileColor = "&y";
                    if (ColorUtility.ColorMap.TryGetValue(next.json.FColor, out var tileColorColor)
                        && ColorUtility.ColorToCharMap.TryGetValue(tileColorColor, out var tileColorChar))
                        tileColor = $"&{tileColorChar}";
                    char detailColor = 'W';
                    if (ColorUtility.ColorMap.TryGetValue(next.json.FColor, out var detailColorColor)
                        && ColorUtility.ColorToCharMap.TryGetValue(detailColorColor, out var detailColorChar))
                        detailColor = detailColorChar;
                    accumulator.Add(
                        item: new BonesInfoData
                        {
                            BonesInfo = next,
                            /*LongDescription = next.Description,
                            Renderable = new Renderable
                            {
                                Tile = next.json.CharIcon,
                                TileColor = tileColor,
                                DetailColor = detailColor,
                            },*/
                        });
                    return accumulator;
                });

        public void UpdateControls()
        {
            Utils.Log($"{nameof(BonesManagement)}.{nameof(UpdateControls)}");
            var savedGameInfo = BonesManager.GetSavedBonesInfoAsync();
            Task.WaitAll(savedGameInfo);
            var result = new List<SaveBonesInfo>(savedGameInfo.Result);
            var bareBones = SaveBonesInfosToUIElements(result);

            prefabComponent.BeforeShow(windowDescriptor, bareBones);

            /*
            Utils.Log($"{nameof(BonesManagement)}.{nameof(UpdateControls)}, do aggregate bonesManagementRows");
            Utils.Log($"{nameof(BonesManagement)}.{nameof(UpdateControls)}, BonesScroller.selectionClones.Count: {prefabComponent.selectionClones?.Count ?? -1}");
            using var bonesManagementRows = ScopeDisposedList<BonesManagementRow>.GetFromPool();
            prefabComponent.selectionClones.Aggregate(
                seed: bonesManagementRows,
                func: delegate (ScopeDisposedList<BonesManagementRow> accumulator, FrameworkUnityScrollChild next)
                {
                    var bonesManagementRow = next.gameObject.AddComponent<BonesManagementRow>();
                    bonesManagementRow.setData(next.GetComponent<BonesInfoData>());
                    Utils.Log($"    {nameof(BonesManagement)}.{nameof(UpdateControls)}, " +
                        $"BonesScroller.selectionClones: {next.GetComponent<BonesManagementRow>()?.TextSkins?[0]?.text ?? $"straight missing {nameof(BonesInfoData)}"}");
                    bonesManagementRows.Add(next.GetComponent<BonesManagementRow>());
                    return bonesManagementRows;
                });

            Utils.Log($"{nameof(BonesManagement)}.{nameof(UpdateControls)}, do bonesManagementRows");
            foreach (var bonesManagementRow in bonesManagementRows)
            {
                if (bonesManagementRow?.Invalid is false)
                {
                    bonesManagementRow.DeleteButton.context.buttonHandlers = new Dictionary<InputButtonTypes, Action>
                    {
                        { InputButtonTypes.AcceptButton, Event.Helpers.Handle(HandleDelete) },
                    };
                    bonesManagementRow.Context.context.commandHandlers = new Dictionary<string, Action>
                    {
                        { "CmdDelete", Event.Helpers.Handle(HandleDelete) },
                    };
                }
            }
            */
        }
    }
}
