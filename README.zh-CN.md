<div align="center">
  <img src="assets/brand/icon-primary-256.png" width="128" alt="Codex 通用启动器图标">
  <h1>Codex 通用网络启动器</h1>
  <p>自动发现 Windows 上真正可用的网络路径，验证 TLS 后再启动 ChatGPT/Codex。</p>

  [English](README.md) · [下载最新版](https://github.com/linagent/codex-universal-launcher/releases/latest) · [反馈问题](https://github.com/linagent/codex-universal-launcher/issues/new?template=bug_report.yml)
</div>

> [!IMPORTANT]
> 这是非官方社区项目，与 OpenAI 不存在隶属、赞助或背书关系。它可以减少“代理配置过期或桌面进程没有继承代理”导致的启动重连，但不能保证所有人的 Codex 永不重连；账号、服务状态、第三方 provider、额度以及流式运行时问题不在它的控制范围内。

## 它解决什么问题

Windows 上可能同时存在系统代理、PAC、WinHTTP、代理环境变量、本地 HTTP/SOCKS 监听端口，以及 VPN/TUN 网卡。浏览器能打开网页，不代表桌面应用继承到了正确路径。

启动器会在打开 ChatGPT/Codex 前执行有限、透明的预检：

1. 读取官方目标域名，以及 Codex 配置里自定义 `base_url` 的主机名。
2. 从标准 Windows 信号自动发现 HTTP/SOCKS 代理和 VPN/TUN 路径，不绑定某个代理软件或固定端口。
3. 通过真实 TCP 隧道和 TLS 握手验证直连、TUN 与候选代理。
4. 必要时先备份，再只修正当前用户的代理环境变量。
5. 动态定位已安装的 ChatGPT/Codex Windows 应用，通过预检后才启动。

它**不会**安装、配置、重启或控制你的代理/VPN 软件，也不会重置 DNS、Winsock、路由、防火墙或 Codex 数据。

## 页面截图

| 正在预检 | 路径验证完成 |
| --- | --- |
| ![正在预检](assets/screenshots/preflight-in-progress.png) | ![路径验证完成](assets/screenshots/preflight-success.png) |

截图来自 `--demo` 脱敏演示模式，不包含真实代理端口、AppID、账号或本机路径。

## 三步使用

1. 从 [Releases](https://github.com/linagent/codex-universal-launcher/releases) 下载对应压缩包：绝大多数电脑选择 `win-x64`，Windows on Arm 设备选择 `win-arm64`。
2. 解压到普通文件夹；如需更严格校验，可对照 Release 说明检查 SHA-256。
3. 保持代理/VPN 正常运行，双击 `CodexUniversalLauncher.exe`。预检通过后，启动器会打开 ChatGPT/Codex。

发布包为自包含单文件，不需要另外安装 .NET。目前二进制没有代码签名，Windows SmartScreen 可能显示“未知发布者”；不愿放行时，请直接审查源码并自行构建。

## 运行模式

```text
CodexUniversalLauncher.exe --check-only
CodexUniversalLauncher.exe --no-repair
CodexUniversalLauncher.exe --silent --result result.txt
CodexUniversalLauncher.exe --demo
CodexUniversalLauncher.exe --self-test --result self-test.txt
```

| 参数 | 行为 |
| --- | --- |
| `--check-only` | 完全只读诊断；不写环境变量，也不启动应用。 |
| `--no-repair` | 验证路径并启动应用，但只预览环境变量修复计划。 |
| `--silent` | 隐藏窗口运行，并写入指定结果文件。 |
| `--result <路径>` | 写出简短的机器可读结果。 |
| `--demo` | 显示脱敏示例；不读取或修改本机网络。 |
| `--self-test` | 运行离线解析、脱敏和安全边界测试。 |

首次使用建议先运行 `--check-only`，查看本地报告后再正常启动。

## 可发现的网络信号

- Windows 系统代理与 PAC 解析结果
- 用户、进程和计算机级 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY`
- WinHTTP 代理配置
- 常见代理/VPN 内核的本地监听端口，端口运行时发现而非写死
- HTTP、HTTPS 代理 CONNECT 与 SOCKS5 隧道
- 直连、透明 VPN 与 TUN 路径
- Codex `config.toml` 中自定义 provider 的 `base_url` 主机名
- 开始菜单里的 ChatGPT/Codex AppID，不写死安装包标识

发现过程是尽力而为：带企业认证的复杂 PAC、不暴露标准 Windows 信号的软件、非 HTTP/SOCKS 私有协议或禁止 TLS 探测的策略，可能需要人工配置。

## 会修改什么、如何回退

普通启动模式只有在已验证路径确实需要时，才可能修改以下**当前用户级**变量：

- `HTTP_PROXY`
- `HTTPS_PROXY`
- `ALL_PROXY`
- `NO_PROXY`

写入前会在 `%LOCALAPPDATA%\CodexUniversalLauncher\backups` 保存 JSON 备份。若代理 URL 内含账号或密码，启动器拒绝自动复制和落盘。修复后如果 ChatGPT/Codex 已经运行，启动器会先询问是否正常退出；需要强制结束进程时还会再次确认。

系统代理注册表、VPN 配置、DNS、Winsock、防火墙、凭据、Cookie、聊天内容和项目文件都不会被修改。

## 隐私

- 无遥测、无统计、无广告、不会上传日志。
- 报告与轮转日志只保存在 `%LOCALAPPDATA%\CodexUniversalLauncher`。
- 报告包含网络路径诊断和本地监听信息，分享前仍应人工检查。
- Bearer Token、API Key 特征、URL 凭据和常见 Secret 字段会脱敏。
- Codex 配置只读取 `base_url` 的主机名，不读取 API Key 或聊天内容。

安全边界与漏洞反馈方式见 [SECURITY.md](SECURITY.md)。

## 从源码构建

需要 Windows 10/11 和 .NET 8 SDK：

```powershell
dotnet restore
dotnet build -c Release
dotnet run -c Release -- --self-test --result self-test.txt
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

项目不依赖第三方 NuGet 包。CI 会在 Windows 上构建并运行离线自测。

## 项目状态

2.0.0 是首个公开版本。已完成内容见 [CHANGELOG.md](CHANGELOG.md)，后续计划见 [ROADMAP.md](ROADMAP.md)。欢迎提交兼容性反馈和 PR，参与前请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 商标说明

“OpenAI”“ChatGPT”和“Codex”属于其权利人。本项目使用原创图标，仅用于识别独立的兼容性工具，不复刻 OpenAI 官方 Logo。官方信息请以 [OpenAI 官方开发者文档](https://developers.openai.com/)为准。

## 许可证

[MIT](LICENSE) © 2026 linagent
