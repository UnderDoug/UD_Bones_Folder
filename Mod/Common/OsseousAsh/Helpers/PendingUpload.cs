using System;
using System.Collections.Generic;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StreamReader = System.IO.StreamReader;

using Newtonsoft.Json.Linq;

using UD_Bones_Folder.Mod.Serialization;
using UD_Bones_Folder.Mod.Logging;
using HarmonyLib;

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    [HasRegisterMethodForDebug]
    public class PendingUpload : IDisposable
    {
        #region Debug Registration
        /*[RegistersMethodsForDebug]
        public static void doDebugRegistryFalse(DebugMethodRegistry Registry)
            => Registry.RegisterEachFalse(
                Type: typeof(PendingUpload),
                Methods: new string[]
                {
                });

        [RegistersMethodsForDebug]
        public static void doDebugRegistryTrue(DebugMethodRegistry Registry)
            => Registry.RegisterEachTrue(
                Type: typeof(PendingUpload),
                Methods: new string[]
                {
                });*/

        [RegistersMethodsForDebug]
        public static void doDebugRegistrySpecial(DebugMethodRegistry Registry)
        {
            Registry.RegisterEach(
                Type: typeof(PendingUpload),
                new MethodRegistryEntry(AccessTools.Method(typeof(PendingUpload), nameof(Attempt)), true),
                new MethodRegistryEntry(AccessTools.Method(typeof(PendingUpload), nameof(CheckValid)), true),
                new MethodRegistryEntry(DebugMethodRegistry.GetAsyncMethod(typeof(PendingUpload), nameof(TryUploadAsync)), true),
                new MethodRegistryEntry(DebugMethodRegistry.GetAsyncMethod(typeof(PendingUpload), nameof(TryUploadRecordAsync)), true),
                new MethodRegistryEntry(DebugMethodRegistry.GetAsyncMethod(typeof(PendingUpload), nameof(TryUploadSavGzAsync)), true)
            );
        }
        #endregion

        public static int MaxFileSizeInBytes => 1000 * 1000 * 4;
        private string OrphanedError => $"{nameof(PendingUpload)} ({BonesID}) has been orphaned from its {nameof(ParentHost)}.";
        private static string UploadInvalidPrefix => $"{nameof(PendingUpload)} Invalid";

        public Host ParentHost;

        public string BonesID;

        public BonesRecord BonesRecord;
        private Guid Token;
        public DateTime RecordUploadTime;
        public int UploadAttempts;
        public bool RecordUploaded;

        public BonesSavGz BonesSavGz;
        
        private Timer Timer;

        public bool Success;
        public bool ForceDispose;

        public ResponseInfo Response;

        public Dictionary<string, PendingUpload> PendingUploads => ParentHost?.PendingUploads;
        public int MaxUploadAttempts => ParentHost.MaxUploadAttempts;

        public byte[] SavGz => BonesSavGz.Data;

        public TimeSpan TimeSinceFirstTry => DateTime.UtcNow - RecordUploadTime;

        public PendingUpload()
            : base()
        { }

        [RegisterMethodForDebug]
        public ResponseInfo Attempt(bool Silent = true)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(indent[1]);

            ResponseInfo responseInfo = default;
            try
            {
                if (TryUploadAsync(Silent: Silent, ResponseCallback: ri => responseInfo = ri).WaitResult())
                    return responseInfo;

                return responseInfo = Response;
            }
            finally
            {
                Debug.YehNah($"Response after Attempt: {responseInfo.Message ?? "NO_RESPONSE"}", Good: responseInfo.Success, Indent: indent[1]);
            }
        }

        private string UploadInvalid(string Message = null)
            => $"{UploadInvalidPrefix}{(!Message.IsNullOrEmpty() ? $"; {Message}" : null)}"
            ;

        [RegisterMethodForDebug]
        public bool CheckValid(bool Silent = false)
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Silent), Silent),
                });

            Response = new ResponseInfo
            {
                Host = ParentHost,
                Success = true,
            };

            if (ParentHost == null)
            {
                Response.Message = UploadInvalid($"{nameof(ParentHost)} is null");
                Response.Success = false;
                ForceDispose = true;
            }
            else
            if (BonesID.IsNullOrEmpty())
            {
                Response.Message = UploadInvalid($"{nameof(BonesID)} is null or empty");
                Response.Success = false;
                ForceDispose = true;
            }
            else
            if (!ParentHost.IsRunning)
            {
                Response.StatusCode = HttpStatusCode.InternalServerError;
                Response.Message = UploadInvalid($"{ParentHost} is not running");
                Response.Success = false;
            }
            else
            if (!ParentHost.CheckRegisterUserAsync(StatusCodeCallback: c => Response.StatusCode = c).WaitResult())
            {
                Response.Message = UploadInvalid($"{ParentHost} is refusing access to User {{{Configuration.UserIDString}}}");
                Response.Success = false;
            }
            else
            if (SavGz.Length > MaxFileSizeInBytes)
            {
                Response.Message = UploadInvalid($"{nameof(SavGz)} is {SavGz.FormatBytes()}, limit is {MaxFileSizeInBytes.FormatBytes()} {{{BonesID}}}");
                Response.Success = false;
                ForceDispose = true;
            }
            else
            if (SavGz.IsNullOrEmpty())
            {
                Response.Message = UploadInvalid($"{nameof(SavGz)} is null or empty {{{BonesID}}}");
                Response.Success = false;
                ForceDispose = true;
            }
            else
            if (RecordUploaded
                && Token == default)
            {
                Response.Message = UploadInvalid($"{nameof(Token)} is empty or default {{{BonesID}}}");
                Response.Success = false;
                ForceDispose = true;
            }
            else
            if (!RecordUploaded
                && UploadAttempts++ > MaxUploadAttempts)
            {
                Response.Message = UploadInvalid($"{nameof(UploadAttempts)} exceeds the maximum {MaxUploadAttempts}");
                Response.Success = false;
                ForceDispose = true;
            }
            else
            if (!RecordUploaded
                && ParentHost.HasBones(BonesID))
            {
                Response.Message = UploadInvalid($"{ParentHost} already has a bones file with ID {{{BonesID}}}");
                Response.Success = false;
                ForceDispose = true;
            }
            else
            if (CheckTimeLimitExceeded(out string message, out bool success, out bool forceDispose))
            {
                Response.Message = message;
                Response.Success = success;
                ForceDispose = forceDispose;
            }

            if (Success)
            {
                string isSuccess = $"{nameof(Success)} is {Success}";
                if (Response.Message.IsNullOrEmpty())
                    Response.Message = UploadInvalid(isSuccess);
                else
                    Response.Message += $", {isSuccess}";

                Response.Success = false;
                ForceDispose = true;
            }
            else
            if (ForceDispose)
            {
                string isForceDisposed = $"{nameof(ForceDispose)} is {ForceDispose}";
                if (Response.Message.IsNullOrEmpty())
                    Response.Message = UploadInvalid(isForceDisposed);
                else
                    Response.Message += $", {isForceDisposed}";

                Response.Success = false;
            }

            if (!Silent
                && !Response.Success
                && !Response.Message.IsNullOrEmpty())
                Utils.Warn($"{ParentHost?.ToString() ?? "MISSING_HOST"} :: {Response.Message}.");

            Debug.YehNah($"{nameof(CheckValid)}: {Response.Message ?? "NO_MESSAGE"}", Response.Success, Indent: indent[2]);

            return Response.Success;
        }

        public bool CheckTimeLimitExceeded(out string Message, out bool Success, out bool ForceDispose)
        {
            Message = null;
            Success = true;
            ForceDispose = false;
            if (RecordUploaded
                && TimeSinceFirstTry is TimeSpan { TotalMilliseconds: >= 180000 } timeSinceFirstTry)
            {
                Response.Message = UploadInvalid($"{nameof(TimeSinceFirstTry)} exceeds 3 minute window: {timeSinceFirstTry.ValueUnits()}");
                Response.Success = false;
                ForceDispose = true;
                return false;
            }
            return true;
        }

        [RegisterAsyncMethodForDebug]
        public async Task<bool> TryUploadAsync(
            bool ForceDispose = false,
            bool Silent = true,
            Action<ResponseInfo> ResponseCallback = null
            )
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(ForceDispose), ForceDispose),
                    Debug.Arg(nameof(Silent), Silent),
                });

            if (ForceDispose)
            {
                Debug.CheckNah($"Arg {nameof(ForceDispose)}", ForceDispose, Indent: indent[1]);
                this.ForceDispose = true;
            }

            if (ParentHost == null)
            {
                Debug.CheckNah($"{nameof(ParentHost)} Null", Indent: indent[1]);
                this.ForceDispose = true;
            }

            if (!CheckValid(Silent: Silent))
            {
                ResponseCallback?.Invoke(Response);
                return Response.Success;
            }

            if (!await TryUploadRecordAsync(ResponseCallback))
                return Response.Success;

            if (!await TryUploadSavGzAsync(ResponseCallback))
                return Response.Success;

            bool success = Response.Success;
            // ResponseCallback?.Invoke(Response);
            if (Success
                || this.ForceDispose)
            {
                lock (ParentHost._PendingUploadsLock)
                {
                    var pendingUploads = PendingUploads;
                    Dispose();
                    string pendingSavsCount = (pendingUploads?.Count)?.ToString() ?? "NO_DICTIONARY";
                    Utils.InfoTimestamp($"Disposing of {nameof(PendingUpload)} due to {Response.Message ?? " an unknown reason"}, {nameof(PendingUploads)}: {pendingSavsCount}");
                }
            }
            return success;
        }

        [RegisterAsyncMethodForDebug]
        public async Task<bool> TryUploadRecordAsync(Action<ResponseInfo> ResponseCallback = null)
        {
            string thisMethod = nameof(TryUploadRecordAsync);

            using var indent = new Indent();
            if (RecordUploaded)
            {
                Debug.LogTypeMethod(
                    Indent: indent[1],
                    ArgPairs: new Debug.ArgPair[]
                    {
                        Debug.Arg(nameof(RecordUploaded), RecordUploaded)
                    });

                Response = new ResponseInfo
                {
                    Host = ParentHost,
                    Success = true,
                    Message = $"{nameof(BonesRecord)} Already Uploaded"
                };

                ResponseCallback?.Invoke(Response);
                Debug.YehNah($"{nameof(TryUploadRecordAsync)}: {Response.Message ?? "NO_MESSAGE"}", Response.Success, Indent: indent[2]);
                return Response.Success;
            }

            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(RecordUploaded), RecordUploaded),
                });

            if (!CheckValid(Silent: true))
            {
                ResponseCallback?.Invoke(Response);
                return Response.Success;
            }

            try
            {
                Response = await PostBonesInfoAsync();
                ResponseCallback?.Invoke(Response);
            }
            catch (WebException x)
            {
                ForceDispose = true;

                Response.WantTimer = false;
                Response.Message = $"Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during response from {ToString()}";
                Utils.Warn($"{thisMethod}: {Response.Message ?? "NO_MESSAGE"}", x);
            }
            catch (Exception x)
            {
                ForceDispose = true;

                Response.Success = false;
                Response.Message ??= $"Caught {x.GetType().ToStringWithGenerics()} during response from {ToString()}";
                Utils.Error($"{thisMethod}: {Response.Message ?? "NO_MESSAGE"}", x);
            }
            finally
            {
                Debug.YehNah($"{nameof(TryUploadRecordAsync)}.{nameof(PostBonesInfoAsync)}: {Response.Message ?? "NO_MESSAGE"}", Response.Success, Indent: indent[2]);
            }

            return Response.Success;
        }

        [RegisterAsyncMethodForDebug]
        public async Task<bool> TryUploadSavGzAsync(Action<ResponseInfo> ResponseCallback = null)
        {
            string thisMethod = nameof(TryUploadSavGzAsync);

            using var indent = new Indent();
            if (Success)
            {
                Debug.LogTypeMethod(
                    Indent: indent[1],
                    ArgPairs: new Debug.ArgPair[]
                    {
                        Debug.Arg(nameof(Success), Success)
                    });

                Response = new ResponseInfo
                {
                    Host = ParentHost,
                    Success = true,
                    Message = $"{nameof(BonesSavGz)} Already Uploaded"
                };

                ResponseCallback?.Invoke(Response);
                Debug.YehNah($"{thisMethod}: {Response.Message ?? "NO_MESSAGE"}", Response.Success, Indent: indent[2]);
                return Response.Success;
            }

            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Success), Success)
                });

            if (!CheckValid(Silent: true))
            {
                ResponseCallback?.Invoke(Response);
                return Response.Success;
            }

            var timeSinceFirstTry = TimeSinceFirstTry;

            string bonesID = $"{{{BonesID ?? "NO_BONES_ID"}}}";
            string parentHost = $"{ParentHost?.ToString() ?? "NO_HOST"}";
            string timeSince = $"{timeSinceFirstTry.ValueUnits()}";

            Utils.InfoTimestamp($"{nameof(PendingUpload)} attempting to {nameof(TryUploadSavGzAsync)} of {bonesID} to {parentHost}, time since first try: {timeSince}");
            bool success = false;
            try
            {
                Response = await PutBonesSavGzAsync();
                ResponseCallback?.Invoke(Response);
            }
            catch (WebException x)
            {
                ForceDispose = true;

                Response.WantTimer = false;
                Response.Message = $"Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during response from {ToString()}";
                Utils.Warn($"{thisMethod}: {Response.Message ?? "NO_MESSAGE"}", x);
            }
            catch (Exception x)
            {
                ForceDispose = true;

                Response.Success = false;
                Response.Message ??= $"Caught {x.GetType().ToStringWithGenerics()} during response from {ToString()}";
                Utils.Error($"{thisMethod}: {Response.Message ?? "NO_MESSAGE"}", x);
            }
            finally
            {
                Debug.YehNah($"{nameof(TryUploadSavGzAsync)}.{nameof(PutBonesSavGzAsync)}: {Response.Message ?? "NO_MESSAGE"}", Response.Success, Indent: indent[2]);
                success = Response.Success;
                if (Response.WantTimer
                    && !Response.Cancel)
                {
                    SetUpTimer();
                }
                else
                if (!HasTimer()
                    || Response.Cancel)
                {
                    ForceDispose = true;
                    Dispose();
                }
            }
            return success;
        }

        public void SetToken(Guid Token)
        {
            this.Token = Token;
            if (this.Token != default)
            {
                RecordUploaded = true;
                RecordUploadTime = DateTime.UtcNow;
            }
        }

        public HttpWebRequest AppendAuthHeader(HttpWebRequest HttpReq)
        {
            if (HttpReq == null)
                return null;

            if (Token != default)
                HttpReq.Headers.AppendWebHeader(HttpRequestHeader.Authorization, $"basic {Token}");

            return HttpReq;
        }

        private TimerCallback GetTimerCallback()
            => async delegate (object state)
            {
                if (ParentHost == null)
                {
                    Utils.Error(OrphanedError);
                    return;
                }

                if (state is not string bonesID
                    || bonesID.IsNullOrEmpty())
                {
                    Utils.Error($"{nameof(Timer)} passed invalid {nameof(BonesID)} ({state?.ToString() ?? "NO_STATE"}).");
                    return;
                }

                PendingUpload pendingUpload = null;
                lock (ParentHost._PendingUploadsLock)
                {
                    if (!PendingUploads.TryGetValue(bonesID, out pendingUpload))
                    {
                        Utils.Warn($"{nameof(Timer)} passed {nameof(BonesID)} not found in {nameof(PendingUploads)} ({bonesID ?? "NO_BONES_ID"}).");
                        return;
                    }
                }

                if (!await pendingUpload.TryUploadAsync()
                    && pendingUpload.CheckTimeLimitExceeded(out _, out _, out _))
                    pendingUpload.Dispose();
            }
            ;

        private Timer GetTimer()
        {
            if (ParentHost == null)
            {
                Utils.Error(OrphanedError);
                return null;
            }

            lock (ParentHost._PendingUploadsLock)
                Utils.InfoTimestamp($"Starting timer to attempts {nameof(PendingUpload)} of {{{BonesID}}} to {ParentHost}. {nameof(PendingUploads)}: {PendingUploads.Count}");

            return new(GetTimerCallback(),
                state: BonesID,
                dueTime: 45000, // every 45 seconds
                period: 45000)
                ;
        }

        public bool HasTimer()
            => Timer != null
            ;

        public void SetUpTimer()
        {
            if (CheckValid(Silent: true))
            {
                Timer ??= GetTimer();
            }
        }

        public async Task<ResponseInfo> PostBonesInfoAsync([CallerMemberName] string CallingMethod = "")
        {
            var responseInfo = new ResponseInfo
            {
                Host = ParentHost,
                Success = false,
                StatusCode = HttpStatusCode.InternalServerError,
            };

            if (ParentHost.CreatePostBonesInfoRequest(this, out string uRI) is not HttpWebRequest httpReq)
                return responseInfo;

            HttpWebResponse httpRes = null;
            try
            {
                httpRes = await ParentHost.GetPostBonesInfoResponseAsync(httpReq, uRI, c => responseInfo.StatusCode = c);
                if (httpRes == null)
                    return responseInfo;

                responseInfo.StatusCode = httpRes.StatusCode;
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.Created)
                    {
                        if (JObject.Parse(result) is JObject jObject
                            && jObject["UploadToken"].ToObject<Guid>() is Guid parsedToken)
                        {
                            SetToken(parsedToken);

                            responseInfo.Success = true;
                            responseInfo.Message = $"Successfully created new BonesInfo on server at {ToString()}";

                            Utils.Info($"{responseInfo.Message} - {httpRes.StatusCode.DisplayString()} - {nameof(parsedToken)}: {parsedToken}");
                            // Utils.Log(jObject);
                        }
                        else
                        {
                            responseInfo.Message = $"{CallingMethod} received unexpected data from {ToString()}";
                            Utils.Warn($"{responseInfo.Message} - {httpRes.StatusCode.DisplayString()} - {result}");
                        }
                    }
                    else
                    {
                        responseInfo.Message = $"{CallingMethod} received unexpected status code from {ToString()}";
                        Utils.Warn($"{responseInfo.Message} - {httpRes.StatusCode.DisplayString()}");
                    }
                }
            }
            catch (WebException x)
            {
                if (x.Status != WebExceptionStatus.Timeout)
                    throw new WebException($"Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during response from {ToString()}", x, x.Status, httpRes);

                ForceDispose = true;

                responseInfo.WantTimer = false;
                responseInfo.Message = $"Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during response from {ToString()}";

                throw new WebException(responseInfo.Message, x, x.Status, httpRes);
            }
            catch (Exception x)
            {
                responseInfo.Success = false;
                responseInfo.Message ??= $"Caught {x.GetType().ToStringWithGenerics()} during response from {ToString()}";
            }
            return responseInfo;
        }

        public async Task<ResponseInfo> PutBonesSavGzAsync([CallerMemberName] string CallingMethod = "")
        {
            var responseInfo = new ResponseInfo
            {
                Host = ParentHost,
                Success = false,
                WantTimer = !HasTimer(),
            };

            if (ParentHost.CreatePutBonesSavGzRequest(this, out string uRI) is not HttpWebRequest httpReq)
                return responseInfo;

            HttpWebResponse httpRes = null;
            try
            {
                httpRes = await ParentHost.GetPutBonesSavGzResponseAsync(httpReq, uRI, Rethrow: true);
                if (httpRes == null)
                    return responseInfo;

                responseInfo.StatusCode = httpRes.StatusCode;
                using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                {
                    var result = await streamReader.ReadToEndAsync();
                    if (httpRes.StatusCode == HttpStatusCode.Created)
                    {
                        if (JObject.Parse(result) is JObject jObject
                            && jObject[nameof(BonesID)].ToObject<string>() is string returnedBonesID
                            && returnedBonesID == BonesID
                            && jObject[nameof(SavGz)].ToObject<string>() is string returnedSavGz
                            && !returnedSavGz.IsNullOrEmpty())
                        {
                            ParentHost.ClearBonesIDCache();
                            responseInfo.Message = $"Successfully added \".sav.gz\" blob to Bones on server at {ToString()}";
                            responseInfo.WantTimer = false;
                            responseInfo.Success = true;
                            Utils.Info($"{responseInfo.Message} - {httpRes.StatusCode.DisplayString()} - \n{jObject}");
                        }
                        else
                        {
                            responseInfo.Message = $"{CallingMethod} received unexpected data from {ToString()}";
                            Utils.Warn($"{responseInfo.Message} - {httpRes.StatusCode.DisplayString()} - {result}");
                        }
                    }
                    else
                    {
                        responseInfo.Message = $"{CallingMethod} received unexpected status code from {ToString()}";
                        Utils.Warn($"{responseInfo.Message} - {httpRes.StatusCode.DisplayString()}");
                    }
                }
            }
            catch (WebException x)
            {
                if (x.Status != WebExceptionStatus.Timeout)
                    throw new WebException($"Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during response from {ToString()}", x, x.Status, httpRes);

                if (httpRes.StatusCode == HttpStatusCode.Unauthorized)
                {
                    using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
                    {
                        var result = await streamReader.ReadToEndAsync();

                        if (JObject.Parse(result) is not JObject jObject
                            || !jObject.TryGetValue("error", out var errorToken)
                            || errorToken.ToObject<string>() is not string errorString)
                        {
                            responseInfo.Message = $"{CallingMethod} received unexpected data from {ToString()}";
                            Utils.Warn($"{responseInfo.Message} - {httpRes.StatusCode.DisplayString()} - {result}");
                        }
                        else
                        if (jObject.TryGetValue("cancel", out var cancelToken)
                            && cancelToken.ToObject<bool>() is bool cancel
                            && cancel)
                        {
                            ForceDispose = true;

                            responseInfo.WantTimer = false;
                            responseInfo.Success = false;
                            responseInfo.Cancel = true;

                            responseInfo.Message = $"Instructed to cancel pending upload of {{{BonesID}}} by server at {ToString()}";
                            Utils.Info($"{responseInfo.Message}: {errorString} - {httpRes.StatusCode.DisplayString()} - \n{jObject}");
                            return responseInfo;
                        }
                    }
                }

                ForceDispose = true;

                responseInfo.WantTimer = false;
                responseInfo.Message = $"Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during response from {ToString()}";

                throw new WebException(responseInfo.Message, x, x.Status, httpRes);
            }
            catch (Exception x)
            {
                responseInfo.Success = false;
                responseInfo.Message ??= $"Caught {x.GetType().ToStringWithGenerics()} during response from {ToString()}";
            }            return responseInfo;
        }

        [RegisterMethodForDebug]
        protected void DisposeInternal()
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(indent[1]);

            if (ParentHost != null)
                lock (ParentHost._PendingUploadsLock)
                    ParentHost?.PendingUploads?.Remove(BonesID);

            ParentHost = null;

            BonesID = null;

            BonesRecord?.Dispose();
            BonesRecord = null;
            Token = default;
            RecordUploadTime = default;
            UploadAttempts = 0;
            RecordUploaded = false;

            BonesSavGz?.Dispose();
            BonesSavGz = null;

            Timer?.Dispose();
            Timer = null;

            Success = false;
            ForceDispose = false;

            Response = default;
        }

        [RegisterMethodForDebug]
        public void Dispose()
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(
                Indent: indent[1],
                ArgPairs: new Debug.ArgPair[]
                {
                    Debug.Arg(nameof(Success), Success),
                    Debug.Arg(nameof(ForceDispose), ForceDispose),
                });

            if (Success
                || ForceDispose)
                DisposeInternal();
            else
                TryUploadAsync(ForceDispose: true).Wait();
        }
    }
}
