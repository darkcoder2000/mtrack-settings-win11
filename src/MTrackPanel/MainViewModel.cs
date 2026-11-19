using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using MTrack.Core;

namespace MTrackPanel;

public sealed record Option(uint Value, string Label)
{
    public override string ToString() => Label;
}

public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly object _deviceLock = new();
    private readonly DispatcherTimer _timer;
    private MTrackDevice? _device;
    private bool _busy;
    private bool _suppressApply;

    private bool _isConnected;
    private string _modelName = "M-Track";
    private string _statusText = "Searching for device…";
    private Option? _selectedSampleRate;
    private Option? _selectedBufferSize;
    private string _driverVersion = "–";
    private string _firmwareVersion = "–";
    private string? _errorMessage;
    private bool _clockLocked = true;
    private uint _currentRate = 44100;

    public MainViewModel()
    {
        PackageVersion = PackageInfo.GetDriverPackageVersion();
        foreach (var r in MTrackDevice.SupportedSampleRates)
            SampleRates.Add(new Option(r, $"{r / 1000.0:0.0##} kHz"));
        RebuildBufferLabels();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _timer.Tick += async (_, _) => await RefreshAsync();
    }

    public ObservableCollection<Option> SampleRates { get; } = [];
    public ObservableCollection<Option> BufferSizes { get; } = [];
    public string PackageVersion { get; }

    public bool IsConnected { get => _isConnected; private set => Set(ref _isConnected, value); }
    public string ModelName { get => _modelName; private set => Set(ref _modelName, value); }
    public string StatusText { get => _statusText; private set => Set(ref _statusText, value); }
    public string DriverVersion { get => _driverVersion; private set => Set(ref _driverVersion, value); }
    public string FirmwareVersion { get => _firmwareVersion; private set => Set(ref _firmwareVersion, value); }
    public bool ClockLocked { get => _clockLocked; private set => Set(ref _clockLocked, value); }
    public bool IsBusy { get => _busy; private set { Set(ref _busy, value); OnPropertyChanged(nameof(CanEdit)); } }
    public bool CanEdit => IsConnected && !IsBusy;

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set { Set(ref _errorMessage, value); OnPropertyChanged(nameof(HasError)); }
    }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public Option? SelectedSampleRate
    {
        get => _selectedSampleRate;
        set
        {
            if (Equals(value, _selectedSampleRate)) return;
            _selectedSampleRate = value;
            OnPropertyChanged();
            if (!_suppressApply && value is not null) _ = ApplyAsync(d => d.SetSampleRate(value.Value), "sample rate");
        }
    }

    public Option? SelectedBufferSize
    {
        get => _selectedBufferSize;
        set
        {
            if (Equals(value, _selectedBufferSize)) return;
            _selectedBufferSize = value;
            OnPropertyChanged();
            if (!_suppressApply && value is not null) _ = ApplyAsync(d => d.SetBufferSize(value.Value), "buffer size");
        }
    }

    /// <summary>Start polling while the window is visible.</summary>
    public void Activate()
    {
        _ = RefreshAsync();
        _timer.Start();
    }

    public void Deactivate() => _timer.Stop();

    private async Task ApplyAsync(Action<MTrackDevice> change, string what)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await Task.Run(() =>
            {
                lock (_deviceLock)
                {
                    var dev = _device ?? throw new InvalidOperationException("The M-Track is not connected.");
                    change(dev);
                }
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not change the {what}: {ex.Message}\n" +
                           "Close audio applications that are using the M-Track (DAW, media player) and try again.";
        }
        finally
        {
            IsBusy = false;
        }
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        MTrackState? state = null;
        try
        {
            state = await Task.Run(() =>
            {
                lock (_deviceLock)
                {
                    _device ??= MTrackDevice.TryOpenFirst();
                    if (_device is null) return null;
                    try
                    {
                        return _device.ReadState();
                    }
                    catch (Exception)
                    {
                        // Device was unplugged or the handle went stale: reopen next time.
                        _device.Dispose();
                        _device = null;
                        return null;
                    }
                }
            });
        }
        catch (Exception) { /* treat as disconnected */ }

        Apply(state);
    }

    private void Apply(MTrackState? s)
    {
        if (s is null)
        {
            IsConnected = false;
            OnPropertyChanged(nameof(CanEdit));
            StatusText = "Not connected – plug in your M-Track";
            DriverVersion = FirmwareVersion = "–";
            _suppressApply = true;
            SelectedSampleRate = null;
            SelectedBufferSize = null;
            _suppressApply = false;
            return;
        }

        IsConnected = true;
        OnPropertyChanged(nameof(CanEdit));
        ModelName = s.ModelName;
        ClockLocked = s.ClockLocked;
        StatusText = s.ClockLocked ? "Connected" : "Connected – clock not locked";
        DriverVersion = s.DriverVersion;
        FirmwareVersion = s.FirmwareVersion;

        if (_currentRate != s.SampleRate)
        {
            _currentRate = s.SampleRate;
            RebuildBufferLabels();
        }

        _suppressApply = true;
        try
        {
            SelectedSampleRate = FindOrAdd(SampleRates, s.SampleRate, v => $"{v / 1000.0:0.0##} kHz");
            SelectedBufferSize = FindOrAdd(BufferSizes, s.BufferSize, BufferLabel);
        }
        finally
        {
            _suppressApply = false;
        }
    }

    /// <summary>Selects the matching entry; adds one if the driver reports a value we don't list.</summary>
    private static Option FindOrAdd(ObservableCollection<Option> list, uint value, Func<uint, string> label)
    {
        var o = list.FirstOrDefault(x => x.Value == value);
        if (o is not null) return o;
        o = new Option(value, label(value));
        int i = 0;
        while (i < list.Count && list[i].Value < value) i++;
        list.Insert(i, o);
        return o;
    }

    private string BufferLabel(uint samples) => $"{samples} samples  ({samples * 1000.0 / _currentRate:0.0} ms)";

    private void RebuildBufferLabels()
    {
        var selected = _selectedBufferSize?.Value;
        var values = BufferSizes.Count > 0 ? BufferSizes.Select(b => b.Value).ToList() : MTrackDevice.SupportedBufferSizes.ToList();
        _suppressApply = true;
        try
        {
            BufferSizes.Clear();
            foreach (var v in values) BufferSizes.Add(new Option(v, BufferLabel(v)));
            if (selected is uint sv) SelectedBufferSize = BufferSizes.First(b => b.Value == sv);
        }
        finally
        {
            _suppressApply = false;
        }
    }

    public void Dispose()
    {
        _timer.Stop();
        lock (_deviceLock)
        {
            _device?.Dispose();
            _device = null;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);
    }
}
