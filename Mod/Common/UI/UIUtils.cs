using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using XRL.UI;

namespace UD_Bones_Folder.Mod.UI
{
    public static class UIUtils
    {
        public static async Task<TResult> PerformPickOptionAsync<T, TResult>(
            PickOptionDataSet<T, Task<TResult>> OptionDataSet,
            Predicate<TResult> BreakWhen,
            string Title = "",
            string Intro = null,
            IRenderable IntroIcon = null,
            int DefaultSelected = 0,
            bool RespectOptionNewlines = false,
            bool AllowEscape = false,
            TResult ValueOnEscape = default,
            Func<PickOptionData<T, Task<TResult>>, Task<TResult>, Task<TResult>> FinalSelectedCallback = null
            )
        {
            Task<TResult> optionCallBack;
            int choice;
            do
            {
                choice = await Popup.PickOptionAsync(
                    Title: Title,
                    Intro: Intro,
                    Options: OptionDataSet.GetOptions(),
                    Hotkeys: OptionDataSet.GetHotkeys(),
                    Icons: OptionDataSet.GetIcons(),
                    IntroIcon: IntroIcon,
                    DefaultSelected: DefaultSelected,
                    RespectOptionNewlines: RespectOptionNewlines,
                    AllowEscape: AllowEscape);

                if (choice <= 0)
                    return ValueOnEscape;

                optionCallBack = OptionDataSet.TryInvokeAt(choice);

                if (BreakWhen.Invoke(await optionCallBack))
                    break;
            }
            while (true);

            if (FinalSelectedCallback != null)
                return await FinalSelectedCallback(OptionDataSet.ElementAtOrDefault(choice), optionCallBack);
            else
                return await optionCallBack;
        }

        public static async Task<TResult> ShowEscancellepedAsync<T, TResult>(
            PickOptionData<T, Task<TResult>> Option,
            Task<TResult> Result,
            Predicate<TResult> CancelledWhen,
            Predicate<TResult> EscapedWhen,
            Func<T, Task<TResult>, Task<TResult>> PostProc = null
            )
        {
            bool escaped = EscapedWhen(await Result);
            if (escaped
                || CancelledWhen(await Result))
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
