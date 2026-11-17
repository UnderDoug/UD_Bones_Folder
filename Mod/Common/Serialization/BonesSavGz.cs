using System;
using System.Collections.Generic;
using System.Text;

using Stream = System.IO.Stream;
using MemoryStream = System.IO.MemoryStream;

using UD_Bones_Folder.Mod.BonesSystem;
using UD_Bones_Folder.Mod.Logging;

namespace UD_Bones_Folder.Mod.Serialization
{
    [HasRegisterMethodForDebug]
    public class BonesSavGz : IBonesFileElement, IDisposable
    {
        public string BonesID { get; set; }

        /// <summary>
        /// Intended for reading a bones file.
        /// </summary>
        public MemoryStream Stream;

        /// <summary>
        /// Intended for writing a bones file. 
        /// </summary>
        public byte[] Data;

        /// <summary>
        /// Able to read bytes from <see cref="Stream"/>.
        /// </summary>
        public bool CanRead => Stream != null
            ;

        /// <summary>
        /// Able to write bytes from <see cref="Data"/>.
        /// </summary>
        public bool CanWrite => Data != null
            ;

        public BonesSavGz()
            : base()
        { }

        public BonesSavGz(string BonesID, MemoryStream Stream)
            : this()
        {
            this.BonesID = BonesID;
            this.Stream = Stream;
        }

        public BonesSavGz(string BonesID, Stream Stream)
            : this()
        {
            this.BonesID = BonesID;
            this.Stream = new();
            Stream.CopyToAsync(this.Stream).Wait();
            Stream.Close();
        }

        public BonesSavGz(string BonesID, byte[] Data)
            : this()
        {
            this.BonesID = BonesID;
            this.Data = Data;
        }

        public BonesSavGz CopyTo(BonesSavGz Other)
        {
            Other ??= new();
            Other.BonesID = BonesID;
            if (Stream != null)
            {
                Other.Stream = new MemoryStream((int)Stream.Length);
                Stream.CopyTo(Other.Stream);
            }
            if (Data != null)
            {
                Other.Data = new byte[Data.Length];
                Array.Copy(Data, Other.Data, Data.Length);
            }
            return Other;
        }

        public BonesSavGz Clone()
            => CopyTo(new BonesSavGz())
            ;


        [RegisterMethodForDebug(true)]
        public BonesSavGz EnsureSavable()
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(indent[1]);

            if (CanWrite)
            {
                Debug.CheckYeh(nameof(CanWrite), Indent: indent[2]);
                return this;
            }

            if (!CanRead)
            {
                Debug.CheckNah($"!{nameof(CanRead)}", nameof(InvalidOperationException), Indent: indent[2]);
                throw new InvalidOperationException($"{nameof(Data)} cannot be constructed from null {nameof(Stream)}");
            }

            Stream.Position = 0L;
            Data = Stream.GetAllBytes();
            Debug.CheckYeh($"{nameof(Stream)}.{nameof(Extensions.GetAllBytes)}", Indent: indent[2]);

            DisposeStream();
            return this;
        }

        public bool TryEnsureSavable()
        {
            try
            {
                EnsureSavable();
                return true;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BonesSavGz)} failed trying to {nameof(EnsureSavable)}", x);
                return false;
            }
        }

        [RegisterMethodForDebug(true)]
        public BonesSavGz EnsureLoadable()
        {
            using var indent = new Indent();
            Debug.LogTypeMethod(indent[1]);

            if (CanRead)
            {
                Stream.Position = 0L;
                Debug.CheckYeh(nameof(CanRead), Indent: indent[2]);
                return this;
            }

            if (!CanWrite)
            {
                Debug.CheckNah($"!{nameof(CanWrite)}", nameof(InvalidOperationException), Indent: indent[2]);
                throw new InvalidOperationException($"{nameof(Stream)} cannot be constructed from null {nameof(Data)}");
            }

            Stream = new MemoryStream(Data);
            Debug.CheckYeh($"new {nameof(MemoryStream)}", Indent: indent[2]);

            DisposeData();
            return this;
        }

        public bool TryEnsureLoadable()
        {
            try
            {
                EnsureLoadable();
                return true;
            }
            catch (Exception x)
            {
                Utils.Error($"{nameof(BonesSavGz)} failed trying to {nameof(EnsureLoadable)}", x);
                return false;
            }
        }

        protected void DisposeStream(bool Clear = false)
        {
            Stream?.Close();

            if (Clear)
                Stream?.Dispose();

            Stream = null;
        }

        protected void DisposeData(bool Clear = false)
        {
            if (Data != null
                && Clear)
                Array.Clear(Data, 0, Data.Length);
            Data = null;
        }

        public void Dispose(bool Clear)
        {
            BonesID = null;
            DisposeStream(Clear: Clear);
            DisposeData(Clear: Clear);
        }

        public void Dispose()
        {
            Dispose(false);
        }

        public static explicit operator Stream(BonesSavGz Operand)
            => Operand?.TryEnsureSavable() is true
            ? Operand.Stream
            : null
            ;

        public static explicit operator byte[](BonesSavGz Operand)
            => Operand?.TryEnsureLoadable() is true
            ? Operand.Data
            : null
            ;
    }
}
