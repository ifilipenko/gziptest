using System;
using System.Collections.Generic;
using System.Linq;
using Parallel.Compression.Helpers;

namespace Parallel.Compression.GzipFormat
{
    internal struct GzipBuffer
    {
        private readonly IReadOnlyList<GzipHeader> headers;
        private readonly int? possiblePartOfHeaderAtTheEnd;

        public GzipBuffer(ArraySegment<byte> bytes)
        {
            if (bytes.Array == null)
                throw new ArgumentNullException(nameof(bytes));

            possiblePartOfHeaderAtTheEnd = null;
            Bytes = bytes;

            var allHeaders = GzipHeader.FindAllHeaders(bytes).ToList();
            if (allHeaders.Count == 0 || allHeaders.Last().EndPosition < bytes.LastOffset())
            {
                var partiallyMatchedOffset = GzipHeader.GetOffsetOfMatchedPartFromEnd(bytes);
                if (partiallyMatchedOffset >= 0)
                {
                    possiblePartOfHeaderAtTheEnd = partiallyMatchedOffset;
                }
            }

            headers = allHeaders;
        }

        private GzipBuffer(
            ArraySegment<byte> bytes,
            IReadOnlyList<GzipHeader> headers,
            int? possiblePartOfHeaderAtTheEnd)
        {
            Bytes = bytes;
            this.headers = headers;
            this.possiblePartOfHeaderAtTheEnd = possiblePartOfHeaderAtTheEnd;
        }

        public ArraySegment<byte> Bytes { get; }
        public bool IsEmpty => Bytes.Count == 0;
        // todo: test falgs
        public bool ContainsOnlyOneCompressedBlockFromStart => headers != null && 
                                                               headers.Count == 1 &&
                                                               possiblePartOfHeaderAtTheEnd == null &&
                                                               headers[0].Position == Bytes.Offset;
        public bool IsStartsWithCompressedBlock => headers != null && 
                                                   headers.Count > 0 &&
                                                   headers[0].Position == Bytes.Offset;
        public bool IsContainAtLestOneWholeBlock => headers != null && headers.Count > 1;
        public bool NoHeadersOrParts => NoHeaders && possiblePartOfHeaderAtTheEnd == null;
        public bool NoHeaders => headers == null || headers.Count == 0;
        public bool ContainsOnlyPart => Bytes.Offset == possiblePartOfHeaderAtTheEnd;
        public IReadOnlyList<GzipHeader> Headers => headers ?? Empty<GzipHeader>.List;
        public int? PossiblePartOfHeaderAtTheEnd => possiblePartOfHeaderAtTheEnd;

        public GzipBuffer ToOwnedBuffer()
        {
            if (Bytes.Array == null)
                return default;

            var srcOffset = Bytes.Offset;
            var copy = new byte[Bytes.Count]; // todo: use array pool
            Buffer.BlockCopy(Bytes.Array, srcOffset, copy, 0, copy.Length);
            var arraySegment = copy.ToSegment();

            var newHeaders = headers.Select(x => new GzipHeader(copy.Segment(x.Position - srcOffset, x.Bytes.Count))).ToList();

            return new GzipBuffer(arraySegment, newHeaders, possiblePartOfHeaderAtTheEnd - srcOffset);
        }

        public (ArraySegment<byte> BeforeHeader, GzipBuffer AfterHeader) CutFirstBlock()
        {
            var bytesArray = Bytes.Array;
            if (bytesArray == null)
                return default;

            var (leftHeaders, blockStartOffset, blockCount) = CutFirstBlock(Bytes.Offset, Bytes.LastOffset(), headers, possiblePartOfHeaderAtTheEnd);

            var blockSegment = new ArraySegment<byte>(bytesArray, blockStartOffset, blockCount);
            
            var leftBytes = blockCount == Bytes.Count
                ? Empty<byte>.ArraySegment
                : Bytes.ShiftOffsetRight(blockCount);
            var leftBuffer = new GzipBuffer(leftBytes, leftHeaders, possiblePartOfHeaderAtTheEnd);
            
            return (blockSegment, leftBuffer);
        }
        
        // todo: test it
        public ArraySegment<byte> GetPossiblePart()
        {
            if (possiblePartOfHeaderAtTheEnd == null)
                return default;

            var bytesArray = Bytes.Array;
            if (bytesArray == null)
                return default;
            
            return new ArraySegment<byte>(bytesArray, possiblePartOfHeaderAtTheEnd.Value, bytesArray.Length - possiblePartOfHeaderAtTheEnd.Value);
        }

        // todo: test it
        public GzipBuffer ReturnToStart(ArraySegment<byte> returnBlock)
        {
            var returnBlockArray = returnBlock.Array;
            if (Bytes.Array == null && returnBlockArray == null)
                return default;

            if (returnBlockArray == null)
                return this;

            if (!ReferenceEquals(returnBlockArray, Bytes.Array))
                throw new InvalidOperationException("Given block belongs to another array");

            if (returnBlock.LastOffset() != Bytes.Offset - 1)
                throw new InvalidOperationException("Returned block is not prefix bytes segment");

            var returnHeaders = GzipHeader.FindAllHeaders(returnBlock).ToList();
            returnHeaders.AddRange(Headers);    
            var joinedSegment = new ArraySegment<byte>(returnBlockArray, returnBlock.Offset, returnBlock.Count + Bytes.Count);
            return new GzipBuffer(joinedSegment, null, possiblePartOfHeaderAtTheEnd);
        }

        private static (IReadOnlyList<GzipHeader> leftHeaders, int blockStartOffset, int blockCount) CutFirstBlock(
            int firstBufferPosition,
            int lastBufferPosition,
            IReadOnlyList<GzipHeader> currentHeaders,
            int? currentPartOfHeaderAtTheEnd)
        {
            IReadOnlyList<GzipHeader> leftHeaders;
            var blockStartOffset = firstBufferPosition;
            int afterBlockOffset;
            if (currentHeaders.Count > 0)
            {
                var firstHeaderOffset = currentHeaders[0].Position;
                if (firstHeaderOffset == firstBufferPosition)
                {
                    if (currentHeaders.Count > 1)
                    {
                        leftHeaders = currentHeaders.Skip(1).ToList();
                        afterBlockOffset = currentHeaders[1].Position;
                    }
                    else
                    {
                        SetReturnWholeBytesBeforePart();
                    }
                }
                else
                {
                    afterBlockOffset = firstHeaderOffset;
                    leftHeaders = currentHeaders.ToList();
                }
            }
            else
            {
                SetReturnWholeBytesBeforePart();
            }

            var blockCount = afterBlockOffset - blockStartOffset;
            return (leftHeaders, blockStartOffset, blockCount);

            void SetReturnWholeBytesBeforePart()
            {
                afterBlockOffset = currentPartOfHeaderAtTheEnd ?? lastBufferPosition + 1;
                leftHeaders = Empty<GzipHeader>.Array;
            }
        }
    }
}