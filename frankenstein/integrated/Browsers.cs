using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;









namespace NavegadorSOS
{
	internal enum Level
	{
		Ok,
		Info,
		Warning,
		Critical
	}
	internal sealed class Finding
	{
		public Level Level;

		public string Check;

		public string Result;

		public string Recommendation;

		public Finding(Level level, string check, string result, string recommendation)
		{
			Level = level;
			Check = check;
			Result = result;
			Recommendation = recommendation;
		}
	}
	internal sealed class BrowserInfo
	{
		public string Name;

		public string Exe;

		public string Kind;

		public string UserData;

		public BrowserInfo(string name, string exe, string kind, string userData)
		{
			Name = name;
			Exe = exe;
			Kind = kind;
			UserData = userData;
		}
	}
	internal sealed partial class MainForm : Form
	{
		private sealed class ExecData
		{
			public int ExitCode;

			public string Output;
		}

		private readonly Color Navy = Color.FromArgb(18, 31, 54);

		private readonly Color Blue = Color.FromArgb(37, 99, 235);

		private readonly Color Bg = Color.FromArgb(243, 246, 250);

		private readonly Color Muted = Color.FromArgb(100, 116, 139);

		private readonly Label phase = new Label();

		private readonly Label headline = new Label();

		private readonly Label reportPath = new Label();

		private readonly Label progressText = new Label();

		private Label browserCount = new Label();

		private Label criticalCount = new Label();

		private Label warningCount = new Label();

		private Label okCount = new Label();

		private readonly Button analyze = new Button();

		private readonly Button repair = new Button();

		private readonly DataGridView grid = new DataGridView();

		private readonly RichTextBox activity = new RichTextBox();

		private readonly ProgressBar progress = new ProgressBar();

		private readonly CheckBox fixNetwork = new CheckBox();

		private readonly CheckBox fixInterference = new CheckBox();

		private readonly CheckBox fixCaches = new CheckBox();

		private readonly CheckBox fixGpu = new CheckBox();

		private readonly CheckBox fixExtensions = new CheckBox();

		private readonly CheckBox fixWinsock = new CheckBox();

		private readonly CheckBox fixWindows = new CheckBox();

		private readonly List<Finding> findings = new List<Finding>();

		private readonly List<BrowserInfo> browsers = FindBrowsers();

		private readonly string reportDir;

		private readonly string reportFile;

		private int criticals;

		private int warnings;

		private int oks;

		private int repairPercent;

		private bool busy;

		public MainForm()
		{
			reportDir = Path.Combine(FrankensteinTI.Distribution.DataRoot, "Navegadores");
			reportFile = Path.Combine(reportDir, "Relatorio_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
			Text = "Navegador SOS Professional";
			base.Icon = SystemIcons.Shield;
			base.StartPosition = FormStartPosition.CenterScreen;
			base.Size = new Size(1120, 780);
			MinimumSize = new Size(1000, 700);
			BackColor = Bg;
			Font = new Font("Segoe UI", 9f);
			base.AutoScaleMode = AutoScaleMode.Dpi;
			// Shared navigation is provided by the application shell.
			BuildMain();
			EventHandler value = async delegate
			{
				await AnalyzeAll();
			};
			// Analysis starts only through the shared workspace action.
		}

		private void BuildSidebar()
		{
			Panel panel = new Panel();
			panel.Dock = DockStyle.Left;
			panel.Width = 226;
			panel.BackColor = Navy;
			Panel panel2 = panel;
			Label label = new Label();
			label.Text = "SOS";
			label.ForeColor = Color.White;
			label.BackColor = Blue;
			label.Font = new Font("Segoe UI Semibold", 13f);
			label.TextAlign = ContentAlignment.MiddleCenter;
			label.Location = new Point(22, 24);
			label.Size = new Size(54, 40);
			Label label2 = label;
			Label label3 = new Label();
			label3.Text = "NAVEGADOR";
			label3.ForeColor = Color.White;
			label3.Font = new Font("Segoe UI Semibold", 14f);
			label3.AutoSize = true;
			label3.Location = new Point(86, 22);
			Label label4 = label3;
			Label label5 = new Label();
			label5.Text = "PROFESSIONAL  3.0";
			label5.ForeColor = Color.FromArgb(148, 163, 184);
			label5.Font = new Font("Segoe UI", 8f);
			label5.AutoSize = true;
			label5.Location = new Point(88, 48);
			Label label6 = label5;
			Panel panel3 = new Panel();
			panel3.BackColor = Color.FromArgb(51, 65, 85);
			panel3.Location = new Point(22, 82);
			panel3.Size = new Size(182, 1);
			Panel panel4 = panel3;
			Label label7 = new Label();
			label7.Text = "FLUXO AUTOMÁTICO";
			label7.ForeColor = Color.FromArgb(148, 163, 184);
			label7.Font = new Font("Segoe UI Semibold", 8f);
			label7.AutoSize = true;
			label7.Location = new Point(24, 106);
			Label label8 = label7;
			panel2.Controls.AddRange(new Control[5] { label2, label4, label6, panel4, label8 });
			AddStep(panel2, 1, "Análise do sistema", 140);
			AddStep(panel2, 2, "Teste dos navegadores", 188);
			AddStep(panel2, 3, "Correções recomendadas", 236);
			AddStep(panel2, 4, "Relatório final", 284);
			Label label9 = new Label();
			label9.Text = "SEUS DADOS";
			label9.ForeColor = Color.FromArgb(148, 163, 184);
			label9.Font = new Font("Segoe UI Semibold", 8f);
			label9.AutoSize = true;
			label9.Location = new Point(24, 362);
			Label label10 = label9;
			Label label11 = new Label();
			label11.Text = "Favoritos, senhas e histórico\nnão são apagados. Alterações\ncriam backup para reversão.";
			label11.ForeColor = Color.FromArgb(203, 213, 225);
			label11.AutoSize = true;
			label11.Location = new Point(24, 390);
			label11.Font = new Font("Segoe UI", 9f);
			Label label12 = label11;
			Label label13 = new Label();
			label13.Text = (IsAdmin() ? "●  Modo administrador" : "●  Modo limitado");
			label13.ForeColor = (IsAdmin() ? Color.FromArgb(74, 222, 128) : Color.FromArgb(251, 191, 36));
			label13.AutoSize = true;
			label13.Location = new Point(24, 690);
			label13.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			Label label14 = label13;
			panel2.Controls.AddRange(new Control[3] { label10, label12, label14 });
			base.Controls.Add(panel2);
		}

		private void AddStep(Panel p, int n, string text, int y)
		{
			Label label = new Label();
			label.Text = n.ToString();
			label.ForeColor = Color.White;
			label.BackColor = ((n == 1) ? Blue : Color.FromArgb(51, 65, 85));
			label.TextAlign = ContentAlignment.MiddleCenter;
			label.Location = new Point(24, y);
			label.Size = new Size(30, 30);
			label.Font = new Font("Segoe UI Semibold", 9f);
			Label value = label;
			Label label2 = new Label();
			label2.Text = text;
			label2.ForeColor = Color.FromArgb(226, 232, 240);
			label2.AutoSize = true;
			label2.Location = new Point(66, y + 6);
			Label value2 = label2;
			p.Controls.Add(value);
			p.Controls.Add(value2);
		}

		private void BuildMain()
		{
			Panel panel = new Panel();
			panel.Dock = DockStyle.Fill;
			panel.Padding = new Padding(26, 20, 26, 18);
			panel.BackColor = Bg;
			Panel panel2 = panel;
			base.Controls.Add(panel2);
			panel2.BringToFront();
			Panel panel3 = new Panel();
			panel3.Dock = DockStyle.Top;
			panel3.Height = 78;
			panel3.BackColor = Color.White;
			panel3.Padding = new Padding(22, 13, 18, 10);
			Panel panel4 = panel3;
			headline.Text = "Saúde dos navegadores";
			headline.Font = new Font("Segoe UI Semibold", 17f);
			headline.AutoSize = true;
			headline.Location = new Point(22, 11);
			phase.Text = "Inicie uma análise para verificar causas e escolher as correções.";
			phase.ForeColor = Muted;
			phase.AutoSize = true;
			phase.Location = new Point(25, 45);
			analyze.Text = "Iniciar análise";
			StyleButton(analyze, false);
			analyze.Size = new Size(146, 36);
			analyze.Margin = new Padding(0, 0, 10, 0);
			analyze.Click += async delegate
			{
				await AnalyzeAll();
			};
			repair.Text = "Corrigir selecionados";
			StyleButton(repair, true);
			repair.Size = new Size(166, 36);
			repair.Margin = new Padding(0);
			repair.Enabled = false;
			repair.Click += async delegate
			{
				await RepairSelected();
			};
			FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
			flowLayoutPanel.Dock = DockStyle.Right;
			flowLayoutPanel.Width = 322;
			flowLayoutPanel.FlowDirection = FlowDirection.LeftToRight;
			flowLayoutPanel.WrapContents = false;
			flowLayoutPanel.Padding = new Padding(0, 7, 0, 0);
			flowLayoutPanel.BackColor = Color.White;
			FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
			flowLayoutPanel2.Controls.Add(analyze);
			flowLayoutPanel2.Controls.Add(repair);
			panel4.Controls.AddRange(new Control[3] { headline, phase, flowLayoutPanel2 });
			TableLayoutPanel tableLayoutPanel = new TableLayoutPanel();
			tableLayoutPanel.Dock = DockStyle.Top;
			tableLayoutPanel.Height = 86;
			tableLayoutPanel.ColumnCount = 4;
			tableLayoutPanel.Padding = new Padding(0, 12, 0, 8);
			TableLayoutPanel tableLayoutPanel2 = tableLayoutPanel;
			for (int num = 0; num < 4; num++)
			{
				tableLayoutPanel2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
			}
			browserCount = AddCard(tableLayoutPanel2, "NAVEGADORES", "0", 0, Blue);
			criticalCount = AddCard(tableLayoutPanel2, "CRÍTICOS", "0", 1, Color.FromArgb(220, 38, 38));
			warningCount = AddCard(tableLayoutPanel2, "ATENÇÃO", "0", 2, Color.FromArgb(217, 119, 6));
			okCount = AddCard(tableLayoutPanel2, "APROVADOS", "0", 3, Color.FromArgb(22, 163, 74));
			SplitContainer body = new SplitContainer
			{
				Dock = DockStyle.Fill,
				Orientation = Orientation.Horizontal,
				SplitterDistance = 180,
				IsSplitterFixed = false,
				BackColor = Bg
			};
			body.Resize += delegate
			{
				if (body.Height > 430)
				{
					body.SplitterDistance = body.Height - 300;
				}
			};
			BuildGrid(body.Panel1);
			BuildActions(body.Panel2);
			Panel panel5 = new Panel();
			panel5.Dock = DockStyle.Bottom;
			panel5.Height = 72;
			panel5.BackColor = Color.White;
			panel5.Padding = new Padding(16, 5, 16, 4);
			Panel panel6 = panel5;
			TableLayoutPanel tableLayoutPanel3 = new TableLayoutPanel();
			tableLayoutPanel3.Dock = DockStyle.Fill;
			tableLayoutPanel3.ColumnCount = 1;
			tableLayoutPanel3.RowCount = 3;
			tableLayoutPanel3.Margin = new Padding(0);
			tableLayoutPanel3.Padding = new Padding(0);
			TableLayoutPanel tableLayoutPanel4 = tableLayoutPanel3;
			tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
			tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Absolute, 13f));
			tableLayoutPanel4.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			progressText.Text = "Pronto para iniciar";
			progressText.ForeColor = Color.FromArgb(51, 65, 85);
			progressText.Font = new Font("Segoe UI Semibold", 8.5f);
			progressText.Dock = DockStyle.Fill;
			progressText.AutoEllipsis = true;
			progress.Dock = DockStyle.Fill;
			progress.Style = ProgressBarStyle.Marquee;
			progress.MarqueeAnimationSpeed = 28;
			progress.Margin = new Padding(0);
			reportPath.Text = "Relatórios e backups ficam nos dados do Frankenstein TI.";
			reportPath.ForeColor = Muted;
			reportPath.AutoEllipsis = true;
			reportPath.Dock = DockStyle.Fill;
			reportPath.Padding = new Padding(0, 5, 0, 0);
			tableLayoutPanel4.Controls.Add(progressText, 0, 0);
			tableLayoutPanel4.Controls.Add(progress, 0, 1);
			tableLayoutPanel4.Controls.Add(reportPath, 0, 2);
			panel6.Controls.Add(tableLayoutPanel4);
			panel2.Controls.Add(body);
			panel2.Controls.Add(panel6);
			panel2.Controls.Add(tableLayoutPanel2);
			panel2.Controls.Add(panel4);
		}

		private Label AddCard(TableLayoutPanel t, string title, string value, int col, Color accent)
		{
			Panel panel = new Panel();
			panel.Dock = DockStyle.Fill;
			panel.BackColor = Color.White;
			panel.Margin = new Padding((col != 0) ? 5 : 0, 0, (col != 3) ? 5 : 0, 0);
			Panel panel2 = panel;
			Panel panel3 = new Panel();
			panel3.Dock = DockStyle.Left;
			panel3.Width = 4;
			panel3.BackColor = accent;
			Panel panel4 = panel3;
			Label label = new Label();
			label.Text = value;
			label.Font = new Font("Segoe UI Semibold", 18f);
			label.ForeColor = Color.FromArgb(30, 41, 59);
			label.AutoSize = true;
			label.Location = new Point(16, 8);
			Label label2 = label;
			Label label3 = new Label();
			label3.Text = title;
			label3.ForeColor = Muted;
			label3.Font = new Font("Segoe UI Semibold", 8f);
			label3.AutoSize = true;
			label3.Location = new Point(18, 43);
			Label label4 = label3;
			panel2.Controls.AddRange(new Control[3] { panel4, label2, label4 });
			t.Controls.Add(panel2, col, 0);
			return label2;
		}

		private void BuildGrid(Control host)
		{
			Panel panel = new Panel();
			panel.Dock = DockStyle.Fill;
			panel.BackColor = Color.White;
			panel.Padding = new Padding(14, 10, 14, 12);
			Panel panel2 = panel;
			Label label = new Label();
			label.Text = "Resultados da análise";
			label.Dock = DockStyle.Top;
			label.Height = 30;
			label.Font = new Font("Segoe UI Semibold", 11f);
			label.ForeColor = Color.FromArgb(30, 41, 59);
			Label value = label;
			grid.Dock = DockStyle.Fill;
			grid.BackgroundColor = Color.White;
			grid.BorderStyle = BorderStyle.None;
			grid.AllowUserToAddRows = false;
			grid.AllowUserToDeleteRows = false;
			grid.AllowUserToResizeRows = false;
			grid.ReadOnly = true;
			grid.RowHeadersVisible = false;
			grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
			grid.MultiSelect = false;
			grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
			grid.ColumnHeadersHeight = 34;
			grid.EnableHeadersVisualStyles = false;
			grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
			grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(71, 85, 105);
			grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 8f);
			grid.DefaultCellStyle.Padding = new Padding(5);
			grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254);
			grid.DefaultCellStyle.SelectionForeColor = Color.FromArgb(30, 41, 59);
			grid.GridColor = Color.FromArgb(226, 232, 240);
			grid.Columns.Add("Status", "STATUS");
			grid.Columns.Add("Check", "VERIFICAÇÃO");
			grid.Columns.Add("Result", "RESULTADO");
			grid.Columns.Add("Action", "RECOMENDAÇÃO");
			grid.Columns[0].Width = 84;
			grid.Columns[1].Width = 165;
			grid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
			grid.Columns[2].FillWeight = 53f;
			grid.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
			grid.Columns[3].FillWeight = 47f;
			panel2.Controls.Add(grid);
			panel2.Controls.Add(value);
			host.Controls.Add(panel2);
		}

		private void BuildActions(Control host)
		{
			Panel panel = new Panel();
			panel.Dock = DockStyle.Fill;
			panel.BackColor = Bg;
			panel.Padding = new Padding(0, 9, 0, 0);
			Panel panel2 = panel;
			Panel panel3 = new Panel();
			panel3.Dock = DockStyle.Left;
			panel3.Width = 465;
			panel3.BackColor = Color.White;
			panel3.Padding = new Padding(15, 10, 10, 7);
			Panel panel4 = panel3;
			Label label = new Label();
			label.Text = "Correções disponíveis";
			label.Dock = DockStyle.Top;
			label.Height = 28;
			label.Font = new Font("Segoe UI Semibold", 11f);
			Label value = label;
			FlowLayoutPanel flowLayoutPanel = new FlowLayoutPanel();
			flowLayoutPanel.Dock = DockStyle.Fill;
			flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
			flowLayoutPanel.WrapContents = false;
			flowLayoutPanel.AutoScroll = true;
			FlowLayoutPanel flowLayoutPanel2 = flowLayoutPanel;
			SetupCheck(fixNetwork, "Rede, proxy e cache DNS", "Redefine configurações que impedem conexões", true);
			SetupCheck(fixInterference, "Interferências de inicialização", "Remove redirecionamentos e compatibilidade indevida", true);
			SetupCheck(fixCaches, "Caches gráficos e de inicialização", "Move apenas caches para backup reversível", true);
			SetupCheck(fixGpu, "Aceleração gráfica dos navegadores", "Desativa GPU se o teste seguro indicar falha de driver", false);
			SetupCheck(fixExtensions, "Extensões dos navegadores", "Isola extensões em backup, sem apagar seus dados", false);
			SetupCheck(fixWinsock, "Catálogo Winsock", "Use se todos os navegadores falharem", false);
			SetupCheck(fixWindows, "Integridade do Windows (DISM + SFC)", "Análise profunda; pode demorar bastante", false);
			flowLayoutPanel2.Controls.AddRange(new Control[7] { fixNetwork, fixInterference, fixCaches, fixGpu, fixExtensions, fixWinsock, fixWindows });
			panel4.Controls.Add(flowLayoutPanel2);
			panel4.Controls.Add(value);
			Panel panel5 = new Panel();
			panel5.Dock = DockStyle.Fill;
			panel5.BackColor = Color.White;
			panel5.Padding = new Padding(15, 10, 10, 7);
			Panel panel6 = panel5;
			Label label2 = new Label();
			label2.Text = "Atividade";
			label2.Dock = DockStyle.Top;
			label2.Height = 28;
			label2.Font = new Font("Segoe UI Semibold", 11f);
			Label value2 = label2;
			activity.Dock = DockStyle.Fill;
			activity.BorderStyle = BorderStyle.None;
			activity.ReadOnly = true;
			activity.BackColor = Color.White;
			activity.ForeColor = Color.FromArgb(71, 85, 105);
			activity.Font = new Font("Consolas", 8.5f);
			panel6.Controls.Add(activity);
			panel6.Controls.Add(value2);
			panel2.Controls.Add(panel6);
			panel2.Controls.Add(panel4);
			host.Controls.Add(panel2);
		}

		private void SetupCheck(CheckBox c, string title, string desc, bool enabled)
		{
			c.Text = title + "\r\n    " + desc;
			c.AutoSize = false;
			c.Width = 390;
			c.Height = 34;
			c.ForeColor = Color.FromArgb(30, 41, 59);
			c.Checked = enabled;
			c.Padding = new Padding(2, 0, 0, 0);
		}

		private void StyleButton(Button b, bool primary)
		{
			b.FlatStyle = FlatStyle.Flat;
			b.FlatAppearance.BorderSize = ((!primary) ? 1 : 0);
			b.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
			b.BackColor = (primary ? Blue : Color.White);
			b.ForeColor = (primary ? Color.White : Color.FromArgb(51, 65, 85));
			b.Cursor = Cursors.Hand;
		}

		private async Task AnalyzeAll()
		{
			if (busy)
			{
				return;
			}
			if (!FrankensteinTI.Session.Begin("Diagnóstico de navegadores")) return;
			SetBusy(true, "Análise completa em andamento", "Verificando sistema, políticas, eventos e executáveis...");
			findings.Clear();
			criticals = (warnings = (oks = 0));
			UI(delegate
			{
				grid.Rows.Clear();
			});
			Log("Início da análise completa — " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
			try
			{
				await Task.Run((Action)RunDiagnostics);
				SetBusy(false, "Análise concluída", SummaryText());
				AutoSelectFixes();
				SaveReport("ANÁLISE");
			}
			catch (Exception ex)
			{
				AddFinding(Level.Critical, "Erro interno", ex.Message, "Execute novamente como administrador.");
				SetBusy(false, "Análise concluída com erro", ex.Message);
				SaveReport("ERRO");
			}
		}

		private void RunDiagnostics()
		{
			CheckSystem();
			CheckBrowsers();
			CheckNetwork();
			CheckEnvironmentAndPolicies();
			CheckInterference();
			CheckServices();
			CheckSecurityProducts();
			CheckSecurityBlocks();
			CheckRecentCrashes();
			TestAllBrowsers();
		}

		private void CheckSystem()
		{
			Log("Verificando ambiente do Windows...");
			AddFinding((!IsAdmin()) ? Level.Warning : Level.Ok, "Privilégios", IsAdmin() ? "Executando como administrador" : "Sem privilégios administrativos", IsAdmin() ? "Nenhuma ação necessária." : "Execute como administrador para aplicar todos os reparos.");
			try
			{
				DriveInfo driveInfo = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
				long num = driveInfo.AvailableFreeSpace / 1024 / 1024 / 1024;
				AddFinding((num < 3) ? Level.Critical : ((num < 10) ? Level.Warning : Level.Ok), "Espaço em disco", num + " GB livres na unidade do Windows", (num < 10) ? "Libere pelo menos 10 GB." : "Espaço suficiente.");
			}
			catch
			{
			}
			string path = Path.Combine(Path.GetTempPath(), "NavegadorSOS_write_test.tmp");
			try
			{
				File.WriteAllText(path, "ok");
				File.Delete(path);
				AddFinding(Level.Ok, "Pasta temporária", "Leitura e gravação funcionando", "Nenhuma ação necessária.");
			}
			catch (Exception ex)
			{
				AddFinding(Level.Critical, "Pasta temporária", ex.Message, "Corrija as permissões da pasta TEMP.");
			}
		}

		private void CheckBrowsers()
		{
			Log("Localizando navegadores instalados...");
			UI(delegate
			{
				browserCount.Text = browsers.Count.ToString();
			});
			if (browsers.Count == 0)
			{
				AddFinding(Level.Critical, "Instalações", "Nenhum navegador compatível localizado", "Reinstale Edge, Chrome ou Firefox.");
				return;
			}
			foreach (BrowserInfo browser in browsers)
			{
				string text = "versão desconhecida";
				try
				{
					text = FileVersionInfo.GetVersionInfo(browser.Exe).FileVersion;
				}
				catch
				{
				}
				AddFinding(Level.Info, browser.Name, text + " — " + browser.Exe, "Será testado com perfil temporário isolado.");
			}
		}

		private void CheckNetwork()
		{
			Log("Analisando proxy, DNS e variáveis de rede...");
			bool flag = false;
			using (RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings"))
			{
				if (registryKey != null)
				{
					int num = Convert.ToInt32(registryKey.GetValue("ProxyEnable", 0));
					string text = Convert.ToString(registryKey.GetValue("ProxyServer", ""));
					string text2 = Convert.ToString(registryKey.GetValue("AutoConfigURL", ""));
					flag = num != 0 || text2.Length > 0;
					AddFinding(flag ? Level.Warning : Level.Ok, "Proxy do usuário", (!flag) ? "Nenhum proxy manual ativo" : ((text.Length > 0) ? text : text2), flag ? "Redefinir se você não usa proxy corporativo." : "Nenhuma ação necessária.");
				}
			}
			string text3 = Exec("netsh", "winhttp show proxy", 15000);
			bool flag2 = text3.IndexOf("Direct access", StringComparison.OrdinalIgnoreCase) < 0 && text3.IndexOf("Acesso direto", StringComparison.OrdinalIgnoreCase) < 0;
			AddFinding(flag2 ? Level.Warning : Level.Ok, "Proxy WinHTTP", OneLine(text3), flag2 ? "Redefinir proxy do sistema." : "Nenhuma ação necessária.");
			string[] array = new string[3] { "HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY" };
			foreach (string text4 in array)
			{
				EnvironmentVariableTarget[] array2 = new EnvironmentVariableTarget[2]
				{
					EnvironmentVariableTarget.User,
					EnvironmentVariableTarget.Machine
				};
				foreach (EnvironmentVariableTarget environmentVariableTarget in array2)
				{
					string environmentVariable = Environment.GetEnvironmentVariable(text4, environmentVariableTarget);
					if (!string.IsNullOrWhiteSpace(environmentVariable))
					{
						AddFinding(Level.Warning, "Variável " + text4 + " (" + ((environmentVariableTarget == EnvironmentVariableTarget.User) ? "usuário" : "sistema") + ")", environmentVariable, "Revise esta variável de ambiente manualmente.");
					}
				}
			}
			string text5 = Exec("nslookup", "microsoft.com", 15000);
			bool flag3 = text5.IndexOf("Addresses", StringComparison.OrdinalIgnoreCase) >= 0 || text5.IndexOf("Endereços", StringComparison.OrdinalIgnoreCase) >= 0 || text5.IndexOf("Address:", StringComparison.OrdinalIgnoreCase) >= 0;
			AddFinding((!flag3) ? Level.Warning : Level.Ok, "Resolução DNS", flag3 ? "Consulta externa respondida" : "Falha ao resolver microsoft.com", flag3 ? "Nenhuma ação necessária." : "Limpar cache DNS e revisar a conexão.");
		}

		private void CheckEnvironmentAndPolicies()
		{
			Log("Verificando variáveis e políticas que podem impedir a inicialização...");
			bool flag = false;
			string[] array = new string[5] { "__COMPAT_LAYER", "CHROME_USER_DATA_DIR", "CHROME_HEADLESS", "MOZ_HEADLESS", "MOZ_DISABLE_CONTENT_SANDBOX" };
			foreach (string text in array)
			{
				EnvironmentVariableTarget[] array2 = new EnvironmentVariableTarget[2]
				{
					EnvironmentVariableTarget.User,
					EnvironmentVariableTarget.Machine
				};
				foreach (EnvironmentVariableTarget environmentVariableTarget in array2)
				{
					string environmentVariable = Environment.GetEnvironmentVariable(text, environmentVariableTarget);
					if (!string.IsNullOrWhiteSpace(environmentVariable))
					{
						flag = true;
						AddFinding(Level.Warning, "Variável " + text + " (" + ((environmentVariableTarget == EnvironmentVariableTarget.User) ? "usuário" : "sistema") + ")", environmentVariable, "Remova a variável se ela não foi configurada intencionalmente.");
					}
				}
			}
			if (!flag)
			{
				AddFinding(Level.Ok, "Variáveis de inicialização", "Nenhuma variável problemática encontrada", "Nenhuma ação necessária.");
			}
			bool flag2 = false;
			RegistryKey[] array3 = new RegistryKey[2]
			{
				Registry.CurrentUser,
				Registry.LocalMachine
			};
			foreach (RegistryKey registryKey in array3)
			{
				using (RegistryKey registryKey2 = registryKey.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer"))
				{
					if (registryKey2 == null)
					{
						continue;
					}
					int num = IntVal(registryKey2.GetValue("DisallowRun"));
					int num2 = IntVal(registryKey2.GetValue("RestrictRun"));
					using (RegistryKey registryKey3 = registryKey2.OpenSubKey("DisallowRun"))
					{
						if (num != 0 && registryKey3 != null)
						{
							string[] valueNames = registryKey3.GetValueNames();
							foreach (string name in valueNames)
							{
								string value = Convert.ToString(registryKey3.GetValue(name, ""));
								if (ExeNames().Any((string x) => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
								{
									flag2 = true;
									AddFinding(Level.Critical, "Política DisallowRun", value + " em " + registryKey.Name, "Remova somente se este computador não for gerenciado pela empresa.");
								}
							}
						}
					}
					if (num2 != 0)
					{
						List<string> allowed = new List<string>();
						using (RegistryKey registryKey4 = registryKey2.OpenSubKey("RestrictRun"))
						{
							if (registryKey4 != null)
							{
								string[] valueNames2 = registryKey4.GetValueNames();
								foreach (string name2 in valueNames2)
								{
									allowed.Add(Convert.ToString(registryKey4.GetValue(name2, "")));
								}
							}
						}
						if (!ExeNames().Any((string exe) => allowed.Any((string x) => string.Equals(x, exe, StringComparison.OrdinalIgnoreCase))))
						{
							flag2 = true;
							AddFinding(Level.Critical, "Política RestrictRun", registryKey.Name + " permite apenas: " + ((allowed.Count == 0) ? "nenhum programa" : string.Join(", ", allowed.Take(12))), "Solicite ao administrador que inclua o navegador na lista permitida.");
						}
						else
						{
							AddFinding(Level.Info, "Política RestrictRun", registryKey.Name + " possui lista de programas permitidos", "Ao menos um navegador está permitido; revise a lista se outro continuar bloqueado.");
						}
					}
					if (num != 0 && !flag2)
					{
						AddFinding(Level.Info, "Política DisallowRun", registryKey.Name + " possui restrições, mas nenhum navegador conhecido foi localizado", "Revise com o administrador se o computador for corporativo.");
					}
				}
			}
			if (!flag2)
			{
				AddFinding(Level.Ok, "Bloqueio DisallowRun", "Nenhum navegador listado como bloqueado", "Nenhuma ação necessária.");
			}
		}

		private void CheckInterference()
		{
			Log("Verificando redirecionamentos, compatibilidade e injeção de DLL...");
			bool flag = false;
			bool flag2 = false;
			RegistryView[] array = RegistryViews();
			foreach (RegistryView registryView in array)
			{
				using (RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
				{
					string[] array2 = ExeNames();
					foreach (string text in array2)
					{
						using (RegistryKey registryKey2 = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\" + text))
						{
							if (registryKey2 != null)
							{
								object value = registryKey2.GetValue("Debugger");
								int num = IntVal(registryKey2.GetValue("GlobalFlag"));
								string text2 = Convert.ToString(registryKey2.GetValue("VerifierDlls", ""));
								if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
								{
									flag = true;
									AddFinding(Level.Critical, string.Concat("Redirecionamento IFEO (", registryView, ")"), text + " → " + value, "Remover o redirecionamento após criar backup restaurável.");
								}
								if ((num & 0x100) != 0 || !string.IsNullOrWhiteSpace(text2))
								{
									flag = true;
									AddFinding(Level.Critical, string.Concat("Application Verifier (", registryView, ")"), text + " GlobalFlag=" + num + " DLLs=" + text2, "Desativar a verificação forçada deste navegador.");
								}
							}
						}
					}
				}
			}
			string[] array3 = ExeNames();
			foreach (string text3 in array3)
			{
				using (RegistryKey registryKey3 = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\" + text3))
				{
					if (registryKey3 != null && registryKey3.GetValue("Debugger") != null)
					{
						flag = true;
						AddFinding(Level.Critical, "Redirecionamento IFEO do usuário", text3 + " → " + registryKey3.GetValue("Debugger"), "Remover o redirecionamento após criar backup restaurável.");
					}
				}
			}
			if (!flag)
			{
				AddFinding(Level.Ok, "Redirecionamento IFEO", "Nenhum bloqueio encontrado", "Nenhuma ação necessária.");
			}
			using (RegistryKey registryKey4 = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers"))
			{
				if (registryKey4 != null)
				{
					string[] valueNames = registryKey4.GetValueNames();
					foreach (string text4 in valueNames)
					{
						if (IsBrowserPath(text4))
						{
							flag2 = true;
							AddFinding(Level.Warning, "Compatibilidade forçada (usuário)", Path.GetFileName(text4) + " = " + registryKey4.GetValue(text4), "Remover modo de compatibilidade.");
						}
					}
				}
			}
			RegistryView[] array4 = RegistryViews();
			foreach (RegistryView registryView2 in array4)
			{
				using (RegistryKey registryKey5 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView2))
				{
					using (RegistryKey registryKey6 = registryKey5.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers"))
					{
						if (registryKey6 == null)
						{
							continue;
						}
						string[] valueNames2 = registryKey6.GetValueNames();
						foreach (string text5 in valueNames2)
						{
							if (IsBrowserPath(text5))
							{
								flag2 = true;
								AddFinding(Level.Warning, string.Concat("Compatibilidade forçada (", registryView2, ")"), Path.GetFileName(text5) + " = " + registryKey6.GetValue(text5), "Remover modo de compatibilidade.");
							}
						}
					}
				}
			}
			if (!flag2)
			{
				AddFinding(Level.Ok, "Compatibilidade", "Nenhum modo forçado nos navegadores", "Nenhuma ação necessária.");
			}
			bool flag3 = false;
			RegistryView[] array5 = RegistryViews();
			foreach (RegistryView registryView3 in array5)
			{
				using (RegistryKey registryKey7 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView3))
				{
					using (RegistryKey registryKey8 = registryKey7.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Windows"))
					{
						if (registryKey8 != null)
						{
							string text6 = Convert.ToString(registryKey8.GetValue("AppInit_DLLs", ""));
							if (IntVal(registryKey8.GetValue("LoadAppInit_DLLs")) != 0 && !string.IsNullOrWhiteSpace(text6))
							{
								flag3 = true;
								AddFinding(Level.Warning, string.Concat("Injeção AppInit (", registryView3, ")"), text6, "Revise o antivírus ou programa responsável; não será removido automaticamente.");
							}
						}
					}
					using (RegistryKey registryKey9 = registryKey7.OpenSubKey("SYSTEM\\CurrentControlSet\\Control\\Session Manager\\AppCertDlls"))
					{
						if (registryKey9 == null)
						{
							continue;
						}
						string[] valueNames3 = registryKey9.GetValueNames();
						foreach (string text7 in valueNames3)
						{
							string text8 = Convert.ToString(registryKey9.GetValue(text7, ""));
							if (!string.IsNullOrWhiteSpace(text8))
							{
								flag3 = true;
								AddFinding(Level.Warning, string.Concat("Injeção AppCertDlls (", registryView3, ")"), text7 + " = " + text8, "Revise a assinatura e o fornecedor da DLL; não será removida automaticamente.");
							}
						}
					}
				}
			}
			if (!flag3)
			{
				AddFinding(Level.Ok, "Injeção global de DLL", "Nenhuma AppInit/AppCert DLL ativa", "Nenhuma ação necessária.");
			}
			ScanPolicy("Software\\Policies\\Google\\Chrome", "Políticas do Chrome");
			ScanPolicy("Software\\Policies\\Microsoft\\Edge", "Políticas do Edge");
			ScanPolicy("Software\\Policies\\Mozilla\\Firefox", "Políticas do Firefox");
		}

		private void ScanPolicy(string path, string name)
		{
			int num = 0;
			RegistryKey[] array = new RegistryKey[2]
			{
				Registry.LocalMachine,
				Registry.CurrentUser
			};
			foreach (RegistryKey registryKey in array)
			{
				using (RegistryKey registryKey2 = registryKey.OpenSubKey(path))
				{
					if (registryKey2 != null)
					{
						num += registryKey2.ValueCount + registryKey2.SubKeyCount;
					}
				}
			}
			if (num > 0)
			{
				AddFinding(Level.Info, name, num + " configuração(ões) administrada(s)", "Revise se este não for um computador corporativo.");
			}
		}

		private void CheckServices()
		{
			Log("Verificando serviços essenciais...");
			string[] array = new string[3] { "CryptSvc", "Dnscache", "BFE" };
			foreach (string text in array)
			{
				string text2 = Exec("sc.exe", "query " + text, 10000);
				bool flag = text2.IndexOf("RUNNING", StringComparison.OrdinalIgnoreCase) >= 0;
				AddFinding((!flag) ? Level.Warning : Level.Ok, "Serviço " + text, flag ? "Em execução" : "Parado ou indisponível", flag ? "Nenhuma ação necessária." : "Revise o serviço no Windows.");
			}
		}

		private void CheckSecurityProducts()
		{
			Log("Identificando produtos de segurança instalados...");
			string text = "Get-CimInstance -Namespace root/SecurityCenter2 -ClassName AntivirusProduct -ErrorAction SilentlyContinue | % {$_.displayName} | Sort-Object -Unique";
			string text2 = Exec("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + text.Replace("\"", "\\\"") + "\"", 20000);
			string[] array = text2.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			AddFinding(Level.Info, "Proteção antimalware", (array.Length == 0) ? "Nenhum produto informado pelo Security Center" : string.Join(", ", array), "Se os testes forem encerrados, revise quarentena e bloqueios do produto listado.");
		}

		private void CheckSecurityBlocks()
		{
			Log("Consultando bloqueios do AppLocker, App Control e Defender...");
			string text = "$p='msedge|chrome|firefox|brave|opera';$logs=@(@{N='Microsoft-Windows-AppLocker/EXE and DLL';I=8004;T='AppLocker'},@{N='Microsoft-Windows-CodeIntegrity/Operational';I=3077;T='App Control'},@{N='Microsoft-Windows-CodeIntegrity/Operational';I=3033;T='Integridade de código'},@{N='Microsoft-Windows-Windows Defender/Operational';I=1121;T='Defender ASR'});foreach($l in $logs){Get-WinEvent -FilterHashtable @{LogName=$l.N;Id=$l.I;StartTime=(Get-Date).AddDays(-14)} -ErrorAction SilentlyContinue | ? {$_.Message -match $p} | select -First 4 | % {($l.T+' | '+$_.Id+' | '+$_.TimeCreated.ToString('s')+' | '+($_.Message -replace '[\\r\\n]+',' '))}}";
			string text2 = Exec("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + text.Replace("\"", "\\\"") + "\"", 35000);
			string[] array = text2.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length == 0)
			{
				AddFinding(Level.Ok, "Bloqueios de segurança", "Nenhum bloqueio de navegador nos últimos 14 dias", "Nenhuma ação necessária.");
				return;
			}
			foreach (string item in array.Take(8))
			{
				AddFinding(Level.Critical, "Bloqueio de segurança", (item.Length > 280) ? (item.Substring(0, 280) + "...") : item, "Não contorne políticas corporativas; revise a regra com o administrador ou antivírus responsável.");
			}
		}

		private void CheckRecentCrashes()
		{
			Log("Consultando falhas recentes do Windows...");
			string text = "$p='msedge|chrome|firefox|brave|opera'; Get-WinEvent -FilterHashtable @{LogName='Application';Id=1000;StartTime=(Get-Date).AddDays(-7)} -ErrorAction SilentlyContinue | ? {$_.Message -match $p} | select -First 6 | % {($_.TimeCreated.ToString('s')+' | '+($_.Message -replace '[\\r\\n]+',' '))};";
			string text2 = Exec("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + text.Replace("\"", "\\\"") + "\"", 30000);
			string[] array = text2.Split(new char[2] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
			if (array.Length == 0)
			{
				AddFinding(Level.Ok, "Falhas recentes", "Nenhum evento 1000 de navegador nos últimos 7 dias", "Nenhuma ação necessária.");
				return;
			}
			foreach (string item in array.Take(4))
			{
				AddFinding(Level.Warning, "Falha registrada", (item.Length > 260) ? (item.Substring(0, 260) + "...") : item, "Correlacione o módulo com antivírus, driver ou instalação.");
			}
		}

		private void TestAllBrowsers()
		{
			Log("Testando executáveis em modo invisível, perfil limpo e modo gráfico seguro...");
			foreach (BrowserInfo browser in browsers)
			{
				string detail;
				string detail2;
				if (RunBrowserProbe(browser, false, out detail))
				{
					AddFinding(Level.Ok, "Teste isolado — " + browser.Name, "Executável, sandbox e perfil limpo funcionaram", "Se a abertura normal falha, isole caches ou extensões do perfil.");
				}
				else if (RunBrowserProbe(browser, true, out detail2))
				{
					AddFinding(Level.Warning, "Aceleração gráfica — " + browser.Name, "O teste normal falhou, mas o modo gráfico seguro funcionou", "Desative a aceleração gráfica deste navegador.");
					AddFinding(Level.Ok, "Teste seguro — " + browser.Name, "Executável funcionou sem GPU/extensões", "A instalação está utilizável; corrija GPU, cache ou extensões.");
				}
				else
				{
					AddFinding(Level.Critical, "Teste isolado — " + browser.Name, "Normal: " + detail + " | Seguro: " + detail2, "Verifique bloqueios de segurança; depois execute DISM/SFC ou reinstale o navegador.");
				}
			}
		}

		private bool RunBrowserProbe(BrowserInfo b, bool safe, out string detail)
		{
			string text = Path.Combine(Path.GetTempPath(), "NavegadorSOS", b.Kind + "_" + (safe ? "safe_" : "normal_") + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(text);
			try
			{
				ExecData execData;
				if (b.Kind == "firefox")
				{
					string text2 = Path.Combine(text, "test.png");
					string text3 = (safe ? " -safe-mode" : "");
					execData = ExecResult(b.Exe, "-headless -no-remote" + text3 + " -profile \"" + text + "\" -screenshot \"" + text2 + "\" about:blank", 25000);
					bool flag = execData.ExitCode == 0 || File.Exists(text2);
					detail = (flag ? "OK" : ("código " + execData.ExitCode + ": " + OneLine(execData.Output)));
					return flag;
				}
				string text4 = (safe ? " --disable-gpu --disable-extensions" : "");
				execData = ExecResult(b.Exe, "--headless=new" + text4 + " --no-first-run --no-default-browser-check --user-data-dir=\"" + text + "\" --dump-dom about:blank", 25000);
				bool flag2 = execData.ExitCode == 0;
				detail = (flag2 ? "OK" : ("código " + execData.ExitCode + ": " + OneLine(execData.Output)));
				return flag2;
			}
			catch (Exception ex)
			{
				detail = ex.Message;
				return false;
			}
			finally
			{
				try
				{
					Directory.Delete(text, true);
				}
				catch
				{
				}
			}
		}

		private async Task RepairSelected()
		{
			if (busy)
			{
				return;
			}
			if (!IsAdmin())
			{
				AddFinding(Level.Warning, "Reparo", "O programa não está elevado", "Clique com o botão direito no EXE e escolha Executar como administrador.");
				return;
			}
			if (MessageBox.Show(this, "Aplicar as correções marcadas? Salve o trabalho e feche os navegadores. As opções de rede e Windows podem exigir reinicialização.", "Revisar reparos", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
			if (!FrankensteinTI.Session.Begin("Reparo de navegadores")) return;
			SetBusy(true, "Aplicando correções selecionadas", "As alterações são registradas em backup antes da execução.");
			repairPercent = 0;
			UI(delegate
			{
				progress.Style = ProgressBarStyle.Continuous;
				progress.MarqueeAnimationSpeed = 0;
				progress.Minimum = 0;
				progress.Maximum = 100;
				progress.Value = 0;
				progressText.Text = "0% — Preparando reparos...";
			});
			try
			{
				await Task.Run((Action)RunRepairs);
				SetBusy(false, "Correções concluídas", "Reinicie o Windows e execute uma nova análise.");
				SaveReport("REPARO");
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Log("ERRO: " + ex2.Message);
				SetBusy(false, "Reparo concluído com erro", ex2.Message);
				UI(delegate
				{
					progress.Value = repairPercent;
					progressText.Text = repairPercent + "% — Processo interrompido: " + ex2.Message;
				});
				SaveReport("ERRO NO REPARO");
			}
		}

		private void RunRepairs()
		{
			bool flag = Checked(fixNetwork);
			bool flag2 = Checked(fixInterference);
			bool flag3 = Checked(fixCaches);
			bool flag4 = Checked(fixGpu);
			bool flag5 = Checked(fixExtensions);
			bool flag6 = Checked(fixWinsock);
			bool flag7 = Checked(fixWindows);
			int total = 2 + (flag ? 1 : 0) + (flag2 ? 1 : 0) + (flag3 ? 1 : 0) + (flag4 ? 1 : 0) + (flag5 ? 1 : 0) + (flag6 ? 1 : 0) + (flag7 ? 2 : 0);
			int num = 0;
			UpdateRepairProgress(num, total, "Preparando pasta de backup");
			string text = Path.Combine(reportDir, "Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
			Directory.CreateDirectory(text);
			List<string> list = new List<string>();
			ExportRegistryBackups(text);
			UpdateRepairProgress(++num, total, "Backup preparado");
			if (flag)
			{
				UpdateRepairProgress(num, total, "Redefinindo proxy e limpando DNS");
				Log("Redefinindo proxy e cache de rede...");
				using (RegistryKey registryKey = Registry.CurrentUser.CreateSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings"))
				{
					list.Add("ProxyEnable=" + Val(registryKey, "ProxyEnable") + "; ProxyServer=" + Val(registryKey, "ProxyServer") + "; AutoConfigURL=" + Val(registryKey, "AutoConfigURL"));
					registryKey.SetValue("ProxyEnable", 0, RegistryValueKind.DWord);
					registryKey.DeleteValue("ProxyServer", false);
					registryKey.DeleteValue("ProxyOverride", false);
					registryKey.DeleteValue("AutoConfigURL", false);
				}
				Log(Exec("netsh", "winhttp reset proxy", 20000));
				Log(Exec("ipconfig", "/flushdns", 20000));
				UpdateRepairProgress(++num, total, "Rede, proxy e DNS concluídos");
			}
			if (flag2)
			{
				UpdateRepairProgress(num, total, "Removendo interferências de inicialização");
				Log("Removendo interferências específicas dos navegadores...");
				RemoveIfeo(list);
				RemoveCompat(list);
				RemoveRunRestrictions(list);
				UpdateRepairProgress(++num, total, "Interferências verificadas");
			}
			if (flag3)
			{
				UpdateRepairProgress(num, total, "Criando backup dos caches");
				Log("Movendo caches para backup reversível...");
				BackupCaches(text, list);
				UpdateRepairProgress(++num, total, "Caches recriados com segurança");
			}
			if (flag4)
			{
				UpdateRepairProgress(num, total, "Desativando aceleração gráfica");
				DisableHardwareAcceleration(text, list);
				UpdateRepairProgress(++num, total, "Aceleração gráfica desativada");
			}
			if (flag5)
			{
				UpdateRepairProgress(num, total, "Isolando extensões em backup");
				IsolateExtensions(text, list);
				UpdateRepairProgress(++num, total, "Extensões isoladas com segurança");
			}
			if (flag6)
			{
				UpdateRepairProgress(num, total, "Redefinindo catálogo Winsock");
				Log(Exec("netsh", "winsock reset", 30000));
				Log("Winsock redefinido; reinicialização necessária.");
				UpdateRepairProgress(++num, total, "Winsock redefinido");
			}
			if (flag7)
			{
				UpdateRepairProgress(num, total, "Executando DISM — esta etapa pode demorar");
				Log("Executando DISM; esta etapa pode demorar...");
				Log(Exec("dism.exe", "/Online /Cleanup-Image /RestoreHealth", 2700000));
				UpdateRepairProgress(++num, total, "DISM concluído; iniciando SFC");
				Log("Executando SFC...");
				Log(Exec("sfc.exe", "/scannow", 2700000));
				UpdateRepairProgress(++num, total, "Verificação SFC concluída");
			}
			UpdateRepairProgress(num, total, "Gravando registro das alterações");
			File.WriteAllLines(Path.Combine(text, "alteracoes.txt"), list, Encoding.UTF8);
			Log("Backup: " + text);
			UpdateRepairProgress(++num, total, "Reparos concluídos");
		}

		private void UpdateRepairProgress(int current, int total, string text)
		{
			int percent = ((total > 0) ? Math.Max(0, Math.Min(100, (int)Math.Round((double)current * 100.0 / (double)total))) : 0);
			repairPercent = percent;
			UI(delegate
			{
				progress.Style = ProgressBarStyle.Continuous;
				progress.Value = percent;
				progressText.Text = percent + "% — " + text;
			});
		}

		private void RemoveIfeo(List<string> backup)
		{
			RegistryView[] array = RegistryViews();
			foreach (RegistryView registryView in array)
			{
				using (RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
				{
					string[] array2 = ExeNames();
					foreach (string text in array2)
					{
						using (RegistryKey k = registryKey.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\" + text, true))
						{
							CleanIfeoKey(k, string.Concat("HKLM ", registryView, " ", text), backup);
						}
					}
				}
			}
			string[] array3 = ExeNames();
			foreach (string text2 in array3)
			{
				using (RegistryKey k2 = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\" + text2, true))
				{
					CleanIfeoKey(k2, "HKCU " + text2, backup);
				}
			}
		}

		private void CleanIfeoKey(RegistryKey k, string label, List<string> backup)
		{
			if (k == null)
			{
				return;
			}
			object value = k.GetValue("Debugger");
			if (value != null)
			{
				backup.Add(label + " Debugger=" + value);
				k.DeleteValue("Debugger", false);
				Log("Redirecionamento removido: " + label);
			}
			object value2 = k.GetValue("VerifierDlls");
			if (value2 != null)
			{
				backup.Add(label + " VerifierDlls=" + value2);
				k.DeleteValue("VerifierDlls", false);
			}
			object value3 = k.GetValue("VerifierFlags");
			if (value3 != null)
			{
				backup.Add(label + " VerifierFlags=" + value3);
				k.DeleteValue("VerifierFlags", false);
			}
			int num = IntVal(k.GetValue("GlobalFlag"));
			if ((num & 0x100) != 0)
			{
				backup.Add(label + " GlobalFlag=" + num);
				int num2 = num & -257;
				if (num2 == 0)
				{
					k.DeleteValue("GlobalFlag", false);
				}
				else
				{
					k.SetValue("GlobalFlag", num2, RegistryValueKind.DWord);
				}
				Log("Application Verifier desativado: " + label);
			}
		}

		private void RemoveCompat(List<string> backup)
		{
			using (RegistryKey k = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers", true))
			{
				CleanCompatKey(k, "HKCU", backup);
			}
			RegistryView[] array = RegistryViews();
			foreach (RegistryView registryView in array)
			{
				using (RegistryKey registryKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
				{
					using (RegistryKey k2 = registryKey.OpenSubKey("Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers", true))
					{
						CleanCompatKey(k2, "HKLM " + registryView, backup);
					}
				}
			}
		}

		private void CleanCompatKey(RegistryKey k, string label, List<string> backup)
		{
			if (k == null)
			{
				return;
			}
			string[] valueNames = k.GetValueNames();
			foreach (string text in valueNames)
			{
				if (IsBrowserPath(text))
				{
					backup.Add(label + " AppCompat " + text + "=" + k.GetValue(text));
					k.DeleteValue(text, false);
					Log("Compatibilidade removida: " + Path.GetFileName(text));
				}
			}
		}

		private void RemoveRunRestrictions(List<string> backup)
		{
			RegistryKey[] array = new RegistryKey[2]
			{
				Registry.CurrentUser,
				Registry.LocalMachine
			};
			foreach (RegistryKey registryKey in array)
			{
				using (RegistryKey registryKey2 = registryKey.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer", true))
				{
					if (registryKey2 == null)
					{
						continue;
					}
					string[] array2 = new string[1] { "DisallowRun" };
					foreach (string text in array2)
					{
						using (RegistryKey registryKey3 = registryKey2.OpenSubKey(text, true))
						{
							if (registryKey3 == null)
							{
								continue;
							}
							string[] valueNames = registryKey3.GetValueNames();
							foreach (string text2 in valueNames)
							{
								string value = Convert.ToString(registryKey3.GetValue(text2, ""));
								if (ExeNames().Any((string x) => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
								{
									backup.Add(registryKey.Name + " " + text + " " + text2 + "=" + value);
									registryKey3.DeleteValue(text2, false);
									Log("Restrição removida: " + text + " → " + value);
								}
							}
						}
					}
				}
			}
		}

		private void ExportRegistryBackups(string backupDir)
		{
			string text = Path.Combine(backupDir, "Registro");
			Directory.CreateDirectory(text);
			int num = 0;
			List<string> list = new List<string>();
			list.Add("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings");
			list.Add("HKCU\\Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");
			list.Add("HKLM\\Software\\Microsoft\\Windows NT\\CurrentVersion\\AppCompatFlags\\Layers");
			list.Add("HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer");
			list.Add("HKLM\\Software\\Microsoft\\Windows\\CurrentVersion\\Policies\\Explorer");
			List<string> list2 = list;
			string[] array = ExeNames();
			foreach (string text2 in array)
			{
				list2.Add("HKLM\\Software\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\" + text2);
				list2.Add("HKCU\\Software\\Microsoft\\Windows NT\\CurrentVersion\\Image File Execution Options\\" + text2);
			}
			foreach (string item in list2)
			{
				string text3 = Regex.Replace(item, "[^A-Za-z0-9]+", "_").Trim('_');
				if (item.StartsWith("HKLM", StringComparison.OrdinalIgnoreCase) && Environment.Is64BitOperatingSystem)
				{
					string[] array2 = new string[2] { "64", "32" };
					foreach (string text4 in array2)
					{
						string[] array3 = new string[6];
						int num2 = ++num;
						array3[0] = num2.ToString("00");
						array3[1] = "_";
						array3[2] = text3;
						array3[3] = "_reg";
						array3[4] = text4;
						array3[5] = ".reg";
						string path = string.Concat(array3);
						try
						{
							ExecResult("reg.exe", "export \"" + item + "\" \"" + Path.Combine(text, path) + "\" /y /reg:" + text4, 15000);
						}
						catch
						{
						}
					}
				}
				else
				{
					int num3 = ++num;
					string path2 = num3.ToString("00") + "_" + text3 + ".reg";
					try
					{
						ExecResult("reg.exe", "export \"" + item + "\" \"" + Path.Combine(text, path2) + "\" /y", 15000);
					}
					catch
					{
					}
				}
			}
			File.WriteAllText(Path.Combine(backupDir, "COMO_RESTAURAR.txt"), "BACKUP DO NAVEGADOR SOS\r\n\r\nArquivos .reg preservam as configurações do Registro anteriores ao reparo.\r\nPastas e arquivos movidos mantêm a estrutura original descrita em alteracoes.txt.\r\nAntes de restaurar, feche os navegadores. Importe somente o arquivo .reg correspondente à alteração que deseja desfazer.\r\n", Encoding.UTF8);
		}

		private void DisableHardwareAcceleration(string backupDir, List<string> backup)
		{
			foreach (BrowserInfo browser in browsers)
			{
				if (browser.Kind == "firefox")
				{
					if (!Directory.Exists(browser.UserData))
					{
						continue;
					}
					string[] directories = Directory.GetDirectories(browser.UserData);
					foreach (string text in directories)
					{
						string text2 = Path.Combine(text, "user.js");
						string text3 = Path.Combine(backupDir, "firefox_" + Path.GetFileName(text) + "_user.js.bak");
						try
						{
							string text4 = (File.Exists(text2) ? File.ReadAllText(text2) : "");
							if (File.Exists(text2))
							{
								File.Copy(text2, text3, true);
							}
							string contents = "\r\n// Navegador SOS: aceleração gráfica desativada\r\nuser_pref(\"layers.acceleration.disabled\", true);\r\nuser_pref(\"gfx.webrender.all\", false);\r\n";
							if (text4.IndexOf("// Navegador SOS: aceleração gráfica desativada", StringComparison.Ordinal) < 0)
							{
								File.AppendAllText(text2, contents, Encoding.UTF8);
							}
							backup.Add("Firefox GPU: " + text2 + " (backup " + text3 + ")");
							Log("GPU desativada no perfil Firefox: " + Path.GetFileName(text));
						}
						catch (Exception ex)
						{
							Log("Não foi possível ajustar GPU no Firefox: " + ex.Message);
						}
					}
					continue;
				}
				string text5 = Path.Combine(browser.UserData ?? "", "Local State");
				if (File.Exists(text5))
				{
					try
					{
						string text6 = Path.Combine(backupDir, browser.Kind + "_Local_State.bak");
						File.Copy(text5, text6, true);
						string input = File.ReadAllText(text5, Encoding.UTF8);
						Regex regex = new Regex("\\\"hardware_acceleration_mode_enabled\\\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
						string contents2 = (regex.IsMatch(input) ? regex.Replace(input, "\"hardware_acceleration_mode_enabled\":false", 1) : Regex.Replace(input, "^\\s*\\{", "{\"hardware_acceleration_mode_enabled\":false,", RegexOptions.Singleline));
						File.WriteAllText(text5, contents2, new UTF8Encoding(false));
						backup.Add("GPU: " + text5 + " (backup " + text6 + ")");
						Log("GPU desativada: " + browser.Name);
					}
					catch (Exception ex2)
					{
						Log("Não foi possível ajustar GPU em " + browser.Name + ": " + ex2.Message);
					}
				}
			}
		}

		private void IsolateExtensions(string backupDir, List<string> backup)
		{
			foreach (BrowserInfo browser in browsers)
			{
				if (string.IsNullOrWhiteSpace(browser.UserData) || !Directory.Exists(browser.UserData))
				{
					continue;
				}
				if (browser.Kind == "firefox")
				{
					string[] directories = Directory.GetDirectories(browser.UserData);
					foreach (string text in directories)
					{
						MoveToBackup(Path.Combine(text, "extensions"), Path.Combine(backupDir, "Extensions", browser.Kind + "_" + Path.GetFileName(text) + "_extensions"), backup);
						MoveFileToBackup(Path.Combine(text, "extensions.json"), Path.Combine(backupDir, "Extensions", browser.Kind + "_" + Path.GetFileName(text) + "_extensions.json"), backup);
					}
					continue;
				}
				foreach (string item in from p in Directory.GetDirectories(browser.UserData)
					where Path.GetFileName(p) == "Default" || Path.GetFileName(p).StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)
					select p)
				{
					MoveToBackup(Path.Combine(item, "Extensions"), Path.Combine(backupDir, "Extensions", browser.Kind + "_" + Path.GetFileName(item) + "_Extensions"), backup);
				}
			}
		}

		private void MoveToBackup(string source, string destination, List<string> backup)
		{
			if (!Directory.Exists(source))
			{
				return;
			}
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(destination));
				Directory.Move(source, destination);
				backup.Add("Movido " + source + " -> " + destination);
				Log("Isolado: " + source);
			}
			catch (Exception ex)
			{
				Log("Não foi possível isolar " + source + ": " + ex.Message);
			}
		}

		private void MoveFileToBackup(string source, string destination, List<string> backup)
		{
			if (!File.Exists(source))
			{
				return;
			}
			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(destination));
				File.Move(source, destination);
				backup.Add("Movido " + source + " -> " + destination);
				Log("Isolado: " + source);
			}
			catch (Exception ex)
			{
				Log("Não foi possível isolar " + source + ": " + ex.Message);
			}
		}

		private void BackupCaches(string backupDir, List<string> backup)
		{
			foreach (BrowserInfo browser in browsers)
			{
				if (browser.Kind == "firefox")
				{
					string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mozilla\\Firefox\\Profiles");
					if (!Directory.Exists(path))
					{
						continue;
					}
					string[] directories = Directory.GetDirectories(path);
					foreach (string text in directories)
					{
						string[] array = new string[2] { "startupCache", "shader-cache" };
						foreach (string text2 in array)
						{
							MoveToBackup(Path.Combine(text, text2), Path.Combine(backupDir, "Caches", "firefox_" + Path.GetFileName(text) + "_" + text2), backup);
						}
					}
				}
				else
				{
					if (string.IsNullOrWhiteSpace(browser.UserData) || !Directory.Exists(browser.UserData))
					{
						continue;
					}
					string[] array2 = new string[4] { "Crashpad", "ShaderCache", "GrShaderCache", "DawnCache" };
					foreach (string text3 in array2)
					{
						MoveToBackup(Path.Combine(browser.UserData, text3), Path.Combine(backupDir, "Caches", browser.Kind + "_root_" + text3), backup);
					}
					foreach (string item in from p in Directory.GetDirectories(browser.UserData)
						where Path.GetFileName(p) == "Default" || Path.GetFileName(p).StartsWith("Profile ", StringComparison.OrdinalIgnoreCase)
						select p)
					{
						string[] array3 = new string[3] { "GPUCache", "Code Cache", "DawnCache" };
						foreach (string text4 in array3)
						{
							MoveToBackup(Path.Combine(item, text4), Path.Combine(backupDir, "Caches", browser.Kind + "_" + Path.GetFileName(item) + "_" + text4.Replace(' ', '_')), backup);
						}
					}
				}
			}
		}

		private void AutoSelectFixes()
		{
			bool interference = findings.Any((Finding f) => (f.Check.Contains("IFEO") || f.Check.Contains("Compatibilidade") || f.Check.Contains("Application Verifier") || f.Check.Contains("DisallowRun")) && (f.Level == Level.Critical || f.Level == Level.Warning));
			bool network = findings.Any((Finding f) => (f.Check.Contains("Proxy") || f.Check.Contains("DNS")) && (f.Level == Level.Critical || f.Level == Level.Warning));
			bool testsFail = findings.Any((Finding f) => f.Check.StartsWith("Teste isolado") && f.Level == Level.Critical);
			bool gpu = findings.Any((Finding f) => f.Check.StartsWith("Aceleração gráfica") && f.Level == Level.Warning);
			bool securityBlock = findings.Any((Finding f) => f.Level == Level.Critical && (f.Check == "Bloqueio de segurança" || f.Check == "Política RestrictRun"));
			UI(delegate
			{
				fixNetwork.Checked = network;
				fixInterference.Checked = interference;
				fixCaches.Checked = !testsFail && browsers.Count > 0;
				fixGpu.Checked = gpu;
				fixExtensions.Checked = false;
				fixWinsock.Checked = false;
				fixWindows.Checked = testsFail && !securityBlock;
				repair.Enabled = true;
			});
		}

		private void AddFinding(Level level, string check, string result, string recommendation)
		{
			Finding f = new Finding(level, check, string.IsNullOrWhiteSpace(result) ? "Sem detalhes" : result, recommendation);
			lock (findings)
			{
				findings.Add(f);
			}
			if (level == Level.Critical)
			{
				criticals++;
			}
			else if (level == Level.Warning)
			{
				warnings++;
			}
			else if (level == Level.Ok)
			{
				oks++;
			}
			UI(delegate
			{
				int index = grid.Rows.Add(LevelText(level), f.Check, f.Result, f.Recommendation);
				DataGridViewRow dataGridViewRow = grid.Rows[index];
				dataGridViewRow.Cells[0].Style.Font = new Font("Segoe UI Semibold", 8f);
				dataGridViewRow.Cells[0].Style.ForeColor = LevelColor(level);
				criticalCount.Text = criticals.ToString();
				warningCount.Text = warnings.ToString();
				okCount.Text = oks.ToString();
			});
		}

		private void SetBusy(bool value, string title, string subtitle)
		{
			busy = value;
			if (!value) FrankensteinTI.Session.End(title, reportFile);
			UI(delegate
			{
				headline.Text = title;
				phase.Text = subtitle;
				analyze.Enabled = !value;
				repair.Enabled = !value && findings.Count > 0;
				progress.Style = ((!value) ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee);
				progress.MarqueeAnimationSpeed = (value ? 28 : 0);
				progress.Value = ((!value) ? 100 : 0);
				progressText.Text = (value ? "Análise em andamento..." : ("100% — " + title));
			});
		}

		private string SummaryText()
		{
			return criticals + " crítico(s), " + warnings + " aviso(s) e " + oks + " verificação(ões) aprovadas.";
		}

		private void Log(string text)
		{
			if (!string.IsNullOrWhiteSpace(text))
			{
				UI(delegate
				{
					activity.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + text.Trim() + Environment.NewLine);
					activity.SelectionStart = activity.TextLength;
					activity.ScrollToCaret();
				});
			}
		}

		private void UI(Action a)
		{
			if (!base.IsDisposed)
			{
				if (base.InvokeRequired)
				{
					Invoke(a);
				}
				else
				{
					a();
				}
			}
		}

		private bool Checked(CheckBox c)
		{
			if (base.InvokeRequired)
			{
				return (bool)Invoke(new Func<CheckBox, bool>(Checked), c);
			}
			return c.Checked;
		}

		private void SaveReport(string stage)
		{
			try
			{
				Directory.CreateDirectory(reportDir);
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("NAVEGADOR SOS PROFESSIONAL 3.0");
				stringBuilder.AppendLine("Etapa: " + stage);
				stringBuilder.AppendLine("Data: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss"));
				stringBuilder.AppendLine(string.Concat("Windows: ", Environment.OSVersion, " | Admin: ", IsAdmin()));
				stringBuilder.AppendLine(new string('=', 88));
				lock (findings)
				{
					foreach (Finding finding in findings)
					{
						stringBuilder.AppendLine("[" + LevelText(finding.Level) + "] " + finding.Check + "\r\nResultado: " + finding.Result + "\r\nRecomendação: " + finding.Recommendation + "\r\n");
					}
				}
				stringBuilder.AppendLine("ATIVIDADE");
				stringBuilder.AppendLine(ActivityText());
				File.WriteAllText(reportFile, stringBuilder.ToString(), Encoding.UTF8);
				UI(delegate
				{
					reportPath.Text = "Relatório salvo automaticamente: " + reportFile;
				});
			}
			catch (Exception ex)
			{
				Log("Não foi possível salvar o relatório: " + ex.Message);
			}
		}

		private string ActivityText()
		{
			if (base.InvokeRequired)
			{
				return (string)Invoke(new Func<string>(ActivityText));
			}
			return activity.Text;
		}

		private static string LevelText(Level l)
		{
			switch (l)
			{
			default:
				return "INFO";
			case Level.Ok:
				return "OK";
			case Level.Warning:
				return "ATENÇÃO";
			case Level.Critical:
				return "CRÍTICO";
			}
		}

		private static Color LevelColor(Level l)
		{
			switch (l)
			{
			default:
				return Color.FromArgb(37, 99, 235);
			case Level.Ok:
				return Color.FromArgb(22, 163, 74);
			case Level.Warning:
				return Color.FromArgb(217, 119, 6);
			case Level.Critical:
				return Color.FromArgb(220, 38, 38);
			}
		}

		private bool IsBrowserPath(string path)
		{
			if (!browsers.Any((BrowserInfo b) => string.Equals(b.Exe, path, StringComparison.OrdinalIgnoreCase)))
			{
				return ExeNames().Any((string x) => path.EndsWith("\\" + x, StringComparison.OrdinalIgnoreCase));
			}
			return true;
		}

		private static string[] ExeNames()
		{
			return new string[6] { "chrome.exe", "msedge.exe", "firefox.exe", "brave.exe", "opera.exe", "launcher.exe" };
		}

		private static RegistryView[] RegistryViews()
		{
			if (!Environment.Is64BitOperatingSystem)
			{
				return new RegistryView[1];
			}
			return new RegistryView[2]
			{
				RegistryView.Registry64,
				RegistryView.Registry32
			};
		}

		private static int IntVal(object value)
		{
			if (value == null)
			{
				return 0;
			}
			try
			{
				if (value is int)
				{
					return (int)value;
				}
				string text = value.ToString().Trim();
				if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
				{
					return Convert.ToInt32(text.Substring(2), 16);
				}
				return Convert.ToInt32(value);
			}
			catch
			{
				return 0;
			}
		}

		private static string Val(RegistryKey k, string n)
		{
			object value = k.GetValue(n);
			if (value != null)
			{
				return value.ToString();
			}
			return "(não definido)";
		}

		private static string OneLine(string s)
		{
			if (string.IsNullOrWhiteSpace(s))
			{
				return "Sem resposta";
			}
			string text = s.Replace("\r", " ").Replace("\n", " ");
			while (text.Contains("  "))
			{
				text = text.Replace("  ", " ");
			}
			if (text.Length <= 220)
			{
				return text;
			}
			return text.Substring(0, 220) + "...";
		}

		private static bool IsAdmin()
		{
			try
			{
				return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
			}
			catch
			{
				return false;
			}
		}

		private static ExecData ExecResult(string file, string args, int timeout)
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(file, args);
			processStartInfo.UseShellExecute = false;
			processStartInfo.CreateNoWindow = true;
			processStartInfo.RedirectStandardOutput = true;
			processStartInfo.RedirectStandardError = true;
			ProcessStartInfo processStartInfo2 = processStartInfo;
			try
			{
				UTF8Encoding standardErrorEncoding = (UTF8Encoding)(processStartInfo2.StandardOutputEncoding = new UTF8Encoding(false));
				processStartInfo2.StandardErrorEncoding = standardErrorEncoding;
			}
			catch
			{
			}
			Process process = new Process();
			process.StartInfo = processStartInfo2;
			process.EnableRaisingEvents = true;
			Process process2 = process;
			StringBuilder sb = new StringBuilder();
			process2.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
			{
				if (e.Data != null)
				{
					lock (sb)
					{
						sb.AppendLine(e.Data);
					}
				}
			};
			process2.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
			{
				if (e.Data != null)
				{
					lock (sb)
					{
						sb.AppendLine(e.Data);
					}
				}
			};
			process2.Start();
			process2.BeginOutputReadLine();
			process2.BeginErrorReadLine();
			if (!process2.WaitForExit(timeout))
			{
				try
				{
					ProcessStartInfo processStartInfo3 = new ProcessStartInfo("taskkill.exe", "/PID " + process2.Id + " /T /F");
					processStartInfo3.UseShellExecute = false;
					processStartInfo3.CreateNoWindow = true;
					Process.Start(processStartInfo3).WaitForExit(5000);
				}
				catch
				{
				}
				ExecData execData = new ExecData();
				execData.ExitCode = -2;
				execData.Output = "Tempo limite excedido";
				return execData;
			}
			process2.WaitForExit();
			ExecData execData2 = new ExecData();
			execData2.ExitCode = process2.ExitCode;
			execData2.Output = sb.ToString().Trim();
			return execData2;
		}

		private static string Exec(string f, string a, int t)
		{
			try
			{
				return ExecResult(f, a, t).Output;
			}
			catch (Exception ex)
			{
				return "Erro em " + f + ": " + ex.Message;
			}
		}

		private static List<BrowserInfo> FindBrowsers()
		{
			List<BrowserInfo> list = new List<BrowserInfo>();
			string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			string folderPath2 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
			string folderPath3 = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			string folderPath4 = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
			Add(list, "Microsoft Edge", Path.Combine(folderPath2, "Microsoft\\Edge\\Application\\msedge.exe"), "edge", Path.Combine(folderPath3, "Microsoft\\Edge\\User Data"));
			Add(list, "Microsoft Edge", Path.Combine(folderPath, "Microsoft\\Edge\\Application\\msedge.exe"), "edge", Path.Combine(folderPath3, "Microsoft\\Edge\\User Data"));
			Add(list, "Google Chrome", Path.Combine(folderPath, "Google\\Chrome\\Application\\chrome.exe"), "chrome", Path.Combine(folderPath3, "Google\\Chrome\\User Data"));
			Add(list, "Google Chrome", Path.Combine(folderPath2, "Google\\Chrome\\Application\\chrome.exe"), "chrome", Path.Combine(folderPath3, "Google\\Chrome\\User Data"));
			Add(list, "Google Chrome", Path.Combine(folderPath3, "Google\\Chrome\\Application\\chrome.exe"), "chrome", Path.Combine(folderPath3, "Google\\Chrome\\User Data"));
			Add(list, "Mozilla Firefox", Path.Combine(folderPath, "Mozilla Firefox\\firefox.exe"), "firefox", Path.Combine(folderPath4, "Mozilla\\Firefox\\Profiles"));
			Add(list, "Mozilla Firefox", Path.Combine(folderPath2, "Mozilla Firefox\\firefox.exe"), "firefox", Path.Combine(folderPath4, "Mozilla\\Firefox\\Profiles"));
			Add(list, "Brave", Path.Combine(folderPath, "BraveSoftware\\Brave-Browser\\Application\\brave.exe"), "brave", Path.Combine(folderPath3, "BraveSoftware\\Brave-Browser\\User Data"));
			Add(list, "Brave", Path.Combine(folderPath3, "BraveSoftware\\Brave-Browser\\Application\\brave.exe"), "brave", Path.Combine(folderPath3, "BraveSoftware\\Brave-Browser\\User Data"));
			Add(list, "Opera", Path.Combine(folderPath3, "Programs\\Opera\\opera.exe"), "opera", Path.Combine(folderPath4, "Opera Software", "Opera Stable"));
			Add(list, "Opera GX", Path.Combine(folderPath3, "Programs\\Opera GX\\opera.exe"), "operagx", Path.Combine(folderPath4, "Opera Software", "Opera GX Stable"));
			return (from g in list.GroupBy((BrowserInfo x) => x.Exe, StringComparer.OrdinalIgnoreCase)
				select g.First()).ToList();
		}

		private static void Add(List<BrowserInfo> l, string n, string p, string k, string u)
		{
			if (File.Exists(p))
			{
				l.Add(new BrowserInfo(n, p, k, u));
			}
		}
	}
	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new MainForm());
		}
	}
}
