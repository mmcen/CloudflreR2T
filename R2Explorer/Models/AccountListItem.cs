using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace R2Explorer.Models;

/// <summary>
/// 左侧帐号列表的展示项。
/// </summary>
public class AccountListItem : INotifyPropertyChanged
{
    public AccountProfile Profile { get; set; }

    public AccountListItem(AccountProfile profile)
    {
        Profile = profile;
    }

    private bool _connected;
    public bool Connected
    {
        get => _connected;
        set
        {
            _connected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(StatusBrush));
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public string Name => Profile.Name;
    public string ModeLabel => AccountModes.Display(Profile.Mode);
    public string EndpointLabel => Profile.ServiceUrl;
    public string StatusText => Connected ? "已连接" : "未连接";

    public Brush StatusBrush
    {
        get
        {
            try
            {
                var app = Application.Current;
                if (app == null) return Brushes.Gray;
                var key = Connected ? "SuccessBrush" : "TextSecondaryBrush";
                return (Brush)app.Resources[key];
            }
            catch
            {
                return Connected ? Brushes.LimeGreen : Brushes.Gray;
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
