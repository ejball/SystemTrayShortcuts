using System.Diagnostics;
using Microsoft.Win32;

namespace SystemTrayShortcuts;

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
		s_notifyIcon = new NotifyIcon();
		s_notifyIcon.Text = c_appCaption;
		s_notifyIcon.Icon = NativeMethods.GetShellIcon(NativeMethods.SIID_STUFFEDFOLDER);
		s_notifyIcon.Visible = true;

		BuildContextMenu();

		s_notifyIcon.MouseUp += (_, e) =>
		{
			if (e.Button == MouseButtons.Left)
				s_notifyIcon.GetType().GetMethod("ShowContextMenu", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(s_notifyIcon, null);
		};

		Application.Run();
	}

	private static void BuildContextMenu()
	{
		if (s_notifyIcon == null)
			return;

		var contextMenu = new ContextMenuStrip();
		AddChildItemsForFileSystemEntries(contextMenu.Items, GetSavedPaths(), sort: false);
		contextMenu.Items.Add(new ToolStripSeparator());
		contextMenu.Items.Add(CreateSettingsMenuItem());
		contextMenu.Items.Add(CreateExitMenuItem());

		s_notifyIcon.ContextMenuStrip = contextMenu;
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

			AddChildItemsForFileSystemEntries(collection, topEntries.Take(maxEntries), sort: true);

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

	private static void AddChildItemsForFileSystemEntries(ToolStripItemCollection collection, IEnumerable<string> paths, bool sort)
	{
		try
		{
			var entries = paths
				.Select(entry => (Path: entry, Name: GetName(entry), IsDirectory: Directory.Exists(entry)));

			if (sort)
			{
				entries = entries
					.OrderBy(entry => entry.IsDirectory ? 0 : 1)
					.ThenBy(entry => entry.Name, StringComparer.InvariantCultureIgnoreCase);
			}

			foreach (var (path, name, isDirectory) in entries)
			{
				var menuItem = new ToolStripMenuItem(name)
				{
					Image = NativeMethods.GetFileIcon(path).ToBitmap(),
				};

				if (isDirectory)
				{
					menuItem.DropDownItems.Add("Loading...");
					menuItem.DropDownOpening += (_, _) =>
					{
						menuItem.DropDownItems.Clear();
						AddChildItemsForDirectory(menuItem.DropDownItems, path);
						if (menuItem.DropDownItems.Count == 0)
							menuItem.DropDownItems.Add(new ToolStripMenuItem("(Empty)") { Enabled = false });
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

		static string GetName(string path) => Path.GetFileName(path) is { Length: > 0 } name ? name : path;
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
		exitItem.Click += (_, _) =>
		{
			s_notifyIcon?.Dispose();
			Application.Exit();
		};
		return exitItem;
	}

	private static void ShowSettingsDialog()
	{
		using var dialog = new Form
		{
			Text = $"{c_appCaption} Settings",
			Size = new Size(640, 480),
			MinimumSize = new Size(640, 480),
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

		pathsTextBox.Text = string.Join(Environment.NewLine, GetSavedPaths());
		startupCheckBox.Checked = IsStartupEnabled();

		if (dialog.ShowDialog() == DialogResult.OK)
		{
			SavePaths(pathsTextBox.Text
				.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
			SetStartupEnabled(startupCheckBox.Checked);

			BuildContextMenu();
		}
	}

	private static bool IsStartupEnabled()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(c_windowsRunRegistryPath, writable: false);
			var registryValue = key?.GetValue(c_appCaption) as string;
			return registryValue is not null &&
				string.Equals(registryValue.Trim('"'), GetExecutablePath(), StringComparison.OrdinalIgnoreCase);
		}
		catch
		{
			return false;
		}
	}

	private static void SetStartupEnabled(bool enabled)
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(c_windowsRunRegistryPath, writable: true);
			if (key is not null)
			{
				if (enabled)
					key.SetValue(c_appCaption, $"\"{GetExecutablePath()}\"");
				else
					key.DeleteValue(c_appCaption, false);
			}
		}
		catch (Exception exception)
		{
			ShowMessageBox($"Failed to update startup setting: {exception.Message}");
		}
	}

	private static string GetExecutablePath() => Environment.ProcessPath ?? Application.ExecutablePath;

	private static List<string> GetSavedPaths()
	{
		try
		{
			using var key = Registry.CurrentUser.OpenSubKey(c_appRegistryPath, writable: false);
			var paths = (key?.GetValue("Paths") as string ?? "")
				.Split(['\r', '\n', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToList();
			return paths.Count != 0 ? paths : [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)];
		}
		catch
		{
			return [Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)];
		}
	}

	private static void SavePaths(IEnumerable<string> paths)
	{
		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(c_appRegistryPath);
			key.SetValue("Paths", string.Join(";", paths));
		}
		catch (Exception exception)
		{
			ShowMessageBox($"Failed to save paths setting: {exception.Message}");
		}
	}

	private static void ShowMessageBox(params string[] lines) =>
		MessageBox.Show(
			text: string.Join(Environment.NewLine, lines),
			caption: c_appCaption);

	private const string c_appCaption = "System Tray Shortcuts";
	private const string c_windowsRunRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
	private const string c_appRegistryPath = @"SOFTWARE\SystemTrayShortcuts";

	private static NotifyIcon? s_notifyIcon;
}
