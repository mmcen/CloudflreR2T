# 构建与安装指南

## 环境要求

- **操作系统**：Windows 10 / Windows 11（x64 / x86）
- **Visual Studio 2022**（17.8 及以上），安装时勾选：
  - **使用 .NET 的桌面开发**（.NET desktop development）工作负载
  - .NET 8.0 SDK（工作负载自带）
- **网络**：首次还原 `AWSSDK.S3` NuGet 包需要可访问 nuget.org；如公司网络受限可配置 NuGet 镜像源。

> 本项目不使用 C++ 工作负载；仅需「.NET 桌面开发」即可编译。

## 构建步骤

1. 打开解决方案 `R2Explorer.sln`（或使用 VS2022 打开 `R2Explorer/R2Explorer.csproj`）。
2. 等待 NuGet 还原完成（首次会自动下载 `AWSSDK.S3` 及其依赖）。
3. 生成 → 生成解决方案（F7），目标框架 `net8.0-windows`。
4. 输出路径：
   - Debug：`R2Explorer/bin/Debug/net8.0-windows/R2Explorer.exe`
   - Release：`R2Explorer/bin/Release/net8.0-windows/R2Explorer.exe`

## 命令行构建（可选）

```powershell
dotnet restore R2Explorer.sln
dotnet build R2Explorer.sln -c Release
```

> 若在无 Windows 的环境交叉编译，需在项目已配置的 `EnableWindowsTargeting=true` 基础上，再安装 .NET SDK 的 Windows 目标包；推荐直接在 Windows 上构建。

## 发布单文件（可选）

使用 Visual Studio「发布」向导，或命令行：

```powershell
dotnet publish R2Explorer/R2Explorer.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

发布后，将 `publish` 目录中的可执行文件整体拷贝到目标机器即可运行（自包含模式无需安装 .NET 运行时）。

## 安装

- 绿色软件：把编译/发布得到的 `R2Explorer.exe` 放到任意目录直接运行即可。
- 首次运行会在 `%APPDATA%\R2Explorer\settings.json` 生成配置，帐号与设置均保存在该文件中（明文，请勿在共享机器上存放敏感凭证）。

## 常见构建问题

| 现象 | 处理 |
| ---- | ---- |
| 找不到 `OpenFolderDialog` | 确认项目启用 `UseWindowsForms=true`（项目文件已配置），且目标为 `net8.0-windows` |
| NuGet 还原失败 / 超时 | 检查网络；或在 VS「工具 → NuGet 包管理器 → 包源」配置国内镜像源 |
| 提示缺少 .NET 8 SDK | 安装 VS2022「使用 .NET 的桌面开发」工作负载，或单独安装 .NET 8 SDK |
| WPF 设计器报 XAML 错误 | 以生成（Build）为准；设计器对部分自定义模板支持不完整 |
