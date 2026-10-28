<p align="center"><img src="docs/icon.png" width="88" alt="拾影花笺的紫色小花截图图标"></p>
<h1 align="center">拾影花笺 · AgentBloom</h1>
<p align="center">把截图挂在屏幕边缘，把灵感收进小花园。<br>A playful screenshot shelf and local collection for Windows.</p>
<p align="center"><a href="https://github.com/Monokumakuki/AgentBloom/releases/latest">下载 Windows 版</a> · <a href="https://github.com/Renly1994/PicRope">PicRope 原作</a> · <a href="NOTICE.md">来源与许可</a></p>

> **这是对 [Renly1994 大佬的 PicRope（图绳）](https://github.com/Renly1994/PicRope) 的二次开发。**
> 截图挂绳的创意与大量基础实现来自原作，不能把这些说成从零原创。
> 感谢 Renly1994，也感谢更早的上游 [Alejandro Buján / Tendedero](https://github.com/alejandrobujan/tendedero)。欢迎先去看看、支持原作；分享与继续开发请尊重原作者，保留来源、版权和许可。本项目是独立衍生版本，不代表原作者或任何模型厂商官方。

## 下载与运行

1. 打开 **[Releases 下载页](https://github.com/Monokumakuki/AgentBloom/releases/latest)**，下载 `AgentBloom-0.1.0-windows-x64.zip`。普通使用请选择这个文件，不是 GitHub 自动生成的 Source code。
2. 完整解压到一个你可读写的文件夹，例如 `D:\Apps\AgentBloom`；不要直接在压缩包内运行。
3. 双击 `Start-AgentBloom.vbs`，或打开 `app\AgentBloom.exe`。不需要账号、模型 API Key 或授权码。
4. 如果 `·` 被其他截图软件占用，在收集箱的快捷键设置中更换。默认不会接入任何 AI 模型服务。

环境：**Windows 10 2004 或更新版本 / Windows 11，64 位，.NET Framework 4.8，Microsoft Edge WebView2 Evergreen Runtime**。多数 Windows 11 电脑已有运行组件；缺少 WebView2 时请从 [Microsoft 官方页面](https://developer.microsoft.com/microsoft-edge/webview2/) 安装。

这是便携式早期公开版本，尚未进行代码签名。请使用本仓库的发布包，并与同一 Release 的 `SHA256SUMS.txt` 核对；不需要管理员身份。移动软件前先退出，移动后重新启动并重新设置开机启动。

## 怎么用

| 操作 | 作用 |
| --- | --- |
| 单按 Esc 下方的 `·` / 反引号键 | 截图：窗口自动高亮；单击截窗口，拖动自由框选 |
| 截图时 `Esc` 或右键 | 取消截图，不生成新图片 |
| `Shift + ·` | 打开收集箱；`Esc` 收回 |
| 鼠标靠近屏幕上边缘 | 唤出挂图与左上转轮；移开后收回 |
| 单击挂着的图片 | 复制；拖动可把图片文件拖到其他应用 |
| 点击挂图的角色头像 | 取下并贴在桌面，默认放大 |
| 取下后拖动图片／拖四个边角 | 移动／等比缩放 |
| 点击取下的图片后按 `Esc`、点头像或拖到顶部 | 带过渡动画挂回 |
| 右键图片 | 原地编辑：画笔、矩形、箭头、文字、马赛克、撤销和重做 |
| 左侧“收集”书签 | 展开／收回收集箱；按住可上下拖动并记住位置 |
| 收集箱右侧书签、“－”或 `Esc` | 收回窗口；顶栏可拖动 |
| 转轮滚动／点击角色 | 转动小伙伴／切换挂饰；中间镂空可点击后方应用 |

`·` 按物理 OEM_3 键识别，不要求输入法实际打出中点字符；可在设置中改键。收集箱支持搜索、分类、备注、收藏、导入和导出。一键清理保护已收藏图片，也可恢复最近清理的记录。

图片未保存的编辑默认直接放弃并挂回；需要提醒时，可在托盘菜单启用。文字标注沿用 **荆南波波黑**。保留七位角色头像、五套挂饰配色和安静模式；角色只是本地视觉元素。

## 大小与运行内存

| 项目 | 本次发布／本机快照 |
| --- | --- |
| Windows 下载压缩包 | 约 17.2 MB（16.4 MiB） |
| 解压后的文件总量 | 约 22.4 MB（21.4 MiB），不含之后的截图与缓存 |
| 空收集箱打开 | 本机约 537 MiB，包含应用与 WebView2 的 7 个相关进程 |
| 收回后待机 | 本机短时约 540 MiB；系统不一定立即回收缓存 |
| 本机已有约百张截图的日常实例 | 一次快照约 781 MiB，内容和进程状态不同，不能当作严格对比 |

运行内存数字为 Windows 进程树工作集合计（可能重复计入共享页），空收集箱使用隔离数据模式测量；不是“程序独占物理内存”的精确值，也不是内存上限。

安装体积与运行内存不是一回事。下载包不捆绑整个 WebView2 Runtime；如果机器上没有它，安装运行组件还会额外占用磁盘。截图数量、分辨率、打开收集箱与编辑窗口、WebView2 缓存和系统回收行为都会影响内存，以下快照不是所有设备的承诺。

## 数据、隐私与开机启动

- 图片、备注、分类等保存在**解压目录下的 `data`**，不是仓库中携带的数据。备份或迁移时请先退出并复制整个文件夹。
- 应用不把截图上传到服务器；角色不调用模型接口。手动检查更新会请求本项目的 GitHub Release，WebView2 的运行组件由 Microsoft 自身维护。
- 退出软件时清空桌面挂图，收集箱中的历史继续保留；导入会复制到自己的存储，不改写外部原图。编辑保存保留相应原图备份。
- **公开版默认不替新用户开启登录自启动**。需要时在收集箱设置里勾选“开机启动”；取消勾选可关闭。首次运行打开收集箱，自启动运行只露出边缘书签。
- 发布包没有维护者的截图、历史索引、账号、访问令牌或个人配置。源码仓库不包含内部测试、日志、录像、备份和开发缓存。

## 从源码构建

```powershell
git clone https://github.com/Monokumakuki/AgentBloom.git
cd AgentBloom
powershell.exe -NoProfile -File .\build.ps1
# 运行 build\AgentBloom.exe
```

构建使用 Windows 自带的 .NET Framework 编译器；必要的 WebView2 SDK、编辑器文件和挂饰资源已随源码保留，无需再克隆整个上游。无需 Node.js 或 Python。生成下载包：

```powershell
powershell.exe -NoProfile -File .\packaging\package.ps1
```

源码结构：`src/Native` 为 WPF、Win32 与截图实现，`src/editor` 为兼容图片编辑器包装，`ui` 为收集箱界面，`vendor` 为构建所需第三方依赖，`licenses` 与 `NOTICE.md` 为原始版权及来源。

## 二次开发范围与许可

在 PicRope 基础上增加、调整了收集箱与分类、七位角色挂饰、珍珠转轮、可移动书签、截图窗口吸附、原地标注、图片边角缩放、回挂交接及相关性能优化。截图收集、剪贴板、原生窗口、弹簧动效等代码大量继承并修改自上游；详见 [NOTICE.md](NOTICE.md)。

代码按 [MIT](LICENSE) 提供，并保留 PicRope / Tendedero 原始声明。**字体、角色图片与第三方依赖遵循各自许可，不因代码 MIT 而自动重新授权**。荆南波波黑使用 [SIL OFL 1.1](ui/assets/fonts/OFL.txt)。请保留署名、版权和授权文件；请勿冒充原作、抹去来源或暗示作者背书。

还在持续改善：动画流畅度、首次唤起、大量截图和多屏混合 DPI 的表现因设备而异。遇到问题欢迎提交 Issue，附 Windows 版本、缩放比例和复现步骤；请先遮挡私人内容，不要上传自己的 `data` 文件夹。
