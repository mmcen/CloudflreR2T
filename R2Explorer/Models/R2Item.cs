namespace R2Explorer.Models;

/// <summary>
/// 存储桶内的一个文件或文件夹条目。
/// </summary>
public class R2Item
{
    public string Name { get; set; } = "";
    public string Key { get; set; } = "";
    public bool IsFolder { get; set; }
    public long Size { get; set; }
    public DateTime? LastModified { get; set; }
    public string? ETag { get; set; }

    public string SizeDisplay => IsFolder ? "-" : FormatSize(Size);
    public string ModifiedDisplay => LastModified?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "-";
    public string TypeDisplay => IsFolder ? "文件夹" : "文件";

    public static string FormatSize(long bytes)
    {
        if (bytes < 0) return "-";
        string[] units = { "B", "KB", "MB", "GB", "TB", "PB" };
        double size = bytes;
        int u = 0;
        while (size >= 1024 && u < units.Length - 1)
        {
            size /= 1024;
            u++;
        }
        return u == 0 ? $"{bytes} B" : $"{size:0.##} {units[u]}";
    }
}
