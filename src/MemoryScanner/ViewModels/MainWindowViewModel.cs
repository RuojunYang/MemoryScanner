using System.Collections.ObjectModel;
using System.Windows.Input;
using MemoryScanner.Models;
using MemoryScanner.Services;

namespace MemoryScanner.ViewModels;

internal sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private const int DisplayLimit = 10_000;

    private readonly ProcessService _processService = new();
    private readonly MemoryScanService _scanService = new();
    private readonly MemoryWriteService _writeService = new();
    private readonly RelayCommand _openProcessCommand;
    private readonly AsyncRelayCommand _firstScanCommand;
    private readonly AsyncRelayCommand _nextScanCommand;
    private readonly RelayCommand _newScanCommand;
    private readonly RelayCommand _stopCommand;
    private readonly RelayCommand _writeCommand;
    private List<nuint>? _candidates;
    private CancellationTokenSource? _scanCancellation;
    private ProcessOption? _selectedProcess;
    private IntegerTypeOption _selectedType = IntegerTypeOption.All[4];
    private MemoryResult? _selectedResult;
    private string _scanValue = "0";
    private string _writeValue = "0";
    private string _statusText = "请选择目标进程。";
    private string _connectedProcessText = "未连接";
    private bool _isBusy;
    private int _totalResultCount;

    public MainWindowViewModel()
    {
        RefreshProcessesCommand = new RelayCommand(RefreshProcesses, () => !IsBusy);
        _openProcessCommand = new RelayCommand(OpenSelectedProcess, () => SelectedProcess is not null && !IsBusy);
        _firstScanCommand = new AsyncRelayCommand(FirstScanAsync, () => CanScan);
        _nextScanCommand = new AsyncRelayCommand(NextScanAsync, () => CanScan && _candidates is not null);
        _newScanCommand = new RelayCommand(NewScan, () => !IsBusy);
        _stopCommand = new RelayCommand(StopScan, () => IsBusy);
        _writeCommand = new RelayCommand(WriteSelectedAddress, () => SelectedResult is not null && !IsBusy && HasTarget);

        OpenProcessCommand = _openProcessCommand;
        FirstScanCommand = _firstScanCommand;
        NextScanCommand = _nextScanCommand;
        NewScanCommand = _newScanCommand;
        StopCommand = _stopCommand;
        WriteCommand = _writeCommand;
        RefreshProcesses();
    }

    public ObservableCollection<ProcessOption> Processes { get; } = [];
    public ObservableCollection<MemoryResult> Results { get; } = [];
    public IReadOnlyList<IntegerTypeOption> IntegerTypes => IntegerTypeOption.All;

    public ICommand RefreshProcessesCommand { get; }
    public ICommand OpenProcessCommand { get; }
    public ICommand FirstScanCommand { get; }
    public ICommand NextScanCommand { get; }
    public ICommand NewScanCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand WriteCommand { get; }

    public ProcessOption? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (SetProperty(ref _selectedProcess, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public IntegerTypeOption SelectedType
    {
        get => _selectedType;
        set
        {
            if (SetProperty(ref _selectedType, value))
            {
                NewScan();
            }
        }
    }

    public MemoryResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (SetProperty(ref _selectedResult, value))
            {
                if (value is not null)
                {
                    WriteValue = value.Value;
                }

                RaiseCommandStates();
            }
        }
    }

    public string ScanValue
    {
        get => _scanValue;
        set => SetProperty(ref _scanValue, value);
    }

    public string WriteValue
    {
        get => _writeValue;
        set => SetProperty(ref _writeValue, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ConnectedProcessText
    {
        get => _connectedProcessText;
        private set => SetProperty(ref _connectedProcessText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                RaiseCommandStates();
            }
        }
    }

    public bool IsIdle => !IsBusy;
    public bool HasTarget => _processService.ProcessId.HasValue && _processService.IsTargetAlive();

    public int TotalResultCount
    {
        get => _totalResultCount;
        private set => SetProperty(ref _totalResultCount, value);
    }

    private bool CanScan => !IsBusy && HasTarget && !string.IsNullOrWhiteSpace(ScanValue);

    private void RefreshProcesses()
    {
        int? selectedId = SelectedProcess?.Id;
        IReadOnlyList<ProcessOption> processes = _processService.GetProcesses();
        Processes.Clear();
        foreach (ProcessOption process in processes)
        {
            Processes.Add(process);
        }

        SelectedProcess = Processes.FirstOrDefault(item => item.Id == selectedId) ?? Processes.FirstOrDefault();
        StatusText = $"已发现 {Processes.Count} 个进程。";
    }

    private void OpenSelectedProcess()
    {
        if (SelectedProcess is null)
        {
            return;
        }

        try
        {
            _processService.Open(SelectedProcess);
            ConnectedProcessText = $"{SelectedProcess.Name}.exe (PID: {SelectedProcess.Id})";
            OnPropertyChanged(nameof(HasTarget));
            NewScan();
            StatusText = "目标进程已打开，可以开始精确数值扫描。";
        }
        catch (Exception exception)
        {
            ConnectedProcessText = "未连接";
            StatusText = exception.Message;
            OnPropertyChanged(nameof(HasTarget));
        }

        RaiseCommandStates();
    }

    private async Task FirstScanAsync()
    {
        byte[] pattern;
        try
        {
            pattern = IntegerValueCodec.Encode(ScanValue, SelectedType);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            return;
        }

        _candidates = null;
        Results.Clear();
        TotalResultCount = 0;
        SelectedResult = null;
        await RunScanAsync(async (progress, token) =>
        {
            List<nuint> found = await _scanService.FirstScanAsync(
                _processService.Handle,
                pattern,
                progress,
                token);
            _candidates = found;
            PopulateResults(found, IntegerValueCodec.Decode(pattern, SelectedType));
        });
    }

    private async Task NextScanAsync()
    {
        if (_candidates is null)
        {
            return;
        }

        byte[] pattern;
        try
        {
            pattern = IntegerValueCodec.Encode(ScanValue, SelectedType);
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
            return;
        }

        IReadOnlyList<nuint> previous = _candidates;
        await RunScanAsync(async (progress, token) =>
        {
            List<nuint> found = await _scanService.NextScanAsync(
                _processService.Handle,
                previous,
                pattern,
                progress,
                token);
            _candidates = found;
            PopulateResults(found, IntegerValueCodec.Decode(pattern, SelectedType));
        });
    }

    private async Task RunScanAsync(
        Func<IProgress<ScanProgress>, CancellationToken, Task> scan)
    {
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        IsBusy = true;
        StatusText = "正在扫描…";

        Progress<ScanProgress> progress = new(value =>
        {
            double megabytes = value.BytesScanned / 1024d / 1024d;
            StatusText = $"正在扫描… 已读取 {megabytes:N1} MB，找到 {value.MatchCount:N0} 个地址";
        });

        try
        {
            await scan(progress, _scanCancellation.Token);
            StatusText = TotalResultCount > DisplayLimit
                ? $"扫描完成：共 {TotalResultCount:N0} 个结果，列表显示前 {DisplayLimit:N0} 个。"
                : $"扫描完成：找到 {TotalResultCount:N0} 个结果。";
        }
        catch (OperationCanceledException)
        {
            StatusText = "扫描已停止。";
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateResults(IReadOnlyList<nuint> addresses, string value)
    {
        Results.Clear();
        foreach (nuint address in addresses.Take(DisplayLimit))
        {
            Results.Add(new MemoryResult(address, value));
        }

        TotalResultCount = addresses.Count;
        SelectedResult = Results.FirstOrDefault();
        RaiseCommandStates();
    }

    private void NewScan()
    {
        if (IsBusy)
        {
            return;
        }

        _candidates = null;
        Results.Clear();
        TotalResultCount = 0;
        SelectedResult = null;
        StatusText = HasTarget ? "已重置扫描条件。" : "请选择目标进程。";
        RaiseCommandStates();
    }

    private void StopScan() => _scanCancellation?.Cancel();

    private void WriteSelectedAddress()
    {
        if (SelectedResult is null)
        {
            return;
        }

        try
        {
            string actualValue = _writeService.WriteAndVerify(
                _processService.Handle,
                SelectedResult.Address,
                WriteValue,
                SelectedType);
            SelectedResult.Value = actualValue;
            StatusText = $"已写入并校验 {SelectedResult.AddressText}。";
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private void RaiseCommandStates()
    {
        (RefreshProcessesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        _openProcessCommand.RaiseCanExecuteChanged();
        _firstScanCommand.RaiseCanExecuteChanged();
        _nextScanCommand.RaiseCanExecuteChanged();
        _newScanCommand.RaiseCanExecuteChanged();
        _stopCommand.RaiseCanExecuteChanged();
        _writeCommand.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _processService.Dispose();
    }
}
