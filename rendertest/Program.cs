using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // 精确模拟真实程序：
        // 1. BtnIcon 样式用 Style.Setter 设置 FontFamily="Segoe MDL2 Assets"（样式值，非本地值）
        // 2. 全局隐式 TextBlock 样式 FontFamily="Segoe UI, Microsoft YaHei UI, PingFang SC"
        // 3. Button.Content = 裸字符串

        var implicitTextBlockStyle = new Style(typeof(TextBlock));
        implicitTextBlockStyle.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new FontFamily("Segoe UI, Microsoft YaHei UI, PingFang SC")));
        implicitTextBlockStyle.Setters.Add(new Setter(TextBlock.FontSizeProperty, 13d));
        implicitTextBlockStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, Brushes.White));

        var btnIconStyle = new Style(typeof(Button));
        btnIconStyle.Setters.Add(new Setter(Button.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets")));
        btnIconStyle.Setters.Add(new Setter(Button.FontSizeProperty, 15d));
        btnIconStyle.Setters.Add(new Setter(Button.WidthProperty, 34d));
        btnIconStyle.Setters.Add(new Setter(Button.HeightProperty, 34d));
        btnIconStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(0)));
        btnIconStyle.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
        btnIconStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));

        // 放 Application 级资源（模拟 Styles.xaml 的全局隐式样式）
        var app = new Application();
        app.Resources.Add(typeof(TextBlock), implicitTextBlockStyle);

        Console.WriteLine("=== BUG REPRO: BtnIcon style + bare string Content ===");
        var b = new Button { Content = ((char)0xE710).ToString(), Style = btnIconStyle };
        RenderButton(app, b);

        Console.WriteLine();
        Console.WriteLine("=== FIX: explicit TextBlock with local FontFamily ===");
        var tb = new TextBlock { Text = ((char)0xE710).ToString(), FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 15, Foreground = Brushes.White };
        var b2 = new Button { Content = tb, Style = btnIconStyle };
        RenderButton(app, b2);

        app.Shutdown();
    }

    private static void RenderButton(Application app, Button b)
    {
        b.Measure(new Size(34, 34));
        b.Arrange(new Rect(0, 0, 34, 34));
        var rtb = new RenderTargetBitmap(34, 34, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(b);
        var px = new byte[34 * 34 * 4];
        rtb.CopyPixels(px, 34 * 4, 0);

        int count = 0;
        for (int y = 0; y < 34; y++)
            for (int x = 0; x < 34; x++)
                if (px[(y * 34 + x) * 4 + 3] > 80) count++;
        Console.WriteLine($"nonTransparent px: {count}");

        for (int y = 0; y < 34; y++)
        {
            var sb = new System.Text.StringBuilder();
            for (int x = 0; x < 34; x++)
            {
                int a = px[(y * 34 + x) * 4 + 3];
                sb.Append(a > 150 ? '#' : a > 90 ? '+' : a > 45 ? '.' : ' ');
            }
            if (sb.ToString().Trim().Length > 0)
                Console.WriteLine(sb.ToString());
        }
    }
}