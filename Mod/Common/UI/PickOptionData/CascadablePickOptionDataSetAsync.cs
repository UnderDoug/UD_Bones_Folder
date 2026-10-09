using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using XRL.Collections;

namespace UD_Bones_Folder.Mod.UI
{
    public class CascadablePickOptionDataSetAsync<T> : PickOptionDataSetAsync<T, UIUtils.CascadableResult>
    {
        public UIUtils.CascadableResult? DefaultOnBackResult;
        public UIUtils.CascadableResult? DefaultOnEscapeResult;

        public CascadablePickOptionDataSetAsync(
            UIUtils.CascadableResult? DefaultOnBackResult = null,
            UIUtils.CascadableResult? DefaultOnEscapeResult = null)
            : base()
        {
            this.DefaultOnBackResult = DefaultOnBackResult;
            this.DefaultOnEscapeResult = DefaultOnEscapeResult;
        }

        public CascadablePickOptionDataSetAsync(
            T SingleElement,
            UIUtils.CascadableResult? DefaultOnBackResult = null,
            UIUtils.CascadableResult? DefaultOnEscapeResult = null
            )
            : base(SingleElement)
        {
            this.DefaultOnBackResult = DefaultOnBackResult;
            this.DefaultOnEscapeResult = DefaultOnEscapeResult;
        }

        public CascadablePickOptionDataSetAsync(CascadablePickOptionDataSetAsync<T> Source)
            : base(Source)
        {
            DefaultOnBackResult = Source?.DefaultOnBackResult;
            DefaultOnEscapeResult = Source?.DefaultOnEscapeResult;
        }

        public override void Dispose()
        {
            base.Dispose();
            DefaultOnBackResult = null;
            DefaultOnEscapeResult = null;
        }
    }
}
