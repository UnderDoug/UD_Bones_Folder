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
        public static List<QudMenuItem> _BackButton = new List<QudMenuItem>
        {
            new QudMenuItem
            {
                text = "{{y|Back}}",
                // command = "No",
                command = "option:-2",
                hotkey = "N,V Negative"
            },
        };

        public static List<QudMenuItem> BackButton
        {
            get
            {
                if (ControlManager.activeControllerType == ControlManager.InputDeviceType.Gamepad)
                {
                    return new List<QudMenuItem>
                    {
                        new QudMenuItem
                        {
                            text = ControlManager.getCommandInputFormatted("V Negative") + " {{y|Back}}",
                            // command = "No",
                            command = "option:-2",
                            hotkey = "N,V Negative"
                        },
                    };
                }
                return _BackButton;
            }
        }

        public static async Task<TResult> PerformPickOptionAsync<T, TResult>(
            PickOptionDataSet<T, Task<TResult>> OptionDataSet,
            Predicate<TResult> BreakWhen,
            string Title = "",
            string Intro = null,
            IRenderable IntroIcon = null,
            IReadOnlyList<QudMenuItem> Buttons = null,
            int DefaultSelected = 0,
            bool RespectOptionNewlines = false,
            TResult ValueOnBack = default,
            Func<PickOptionData<T, Task<TResult>>, Task<TResult>, Task<TResult>> BackCallback = null,
            bool AllowEscape = true,
            TResult ValueOnEscape = default,
            Func<PickOptionData<T, Task<TResult>>, Task<TResult>, Task<TResult>> FinalSelectedCallback = null
            )
        {
            Buttons ??= BackButton;
            int choice = DefaultSelected;
            PickOptionData<T, Task<TResult>> chosenOption = OptionDataSet[DefaultSelected];
            Task<TResult> optionCallBack;
            TResult result = default;
            do
            {
                var navController = NavigationController.instance;
                var oldContext = navController.activeContext;
                navController.activeContext = NavigationController.instance.suspensionContext;
                try
                {
                    var taskCompletionSource = new TaskCompletionSource<int>();
                    Popup.PickOption(
                        Title: Title,
                        Intro: Intro,
                        Options: OptionDataSet.GetOptions(),
                        Hotkeys: OptionDataSet.GetHotkeys(),
                        Icons: OptionDataSet.GetIcons(),
                        IntroIcon: IntroIcon,
                        Buttons: Buttons,
                        DefaultSelected: DefaultSelected,
                        RespectOptionNewlines: RespectOptionNewlines,
                        AllowEscape: AllowEscape,
                        OnResult: choice => taskCompletionSource.TrySetResult(choice),
                        ForceNewPopup: true);

                    choice = await taskCompletionSource.Task;

                    if (choice < 0)
                    {
                        chosenOption = null;
                        optionCallBack = Task<TResult>.Run(delegate ()
                        {
                            return choice != -2 
                            ? ValueOnEscape
                            : ValueOnBack
                            ;
                        });
                        break;
                    }

                    chosenOption = OptionDataSet[choice];

                    optionCallBack = chosenOption.Invoke();

                    result = await optionCallBack;

                    if (BreakWhen.Invoke(result))
                        break;
                }
                finally
                {
                    navController.activeContext = oldContext;
                }
                /*choice = await Popup.PickOptionAsync(
                    Title: Title,
                    Intro: Intro,
                    Options: OptionDataSet.GetOptions(),
                    Hotkeys: OptionDataSet.GetHotkeys(),
                    Icons: OptionDataSet.GetIcons(),
                    IntroIcon: IntroIcon,
                    DefaultSelected: DefaultSelected,
                    RespectOptionNewlines: RespectOptionNewlines,
                    AllowEscape: AllowEscape);

                if (choice < 0)
                    return result;

                chosenOption = OptionDataSet[choice];

                optionCallBack = chosenOption.Invoke();

                result = await optionCallBack;

                if (BreakWhen.Invoke(result))
                break;*/
            }
            while (true);

            if (FinalSelectedCallback != null)
                return await FinalSelectedCallback(chosenOption, optionCallBack);
            else
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

                await Popup.ShowAsync($"\"{Option.Text}\" operation {escancelleped}.");
            }
            return PostProc != null
                ? await PostProc.Invoke(Option.Element, Result)
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

                Popup.ShowAsync($"\"{Option.Text}\" operation {escancelleped}.").Wait();
            }
            return PostProc != null
                ? PostProc.Invoke(Option.Element, Result)
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
    }
}
