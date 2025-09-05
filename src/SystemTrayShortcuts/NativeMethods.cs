using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SystemTrayShortcuts;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Public within internal.")]
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Native methods.")]
internal static partial class NativeMethods
{
	public const int SIID_STUFFEDFOLDER = 57;
	public const uint SHGSI_ICON = 0x000000100;
	public const uint SHGSI_SMALLICON = 0x000000001;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public unsafe struct SHSTOCKICONINFO
	{
		public uint cbSize;
		public nint hIcon;
		public int iSysImageIndex;
		public int iIcon;
		public fixed char szPath[260];
	}

	[LibraryImport("Shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
	public static partial int SHGetStockIconInfo(int siid, uint uFlags, ref SHSTOCKICONINFO psii);

	public static Icon GetShellIcon(int shellIconId)
	{
		var info = new SHSTOCKICONINFO { cbSize = (uint) Marshal.SizeOf<SHSTOCKICONINFO>() };
		return SHGetStockIconInfo(shellIconId, SHGSI_ICON | SHGSI_SMALLICON, ref info) == 0 && info.hIcon != IntPtr.Zero
			? Icon.FromHandle(info.hIcon)
			: SystemIcons.Application;
	}
}
