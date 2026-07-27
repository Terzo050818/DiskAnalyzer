# DiskAnalyzer

DiskAnalyzer 是一个面向 Windows 10/11 的磁盘大文件定位工具。选择一个固定磁盘后，它会扫描其中的真实文件，并在完成后按文件大小从大到小排列。

应用图标以硬盘、放大镜和大小不同的文件块表达“快速找到磁盘中最大的文件”，并已应用到 EXE、任务栏、窗口标题和主界面。

## 普通用户下载

不需要下载源码，也不需要安装 Visual Studio。

1. 在 GitHub 项目页面右侧点击 **Releases**；
2. 下载最新版 `DiskAnalyzer-版本号-win-x64.zip`；
3. 解压 ZIP；
4. 解压后的文件夹根目录只有一个 `DiskAnalyzer.exe`；
5. 双击 `DiskAnalyzer.exe` 即可使用。

## MVP 功能

- 显示固定磁盘、总容量和剩余空间；
- 后台扫描整个磁盘，界面保持响应；
- 实时显示当前路径、文件数、累计大小和跳过项；
- 自动跳过无权限或扫描期间失效的文件系统项；
- 显示文件名、完整路径、大小、类型和修改时间；
- 扫描完成后按大小降序；
- 通过资源管理器打开文件所在位置；
- 支持随时取消扫描。

## 使用发布包

1. 解压 `DiskAnalyzer-0.1.2-win-x64.zip`；
2. 双击 `DiskAnalyzer.exe`；
3. 选择磁盘并点击“开始扫描”；
4. 扫描结束后，最大的文件位于列表顶部；
5. 选择文件后点击“打开文件位置”，或使用右键菜单。

发布包为 Windows x64 自包含版本，不要求目标电脑预先安装 .NET。

软件当前未进行代码签名，首次运行时 Windows SmartScreen 可能显示未知发布者提示。请只运行来自可信构建来源且 SHA-256 校验一致的文件。

## 从源码构建

要求：

- Windows 10/11 x64；
- .NET 8 SDK；

```powershell
dotnet restore DiskAnalyzer.sln --configfile NuGet.Config
dotnet build DiskAnalyzer.sln --configuration Release
dotnet test DiskAnalyzer.sln --configuration Release
```

生成自包含发布包：

```powershell
.\scripts\publish.ps1
```

输出位于 `artifacts`。

## 当前限制

- MVP 不支持删除、搜索、排除目录、重复文件或缓存识别；
- 不主动申请管理员权限，无权访问的目录会跳过；
- 全部文件元数据保留在内存中，百万文件场景可能占用较多内存；
- 扫描完成时的全量排序可能短暂占用 UI 线程；
- 目录联接点和目录符号链接默认不向下遍历，以避免循环和重复扫描。

架构和性能说明参见：

- [架构设计](docs/ARCHITECTURE.md)
- [性能验证](docs/PERFORMANCE.md)
