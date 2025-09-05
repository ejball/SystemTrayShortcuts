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
			var exitItem = new ToolStripMenuItem("Exit");
			exitItem.Click += (_, _) => Application.Exit();
			contextMenu.Items.Add(exitItem);
			notifyIcon.ContextMenuStrip = contextMenu;

			notifyIcon.MouseUp += (_, e) =>
			{
				if (e.Button == MouseButtons.Left)
					contextMenu.Show(Cursor.Position);
			};

			Application.Run();
		}

		private static void ShowMessageBox(params string[] lines)
		{
			MessageBox.Show(
				text: string.Join(Environment.NewLine, lines),
				caption: c_appCaption);
		}

		private const string c_appCaption = "System Tray Shortcuts";
	}
}
