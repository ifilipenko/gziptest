using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Parallel.Compression.Decompression.Streams;
using Parallel.Compression.Logging;

namespace Parallel.Compression.Decompression.GzipSplitting
{
    internal class GzipBlockSplitter
    {
        private readonly IGzipToIndependentBlockSplitter[] independentBlockSplitters;

        public GzipBlockSplitter(int readWithKnwonLengthBlockSizeLimit, int indpendentBlockSearchingBufferSize, ILog log)
        {
            independentBlockSplitters = new IGzipToIndependentBlockSplitter[]
            {
                new MimeTypeLengthGzipSplitter(readWithKnwonLengthBlockSizeLimit, log), 
                new BufferBoundGzipSplitter(indpendentBlockSearchingBufferSize, log)
            };
        }

        public IEnumerable<IGzipBlock> SplitBlocks(Stream stream)
        {
            var streamIsEnds = false;
            var wrongFormat= false;
            
            IEnumerable<IndependentGzipBlock> SplitToIndependensBlocks(RewindableReadonlyStream rewindableStream)
            {
                var splittersQueue = independentBlockSplitters.ToArray();
                foreach (var splitter in splittersQueue)
                {
                    var changeSplitter = false;
                    
                    foreach (var (block, status) in splitter.SplitToIndependentBlocks(rewindableStream))
                    {
                        switch (status)
                        {
                            case GzipSplittingStatus.Block:
                                yield return block;
                                break;
                            case GzipSplittingStatus.StreamIsEnd:
                                streamIsEnds = true;
                                yield break;
                            case GzipSplittingStatus.WrongFormat:
                                wrongFormat = true;
                                yield break;
                            case GzipSplittingStatus.CantReadBlock:
                                changeSplitter = true;
                                break;
                        }

                        if (changeSplitter)
                        {
                            break;
                        }
                    }

                    if (!changeSplitter)
                    {
                        yield break;
                    }
                }
            }

            var inputStream = new RewindableReadonlyStream(stream);
            foreach (var independenBlock in SplitToIndependensBlocks(inputStream))
            {
                yield return independenBlock;
            }

            if (wrongFormat)
            {
                throw new InvalidOperationException("Stream has invalid format");
            }

            if (!streamIsEnds)
            {
                yield return new StreamingGzipBlock(inputStream);
            }
        }
    }
}