using CrossPlatformDownloadManager.Utils.PropertyChanged;
using Downloader;

namespace CrossPlatformDownloadManager.Data.ViewModels;

public class DownloadFileTaskViewModel : PropertyChangedBase
{
    #region Private Fields

    private int _key;
    private DownloadConfiguration? _configuration;
    private DownloadService? _service;

    #endregion

    #region Properties

    public int Key
    {
        get => _key;
        set => SetField(ref _key, value);
    }

    public DownloadConfiguration? Configuration
    {
        get => _configuration;
        set => SetField(ref _configuration, value);
    }

    public DownloadService? Service
    {
        get => _service;
        set => SetField(ref _service, value);
    }

    #endregion
}