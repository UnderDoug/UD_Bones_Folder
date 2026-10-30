using System;
using System.Collections.Generic;
using System.Text;

using Stream = System.IO.Stream;
using MemoryStream = System.IO.MemoryStream;

using UD_Bones_Folder.Mod.BonesSystem;

namespace UD_Bones_Folder.Mod.Serialization
{
    public class BonesSavGz : IBonesFileElement, IDisposable
    {
        public string BonesID { get; set; }

        /// <summary>
        /// Intended for reading a bones file.
        /// </summary>
        public Stream Stream;

        /// <summary>
        /// Intended for writing a bones file. 
        /// </summary>
        public byte[] Data;

        public bool IsRead => Stream != null
            ;

        public bool IsWrite => Data != null
            ;

        public BonesSavGz()
            : base()
        { }

        public BonesSavGz(string BonesID, Stream Stream)
            : this()
        {
            this.BonesID = BonesID;
            this.Stream = Stream;
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
            Stream.CopyTo(Other.Stream);
            Array.Copy(Data, Other.Data, Data.Length);
            return Other;
        }

        public BonesSavGz Clone()
            => CopyTo(new BonesSavGz())
            ;

        public BonesSavGz EnsureSavable()
        {
            if (IsWrite)
                return this;

            if (!IsRead)
                throw new InvalidOperationException($"{nameof(Data)} cannot be constructed from null {nameof(Stream)}");

            Data = Stream.ReadAllBytes();
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

        public BonesSavGz EnsureLoadable()
        {
            if (IsRead)
                return this;

            if (!IsWrite)
                throw new InvalidOperationException($"{nameof(Stream)} cannot be constructed from null {nameof(Data)}");

            Stream = new MemoryStream(Data);
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

        protected void DisposeStream()
        {
            Stream?.Dispose();
            Stream = null;
        }

        protected void DisposeData()
        {
            if (Data != null)
                Array.Clear(Data, 0, Data.Length);
            Data = null;
        }

        public void Dispose()
        {
            BonesID = null;
            DisposeStream();
            DisposeData();
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
