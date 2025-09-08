using System.Diagnostics;

namespace SystemTrayShortcuts
{
	internal static class Program
	{
		[STAThread]
		public static void Main(string[] args)
		{
			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.SetHighDpiMode(HighDpiMode.SystemAware);
#pragma warning disable WFO5001
			Application.SetColorMode(SystemColorMode.System);
#pragma warning restore WFO5001

			try
			{
				Run();
			}
			catch (Exception exception)
			{
				ShowMessageBox($"Error: {exception.Message}");
			}
		}

		private static void Run()
		{
			using var notifyIcon = new NotifyIcon();
			notifyIcon.Text = c_appCaption;
			notifyIcon.Icon = NativeMethods.GetShellIcon(NativeMethods.SIID_STUFFEDFOLDER);
			notifyIcon.Visible = true;

			var contextMenu = new ContextMenuStrip();
			AddChildItemsForFileSystemEntries(contextMenu.Items, [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)]);
			contextMenu.Items.Add(new ToolStripSeparator());
			contextMenu.Items.Add(CreateSettingsMenuItem());
			contextMenu.Items.Add(CreateExitMenuItem());

			notifyIcon.ContextMenuStrip = contextMenu;

			notifyIcon.MouseUp += (_, e) =>
			{
				if (e.Button == MouseButtons.Left)
					notifyIcon.GetType().GetMethod("ShowContextMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(notifyIcon, null);
			};

			Application.Run();
		}

		private static void AddChildItemsForDirectory(ToolStripItemCollection collection, string directoryPath)
		{
			try
			{
				if (!Directory.Exists(directoryPath))
					return;

				const int maxEntries = 100;
				var topEntries = Directory.EnumerateFileSystemEntries(directoryPath)
					.Where(entry => !IsHiddenOrSystem(entry))
					.Take(maxEntries + 1)
					.ToList();

				AddChildItemsForFileSystemEntries(collection, topEntries.Take(maxEntries));

				if (topEntries.Count > maxEntries)
				{
					var moreItem = new ToolStripMenuItem("More...");
					moreItem.Click += (_, _) => OpenFileSystemEntry(directoryPath);
					collection.Add(moreItem);
				}
			}
			catch (Exception ex)
			{
				collection.Add(new ToolStripMenuItem($"Error: {ex.Message}") { Enabled = false });
			}
		}

		private static void AddChildItemsForFileSystemEntries(ToolStripItemCollection collection, IEnumerable<string> paths)
		{
			try
			{
				foreach (var (path, name, isDirectory) in paths
					.Select(entry => (Path: entry, Name: Path.GetFileName(entry), IsDirectory: Directory.Exists(entry)))
					.OrderBy(entry => entry.IsDirectory ? 0 : 1)
					.ThenBy(entry => entry.Name, StringComparer.InvariantCultureIgnoreCase))
				{
					var menuItem = new ToolStripMenuItem(name)
					{
						Image = NativeMethods.GetFileIcon(path).ToBitmap(),
					};

					if (isDirectory)
					{
						const string loadingText = "Loading...";
						menuItem.DropDownItems.Add(loadingText);
						menuItem.DropDownOpening += (_, _) =>
						{
							if (menuItem.DropDownItems is [{ Text: loadingText }])
							{
								menuItem.DropDownItems.Clear();
								AddChildItemsForDirectory(menuItem.DropDownItems, path);
								if (menuItem.DropDownItems.Count == 0)
									menuItem.DropDownItems.Add(new ToolStripMenuItem("(Empty)") { Enabled = false });
							}
						};
						menuItem.DoubleClickEnabled = true;
						menuItem.DoubleClick += (_, _) => OpenFileSystemEntry(path);
					}
					else
					{
						menuItem.Click += (_, _) => OpenFileSystemEntry(path);
					}

					collection.Add(menuItem);
				}
			}
			catch (Exception exception)
			{
				collection.Add(new ToolStripMenuItem($"Error: {exception.Message}") { Enabled = false });
			}
		}

		private static bool IsHiddenOrSystem(string path)
		{
			try
			{
				var attributes = File.GetAttributes(path);
				return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
					(attributes & FileAttributes.System) == FileAttributes.System;
			}
			catch
			{
				return false;
			}
		}

		private static void OpenFileSystemEntry(string path)
		{
			try
			{
				Process.Start(new ProcessStartInfo
				{
					FileName = path,
					UseShellExecute = true,
				});
			}
			catch (Exception exception)
			{
				ShowMessageBox($"Failed to open file: {exception.Message}");
			}
		}

		private static ToolStripMenuItem CreateSettingsMenuItem()
		{
			var settingsItem = new ToolStripMenuItem("Settings...");
			settingsItem.Click += (_, _) => ShowSettingsDialog();
			return settingsItem;
		}

		private static ToolStripMenuItem CreateExitMenuItem()
		{
			var exitItem = new ToolStripMenuItem("Exit");
			exitItem.Click += (_, _) => Application.Exit();
			return exitItem;
		}

		private static void ShowSettingsDialog()
		{
			using var dialog = new Form
			{
				Text = "Settings",
				Size = new Size(500, 400),
				MinimumSize = new Size(400, 300),
				StartPosition = FormStartPosition.CenterScreen,
				FormBorderStyle = FormBorderStyle.Sizable,
				MaximizeBox = true,
				MinimizeBox = false,
				ShowIcon = false,
				ShowInTaskbar = false,
			};

			var mainLayout = new TableLayoutPanel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(12),
				RowCount = 4,
				ColumnCount = 1,
			};

			mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Label row
			mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // TextBox row
			mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // CheckBox row
			mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Button row

			mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

			dialog.Controls.Add(mainLayout);

			var pathsLabel = new Label
			{
				Text = "Paths to display (one per line):",
				AutoSize = true,
				Anchor = AnchorStyles.Left,
				Margin = new Padding(0, 0, 0, 6),
			};
			mainLayout.Controls.Add(pathsLabel, 0, 0);

			var pathsTextBox = new TextBox
			{
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				AcceptsReturn = true,
				AcceptsTab = false,
				WordWrap = false,
				Dock = DockStyle.Fill,
				Margin = new Padding(0, 0, 0, 12),
			};
			mainLayout.Controls.Add(pathsTextBox, 0, 1);

			var startupCheckBox = new CheckBox
			{
				Text = "Launch on Windows startup",
				AutoSize = true,
				Anchor = AnchorStyles.Left,
				Margin = new Padding(0, 0, 0, 12),
			};
			mainLayout.Controls.Add(startupCheckBox, 0, 2);

			var buttonPanel = new TableLayoutPanel
			{
				AutoSize = true,
				AutoSizeMode = AutoSizeMode.GrowAndShrink,
				Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
				RowCount = 1,
				ColumnCount = 2,
				Margin = new Padding(0),
			};

			buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			buttonPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			buttonPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

			var okButton = new Button
			{
				Text = "OK",
				DialogResult = DialogResult.OK,
				AutoSize = true,
				MinimumSize = new Size(75, 23),
				Margin = new Padding(0, 0, 6, 0),
			};
			buttonPanel.Controls.Add(okButton, 0, 0);

			var cancelButton = new Button
			{
				Text = "Cancel",
				DialogResult = DialogResult.Cancel,
				AutoSize = true,
				MinimumSize = new Size(75, 23),
				Margin = new Padding(0),
			};
			buttonPanel.Controls.Add(cancelButton, 1, 0);

			mainLayout.Controls.Add(buttonPanel, 0, 3);

			dialog.AcceptButton = okButton;
			dialog.CancelButton = cancelButton;

			// TODO: Load current settings into the controls
			// For now, just show placeholder text
			pathsTextBox.Text = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

			if (dialog.ShowDialog() == DialogResult.OK)
			{
				// TODO: Save the settings
				// For now, just show what would be saved
				var paths = pathsTextBox.Text;
				var launchOnStartup = startupCheckBox.Checked;

				// Placeholder - we'll implement actual saving later
				ShowMessageBox($"Settings saved (placeholder):\nPaths: {paths}\nLaunch on startup: {launchOnStartup}");
			}
		}

		private static void ShowMessageBox(params string[] lines) =>
			MessageBox.Show(
				text: string.Join(Environment.NewLine, lines),
				caption: c_appCaption);

		private const string c_appCaption = "System Tray Shortcuts";
	}
}
