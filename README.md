# ClipNest

ClipNest 是一个面向 Windows 的剪切板历史工具，使用 WPF 和 .NET 8 开发。它会在本地记录文本剪切板历史，并提供快速搜索、收藏、分类、固定、统计和系统托盘等能力。

## 主要能力

- **剪切板历史**：自动记录文本复制内容，按时间展示，重复内容会合并更新。
- **快速搜索面板**：默认使用 `Ctrl + Shift + V` 唤起，只打开搜索窗口，不打断主界面状态。
- **自定义快捷键**：可以在设置页修改全局快捷键。
- **历史与收藏分区**：搜索页会同时展示收藏条目和历史条目，便于快速定位。
- **收藏管理**：收藏时可以设置别名和分类；收藏页支持分类过滤。
- **分类标签**：支持新增、重命名和删除分类标签。
- **固定收藏**：收藏条目可以固定，固定内容会排在普通收藏前面。
- **拖动排序**：收藏页支持拖动调整收藏条目的顺序。
- **排序切换**：历史页、收藏页和搜索页支持在“最新优先”和“最旧优先”之间切换。
- **一键粘贴**：点击条目即可复制并粘贴到当前应用。
- **键盘操作**：搜索页支持方向键移动选中项，回车粘贴当前选中条目，`Esc` 关闭。
- **使用统计**：主界面左下角显示复制次数、粘贴次数，并支持重置统计。
- **历史容量设置**：默认最多保留 100 条历史，可在设置页调整。
- **暂停记录**：支持临时暂停剪切板记录。
- **开机自启**：安装时可选择开机自动启动，也可以在设置中调整。
- **系统托盘**：关闭窗口后可驻留托盘，支持打开、暂停、设置、清空历史和退出。
- **本地存储**：数据使用 SQLite 保存在本机，不依赖云端服务。

## 下载安装

前往 GitHub Release 下载最新版：

https://github.com/bufeibufei/ClinpNest/releases

推荐下载：

- `ClipNest-Setup-v5.0.0.exe`：Windows 安装包，包含运行所需环境。
- `ClipNest-v5.0.0-win-x64-portable.zip`：便携版，解压后运行 `ClipNest.exe`。

## 默认配置

- 快速搜索快捷键：`Ctrl + Shift + V`
- 历史记录上限：`100`
- 数据库位置：`%LOCALAPPDATA%\ClipNest\clipnest.db`
- 日志位置：`%LOCALAPPDATA%\ClipNest\clipnest.log`

## 使用说明

1. 启动 ClipNest 后，复制文本内容会自动进入历史记录。
2. 在主界面点击历史条目即可粘贴该内容。
3. 点击历史条目的星标按钮，可以将条目加入收藏，并设置别名和分类。
4. 在收藏页可以按分类过滤、固定条目、删除收藏或拖动调整顺序。
5. 按 `Ctrl + Shift + V` 打开搜索页，可以同时搜索历史和收藏。
6. 搜索页支持根据内容、来源、别名和分类进行过滤。

## 开发运行

需要安装 .NET 8 SDK。

```powershell
dotnet run --project .\ClipNest.csproj
```

如果当前终端找不到 `dotnet`，可以使用完整路径：

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' run --project .\ClipNest.csproj
```

## 构建发布

```powershell
dotnet publish .\ClipNest.csproj --configuration Release --runtime win-x64 --self-contained true --output artifacts\publish\win-x64
```

安装包使用 Inno Setup 构建：

```powershell
& 'C:\Users\98465\AppData\Local\Programs\Inno Setup 6\ISCC.exe' installer\ClipNest.iss
```

## 技术栈

- WPF
- .NET 8
- SQLite
- Microsoft.Data.Sqlite
- Inno Setup

## 当前限制

- 当前主要支持文本剪切板历史。
- 暂不支持图片、文件、OCR、云同步和端到端加密。
