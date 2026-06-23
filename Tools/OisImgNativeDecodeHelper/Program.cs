using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    private const int PngBufferCapacity = 16 * 1024 * 1024;

    private static int Main(string[] args)
    {
        try
        {
            string sdk = Arg(args, "--sdk") ?? @"D:\OISV3\Plug-ins\Plugin_WeKnow\sdk";
            string img = Arg(args, "--img") ?? string.Empty;
            string outDir = Arg(args, "--out") ?? Environment.CurrentDirectory;

            if (!File.Exists(img))
            {
                Console.Error.WriteLine("IMG file not found.");
                return 2;
            }

            string dllPath = Path.Combine(sdk, "img2png.dll");
            if (!File.Exists(dllPath))
            {
                Console.Error.WriteLine("img2png.dll not found.");
                return 3;
            }

            Directory.CreateDirectory(outDir);
            Environment.SetEnvironmentVariable("PATH", sdk + Path.PathSeparator + (Environment.GetEnvironmentVariable("PATH") ?? string.Empty));
            Native.SetDllDirectory(sdk);

            IntPtr dll = Native.LoadLibrary(dllPath);
            if (dll == IntPtr.Zero)
            {
                Console.Error.WriteLine("LoadLibrary failed: " + Marshal.GetLastWin32Error());
                return 4;
            }

            string logDir = Path.Combine(outDir, "log");
            Directory.CreateDirectory(logDir);

            int init = Get<InitCdecl>(dll, "IMG2PNG_Init")(logDir, 0, 0);
            if (init == 0)
            {
                Console.Error.WriteLine("IMG2PNG_Init failed.");
                return 5;
            }

            byte[] imgBytes = File.ReadAllBytes(img);
            IntPtr imgData = Marshal.AllocHGlobal(imgBytes.Length);
            try
            {
                Marshal.Copy(imgBytes, 0, imgData, imgBytes.Length);

                IntPtr outCount = Marshal.AllocHGlobal(4);
                IntPtr outItems = Marshal.AllocHGlobal(64 * 4);
                try
                {
                    Marshal.WriteInt32(outCount, 0);
                    Zero(outItems, 64 * 4);

                    int setImg = Get<SetImgCdecl>(dll, "IMG2PNG_set_img")(img, imgData, imgBytes.Length, outCount, outItems);
                    if (setImg == 0)
                    {
                        Console.Error.WriteLine("IMG2PNG_set_img failed.");
                        return 6;
                    }

                    int viewCount = Math.Clamp(Marshal.ReadInt32(outCount), 0, 8);
                    int exported = 0;
                    for (int view = 0; view < Math.Max(1, viewCount); view++)
                    {
                        string pngPath = Path.Combine(outDir, $"view{view}.png");
                        int pngLength = ExportPngView(dll, view, pngPath);
                        if (pngLength > 0)
                        {
                            Console.WriteLine($"view{view}={pngPath}|{pngLength}");
                            exported++;
                        }
                    }

                    return exported > 0 ? 0 : 7;
                }
                finally
                {
                    Marshal.FreeHGlobal(outCount);
                    Marshal.FreeHGlobal(outItems);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(imgData);
                TryUninit(dll);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static int ExportPngView(IntPtr dll, int view, string pngPath)
    {
        IntPtr buffer = Marshal.AllocHGlobal(PngBufferCapacity);
        IntPtr a = Marshal.AllocHGlobal(4);
        IntPtr b = Marshal.AllocHGlobal(4);
        IntPtr c = Marshal.AllocHGlobal(4);
        IntPtr d = Marshal.AllocHGlobal(4);
        IntPtr e = Marshal.AllocHGlobal(4);

        try
        {
            Zero(buffer, PngBufferCapacity);
            Marshal.WriteInt32(a, 0);
            Marshal.WriteInt32(b, 0);
            Marshal.WriteInt32(c, 0);
            Marshal.WriteInt32(d, 0);
            Marshal.WriteInt32(e, 0);

            Get<GetPngCdecl>(dll, "IMG2PNG_get_png")(buffer, a, b, c, d, e, view);

            byte[] data = new byte[PngBufferCapacity];
            Marshal.Copy(buffer, data, 0, data.Length);
            int pngLength = FindPngLength(data);
            if (pngLength <= 0)
                return 0;

            File.WriteAllBytes(pngPath, data.AsSpan(0, pngLength).ToArray());
            return pngLength;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
            Marshal.FreeHGlobal(a);
            Marshal.FreeHGlobal(b);
            Marshal.FreeHGlobal(c);
            Marshal.FreeHGlobal(d);
            Marshal.FreeHGlobal(e);
        }
    }

    private static int FindPngLength(byte[] data)
    {
        byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        for (int i = 0; i < sig.Length; i++)
        {
            if (data[i] != sig[i])
                return 0;
        }

        for (int i = 8; i + 12 <= data.Length;)
        {
            int len = (data[i] << 24) | (data[i + 1] << 16) | (data[i + 2] << 8) | data[i + 3];
            if (len < 0 || i + 12 + len > data.Length)
                return 0;

            string type = Encoding.ASCII.GetString(data, i + 4, 4);
            i += 12 + len;
            if (type == "IEND")
                return i;
        }

        return 0;
    }

    private static T Get<T>(IntPtr dll, string name) where T : Delegate
    {
        IntPtr ptr = Native.GetProcAddress(dll, name);
        if (ptr == IntPtr.Zero)
            throw new MissingMethodException(name);

        return Marshal.GetDelegateForFunctionPointer<T>(ptr);
    }

    private static void TryUninit(IntPtr dll)
    {
        try
        {
            IntPtr ptr = Native.GetProcAddress(dll, "IMG2PNG_UNInit");
            if (ptr != IntPtr.Zero)
                Marshal.GetDelegateForFunctionPointer<UninitCdecl>(ptr)();
        }
        catch
        {
        }
    }

    private static void Zero(IntPtr ptr, int bytes)
    {
        byte[] zero = new byte[Math.Min(bytes, 1024 * 1024)];
        int written = 0;
        while (written < bytes)
        {
            int count = Math.Min(zero.Length, bytes - written);
            Marshal.Copy(zero, 0, IntPtr.Add(ptr, written), count);
            written += count;
        }
    }

    private static string? Arg(string[] args, string key)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(key, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }

        return null;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int InitCdecl(string logDir, int imgType, int modeType);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private delegate int SetImgCdecl(string imgName, IntPtr imgData, int imgLength, IntPtr outCount, IntPtr outItems);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void GetPngCdecl(IntPtr buffer, IntPtr outA, IntPtr outB, IntPtr outC, IntPtr outD, IntPtr outE, int view);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void UninitCdecl();
}

internal static class Native
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool SetDllDirectory(string lpPathName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    internal static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
}
