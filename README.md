# R2 Explorer

Cloudflare R2 对象存储的 Windows 桌面客户端（WPF / .NET 8 / C#），使用 Visual Studio 2022 编译。

界面参考 [hexhub](https://www.hexhub.cn/) 布局：左侧为「资产 / 帐号管理」，右侧为「文件管理」，支持多种登录模式、常用代理加速、关闭最小化到系统托盘。

## 功能特性

- **四种登录模式**
  - R2 S3 API Token（推荐）：Account ID + Access Key ID + Secret Access Key，直接签名访问
  - R2 API Token：通过 Cloudflare v4 API 自动换取临时 S3 凭证（到期前 5 分钟自动刷新）
  - Cloudflare 全局 API Key：邮箱 + 全局 Key，同样自动换取临时凭证
  - 自定义 S3 兼容端点：支持 MinIO、AWS S3、阿里云 OSS 等任意 S3 服务
- **存储桶管理**：列表、新建、删除（删除前先递归清空对象）
- **对象管理**：浏览（文件夹/文件）、进入/返回上级、路径直达、按名称筛选
  - 上传：按钮菜单或直接拖拽本地文件/文件夹到对象列表
  - 下载：保存为文件或文件夹到本地；支持选中多个对象批量下载
  - 打开：下载到临时目录并用默认程序打开
  - 复制 / 剪切 / 粘贴（粘贴时移动/复制，递归处理文件夹）
  - 重命名、删除（删除前可确认）、属性（大小、Content-Type、ETag、修改时间）
  - 复制公开 URL（配置公开域名时）或生成预签名临时链接（可选 5 分钟～24 小时）
- **传输队列**：并发受限（可配置 1-32），实时进度，支持取消单个任务、清除已完成
- **代理加速**：HTTP / HTTPS / SOCKS5，作用于全部 S3 与 Cloudflare API 请求，支持用户名密码认证，带连接测试
- **系统托盘**：关闭按钮最小化到托盘、最小化时隐藏到托盘、气泡通知，托盘菜单「打开 / 退出」
- **深色 / 浅色主题**，运行时可切换
- 临时凭证模式到期前 5 分钟自动刷新，长传/长下传任务不会因凭证过期中断

## 文档

- [构建与安装指南](docs/BUILD.md)（Visual Studio 2022 / .NET 8，含发布选项）
- [使用说明](docs/USAGE.md)（帐号配置、代理、托盘、上传下载、快捷键、常见问题）

## 项目结构

```
R2Explorer.sln                    解决方案
R2Explorer/
  R2Explorer.csproj               .NET 8 WPF + WinForms（托盘）
  App.xaml(.cs)                   应用入口、主题切换、全局异常
  MainWindow.xaml(.cs)            主界面与全部业务逻辑
  Models/                         帐号、设置、对象、传输、临时凭证等模型
  Services/                       R2/S3 封装、Cloudflare API、代理、队列、托盘、设置持久化
  Dialogs/                        帐号、设置、输入、预签名链接对话框
  Converters/                     可见性 / 图标 / 状态颜色转换器
  Themes/                         深色、浅色主题与控件样式
  Resources/app.ico               应用图标
docs/                             文档
tools/                            图标生成脚本（Perl / Python）
```

## 说明

- 本仓库在无 .NET SDK / 无 WPF 的 Linux 容器中开发，无法本地编译，代码需在 Windows + Visual Studio 2022（勾选「.NET 桌面开发」工作负载）下构建验证。
- AWS SDK for .NET（`AWSSDK.S3`）版本固定为 `3.7.401.2`，升级需重新验证 API 兼容性。
- R2 固定使用 `auto` 区域与 Path-Style 访问；自定义 S3 端点可自由配置。
