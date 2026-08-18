using System.Windows;

namespace R2Explorer.Dialogs;

public partial class InputDialog : Window
{
    public InputDialog(string title, string prompt, string defaultText = "")
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        InputBox.Text = defaultText;
        InputBox.SelectAll();
        Loaded += (_, _) => InputBox.Focus();
    }

    public string Value => InputBox.Text.Trim();

    public static string? ShowInput(Window? owner, string title, string prompt, string defaultText = "")
    {
        var dialog = new InputDialog(title, prompt, defaultText) { Owner = owner };
        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text))
        {
            MessageBox.Show(this, "输入内容不能为空。", Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        DialogResult = true;
    }
}
