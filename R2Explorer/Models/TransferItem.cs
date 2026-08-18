using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace R2Explorer.Models;

public enum TransferState
{
    Waiting,
    Running,
    Completed,
    Failed,
    Canceled,
}

/// <summary>
/// 传输队列中的一条任务（上传 / 下载 / 复制 / 删除等）。
/// </summary>
public class TransferItem : INotifyPropertyChanged
{
    public string Operation { get; set; } = "";

    private string _name = "";
    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    private long _transferred;
    public long Transferred
    {
        get => _transferred;
        set
        {
            if (_transferred != value)
            {
                _transferred = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TransferredDisplay));
                OnPropertyChanged(nameof(Progress));
            }
        }
    }

    private long _total;
    public long Total
    {
        get => _total;
        set
        {
            if (_total != value)
            {
                _total = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TransferredDisplay));
                OnPropertyChanged(nameof(Progress));
            }
        }
    }

    public string TransferredDisplay =>
        Total > 0 ? $"{R2Item.FormatSize(Transferred)} / {R2Item.FormatSize(Total)}" : R2Item.FormatSize(Transferred);

    public double Progress => Total > 0 ? Transferred * 100.0 / Total : 0;

    private TransferState _state = TransferState.Waiting;
    public TransferState State
    {
        get => _state;
        set { _state = value; OnPropertyChanged(); OnPropertyChanged(nameof(StateText)); }
    }

    public string StateText => State switch
    {
        TransferState.Waiting => "等待中",
        TransferState.Running => "进行中",
        TransferState.Completed => "已完成",
        TransferState.Failed => "失败",
        TransferState.Canceled => "已取消",
        _ => "",
    };

    private string _error = "";
    public string Error
    {
        get => _error;
        set { _error = value; OnPropertyChanged(); }
    }

    public CancellationTokenSource? Cts { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
