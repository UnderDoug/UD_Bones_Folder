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
    public class BonesManagement : SingletonWindowBase<BonesManagement>, ControlManager.IControllerChangedEvent
    {
        public const string BONES_MANAGEMENT_WINDOW_ID = "UD_BonesFolderManagement";

        public const string CMD_INSERT = "CmdInsert";

        public static MenuOption BACK_BUTTON => EmbarkBuilderOverlayWindow.BackMenuOption;

        public static List<MenuOption> LegendBarOptions = new List<MenuOption>
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
                Description = "cremate"
            }
        };

        public static List<MenuOption> MenuBarOptions = new List<MenuOption>
        {
            new MenuOption
            {
                InputCommand = "CmdInsert",
                Description = "cremate all"
            },
        };

        public static bool WishContext;

        public SaveBonesInfo Preselected;

        public Image Background;

        public FrameworkScroller LegendBar;

        public FrameworkScroller AllBonesMenuBar;

        public FrameworkScroller BonesScroller;

        protected List<FrameworkDataElement> Bones;

        public EmbarkBuilderModuleBackButton BackButton;

        public TaskCompletionSource<SaveBonesInfo> CompletionSource;

        public NavigationContext MainNavContext = new();

        public ScrollContext<NavigationContext> MidHorizNav = new();

        private bool SelectFirst = true;

        public bool WasInScroller;

        public static bool Printed = false;

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
            Utils.Log($"{nameof(BonesManagement)}.{nameof(Init)}");
            base.Init();
            if (Instantiate(SaveManagement.instance.gameObject) is not GameObject saveManagementObject)
                throw new Exception($"Failed to get {nameof(SaveManagement)} game object for cloning.");

            BonesScroller = Instantiate(SaveManagement.instance.savesScroller);
            if (BonesScroller != null)
            {
                SetParentTransform(BonesScroller);

                Background = BonesScroller.gameObject.AddComponent<Image>();
                Background.color = SaveManagement.instance?.background?.color ?? The.Color.Black;
                Background.material = SaveManagement.instance?.background?.material;
            }
            else
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Init)}", new NullReferenceException($"{nameof(BonesScroller)} must not be null"));

            BackButton = Instantiate(SaveManagement.instance.backButton);
            if (BackButton != null)
                SetParentTransform(BackButton);
            else
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Init)}", new NullReferenceException($"{nameof(BackButton)} must not be null"));

            if (BonesScroller.GetComponentsInChildren<FrameworkScroller>() is FrameworkScroller[] frameworkScrollers
                && frameworkScrollers.Length > 1)
                LegendBar = frameworkScrollers[1];

            if (BonesScroller.GetComponentsInChildren<UITextSkin>() is UITextSkin[] uITextSkins)
                foreach (var uITextSkin in uITextSkins)
                    if (uITextSkin.name == "header")
                        uITextSkin.SetText("{{W|MANAGE BONES}}");

            // AllBonesMenuBar = Instantiate(UIManager.getWindow<EmbarkBuilderOverlayWindow>("Chargen/Overlay").menuBar);
            AllBonesMenuBar = Instantiate(LegendBar);
            SetParentTransform(AllBonesMenuBar, LegendBar.transform.parent);

            AllBonesMenuBar.gameObject.LogComponentTree($"{Utils.CallChain(nameof(AllBonesMenuBar), nameof(AllBonesMenuBar.gameObject))} {AllBonesMenuBar.gameObject.name}");
            var menuBar = Instantiate(UIManager.getWindow<EmbarkBuilderOverlayWindow>("Chargen/Overlay").menuBar);
            menuBar.gameObject.LogComponentTree($"{Utils.CallChain(nameof(EmbarkBuilderOverlayWindow), nameof(menuBar), nameof(menuBar.gameObject))} {menuBar.gameObject.name}");

            /*
            // if (AllBonesMenuBar.transform.Find())
            if (menuBar.transform.Find("KeyMenuOption") is RectTransform keyMenuOption)
            {

            }*/
            /*
            if (AllBonesMenuBar.GetComponentInChildren<LayoutElement>() is LayoutElement allBonesLayout
                && LegendBar.GetComponentInChildren<LayoutElement>() is LayoutElement legendLayout)
            {
                allBonesLayout.preferredWidth = legendLayout.preferredWidth;
                allBonesLayout.minWidth = legendLayout.minWidth;
                allBonesLayout.preferredHeight = legendLayout.preferredHeight;
                allBonesLayout.minHeight = legendLayout.minHeight;
            }
            */

            LegendBar.transform.SetAsLastSibling();
            
            if (AllBonesMenuBar.GetComponent<RectTransform>() is RectTransform allBonesRectTransform)
                allBonesRectTransform.Translate(0, allBonesRectTransform.rect.y * -0.5f, 0);

            if (LegendBar.GetComponent<RectTransform>() is RectTransform hotkeyBarRectTransform)
                hotkeyBarRectTransform.Translate(0, hotkeyBarRectTransform.rect.y * 0.5f, 0);

            /*
            instance?.gameObject.PrintComponents(Utils.CallChain(nameof(BonesManagement), nameof(Init)));
            SaveManagement.instance?.gameObject.PrintComponents(Utils.CallChain(nameof(SaveManagement), nameof(instance)));
            Utils.Log("=".ThisManyTimes(45));

            BonesScroller?.gameObject.PrintComponents(Utils.CallChain(nameof(BonesManagement), nameof(Init), nameof(BonesScroller)));
            SaveManagement.instance?.savesScroller?.gameObject.PrintComponents(Utils.CallChain(nameof(SaveManagement), nameof(instance), nameof(SaveManagement.instance.savesScroller)));
            Utils.Log("=".ThisManyTimes(45));

            HotkeyBar?.gameObject.PrintComponents(Utils.CallChain(nameof(BonesManagement), nameof(Init), nameof(HotkeyBar)));
            SaveManagement.instance?.hotkeyBar?.gameObject.PrintComponents(Utils.CallChain(nameof(SaveManagement), nameof(instance), nameof(SaveManagement.instance.hotkeyBar)));
            Utils.Log("=".ThisManyTimes(45));
            */
        }

        public void SetParentTransform(Component Component, Transform Transform = null)
        {
            Transform ??= transform;
            Component.gameObject.SetActive(value: false);
            Component.transform.SetParent(Transform, worldPositionStays: false);
        }

        public void SetupContext()
        {
            Utils.Log($"{nameof(BonesManagement)}.{nameof(SetupContext)}");
            MainNavContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            MainNavContext.buttonHandlers.Set(InputButtonTypes.CancelButton, Event.Helpers.Handle(Exit));

            MidHorizNav.SetAxis(InputAxisTypes.NavigationXAxis);
            MidHorizNav.contexts.Clear();
            MidHorizNav.contexts.Add(BackButton.navigationContext);
            MidHorizNav.contexts.Add(BonesScroller.GetNavigationContext());
            MidHorizNav.Setup();
            MidHorizNav.parentContext = MainNavContext;

            AllBonesMenuBar.GetNavigationContext().parentContext = MidHorizNav;
            LegendBar.GetNavigationContext().parentContext = MidHorizNav;
        }

        public override void Show()
        {
            if (!Printed
                && Printed)
            {
                Printed = true;
                Utils.Log("#".ThisManyTimes(45));
                if (BonesScroller.GetComponentsInChildren<Component>() is Component[] components)
                    foreach (var component in components)
                        component.gameObject.PrintComponents($"{component.GetType()}|{component.gameObject.name}: ");
                // instance.gameObject.LogComponentTree();
                Utils.Log("#".ThisManyTimes(45));
            }

            Utils.Log($"{nameof(BonesManagement)}.{nameof(Show)}");
            if (BonesManager.GetSaveBonesInfoAsync() is not Task<IEnumerable<SaveBonesInfo>> savedBonesInfoTask)
            {
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Show)}: failed to get bonesInfo task");
                CompletionSource?.TrySetResult(null);
                Exit();
                return;
            }

            Task.WaitAll(savedBonesInfoTask);

            if (savedBonesInfoTask.Result is not IEnumerable<SaveBonesInfo> bonesInfos
                || bonesInfos.OrderBy(bones => bones, SaveBonesInfo.SaveBonesInfoComparerDescending).AsEnumerable() is not IEnumerable<SaveBonesInfo> orderedBonesInfos
                || SaveBonesInfosToUIElements(orderedBonesInfos) is not List<BonesInfoData> bareBones
                || bareBones.IsNullOrEmpty()
                || (Bones = new(bareBones)).IsNullOrEmpty())
            {
                Utils.Error($"{nameof(BonesManagement)}.{nameof(Show)}: failed to get choices");
                CompletionSource?.TrySetResult(null);
                Exit();
                return;
            }

            base.Show();

            MainNavContext.commandHandlers ??= new();
            MainNavContext.commandHandlers.Set(CMD_INSERT, Event.Helpers.Handle(HandleDeleteAll));

            BackButton.gameObject.SetActive(value: true);
            if (BackButton.navigationContext == null)
                BackButton.Awake();
            BackButton.navigationContext.buttonHandlers = new Dictionary<InputButtonTypes, Action>();
            BackButton.navigationContext.buttonHandlers.Set(InputButtonTypes.AcceptButton, Event.Helpers.Handle(Exit));

            BonesScroller.gameObject.SetActive(value: true);
            BonesScroller.scrollContext.wraps = true;
            BonesScroller.BeforeShow(Bones);

            for (int i = 0; i < (BonesScroller.selectionClones?.Count ?? 0); i++)
            {
                if (BonesScroller.selectionClones[i] is FrameworkUnityScrollChild selectionCloneI
                    && Bones[i] is BonesInfoData bonesDataI
                    && selectionCloneI.gameObject.GetComponent<SaveManagementRow>() is SaveManagementRow saveRow)
                {
                    saveRow.setBonesData(bonesDataI);
                    saveRow.deleteButton.context.buttonHandlers = BonesManagementRow.ButtonHandlers;
                    saveRow.context.context.commandHandlers = BonesManagementRow.CommandHandlers;
                }
                
            }

            if (Preselected != null
                || MainMenuBones.ReturnToBones != null)
            {
                Preselected ??= MainMenuBones.ReturnToBones;
                BonesScroller.scrollContext.selectedPosition = Math.Clamp(Bones.FindIndex(e => e is BonesInfoData bonesData && bonesData.BonesInfo.ID == Preselected.ID), 0, Bones.Count - 1);
            }
            else
            if (SelectFirst)
            {
                SelectFirst = false;
                BonesScroller.scrollContext.selectedPosition = 0;
            }
            else
            if (BonesScroller.scrollContext.selectedPosition >= Bones.Count)
                BonesScroller.scrollContext.selectedPosition = Math.Max(Bones.Count - 1, 0);

            BonesScroller.onSelected.RemoveAllListeners();
            BonesScroller.onSelected.AddListener(SelectedBones);

            AllBonesMenuBar.onSelected ??= new();
            AllBonesMenuBar.onSelected.RemoveAllListeners();
            AllBonesMenuBar.onSelected.AddListener(SelectedAllBones);

            AllBonesMenuBar.onHighlight ??= new();
            AllBonesMenuBar.onHighlight.RemoveAllListeners();
            AllBonesMenuBar.onHighlight.AddListener(HighlightedAllBones);

            SetupContext();
            EnableNavContext();
            UpdateLegendBar();
            UpdateMenuBars();
        }

        public override void Hide()
        {
            base.Hide();
            DisableNavContext();
            gameObject.SetActive(value: false);
            Printed = false;
        }

        public void EnableNavContext()
        {
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

        public void DisableNavContext(bool deactivate)
        {
            if (deactivate
                && IsInsideActiveContext(MainNavContext))
                NavigationController.instance.activeContext = null;
            MainNavContext.disabled = true;
        }

        public void DisableNavContext()
            => DisableNavContext(true);

        public void SelectedBones(FrameworkDataElement data)
        {
            if (data is BonesInfoData bonesData)
                CompletionSource?.TrySetResult(bonesData.BonesInfo);
        }

        public void SelectedAllBones(FrameworkDataElement data)
        {
            if (data is MenuOption menuData)
            {
                if (menuData.InputCommand == CMD_INSERT)
                    HandleDeleteAll();
            }
        }

        public void HighlightedAllBones(FrameworkDataElement data)
        {
            if (data is MenuOption menuData)
            {
                if (menuData.InputCommand == CMD_INSERT
                    && !menuData.Description.StartsWith("{{red|"))
                    menuData.Description = menuData.Description.Colored("red");
                else
                if (menuData.InputCommand != CMD_INSERT
                    && menuData.Description.StartsWith("{{red|"))
                    menuData.Description = menuData.Description.Strip();
            }
        }

        public async Task<SaveBonesInfo> BonesMenu()
        {
            gameObject.SetActive(value: true);

            SelectFirst = Preselected == null
                && MainMenuBones.ReturnToBones == null;

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
                    if (bonesInfo.GetBonesJSON() is not SaveBonesJSON json)
                    {
                        await Popup.ShowAsync("That bones file appears to be missing an info file, or the info file has been lost during load." +
                            $"\n\nPlease contact {Utils.AuthorOnPlatforms} to see if the file can be recovered.");
                    }
                    else
                    if (json.SaveVersion < 400)
                    {
                        await Popup.ShowAsync("That bones file has an impossibly low save version and has either been modified haphardly or did something goofy while saving." +
                            $"\n\nPlease contact {Utils.AuthorOnPlatforms} to see if the file can be recovered.");
                    }
                    else
                    if (await bonesInfo.TryRestoreModsAsync())
                    {
                        Hide();
                        return bonesInfo;
                    }
                }
                catch (Exception x)
                {
                    Utils.Error("Bones Menu", x);
                }
            }
            Hide();
            return null;
        }

        public void Exit()
        {
            Printed = false;
            MetricsManager.LogEditorInfo("Exiting bones screen");
            CompletionSource?.TrySetResult(null);
            ControlManager.ResetInput();
        }

        public async void HandleDeleteAll()
        {
            if (await BonesManager.CremateAllMoonKings(DisableNavContext, EnableNavContext) is IEnumerable<SaveBonesInfo> currentBonesInfos)
            {
                Bones ??= new();
                Bones.Clear();
                if (currentBonesInfos.IsNullOrEmpty()
                    || SaveBonesInfosToUIElements(currentBonesInfos) is not IEnumerable<BonesInfoData> bareBones
                    || !bareBones.IsNullOrEmpty())
                    Exit();
                else
                {
                    Bones.AddRange(bareBones);
                    Show();
                }
            }

            if (int.TryParse("1", out int result) && result > 1)
            {
                List<QudMenuItem> buttons = PopupMessage.AcceptCancelButtonWithoutHotkey;
                if (CapabilityManager.CurrentPlatformClassification() != CapabilityManager.PlatformClassification.PC)
                    buttons = PopupMessage.AcceptCancelButton;

                string title = $"Cremate All".Colored("R");
                string confirmText = "CREMATE";
                string typeToConfirmText = "\n\nType '" + confirmText + "' to confirm.";
                string defaultValue = string.Empty;
                if (CapabilityManager.CurrentPlatformClassification() == CapabilityManager.PlatformClassification.Console)
                {
                    typeToConfirmText = string.Empty;
                    defaultValue = null;
                }
                if ((await Popup.AskStringAsync(
                        Message: "Are you sure you want to cremate {{red|all}} bones?" + typeToConfirmText,
                        Default: defaultValue,
                        WantsSpecificPrompt: confirmText,
                        MaxLength: confirmText.Length)
                    ) == confirmText)
                {
                    DisableNavContext();

                    int countBefore = Bones.Count;
                    int paddingAmount = countBefore.ToString().Length;
                    int cremateCounter = 0;
                    int crematedCounter = 0;
                    foreach (var frameworkDataElement in Bones)
                    {
                        cremateCounter++;
                        if (frameworkDataElement is not BonesInfoData bonesData
                            || bonesData.BonesInfo is not SaveBonesInfo bonesInfo)
                            continue;

                        Loading.SetLoadingStatus($"Cremating {cremateCounter.ToString().PadLeft(paddingAmount, '0')}/{countBefore} " +
                            $":: {bonesInfo.Name.Strip()}");

                        crematedCounter++;
                        bonesInfo.Cremate();
                    }

                    EnableNavContext();

                    var comparer = SaveBonesInfo.SaveBonesInfoComparerDescending;
                    if (await BonesManager.GetSaveBonesInfoAsync() is IEnumerable<SaveBonesInfo> bonesInfos
                        && bonesInfos.OrderBy(bones => bones, comparer).AsEnumerable() is IEnumerable<SaveBonesInfo> orderedBonesInfos
                        && SaveBonesInfosToUIElements(orderedBonesInfos) is IEnumerable<BonesInfoData> bareBones
                        && !bareBones.IsNullOrEmpty())
                        Bones = new(bareBones);
                    else
                        Bones = new();

                    string crematedString = crematedCounter.ToString();
                    if (crematedCounter != countBefore)
                        crematedString = crematedString.Colored("red");

                    string somethingWrongString = !Bones.IsNullOrEmpty() ? "\n\n{{K|(something went wrong)}}" : null;
                    await Popup.NewPopupMessageAsync($"{crematedString}/{countBefore} Bones Cremated!{somethingWrongString}", PopupMessage.AcceptButton);

                    Loading.SetLoadingStatus(null);

                    if (Bones.IsNullOrEmpty())
                        Exit();
                    else
                        Show();
                }
            }
        }

        public static Task HandleDeleteAllTask()
            => Task.Run(() =>
            {
                if (CheckInit())
                    instance?.HandleDeleteAll();
            });

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

            string title = $"Cremate {bonesInfo.Name}".Colored("R");
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

                if (await BonesManager.GetSaveBonesInfoAsync() is not IEnumerable<SaveBonesInfo> bonesInfos
                    || bonesInfos.OrderBy(bones => bones, SaveBonesInfo.SaveBonesInfoComparerDescending).AsEnumerable() is not IEnumerable<SaveBonesInfo> orderedBonesInfos
                    || SaveBonesInfosToUIElements(orderedBonesInfos) is not IEnumerable<BonesInfoData> bareBones
                    || bareBones.IsNullOrEmpty()
                    || (Bones = new(bareBones)).IsNullOrEmpty())
                    Exit();
                else
                    Show();
            }
        }

        public void UpdateLegendBar()
        {
            // HotkeyBar.gameObject.SetActive(value: true);
            LegendBar.GetNavigationContext().disabled = true;
            LegendBar.BeforeShow(LegendBarOptions);
        }

        public void UpdateMenuBars()
        {
            if (!AllBonesMenuBar.gameObject.activeSelf)
                AllBonesMenuBar.gameObject.SetActive(value: true);
            AllBonesMenuBar.GetNavigationContext().disabled = false;
            AllBonesMenuBar.BeforeShow(MenuBarOptions);
            /*
            foreach (var selectionClone in AllBonesMenuBar.selectionClones ?? Enumerable.Empty<FrameworkUnityScrollChild>())
            {
                if (selectionClone.gameObject.GetComponent<HasSelectionCaret>() == null)
                    selectionClone.gameObject.AddComponent<HasSelectionCaret>();
            }
            */
        }

        public void Update()
        {
            if (MainNavContext.IsActive()
                && IsInsideActiveContext(BonesScroller.GetNavigationContext()) != WasInScroller)
                UpdateLegendBar();
        }

        public void ControllerChanged()
        {
            UpdateLegendBar();
            UpdateMenuBars();
        }

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
