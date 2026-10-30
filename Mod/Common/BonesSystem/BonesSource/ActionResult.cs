using System;
using System.Collections.Generic;
using System.Text;

namespace UD_Bones_Folder.Mod.BonesSystem.BonesSource
{
    public struct ActionResult
    {
        /// <summary>
        /// Indicates the general location from and to which the Bones Source will save and load bones.
        /// </summary>
        [Serializable]
        public enum ResultType
        {
            /// <summary>The action failed.</summary>
            /// <remarks><see langword="default"/> result.</remarks>
            Failed,

            /// <summary>The action completed but encountered issues.</summary>
            CompletedWithWarnings,

            /// <summary>The action is pending.</summary>
            Pending,

            /// <summary>The action was compeltely successful.</summary>
            Success,
        }

        public ResultType Type;
        public string Message;

        public static ActionResult NewSuccess()
            => new ActionResult
            {
                Type = ResultType.Success,
            };

        public static ActionResult NewPending()
            => new ActionResult
            {
                Type = ResultType.Pending,
            };

        public static ActionResult NewFailed()
            => new ActionResult
            {
                Type = ResultType.Failed,
            };

        public static ResultType LeastSuccesful(ResultType TypeX, ResultType TypeY)
            => (ResultType)Math.Min((int)TypeX, (int)TypeY)
            ;

        public override readonly string ToString()
            => !Message.IsNullOrEmpty()
            ? Message
            : Type.ToString()
            ;

        public ActionResult SetMessage(string Message)
        {
            this.Message = Message;
            return this;
        }

        public ActionResult SetCaught(Exception X)
            => SetMessage($"Caught {X.GetType().ToStringWithGenerics()} during exection")
            ;

        public ActionResult AppendMessage(string Message)
        {
            if (!Message.IsNullOrEmpty())
            {
                if (!this.Message.IsNullOrEmpty())
                    this.Message += "; ";
                this.Message = Message;
            }
            return this;
        }

        public ActionResult AppendCaught(Exception X)
            => AppendMessage($"Caught {X.GetType().ToStringWithGenerics()} during exection")
            ;

        public ActionResult SetType(ResultType Type)
        {
            this.Type = Type;
            return this;
        }

        public ActionResult SetSuccess()
            => SetType(ResultType.Success)
            ;

        public ActionResult SetPending()
            => SetType(ResultType.CompletedWithWarnings)
            ;

        public ActionResult SetCompletedWithWarnings()
            => SetType(ResultType.CompletedWithWarnings)
            ;

        public ActionResult SetFailed()
            => SetType(ResultType.Failed)
            ;

        public ActionResult MergeWith(ActionResult Other)
        {
            if (Other == default)
                return this;

            AppendMessage(Other.Message);

            Type = LeastSuccesful(Type, Other.Type);

            return this;
        }

        public static implicit operator bool(ActionResult Operand)
            => Operand.Type > ResultType.Failed
            ;
    }
}
