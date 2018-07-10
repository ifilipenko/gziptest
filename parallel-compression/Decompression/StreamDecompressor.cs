using System;
using System.IO;
using System.Threading;
using JetBrains.Annotations;
using Parallel.Compression.Configuration;
using Parallel.Compression.Decompression.GzipSplitting;
using Parallel.Compression.Decompression.Streams;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;
using Parallel.Compression.Helpers;
using Parallel.Compression.IO;
using Parallel.Compression.Logging;
using Parallel.Compression.TaskQueue;
using Parallel.Compression.WorkersPool;

namespace Parallel.Compression.Decompression
{
    public class StreamDecompressor : IStreamDecompressor
    {
        private readonly ILog log;
        private readonly CompressorSettings settings;
        private readonly GzipBlockSplitter gzipBlockSplitter;

        public StreamDecompressor([NotNull] CompressorSettings settings, [NotNull] ILog log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            gzipBlockSplitter = new GzipBlockSplitter(
                settings.MaxParallelDecompressionBufferSize,
                settings.ParallelDecompressionBufferSize,
                log);
        }

        public Result<int, ErrorCodes?> Decompress([NotNull] Stream inputStream, [NotNull] Stream outputStream)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));

            if (inputStream.Position == inputStream.Length)
                return ErrorCodes.NothingToDecompress;

            //return SingleThreadDecompression(inputStream, outputStream);
            return MultyThreadedDecompression(inputStream, outputStream);
        }

        private Result<int, ErrorCodes?> SingleThreadDecompression(Stream inputStream, Stream outputStream)
        {
            var streamingGzipBlock = new StreamingGzipBlock(new RewindableReadonlyStream(inputStream));
            streamingGzipBlock?.WriteDecompressedDataTo(outputStream);
            return (int) Math.Floor(outputStream.Length*100.0/inputStream.Length);
        }

        private Result<int, ErrorCodes?> MultyThreadedDecompression(Stream inputStream, Stream outputStream)
        {
            using (var writerFinished = new ManualResetEvent(false))
            using (var workersPool = new WorkersPool.WorkersPool(settings.ThreadsCount, log))
            using (var tasksQueue = new BlockingTasksQueue(settings.CompressingQueueSize, workersPool, log))
            {
                var outputFile = new OutputFileWithoutOffsetStore(outputStream);
                var blockId = 0;

                var writeDecompressedTask =
                    RunDequeueingOutputWriteDecompressedBlocks(writerFinished, outputFile, tasksQueue);
                workersPool.PushTask(writeDecompressedTask);

                var needWaitTasksFinished = false;
                var needEndTasks = true;
                StreamingGzipBlock streamingGzipBlock = null;
                try
                {
                    foreach (var block in gzipBlockSplitter.SplitBlocks(inputStream))
                    {
                        if (block is StreamingGzipBlock gzipBlock)
                        {
                            tasksQueue.EndTasks();
                            needEndTasks = false;
                            if (needWaitTasksFinished)
                            {
                                writerFinished.WaitOne();
                                needWaitTasksFinished = false;
                            }

                            streamingGzipBlock = gzipBlock;
                            break;
                        }

                        tasksQueue.EnqueueTask(
                            new DelegateTask(
                                blockId++.ToString(),
                                () => ((IndependentGzipBlock) block).Decompress())
                        );
                        needWaitTasksFinished = true;
                    }
                }
                finally
                {
                    if (needEndTasks)
                    {
                        tasksQueue.EndTasks();
                    }
                }

                if (needWaitTasksFinished)
                {
                    writerFinished.WaitOne();
                }

                streamingGzipBlock?.WriteDecompressedDataTo(outputStream);

                return outputFile.CompressionRatio(inputStream.Length);
            }
        }

        private static IWorkerTask RunDequeueingOutputWriteDecompressedBlocks(
            ManualResetEvent writerFinished,
            OutputFileWithoutOffsetStore outputFile,
            BlockingTasksQueue tasksQueue)
        {
            return new DelegateWorkerTask(
                "Writer",
                () =>
                {
                    var error = WriteCompressedBlocks();
                    if (error != null)
                        throw new CompressorException(error.Value);
                });

            ErrorCodes? WriteCompressedBlocks()
            {
                try
                {
                    foreach (var result in tasksQueue.ConsumeTaskResults())
                    {
                        if (result.IsFailed)
                        {
                            if (result.Exception is CompressorException fileSystemException)
                                return fileSystemException.Error;
                            throw result.Exception;
                        }

                        var decompressedBlock = (byte[]) result.Result;
                        var error = outputFile.Append(decompressedBlock.ToSegment());
                        if (error != null)
                        {
                            return error.Value;
                        }
                    }

                    return null;
                }
                finally
                {
                    writerFinished.Set();
                }
            }
        }
    }
}