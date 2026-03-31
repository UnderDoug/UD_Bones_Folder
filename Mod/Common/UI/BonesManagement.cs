using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Qud.UI;

using UnityEngine;
using UnityEngine.UI;

using XRL;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.UI;
using XRL.Collections;
using XRL.UI;
using XRL.UI.Framework;

using Event = XRL.UI.Framework.Event;

namespace UD_Bones_Folder.Mod.UI
{
    [UIView(BONES_MANAGEMENT_WINDOW_ID,
        NavCategory = "Menu",
        UICanvas = "SaveManagement",
        UICanvasHost = 1)]
    public class BonesManagement
        : SingletonWindowBase<BonesManagement>, ControlManager.IControllerChangedEvent
        // : EmbarkBuilderModuleWindowPrefabBase<BonesManagementModule, FrameworkScroller>, ControlManager.IControllerChangedEvent
    {
        public const string BONES_MANAGEMENT_WINDOW_ID = "UD_BonesFolderManagement";

        public static MenuOption BACK_BUTTON => EmbarkBuilderOverlayWindow.BackMenuOption;

        public Image Background;

        public FrameworkScroller HotkeyBar;

        public FrameworkScroller BonesScroller;

        protected List<FrameworkDataElement> Bones;

        public EmbarkBuilderModuleBackButton BackButton;

        public TaskCompletionSource<SaveBonesInfo> CompletionSource;

        public NavigationContext MainNavContext = new();

        public ScrollContext<NavigationContext> MidHorizNav = new();

        private bool SelectFirst = true;

        public bool WasInScroller;

        protected static void InitializeWithUIManager()
        {
            if (instance == null)
            {
                try
                {
                    var bonesManagementWindow = UIManager
                        .createWindow(
                            name: BONES_MANAGEMENT_WINDOW_ID,
                            scriptType: typeof(BonesManagement)
                        ) as BonesManagement;

                    bonesManagementWindow.Init();
                    bonesManagementWindow.name = "Bones Management";
                }
                catch (Exception x)
                {
                    Utils.Error(new NullReferenceException($"{nameof(UIManager)} didn't like creating a {nameof(BonesManagement)} window", x));
                }
            }
            if (instance == null)
                Utils.Error($"{nameof(BonesManagement)} window failed to initialize", new NullReferenceException($"{nameof(instance)} must not be null"));
        }

        public static bool CheckInit()
        {
            if (instance == null)
                InitializeWithUIManager();

            return instance != null;
        }

        public override void Init()
        {
            base.Init();
            if (Instantiate(SaveManagement.instance.gameObject) is not GameObject saveManagementObject)
                throw new Exception($"Failed to get {nameof(SaveManagement)} game object for cloning.");

            BonesScroller = Instantiate(SaveManagement.instance.savesScroller);
            if (BonesScroller != null)
            {
                SetParentTransform(BonesScroller);
            }
            else
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Init)}", new NullReferenceException($"{nameof(BonesScroller)} must not be null"));

            HotkeyBar = Instantiate(SaveManagement.instance.hotkeyBar);
            if (HotkeyBar != null)
                SetParentTransform(HotkeyBar);
            else
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Init)}", new NullReferenceException($"{nameof(HotkeyBar)} must not be null"));

            BackButton = Instantiate(SaveManagement.instance.backButton);
            if (BackButton != null)
                SetParentTransform(BackButton);
            else
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Init)}", new NullReferenceException($"{nameof(BackButton)} must not be null"));
        }

        public void SetParentTransform(Component Component)
        {
            Component.gameObject.SetActive(value: false);
            Component.transform.SetParent(transform, worldPositionStays: false);
        }

        public void SetupContext()
        {
            Utils.Log($"{nameof(BonesManagement)}.{nameof(SetupContext)}");
            MainNavContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            MainNavContext.buttonHandlers.Set(InputButtonTypes.CancelButton, Event.Helpers.Handle(Exit));

            Background = SaveManagement.instance.background;

            MidHorizNav.SetAxis(InputAxisTypes.NavigationXAxis);
            MidHorizNav.contexts.Clear();
            MidHorizNav.contexts.Add(BackButton.navigationContext);
            MidHorizNav.contexts.Add(BonesScroller.GetNavigationContext());
            MidHorizNav.Setup();
            MidHorizNav.parentContext = MainNavContext;
        }

        public override void Show()
        {
            /*
            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}");
            if (BonesManager.GetSavedBonesInfoAsync() is not Task<IEnumerable<SaveBonesInfo>> savedBonesInfoTask)
            {
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Show)}: failed to get bonesInfo task");
                CompletionSource?.TrySetResult(null);
                Exit();
                return;
            }

            Task.WaitAll(savedBonesInfoTask);

            if (savedBonesInfoTask.Result is not IEnumerable<SaveBonesInfo> bonesInfos
                || SaveBonesInfosToUIElements(bonesInfos) is not List<SaveInfoData> bareBones
                || bareBones.IsNullOrEmpty()
                || (Bones = new(bareBones)).IsNullOrEmpty())
            {
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Show)}: failed to get choices");
                CompletionSource?.TrySetResult(null);
                Exit();
                return;
            }
            */
            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}");
            if (BonesManager.GetSavedBonesInfoAsync() is not Task<IEnumerable<SaveBonesInfo>> savedBonesInfoTask)
            {
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Show)}: failed to get bonesInfo task");
                CompletionSource?.TrySetResult(null);
                Exit();
                return;
            }

            Task.WaitAll(savedBonesInfoTask);

            if (savedBonesInfoTask.Result is not IEnumerable<SaveBonesInfo> bonesInfos
                || SaveBonesInfosToUIElements(bonesInfos) is not List<BonesInfoData> bareBones
                || bareBones.IsNullOrEmpty()
                || (Bones = new(bareBones)).IsNullOrEmpty())
            {
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Show)}: failed to get choices");
                CompletionSource?.TrySetResult(null);
                Exit();
                return;
            }

            base.Show();

            BackButton.gameObject.SetActive(value: true);
            if (BackButton.navigationContext == null)
                BackButton.Awake();
            BackButton.navigationContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            BackButton.navigationContext.buttonHandlers.Set(InputButtonTypes.AcceptButton, Event.Helpers.Handle(Exit));

            BonesScroller.gameObject.SetActive(value: true);
            BonesScroller.scrollContext.wraps = true;
            BonesScroller.BeforeShow(Bones);

            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, BonesScroller.selectionClones.Count: {BonesScroller.selectionClones?.Count ?? -1}");
            for (int i = 0; i < (BonesScroller.selectionClones?.Count ?? 0); i++)
            {
                Utils.Log($"    {nameof(BonesManagement)}.{nameof(Show)}, BonesScroller.selectionClones {i}");
                if (BonesScroller.selectionClones[i] is FrameworkUnityScrollChild selectionCloneI
                    && Bones[i] is BonesInfoData bonesDataI)
                {
                    /*
                    Utils.Log($"        {nameof(BonesManagement)}.{nameof(Show)}, {nameof(SaveManagementRow)}.{nameof(SaveManagementRow.DestroyImmediate)}");
                    selectionCloneI.gameObject.GetComponent<SaveManagementRow>()?.DestroyImmediate();

                    Utils.Log($"        {nameof(BonesManagement)}.{nameof(Show)}, {nameof(gameObject.AddComponent)}.{nameof(BonesManagementRow)}");
                    Utils.Log($"        {nameof(BonesManagement)}.{nameof(Show)}, {nameof(BonesScroller)}.{nameof(BonesScroller.SetupPrefab)}");
                    if (selectionCloneI.gameObject.AddComponent<BonesManagementRow>() is not BonesManagementRow bonesRow)
                    {
                        Utils.Log($"        {nameof(BonesManagement)}.{nameof(Show)}, {nameof(FrameworkUnityScrollChild)} doesn't want {nameof(BonesManagementRow)}");
                        continue;
                    }
                    var childContext =  BonesScroller.MakeContextFor(data, i);
                    BonesScroller.SetupPrefab(selectionCloneI, childContext, bonesDataI, i);
                    childContext.Setup();

                    Utils.Log($"        {nameof(BonesManagement)}.{nameof(Show)}, {nameof(BonesManagementRow)} set up handlers");

                    Utils.Log($"            {nameof(BonesManagement)}.{nameof(Show)}, {nameof(bonesRow)} {bonesRow != null}");
                    Utils.Log($"            {nameof(BonesManagement)}.{nameof(Show)}, {nameof(bonesRow.DeleteButton)} {bonesRow.DeleteButton != null}");
                    Utils.Log($"            {nameof(BonesManagement)}.{nameof(Show)}, {nameof(bonesRow.DeleteButton.context)} {bonesRow.DeleteButton.context != null}");
                    Utils.Log($"            {nameof(BonesManagement)}.{nameof(Show)}, {nameof(bonesRow.DeleteButton.context.buttonHandlers)} {bonesRow.DeleteButton.context.buttonHandlers != null}");
                    bonesRow.DeleteButton.context.buttonHandlers = BonesManagementRow.DeleteButtonHandler;
                    bonesRow.Context.context.commandHandlers = BonesManagementRow.DeleteCommandHandler;
                    */
                    if (selectionCloneI.gameObject.GetComponent<SaveManagementRow>() is SaveManagementRow saveRow)
                    {
                        saveRow.deleteButton.context.buttonHandlers = BonesManagementRow.DeleteButtonHandler;
                        saveRow.context.context.commandHandlers = BonesManagementRow.DeleteCommandHandler;
                    }
                }
                if (!BonesScroller.spacerClones.IsNullOrEmpty())
                {
                    // BonesScroller.spacerClones[0].
                }
                
            }

            if (SelectFirst)
            {
                Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do SelectFirst");
                SelectFirst = false;
                BonesScroller.scrollContext.selectedPosition = 0;
            }
            else
            if (BonesScroller.scrollContext.selectedPosition >= Bones.Count)
            {
                Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do not SelectFirst");
                BonesScroller.scrollContext.selectedPosition = Math.Max(Bones.Count - 1, 0);
            }

            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}, do BonesScroller.onSelected");
            BonesScroller.onSelected.RemoveAllListeners();
            BonesScroller.onSelected.AddListener(SelectedBones);

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
            MainNavContext.disabled = false;
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
            Utils.Log($"{nameof(BonesManagement)}.{nameof(DisableNavContext)}");
            if (deactivate
                && IsInsideActiveContext(MainNavContext))
                NavigationController.instance.activeContext = null;
            MainNavContext.disabled = true;
        }

        public void SelectedBones(FrameworkDataElement data)
        {
            //Utils.Log($"{nameof(BonesManagement)}.{nameof(SelectedBones)}: {(data as SaveInfoData).SaveGame.Name}");
            Utils.Log($"{nameof(BonesManagement)}.{nameof(SelectedBones)}: {(data as BonesInfoData).BonesInfo.Name}");
            if (data is BonesInfoData bonesData)
                CompletionSource?.TrySetResult(bonesData.BonesInfo);
        }

        public async Task BonesMenu()
        {
            gameObject.SetActive(value: true);
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
                    break;

                try
                {
                    if (bonesInfo.GetBonesJSON() is SaveBonesJSON json
                        && await bonesInfo.TryRestoreModsAsync())
                    {
                        Hide();
                        return;
                    }
                }
                catch (Exception x)
                {
                    Utils.Error("Bones Menu", x);
                }
            }
            Hide();
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

            if (Bones[BonesScroller.selectedPosition] is not BonesInfoData bonesData
                || bonesData.BonesInfo is not SaveBonesInfo bonesInfo)
                return;

            List<QudMenuItem> buttons = PopupMessage.AcceptCancelButtonWithoutHotkey;
            if (CapabilityManager.CurrentPlatformClassification() != CapabilityManager.PlatformClassification.PC)
                buttons = PopupMessage.AcceptCancelButton;

            string title = $"Delete {bonesInfo.Name}".WithColor("R");
            if ((await Popup.NewPopupMessageAsync(
                    message: $"Are you sure you want to cremate {bonesInfo.Name}'s bones?",
                    buttons: buttons,
                    title: title,
                    DefaultSelected: 1)
                ).command != "Cancel")
            {
                DisableNavContext();

                bonesInfo.Cremate();

                EnableNavContext();

                await Popup.NewPopupMessageAsync("Bones Cremated!", PopupMessage.AcceptButton);

                Bones = new(SaveBonesInfosToUIElements(await BonesManager.GetSavedBonesInfoAsync()));
                if (Bones.Count == 0)
                    Exit();
                else
                    Show();
            }
        }

        public void UpdateMenuBars()
        {
            // HotkeyBar.gameObject.SetActive(value: true);
            HotkeyBar.GetNavigationContext().disabled = true;
            HotkeyBar.BeforeShow(new List<MenuOption>
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
            });
        }

        public void Update()
        {
            if (MainNavContext.IsActive()
                && IsInsideActiveContext(BonesScroller.GetNavigationContext()) != WasInScroller)
                UpdateMenuBars();
        }

        public void ControllerChanged()
            => UpdateMenuBars()
            ;

        /*
        public static IEnumerable<SaveInfoData> SaveBonesInfosToUIElements(IEnumerable<SaveBonesInfo> SaveGameInfoList)
            => !SaveGameInfoList.IsNullOrEmpty()
            ? SaveGameInfoList.Aggregate(
                seed: new List<SaveInfoData>(),
                func: delegate (List<SaveInfoData> accumulator, SaveBonesInfo next)
                {
                    accumulator.Add(
                        item: new SaveInfoData
                        { 
                            SaveGame = next,
                        });
                    return accumulator;
                })
            : new()
            ;
        */
        public static IEnumerable<BonesInfoData> SaveBonesInfosToUIElements(IEnumerable<SaveBonesInfo> SaveGameInfoList)
            => !SaveGameInfoList.IsNullOrEmpty()
            ? SaveGameInfoList.Aggregate(
                seed: new List<BonesInfoData>(),
                func: delegate (List<BonesInfoData> accumulator, SaveBonesInfo next)
                {
                    accumulator.Add(
                        item: new BonesInfoData
                        { 
                            BonesInfo = next,
                        });
                    return accumulator;
                })
            : new()
            ;
    }
}
