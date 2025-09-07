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

		private static ToolStripMenuItem CreateExitMenuItem()
		{
			var exitItem = new ToolStripMenuItem("Exit");
			exitItem.Click += (_, _) => Application.Exit();
			return exitItem;
		}

		private static void ShowMessageBox(params string[] lines) =>
			MessageBox.Show(
				text: string.Join(Environment.NewLine, lines),
				caption: c_appCaption);

		private const string c_appCaption = "System Tray Shortcuts";
	}
}
