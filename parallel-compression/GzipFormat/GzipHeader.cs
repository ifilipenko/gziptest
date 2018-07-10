using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Parallel.Compression.Helpers;
using Parallel.Compression.Models;

namespace Parallel.Compression.GzipFormat
{
    internal struct GzipHeader
    {
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        private static class OsFlags
        {
            private const byte FAT = 0;
            private const byte Amiga = 1;
            private const byte VMS = 2;
            private const byte Unix = 3;
            private const byte VM_CMS = 4;
            private const byte Atari_TOS = 5;
            private const byte HPFS_filesystem = 6;
            private const byte Macintosh = 7;
            private const byte Z_System = 8;
            private const byte CP_M = 9;
            private const byte TOPS_20 = 10;
            private const byte NTFS_filesystem = 11;
            private const byte QDOS = 12;
            private const byte Acorn_RISCOS = 13;
            private const byte OSX = 19;
            private const byte Unknown = 255;

            public static readonly byte[] All = {FAT, Amiga, VMS, Unix, VM_CMS, Atari_TOS, HPFS_filesystem, Macintosh, Z_System, CP_M, TOPS_20, NTFS_filesystem, QDOS, Acorn_RISCOS, OSX, Unknown};
        }

        private static readonly ByteMask[] HeaderMask = {31, 139, 8, ByteMask.Any, ByteMask.Any, ByteMask.Any, ByteMask.Any, ByteMask.Any, ByteMask.Any, OsFlags.All};
        public static readonly int Length = HeaderMask.Length;

        // todo: test it
        public static bool IsHeader(ArraySegment<byte> arraySegment)
        {
            if (arraySegment.Array == null)
                return false;

            return arraySegment.Count == Length && arraySegment.IsMatchToMask(0, HeaderMask);
        }
        
        public static bool IsPrefixFor(ArraySegment<byte> arraySegment)
        {
            if (arraySegment.Array == null)
                return false;

            return arraySegment.IsMatchToMask(0, HeaderMask);
        }

        // todo: test it
        public static IEnumerable<GzipHeader> FindAllHeaders(ArraySegment<byte> arraySegment)
        {
            var bytes = arraySegment.Array;
            if (bytes == null)
                yield break;

            while (arraySegment.Count > 0)
            {
                var gzipHeader = FindFirst(arraySegment);
                if (gzipHeader == null)
                    yield break;

                yield return gzipHeader.Value;

                var newOffset = gzipHeader.Value.Position + Length;
                var count = arraySegment.Count - (newOffset - arraySegment.Offset);
                if (count < Length)
                    yield break;
                
                arraySegment = new ArraySegment<byte>(bytes, newOffset, count);
            }
        }

        // todo: test it
        public static int GetOffsetOfMatchedPartFromEnd(ArraySegment<byte> arraySegment)
        {
            var bytes = arraySegment.Array;
            if (bytes == null)
                return -1;

            var biggestPartSize = Math.Min(arraySegment.Count, HeaderMask.Length - 1);
            for (var partSize = biggestPartSize; partSize > 0; partSize--)
            {
                if (arraySegment.IsMatchedPartiallyFromEnd(HeaderMask, partSize))
                {
                    return arraySegment.Offset + arraySegment.Count - partSize;
                }
            }

            return -1;
        }

        // todo: test it
        public static ArraySegment<byte> AfterHeader(ArraySegment<byte> arraySegment)
        {
            return arraySegment.ShiftOffsetRight(Length);
        }

        public static GzipHeader? FindFirst(ArraySegment<byte> arraySegment)
        {
            var bytes = arraySegment.Array;
            if (bytes == null || arraySegment.Count == 0)
                return null;

            for (var i = 0; i < arraySegment.Count - HeaderMask.Length + 1; i++)
            {
                if (arraySegment.IsMatchToMask(i, HeaderMask))
                {
                    return new GzipHeader(new ArraySegment<byte>(bytes, i + arraySegment.Offset, Length));
                }
            }

            return null;
        }

        private readonly ArraySegment<byte> bytes;

        internal GzipHeader(ArraySegment<byte> bytes)
        {
            this.bytes = bytes;
        }

        public ArraySegment<byte> Bytes => bytes;
        public ArraySegment<byte> MimetypeBytes => bytes.Array?.Segment(bytes.Offset + 4, 4) ?? default(ArraySegment<byte>);
        public int Position => bytes.Offset;
        public int EndPosition => bytes.Offset + bytes.Count - 1; // todo: test it

        public int GetMimetypeAsInt()
        {
            var mimetypes = MimetypeBytes;
            if (mimetypes.Array == null)
                return 0;

            return BitConverter.ToInt32(mimetypes.Array, mimetypes.Offset);
        }

        [Pure]
        [SuppressMessage("ReSharper", "PureAttributeOnVoidMethod")]
        public void SetMimetypeBytes([NotNull] byte[] mimetype)
        {
            if (bytes.Array == null)
                throw new InvalidOperationException("Can't set mimetype bytes to default header");

            if (mimetype == null)
                throw new ArgumentNullException(nameof(mimetype));

            if (mimetype.Length != 4)
                throw new ArgumentException($"Mime types can have only 4 bytes but given array have {mimetype.Length}", nameof(mimetype));

            Buffer.BlockCopy(mimetype, 0, bytes.Array, bytes.Offset + 4, 4);
        }
    }
}