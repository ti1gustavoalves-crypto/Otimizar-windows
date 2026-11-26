using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;




namespace CentralInstalacaoTI
{
	internal enum InstallKind
	{
		Winget,
		MsiSilent,
		ExeSilent,
		Interactive,
		Manual,
		Portable
	}
	internal sealed class AppItem
	{
		public string Id;

		public string Name;

		public string Category;

		public string WingetId;

		public string RelativePath;

		public string Arguments;

		public string Notes;

		public string AbsolutePath;

		public string Url;

		public InstallKind LocalKind;

		public bool Essential;

		public AppItem(string id, string name, string category, string wingetId, string relativePath, InstallKind localKind, string arguments, bool essential, string notes)
		{
			Id = id;
			Name = name;
			Category = category;
			WingetId = wingetId;
			RelativePath = relativePath;
			LocalKind = localKind;
			Arguments = arguments;
			Essential = essential;
			Notes = notes;
		}

		public AppItem FromDrive(string path)
		{
			AbsolutePath = path;
			return this;
		}

		public AppItem FromOfficialSite(string url)
		{
			Url = url;
			return this;
		}
	}
	internal static class Program
	{
		[STAThread]
		private static void Main()
		{
			bool createdNew;
			using (Mutex obj = new Mutex(true, "Local\\CentralDeInstalacaoTI_SingleInstance", out createdNew))
			{
				if (createdNew)
				{
					Application.EnableVisualStyles();
					Application.SetCompatibleTextRenderingDefault(false);
					Application.Run(new MainForm());
					GC.KeepAlive(obj);
				}
			}
		}
	}
	internal sealed partial class MainForm : Form
	{
		private static readonly string DefaultSoftwareRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Softwares");

		private readonly List<AppItem> _catalog;

		private readonly HashSet<string> _selected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		private readonly TextBox _folder = new TextBox();

		private readonly ComboBox _drive = new ComboBox();

		private readonly TextBox _search = new TextBox();

		private readonly ListBox _category = new ListBox();

		private readonly CheckBox _preferLocal = new CheckBox();

		private readonly FlowLayoutPanel _cards = new FlowLayoutPanel();

		private readonly TextBox _log = new TextBox();

		private readonly Button _install = new Button();

		private readonly Label _summary = new Label();

		private readonly Dictionary<string, Image> _icons = new Dictionary<string, Image>();

		private readonly Dictionary<string, CheckBox> _cardChecks = new Dictionary<string, CheckBox>();

		private readonly Dictionary<string, Control> _cardCache = new Dictionary<string, Control>();

		private readonly Dictionary<string, string> _sourceCache = new Dictionary<string, string>();

		private readonly Dictionary<string, string> _versionCache = new Dictionary<string, string>();

		private readonly System.Windows.Forms.Timer _searchTimer = new System.Windows.Forms.Timer
		{
			Interval = 180
		};

		private Bitmap _iconSprite;

		private Bitmap _programIcon;

		private string _logPath;

		private string _runSoftwareRoot;

		private bool _runPreferLocal;

		private Panel _bottom;

		private Panel _contentHost;

		private Panel _toolbarPanel;

		private Panel _settingsPanel;

		private bool _uiReady;

		private bool _softwareRootAvailable;

		private readonly HashSet<string> _reachableDriveRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		public MainForm()
		{
			Text = "Central de Instalação de Softwares - TI";
			base.Width = 1180;
			base.Height = 780;
			MinimumSize = new Size(980, 650);
			base.StartPosition = FormStartPosition.CenterScreen;
			Font = new Font("Segoe UI", 9f);
			BackColor = Color.FromArgb(15, 23, 42);
			ForeColor = Color.FromArgb(226, 232, 240);
			base.FormBorderStyle = FormBorderStyle.None;
			base.Padding = new Padding(1);
			_catalog = BuildCatalog().Where(a => a.Id != "sosbrowser").ToList();
			LoadIconSprite();
			LoadProgramIcon();
			_folder.Text = DefaultSoftwareRoot;
			BuildUi(); _settingsPanel.Visible=true; LayoutContent(); Shown+=delegate{LayoutContent();};
			LoadDrives();
			RebuildCaches();
			_uiReady = true;
			RefreshCards();
		}

		private void BuildUi()
		{
			Panel panel = new Panel();
			panel.Dock = DockStyle.Top;
			panel.Height = 44;
			panel.BackColor = Color.FromArgb(11, 18, 32);
			Panel panel2 = panel;
			panel2.Visible = false; base.Controls.Add(panel2);
			PictureBox pictureBox = new PictureBox();
			pictureBox.Left = 12;
			pictureBox.Top = 8;
			pictureBox.Width = 27;
			pictureBox.Height = 27;
			pictureBox.Image = _programIcon;
			pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
			PictureBox pictureBox2 = pictureBox;
			Label label = new Label();
			label.Text = "Central TI";
			label.Left = 47;
			label.Top = 10;
			label.Width = 230;
			label.Height = 27;
			label.ForeColor = Color.White;
			label.Font = new Font("Segoe UI Semibold", 13f);
			Label label2 = label;
			panel2.Controls.Add(pictureBox2);
			panel2.Controls.Add(label2);
			Button button = WindowButton("×", Color.FromArgb(185, 28, 28));
			button.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			button.Left = panel2.Width - 45;
			button.Click += delegate
			{
				Close();
			};
			panel2.Controls.Add(button);
			Button button2 = WindowButton("□", Color.FromArgb(30, 41, 59));
			button2.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			button2.Left = panel2.Width - 88;
			button2.Click += delegate
			{
				base.WindowState = ((base.WindowState != FormWindowState.Maximized) ? FormWindowState.Maximized : FormWindowState.Normal);
			};
			panel2.Controls.Add(button2);
			Button button3 = WindowButton("—", Color.FromArgb(30, 41, 59));
			button3.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			button3.Left = panel2.Width - 131;
			button3.Click += delegate
			{
				base.WindowState = FormWindowState.Minimized;
			};
			panel2.Controls.Add(button3);
			Button more = WindowButton("⋮", Color.FromArgb(30, 41, 59));
			more.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			more.Left = panel2.Width - 174;
			panel2.Controls.Add(more);
			Button button4 = WindowButton("⚙", Color.FromArgb(30, 41, 59));
			button4.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			button4.Left = panel2.Width - 217;
			panel2.Controls.Add(button4);
			panel2.MouseDown += DragWindow;
			label2.MouseDown += DragWindow;
			pictureBox2.MouseDown += DragWindow;
			Panel body = new Panel
			{
				BackColor = Color.FromArgb(15, 23, 42)
			};
			base.Controls.Add(body);
			body.BringToFront();
			panel2.BringToFront();
			Action layoutBody = delegate
			{
				body.SetBounds(base.Padding.Left, base.Padding.Top, Math.Max(0, base.ClientSize.Width - base.Padding.Horizontal), Math.Max(0, base.ClientSize.Height - base.Padding.Vertical));
			};
			base.Resize += delegate
			{
				layoutBody();
			};
			layoutBody();
			Panel sidebar = new Panel
			{
				Dock = DockStyle.Left,
				Width = 184,
				Padding = new Padding(10),
				BackColor = Color.FromArgb(11, 18, 32)
			};
			body.Controls.Add(sidebar);
			sidebar.Controls.Add(new Label
			{
				Text = "CATEGORIAS",
				Dock = DockStyle.Top,
				Height = 30,
				ForeColor = Color.FromArgb(96, 165, 250),
				Font = new Font("Segoe UI Semibold", 8f)
			});
			_category.Dock = DockStyle.Fill;
			_category.BorderStyle = BorderStyle.None;
			_category.BackColor = Color.FromArgb(11, 18, 32);
			_category.ForeColor = Color.FromArgb(203, 213, 225);
			_category.DrawMode = DrawMode.OwnerDrawFixed;
			_category.ItemHeight = 31;
			_category.DrawItem += DrawCategory;
			sidebar.Controls.Add(_category);
			_category.BringToFront();
			_category.Items.Add("Todos");
			foreach (string item in _catalog.Select((AppItem x) => x.Category).Distinct().OrderBy(CategoryRank)
				.ThenBy((string x) => x))
			{
				_category.Items.Add(item);
			}
			_category.SelectedIndexChanged += delegate
			{
				if (_uiReady)
				{
					RefreshCards();
				}
			};
			Panel content = new Panel
			{
				BackColor = Color.FromArgb(15, 23, 42)
			};
			body.Controls.Add(content);
			content.BringToFront();
			sidebar.BringToFront();
			Action layoutBodyContent = delegate
			{
				content.SetBounds(sidebar.Width, 0, Math.Max(0, body.ClientSize.Width - sidebar.Width), body.ClientSize.Height);
			};
			body.Resize += delegate
			{
				layoutBodyContent();
			};
			layoutBodyContent();
			Panel toolbar = new Panel
			{
				BackColor = Color.FromArgb(15, 23, 42)
			};
			_toolbarPanel = toolbar;
			_search.SetBounds(14, 15, 300, 28);
			_search.BackColor = Color.FromArgb(30, 41, 59);
			_search.ForeColor = Color.White;
			_search.BorderStyle = BorderStyle.FixedSingle;
			toolbar.Controls.Add(_search);
			SendMessageString(_search.Handle, 5377, IntPtr.Zero, "Pesquisar aplicativos...");
			_searchTimer.Tick += delegate
			{
				_searchTimer.Stop();
				RefreshCards();
			};
			_search.TextChanged += delegate
			{
				_searchTimer.Stop();
				_searchTimer.Start();
			};
			Label destinationLabel = new Label
			{
				Text = "Destino",
				Left = 330,
				Top = 21,
				Width = 50,
				ForeColor = Color.FromArgb(148, 163, 184)
			};
			toolbar.Controls.Add(destinationLabel);
			_drive.SetBounds(382, 15, 190, 28);
			_drive.DropDownStyle = ComboBoxStyle.DropDownList;
			_drive.FlatStyle = FlatStyle.Flat;
			toolbar.Controls.Add(_drive);
			_preferLocal.Text = "Usar arquivos locais";
			_preferLocal.Checked = true;
			_preferLocal.SetBounds(590, 17, 155, 26);
			_preferLocal.ForeColor = Color.FromArgb(203, 213, 225);
			_preferLocal.CheckedChanged += delegate
			{
				if (_uiReady)
				{
					RebuildCaches();
					RefreshCards();
				}
			};
			toolbar.Controls.Add(_preferLocal);
			_install.Text = "INSTALAR";
			_install.Width = 130;
			_install.Height = 32;
			_install.Top = 13;
			_install.Anchor = AnchorStyles.Top;
			_install.Left = toolbar.Width - 146;
			_install.BackColor = Color.FromArgb(37, 99, 235);
			_install.ForeColor = Color.White;
			_install.FlatStyle = FlatStyle.Flat;
			_install.Click += InstallSelected;
			toolbar.Controls.Add(_install);
			Button clearSelection = NewButton("LIMPAR", delegate
			{
				_selected.Clear();
				SyncCardChecks();
			});
			clearSelection.AutoSize = false;
			clearSelection.Width = 105;
			clearSelection.Height = 32;
			clearSelection.Top = 13;
			clearSelection.Anchor = AnchorStyles.Top;
			clearSelection.Left = toolbar.Width - 261;
			toolbar.Controls.Add(clearSelection);
			Action layoutToolbar = delegate
			{
				int num = toolbar.ClientSize.Width;
                if(num<850){_search.SetBounds(14,8,Math.Max(80,num-28),28); destinationLabel.SetBounds(14,54,50,24);_drive.SetBounds(68,50,145,28);_preferLocal.SetBounds(223,50,130,28);_install.SetBounds(Math.Max(0,num-146),48,130,32);clearSelection.SetBounds(Math.Max(0,num-261),48,105,32);return;}
                _install.Top=13;clearSelection.Top=13;destinationLabel.Top=21;
				_install.Left = Math.Max(0, num - 146);
				clearSelection.Left = Math.Max(0, _install.Left - 115);
				int num2 = Math.Max(0, clearSelection.Left - 10);
				_preferLocal.SetBounds(Math.Max(0, num2 - 150), 17, 150, 26);
				_drive.SetBounds(Math.Max(0, _preferLocal.Left - 185), 15, 175, 28);
				destinationLabel.Left = Math.Max(0, _drive.Left - 55);
				int num3 = Math.Max(90, destinationLabel.Left - 28);
				_search.SetBounds(14, 15, num3, 28);
			};
			toolbar.Resize += delegate
			{
				layoutToolbar();
			};
			layoutToolbar();
			Panel settings = new Panel
			{
				BackColor = Color.FromArgb(23, 34, 52),
				Padding = new Padding(12),
				Visible = false
			};
			_settingsPanel = settings;
			settings.Controls.Add(new Label
			{
				Text = "Pasta de instaladores locais",
				Left = 14,
				Top = 10,
				Width = 190,
				ForeColor = Color.FromArgb(148, 163, 184)
			});
			_folder.SetBounds(14, 30, 650, 27);
			_folder.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			_folder.BackColor = Color.FromArgb(30, 41, 59);
			_folder.ForeColor = Color.White;
			settings.Controls.Add(_folder);
			Button button5 = NewButton("Procurar", BrowseFolder);
			button5.SetBounds(675, 27, 90, 30);
			button5.Anchor = AnchorStyles.Top | AnchorStyles.Right;
			settings.Controls.Add(button5);
            more.Parent=settings;more.Text="Opções";more.Width=80;more.Height=30;
            settings.Resize+=delegate{_folder.SetBounds(14,30,Math.Max(80,settings.Width-210),27);button5.SetBounds(settings.Width-187,27,90,30);more.SetBounds(settings.Width-89,27,80,30);};
			button4.Click += delegate
			{
				settings.Visible = !settings.Visible;
				LayoutContent();
			};
			ContextMenuStrip menu = new ContextMenuStrip
			{
				BackColor = Color.FromArgb(30, 41, 59),
				ForeColor = Color.White,
				ShowImageMargin = false
			};
			menu.Items.Add("Selecionar essenciais", null, delegate
			{
				SelectBy((AppItem a) => a.Essential);
			});
			menu.Items.Add("Selecionar itens visíveis", null, delegate
			{
				SelectVisible();
			});
			menu.Items.Add("Limpar seleção", null, delegate
			{
				_selected.Clear();
				SyncCardChecks();
			});
			menu.Items.Add(new ToolStripSeparator());
			menu.Items.Add("Abrir relatórios", null, OpenReports);
			menu.Items.Add("Recarregar fontes locais", null, delegate
			{
				RebuildCaches();
				RefreshCards();
			});
			more.Click += delegate
			{
				menu.Show(more, new Point(0, more.Height));
			};
			Panel panel3 = new Panel();
			panel3.Padding = new Padding(10, 5, 10, 0);
			panel3.BackColor = Color.FromArgb(11, 18, 32);
			Panel panel4 = panel3;
			_summary.Dock = DockStyle.Fill;
			_summary.ForeColor = Color.FromArgb(148, 163, 184);
			panel4.Controls.Add(_summary);
			_bottom = new Panel
			{
				Padding = new Padding(8),
				BackColor = Color.FromArgb(2, 6, 23),
				Visible = false
			};
			_log.Multiline = true;
			_log.ReadOnly = true;
			_log.ScrollBars = ScrollBars.Vertical;
			_log.Dock = DockStyle.Fill;
			_log.BackColor = Color.FromArgb(2, 6, 23);
			_log.ForeColor = Color.FromArgb(148, 163, 184);
			_log.BorderStyle = BorderStyle.None;
			_bottom.Controls.Add(_log);
			_cards.Dock = DockStyle.None;
			_cards.AutoScroll = true;
			_cards.WrapContents = true;
			_cards.FlowDirection = FlowDirection.LeftToRight;
			_cards.Padding = new Padding(10);
			_cards.BackColor = Color.FromArgb(15, 23, 42);
			_contentHost = content;
			content.Controls.Add(toolbar);
			content.Controls.Add(settings);
			content.Controls.Add(_cards);
			content.Controls.Add(_bottom);
			content.Controls.Add(panel4);
			panel4.Name = "StatusBar";
			content.Resize += delegate
			{
				LayoutContent();
			};
			LayoutContent();
			_category.SelectedItem = "Todos";
		}

		private void LayoutContent()
		{
			if (_contentHost == null)
			{
				return;
			}
			int num = Math.Max(0, _contentHost.ClientSize.Width);
			int num2 = Math.Max(0, _contentHost.ClientSize.Height);
			int num3 = num<850?96:58;
			int num4 = ((_settingsPanel != null && _settingsPanel.Visible) ? 70 : 0);
			int num5 = 28;
			int num6 = ((_bottom != null && _bottom.Visible) ? 150 : 0);
			int num7 = num3 + num4;
			int num8 = Math.Max(0, num2 - num7 - num5 - num6);
			_toolbarPanel.SetBounds(0, 0, num, num3);
			_settingsPanel.SetBounds(0, num3, num, num4);
			_cards.SetBounds(0, num7, num, num8);
			_bottom.SetBounds(0, num7 + num8, num, num6);
			foreach (Control control in _contentHost.Controls)
			{
				if (control.Name == "StatusBar")
				{
					control.SetBounds(0, Math.Max(0, num2 - num5), num, num5);
				}
			}
		}

		private Button NewButton(string text, EventHandler click)
		{
			Button button = new Button();
			button.Text = text;
			button.AutoSize = true;
			button.Height = 30;
			button.Margin = new Padding(3);
			button.FlatStyle = FlatStyle.Flat;
			button.BackColor = Color.FromArgb(30, 41, 59);
			button.ForeColor = Color.FromArgb(226, 232, 240);
			Button button2 = button;
			button2.Click += click;
			return button2;
		}

		private Button WindowButton(string text, Color hover)
		{
			Button b = new Button
			{
				Text = text,
				Width = 42,
				Height = 32,
				Top = 0,
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(15, 23, 42),
				ForeColor = Color.White,
				Font = new Font("Segoe UI", 12f)
			};
			b.FlatAppearance.BorderSize = 0;
			b.MouseEnter += delegate
			{
				b.BackColor = hover;
			};
			b.MouseLeave += delegate
			{
				b.BackColor = Color.FromArgb(15, 23, 42);
			};
			return b;
		}

		[DllImport("user32.dll")]
		private static extern bool ReleaseCapture();

		[DllImport("user32.dll")]
		private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "SendMessageW")]
		private static extern IntPtr SendMessageString(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

		private void DragWindow(object sender, MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				ReleaseCapture();
				SendMessage(base.Handle, 161, 2, 0);
			}
		}

		private void DrawCategory(object sender, DrawItemEventArgs e)
		{
			if (e.Index < 0)
			{
				return;
			}
			bool flag = (e.State & DrawItemState.Selected) != 0;
			using (SolidBrush brush = new SolidBrush(flag ? Color.FromArgb(30, 64, 108) : Color.FromArgb(11, 18, 32)))
			{
				e.Graphics.FillRectangle(brush, e.Bounds);
			}
			using (SolidBrush brush2 = new SolidBrush(flag ? Color.White : Color.FromArgb(148, 163, 184)))
			{
				e.Graphics.DrawString(_category.Items[e.Index].ToString(), new Font("Segoe UI", 9f), brush2, e.Bounds.Left + 10, e.Bounds.Top + 7);
			}
		}

		private void LoadDrives()
		{
			foreach (DriveInfo item in from d in DriveInfo.GetDrives()
				where d.IsReady && d.DriveType == DriveType.Fixed
				select d)
			{
				_drive.Items.Add(string.Format("{0} ({1:N1} GB livres)", item.Name, (double)item.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0));
			}
			if (_drive.Items.Count > 0)
			{
				_drive.SelectedIndex = 0;
			}
		}

		private void BrowseFolder(object sender, EventArgs e)
		{
			FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
			folderBrowserDialog.Description = "Selecione a pasta com os instaladores";
			using (FolderBrowserDialog folderBrowserDialog2 = folderBrowserDialog)
			{
				if (Directory.Exists(_folder.Text))
				{
					folderBrowserDialog2.SelectedPath = _folder.Text;
				}
				if (folderBrowserDialog2.ShowDialog(this) == DialogResult.OK)
				{
					_folder.Text = folderBrowserDialog2.SelectedPath;
					RebuildCaches();
					RefreshCards();
				}
			}
		}

		private void SaveVisibleSelections()
		{
			foreach (KeyValuePair<string, CheckBox> cardCheck in _cardChecks)
			{
				if (cardCheck.Value.Checked)
				{
					_selected.Add(cardCheck.Key);
				}
				else
				{
					_selected.Remove(cardCheck.Key);
				}
			}
		}

		private void RefreshCards()
		{
			if (!_uiReady)
			{
				return;
			}
			SaveVisibleSelections();
			string filter = _search.Text.Trim();
			string category = ((_category.SelectedItem == null) ? "Todos" : _category.SelectedItem.ToString());
			IEnumerable<AppItem> source = _catalog.Where((AppItem a) => (category == "Todos" || a.Category == category) && (filter.Length == 0 || a.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || a.Category.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0));
			source = ((!(category == "Todos")) ? source.OrderByDescending((AppItem a) => a.Essential).ThenBy((AppItem a) => a.Name, StringComparer.CurrentCultureIgnoreCase) : source.OrderBy((AppItem a) => a.Name, StringComparer.CurrentCultureIgnoreCase));
			_cards.SuspendLayout();
			_cards.Controls.Clear();
			foreach (AppItem item in source)
			{
				_cards.Controls.Add(_cardCache[item.Id]);
			}
			_cards.ResumeLayout(); FrankensteinTI.IntegratedStyle.Apply(_cards);
			UpdateSummary();
		}

		private void RebuildCaches()
		{
			_cards.Controls.Clear();
			foreach (Control value in _cardCache.Values)
			{
				value.Dispose();
			}
			_sourceCache.Clear();
			_versionCache.Clear();
			_cardCache.Clear();
			_cardChecks.Clear();
			_softwareRootAvailable = Directory.Exists(_folder.Text);
			_reachableDriveRoots.Clear();
			DriveInfo[] drives = DriveInfo.GetDrives();
			foreach (DriveInfo driveInfo in drives)
			{
				try
				{
					if (driveInfo.IsReady)
					{
						_reachableDriveRoots.Add(driveInfo.RootDirectory.FullName);
					}
				}
				catch
				{
				}
			}
			foreach (AppItem item in _catalog)
			{
				_sourceCache[item.Id] = ResolveSource(item);
			}
			foreach (AppItem item2 in _catalog)
			{
				_versionCache[item2.Id] = ResolveVersion(item2);
			}
			foreach (AppItem item3 in _catalog)
			{
				_cardCache[item3.Id] = CreateAppCard(item3);
			}
		}

		private static int CategoryRank(string category)
		{
			string[] array = new string[19]
			{
				"Essenciais", "Escritório", "Produtividade", "Comunicação", "Documentos", "Utilitários", "Desenvolvimento", "Acesso remoto", "TI corporativo", "Drivers e hardware",
				"Impressão e scanner", "Multimídia", "Banco de dados", "Automação", "Engenharia e 3D", "Impressão 3D", "Adobe", "Jogos e launchers", "Componentes"
			};
			int num = Array.IndexOf(array, category);
			if (num >= 0)
			{
				return num;
			}
			return 999;
		}

		private Control CreateAppCard(AppItem app)
		{
			string source = GetSource(app);
			bool available = source != "Indisponível";
			Panel card = new Panel
			{
				Width = 174,
				Height = 62,
				Margin = new Padding(4),
				BackColor = (_selected.Contains(app.Id) ? Color.FromArgb(30, 64, 108) : Color.FromArgb(30, 41, 59)),
				Cursor = Cursors.Hand,
				Tag = app
			};
			PictureBox pictureBox = new PictureBox();
			pictureBox.Left = 8;
			pictureBox.Top = 12;
			pictureBox.Width = 38;
			pictureBox.Height = 38;
			pictureBox.Image = GetAppImage(app);
			pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
			pictureBox.BackColor = Color.Transparent;
			PictureBox pictureBox2 = pictureBox;
			Label label = new Label();
			label.Left = 52;
			label.Top = 6;
			label.Width = 94;
			label.Height = 31;
			label.Text = app.Name;
			label.ForeColor = (available ? Color.White : Color.FromArgb(100, 116, 139));
			label.Font = new Font("Segoe UI Semibold", 8.25f);
			label.AutoEllipsis = true;
			Label label2 = label;
			Label label3 = new Label();
			label3.Left = 52;
			label3.Top = 39;
			label3.Width = 94;
			label3.Height = 18;
			label3.Text = GetVersionLabel(app);
			label3.ForeColor = Color.FromArgb(148, 163, 184);
			label3.Font = new Font("Segoe UI", 7.25f);
			label3.AutoEllipsis = true;
			Label label4 = label3;
			CheckBox check = new CheckBox
			{
				Left = 150,
				Top = 7,
				Width = 18,
				Height = 18,
				Checked = _selected.Contains(app.Id),
				Enabled = available,
				BackColor = Color.Transparent
			};
			Button expand = new Button
			{
				Left = 149,
				Top = 34,
				Width = 20,
				Height = 21,
				Text = "⋮",
				FlatStyle = FlatStyle.Flat,
				BackColor = Color.FromArgb(51, 65, 85),
				ForeColor = Color.White,
				Cursor = Cursors.Hand,
				Font = new Font("Segoe UI", 8f)
			};
			expand.FlatAppearance.BorderSize = 0;
			card.Controls.Add(pictureBox2);
			card.Controls.Add(label2);
			card.Controls.Add(label4);
			card.Controls.Add(check);
			card.Controls.Add(expand);
			_cardChecks[app.Id] = check;
			EventHandler value = delegate
			{
				if (available)
				{
					check.Checked = !check.Checked;
				}
			};
			card.Click += value;
			pictureBox2.Click += value;
			label2.Click += value;
			label4.Click += value;
			check.CheckedChanged += delegate
			{
				if (check.Checked)
				{
					_selected.Add(app.Id);
				}
				else
				{
					_selected.Remove(app.Id);
				}
				card.BackColor = (check.Checked ? Color.FromArgb(30, 64, 108) : Color.FromArgb(30, 41, 59));
				UpdateSummary();
			};
			expand.Click += delegate
			{
				ContextMenuStrip contextMenuStrip = new ContextMenuStrip
				{
					BackColor = Color.FromArgb(30, 41, 59),
					ForeColor = Color.FromArgb(226, 232, 240),
					ShowImageMargin = false,
					Font = new Font("Segoe UI", 8.5f)
				};
				contextMenuStrip.Items.Add(new ToolStripMenuItem("Categoria: " + app.Category)
				{
					Enabled = false
				});
				contextMenuStrip.Items.Add(new ToolStripMenuItem("Fonte: " + source)
				{
					Enabled = false
				});
				contextMenuStrip.Items.Add(new ToolStripMenuItem("Instalação: " + GetMode(app, source))
				{
					Enabled = false
				});
				contextMenuStrip.Items.Add(new ToolStripSeparator());
				contextMenuStrip.Items.Add(new ToolStripMenuItem(WrapMenuText(app.Notes, 42))
				{
					Enabled = false
				});
				contextMenuStrip.Show(expand, new Point(0, expand.Height));
			};
			return card;
		}

		private static string WrapMenuText(string text, int width)
		{
			if (string.IsNullOrEmpty(text) || text.Length <= width)
			{
				return text;
			}
			string[] array = text.Split(' ');
			List<string> list = new List<string>();
			StringBuilder stringBuilder = new StringBuilder();
			string[] array2 = array;
			foreach (string text2 in array2)
			{
				if (stringBuilder.Length > 0 && stringBuilder.Length + text2.Length + 1 > width)
				{
					list.Add(stringBuilder.ToString());
					stringBuilder.Clear();
				}
				if (stringBuilder.Length > 0)
				{
					stringBuilder.Append(' ');
				}
				stringBuilder.Append(text2);
			}
			if (stringBuilder.Length > 0)
			{
				list.Add(stringBuilder.ToString());
			}
			return string.Join(Environment.NewLine, list);
		}

		private string LocalPath(AppItem app)
		{
			string text = (string.IsNullOrEmpty(app.RelativePath) ? null : Path.Combine(_folder.Text, app.RelativePath));
			if (text != null && File.Exists(text))
			{
				return text;
			}
			if (string.IsNullOrEmpty(app.AbsolutePath) || !File.Exists(app.AbsolutePath))
			{
				return text;
			}
			return app.AbsolutePath;
		}

		private bool HasLocal(AppItem app)
		{
			if (!string.IsNullOrEmpty(app.RelativePath) && File.Exists(Path.Combine(_folder.Text, app.RelativePath)))
			{
				return true;
			}
			if (!string.IsNullOrEmpty(app.AbsolutePath))
			{
				return File.Exists(app.AbsolutePath);
			}
			return false;
		}

		private bool IsAvailable(AppItem app)
		{
			return GetSource(app) != "Indisponível";
		}

		private string GetSource(AppItem app)
		{
			string value;
			if (_sourceCache.TryGetValue(app.Id, out value))
			{
				return value;
			}
			value = ResolveSource(app);
			_sourceCache[app.Id] = value;
			return value;
		}

		private string ResolveSource(AppItem app)
		{
			bool flag = HasConfiguredLocal(app);
			if (_preferLocal.Checked && flag)
			{
				return "Local";
			}
			if (!string.IsNullOrEmpty(app.WingetId))
			{
				return "Internet/winget";
			}
			if (flag)
			{
				return "Local";
			}
			if (!string.IsNullOrEmpty(app.Url))
			{
				return "Site oficial";
			}
			return "Indisponível";
		}

		private bool HasConfiguredLocal(AppItem app)
		{
			if (!string.IsNullOrEmpty(app.RelativePath) && _softwareRootAvailable)
			{
				return true;
			}
			if (!string.IsNullOrEmpty(app.AbsolutePath))
			{
				string pathRoot = Path.GetPathRoot(app.AbsolutePath);
				if (!string.IsNullOrEmpty(pathRoot))
				{
					return _reachableDriveRoots.Contains(pathRoot);
				}
				return false;
			}
			return false;
		}

		private string GetMode(AppItem app, string source)
		{
			if (source == "Internet/winget")
			{
				return "Automática";
			}
			if (source == "Site oficial")
			{
				return "Download assistido";
			}
			if (app.LocalKind == InstallKind.Manual)
			{
				return "Manual/assistida";
			}
			if (app.LocalKind == InstallKind.Interactive)
			{
				return "Interativa";
			}
			if (app.LocalKind == InstallKind.Portable)
			{
				return "Abrir aplicativo";
			}
			return "Silenciosa";
		}

		private Image GetAppImage(AppItem app)
		{
			Image value;
			if (_icons.TryGetValue(app.Id, out value))
			{
				return value;
			}
			if (_iconSprite != null)
			{
				int num = BuildCatalog().FindIndex((AppItem a) => a.Id == app.Id);
				if (num >= 0)
				{
					Bitmap bitmap = new Bitmap(64, 64);
					using (Graphics graphics = Graphics.FromImage(bitmap))
					{
						graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
						graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
						graphics.DrawImage(_iconSprite, new Rectangle(0, 0, 64, 64), new Rectangle(num % 10 * 96, num / 10 * 96, 96, 96), GraphicsUnit.Pixel);
					}
					value = bitmap;
				}
			}
			if (value == null)
			{
				value = CreateBadge(app);
			}
			_icons[app.Id] = value;
			return value;
		}

		private void LoadIconSprite()
		{
			try
			{
				using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("CentralInstalacaoTI.icons.png"))
				{
					if (stream != null)
					{
						using (Bitmap original = new Bitmap(stream))
						{
							_iconSprite = new Bitmap(original);
							return;
						}
					}
				}
			}
			catch
			{
				_iconSprite = null;
			}
		}

		private void LoadProgramIcon()
		{
			try
			{
				using (Icon icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
				{
					if (icon != null)
					{
						_programIcon = icon.ToBitmap();
					}
				}
			}
			catch
			{
				_programIcon = null;
			}
		}

		private string GetVersionLabel(AppItem app)
		{
			string value;
			if (_versionCache.TryGetValue(app.Id, out value))
			{
				return value;
			}
			value = ResolveVersion(app);
			_versionCache[app.Id] = value;
			return value;
		}

		private string ResolveVersion(AppItem app)
		{
			string source = GetSource(app);
			if (source == "Site oficial")
			{
				return "Detecção automática";
			}
			if (source == "Internet/winget")
			{
				return "Versão mais recente";
			}
			Dictionary<string, string> dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			dictionary.Add("chrome", "152.0");
			dictionary.Add("office", "Microsoft 365");
			dictionary.Add("reader", "64 bits");
			dictionary.Add("chatgpt", "22607.710");
			dictionary.Add("wacom", "6.4.13-4");
			dictionary.Add("steam", "Atual");
			dictionary.Add("epic", "20.1.4");
			dictionary.Add("discord", "1.0.9253");
			dictionary.Add("league", "Atual");
			dictionary.Add("sosbrowser", "3.0");
			dictionary.Add("creativecloud", "6.9.1");
			dictionary.Add("arduino", "2.3.6");
			dictionary.Add("blueplant", "9.1.40");
			dictionary.Add("bricscad", "V25");
			dictionary.Add("cadesimu", "4.2");
			dictionary.Add("citrix", "25.3.10");
			dictionary.Add("codesys", "3.5.15.30");
			dictionary.Add("java32", "8u461 • 32 bits");
			dictionary.Add("workbench", "8.0.41");
			dictionary.Add("naps2", "8.2.0");
			dictionary.Add("orcaslicer", "2.3.0");
			dictionary.Add("rufus", "4.9");
			dictionary.Add("simmaq", "3.8.9b");
			dictionary.Add("sinutrain", "4.8");
			dictionary.Add("vscode", "1.101.2");
			dictionary.Add("nx", "2512.3002");
			dictionary.Add("tia", "V17");
			dictionary.Add("fluidsim", "6");
			dictionary.Add("xampp", "8.2");
			Dictionary<string, string> dictionary2 = dictionary;
			string value;
			if (!dictionary2.TryGetValue(app.Id, out value))
			{
				return "Versão local";
			}
			if (!(value == "Atual") && !value.StartsWith("V", StringComparison.OrdinalIgnoreCase) && !value.Contains(" "))
			{
				return "v" + value;
			}
			return value;
		}

		private static Image CreateBadge(AppItem app)
		{
			Bitmap bitmap = new Bitmap(38, 38);
			Color color = Color.FromArgb(37, 99, 235);
			if (app.Category.Contains("Automação"))
			{
				color = Color.FromArgb(8, 145, 178);
			}
			else if (app.Category.Contains("Jogos"))
			{
				color = Color.FromArgb(124, 58, 237);
			}
			else if (app.Category.Contains("Driver") || app.Category.Contains("Hardware"))
			{
				color = Color.FromArgb(220, 38, 38);
			}
			else if (app.Category.Contains("Adobe"))
			{
				color = Color.FromArgb(225, 29, 72);
			}
			else if (app.Category.Contains("Engenharia"))
			{
				color = Color.FromArgb(5, 150, 105);
			}
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				using (SolidBrush brush = new SolidBrush(color))
				{
					using (SolidBrush brush2 = new SolidBrush(Color.White))
					{
						using (Font font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold))
						{
							graphics.SmoothingMode = SmoothingMode.AntiAlias;
							graphics.FillEllipse(brush, 1, 1, 36, 36);
							string s = string.Concat(from x in app.Name.Split(new char[3] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries).Take(2)
								select char.ToUpperInvariant(x[0]));
							SizeF sizeF = graphics.MeasureString(s, font);
							graphics.DrawString(s, font, brush2, (38f - sizeF.Width) / 2f, (38f - sizeF.Height) / 2f);
							return bitmap;
						}
					}
				}
			}
		}

		private void SelectBy(Func<AppItem, bool> condition)
		{
			foreach (AppItem item in _catalog.Where(condition))
			{
				_selected.Add(item.Id);
			}
			SyncCardChecks();
		}

		private void SelectVisible()
		{
			foreach (Control control in _cards.Controls)
			{
				AppItem appItem = control.Tag as AppItem;
				if (appItem != null && IsAvailable(appItem))
				{
					_selected.Add(appItem.Id);
				}
			}
			SyncCardChecks();
		}

		private void SyncCardChecks()
		{
			foreach (KeyValuePair<string, CheckBox> cardCheck in _cardChecks)
			{
				cardCheck.Value.Checked = _selected.Contains(cardCheck.Key);
			}
			UpdateSummary();
		}

		private void UpdateSummary()
		{
			_summary.Text = string.Format("{0} aplicativo(s) selecionado(s) — {1} item(ns) no catálogo", _selected.Count, _catalog.Count);
		}

		private void InstallSelected(object sender, EventArgs e)
		{
			SaveVisibleSelections();
			List<AppItem> apps = _catalog.Where((AppItem a) => _selected.Contains(a.Id) && IsAvailable(a)).ToList();
			if (apps.Count == 0)
			{
				MessageBox.Show(this, "Selecione pelo menos um aplicativo disponível.", "Central de Instalação", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
				return;
			}
			if (!IsAdministrator())
			{
				MessageBox.Show(this, "Abra a ferramenta como administrador.", "Elevação necessária", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
				return;
			}
			if (_drive.SelectedItem == null)
			{
				MessageBox.Show(this, "Escolha o disco de destino.");
				return;
			}
			if (MessageBox.Show(this, "Instalar os aplicativos selecionados? Instaladores podem solicitar permissões e reinicialização.", "Instalar programas", MessageBoxButtons.OKCancel) != DialogResult.OK) return;
			string path = _drive.SelectedItem.ToString().Substring(0, 3);
			string installRoot = Path.Combine(path, "Programas");
			Directory.CreateDirectory(installRoot);
			string text = Path.Combine(FrankensteinTI.Distribution.DataRoot, "Instalacoes");
			Directory.CreateDirectory(text);
			_logPath = Path.Combine(text, string.Format("instalacao-{0:yyyyMMdd-HHmmss}.log", DateTime.Now));
			if (!FrankensteinTI.Session.Begin("Instalação de programas")) return;
			_install.Enabled = false;
			_log.Clear();
			_bottom.Visible = true;
			LayoutContent();
			_runSoftwareRoot = _folder.Text;
			_runPreferLocal = _preferLocal.Checked;
			LogUi("Iniciando instalação de " + apps.Count + " aplicativo(s). Destino preferido: " + installRoot);
			BackgroundWorker backgroundWorker = new BackgroundWorker();
			backgroundWorker.DoWork += delegate
			{
				foreach (AppItem item in apps)
				{
					InstallOne(item, installRoot);
				}
			};
			backgroundWorker.RunWorkerCompleted += delegate(object s, RunWorkerCompletedEventArgs completed)
			{
				FrankensteinTI.Session.End(completed.Error == null ? "Encerrado · conferir relatório" : "Falha", _logPath);
				_install.Enabled = true;
				if (completed.Error != null)
				{
					LogUi("ERRO GERAL: " + completed.Error.Message);
				}
				LogUi("Processo concluído. Relatório: " + _logPath);
				MessageBox.Show(this, "Processo concluído. Consulte o painel de atividade e o relatório.", "Central de Instalação", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
			};
			backgroundWorker.RunWorkerAsync();
		}

		private void InstallOne(AppItem app, string installRoot)
		{
			string sourceSafe = GetSourceSafe(app);
			LogUi("\r\n>>> " + app.Name + " [" + sourceSafe + "]");
			try
			{
				if (sourceSafe == "Site oficial")
				{
					ProcessStartInfo processStartInfo = new ProcessStartInfo(app.Url);
					processStartInfo.UseShellExecute = true;
					Process.Start(processStartInfo);
					LogUi("Página oficial aberta para detectar o hardware e baixar a versão compatível.");
					return;
				}
				int num;
				if (sourceSafe == "Internet/winget")
				{
					string text = Path.Combine(installRoot, Sanitize(app.Name));
					Directory.CreateDirectory(text);
					string arguments = string.Format("install --id {0} --exact --silent --accept-package-agreements --accept-source-agreements --disable-interactivity --source winget --location \"{1}\"", app.WingetId, text);
					num = RunProcess("winget.exe", arguments, null, true);
				}
				else
				{
					string text2 = (string.IsNullOrEmpty(app.RelativePath) ? null : Path.Combine(_runSoftwareRoot, app.RelativePath));
					string text3 = ((text2 != null && File.Exists(text2)) ? text2 : app.AbsolutePath);
					if (app.LocalKind == InstallKind.Manual || app.LocalKind == InstallKind.Portable)
					{
						ProcessStartInfo processStartInfo2 = new ProcessStartInfo(text3);
						processStartInfo2.UseShellExecute = true;
						Process.Start(processStartInfo2);
						LogUi((app.LocalKind == InstallKind.Manual) ? "Aberto para instalação assistida." : "Aplicativo portátil aberto.");
						return;
					}
					num = ((app.LocalKind != InstallKind.MsiSilent) ? RunProcess(text3, app.Arguments ?? string.Empty, Path.GetDirectoryName(text3), false) : RunProcess("msiexec.exe", string.Format("/i \"{0}\" /qn /norestart", text3), Path.GetDirectoryName(text3), false));
				}
				LogUi((num == 0) ? "Concluído sem erro." : ("Finalizado com código de saída " + num + "."));
			}
			catch (Exception ex)
			{
				LogUi("FALHA: " + ex.Message);
			}
		}

		private string GetSourceSafe(AppItem app)
		{
			string text = (string.IsNullOrEmpty(app.RelativePath) ? null : Path.Combine(_runSoftwareRoot, app.RelativePath));
			bool flag = text != null && File.Exists(text);
			if (!flag && !string.IsNullOrEmpty(app.AbsolutePath))
			{
				flag = File.Exists(app.AbsolutePath);
			}
			if (_runPreferLocal && flag)
			{
				return "Local";
			}
			if (!string.IsNullOrEmpty(app.WingetId))
			{
				return "Internet/winget";
			}
			if (flag)
			{
				return "Local";
			}
			if (string.IsNullOrEmpty(app.Url))
			{
				return "Indisponível";
			}
			return "Site oficial";
		}

		private int RunProcess(string file, string arguments, string workingDirectory, bool capture)
		{
			ProcessStartInfo processStartInfo = new ProcessStartInfo(file, arguments);
			processStartInfo.UseShellExecute = !capture;
			processStartInfo.CreateNoWindow = capture;
			ProcessStartInfo processStartInfo2 = processStartInfo;
			if (!string.IsNullOrEmpty(workingDirectory))
			{
				processStartInfo2.WorkingDirectory = workingDirectory;
			}
			if (capture)
			{
				processStartInfo2.RedirectStandardOutput = true;
				processStartInfo2.RedirectStandardError = true;
				processStartInfo2.StandardOutputEncoding = Encoding.UTF8;
				processStartInfo2.StandardErrorEncoding = Encoding.UTF8;
			}
			Process process = new Process();
			process.StartInfo = processStartInfo2;
			using (Process process2 = process)
			{
				if (capture)
				{
					process2.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
					{
						if (!string.IsNullOrWhiteSpace(e.Data))
						{
							LogUi(e.Data);
						}
					};
					process2.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
					{
						if (!string.IsNullOrWhiteSpace(e.Data))
						{
							LogUi(e.Data);
						}
					};
				}
				process2.Start();
				if (capture)
				{
					process2.BeginOutputReadLine();
					process2.BeginErrorReadLine();
				}
				process2.WaitForExit();
				if (capture)
				{
					process2.WaitForExit();
				}
				return process2.ExitCode;
			}
		}

		private void LogUi(string message)
		{
			string line = string.Format("[{0:HH:mm:ss}] {1}", DateTime.Now, message);
			lock (this)
			{
				if (!string.IsNullOrEmpty(_logPath))
				{
					File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
				}
			}
			if (!base.IsDisposed)
			{
				BeginInvoke((MethodInvoker)delegate
				{
					_log.AppendText(line + Environment.NewLine);
				});
			}
		}

		private void OpenReports(object sender, EventArgs e)
		{
			string text = Path.Combine(FrankensteinTI.Distribution.DataRoot, "Instalacoes");
			Directory.CreateDirectory(text);
			Process.Start("explorer.exe", text);
		}

		private static bool IsAdministrator()
		{
			using (WindowsIdentity ntIdentity = WindowsIdentity.GetCurrent())
			{
				return new WindowsPrincipal(ntIdentity).IsInRole(WindowsBuiltInRole.Administrator);
			}
		}

		private static string Sanitize(string value)
		{
			char[] invalid = Path.GetInvalidFileNameChars();
			return string.Concat(value.Select((char c) => (!invalid.Contains(c)) ? c : '_'));
		}

		private static List<AppItem> BuildCatalog()
		{
			List<AppItem> list = new List<AppItem>();
			list.Add(new AppItem("chrome", "Google Chrome", "Essenciais", "Google.Chrome", null, InstallKind.Interactive, "", true, "Navegador web").FromDrive(null));
			list.Add(new AppItem("office", "Microsoft 365 / Office", "Escritório", "Microsoft.Office", null, InstallKind.Winget, null, true, "Requer licença Microsoft 365/Office"));
			list.Add(new AppItem("7zip", "7-Zip", "Essenciais", "7zip.7zip", null, InstallKind.Winget, null, true, "Compactação de arquivos"));
			list.Add(new AppItem("firefox", "Mozilla Firefox", "Essenciais", "Mozilla.Firefox", null, InstallKind.Winget, null, false, "Navegador alternativo"));
			list.Add(new AppItem("vlc", "VLC media player", "Multimídia", "VideoLAN.VLC", null, InstallKind.Winget, null, true, "Áudio e vídeo"));
			list.Add(new AppItem("reader", "Adobe Acrobat Reader", "Documentos", "Adobe.Acrobat.Reader.64-bit", null, InstallKind.Winget, null, true, "Leitor de PDF"));
			list.Add(new AppItem("notepadpp", "Notepad++", "Desenvolvimento", "Notepad++.Notepad++", null, InstallKind.Winget, null, true, "Editor de texto"));
			list.Add(new AppItem("git", "Git", "Desenvolvimento", "Git.Git", null, InstallKind.Winget, null, false, "Controle de versão"));
			list.Add(new AppItem("powertoys", "Microsoft PowerToys", "Utilitários", "Microsoft.PowerToys", null, InstallKind.Winget, null, false, "Ferramentas avançadas do Windows"));
			list.Add(new AppItem("teams", "Microsoft Teams", "Comunicação", "Microsoft.Teams", null, InstallKind.Winget, null, true, "Comunicação corporativa"));
			list.Add(new AppItem("zoom", "Zoom Workplace", "Comunicação", "Zoom.Zoom", null, InstallKind.Winget, null, false, "Videoconferência"));
			list.Add(new AppItem("winrar", "WinRAR", "Utilitários", "RARLab.WinRAR", null, InstallKind.Winget, null, false, "Requer licença após avaliação"));
			list.Add(new AppItem("obs", "OBS Studio", "Multimídia", "OBSProject.OBSStudio", null, InstallKind.Winget, null, false, "Gravação e transmissão"));
			list.Add(new AppItem("chatgpt", "ChatGPT", "Produtividade", null, null, InstallKind.Interactive, "", false, "Instalador oficial da Microsoft Store").FromDrive(null));
			list.Add(new AppItem("wacom", "Driver Wacom Tablet", "Drivers e hardware", "Wacom.WacomTabletDriver", null, InstallKind.Interactive, "", false, "Driver para mesas digitalizadoras Wacom").FromDrive(null));
			list.Add(new AppItem("blender", "Blender", "Engenharia e 3D", "BlenderFoundation.Blender", null, InstallKind.Winget, null, false, "Modelagem 3D"));
			list.Add(new AppItem("unity", "Unity Hub", "Desenvolvimento", "Unity.UnityHub", null, InstallKind.Winget, null, false, "Editor e módulos são escolhidos no Hub"));
			list.Add(new AppItem("mysql", "MySQL Server", "Banco de dados", "Oracle.MySQL", null, InstallKind.Winget, null, false, "Pode conflitar com o MySQL do XAMPP na porta 3306"));
			list.Add(new AppItem("xampp", "XAMPP 8.2", "Desenvolvimento", "ApacheFriends.Xampp.8.2", null, InstallKind.Winget, null, false, "Inclui Apache, PHP e MySQL/MariaDB"));
			list.Add(new AppItem("steam", "Steam", "Jogos e launchers", "Valve.Steam", null, InstallKind.Interactive, "", false, "Launcher de jogos da Valve").FromDrive(null));
			list.Add(new AppItem("epic", "Epic Games Launcher", "Jogos e launchers", "EpicGames.EpicGamesLauncher", null, InstallKind.Interactive, "", false, "Launcher da Epic Games").FromDrive(null));
			list.Add(new AppItem("discord", "Discord", "Comunicação", "Discord.Discord", null, InstallKind.Interactive, "", false, "Comunicação por voz e texto").FromDrive(null));
			list.Add(new AppItem("league", "League of Legends", "Jogos e launchers", null, null, InstallKind.Interactive, "", false, "Instalador Riot Games assinado digitalmente").FromDrive(null));
			list.Add(new AppItem("nvidiaapp", "NVIDIA App", "Drivers e hardware", null, null, InstallKind.Manual, "", false, "Baixe o driver NVIDIA compatível com o hardware").FromOfficialSite("https://www.nvidia.com/en-us/software/nvidia-app/"));
			list.Add(new AppItem("ryzenmaster", "AMD Ryzen Master", "Drivers e hardware", null, null, InstallKind.Manual, "", false, "Somente para processadores Ryzen compatíveis; altera parâmetros de desempenho").FromOfficialSite("https://www.amd.com/en/products/software/ryzen-master.html"));
			list.Add(new AppItem("adrenalin", "AMD Software: Adrenalin Edition", "Drivers e hardware", null, null, InstallKind.Manual, "", false, "Detecção oficial do modelo Radeon e download do driver correto").FromOfficialSite("https://www.amd.com/en/support/download/drivers.html"));
			list.Add(new AppItem("sosbrowser", "Navegador SOS Professional", "TI corporativo", null, null, InstallKind.Interactive, "", false, "Aplicativo interno sem assinatura digital; use somente se autorizado").FromDrive(null));
			list.Add(new AppItem("meshagent", "MeshCentral Agent - SENAI FZ", "TI corporativo", null, null, InstallKind.Manual, "", false, "Agente de gerenciamento remoto específico da unidade; instalação assistida").FromDrive(null));
			list.Add(new AppItem("restricoes", "Restrições de Usuário - Windows 10", "TI corporativo", null, null, InstallKind.Manual, "", false, "Altera políticas do Windows; execute somente em equipamento autorizado").FromDrive(null));
			list.Add(new AppItem("creativecloud", "Adobe Creative Cloud", "Adobe", "Adobe.CreativeCloud", "Adobe Creative Cloud_pt_BR_WIN_64 (1)\\Adobe Creative Cloud\\Build\\setup.exe", InstallKind.ExeSilent, "--silent", false, "Requer conta/licença Adobe"));
			list.Add(new AppItem("arduino", "Arduino IDE", "Automação", "ArduinoSA.IDE.stable", "Arduino IDE\\arduino-ide_2.3.6_Windows_64bit.msi", InstallKind.MsiSilent, null, false, "Instalador local disponível"));
			list.Add(new AppItem("blueplant", "BluePlant 9.1", "Automação", null, "BluePlant\\BluePlant 9.1.40.exe", InstallKind.Interactive, "", false, "Instalação interativa/licenciada"));
			list.Add(new AppItem("bricscad", "BricsCAD V25", "Engenharia e 3D", null, "BRICS CAD\\BricsCAD V25.msi", InstallKind.MsiSilent, null, false, "Requer licença BricsCAD"));
			list.Add(new AppItem("cadesimu", "CADe_SIMU 4.2", "Automação", null, "CADeSIMU\\CADe_SIMU_V4.2.exe", InstallKind.Interactive, "", false, "Instalação interativa"));
			list.Add(new AppItem("citrix", "Citrix Workspace", "Acesso remoto", "Citrix.Workspace", "Citrix Workspace\\CitrixWorkspaceApp.exe", InstallKind.ExeSilent, "/silent /noreboot", true, "Cliente corporativo"));
			list.Add(new AppItem("codesys", "CODESYS 64 bits", "Automação", null, "Codesys\\CODESYS 64 bits.exe", InstallKind.Interactive, "", false, "Versão local 3.5.15.30"));
			list.Add(new AppItem("java32", "Java JRE 8 (32 bits)", "Componentes", null, "Java JRE 32bits\\jre-8u461-windows-i586.exe", InstallKind.ExeSilent, "/s", false, "Necessário para alguns sistemas legados"));
			list.Add(new AppItem("lexmark", "Lexmark ScanBack", "Impressão e scanner", null, "Lexmark ScanBack\\Lexmark_ScanBack_01182023.exe", InstallKind.Interactive, "", false, "Utilitário de digitalização"));
			list.Add(new AppItem("microsip", "MicroSIP", "Comunicação", "MicroSIP.MicroSIP", "MicroSIP\\MicroSIP-3.21.6.exe", InstallKind.ExeSilent, "/VERYSILENT /NORESTART", false, "Cliente SIP"));
			list.Add(new AppItem("workbench", "MySQL Workbench", "Banco de dados", "Oracle.MySQLWorkbench", "MySQL - Workbench\\mysql-workbench-community-8.0.41-winx64.msi", InstallKind.MsiSilent, null, false, "Administração do MySQL"));
			list.Add(new AppItem("naps2", "NAPS2", "Impressão e scanner", "Cyanfish.NAPS2", "NAPS2\\naps2-8.2.0-win-x64.exe", InstallKind.ExeSilent, "/VERYSILENT /NORESTART", false, "Digitalização e PDF"));
			list.Add(new AppItem("orcaslicer", "OrcaSlicer", "Impressão 3D", "SoftFever.OrcaSlicer", "Orca Slicer\\OrcaSlicer_Windows_Installer_V2.3.0.exe", InstallKind.ExeSilent, "/S", false, "Fatiador para impressão 3D"));
			list.Add(new AppItem("rufus", "Rufus (portátil)", "Utilitários", "Rufus.Rufus", "Rufus\\rufus-4.9.exe", InstallKind.Portable, "", false, "Executável portátil; será apenas aberto"));
			list.Add(new AppItem("simmaq", "Simmaq3D Modbus 3.8.9b", "Automação", null, "Simmaq 3D Modbus\\Simmaq3D_Modbus_3.8.9b_Setup.exe", InstallKind.Interactive, "", false, "Instalação interativa"));
			list.Add(new AppItem("sinutrain", "SinuTrain for SINUMERIK", "Automação", null, "SINUTRAIN for SINUMERIK Operate Basic V4_8 Ed_2\\Setup.exe", InstallKind.Interactive, "", false, "Instalação Siemens assistida"));
			list.Add(new AppItem("vscode", "Visual Studio Code", "Desenvolvimento", "Microsoft.VisualStudioCode", "Visual Studio Code\\VSCodeUserSetup-x64-1.101.2.exe", InstallKind.ExeSilent, "/VERYSILENT /NORESTART /MERGETASKS=!runcode", true, "Editor de código"));
			list.Add(new AppItem("nx", "Siemens NX 2512", "Engenharia e 3D", null, "NX\\SiemensDesigncenterNX-2512.3002_wntx64.zip", InstallKind.Manual, "", false, "Arquivo ZIP grande; abre para instalação assistida"));
			list.Add(new AppItem("tia", "TIA Portal V17", "Automação", null, "Tia portal\\TIA_Portal_STEP7_Prof_Safety_WINCC_Adv_Unified_V17.iso", InstallKind.Manual, "", false, "ISO; instalação manual e licença Siemens"));
			list.Add(new AppItem("fluidsim", "FluidSIM 6 - instruções", "Automação", null, "FLUIDSIM\\InstalacaoFluidSIM6 Atualizada_Servidor MINDURI-N-GEP-SN272659.docx", InstallKind.Manual, "", false, "Abre o procedimento de instalação interno"));
			return list;
		}
	}
}
