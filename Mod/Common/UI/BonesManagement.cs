using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Qud.UI;

using UnityEngine.UI;

using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.UI;
using XRL.Collections;
using XRL.UI;
using XRL.UI.Framework;

namespace UD_Bones_Folder.Mod.UI
{
    [UIView("UD_BonesFolderManagement",
        NavCategory = "Menu",
        UICanvas = "SaveManagement",
        UICanvasHost = 1)]
    public class BonesManagement
        : SingletonWindowBase<BonesManagement>, ControlManager.IControllerChangedEvent
        // : EmbarkBuilderModuleWindowPrefabBase<BonesManagementModule, FrameworkScroller>, ControlManager.IControllerChangedEvent
    {
        protected List<FrameworkDataElement> Bones
        {
            get => BonesScroller.choices;
            set => BonesScroller.choices = value;
        }

        public Image Background;

        public FrameworkScroller HotkeyBar;

        public FrameworkScroller BonesScroller;

        public EmbarkBuilderModuleBackButton BackButton;

        public TaskCompletionSource<SaveBonesInfo> CompletionSource;

        public NavigationContext NavigationContext = new();

        public ScrollContext<NavigationContext> MidHorizNav = new();

        private bool SelectFirst = true;

        public bool WasInScroller;

        public void SetupContext()
        {
            Utils.Log($"{nameof(BonesManagement)}.{nameof(SetupContext)}");
            NavigationContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            NavigationContext.buttonHandlers.Set(InputButtonTypes.CancelButton, Event.Helpers.Handle(Exit));
            MidHorizNav.SetAxis(InputAxisTypes.NavigationXAxis);
            MidHorizNav.contexts.Clear();
            MidHorizNav.contexts.Add(BackButton.navigationContext);
            MidHorizNav.contexts.Add(BonesScroller.GetNavigationContext());
            MidHorizNav.Setup();
            MidHorizNav.parentContext = NavigationContext;
        }

        public override void Show()
        {
            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}");
            var savedGameInfo = BonesManager.GetSavedBonesInfoAsync();
            Task.WaitAll(savedGameInfo);
            var result = new List<SaveBonesInfo>(savedGameInfo.Result);
            var bareBones = SaveBonesInfosToUIElements(result);
            Bones = new(bareBones);

            if (Bones.Count == 0)
            {
                CompletionSource.TrySetResult(null);
                Exit();
                return;
            }
            /**/
            base.Show();
            /**/

            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do backButton");
            BackButton?.gameObject.SetActive(value: true);
            if (BackButton.navigationContext == null)
                BackButton.Awake();
            BackButton.navigationContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            BackButton.navigationContext.buttonHandlers.Set(InputButtonTypes.AcceptButton, Event.Helpers.Handle(Exit));

            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do BonesScroller");
            BonesScroller.scrollContext.wraps = true;
            BonesScroller.BeforeShow(null, Bones);

            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do aggregate bonesManagementRows");
            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, BonesScroller.selectionClones.Count: {BonesScroller.selectionClones?.Count ?? -1}");
            using var bonesManagementRows = ScopeDisposedList<BonesManagementRow>.GetFromPool();
            BonesScroller.selectionClones.Aggregate(
                seed: bonesManagementRows,
                func: delegate (ScopeDisposedList<BonesManagementRow> accumulator, FrameworkUnityScrollChild next)
                {
                    Utils.Log($"    {nameof(BonesManagement)}.{nameof(Show)}, BonesScroller.selectionClones: {next.GetComponent<BonesManagementRow>().TextSkins[0].text}");
                    bonesManagementRows.Add(next.GetComponent<BonesManagementRow>());
                    return bonesManagementRows;
                });

            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do bonesManagementRows");
            foreach (var bonesManagementRow in bonesManagementRows)
            {
                if (bonesManagementRow != null)
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

            if (SelectFirst)
            {
                Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do SelectFirst");
                SelectFirst = false;
                BonesScroller.scrollContext.selectedPosition = 0;
            }
            else
            if (BonesScroller.scrollContext.selectedPosition >= result.Count)
            {
                Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do not SelectFirst");
                BonesScroller.scrollContext.selectedPosition = Math.Max(result.Count - 1, 0);
            }

            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do BonesScroller.onSelected");
            BonesScroller.onSelected.RemoveAllListeners();
            BonesScroller.onSelected.AddListener(SelectedInfo);

            SetupContext();
            EnableNavContext();
            UpdateMenuBars();
        }

        public override void Hide()
        {
            base.Hide();
            DisableNavContext();
            gameObject.SetActive(value: false);
        }

        public void EnableNavContext()
        {
            Utils.Log($"{nameof(BonesManagement)}.{nameof(EnableNavContext)}");
            NavigationContext.disabled = false;
            BonesScroller.GetNavigationContext().ActivateAndEnable();
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
            if (deactivate
                && IsInsideActiveContext(NavigationContext))
                NavigationController.instance.activeContext = null;
            NavigationContext.disabled = true;
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
                CompletionSource?.TrySetCanceled();
                CompletionSource = new TaskCompletionSource<SaveBonesInfo>();

                await The.UiContext;
                ControlManager.ResetInput();
                Show();
                var bonesInfo = await CompletionSource.Task;
                DisableNavContext();

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
            if (!IsInsideActiveContext(BonesScroller.GetNavigationContext()))
                return;

            var bonesData = Bones[BonesScroller.selectedPosition] as BonesInfoData;
            var bonesInfo = bonesData.BonesInfo;

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

                Bones = new(SaveBonesInfosToUIElements(await BonesManager.GetSavedBonesInfoAsync()));
                if (Bones.Count == 0)
                    Exit();
                else
                    Show();
            }
        }

        public void UpdateMenuBars()
        {
            var list = new List<MenuOption>
            {
                new MenuOption
                {
                    InputCommand = "NavigationXYAxis",
                    Description = "navigate"
                },
                new MenuOption
                {
                    KeyDescription = ControlManager.getCommandInputDescription("Accept"),
                    Description = "select"
                },
                new MenuOption
                {
                    KeyDescription = ControlManager.getCommandInputDescription("CmdDelete"),
                    Description = "delete"
                }
            };
            HotkeyBar.GetNavigationContext().disabled = true;
            HotkeyBar.BeforeShow(null, list);
        }

        public void Update()
        {
            if (NavigationContext.IsActive()
                && IsInsideActiveContext(BonesScroller.GetNavigationContext()) != WasInScroller)
                UpdateMenuBars();
        }

        public void ControllerChanged()
            => UpdateMenuBars()
            ;

        public static List<BonesInfoData> SaveBonesInfosToUIElements(IEnumerable<SaveBonesInfo> SaveGameInfoList)
            => SaveGameInfoList.Aggregate(
                seed: new List<BonesInfoData>(),
                func: delegate (List<BonesInfoData> accumulator, SaveBonesInfo next)
                {
                    accumulator.Add(
                        item: new BonesInfoData
                        { 
                            BonesInfo = next,
                        });
                    return accumulator;
                });
    }
}
