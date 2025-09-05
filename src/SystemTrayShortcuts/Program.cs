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
			ShowMessageBox("TODO");
		}

		private static void ShowMessageBox(params string[] lines)
		{
			MessageBox.Show(
				text: string.Join(Environment.NewLine, lines),
				caption: c_appCaption);
		}

		private const string c_appCaption = "SystemTrayShortcuts";
	}
}
