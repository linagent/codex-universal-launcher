using System.Diagnostics;
using System.Text;

namespace CodexUniversalLauncher;

internal sealed class LauncherForm : Form
{
    private readonly LauncherOptions _options;
    private readonly Panel _header = new();
    private readonly PictureBox _brandIcon = new();
    private readonly Label _title = new();
    private readonly Label _subtitle = new();
    private readonly Label _modeBadge = new();
    private readonly Label _status = new();
    private readonly ProgressBar _progress = new();
    private readonly TextBox _details = new();
    private readonly Button _closeButton = new();
    private readonly Button _reportButton = new();
    private readonly List<string> _resultLines = [];
    private string? _reportPath;

    public int ExitCode { get; private set; } = 1;

    public LauncherForm(LauncherOptions options)
    {
        _options = options;
        Text = "Codex启动器，不再5次重连";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = !options.Silent;
        if (options.Silent)
        {
            Opacity = 0;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
        }
        ClientSize = new Size(820, 600);
        Font = new Font("Microsoft YaHei UI", 10F);
        BackColor = Color.FromArgb(244, 247, 251);

        _header.BackColor = Color.FromArgb(8, 28, 61);
        _header.Location = Point.Empty;
        _header.Size = new Size(820, 104);

        _brandIcon.Image = LoadBrandIcon();
        _brandIcon.SizeMode = PictureBoxSizeMode.Zoom;
        _brandIcon.Location = new Point(24, 15);
        _brandIcon.Size = new Size(74, 74);

        _title.Text = "Codex启动器，不再5次重连";
        _title.ForeColor = Color.White;
        _title.Font = new Font(Font.FontFamily, 19F, FontStyle.Bold);
        _title.AutoSize = true;
        _title.Location = new Point(112, 18);

        _subtitle.Text = "自动发现代理与 VPN · 验证连接 · 安全启动";
        _subtitle.ForeColor = Color.FromArgb(177, 213, 245);
        _subtitle.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Regular);
        _subtitle.AutoSize = true;
        _subtitle.Location = new Point(114, 60);

        _modeBadge.Text = options.Demo ? "演示模式" : options.CheckOnly ? "只读检查" : options.NoRepair ? "不修复" : "智能预检";
        _modeBadge.ForeColor = Color.White;
        _modeBadge.BackColor = options.CheckOnly
            ? Color.FromArgb(31, 138, 220)
            : Color.FromArgb(12, 174, 132);
        _modeBadge.TextAlign = ContentAlignment.MiddleCenter;
        _modeBadge.Font = new Font(Font.FontFamily, 9F, FontStyle.Bold);
        _modeBadge.Location = new Point(704, 32);
        _modeBadge.Size = new Size(88, 30);

        _status.Text = "准备检查……";
        _status.ForeColor = Color.FromArgb(25, 45, 72);
        _status.Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold);
        _status.AutoSize = false;
        _status.Location = new Point(28, 120);
        _status.Size = new Size(764, 30);

        _progress.Style = ProgressBarStyle.Marquee;
        _progress.MarqueeAnimationSpeed = 24;
        _progress.Location = new Point(28, 156);
        _progress.Size = new Size(764, 8);

        _details.Multiline = true;
        _details.ReadOnly = true;
        _details.ScrollBars = ScrollBars.Vertical;
        _details.BackColor = Color.FromArgb(251, 252, 254);
        _details.ForeColor = Color.FromArgb(36, 53, 76);
        _details.Font = new Font("Cascadia Mono", 9.25F);
        _details.BorderStyle = BorderStyle.FixedSingle;
        _details.Location = new Point(28, 182);
        _details.Size = new Size(764, 354);

        _reportButton.Text = "打开报告位置";
        _reportButton.Enabled = false;
        _reportButton.FlatStyle = FlatStyle.Flat;
        _reportButton.FlatAppearance.BorderColor = Color.FromArgb(180, 194, 211);
        _reportButton.Location = new Point(550, 552);
        _reportButton.Size = new Size(132, 34);
        _reportButton.Click += (_, _) => OpenReportLocation();

        _closeButton.Text = "关闭";
        _closeButton.Enabled = false;
        _closeButton.FlatStyle = FlatStyle.Flat;
        _closeButton.FlatAppearance.BorderSize = 0;
        _closeButton.BackColor = Color.FromArgb(13, 55, 112);
        _closeButton.ForeColor = Color.White;
        _closeButton.Location = new Point(692, 552);
        _closeButton.Size = new Size(100, 34);
        _closeButton.Click += (_, _) => Close();

        _header.Controls.AddRange([_brandIcon, _title, _subtitle, _modeBadge]);
        Controls.AddRange([_header, _status, _progress, _details, _reportButton, _closeButton]);
#if SCREENSHOT
        Scale(new SizeF(1.75F, 1.75F));
        ClientSize = new Size(1435, 1120);
#endif
        Shown += async (_, _) =>
        {
            if (options.Demo)
                await RunDemoAsync();
            else
                await RunAsync();
        };
    }

    private static Image? LoadBrandIcon()
    {
        try
        {
            var assembly = typeof(LauncherForm).Assembly;
            var resourceName = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith("icon-primary-128.png", StringComparison.OrdinalIgnoreCase));
            if (resourceName is null)
                return null;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                return null;
            using var image = Image.FromStream(stream);
            return new Bitmap(image);
        }
        catch
        {
            return null;
        }
    }

    private async Task RunDemoAsync()
    {
        Step("读取 Codex 目标并发现系统网络路径……");
        AddResult("TARGETS", "OK", "chatgpt.com、api.openai.com、auth.openai.com");
        await Task.Delay(3000);
        AddResult("SYSTEM_PROXY", "INFO", "已自动发现本机 HTTP 代理");
        AddResult("PAC", "INFO", "未配置 PAC");
        AddResult("CANDIDATES", "INFO", "发现 3 个去重候选");
        Step("测试 VPN/TUN/直连与代理 TLS……");
        await Task.Delay(900);
        AddResult("TLS", "OK", "direct/TUN -> auth.openai.com | TLS OK");
        AddResult("TLS", "OK", "selected HTTP proxy -> chatgpt.com | TLS OK");
        AddResult("TLS", "OK", "selected HTTP proxy -> api.openai.com | TLS OK");
        AddResult("TLS", "OK", "selected HTTP proxy -> auth.openai.com | TLS OK");
        await Task.Delay(700);
        AddResult("ROUTE", "OK", "采用已配置代理，通过 CONNECT 与目标 TLS 双层验证");
        AddResult("ENVIRONMENT", "OK", "无需修改用户代理环境变量");
        AddResult("APP", "OK", "已动态定位 ChatGPT/Codex Windows 应用");
        Step("演示完成；此模式没有访问或修改本机网络设置。");
        ExitCode = 0;
        _progress.Style = ProgressBarStyle.Blocks;
        _progress.Value = 100;
        _closeButton.Enabled = true;
    }

    private async Task RunAsync()
    {
        CodexContext? context = null;
        ProxyDiscoveryResult? discovery = null;
        ConnectivityDecision? decision = null;
        EnvironmentRepairResult repair = new([], "未执行", null);
        InstalledCodexApp? app = null;

        try
        {
            AppStorage.Log("START " + (_options.CheckOnly ? "check-only" : "launch"));
            Step("读取 Codex 安全配置字段与目标域名……");
            context = CodexContextInspector.Inspect();
            AddResult("TARGETS", "OK", string.Join("、", context.Targets.Select(target =>
                target.Required ? target.Display : target.Display + "(辅助)")));
            if (context.CustomProviderHosts.Count > 0)
                AddResult("PROVIDER", "INFO", "检测到自定义 provider：" + string.Join("、", context.CustomProviderHosts));

            Step("从系统代理、PAC、环境变量和监听端口发现候选……");
            discovery = await ProxyDiscovery.DiscoverAsync(context.Targets, CancellationToken.None);
            AddResult("SYSTEM_PROXY", "INFO", discovery.SystemProxySummary);
            AddResult("PAC", "INFO", discovery.PacSummary);
            AddResult("CANDIDATES", "INFO", $"发现 {discovery.Candidates.Count} 个去重候选");
            if (discovery.ActiveTunnelAdapters.Count > 0)
                AddResult("VPN_TUN", "INFO", string.Join("；", discovery.ActiveTunnelAdapters.Take(4)));

            decision = await ConnectivityAnalyzer.AnalyzeAsync(
                discovery,
                context.Targets,
                Step,
                CancellationToken.None);
            foreach (var probe in decision.Probes.Where(probe => probe.Success).TakeLast(8))
                AddResult(probe.TlsValidated ? "TLS" : "TUNNEL", probe.TlsValidated ? "OK" : "LIMITED",
                    $"{probe.Route} | {probe.Detail}");
            AddResult("ROUTE", decision.IsUsable ? "OK" : "FAIL", decision.Summary);
            if (!decision.IsUsable)
                throw new InvalidOperationException(decision.Summary + "。请打开代理/VPN、切换节点后重试。 ");

            Step(_options.CheckOnly || _options.NoRepair
                ? "计算安全修复计划（不写入）……"
                : "备份并校正 Codex 用户代理环境……");
            var shouldRepair = !_options.CheckOnly && !_options.NoRepair;
            repair = EnvironmentRepair.Apply(decision, discovery, shouldRepair);
            AddResult("ENVIRONMENT", repair.Changed ? (shouldRepair ? "REPAIRED" : "PLAN") : "OK", repair.Summary);
            if (!string.IsNullOrWhiteSpace(repair.BackupPath))
                AddResult("BACKUP", "OK", repair.BackupPath);

            Step("动态定位已安装的 ChatGPT/Codex Windows 应用……");
            app = await CodexAppLocator.FindAsync(CancellationToken.None);
            if (app is null)
                throw new InvalidOperationException("未在 Windows 开始菜单中找到已安装的 ChatGPT/Codex 应用。 ");
            AddResult("APP", "OK", $"{app.Name} | {app.AppId}");

            _reportPath = AppStorage.WriteDiagnosticReport(context, discovery, decision, repair, app);
            _reportButton.Enabled = !string.IsNullOrWhiteSpace(_reportPath);

            if (!_options.CheckOnly)
            {
                if (_options.ShouldCloseExistingApp)
                {
                    var shutdown = await AppProcessManager.CloseRunningAppAsync(Step, CancellationToken.None);
                    if (shutdown.Detected > 0)
                    {
                        var state = shutdown.ForcedClosed > 0 ? "FORCED" : "OK";
                        AddResult(
                            "APP_CLOSE",
                            state,
                            $"检测 {shutdown.Detected}，正常关闭 {shutdown.GracefullyClosed}，强制结束 {shutdown.ForcedClosed}");
                    }
                    else
                    {
                        AddResult("APP_CLOSE", "NONE", "未检测到正在运行的 ChatGPT 应用");
                    }
                }
                else if (_options.KeepExistingApp && AppProcessManager.HasRunningApp())
                {
                    AddResult("APP_CLOSE", "SKIPPED", "--keep-existing 已保留当前运行中的应用");
                }

                Step("检查通过，正在打开 ChatGPT/Codex……");
                CodexAppLocator.Launch(app);
                AddResult("LAUNCH", "OK", app.AppId);
            }

            Step(_options.CheckOnly
                ? "只读检查完成；没有修改系统或代理软件。"
                : "启动请求已发出；请用第一条真实任务验证持续流式输出。 ");
            ExitCode = 0;
            AppStorage.Log("SUCCESS " + decision.Summary);
        }
        catch (Exception ex)
        {
            AddResult("ERROR", "FAIL", ex.Message);
            Step(_options.CheckOnly ? "检查未通过。" : "启动前检查未通过，未继续打开应用。 ");
            AppStorage.Log("ERROR " + ex);
            if (!_options.Silent)
            {
                MessageBox.Show(
                    ex.Message + "\n\n启动器没有修改代理软件、路由、DNS 或 Winsock。",
                    "Codex 网络启动检查",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
        finally
        {
            if (_reportPath is null && context is not null && discovery is not null && decision is not null)
            {
                _reportPath = AppStorage.WriteDiagnosticReport(context, discovery, decision, repair, app);
                _reportButton.Enabled = !string.IsNullOrWhiteSpace(_reportPath);
            }
            WriteResult();
            _progress.Style = ProgressBarStyle.Blocks;
            _progress.Value = ExitCode == 0 ? 100 : 0;
            _closeButton.Enabled = true;
            if (_options.Silent)
                Close();
            else if (!_options.CheckOnly && ExitCode == 0)
            {
                await Task.Delay(1300);
                Close();
            }
        }
    }

    private void Step(string message)
    {
        _status.Text = message;
        _status.Refresh();
        AppStorage.Log("STEP " + message);
    }

    private void AddResult(string check, string state, string detail)
    {
        var line = $"{check,-18} {state,-9} {SafeText.Redact(detail)}";
        _resultLines.Add(line);
        _details.AppendText(line + Environment.NewLine);
        _details.SelectionStart = _details.TextLength;
        _details.ScrollToCaret();
        AppStorage.Log("RESULT " + line);
    }

    private void WriteResult()
    {
        if (string.IsNullOrWhiteSpace(_options.ResultPath))
            return;
        try
        {
            var fullPath = Path.GetFullPath(_options.ResultPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllLines(
                fullPath,
                new[]
                {
                    $"EXIT_CODE={ExitCode}",
                    $"TIME={DateTimeOffset.Now:O}",
                    $"REPORT={_reportPath ?? "<none>"}"
                }.Concat(_resultLines),
                new UTF8Encoding(true));
        }
        catch (Exception ex)
        {
            AppStorage.Log("RESULT_WRITE_FAIL " + ex.Message);
        }
    }

    private void OpenReportLocation()
    {
        if (string.IsNullOrWhiteSpace(_reportPath))
            return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{_reportPath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "无法打开报告位置", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
