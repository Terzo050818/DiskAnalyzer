using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using DiskAnalyzer.App.Commands;
using DiskAnalyzer.App.Models;
using DiskAnalyzer.App.Services;

namespace DiskAnalyzer.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private const int UiBatchSize = 500;
    private static readonly TimeSpan UiBatchInterval =
        TimeSpan.FromMilliseconds(200);

    private readonly IFileScannerService _fileScannerService;
    private readonly IFileLocationService _fileLocationService;
    private readonly Dispatcher _dispatcher;
    private CancellationTokenSource? _scanCancellation;

    private DriveItem? _selectedDrive;
    private FileItem? _selectedFile;
    private bool _isScanning;
    private string _currentPath = string.Empty;
    private string _statusMessage = "准备就绪";
    private long _filesDiscovered;
    private long _bytesDiscovered;
    private long _skippedItems;
    private TimeSpan _elapsed;

    public MainViewModel()
        : this(
            new DriveService(),
            new FileScannerService(),
            new FileLocationService(),
            Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
    {
    }

    public MainViewModel(
        IDriveService driveService,
        IFileScannerService fileScannerService,
        IFileLocationService fileLocationService,
        Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(driveService);
        ArgumentNullException.ThrowIfNull(fileScannerService);
        ArgumentNullException.ThrowIfNull(fileLocationService);
        ArgumentNullException.ThrowIfNull(dispatcher);

        _fileScannerService = fileScannerService;
        _fileLocationService = fileLocationService;
        _dispatcher = dispatcher;

        Drives = new ObservableCollection<DriveItem>(driveService.GetFixedDrives());
        Files = new ObservableCollection<FileItem>();
        FilesView = CollectionViewSource.GetDefaultView(Files);

        StartScanCommand = new AsyncRelayCommand(
            _ => StartScanAsync(),
            _ => CanStartScan);
        CancelScanCommand = new RelayCommand(
            _ => CancelScan(),
            _ => IsScanning);
        OpenFileLocationCommand = new RelayCommand(
            OpenFileLocation,
            parameter => parameter is FileItem);

        SelectedDrive = Drives.FirstOrDefault();
        StatusMessage = Drives.Count == 0
            ? "未找到可用的固定磁盘"
            : "请选择磁盘并开始扫描";
    }

    public string AppTitle => "DiskAnalyzer";

    public ObservableCollection<DriveItem> Drives { get; }

    public ObservableCollection<FileItem> Files { get; }

    public ICollectionView FilesView { get; }

    public AsyncRelayCommand StartScanCommand { get; }

    public RelayCommand CancelScanCommand { get; }

    public RelayCommand OpenFileLocationCommand { get; }

    public DriveItem? SelectedDrive
    {
        get => _selectedDrive;
        set
        {
            if (SetProperty(ref _selectedDrive, value))
            {
                StartScanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public FileItem? SelectedFile
    {
        get => _selectedFile;
        set => SetProperty(ref _selectedFile, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (SetProperty(ref _isScanning, value))
            {
                OnPropertyChanged(nameof(CanSelectDrive));
                StartScanCommand.RaiseCanExecuteChanged();
                CancelScanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool CanSelectDrive => !IsScanning;

    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public long FilesDiscovered
    {
        get => _filesDiscovered;
        private set => SetProperty(ref _filesDiscovered, value);
    }

    public long BytesDiscovered
    {
        get => _bytesDiscovered;
        private set => SetProperty(ref _bytesDiscovered, value);
    }

    public long SkippedItems
    {
        get => _skippedItems;
        private set => SetProperty(ref _skippedItems, value);
    }

    public TimeSpan Elapsed
    {
        get => _elapsed;
        private set => SetProperty(ref _elapsed, value);
    }

    private bool CanStartScan => SelectedDrive is not null && !IsScanning;

    public async Task StartScanAsync()
    {
        if (!CanStartScan || SelectedDrive is null)
        {
            return;
        }

        var scanRoot = SelectedDrive.Name;
        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;

        ResetScanState(scanRoot);
        IsScanning = true;
        StatusMessage = $"正在扫描 {scanRoot}";

        var progress = new Progress<ScanProgress>(UpdateProgress);

        try
        {
            await ConsumeScanAsync(scanRoot, progress, cancellation.Token);
            ApplyDefaultSort();
            StatusMessage =
                $"扫描完成：发现 {FilesDiscovered:N0} 个文件，跳过 {SkippedItems:N0} 项";
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            StatusMessage =
                $"扫描已取消：已发现 {FilesDiscovered:N0} 个文件";
        }
        catch (Exception exception)
        {
            StatusMessage = $"扫描失败：{exception.Message}";
        }
        finally
        {
            if (ReferenceEquals(_scanCancellation, cancellation))
            {
                _scanCancellation = null;
            }

            cancellation.Dispose();
            IsScanning = false;
        }
    }

    private async Task ConsumeScanAsync(
        string scanRoot,
        IProgress<ScanProgress> progress,
        CancellationToken cancellationToken)
    {
        var batch = new List<FileItem>(UiBatchSize);
        var batchStopwatch = Stopwatch.StartNew();

        await foreach (var file in _fileScannerService
                           .ScanAsync(scanRoot, progress, cancellationToken)
                           .ConfigureAwait(false))
        {
            batch.Add(file);

            if (batch.Count < UiBatchSize &&
                batchStopwatch.Elapsed < UiBatchInterval)
            {
                continue;
            }

            var completedBatch = batch;
            batch = new List<FileItem>(UiBatchSize);
            await CommitBatchAsync(completedBatch, cancellationToken)
                .ConfigureAwait(false);
            batchStopwatch.Restart();
        }

        if (batch.Count > 0)
        {
            await CommitBatchAsync(batch, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task CommitBatchAsync(
        IReadOnlyList<FileItem> batch,
        CancellationToken cancellationToken)
    {
        await _dispatcher.InvokeAsync(
            () =>
            {
                foreach (var file in batch)
                {
                    Files.Add(file);
                }
            },
            DispatcherPriority.Background,
            cancellationToken);
    }

    private void ResetScanState(string scanRoot)
    {
        using (FilesView.DeferRefresh())
        {
            FilesView.SortDescriptions.Clear();
            Files.Clear();
        }

        SelectedFile = null;
        CurrentPath = scanRoot;
        FilesDiscovered = 0;
        BytesDiscovered = 0;
        SkippedItems = 0;
        Elapsed = TimeSpan.Zero;
    }

    private void UpdateProgress(ScanProgress progress)
    {
        CurrentPath = progress.CurrentPath;
        FilesDiscovered = progress.FilesDiscovered;
        BytesDiscovered = progress.BytesDiscovered;
        SkippedItems = progress.SkippedItems;
        Elapsed = progress.Elapsed;
    }

    private void ApplyDefaultSort()
    {
        using (FilesView.DeferRefresh())
        {
            FilesView.SortDescriptions.Clear();
            FilesView.SortDescriptions.Add(
                new SortDescription(
                    nameof(FileItem.SizeBytes),
                    ListSortDirection.Descending));
        }
    }

    private void CancelScan()
    {
        if (_scanCancellation is null)
        {
            return;
        }

        StatusMessage = "正在取消扫描…";
        _scanCancellation.Cancel();
    }

    private void OpenFileLocation(object? parameter)
    {
        if (parameter is not FileItem file)
        {
            return;
        }

        try
        {
            _fileLocationService.OpenContainingFolder(file.FullPath);
            StatusMessage = $"已打开：{file.FullPath}";
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or System.ComponentModel.Win32Exception)
        {
            StatusMessage = $"无法打开文件位置：{exception.Message}";
        }
    }
}
