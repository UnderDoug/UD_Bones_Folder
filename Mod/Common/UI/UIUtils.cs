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

        public static async Task<TResult> PerformPickOptionAsync<T, TResult>(
            PickOptionDataSet<T, Task<TResult>> OptionDataSet,
            string Title = "",
            string Intro = null,
            IRenderable IntroIcon = null,
            IReadOnlyList<QudMenuItem> AdditionalButtons = null,
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

                    Utils.Log($"PickOption");
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
                        OnResult: delegate (int choice)
                        {
                            /*Utils.Log($"{nameof(choice)} is {choice}");
                            if (choice >= 0)
                                Utils.Log($"{1.Indent()}: {OptionDataSet[choice].Text ?? "NO_CHOICE_TEXT"}");
                            else
                            {
                                if (choice == -1)
                                    Utils.Log($"{1.Indent()}: back");
                                else
                                    Utils.Log($"{1.Indent()}: cancel");
                            }*/
                            taskCompletionSource.TrySetResult(choice);
                        });

                    choice = await taskCompletionSource.Task;

                    if (choice < 0)
                    {
                        chosenOption = null;
                        optionCallBack = ButtonCallbacks?.GetValueOrDefault(choice)?.Invoke();
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

            if (result == CascadableResult.BackSilent
                || result == CascadableResult.CancelSilent)
                return result;

            return await ShowEscancellepedAsync(
                Option: Option,
                Result: Result,
                CancelledWhen: r => r.IsTwixtInclusive(CascadableResult.Back, CascadableResult.BackSilent),
                EscapedWhen: r => r.IsTwixtInclusive(CascadableResult.Cancel, CascadableResult.CancelSilent),
                PostProc: async delegate (T o, Task<CascadableResult> r)
                {
                    var result = await r.AwaitResultIfNotIsCompletedSuccessfully();
                    if (result == CascadableResult.Back
                        || result == CascadableResult.Cancel)
                        return ++result;
                    return result;
                });
        }
    }
}
