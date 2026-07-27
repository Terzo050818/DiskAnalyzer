using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Channels;
using DiskAnalyzer.App.Models;

namespace DiskAnalyzer.App.Services;

public sealed class FileScannerService : IFileScannerService
{
    private static readonly EnumerationOptions EnumerationOptions = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false
    };

    private readonly FileScannerOptions _options;

    public FileScannerService(FileScannerOptions? options = null)
    {
        _options = options ?? new FileScannerOptions();
        _options.Validate();
    }

    public async IAsyncEnumerable<FileItem> ScanAsync(
        string rootPath,
        IProgress<ScanProgress>? progress = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var normalizedRootPath = Path.GetFullPath(rootPath);
        if (!Directory.Exists(normalizedRootPath))
        {
            throw new DirectoryNotFoundException(
                $"The scan root does not exist: {normalizedRootPath}");
        }

        using var scanCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        var resultChannel = Channel.CreateBounded<FileItem>(
            new BoundedChannelOptions(_options.ResultBufferCapacity)
            {
                SingleReader = true,
                SingleWriter = _options.DirectoryWorkerCount == 1,
                FullMode = BoundedChannelFullMode.Wait
            });

        var producer = Task.Run(
            () => ProduceFilesAsync(
                normalizedRootPath,
                resultChannel.Writer,
                progress,
                scanCancellation.Token),
            CancellationToken.None);

        try
        {
            await foreach (var file in resultChannel.Reader
                               .ReadAllAsync(cancellationToken)
                               .ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
            }

            await producer.ConfigureAwait(false);
        }
        finally
        {
            scanCancellation.Cancel();

            try
            {
                await producer.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (scanCancellation.IsCancellationRequested)
            {
                // Cancellation is surfaced by the async stream to its caller.
            }
        }
    }

    private async Task ProduceFilesAsync(
        string rootPath,
        ChannelWriter<FileItem> resultWriter,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        Exception? completionError = null;

        try
        {
            await ScanFileSystemAsync(
                rootPath,
                resultWriter,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            completionError = exception;
        }
        finally
        {
            resultWriter.TryComplete(completionError);
        }
    }

    private async Task ScanFileSystemAsync(
        string rootPath,
        ChannelWriter<FileItem> resultWriter,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var directoryChannel = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions
            {
                SingleReader = _options.DirectoryWorkerCount == 1,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

        var state = new ScanState(
            rootPath,
            progress,
            _options.ProgressReportInterval);
        var pendingDirectories = 1;

        await directoryChannel.Writer
            .WriteAsync(rootPath, cancellationToken)
            .ConfigureAwait(false);

        var workers = Enumerable.Range(0, _options.DirectoryWorkerCount)
            .Select(_ => ScanDirectoriesAsync())
            .ToArray();

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        finally
        {
            state.Report(force: true);
        }

        async Task ScanDirectoriesAsync()
        {
            try
            {
                await foreach (var directoryPath in directoryChannel.Reader
                                   .ReadAllAsync(cancellationToken)
                                   .ConfigureAwait(false))
                {
                    state.SetCurrentPath(directoryPath);

                    try
                    {
                        await ScanDirectoryAsync(directoryPath).ConfigureAwait(false);
                    }
                    finally
                    {
                        if (Interlocked.Decrement(ref pendingDirectories) == 0)
                        {
                            directoryChannel.Writer.TryComplete();
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                directoryChannel.Writer.TryComplete(exception);
                throw;
            }
        }

        async Task ScanDirectoryAsync(string directoryPath)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IEnumerator<FileSystemInfo>? enumerator = null;

            try
            {
                enumerator = new DirectoryInfo(directoryPath)
                    .EnumerateFileSystemInfos("*", EnumerationOptions)
                    .GetEnumerator();

                while (TryMoveNext(enumerator, state, out var entry))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry is DirectoryInfo directory)
                    {
                        if (!IsReparsePoint(directory, state))
                        {
                            Interlocked.Increment(ref pendingDirectories);

                            try
                            {
                                await directoryChannel.Writer
                                    .WriteAsync(directory.FullName, cancellationToken)
                                    .ConfigureAwait(false);
                            }
                            catch
                            {
                                Interlocked.Decrement(ref pendingDirectories);
                                throw;
                            }
                        }

                        continue;
                    }

                    if (entry is not FileInfo file)
                    {
                        continue;
                    }

                    FileItem fileItem;
                    try
                    {
                        fileItem = new FileItem(
                            file.FullName,
                            file.Length,
                            file.CreationTime,
                            file.LastWriteTime);
                    }
                    catch (Exception exception) when (
                        IsSkippableFileSystemException(exception))
                    {
                        state.RecordSkippedItem();
                        continue;
                    }

                    await resultWriter
                        .WriteAsync(fileItem, cancellationToken)
                        .ConfigureAwait(false);

                    state.RecordFile(fileItem.SizeBytes);
                }
            }
            catch (Exception exception) when (
                IsSkippableFileSystemException(exception))
            {
                state.RecordSkippedItem();
            }
            finally
            {
                enumerator?.Dispose();
            }
        }
    }

    private static bool TryMoveNext(
        IEnumerator<FileSystemInfo> enumerator,
        ScanState state,
        out FileSystemInfo? entry)
    {
        try
        {
            if (enumerator.MoveNext())
            {
                entry = enumerator.Current;
                return true;
            }
        }
        catch (Exception exception) when (IsSkippableFileSystemException(exception))
        {
            state.RecordSkippedItem();
        }

        entry = null;
        return false;
    }

    private static bool IsReparsePoint(
        DirectoryInfo directory,
        ScanState state)
    {
        try
        {
            return directory.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (Exception exception) when (IsSkippableFileSystemException(exception))
        {
            state.RecordSkippedItem();
            return true;
        }
    }

    private static bool IsSkippableFileSystemException(Exception exception)
    {
        return exception is UnauthorizedAccessException
            or SecurityException
            or IOException;
    }

    private sealed class ScanState
    {
        private readonly IProgress<ScanProgress>? _progress;
        private readonly TimeSpan _reportInterval;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private string _currentPath;
        private long _filesDiscovered;
        private long _bytesDiscovered;
        private long _skippedItems;
        private long _lastReportTimestamp;

        public ScanState(
            string rootPath,
            IProgress<ScanProgress>? progress,
            TimeSpan reportInterval)
        {
            _currentPath = rootPath;
            _progress = progress;
            _reportInterval = reportInterval;
        }

        public void SetCurrentPath(string path)
        {
            Volatile.Write(ref _currentPath, path);
            Report(force: false);
        }

        public void RecordFile(long sizeBytes)
        {
            Interlocked.Increment(ref _filesDiscovered);
            Interlocked.Add(ref _bytesDiscovered, sizeBytes);
            Report(force: false);
        }

        public void RecordSkippedItem()
        {
            Interlocked.Increment(ref _skippedItems);
            Report(force: false);
        }

        public void Report(bool force)
        {
            if (_progress is null)
            {
                return;
            }

            var now = _stopwatch.ElapsedTicks;
            var lastReport = Volatile.Read(ref _lastReportTimestamp);
            var intervalTicks =
                (long)(_reportInterval.TotalSeconds * Stopwatch.Frequency);

            if (!force && now - lastReport < intervalTicks)
            {
                return;
            }

            if (!force &&
                Interlocked.CompareExchange(
                    ref _lastReportTimestamp,
                    now,
                    lastReport) != lastReport)
            {
                return;
            }

            if (force)
            {
                Interlocked.Exchange(ref _lastReportTimestamp, now);
            }

            _progress.Report(new ScanProgress(
                Volatile.Read(ref _currentPath),
                Interlocked.Read(ref _filesDiscovered),
                Interlocked.Read(ref _bytesDiscovered),
                Interlocked.Read(ref _skippedItems),
                _stopwatch.Elapsed));
        }
    }
}
