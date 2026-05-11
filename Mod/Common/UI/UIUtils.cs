using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using Qud.UI;

using XRL.UI;
using XRL.UI.Framework;

namespace UD_Bones_Folder.Mod.UI
{
    public static class UIUtils
    {
        public enum CascadableResult : int
        {
            Continue,
            Back,
            BackSilent,
            Cancel,
            CancelSilent,
        }
        public static List<QudMenuItem> _BackButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{y|Back}}",
                command = "option:-2",
                hotkey = "N,V Negative"
            },
        };

        public static List<QudMenuItem> _SaveButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{y|Save}}",
                command = "option:-3",
                hotkey = "Accept"
            },
        };

        public static List<QudMenuItem> _ConfirmButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{y|Confirm}}",
                command = "option:-4",
                hotkey = "Accept"
            },
        };

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

        public static async Task<TResult> PerformPickOptionAsync<T, TResult>(
            PickOptionDataSet<T, Task<TResult>> OptionDataSet,
            string Title = "",
            string Intro = null,
            IRenderable IntroIcon = null,
            IReadOnlyList<QudMenuItem> AdditionalButtons = null,
            bool NoBackButton = false,
            int DefaultSelected = 0,
            bool RespectOptionNewlines = false,
            Func<Task<TResult>> OnBackCallback = null,
            Func<Task<TResult>> OnEscapeCallback = null,
            Dictionary<int, Func<Task<TResult>>> ButtonCallbacks = null,
            Func<PickOptionData<T, Task<TResult>>, Task<TResult>, Task<TResult>> FinalSelectedCallback = null
            )
        {
            DefaultSelected = Math.Clamp(DefaultSelected, 0, OptionDataSet.Count - 1);
            ButtonCallbacks ??= new();

            ButtonCallbacks.Add(-1, OnEscapeCallback ?? (() => Task.Run(() => (TResult)default)));
            ButtonCallbacks.Add(-2, OnBackCallback ?? (() => Task.Run(() => (TResult)default)));

            var buttons = new List<QudMenuItem>();

            if (!AdditionalButtons.IsNullOrEmpty())
                buttons.AddRange(AdditionalButtons);

            if (!NoBackButton)
                buttons.AddRange(BackButton);

            int choice = DefaultSelected;
            PickOptionData<T, Task<TResult>> chosenOption = OptionDataSet[DefaultSelected];
            Task<TResult> optionCallBack;
            TResult result = default;
            
            try
            {
                return await NavigationController.instance.SuspendContextWhile(async delegate ()
                {
                    var taskCompletionSource = new TaskCompletionSource<int>();

                    //Utils.Log($"PickOption");
                    Popup.PickOption(
                        Title: Title,
                        Intro: Intro,
                        Options: OptionDataSet.GetOptions(),
                        Hotkeys: OptionDataSet.GetHotkeys(),
                        Icons: OptionDataSet.GetIcons(),
                        IntroIcon: IntroIcon,
                        Buttons: buttons,
                        DefaultSelected: DefaultSelected,
                        RespectOptionNewlines: RespectOptionNewlines,
                        AllowEscape: true,
                        OnResult: choice => taskCompletionSource.TrySetResult(choice));

                    choice = await taskCompletionSource.Task;

                    if (choice < 0)
                    {
                        chosenOption = null;
                        optionCallBack = ButtonCallbacks?.GetValueOrDefault(choice)?.Invoke()
                            ?? ButtonCallbacks.GetValue(-2).Invoke();
                    }
                    else
                    {
                        chosenOption = OptionDataSet[choice];
                        optionCallBack = chosenOption.Invoke();
                    }

                    result = await optionCallBack;

                    if (FinalSelectedCallback != null)
                        result = await FinalSelectedCallback(chosenOption, optionCallBack);

                    return result;
                });
            }
            catch (Exception x)
            {
                Utils.Error(Utils.CallChain(nameof(NavigationController), nameof(NavigationController.SuspendContextWhile)), x);
            }
            return result;
        }

        public static TResult PerformPickOption<T, TResult>(
            PickOptionDataSet<T, TResult> OptionDataSet,
            string Title = "",
            string Intro = null,
            IRenderable IntroIcon = null,
            IReadOnlyList<QudMenuItem> AdditionalButtons = null,
            bool NoBackButton = false,
            int DefaultSelected = 0,
            bool RespectOptionNewlines = false,
            Func<TResult> OnBackCallback = null,
            Func<TResult> OnEscapeCallback = null,
            Dictionary<int, Func<TResult>> ButtonCallbacks = null,
            Func<PickOptionData<T, TResult>, TResult, TResult> FinalSelectedCallback = null
            )
        {
            DefaultSelected = Math.Clamp(DefaultSelected, 0, OptionDataSet.Count - 1);
            ButtonCallbacks ??= new();

            ButtonCallbacks.Add(-1, OnEscapeCallback ?? (() => (TResult)default));
            ButtonCallbacks.Add(-2, OnBackCallback ?? (() => (TResult)default));

            var buttons = new List<QudMenuItem>();

            if (!AdditionalButtons.IsNullOrEmpty())
                buttons.AddRange(AdditionalButtons);

            if (!NoBackButton)
                buttons.AddRange(BackButton);

            int choice = DefaultSelected;
            PickOptionData<T, TResult> chosenOption = OptionDataSet[DefaultSelected];
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
                    DefaultSelected: DefaultSelected,
                    RespectOptionNewlines: RespectOptionNewlines,
                    AllowEscape: true);

                if (choice < 0)
                {
                    chosenOption = null;
                    result = ButtonCallbacks.GetValueOrDefault(choice).Invoke();
                }
                else
                {
                    chosenOption = OptionDataSet[choice];
                    result = chosenOption.Invoke();
                }

                if (FinalSelectedCallback != null)
                    result = FinalSelectedCallback(chosenOption, result);

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
    }
}
