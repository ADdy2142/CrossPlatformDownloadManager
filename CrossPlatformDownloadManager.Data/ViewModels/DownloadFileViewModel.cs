﻿using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Threading;
using CrossPlatformDownloadManager.Data.Services.UnitOfWork;
using CrossPlatformDownloadManager.Data.ViewModels.CustomEventArgs;
using CrossPlatformDownloadManager.Utils;
using CrossPlatformDownloadManager.Utils.Enums;
using Downloader;
using PropertyChanged;

namespace CrossPlatformDownloadManager.Data.ViewModels;

[AddINotifyPropertyChangedInterface]
public sealed class DownloadFileViewModel
{
    #region Private Fields

    // ElapsedTime timer
    private DispatcherTimer? _elapsedTimeTimer;
    private TimeSpan? _elapsedTime;

    // UpdateChunksData timer
    private DispatcherTimer? _updateChunksDataTimer;
    private List<ChunkProgressViewModel>? _chunkProgresses;
    
    // DownloadFinished timer
    private DispatcherTimer? _downloadFinishedTimer;
    private DownloadFileEventArgs? _downloadFinishedEventArgs;

    #endregion

    #region Events

    public event EventHandler<DownloadFileEventArgs>? DownloadFinished;

    #endregion

    #region Properties

    public int Id { get; set; }
    public string? Url { get; set; }
    public string? FileName { get; set; }
    public string? FileType { get; set; }
    public int? DownloadQueueId { get; set; }
    public string? DownloadQueueName { get; set; }
    public double? Size { get; set; }
    public string? SizeAsString => Size.ToFileSize();
    public DownloadFileStatus? Status { get; set; }
    public bool IsCompleted => Status == DownloadFileStatus.Completed;
    public bool IsDownloading => Status == DownloadFileStatus.Downloading;
    public bool IsPaused => Status == DownloadFileStatus.Paused;
    public bool IsError => Status == DownloadFileStatus.Error;
    public DateTime? LastTryDate { get; set; }
    public string? LastTryDateAsString => LastTryDate?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    public DateTime DateAdded { get; set; }
    public string DateAddedAsString => DateAdded.ToString(CultureInfo.InvariantCulture);
    public int? DownloadQueuePriority { get; set; }
    public int? CategoryId { get; set; }
    public float? DownloadProgress { get; set; }
    public string? DownloadProgressAsString => DownloadProgress == null ? string.Empty : $"{DownloadProgress:00.00}%";
    public string? DownloadedSizeAsString { get; set; }
    public TimeSpan? ElapsedTime { get; set; }
    public string? ElapsedTimeAsString => ElapsedTime.GetShortTime();
    public TimeSpan? TimeLeft { get; set; }
    public string? TimeLeftAsString => TimeLeft.GetShortTime();
    public float? TransferRate { get; set; }
    public string? TransferRateAsString => TransferRate.ToFileSize();
    public string? SaveLocation { get; set; }
    public string? DownloadPackage { get; set; }
    public ObservableCollection<ChunkDataViewModel> ChunksData { get; set; } = [];
    public int CountOfError { get; set; }

    #endregion

    public async Task StartDownloadFileAsync(DownloadService? downloadService,
        DownloadConfiguration downloadConfiguration, IUnitOfWork? unitOfWork)
    {
        if (downloadService == null || unitOfWork == null)
            return;

        downloadService.DownloadStarted += DownloadServiceOnDownloadStarted;
        downloadService.DownloadFileCompleted += DownloadServiceOnDownloadFileCompleted;
        downloadService.DownloadProgressChanged += DownloadServiceOnDownloadProgressChanged;
        downloadService.ChunkDownloadProgressChanged += DownloadServiceOnChunkDownloadProgressChanged;

        var downloadPath = SaveLocation;
        if (downloadPath.IsNullOrEmpty())
        {
            var saveDirectory = await unitOfWork.CategorySaveDirectoryRepository
                .GetAsync(where: sd => sd.CategoryId == null);

            downloadPath = saveDirectory?.SaveDirectory;
            if (downloadPath.IsNullOrEmpty())
                return;
        }

        if (FileName.IsNullOrEmpty() || Url.IsNullOrEmpty())
            return;

        if (!Directory.Exists(downloadPath!))
            Directory.CreateDirectory(downloadPath!);

        CreateChunksData(downloadConfiguration.ChunkCount);
        CalculateElapsedTime();
        UpdateChunksData();

        var fileName = Path.Combine(downloadPath!, FileName!);
        var downloadPackage = DownloadPackage.ConvertFromJson<DownloadPackage>();
        if (downloadPackage == null)
        {
            await downloadService.DownloadFileTaskAsync(address: Url!, fileName: fileName);
        }
        else
        {
            var urls = downloadPackage
                .Urls
                .ToList();
            
            var currentUrl = urls.FirstOrDefault(u => u.Equals(Url!));
            if (currentUrl.IsNullOrEmpty())
            {
                urls.Clear();
                urls.Add(Url!);

                downloadPackage.Urls = urls.ToArray();
            }
            
            await downloadService.DownloadFileTaskAsync(downloadPackage);
        }
    }

    public async Task StopDownloadFileAsync(DownloadService? downloadService)
    {
        if (downloadService == null)
            return;

        _elapsedTimeTimer?.Stop();
        _updateChunksDataTimer?.Stop();

        _elapsedTimeTimer = null;
        _elapsedTime = null;
        _updateChunksDataTimer = null;
        _chunkProgresses = null;

        var pack = downloadService.Package;
        await downloadService.CancelTaskAsync();
        SaveDownloadPackage(pack);
    }

    public void ResumeDownloadFile(DownloadService? downloadService)
    {
        if (downloadService == null)
            return;

        downloadService.Resume();
        _elapsedTimeTimer?.Start();
        _updateChunksDataTimer?.Start();
        Status = DownloadFileStatus.Downloading;
    }

    public void PauseDownloadFile(DownloadService? downloadService)
    {
        if (downloadService == null)
            return;

        downloadService.Pause();
        _elapsedTimeTimer?.Stop();
        _updateChunksDataTimer?.Stop();
        Status = DownloadFileStatus.Paused;
        UpdateChunksDataTimerOnTick(null, EventArgs.Empty);
        SaveDownloadPackage(downloadService.Package);
    }

    #region Helpers

    private void DownloadServiceOnDownloadStarted(object? sender, DownloadStartedEventArgs e)
    {
        Status = DownloadFileStatus.Downloading;
        LastTryDate = DateTime.Now;
    }

    private void DownloadServiceOnDownloadFileCompleted(object? sender, AsyncCompletedEventArgs e)
    {
        bool isSuccess;
        string? error = null;
        
        // TODO: Show error
        if (e is { Error: not null, Cancelled: false })
        {
            Status = DownloadFileStatus.Error;
            isSuccess = false;
            error = e.Error.Message;
        }
        else if (e.Cancelled)
        {
            Status = DownloadFileStatus.Paused;
            isSuccess = false;
        }
        else
        {
            Status = DownloadFileStatus.Completed;
            isSuccess = true;
        }

        _downloadFinishedEventArgs = new DownloadFileEventArgs
        {
            Id = Id,
            IsSuccess = isSuccess,
            Error = error,
        };

        _downloadFinishedTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _downloadFinishedTimer.Tick += DownloadFinishedTimerOnTick;
        _downloadFinishedTimer.Start();
    }

    private void DownloadFinishedTimerOnTick(object? sender, EventArgs e)
    {
        if (_downloadFinishedTimer == null || _downloadFinishedEventArgs == null)
            return;
        
        _downloadFinishedTimer.Stop();
        _downloadFinishedTimer.Tick -= DownloadFinishedTimerOnTick;
        _downloadFinishedTimer = null;
        
        DownloadFinished?.Invoke(this, _downloadFinishedEventArgs);
        _downloadFinishedEventArgs = null;
    }

    private void DownloadServiceOnDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        DownloadProgress = (float)e.ProgressPercentage;
        TransferRate = (float)e.BytesPerSecondSpeed;
        DownloadedSizeAsString = e.ReceivedBytesSize.ToFileSize();

        var timeLeft = TimeSpan.Zero;
        var remainSizeToReceive = (Size ?? 0) - e.ReceivedBytesSize;
        var remainSeconds = remainSizeToReceive / e.BytesPerSecondSpeed;
        if (!double.IsInfinity(remainSeconds))
            timeLeft = TimeSpan.FromSeconds(remainSeconds);

        TimeLeft = timeLeft;
    }

    private void DownloadServiceOnChunkDownloadProgressChanged(object? sender, DownloadProgressChangedEventArgs e)
    {
        if (_chunkProgresses == null || _chunkProgresses.Count == 0)
            return;

        var chunkProgress = _chunkProgresses.FirstOrDefault(cp => cp.ProgressId.Equals(e.ProgressId));
        if (chunkProgress == null)
            return;

        chunkProgress.ReceivedBytesSize = e.ReceivedBytesSize;
        chunkProgress.TotalBytesToReceive = e.TotalBytesToReceive;
    }

    private void CreateChunksData(int count)
    {
        var chunks = new List<ChunkDataViewModel>();
        _chunkProgresses ??= [];

        for (var i = 0; i < count; i++)
        {
            chunks.Add(new ChunkDataViewModel { ChunkIndex = i });
            _chunkProgresses.Add(new ChunkProgressViewModel { ProgressId = i.ToString() });
        }

        ChunksData = chunks.ToObservableCollection();
    }

    private void CalculateElapsedTime()
    {
        _elapsedTimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _elapsedTimeTimer.Tick += ElapsedTimeTimerOnTick;
        _elapsedTimeTimer.Start();
    }

    private void ElapsedTimeTimerOnTick(object? sender, EventArgs e)
    {
        _elapsedTime ??= TimeSpan.Zero;
        _elapsedTime = TimeSpan.FromSeconds(_elapsedTime.Value.TotalSeconds + 1);
        ElapsedTime = _elapsedTime;
    }

    private void UpdateChunksData()
    {
        _updateChunksDataTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _updateChunksDataTimer.Tick += UpdateChunksDataTimerOnTick;
        _updateChunksDataTimer.Start();
    }

    private void UpdateChunksDataTimerOnTick(object? sender, EventArgs e)
    {
        if (_chunkProgresses == null || _chunkProgresses.Count == 0)
            return;

        foreach (var chunkProgress in _chunkProgresses)
        {
            if (!int.TryParse(chunkProgress.ProgressId, out var progressId))
                return;

            var chunkData = ChunksData.FirstOrDefault(cd => cd.ChunkIndex == progressId);
            if (chunkData == null)
                return;

            if (chunkProgress.CheckCount % 5 == 0)
            {
                if (_updateChunksDataTimer!.IsEnabled && chunkData.DownloadedSize != chunkData.TotalSize)
                {
                    chunkData.Info = chunkData.DownloadedSize == chunkProgress.ReceivedBytesSize
                        ? "Connecting..."
                        : "Receiving...";
                }

                chunkProgress.CheckCount = 1;
            }
            else
            {
                chunkProgress.CheckCount++;
            }

            if (chunkData.DownloadedSize == chunkData.TotalSize)
                chunkData.Info = "Completed";
            else if (!_updateChunksDataTimer!.IsEnabled)
                chunkData.Info = "Paused";

            chunkData.DownloadedSize = chunkProgress.ReceivedBytesSize;
            chunkData.TotalSize = chunkProgress.TotalBytesToReceive;
        }
    }

    private void SaveDownloadPackage(DownloadPackage? downloadPackage)
    {
        DownloadPackage = downloadPackage?.ConvertToJson();
    }

    #endregion
}