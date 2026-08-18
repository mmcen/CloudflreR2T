using System.Drawing;
using System.Windows.Forms;

namespace R2Explorer.Services;

/// <summary>
/// 系统托盘图标：支持“关闭最小化到托盘”“双击恢复”“右键菜单”。
/// </summary>
public class TrayService : IDisposable
{
    private readonly NotifyIcon _icon;

    public TrayService(Action showWindow, Action exitApp)
    {
        _icon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "R2 Explorer",
            Visible = true,
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add("打开 R2 Explorer", null, (_, _) => showWindow());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => exitApp());
        _icon.ContextMenuStrip = menu;

        _icon.DoubleClick += (_, _) => showWindow();
        _icon.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Middle)
                showWindow();
        };
    }

    /// <summary>显示一条气泡通知（避免频繁打扰，失败时静默忽略）。</summary>
    public void ShowBalloon(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        try
        {
            _icon.ShowBalloonTip(3000, title, message, icon);
        }
        catch
        {
            // 忽略通知失败
        }
    }

    private static Icon LoadAppIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
            {
                var icon = Icon.ExtractAssociatedIcon(path);
                if (icon != null)
                    return icon;
            }
        }
        catch
        {
            // 回退到系统图标
        }
        return SystemIcons.Application;
    }

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
    }
}
