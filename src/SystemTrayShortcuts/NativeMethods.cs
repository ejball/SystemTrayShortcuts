using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SystemTrayShortcuts;

[SuppressMessage("Maintainability", "CA1515:Consider making public types internal", Justification = "Public within internal.")]
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Native methods.")]
internal static partial class NativeMethods
{
	public const int SIID_STUFFEDFOLDER = 57;
	public const int SIID_FOLDER = 3;
	public const int SIID_DOCNOASSOC = 0;
	public const uint SHGSI_ICON = 0x000000100;
	public const uint SHGSI_SMALLICON = 0x000000001;
	public const uint SHGFI_ICON = 0x000000100;
	public const uint SHGFI_SMALLICON = 0x000000001;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public unsafe struct SHSTOCKICONINFO
	{
		public uint cbSize;
		public nint hIcon;
		public int iSysImageIndex;
		public int iIcon;
		public fixed char szPath[260];
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public unsafe struct SHFILEINFOW
	{
		public nint hIcon;
		public int iIcon;
		public uint dwAttributes;
		public fixed char szDisplayName[260];
		public fixed char szTypeName[80];
	}

	[LibraryImport("Shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
	public static partial int SHGetStockIconInfo(int siid, uint uFlags, ref SHSTOCKICONINFO psii);

	[LibraryImport("Shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
	[DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
	public static partial nint SHGetFileInfoW(string pszPath, uint dwFileAttributes, ref SHFILEINFOW psfi, uint cbSizeFileInfo, uint uFlags);

	public static Icon GetShellIcon(int shellIconId)
	{
		var info = new SHSTOCKICONINFO { cbSize = (uint) Marshal.SizeOf<SHSTOCKICONINFO>() };
		return SHGetStockIconInfo(shellIconId, SHGSI_ICON | SHGSI_SMALLICON, ref info) == 0 && info.hIcon != IntPtr.Zero
			? Icon.FromHandle(info.hIcon)
			: SystemIcons.Application;
	}

	public static Icon GetFileIcon(string filePath)
	{
		var shfi = default(SHFILEINFOW);
		return SHGetFileInfoW(filePath, 0, ref shfi, (uint) Marshal.SizeOf(shfi), SHGFI_ICON | SHGFI_SMALLICON) != IntPtr.Zero && shfi.hIcon != IntPtr.Zero
			? Icon.FromHandle(shfi.hIcon)
			: Directory.Exists(filePath)
				? GetShellIcon(SIID_FOLDER)
				: GetShellIcon(SIID_DOCNOASSOC);
	}
}
