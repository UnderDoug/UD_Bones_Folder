using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using ConsoleLib.Console;

using XRL.Collections;

namespace UD_Bones_Folder.Mod.UI
{
    public class PickOptionDataSetAsync<T, TResult> : PickOptionDataSet<T, Task<TResult>>
    {
        public PickOptionDataSetAsync()
            : base()
        { }
        public PickOptionDataSetAsync(PickOptionDataSet<T, Task<TResult>> Source)
            : base(Source)
        { }
    }
}
