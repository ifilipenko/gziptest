using System;
using System.IO;
using JetBrains.Annotations;
using Parallel.Compression.Configuration;
using Parallel.Compression.Errors;
using Parallel.Compression.Func;
using Parallel.Compression.IO;
using Parallel.Compression.Logging;
using Parallel.Compression.Models;
using Parallel.Compression.TaskQueue;
using Parallel.Compression.WorkersPool;

namespace Parallel.Compression.Compression
{
    public class StreamCompressor : IStreamCompressor
    {
        private readonly ILog log;
        private readonly IBlockCompression compression;
        [NotNull]
        private readonly CompressorSettings settings;

        public StreamCompressor([NotNull] IBlockCompression compression, [NotNull] CompressorSettings settings, [NotNull] ILog log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.compression = compression ?? throw new ArgumentNullException(nameof(compression));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public Result<int, ErrorCodes?> Compress([NotNull] Stream inputStream, [NotNull] Stream outputStream)
        {
            if (inputStream == null)
                throw new ArgumentNullException(nameof(inputStream));
            if (outputStream == null)
                throw new ArgumentNullException(nameof(outputStream));

            if (inputStream.Position == inputStream.Length)
                return ErrorCodes.NothingToCompress;

            using (var workersPool = new WorkersPool.WorkersPool(settings.ThreadsCount, log))
            using (var tasksQueue = new BlockingTasksQueue(settings.CompressingQueueSize, workersPool, log))
            {
                var outputFile = new OutputFile(outputStream, settings.OffsetLabel);
                var (lastReadOffset, offsetError) = outputFile.GetLastOffset();
                if (offsetError.HasValue)
                    return offsetError;

                var compressInputFileTask = RunEnqueueingInputFileBlockCompression(inputStream, lastReadOffset, tasksQueue);
                workersPool.PushTask(compressInputFileTask);

                foreach (var result in tasksQueue.ConsumeTaskResults())
                {
                    if (result.IsFailed)
                    {
                        if (result.Exception is CompressorException fileSystemException)
                            return fileSystemException.Error;
                        throw result.Exception;
                    }

                    var resultBlock = (Block) result.Result;

                    outputFile.Append(resultBlock.Bytes, resultBlock.Offset);
                }

                var commitError = outputFile.Commit();
                if (commitError.HasValue)
                    return commitError;

                return outputFile.CompressionRatio(inputStream.Length);
            }
        }

        private IWorkerTask RunEnqueueingInputFileBlockCompression(
            Stream inputStream,
            long offset,
            BlockingTasksQueue tasksQueue)
        {
            return new DelegateWorkerTask(
                "reader",
                () =>
                {
                    try
                    {
                        var inputFile = new InputFile(inputStream);
                        var readBlocks = inputFile.ReadBlocks(settings.InputFileReadingBufferSize, offset);
                        foreach (var (block, error) in readBlocks)
                        {
                            if (error.HasValue)
                                throw new CompressorException(error.Value);
                            var task = new DelegateTask(offset.ToString(), () => compression.Compress(block));
                            tasksQueue.EnqueueTask(task);
                            offset = block.Offset;
                        }
                    }
                    finally
                    {
                        tasksQueue.EndTasks();
                    }
                });
        }
    }
}