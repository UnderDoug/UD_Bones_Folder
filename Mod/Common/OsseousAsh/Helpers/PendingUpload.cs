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

namespace UD_Bones_Folder.Mod.OsseousAsh
{
    public class PendingUpload : IDisposable
    {
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

        public ResponseInfo Attempt()
        {
            TryUploadAsync().WaitResult();
            return Response;
        }

        private string UploadInvalid(string Message = null)
            => $"{UploadInvalidPrefix}{(!Message.IsNullOrEmpty() ? $"; {Message}" : null)}"
            ;

        public bool CheckValid(bool Silent = false)
        {
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
            if (!ParentHost.CheckRegisterUserAsync().WaitResult())
            {
                Response.StatusCode = HttpStatusCode.InternalServerError;
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
            if (!RecordUploaded
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
            if (RecordUploaded
                && TimeSinceFirstTry is TimeSpan timeSinceFirstTry
                && timeSinceFirstTry.TotalMilliseconds >= 180000)
            {
                Response.Message = UploadInvalid($"{nameof(TimeSinceFirstTry)} exceeds 3 minute window: {timeSinceFirstTry.ValueUnits()}");
                Response.Success = false;
                ForceDispose = true;
            }
            if (Success)
            {
                Response.Message = UploadInvalid($"{nameof(Success)} is {Success}");
                Response.Success = false;
                ForceDispose = true;
            }
            else
            if (ForceDispose)
            {
                Response.Message = UploadInvalid($"{nameof(ForceDispose)} is {ForceDispose}");
                Response.Success = false;
            }

            if (!Silent
                && !Response.Success
                && !Response.Message.IsNullOrEmpty())
                Utils.Warn($"{ParentHost?.ToString() ?? "MISSING_HOST"} :: {Response.Message}.");

            return Response.Success;
        }

        public async Task<bool> TryUploadAsync(bool ForceDispose = false)
        {
            if (ForceDispose)
                this.ForceDispose = true;

            if (ParentHost == null)
                this.ForceDispose = true;

            if (!CheckValid(Silent: true))
                return Response.Success;

            if (!await TryUploadRecordAsync())
                return Response.Success;

            if (!await TryUploadSavGzAsync())
                return Response.Success;

            if (Success
                || this.ForceDispose)
            {
                var pendingUploads = PendingUploads;
                Dispose();
                string pendingSavsCount = (pendingUploads?.Count)?.ToString() ?? "NO_DICTIONARY";
                Utils.InfoTimestamp($"Disposing of {nameof(PendingUpload)} due to {Response.Message ?? " an unknown reason"}. {nameof(PendingUploads)}: {pendingSavsCount}");
            }
            return Response.Success;
        }

        public async Task<bool> TryUploadRecordAsync()
        {
            if (RecordUploaded)
            {
                Response = new ResponseInfo
                {
                    Host = ParentHost,
                    Success = true,
                    Message = $"{nameof(BonesRecord)} Already Uploaded"
                };

                return Response.Success;
            }

            if (!CheckValid(Silent: true))
                return Response.Success;

            Response = await PostBonesInfoAsync();
            return Response.Success;
        }

        public async Task<bool> TryUploadSavGzAsync()
        {
            if (Success)
            {
                Response = new ResponseInfo
                {
                    Host = ParentHost,
                    Success = true,
                    Message = $"{nameof(BonesSavGz)} Already Uploaded"
                };
                return Response.Success;
            }

            if (!CheckValid(Silent: true))
                return Response.Success;

            var timeSinceFirstTry = TimeSinceFirstTry;

            string bonesID = $"{{{BonesID ?? "NO_BONES_ID"}}}";
            string parentHost = $"{ParentHost?.ToString() ?? "NO_HOST"}";
            string timeSince = $"{timeSinceFirstTry.ValueUnits()}";

            Utils.InfoTimestamp($"{nameof(PendingUpload)} attempting to {nameof(TryUploadSavGzAsync)} of {bonesID} to {parentHost}, time since first try: {timeSince}");

            Response = await PutBonesSavGzAsync();

            if (Response.WantTimer)
            {
                SetUpTimer();
            }
            else
            if (!HasTimer())
            {
                ForceDispose = true;
                Dispose();
            }

            return Response.Success;
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

                if (PendingUploads.TryGetValue(bonesID, out var pendingUpload))
                    await pendingUpload.TryUploadAsync();
                else
                    Utils.Warn($"{nameof(Timer)} passed {nameof(BonesID)} not found in {nameof(PendingUploads)} ({bonesID ?? "NO_BONES_ID"}).");
            }
            ;

        private Timer GetTimer()
        {
            if (ParentHost == null)
            {
                Utils.Error(OrphanedError);
                return null;
            }

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
            };

            if (ParentHost.CreatePostBonesInfoRequest(this, out string uRI) is not HttpWebRequest httpReq
                || await ParentHost.GetPostBonesInfoResponseAsync(httpReq, uRI) is not HttpWebResponse httpRes)
                return responseInfo;

            responseInfo.StatusCode = httpRes.StatusCode;
            using (var streamReader = new StreamReader(httpRes.GetResponseStream()))
            {
                var result = await streamReader.ReadToEndAsync();
                if (httpRes.StatusCode == HttpStatusCode.Created)
                {
                    if (JObject.Parse(result) is JObject jObject
                        && jObject["success"].ToObject<Guid>() is Guid parsedToken)
                    {
                        SetToken(parsedToken);

                        responseInfo.Success = true;
                        responseInfo.Message = $"Successfully created new BonesInfo on server at {ToString()}";

                        Utils.Info($"{responseInfo.Message} - {httpRes.StatusCode.DisplayString()} - {nameof(parsedToken)}: {parsedToken}");
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

            try
            {
                if (ParentHost.CreatePutBonesSavGzRequest(this, out string uRI) is not HttpWebRequest httpReq
                    || await ParentHost.GetPutBonesSavGzResponseAsync(httpReq, uRI, Rethrow: true) is not HttpWebResponse httpRes)
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
                            Utils.Info($"{responseInfo.Message} - {httpRes.StatusCode.DisplayString()} - {jObject}");
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
                    throw x;

                ForceDispose = true;

                responseInfo.WantTimer = false;
                responseInfo.Message = $"Caught {x.GetType().Name} with {nameof(WebExceptionStatus)} {x.Status} during response from {ToString()}";

                throw x;
            }
            catch (Exception x)
            {
                responseInfo.Success = false;
                responseInfo.Message ??= $"Caught {x.GetType().ToStringWithGenerics()} during response from {ToString()}";
            }          
            return responseInfo;
        }

        protected void DisposeInternal()
        {
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

        public void Dispose()
        {
            if (Success
                || ForceDispose)
                DisposeInternal();
            else
                TryUploadAsync(ForceDispose: true).Wait();
        }
    }
}
