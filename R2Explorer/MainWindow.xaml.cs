using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using R2Explorer.Dialogs;
using R2Explorer.Models;
using R2Explorer.Services;

namespace R2Explorer;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly List<AccountListItem> _accountItems = new();
    private readonly TransferQueue _queue;

    private R2Service? _service;
    private string _bucket = "";
    private string _prefix = "";

    private List<R2Item> _allItems = new();
    private readonly List<(string Key, bool IsFolder)> _clipboard = new();
    private bool _clipboardIsCut;

    private string _lastLocalDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private bool _suppressBucketEvent;

    public MainWindow()
    {
        InitializeComponent();

        _settings = App.Settings;
        _queue = new TransferQueue(_settings.MaxConcurrentTransfers);
        _queue.Changed += OnQueueChanged;
        TransferList.ItemsSource = _queue.Items;

        RefreshAccountList();
        UpdateActionButtons();
        UpdateEmptyOverlay();
        UpdatePathBox();

        if (_settings.AutoConnectLast && _settings.LastAccountId != null)
        {
            var last = _accountItems.FirstOrDefault(a => a.Profile.Id == _settings.LastAccountId);
            if (last != null)
            {
                AccountList.SelectedItem = last;
                _ = ConnectAsync(last);
            }
        }
        else if (_accountItems.Count > 0)
        {
            AccountList.SelectedItem = _accountItems[0];
        }
    }

    // ===================== 帐号 =====================

    private void RefreshAccountList()
    {
        _accountItems.Clear();
        foreach (var profile in _settings.Accounts)
        {
            _accountItems.Add(new AccountListItem(profile));
        }
        AccountList.ItemsSource = _accountItems;
        NoAccountOverlay.Visibility = _accountItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        AccountList.Visibility = _accountItems.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private AccountListItem? SelectedAccount => AccountList.SelectedItem as AccountListItem;

    private async Task ConnectAsync(AccountListItem item)
    {
        _service = null;
        SetLoading(true);
        try
        {
            var svc = await R2Service.ConnectAsync(item.Profile, _settings.Proxy);
            await svc.ValidateAsync();
            _service = svc;

            foreach (var acc in _accountItems)
            {
                acc.Connected = acc == item;
            }
            _settings.LastAccountId = item.Profile.Id;

            _bucket = "";
            _prefix = "";
            await LoadBucketsAsync();
            SetStatus($"已连接：{item.Profile.Name}（{AccountModes.Display(item.Profile.Mode)}）");
            SetConn(true);
            ((App)Application.Current).Notify("连接成功", $"{item.Profile.Name} 已连接");
        }
        catch (Exception ex)
        {
            item.Connected = false;
            SetStatus($"连接失败：{ex.Message}");
            SetConn(false);
            MessageBox.Show(this,
                $"连接失败：\n{ex.Message}\n\n请检查：\n1. 帐号配置（ID、Key、Token 是否正确）\n2. 网络与代理设置\n3. 系统时间是否准确（影响签名）",
                "连接失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void BtnConnect_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedAccount;
        if (item == null)
        {
            MessageBox.Show(this, "请先在左侧选择要连接的帐号。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _ = ConnectAsync(item);
    }

    private void AccountList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtons();
    }

    private void BtnAddAccount_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AccountDialog { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultProfile != null)
        {
            _settings.Accounts.Add(dialog.ResultProfile);
            RefreshAccountList();
            AccountList.SelectedItem = _accountItems[^1];
        }
    }

    private void BtnEditAccount_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedAccount;
        if (item == null)
            return;
        var dialog = new AccountDialog(item.Profile) { Owner = this };
        if (dialog.ShowDialog() == true && dialog.ResultProfile != null)
        {
            RefreshAccountList();
            AccountList.SelectedItem = _accountItems.FirstOrDefault(a => a.Profile.Id == item.Profile.Id);
        }
    }

    private void BtnDeleteAccount_Click(object sender, RoutedEventArgs e)
    {
        var item = SelectedAccount;
        if (item == null)
            return;
        if (MessageBox.Show(this, $"确定删除帐号“{item.Profile.Name}”吗？\n（仅删除本地配置，不会影响云端数据）",
                "删除帐号", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }
        _settings.Accounts.Remove(item.Profile);
        if (_service?.Account.Id == item.Profile.Id)
        {
            _service = null;
            SetConn(false);
            _bucket = "";
            _prefix = "";
            ObjectGrid.ItemsSource = null;
            BucketCombo.ItemsSource = null;
            UpdateEmptyOverlay();
            SetStatus("已断开");
        }
        RefreshAccountList();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsDialog(_settings) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            App.SetTheme(_settings.Theme);
            SetStatus("设置已保存");
        }
    }

    // ===================== 桶 =====================

    private async Task LoadBucketsAsync()
    {
        if (_service == null)
            return;
        try
        {
            var names = await _service.ListBucketsAsync();
            _suppressBucketEvent = true;
            BucketCombo.ItemsSource = names;

            string? selected = null;
            if (names.Contains(_settings.LastBucket))
            {
                selected = _settings.LastBucket;
            }
            else if (names.Count > 0)
            {
                selected = names[0];
            }
            BucketCombo.SelectedItem = selected;
            _suppressBucketEvent = false;

            _bucket = selected ?? "";
            _prefix = "";
            await LoadObjectsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"加载存储桶失败：{ex.Message}");
        }
    }

    private void BucketCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressBucketEvent)
            return;
        _bucket = BucketCombo.SelectedItem as string ?? "";
        _prefix = "";
        _settings.LastBucket = _bucket;
        _ = LoadObjectsAsync();
    }

    private async void BtnRefreshBuckets_Click(object sender, RoutedEventArgs e)
        => await LoadBucketsAsync();

    private async void BtnCreateBucket_Click(object sender, RoutedEventArgs e)
    {
        if (_service == null)
        {
            MessageBox.Show(this, "请先连接帐号。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var name = InputDialog.ShowInput(this, "新建存储桶", "存储桶名称（小写字母、数字、连字符）：");
        if (string.IsNullOrEmpty(name))
            return;
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z0-9][a-z0-9-]{1,61}[a-z0-9]$"))
        {
            MessageBox.Show(this, "存储桶名称不合法：只能包含小写字母、数字、连字符，长度 3-63。", "提示",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            SetLoading(true);
            await _service.CreateBucketAsync(name);
            SetStatus($"已创建存储桶：{name}");
            _settings.LastBucket = name;
            await LoadBucketsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"创建失败：{ex.Message}", "新建存储桶", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void BtnDeleteBucket_Click(object sender, RoutedEventArgs e)
    {
        if (_service == null || string.IsNullOrEmpty(_bucket))
            return;
        if (MessageBox.Show(this, $"确定删除存储桶“{_bucket}”吗？\n该操作会先递归清空桶内所有对象，且不可恢复！",
                "删除存储桶", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }
        try
        {
            SetLoading(true);
            var keys = await _service.ListAllKeysAsync(_bucket, "");
            if (keys.Count > 0)
            {
                await _service.DeleteObjectsAsync(_bucket, keys);
            }
            await _service.DeleteBucketAsync(_bucket);
            SetStatus($"已删除存储桶：{_bucket}");
            _bucket = "";
            _prefix = "";
            await LoadBucketsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"删除失败：{ex.Message}", "删除存储桶", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    // ===================== 对象列表 =====================

    private async Task LoadObjectsAsync()
    {
        if (_service == null || string.IsNullOrEmpty(_bucket))
        {
            ObjectGrid.ItemsSource = null;
            _allItems = new List<R2Item>();
            UpdateEmptyOverlay();
            return;
        }
        try
        {
            var items = await _service.ListObjectsAsync(_bucket, _prefix);
            _allItems = items;
            ApplyFilter();
            UpdateEmptyOverlay();
            UpdatePathBox();
        }
        catch (Exception ex)
        {
            SetStatus($"加载对象失败：{ex.Message}");
        }
    }

    private void ApplyFilter()
    {
        var keyword = SearchBox.Text.Trim();
        if (keyword.Length == 0)
        {
            ObjectGrid.ItemsSource = _allItems;
        }
        else
        {
            ObjectGrid.ItemsSource = _allItems
                .Where(i => i.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    private void UpdateEmptyOverlay()
    {
        if (_service == null)
        {
            EmptyOverlayText.Text = "请在左侧选择帐号并连接";
            EmptyOverlayHint.Text = "支持 R2 S3 API Token / API Token / 全局 Key / 自定义 S3 四种登录模式";
        }
        else if (string.IsNullOrEmpty(_bucket))
        {
            EmptyOverlayText.Text = "请选择存储桶（或在左侧工具栏新建）";
            EmptyOverlayHint.Text = "左侧选择存储桶后即可浏览与管理对象";
        }
        else if (ObjectGrid.Items.Count == 0)
        {
            EmptyOverlayText.Text = "当前目录为空";
            EmptyOverlayHint.Text = "可以拖拽本地文件到此处进行上传";
        }
        else
        {
            EmptyOverlay.Visibility = Visibility.Collapsed;
            return;
        }
        EmptyOverlay.Visibility = Visibility.Visible;
    }

    private void UpdatePathBox()
        => PathBox.Text = "/" + _prefix;

    private void NavigateInto(R2Item folder)
    {
        _prefix = folder.Key;
        _ = LoadObjectsAsync();
    }

    private void NavigateUp()
    {
        if (string.IsNullOrEmpty(_prefix))
            return;
        var trimmed = _prefix.TrimEnd('/');
        var idx = trimmed.LastIndexOf('/');
        _prefix = idx < 0 ? "" : trimmed[..(idx + 1)];
        _ = LoadObjectsAsync();
    }

    private void GoPath()
    {
        var text = PathBox.Text.Trim();
        var p = text.StartsWith('/') ? text[1..] : text;
        p = p.Replace('\\', '/').Trim('/');
        _prefix = p.Length == 0 ? "" : p + "/";
        _ = LoadObjectsAsync();
    }

    private void PathBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            GoPath();
    }

    private void BtnGoPath_Click(object sender, RoutedEventArgs e) => GoPath();
    private void BtnUp_Click(object sender, RoutedEventArgs e) => NavigateUp();
    private void BtnRefresh_Click(object sender, RoutedEventArgs e) => _ = LoadObjectsAsync();

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
            ApplyFilter();
    }

    private void ObjectGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var count = GetSelectedItems().Count;
        SelectedText.Text = count == 0 ? "" : $"已选择 {count} 项";
        UpdateActionButtons();
    }

    private void ObjectGrid_PreviewRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = ItemsControl.ContainerFromElement(ObjectGrid, e.OriginalSource as DependencyObject) as DataGridRow;
        if (row != null && !row.IsSelected)
        {
            row.IsSelected = true;
        }
    }

    private void ObjectGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var items = GetSelectedItems();
        if (items.Count != 1)
            return;
        var item = items[0];
        if (item.IsFolder)
        {
            NavigateInto(item);
        }
        else
        {
            OpenItem(item);
        }
    }

    private void ObjectGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete)
        {
            DeleteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.F2)
        {
            RenameSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            var items = GetSelectedItems();
            if (items.Count == 1 && items[0].IsFolder)
            {
                NavigateInto(items[0]);
                e.Handled = true;
            }
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.C)
        {
            CopySelected(false);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.X)
        {
            CopySelected(true);
            e.Handled = true;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V)
        {
            PasteClipboard();
            e.Handled = true;
        }
    }

    private List<R2Item> GetSelectedItems()
    {
        var result = new List<R2Item>();
        foreach (var obj in ObjectGrid.SelectedItems)
        {
            if (obj is R2Item item)
                result.Add(item);
        }
        return result;
    }

    private void UpdateActionButtons()
    {
        var selected = GetSelectedItems().Count;
        var hasService = _service != null && !string.IsNullOrEmpty(_bucket);

        BtnEditAccount.IsEnabled = SelectedAccount != null;
        BtnDeleteAccount.IsEnabled = SelectedAccount != null;

        BtnDownload.IsEnabled = hasService && selected > 0;
        BtnOpen.IsEnabled = hasService && selected == 1 && !GetSelectedItems().FirstOrDefault()?.IsFolder == true;
        BtnCopyUrl.IsEnabled = hasService && selected > 0;
        BtnPresigned.IsEnabled = hasService && selected > 0;
        BtnCut.IsEnabled = hasService && selected > 0;
        BtnCopy.IsEnabled = hasService && selected > 0;
        BtnPaste.IsEnabled = hasService && _clipboard.Count > 0;
        BtnRename.IsEnabled = hasService && selected == 1;
        BtnDelete.IsEnabled = hasService && selected > 0;

        BtnDeleteBucket.IsEnabled = hasService;
        BtnNewFolder.IsEnabled = hasService;
        BtnUpload.IsEnabled = hasService;
    }

    // ===================== 上传 =====================

    private void BtnUpload_Click(object sender, RoutedEventArgs e)
    {
        if (e.Source is Button b && b.ContextMenu != null)
        {
            b.ContextMenu.PlacementTarget = b;
            b.ContextMenu.IsOpen = true;
        }
    }

    private void CtxUploadFiles_Click(object sender, RoutedEventArgs e) => UploadFiles();

    private void CtxUploadFolder_Click(object sender, RoutedEventArgs e) => UploadFolder();

    private void UploadFiles()
    {
        if (_service == null || string.IsNullOrEmpty(_bucket))
            return;
        var dialog = new OpenFileDialog
        {
            Title = "选择要上传的文件",
            Multiselect = true,
            InitialDirectory = _lastLocalDir,
        };
        if (dialog.ShowDialog(this) == true)
        {
            _lastLocalDir = Path.GetDirectoryName(dialog.FileNames[0]) ?? _lastLocalDir;
            EnqueueUploadFiles(dialog.FileNames);
        }
    }

    private void UploadFolder()
    {
        if (_service == null || string.IsNullOrEmpty(_bucket))
            return;
        var dialog = new OpenFolderDialog
        {
            Title = "选择要上传的文件夹",
            InitialDirectory = _lastLocalDir,
        };
        if (dialog.ShowDialog(this) == true)
        {
            _lastLocalDir = dialog.FolderName;
            EnqueueUploadFolder(dialog.FolderName);
        }
    }

    private void EnqueueUploadFiles(string[] files)
    {
        var svc = _service;
        var bucket = _bucket;
        var prefix = _prefix;
        if (svc == null)
            return;

        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var total = new FileInfo(file).Length;
            _queue.Enqueue("上传", name, async (progress, ct) =>
            {
                await svc.UploadFileAsync(bucket, prefix + name, file, progress, ct);
            }, total);
        }
        SetStatus($"已加入 {files.Length} 个文件到上传队列");
    }

    private void EnqueueUploadFolder(string localDir)
    {
        var svc = _service;
        var bucket = _bucket;
        var prefix = _prefix;
        if (svc == null)
            return;

        var dirName = Path.GetFileName(Path.TrimEndingDirectorySeparator(localDir));
        if (string.IsNullOrEmpty(dirName))
            dirName = "folder";

        var files = Directory.EnumerateFiles(localDir, "*", SearchOption.AllDirectories).ToList();
        if (files.Count == 0)
        {
            MessageBox.Show(this, "所选文件夹为空，未上传任何文件。", "上传", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        long total = files.Sum(f => new FileInfo(f).Length);
        _queue.Enqueue("上传文件夹", dirName, async (progress, ct) =>
        {
            long done = 0;
            var fileInfos = files.Select(f => (FullPath: f, Info: new FileInfo(f))).ToList();
            foreach (var (fullPath, info) in fileInfos)
            {
                ct.ThrowIfCancellationRequested();
                var rel = Path.GetRelativePath(localDir, fullPath).Replace('\\', '/');
                var key = prefix + dirName + "/" + rel;
                var fileProgress = new Progress<TransferProgress>(p =>
                    progress.Report(new TransferProgress(done + p.Transferred, total)));
                await svc.UploadFileAsync(bucket, key, fullPath, fileProgress, ct);
                done += info.Length;
                progress.Report(new TransferProgress(done, total));
            }
        }, total);
        SetStatus($"已加入文件夹“{dirName}”（{files.Count} 个文件）到上传队列");
    }

    private void ObjectGrid_Drop(object sender, DragEventArgs e)
    {
        if (_service == null || string.IsNullOrEmpty(_bucket))
            return;
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;
        var paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (paths == null || paths.Length == 0)
            return;

        var files = new List<string>();
        var folders = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                folders.Add(path);
            else
                files.Add(path);
        }

        var targetPrefix = _prefix;

        if (files.Count > 0)
        {
            EnqueueUploadFilesTo(files.ToArray(), targetPrefix);
        }
        foreach (var folder in folders)
        {
            EnqueueUploadFolderTo(folder, targetPrefix);
        }

        e.Handled = true;
        SetStatus("已加入上传队列");
    }

    private void EnqueueUploadFilesTo(string[] files, string prefix)
    {
        var svc = _service;
        var bucket = _bucket;
        if (svc == null)
            return;
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var total = new FileInfo(file).Length;
            _queue.Enqueue("上传", name, async (progress, ct) =>
            {
                await svc.UploadFileAsync(bucket, prefix + name, file, progress, ct);
            }, total);
        }
    }

    private void EnqueueUploadFolderTo(string localDir, string prefix)
    {
        var svc = _service;
        var bucket = _bucket;
        if (svc == null)
            return;
        var dirName = Path.GetFileName(localDir.TrimEnd('\\', '/')) ?? "folder";
        var files = Directory.EnumerateFiles(localDir, "*", SearchOption.AllDirectories).ToList();
        long total = files.Sum(f => new FileInfo(f).Length);
        _queue.Enqueue("上传文件夹", dirName, async (progress, ct) =>
        {
            long done = 0;
            foreach (var path in files)
            {
                ct.ThrowIfCancellationRequested();
                var rel = Path.GetRelativePath(localDir, path).Replace('\\', '/');
                var key = prefix + dirName + "/" + rel;
                var fileProgress = new Progress<TransferProgress>(p =>
                    progress.Report(new TransferProgress(done + p.Transferred, total)));
                await svc.UploadFileAsync(bucket, key, path, fileProgress, ct);
                done += new FileInfo(path).Length;
                progress.Report(new TransferProgress(done, total));
            }
        }, total);
    }

    private void ObjectGrid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    // ===================== 下载 =====================

    private void BtnDownload_Click(object sender, RoutedEventArgs e) => DownloadSelected();
    private void CtxDownload_Click(object sender, RoutedEventArgs e) => DownloadSelected();

    private void DownloadSelected()
    {
        var items = GetSelectedItems();
        if (items.Count == 0 || _service == null)
            return;

        if (items.Count == 1 && !items[0].IsFolder)
        {
            var dialog = new SaveFileDialog
            {
                Title = "保存文件",
                FileName = items[0].Name,
                InitialDirectory = _lastLocalDir,
            };
            if (dialog.ShowDialog(this) == true)
            {
                _lastLocalDir = Path.GetDirectoryName(dialog.FileName) ?? _lastLocalDir;
                EnqueueDownloadFile(items[0], dialog.FileName);
            }
        }
        else
        {
            var dialog = new OpenFolderDialog
            {
                Title = "选择保存目录",
                InitialDirectory = _lastLocalDir,
            };
            if (dialog.ShowDialog(this) == true)
            {
                _lastLocalDir = dialog.FolderName;
                foreach (var item in items)
                {
                    EnqueueDownloadToFolder(item, dialog.FolderName);
                }
            }
        }
    }

    private void EnqueueDownloadFile(R2Item item, string localPath)
    {
        var svc = _service;
        var bucket = _bucket;
        if (svc == null)
            return;
        var dir = Path.GetDirectoryName(localPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        _queue.Enqueue("下载", item.Name, async (progress, ct) =>
        {
            await svc.DownloadFileAsync(bucket, item.Key, localPath, progress, ct);
        });
    }

    private void EnqueueDownloadToFolder(R2Item item, string localDir)
    {
        var svc = _service;
        var bucket = _bucket;
        if (svc == null)
            return;

        if (!item.IsFolder)
        {
            var localPath = Path.Combine(localDir, ToLocalRelative(item.Key, _prefix, item.Name));
            EnqueueDownloadFile(item, localPath);
            return;
        }

        var folderKey = item.Key;
        _queue.Enqueue("下载文件夹", item.Name, async (progress, ct) =>
        {
            var keys = await svc.ListAllKeysAsync(bucket, folderKey, ct);
            var fileKeys = keys.Where(k => !k.EndsWith("/")).ToList();

            long total = 0;
            var plans = new List<(string Key, string LocalPath, long Size)>();
            foreach (var key in fileKeys)
            {
                var rel = key[Math.Min(key.Length, folderKey.Length)..];
                var localPath = Path.Combine(localDir, rel.Replace('/', Path.DirectorySeparatorChar));
                long size = 0;
                try
                {
                    size = (await svc.GetMetadataAsync(bucket, key, ct)).ContentLength;
                }
                catch
                {
                    // 忽略元数据获取失败，按 0 字节计
                }
                total += size;
                plans.Add((key, localPath, size));
            }

            long done = 0;
            foreach (var (key, localPath, size) in plans)
            {
                ct.ThrowIfCancellationRequested();
                var dir = Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                var fileProgress = new Progress<TransferProgress>(p =>
                    progress.Report(new TransferProgress(done + p.Transferred, total)));
                await svc.DownloadFileAsync(bucket, key, localPath, fileProgress, ct);
                done += size;
                progress.Report(new TransferProgress(done, total));
            }
        });
    }

    private static string ToLocalRelative(string key, string prefix, string fallbackName)
    {
        var rel = key;
        if (rel.StartsWith(prefix, StringComparison.Ordinal))
            rel = rel[prefix.Length..];
        if (string.IsNullOrEmpty(rel))
            rel = fallbackName;
        return rel.Replace('/', Path.DirectorySeparatorChar);
    }

    private void BtnOpen_Click(object sender, RoutedEventArgs e) => OpenSelected();
    private void CtxOpen_Click(object sender, RoutedEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        var items = GetSelectedItems();
        if (items.Count != 1 || items[0].IsFolder || _service == null)
            return;
        OpenItem(items[0]);
    }

    private void OpenItem(R2Item item)
    {
        var svc = _service;
        var bucket = _bucket;
        if (svc == null)
            return;
        var tempDir = Path.Combine(Path.GetTempPath(), "R2Explorer", bucket);
        var localPath = Path.Combine(tempDir, ToLocalRelative(item.Key, _prefix, item.Name));

        _queue.Enqueue("打开", item.Name, async (progress, ct) =>
        {
            var dir = Path.GetDirectoryName(localPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            await svc.DownloadFileAsync(bucket, item.Key, localPath, progress, ct);
            Process.Start(new ProcessStartInfo(localPath) { UseShellExecute = true });
        });
    }

    // ===================== URL =====================

    private void BtnCopyUrl_Click(object sender, RoutedEventArgs e) => CopyUrlSelected();
    private void CtxCopyUrl_Click(object sender, RoutedEventArgs e) => CopyUrlSelected();

    private void CopyUrlSelected()
    {
        var items = GetSelectedItems();
        if (items.Count == 0 || _service == null)
            return;
        var urls = new List<string>();
        foreach (var item in items)
        {
            var publicUrl = _service.BuildPublicUrl(_bucket, item.Key);
            urls.Add(publicUrl.Length > 0 ? publicUrl : _service.GetPresignedUrl(_bucket, item.Key, 60));
        }
        Clipboard.SetText(string.Join(Environment.NewLine, urls));
        var mode = _service.BuildPublicUrl(_bucket, items[0].Key).Length > 0 ? "公开地址" : "临时地址";
        SetStatus($"已复制 {items.Count} 个对象的{mode}到剪贴板");
    }

    private void BtnPresigned_Click(object sender, RoutedEventArgs e) => PresignedSelected();
    private void CtxPresigned_Click(object sender, RoutedEventArgs e) => PresignedSelected();

    private void PresignedSelected()
    {
        var items = GetSelectedItems();
        if (items.Count != 1 || _service == null)
            return;
        var svc = _service;
        var bucket = _bucket;
        var key = items[0].Key;
        var name = items[0].Name;
        var dialog = new PresignedUrlDialog(minutes => svc.GetPresignedUrl(bucket, key, minutes), name)
        {
            Owner = this,
        };
        dialog.ShowDialog();
    }

    // ===================== 复制 / 剪切 / 粘贴 / 重命名 / 删除 =====================

    private void BtnCut_Click(object sender, RoutedEventArgs e) => CopySelected(true);
    private void BtnCopy_Click(object sender, RoutedEventArgs e) => CopySelected(false);
    private void CtxCut_Click(object sender, RoutedEventArgs e) => CopySelected(true);
    private void CtxCopy_Click(object sender, RoutedEventArgs e) => CopySelected(false);

    private void CopySelected(bool cut)
    {
        var items = GetSelectedItems();
        if (items.Count == 0)
            return;
        _clipboard.Clear();
        foreach (var item in items)
        {
            _clipboard.Add((item.Key, item.IsFolder));
        }
        _clipboardIsCut = cut;
        UpdateActionButtons();
        SetStatus(cut ? $"已剪切 {items.Count} 项，可粘贴到目标目录" : $"已复制 {items.Count} 项，可粘贴到目标目录");
    }

    private void BtnPaste_Click(object sender, RoutedEventArgs e) => PasteClipboard();
    private void CtxPaste_Click(object sender, RoutedEventArgs e) => PasteClipboard();

    private async void PasteClipboard()
    {
        if (_service == null || string.IsNullOrEmpty(_bucket) || _clipboard.Count == 0)
            return;

        var svc = _service;
        var bucket = _bucket;
        var targetPrefix = _prefix;
        var clip = _clipboard.ToList();
        var isCut = _clipboardIsCut;

        try
        {
            SetLoading(true);
            var totalKeys = new List<string>();

            // 先枚举需要复制的所有 key
            foreach (var (key, isFolder) in clip)
            {
                if (!isFolder)
                {
                    totalKeys.Add(key);
                }
                else
                {
                    var sub = await svc.ListAllKeysAsync(bucket, key);
                    totalKeys.AddRange(sub);
                    if (!totalKeys.Contains(key))
                        totalKeys.Add(key);
                }
            }

            var name = clip.Count == 1 ? clip[0].Key.Split('/').Last(x => x.Length > 0) : $"{clip.Count} 项";
            _queue.Enqueue(isCut ? "移动" : "复制", name, async (progress, ct) =>
            {
                long done = 0;
                foreach (var (key, isFolder) in clip)
                {
                    ct.ThrowIfCancellationRequested();
                    var baseName = key.TrimEnd('/').Split('/').Last();
                    if (!isFolder)
                    {
                        await svc.CopyObjectAsync(bucket, key, targetPrefix + baseName, ct);
                        done++;
                        progress.Report(new TransferProgress(done, totalKeys.Count));
                    }
                    else
                    {
                        var sub = await svc.ListAllKeysAsync(bucket, key, ct);
                        var srcPrefix = key;
                        foreach (var subKey in sub)
                        {
                            ct.ThrowIfCancellationRequested();
                            var rel = subKey[Math.Min(subKey.Length, srcPrefix.Length)..];
                            await svc.CopyObjectAsync(bucket, subKey, targetPrefix + baseName + "/" + rel, ct);
                            done++;
                            progress.Report(new TransferProgress(done, totalKeys.Count));
                        }
                    }
                }

                if (isCut)
                {
                    foreach (var (key, _) in clip)
                    {
                        ct.ThrowIfCancellationRequested();
                        if (!key.EndsWith("/"))
                        {
                            await svc.DeleteObjectsAsync(bucket, new[] { key }, ct);
                        }
                        else
                        {
                            var sub = await svc.ListAllKeysAsync(bucket, key, ct);
                            if (sub.Count > 0)
                            {
                                await svc.DeleteObjectsAsync(bucket, sub, ct);
                            }
                        }
                    }
                }
            }, totalKeys.Count);

            _clipboard.Clear();
            _clipboardIsCut = false;
            UpdateActionButtons();
            SetStatus(isCut ? "已移动，正在刷新..." : "已复制，正在刷新...");
            await LoadObjectsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"粘贴失败：{ex.Message}", "粘贴", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void BtnRename_Click(object sender, RoutedEventArgs e) => RenameSelected();
    private void CtxRename_Click(object sender, RoutedEventArgs e) => RenameSelected();

    private async void RenameSelected()
    {
        var items = GetSelectedItems();
        if (items.Count != 1 || _service == null)
            return;
        var item = items[0];
        var newName = InputDialog.ShowInput(this, "重命名", "新名称：", item.Name);
        if (string.IsNullOrEmpty(newName) || newName == item.Name)
            return;
        if (newName.Contains('/') || newName.Contains('\\'))
        {
            MessageBox.Show(this, "名称不能包含 / 或 \\。", "重命名", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var svc = _service;
        var bucket = _bucket;
        var parentPrefix = _prefix;

        try
        {
            SetLoading(true);
            if (!item.IsFolder)
            {
                await svc.CopyObjectAsync(bucket, item.Key, parentPrefix + newName);
                await svc.DeleteObjectsAsync(bucket, new[] { item.Key });
            }
            else
            {
                var oldPrefix = item.Key;
                var newPrefix = parentPrefix + newName + "/";
                var keys = await svc.ListAllKeysAsync(bucket, oldPrefix);
                var fileKeys = keys.Where(k => !k.EndsWith("/")).ToList();
                foreach (var key in fileKeys)
                {
                    var rel = key[Math.Min(key.Length, oldPrefix.Length)..];
                    await svc.CopyObjectAsync(bucket, key, newPrefix + rel);
                }
                await svc.DeleteObjectsAsync(bucket, keys);
            }
            await LoadObjectsAsync();
            SetStatus($"已重命名为：{newName}");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"重命名失败：{ex.Message}", "重命名", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e) => DeleteSelected();
    private void CtxDelete_Click(object sender, RoutedEventArgs e) => DeleteSelected();

    private async void DeleteSelected()
    {
        var items = GetSelectedItems();
        if (items.Count == 0 || _service == null)
            return;

        if (_settings.ConfirmBeforeDelete)
        {
            if (MessageBox.Show(this, $"确定删除选中的 {items.Count} 项吗？\n该操作不可恢复！",
                    "删除确认", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var svc = _service;
        var bucket = _bucket;

        try
        {
            SetLoading(true);
            var keys = new List<string>();
            foreach (var item in items)
            {
                if (!item.IsFolder)
                {
                    keys.Add(item.Key);
                }
                else
                {
                    var sub = await svc.ListAllKeysAsync(bucket, item.Key);
                    keys.AddRange(sub);
                }
            }

            var displayName = items.Count == 1 ? items[0].Name : $"{items.Count} 项";
            _queue.Enqueue("删除", displayName, async (progress, ct) =>
            {
                var done = 0;
                for (int i = 0; i < keys.Count; i += 1000)
                {
                    ct.ThrowIfCancellationRequested();
                    var chunk = keys.Skip(i).Take(1000).ToList();
                    await svc.DeleteObjectsAsync(bucket, chunk, ct);
                    done += chunk.Count;
                    progress.Report(new TransferProgress(done, keys.Count));
                }
            }, keys.Count);

            await LoadObjectsAsync();
            SetStatus("删除任务已提交");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"删除失败：{ex.Message}", "删除", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetLoading(false);
        }
    }

    // ===================== 属性 =====================

    private void CtxProperties_Click(object sender, RoutedEventArgs e)
    {
        var items = GetSelectedItems();
        if (items.Count != 1 || _service == null)
            return;
        var item = items[0];
        var svc = _service;
        var bucket = _bucket;

        _ = ShowPropertiesAsync(svc, bucket, item);
    }

    private async Task ShowPropertiesAsync(R2Service svc, string bucket, R2Item item)
    {
        try
        {
            string details;
            if (item.IsFolder)
            {
                var keys = await svc.ListAllKeysAsync(bucket, item.Key);
                var fileKeys = keys.Where(k => !k.EndsWith("/")).ToList();
                long size = 0;
                foreach (var key in fileKeys)
                {
                    var meta = await svc.GetMetadataAsync(bucket, key);
                    size += meta.ContentLength;
                }
                details = $"名称：{item.Name}\nKey：{item.Key}\n类型：文件夹\n对象数：{fileKeys.Count}\n总大小：{R2Item.FormatSize(size)}";
            }
            else
            {
                var meta = await svc.GetMetadataAsync(bucket, item.Key);
                details = $"名称：{item.Name}\nKey：{item.Key}\n类型：文件\n大小：{R2Item.FormatSize(meta.ContentLength)}\nContent-Type：{meta.Headers.ContentType}\nETag：{meta.ETag}\n修改时间：{meta.LastModified.ToLocalTime():yyyy-MM-dd HH:mm:ss}";
            }
            MessageBox.Show(this, details, "属性", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"获取属性失败：{ex.Message}", "属性", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ===================== 新建文件夹 =====================

    private async void BtnNewFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_service == null || string.IsNullOrEmpty(_bucket))
            return;
        var name = InputDialog.ShowInput(this, "新建文件夹", "文件夹名称：");
        if (string.IsNullOrEmpty(name))
            return;
        if (name.Contains('/') || name.Contains('\\'))
        {
            MessageBox.Show(this, "名称不能包含 / 或 \\。", "新建文件夹", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        try
        {
            await _service.CreateFolderAsync(_bucket, _prefix + name + "/");
            await LoadObjectsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"创建失败：{ex.Message}", "新建文件夹", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ===================== 传输队列 =====================

    private void OnQueueChanged(object? sender, EventArgs e)
    {
        TransferList.Items.Refresh();

        var (done, total) = _queue.GetAggregate();
        TransferProgressBar.Value = total > 0 ? done * 100.0 / total : 0;

        var running = _queue.Items.Count(i => i.State == TransferState.Running);
        var waiting = _queue.Items.Count(i => i.State == TransferState.Waiting);
        var failed = _queue.Items.Count(i => i.State == TransferState.Failed);
        TransferSummary.Text = $"{_queue.Items.Count} 项任务 · {running} 进行中 · {waiting} 等待 · {failed} 失败";
    }

    private void BtnCancelTransfer_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TransferItem item })
        {
            _queue.Cancel(item);
        }
    }

    private void BtnClearTransfers_Click(object sender, RoutedEventArgs e)
        => _queue.RemoveCompleted();

    // ===================== 窗口 / 托盘 =====================

    private bool _allowClose;

    /// <summary>允许真正关闭窗口（托盘“退出”时由 App 调用，绕过最小化到托盘）。</summary>
    public void AllowClose()
    {
        _allowClose = true;
    }

    // ===================== 自定义标题栏 =====================

    /// <summary>标题栏拖拽移动窗口；双击切换最大化/还原。</summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleMaximize();
            return;
        }
        if (WindowState == WindowState.Maximized)
        {
            // 最大化状态下拖拽：按鼠标比例还原后继续拖动
            var pos = e.GetPosition(this);
            var screen = PointToScreen(pos);
            WindowState = WindowState.Normal;
            Left = screen.X - pos.X;
            Top = screen.Y - pos.Y;
            try { DragMove(); } catch (InvalidOperationException) { }
        }
        else
        {
            try { DragMove(); } catch (InvalidOperationException) { }
        }
    }

    private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        => ToggleMaximize();

    private void BtnClose_Click(object sender, RoutedEventArgs e)
        => Close();

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;

        if (_settings.MinimizeToTrayOnClose)
        {
            e.Cancel = true;
            Hide();
            ((App)Application.Current).Notify("R2 Explorer 仍在运行", "已最小化到系统托盘，双击托盘图标可恢复。");
        }
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settings.MinimizeToTrayOnMinimize)
        {
            Hide();
        }

        // 无边框窗口最大化时对齐工作区，避免覆盖任务栏
        if (WindowState == WindowState.Maximized)
        {
            var wa = SystemParameters.WorkArea;
            MaxHeight = wa.Height;
            MaxWidth = wa.Width;
            Left = wa.Left;
            Top = wa.Top;
        }
        else
        {
            MaxHeight = double.PositiveInfinity;
            MaxWidth = double.PositiveInfinity;
        }

        // 更新标题栏最大化/还原图标与提示
        if (BtnMaximize != null)
        {
            bool isMax = WindowState == WindowState.Maximized;
            BtnMaximize.Content = isMax ? "\uE923" : "\uE922"; // 还原 / 最大化
            BtnMaximize.ToolTip = isMax ? "还原" : "最大化";
        }
    }

    // ===================== 状态 / UI 辅助 =====================

    private void SetStatus(string text)
    {
        StatusText.Text = text;
        StatusText.ToolTip = text;
    }

    private void SetConn(bool connected)
    {
        ConnText.Text = connected ? "已连接" : "未连接";
        ConnDot.Fill = connected
            ? (Brush)FindResource("SuccessBrush")
            : (Brush)FindResource("TextSecondaryBrush");
    }

    private void SetLoading(bool loading)
    {
        LoadingOverlay.Visibility = loading ? Visibility.Visible : Visibility.Collapsed;
        ObjectGrid.IsEnabled = !loading;
        AccountList.IsEnabled = !loading;
    }
}
