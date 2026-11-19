using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public class PendingWebTask<T>
    {
        public TaskCompletionSource<T> CompletionSource;
        public Task<T> Task;

        public void Deconstruct(out TaskCompletionSource<T> CompletionSource, out Task<T> Task)
        {
            CompletionSource = this.CompletionSource;
            Task = this.Task;
        }

        public async Task CompleteAsync()
        {
            try
            {
                if (!CompletionSource.TrySetResult(await Task) is true)
                    CompletionSource?.TrySetCanceled();
            }
            catch (Exception x)
            {
                CompletionSource.TrySetException(x);
            }
        }
    }
}
