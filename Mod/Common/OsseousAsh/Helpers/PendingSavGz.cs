using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public class PendingSavGz : IDisposable
    {
        public static int MaxFileSizeInBytes => 1000 * 1000 * 4;

        public Host ParentHost;

        public string BonesID;
        public Guid Token;
        public byte[] SavGz;
        public DateTime UploadTime;
        public Timer Timer;

        public bool Success;
        public bool ForceDispose;

        public ResponseInfo CheckValid(bool IgnoreToken = false, bool Silent = false)
        {
            var responseInfo = new ResponseInfo
            {
                Host = ParentHost,
                Success = true,
            };

            if (ParentHost == null)
            {
                responseInfo.Message = $"{nameof(PendingSavGz)} Invalid; {nameof(ParentHost)} is null.";
                responseInfo.Success = false;
            }
            else
            if (BonesID.IsNullOrEmpty())
            {
                responseInfo.Message = $"{nameof(PendingSavGz)} Invalid; {nameof(BonesID)} is null or empty.";
                responseInfo.Success = false;
            }
            else
            if (!IgnoreToken
                && Token.IsEmptyOrDefault())
            {
                responseInfo.Message = $"{nameof(PendingSavGz)} Invalid; {nameof(Token)} is empty or default {{{BonesID}}}.";
                responseInfo.Success = false;
            }
            else
            if (SavGz.IsNullOrEmpty())
            {
                responseInfo.Message = $"{nameof(PendingSavGz)} Invalid; {nameof(SavGz)} is null or empty {{{BonesID}}}.";
                responseInfo.Success = false;
            }
            else
            if (SavGz.Length > MaxFileSizeInBytes)
            {
                responseInfo.Message = $"{nameof(PendingSavGz)} Invalid; {nameof(SavGz)} is {SavGz.FormatBytes()}, limit is {MaxFileSizeInBytes.FormatBytes()} {{{BonesID}}}.";
                responseInfo.Success = false;
            }
            else
            if (Success)
            {
                responseInfo.Message = $"{nameof(PendingSavGz)} Invalid; {nameof(Success)} is {Success}.";
                responseInfo.Success = false;
            }
            else
            if (ForceDispose)
            {
                responseInfo.Message = $"{nameof(PendingSavGz)} Invalid; {nameof(ForceDispose)} is {ForceDispose}.";
                responseInfo.Success = false;
            }

            if (!Silent
                && !responseInfo.Success
                && !responseInfo.Message.IsNullOrEmpty())
                Utils.Warn(responseInfo.Message);

            return responseInfo;
        }

        public async Task<bool> RetryUpload(bool ForceDispose = false)
        {
            if (ForceDispose)
                this.ForceDispose = true;

            var timeSinceFirstTry = DateTime.UtcNow - UploadTime;
            bool exceededTimeLimit = timeSinceFirstTry.TotalMilliseconds >= 180000;

            Utils.InfoTimestamp($"{nameof(PendingSavGz)} attempting to {nameof(RetryUpload)} of {{{BonesID ?? "NO_BONES_ID"}}} to {ParentHost?.ToString() ?? "NO_HOST"}, time since first try: {timeSinceFirstTry.ValueUnits()}");

            if (ParentHost == null)
                this.ForceDispose = true;
            else
            if (exceededTimeLimit)
                this.ForceDispose = true;
            else
            if ((await ParentHost.PutBonesSavGz(this)).Success)
                Success = true;

            if (!this.ForceDispose
                && !Success)
                Utils.Warn($"{nameof(PendingSavGz)}.{nameof(RetryUpload)} fell through.");

            string dueTo = "unknown reason";
            if (Success)
                dueTo = "successful PUT";
            else
            if (exceededTimeLimit)
                dueTo = $"exceeding token duration";
            else
            if (ForceDispose)
                dueTo = "forced disposal";
            else
            if (ParentHost == null)
                dueTo = "missing parent host";

            if (Success
                || this.ForceDispose)
            {
                var pendingSavGzs = ParentHost?.PendingSavGzs;
                Dispose();
                string pendingSavsCount = (pendingSavGzs?.Count)?.ToString() ?? "NO_DICTIONARY";
                Utils.InfoTimestamp($"Disposing of {nameof(PendingSavGz)} due to {dueTo}. {nameof(ParentHost.PendingSavGzs)}: {pendingSavsCount}");
            }

            return Success;
        }

        protected void DisposeInternal()
        {
            ParentHost?.PendingSavGzs?.Remove(BonesID);

            ParentHost = null;

            BonesID = null;
            Token = default;
            SavGz = null;
            UploadTime = default;
            Timer?.Dispose();
            Timer = null;

            Success = false;
            ForceDispose = false;
        }

        public void Dispose()
        {
            if (Success
                || ForceDispose)
                DisposeInternal();
            else
                RetryUpload(ForceDispose: true).Wait();
        }
    }
}
