using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using XRL.Collections;

using UD_Bones_Folder.Mod.Logging;
using UD_Bones_Folder.Mod.OsseousAsh;

namespace UD_Bones_Folder.Mod
{
    [HasRegisterMethodForDebug]
    public class PooledBatch<T> : PooledList<PooledBatch<T>.BatchEntry>, INotifyCompletion
    {
        public class BatchEntry
        {
            public Task<T> Task;
            public T Result;
            public Exception Exception;

            private bool _Cancelled;
            public bool Cancelled => _Cancelled;

            public bool Processed
                => Task == null
                || Task.IsCompleted
                || Task.IsCanceled
                || Task.IsFaulted
                ;

            public bool Succeeded => Processed && !Cancelled && Exception == null;

            public bool Failed => Processed && !Succeeded;

            public async void WaitTask(bool Silent = true)
            {
                if (Task == null)
                    return;

                try
                {
                    Result = await Task;
                }
                catch (Exception x)
                {
                    Exception = x;
                    if (!Silent)
                        Utils.Warn($"{GetType().ToStringWithGenerics()} failed to process {Task?.GetType()?.ToStringWithGenerics() ?? "MISSING_TASK"}", Exception);
                }
                finally
                {
                    if (Task?.IsCanceled is true)
                        _Cancelled = true;

                    Task = null;
                }
            }

            public static explicit operator T(BatchEntry Operand)
                => Operand != null
                ? Operand.Result
                : default
                ;

            public static explicit operator Task<T>(BatchEntry Operand)
                => Operand != null
                ? Operand.Task
                : default
                ;

            public static explicit operator Exception(BatchEntry Operand)
                => Operand != null
                ? Operand.Exception
                : default
                ;
        }

        private static readonly object _UnusedPoolLock = new();
        protected static ConcurrentBag<PooledBatch<T>> UnusedPool = new();

        protected bool Pooled;

        public bool IsCompleted { get; private set; } = false;
        
        private Action _Continuation;

        public PooledBatch(params Task<T>[] Tasks)
            : base(Tasks.IteratorSafe().Select(t => new BatchEntry { Task = t }).ToArray())
        { }

        [RegisterMethodForDebug]
        protected static PooledBatch<T> GetFromPool()
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(indent[1]);
            lock (_UnusedPoolLock)
            {
                if (UnusedPool.TryTake(out var result))
                {
                    result.Reuse();
                    return result;
                }
            }
            return new PooledBatch<T>();
        }

        [RegisterMethodForDebug]
        public static PooledBatch<T> GetWith(IEnumerable<Task<T>> Tasks)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Tasks), Tasks.GetNullString()),
                });

            var batch = GetFromPool();
            Debug.YehNah($"{nameof(batch)}", Good: batch != null, Indent: indent[2]);
            try
            {
                batch.AddRange(Tasks.IteratorSafe().Select(t => new BatchEntry { Task = t }));
                return batch;
            }
            catch (Exception x)
            {
                Utils.Error($"{typeof(PooledBatch<T>).ToStringWithGenerics()} failed to {nameof(GetWith)}", x);
                return batch;
            }
            finally
            {
                if (batch != null)
                {
                    batch.IsCompleted = batch.IsNullOrEmpty();
                    batch.ProcessBatch();
                }
            }
        }

        public static PooledBatch<T> GetWith(params Task<T>[] Tasks)
            => GetWith(Tasks.IteratorSafe())
            ;

        public PooledBatch<T> GetAwaiter()
            => this
            ;

        public void ProcessBatch()
        {
            try
            {
                foreach (var batchResult in this)
                {
                    try
                    {
                        batchResult.WaitTask();
                    }
                    catch (Exception x)
                    {
                        Utils.Warn($"{GetType().ToStringWithGenerics()}.{nameof(ProcessBatch)} failed to process {batchResult?.Task?.GetType()?.ToStringWithGenerics() ?? "MISSING_TASK"}", x);
                        batchResult.Exception ??= x;
                    }
                }
            }
            finally
            {
                IsCompleted = true;
                _Continuation?.Invoke();
            }
        }

        public PooledList<BatchEntry> GetResult()
            => this
            ;

        public void OnCompleted(Action Continuation)
        {
            if (IsCompleted)
                Continuation();
            else
                _Continuation = Continuation;
        }

        protected void Reuse()
        {
            if (!Pooled)
                throw new Exception($"Attempt to reuse {GetType()?.ToStringWithGenerics() ?? nameof(PooledBatch<T>)} not in pool");

            Pooled = false;
        }

        public override void Dispose()
        {
            if (!Pooled)
            {
                lock (_UnusedPoolLock)
                {
                    base.Dispose();
                    Pooled = true;
                    UnusedPool.Add(this);
                }
            }
        }
    }
}
