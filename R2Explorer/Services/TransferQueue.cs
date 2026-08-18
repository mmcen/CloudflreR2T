using System.Collections.ObjectModel;
using System.Windows;
using R2Explorer.Models;

namespace R2Explorer.Services;

/// <summary>
/// 传输进度信息（字节）。由后台传输线程向 UI 线程投递。
/// </summary>
public record struct TransferProgress(long Transferred, long Total);

/// <summary>
/// 并发受限的传输队列。所有项必须由 UI 线程入队；
/// 状态变更通过 Changed 事件（UI 线程）广播。
/// </summary>
public class TransferQueue
{
    private readonly SemaphoreSlim _gate;
    private readonly object _lock = new();
    private readonly Queue<TransferJob> _pending = new();

    public ObservableCollection<TransferItem> Items { get; } = new();

    /// <summary>在 UI 线程触发，用于刷新列表与汇总进度。</summary>
    public event EventHandler? Changed;

    public TransferQueue(int maxConcurrent)
    {
        _gate = new SemaphoreSlim(Math.Max(1, maxConcurrent));
    }

    /// <summary>
    /// 入队一个传输任务。total 为预计总字节数，可为 0（运行时更新）。
    /// </summary>
    public TransferItem Enqueue(
        string operation,
        string name,
        Func<IProgress<TransferProgress>, CancellationToken, Task> action,
        long total = 0)
    {
        var item = new TransferItem { Operation = operation, Name = name, Total = total };
        Items.Insert(0, item);
        RaiseChanged();

        lock (_lock)
        {
            _pending.Enqueue(new TransferJob(item, action));
        }

        _ = Task.Run(() => PumpAsync());
        return item;
    }

    private async Task PumpAsync()
    {
        while (true)
        {
            TransferJob job;
            lock (_lock)
            {
                if (_pending.Count == 0)
                    return;
                job = _pending.Dequeue();
            }

            await _gate.WaitAsync();
            try
            {
                await RunJobAsync(job);
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private async Task RunJobAsync(TransferJob job)
    {
        var item = job.Item;
        item.State = TransferState.Running;
        RaiseChanged();

        var cts = new CancellationTokenSource();
        item.Cts = cts;

        var progress = new Progress<TransferProgress>(p =>
        {
            item.Total = Math.Max(item.Total, p.Total);
            item.Transferred = p.Transferred;
            RaiseChanged();
        });

        try
        {
            await job.Action(progress, cts.Token);
            item.Total = Math.Max(item.Total, item.Transferred);
            item.State = TransferState.Completed;
        }
        catch (OperationCanceledException)
        {
            item.State = TransferState.Canceled;
        }
        catch (Exception ex)
        {
            item.State = TransferState.Failed;
            item.Error = ex.Message;
        }
        finally
        {
            item.Cts = null;
            RaiseChanged();
        }
    }

    public void Cancel(TransferItem item) => item.Cts?.Cancel();

    public void RemoveCompleted()
    {
        var finished = Items
            .Where(i => i.State is TransferState.Completed or TransferState.Failed or TransferState.Canceled)
            .ToList();
        foreach (var item in finished)
        {
            Items.Remove(item);
        }
        RaiseChanged();
    }

    /// <summary>汇总当前进行中任务的总字节与已完成字节。</summary>
    public (long Done, long Total) GetAggregate()
    {
        long done = 0, total = 0;
        foreach (var item in Items)
        {
            if (item.State is TransferState.Running or TransferState.Waiting)
            {
                done += item.Transferred;
                total += Math.Max(item.Total, item.Transferred);
            }
        }
        return (done, total);
    }

    private void RaiseChanged()
    {
        var app = Application.Current;
        if (app == null)
            return;
        app.Dispatcher.BeginInvoke(() => Changed?.Invoke(this, EventArgs.Empty));
    }

    private class TransferJob
    {
        public TransferItem Item { get; }
        public Func<IProgress<TransferProgress>, CancellationToken, Task> Action { get; }

        public TransferJob(TransferItem item, Func<IProgress<TransferProgress>, CancellationToken, Task> action)
        {
            Item = item;
            Action = action;
        }
    }
}
