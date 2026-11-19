using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Genkit;

using Qud.UI;

using XRL.Messages;
using XRL.UI;
using XRL.UI.Framework;

namespace UD_Bones_Folder.Mod.UI
{
    public static class UIUtils
    {
        public class SelectionPair<T>
        {
            public T Element;
            public int Selected;
            public int Max = 1;

            public string Label => _GetLabel?.Invoke(Element) ?? Element.ToString();

            public char[] DesiredHotkeys => Label
                .Where(c => char.IsLetterOrDigit(c))
                .Distinct()
                .ToArray()
                ;

            private Func<T, string> _GetLabel;
            public Func<T, string> GetLabel
            {
                set => _GetLabel = value;
            }

            public override string ToString()
                => $"{Selected.GetCheckboxText(Max, Label)}"
                ;

            public void Select(int Amount)
            {
                if (Selected == Max)
                    Selected = 0;
                else
                    Selected = Math.Min(Max, Selected + Amount);
            }

            public void Select()
                => Select(1)
                ;

            public CascadableResult SelectContinue(int Amount)
            {
                if (Selected == Max)
                    Selected = 0;
                else
                    Selected = Math.Min(Max, Selected + Amount);

                return CascadableResult.Continue;
            }

            public CascadableResult SelectContinue()
                => SelectContinue(1)
                ;

            public bool IsSelected()
                => Selected > 0
                ;
        }

        public enum CascadableResult : int
        {
            Continue,
            Back,
            BackSilent,
            Cancel,
            CancelSilent,
        }

        /// <summary>
        /// Returns Option:-2
        /// </summary>
        public static List<QudMenuItem> _BackButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{hotkey|[" + ControlManager.getCommandInputFormatted("V Negative", XRL.UI.Options.ModernUI) + "]}} {{y|Back}}",
                command = "option:-2",
                hotkey = "N,V Negative"
            },
        };

        /// <summary>
        /// Returns Option:-2
        /// </summary>
        public static List<QudMenuItem> _BackancelButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{hotkey|[" + ControlManager.getCommandInputFormatted("Cancel", XRL.UI.Options.ModernUI) + "]}} {{y|Back}}",
                command = "option:-2",
                hotkey = "N,V Negative,Cancel"
            },
        };

        /// <summary>
        /// Returns Option:-1
        /// </summary>
        public static List<QudMenuItem> _ExitButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{hotkey|[" + ControlManager.getCommandInputFormatted("Cancel", XRL.UI.Options.ModernUI) + "]}} {{y|Exit}}",
                command = "option:-1",
                hotkey = "N,V Negative,Cancel"
            },
        };

        /// <summary>
        /// Returns Option:-3
        /// </summary>
        public static List<QudMenuItem> _SaveButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{hotkey|[" + ControlManager.getCommandInputFormatted("Accept", XRL.UI.Options.ModernUI) + "]}} {{y|Save}}",
                command = "option:-3",
                hotkey = "Accept"
            },
        };

        /// <summary>
        /// Returns Option:-4
        /// </summary>
        public static List<QudMenuItem> _ConfirmButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{hotkey|[" + ControlManager.getCommandInputFormatted("Accept", XRL.UI.Options.ModernUI) + "]}} {{y|Confirm}}",
                command = "option:-4",
                hotkey = "Accept"
            },
        };

        /// <summary>
        /// Returns Cancel (Option:-1)
        /// </summary>
        public static List<QudMenuItem> _BummerButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{hotkey|[" + ControlManager.getCommandInputFormatted("Accept", XRL.UI.Options.ModernUI) + "]}} {{y|Bummer!}}",
                hotkey = "Accept,Cancel",
                command = "Cancel"
            }
        };

        /// <summary>
        /// Returns Option:-2
        /// </summary>
        public static List<QudMenuItem> BackButton
        {
            get
            {
                if (ControlManager.activeControllerType != ControlManager.InputDeviceType.Gamepad)
                    return _BackButton;

                return new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = ControlManager.getCommandInputFormatted("V Negative", XRL.UI.Options.ModernUI) + " {{y|Back}}",
                        command = "option:-2",
                        hotkey = "N,V Negative"
                    },
                };
            }
        }

        /// <summary>
        /// Returns Option:-2
        /// </summary>
        public static List<QudMenuItem> BackancelButton
        {
            get
            {
                if (ControlManager.activeControllerType != ControlManager.InputDeviceType.Gamepad)
                    return _BackancelButton;

                return new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = ControlManager.getCommandInputFormatted("V Negative", XRL.UI.Options.ModernUI) + " {{y|Back}}",
                        command = "option:-2",
                        hotkey = "N,V Negative,Cancel"
                    },
                };
            }
        }

        /// <summary>
        /// Returns Option:-3
        /// </summary>
        public static List<QudMenuItem> SaveButton
        {
            get
            {
                if (ControlManager.activeControllerType != ControlManager.InputDeviceType.Gamepad)
                    return _SaveButton;

                return new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = ControlManager.getCommandInputDescription("Accept", XRL.UI.Options.ModernUI) + " {{W|Save}}",
                        command = "option:-3",
                        hotkey = "Accept"
                    },
                };
            }
        }

        /// <summary>
        /// Returns Option:-4
        /// </summary>
        public static List<QudMenuItem> ConfirmButton
        {
            get
            {
                if (ControlManager.activeControllerType != ControlManager.InputDeviceType.Gamepad)
                    return _ConfirmButton;

                return new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = ControlManager.getCommandInputDescription("Accept", XRL.UI.Options.ModernUI) + " {{W|Confirm}}",
                        command = "option:-4",
                        hotkey = "Accept"
                    },
                };
            }
        }

        /// <summary>
        /// Returns Cancel (Option:-1)
        /// </summary>
        public static List<QudMenuItem> BummerButton
        {
            get
            {
                if (ControlManager.activeControllerType != ControlManager.InputDeviceType.Gamepad)
                    return _BummerButton;

                return new List<QudMenuItem>
                {
                    new QudMenuItem
                    {
                        text = ControlManager.getCommandInputFormatted("Accept", XRL.UI.Options.ModernUI) + " {{y|Bummer!}}",
                        hotkey = "Accept,Cancel",
                        command = "Cancel"
                    },
                };
            }
        }

        public static async Task<TResult> PerformPickOptionAsync<T, TResult>(
            PickOptionDataSet<T, Task<TResult>> OptionDataSet,
            string Title = "",
            string Intro = null,
            IRenderable IntroIcon = null,
            IReadOnlyList<QudMenuItem> AdditionalButtons = null,
            bool NoBackButton = false,
            int? DefaultSelected = null,
            bool RespectOptionNewlines = false,
            Func<Task<TResult>> OnBackCallback = null,
            Func<Task<TResult>> OnEscapeCallback = null,
            Dictionary<int, Func<Task<TResult>>> ButtonCallbacks = null,
            Func<PickOptionData<T, Task<TResult>>, Task<TResult>, Task<TResult>> FinalSelectedCallback = null,
            Action<int> LastSelectedCallback = null
            )
        {
            int defaultSelected = DefaultSelected ?? OptionDataSet.GetDefaultIndex();
            defaultSelected = Math.Clamp(defaultSelected, 0, Math.Max(0, OptionDataSet.Count - 1));
            ButtonCallbacks ??= new();

            if (!ButtonCallbacks.ContainsKey(-1))
                ButtonCallbacks.Add(-1, OnEscapeCallback ?? (() => Task.Run(() => (TResult)default)));

            if (!ButtonCallbacks.ContainsKey(-2))
                ButtonCallbacks.Add(-2, OnBackCallback ?? (() => Task.Run(() => (TResult)default)));

            var buttons = new List<QudMenuItem>();

            if (!AdditionalButtons.IsNullOrEmpty())
                buttons.AddRange(AdditionalButtons);

            if (!NoBackButton)
                buttons.AddRange(BackButton);

            int choice = defaultSelected;
            PickOptionData<T, Task<TResult>> chosenOption = OptionDataSet[defaultSelected];
            Task<TResult> optionCallBack;
            TResult result = default;

            try
            {
                return await NavigationController.instance.SuspendContextWhile(async delegate ()
                {
                    var taskCompletionSource = new TaskCompletionSource<int>();

                    Popup.PickOption(
                        Title: Title,
                        Intro: Intro,
                        Options: OptionDataSet.GetOptions(),
                        Hotkeys: OptionDataSet.GetHotkeys(),
                        Icons: OptionDataSet.GetIcons(),
                        IntroIcon: IntroIcon,
                        Buttons: buttons,
                        DefaultSelected: defaultSelected,
                        RespectOptionNewlines: RespectOptionNewlines,
                        AllowEscape: true,
                        OnResult: choice => taskCompletionSource.TrySetResult(choice));

                    choice = await taskCompletionSource.Task;

                    LastSelectedCallback?.Invoke(choice);

                    var elements = OptionDataSet?.GetElements() ?? new List<T>();

                    if (choice < 0)
                    {
                        chosenOption = null;
                        optionCallBack = ButtonCallbacks?.GetValueOrDefault(choice)?.Invoke()
                            ?? ButtonCallbacks.GetValue(-2).Invoke();
                    }
                    else
                    {
                        chosenOption = OptionDataSet[choice];
                        optionCallBack = chosenOption.Invoke(elements.ElementAtOrDefault(choice));
                    }

                    result = await optionCallBack;

                    if (FinalSelectedCallback != null)
                        result = await FinalSelectedCallback.Invoke(chosenOption, optionCallBack);

                    return result;
                });
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(NavigationController), nameof(NavigationController.SuspendContextWhile)), x);
            }
            Utils.Log($"fall out return {nameof(result)}");
            return result;
        }

        public static async Task<CascadableResult> PerformPickOptionAsync<T>(
            CascadablePickOptionDataSetAsync<T> OptionDataSet,
            string Title = "",
            string Intro = null,
            IRenderable IntroIcon = null,
            IReadOnlyList<QudMenuItem> AdditionalButtons = null,
            bool NoBackButton = false,
            int? DefaultSelected = null,
            Action<int> LastSelectedCallback = null,
            bool RespectOptionNewlines = false,
            Func<Task<CascadableResult>> OnBackCallback = null,
            Func<Task<CascadableResult>> OnEscapeCallback = null,
            Dictionary<int, Func<Task<CascadableResult>>> ButtonCallbacks = null,
            Func<PickOptionData<T, Task<CascadableResult>>, Task<CascadableResult>, Task<CascadableResult>> FinalSelectedCallback = null
            )
            => await PerformPickOptionAsync<T, CascadableResult>(
                OptionDataSet: OptionDataSet,
                Title: Title,
                Intro: Intro,
                IntroIcon: IntroIcon,
                AdditionalButtons: AdditionalButtons,
                NoBackButton: NoBackButton,
                DefaultSelected: DefaultSelected,
                LastSelectedCallback: LastSelectedCallback,
                RespectOptionNewlines: RespectOptionNewlines,
                OnBackCallback: OnBackCallback ?? (() => Task.Run(() => OptionDataSet?.DefaultOnBackResult ?? CascadableResult.CancelSilent)),
                OnEscapeCallback: OnEscapeCallback ?? (() => Task.Run(() => OptionDataSet?.DefaultOnEscapeResult ?? CascadableResult.BackSilent)),
                ButtonCallbacks: ButtonCallbacks,
                FinalSelectedCallback: FinalSelectedCallback ?? ShowEscancellepedAsync)
            ;

        public static TResult PerformPickOption<T, TResult>(
            PickOptionDataSet<T, TResult> OptionDataSet,
            string Title = "",
            string Intro = null,
            IRenderable IntroIcon = null,
            IReadOnlyList<QudMenuItem> AdditionalButtons = null,
            bool NoBackButton = false,
            int? DefaultSelected = null,
            Action<int> LastSelectedCallback = null,
            bool RespectOptionNewlines = false,
            Func<TResult> OnBackCallback = null,
            Func<TResult> OnEscapeCallback = null,
            Dictionary<int, Func<TResult>> ButtonCallbacks = null,
            Func<PickOptionData<T, TResult>, TResult, TResult> FinalSelectedCallback = null
            )
        {
            int defaultSelected = DefaultSelected ?? OptionDataSet.GetDefaultIndex();
            defaultSelected = Math.Clamp(defaultSelected, 0, Math.Max(0, OptionDataSet.Count - 1));
            ButtonCallbacks ??= new();

            if (!ButtonCallbacks.ContainsKey(-1))
                ButtonCallbacks.Add(-1, OnEscapeCallback ?? (() => (TResult)default));

            if (!ButtonCallbacks.ContainsKey(-2))
                ButtonCallbacks.Add(-2, OnBackCallback ?? (() => (TResult)default));

            var buttons = new List<QudMenuItem>();

            if (!AdditionalButtons.IsNullOrEmpty())
                buttons.AddRange(AdditionalButtons);

            if (!NoBackButton)
                buttons.AddRange(BackButton);

            int choice = defaultSelected;
            PickOptionData<T, TResult> chosenOption = OptionDataSet[defaultSelected];
            TResult result = default;
            
            try
            {
                choice = Popup.PickOption(
                    Title: Title,
                    Intro: Intro,
                    Options: OptionDataSet.GetOptions(),
                    Hotkeys: OptionDataSet.GetHotkeys(),
                    Icons: OptionDataSet.GetIcons(),
                    IntroIcon: IntroIcon,
                    Buttons: buttons,
                    DefaultSelected: defaultSelected,
                    RespectOptionNewlines: RespectOptionNewlines,
                    AllowEscape: true);

                LastSelectedCallback?.Invoke(choice);

                var elements = OptionDataSet.GetElements() ?? new List<T>();

                if (choice < 0)
                {
                    chosenOption = null;
                    result = ButtonCallbacks.GetValueOrDefault(choice).Invoke();
                }
                else
                {
                    chosenOption = OptionDataSet[choice];
                    result = chosenOption.Invoke(elements.ElementAtOrDefault(choice));
                }

                if (FinalSelectedCallback != null)
                    result = FinalSelectedCallback.Invoke(chosenOption, result);

                return result;
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(NavigationController), nameof(NavigationController.SuspendContextWhile)), x);
            }
            return result;
        }

        public static async Task<TResult> ShowEscancellepedAsync<T, TResult>(
            PickOptionData<T, Task<TResult>> Option,
            Task<TResult> Result,
            Predicate<TResult> CancelledWhen,
            Predicate<TResult> EscapedWhen,
            Func<T, Task<TResult>, Task<TResult>> PostProc = null
            )
        {
            bool escaped = EscapedWhen(await Result.AwaitResultIfNotIsCompletedSuccessfully());
            if (escaped
                || CancelledWhen(await Result.AwaitResultIfNotIsCompletedSuccessfully()))
            {
                string escancelleped = "cancelled";
                if (escaped)
                    escancelleped = "escaped";

                if (!(Option?.Text).IsNullOrEmpty())
                    await Popup.ShowAsync($"\"{Option.Text}\" operation {escancelleped}.");
            }

            T element = default;
            if (Option != null)
                element = Option.Element;

            return PostProc != null
                ? await PostProc.Invoke(element, Result)
                : await Result
                ;
        }

        public static TResult ShowEscancelleped<T, TResult>(
            PickOptionData<T, TResult> Option,
            TResult Result,
            Predicate<TResult> CancelledWhen,
            Predicate<TResult> EscapedWhen,
            Func<T, TResult, TResult> PostProc = null
            )
        {
            bool escaped = EscapedWhen(Result);
            if (escaped
                || CancelledWhen(Result))
            {
                string escancelleped = "cancelled";
                if (escaped)
                    escancelleped = "escaped";

                if (!(Option?.Text).IsNullOrEmpty())
                    Popup.ShowAsync($"\"{Option.Text}\" operation {escancelleped}.").Wait();
            }

            T element = default;
            if (Option != null)
                element = Option.Element;

            return PostProc != null
                ? PostProc.Invoke(element, Result)
                : Result
                ;
        }

        public static Task<bool?> ShowEscancellepedAsync<T>(
            PickOptionData<T, Task<bool?>> Option,
            Task<bool?> Result
            )
            => ShowEscancellepedAsync(
                Option: Option,
                Result: Result,
                CancelledWhen: r => r is false,
                EscapedWhen: r => r is null)
            ;

        public static bool? ShowEscancelleped<T>(
            PickOptionData<T, bool?> Option,
            bool? Result
            )
            => ShowEscancelleped(
                Option: Option,
                Result: Result,
                CancelledWhen: r => r is false,
                EscapedWhen: r => r is null)
            ;

        public static async Task<CascadableResult> ShowEscancellepedAsync<T>(
            PickOptionData<T, Task<CascadableResult>> Option,
            Task<CascadableResult> Result
            )
        {
            var result = await Result.AwaitResultIfNotIsCompletedSuccessfully();

            if (result.IsSilent())
                return result;

            return await ShowEscancellepedAsync(
                Option: Option,
                Result: Result,
                CancelledWhen: r => r.IsBack(),
                EscapedWhen: r => r.IsCancel(),
                PostProc: async delegate (T o, Task<CascadableResult> r)
                {
                    var result = await r.AwaitResultIfNotIsCompletedSuccessfully();
                    if (!result.IsContinue() && !result.IsSilent())
                        return ++result;
                    return result;
                });
        }

        public static CascadableResult ShowEscancelleped<T>(
            PickOptionData<T, CascadableResult> Option,
            CascadableResult Result
            )
        {
            if (Result.IsSilent())
                return Result;

            return ShowEscancelleped(
                Option: Option,
                Result: Result,
                CancelledWhen: r => r.IsBack(),
                EscapedWhen: r => r.IsCancel(),
                PostProc: (o, r) => !r.IsContinue() && !r.IsSilent() ? ++r : r);
        }

        public static async Task ShowBummerAsync(
            string Message,
            string Title = null,
            IRenderable Icon = null,
            bool PushView = false
            )
            => await Popup.NewPopupMessageAsync(
                message: Message,
                buttons: BummerButton,
                title: Title,
                afterRender: Icon,
                pushView: PushView,
                PopupID: Utils.CallChain(nameof(UIUtils), nameof(ShowBummerAsync)))
            ;

        public static async Task<string> AskStringAsync(
            string Message,
            string Title = null,
            string Default = "",
            IRenderable Icon = null,
            string RestrictChars = null,
            bool ReturnNullForEscape = false,
            bool EscapeNonMarkupFormatting = true,
            bool? AllowColorize = null,
            bool pushView = false,
            string WantsSpecificPrompt = null
            )
        {
            ControlManager.ResetInput();

            var buttons = (AllowColorize
                    ?? XRL.UI.Options.GetOption("OptionEnableColorTextInput").EqualsNoCase("Yes"))
                ? PopupMessage.SubmitCancelColorButton
                : PopupMessage.SubmitCancelButton;

            if (WantsSpecificPrompt != null)
            {
                if (CapabilityManager.CurrentPlatformClassification() == CapabilityManager.PlatformClassification.PC)
                    buttons = PopupMessage.SubmitCancelHoldButton;
                else
                if (Default == null)
                    buttons = PopupMessage.CancelHoldButton;
            }

            if (pushView)
                GameManager.Instance.PushGameView("ModernPopup:AskString");

            string result = Default;
            try
            {
                QudMenuItem qudMenuItem;
                while (true)
                {
                    qudMenuItem = await Popup.NewPopupMessageAsync(
                        message: StringFormat.ClipText(Message, 60, KeepNewlines: true),
                        buttons: buttons,
                        options: null,
                        title: Title,
                        inputDefault: result,
                        DefaultSelected: 0,
                        contextTitle: null,
                        contextRender: null,
                        afterRender: Icon,
                        showContextFrame: true,
                        EscapeNonMarkupFormatting: EscapeNonMarkupFormatting,
                        pushView: pushView,
                        cancelToken: default,
                        askingNumber: false,
                        RestrictChars: RestrictChars ?? "",
                        WantsSpecificPrompt: WantsSpecificPrompt);

                    if (!(qudMenuItem.command == "Color"))
                    {
                        break;
                    }

                    result = qudMenuItem.text;
                    string text = await Popup.ShowColorPickerAsync(
                        Title: "Choose color",
                        AllowEscape: true,
                        includePatterns: true,
                        previewContent: ColorUtility.StripFormatting(result));
                    if (!string.IsNullOrEmpty(text))
                    {
                        if (string.IsNullOrWhiteSpace(result))
                        {
                            result = " ";
                        }

                        result = "{{" + text + "|" + ColorUtility.StripFormatting(result) + "}}";
                    }
                }

                if (qudMenuItem.command == "Cancel")
                {
                    return ReturnNullForEscape
                        ? null
                        : ""
                        ;
                }

                return qudMenuItem.text;
            }
            catch (Exception x)
            {
                Utils.Error("AskStringAsync", x);
                return null;
            }
            finally
            {
                Keyboard.ClearInput(LeaveMovementEvents: false, clearFrameDown: true);

                if (pushView)
                    GameManager.Instance.PopGameView(bHard: true);
            }
        }

        public static async Task<int?> AskNumberAsync(
            string Message,
            string Title = null,
            IRenderable Icon = null,
            int Start = 0,
            int Min = 0,
            int Max = int.MaxValue,
            string RestrictChars = "",
            bool pushView = false)
        {
            ControlManager.ResetInput();
            if (ControlManager.activeControllerType != ControlManager.InputDeviceType.Keyboard)
            {
                if (pushView)
                    GameManager.Instance.PushGameView("ModernPopupGamepadAskNumber");

                try
                {
                    return await AskNumberScreen.show($"{Title}\n\n{Message}", Start, Min, Max);
                }
                finally
                {
                    if (pushView)
                        GameManager.Instance.PopGameView(bHard: true);
                }
            }

            var submitCancelButton = PopupMessage.SubmitCancelButton;

            if (pushView)
                GameManager.Instance.PushGameView("ModernPopup:AskNumber");

            try
            {
                var qudMenuItem = await Popup.NewPopupMessageAsync(
                    message: StringFormat.ClipText(Message, 60, KeepNewlines: true),
                    buttons: submitCancelButton,
                    options: null,
                    title: Title,
                    inputDefault: Start.ToString(),
                    DefaultSelected: 0,
                    afterRender: Icon,
                    pushView: pushView,
                    askingNumber: true,
                    RestrictChars: RestrictChars);

                if (qudMenuItem.command == "Cancel")
                    return null;


                return Convert.ToInt32(qudMenuItem.text);
            }
            catch (Exception x)
            {
                Utils.Error("AskNumberAsync", x);
                return null;
            }
            finally
            {
                Keyboard.ClearInput(LeaveMovementEvents: false, clearFrameDown: true);
                if (pushView)
                    GameManager.Instance.PopGameView(bHard: true);
            }
        }

        public static async Task PopupShowAsync(
            string Message,
            string Title = null,
            bool Capitalize = true,
            bool LogMessage = true,
            Location2D PopupLocation = null
            )
        {
            if (Capitalize)
                Message = ColorUtility.CapitalizeExceptFormatting(Message);

            if (LogMessage)
                MessageQueue.AddPlayerMessage(Message: Message, Capitalize: Capitalize);

            await Popup.NewPopupMessageAsync(
                message: Message,
                buttons: PopupMessage.AcceptButton,
                title: Title,
                PopupLocation: PopupLocation);
        }
    }
}
