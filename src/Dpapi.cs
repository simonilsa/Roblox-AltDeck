using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace RobloxAltManager;

/// <summary>
/// Encrypts saved logins with Windows DPAPI (current-user scope): the data can only be
/// decrypted by the same Windows account on the same PC.
/// </summary>
internal static class Dpapi
{
    [StructLayout(LayoutKind.Sequential)]
    private struct DATA_BLOB
    {
        public int cbData;
        public IntPtr pbData;
    }

    private const int CRYPTPROTECT_UI_FORBIDDEN = 0x1;

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptProtectData(ref DATA_BLOB pDataIn, string? szDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CryptUnprotectData(ref DATA_BLOB pDataIn, IntPtr ppszDataDescr, IntPtr pOptionalEntropy,
        IntPtr pvReserved, IntPtr pPromptStruct, int dwFlags, ref DATA_BLOB pDataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public static string Protect(string plain) =>
        string.IsNullOrEmpty(plain) ? "" : Convert.ToBase64String(Run(Encoding.UTF8.GetBytes(plain), protect: true));

    public static string Unprotect(string base64)
    {
        if (string.IsNullOrEmpty(base64)) return "";
        try { return Encoding.UTF8.GetString(Run(Convert.FromBase64String(base64), protect: false)); }
        catch { return ""; } // e.g. the file was copied from another PC or Windows user
    }

    private static byte[] Run(byte[] input, bool protect)
    {
        var inBlob = new DATA_BLOB { cbData = input.Length, pbData = Marshal.AllocHGlobal(Math.Max(input.Length, 1)) };
        var outBlob = new DATA_BLOB();
        try
        {
            Marshal.Copy(input, 0, inBlob.pbData, input.Length);
            bool ok = protect
                ? CryptProtectData(ref inBlob, null, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob)
                : CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, CRYPTPROTECT_UI_FORBIDDEN, ref outBlob);
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());

            var result = new byte[outBlob.cbData];
            Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(inBlob.pbData);
            if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
        }
    }
}
