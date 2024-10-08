using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using CrossPlatformDownloadManager.Data.ViewModels.CustomEventArgs;
using CrossPlatformDownloadManager.Utils;

namespace CrossPlatformDownloadManager.DesktopApp.Views.UserControls.DownloadWindowControls;

public partial class DownloadSpeedLimiterView : UserControl
{
    #region Private Fields

    private readonly DispatcherTimer _textChangedTimer;

    #endregion

    #region Properties

    public static readonly StyledProperty<bool> SpeedLimiterEnabledProperty =
        AvaloniaProperty.Register<DownloadSpeedLimiterView, bool>(
            "SpeedLimiterEnabled", defaultValue: false);

    public bool SpeedLimiterEnabled
    {
        get => GetValue(SpeedLimiterEnabledProperty);
        set => SetValue(SpeedLimiterEnabledProperty, value);
    }

    public static readonly StyledProperty<IEnumerable<string>?> UnitsDropDownItemsSourceProperty =
        AvaloniaProperty.Register<DownloadSpeedLimiterView, IEnumerable<string>?>(
            "UnitsDropDownItemsSource");

    public IEnumerable<string>? UnitsDropDownItemsSource
    {
        get => GetValue(UnitsDropDownItemsSourceProperty);
        set => SetValue(UnitsDropDownItemsSourceProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<DownloadSpeedLimiterViewEventArgs> SpeedLimiterStateChanged;

    #endregion

    public DownloadSpeedLimiterView()
    {
        InitializeComponent();

        _textChangedTimer = new DispatcherTimer();
        _textChangedTimer.Interval = TimeSpan.FromSeconds(2);
        _textChangedTimer.Tick += (sender, e) => ChangeSpeedLimiterState();

        this.Loaded += DownloadSpeedLimiterViewOnLoaded;
    }

    private void DownloadSpeedLimiterViewOnLoaded(object? sender, RoutedEventArgs e)
    {
        CboSpeedLimiterUnit.SelectedItem = Enumerable.FirstOrDefault<object?>(CboSpeedLimiterUnit.Items);
    }

    private void BtnEnableOrDisableSpeedLimiter_OnClick(object? sender, RoutedEventArgs e)
    {
        ChangeSpeedLimiterState(true);
    }

    private void CboSpeedLimiterUnit_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ChangeSpeedLimiterState();
    }

    private void TxtSpeedLimiterValue_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        // First, the timer must be stopped
        _textChangedTimer.Stop();
        
        // And it must be started again to reset the timer
        _textChangedTimer.Start();
    }

    private void ChangeSpeedLimiterState(bool changeState = false)
    {
        // The timer must be stopped so that it does not run again
        _textChangedTimer.Stop();
        
        var value = this.GetValue(SpeedLimiterEnabledProperty);
        if (changeState)
        {
            value = !value;
            this.SetValue(SpeedLimiterEnabledProperty, value);
        }

        if (ExtensionMethods.IsNullOrEmpty(TxtSpeedLimiterValue.Text) || CboSpeedLimiterUnit.SelectedItem == null)
            return;

        var isValid = double.TryParse((string?)TxtSpeedLimiterValue.Text, out var speed);
        if (!isValid)
            return;

        var unit = CboSpeedLimiterUnit.SelectedItem as string;
        if (unit.IsNullOrEmpty())
            return;

        var eventArgs = new DownloadSpeedLimiterViewEventArgs
        {
            Enabled = value,
            Speed = speed,
            Unit = unit,
        };

        SpeedLimiterStateChanged?.Invoke(this, eventArgs);
    }
}