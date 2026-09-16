using System.Runtime.InteropServices;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Services;

public sealed class DemoWindowEnumerationService
{
    public List<DemoWindowInfo> GetTopLevelWindows()
    {
        if (OperatingSystem.IsWindows())
        {
            return GetWindowsTopLevelWindows();
        }

        if (OperatingSystem.IsMacOS())
        {
            return GetMacOsTopLevelWindows();
        }

        return [];
    }

    private static List<DemoWindowInfo> GetWindowsTopLevelWindows()
    {
        var results = new List<DemoWindowInfo>(256);
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd))
            {
                return true;
            }

            var length = GetWindowTextLengthW(hwnd);
            if (length <= 0 || length > 512)
            {
                return true;
            }

            var sb = new char[length + 1];
            var copied = GetWindowTextW(hwnd, sb, sb.Length);
            if (copied <= 0)
            {
                return true;
            }

            var title = new string(sb, 0, copied).Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            results.Add(new DemoWindowInfo(hwnd, title));
            return true;
        }, 0);

        return results
            .DistinctBy(w => w.PlatformWindowId)
            .OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<DemoWindowInfo> GetMacOsTopLevelWindows()
    {
        var results = new List<DemoWindowInfo>(256);

        var array = CGWindowListCopyWindowInfo(
            CGWindowListOption.OnScreenOnly | CGWindowListOption.ExcludeDesktopElements,
            0);
        if (array == IntPtr.Zero)
        {
            return results;
        }

        try
        {
            var count = (int)CFArrayGetCount(array);
            for (var i = 0; i < count; i++)
            {
                var dict = CFArrayGetValueAtIndex(array, i);
                if (dict == IntPtr.Zero)
                {
                    continue;
                }

                var windowId = GetCfDictInt64(dict, "kCGWindowNumber");
                if (windowId <= 0)
                {
                    continue;
                }

                var ownerName = GetCfDictString(dict, "kCGWindowOwnerName");
                var name = GetCfDictString(dict, "kCGWindowName");
                var title = string.IsNullOrWhiteSpace(name)
                    ? ownerName
                    : $"{ownerName} — {name}";
                title = title?.Trim();

                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                results.Add(new DemoWindowInfo(windowId, title, ownerName));
            }
        }
        finally
        {
            CFRelease(array);
        }

        return results
            .DistinctBy(w => w.PlatformWindowId)
            .OrderBy(w => w.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? GetCfDictString(IntPtr dict, string key)
    {
        var keyStr = CFStringCreateWithCString(IntPtr.Zero, key, 0x08000100 /*kCFStringEncodingUTF8*/);
        if (keyStr == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            var val = CFDictionaryGetValue(dict, keyStr);
            if (val == IntPtr.Zero)
            {
                return null;
            }

            var len = CFStringGetLength(val);
            if (len <= 0)
            {
                return null;
            }

            var maxBytes = CFStringGetMaximumSizeForEncoding(len, 0x08000100) + 1;
            var buf = Marshal.AllocHGlobal(maxBytes);
            try
            {
                if (!CFStringGetCString(val, buf, maxBytes, 0x08000100))
                {
                    return null;
                }
                return Marshal.PtrToStringUTF8(buf);
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        finally
        {
            CFRelease(keyStr);
        }
    }

    private static long GetCfDictInt64(IntPtr dict, string key)
    {
        var keyStr = CFStringCreateWithCString(IntPtr.Zero, key, 0x08000100 /*kCFStringEncodingUTF8*/);
        if (keyStr == IntPtr.Zero)
        {
            return 0;
        }

        try
        {
            var val = CFDictionaryGetValue(dict, keyStr);
            if (val == IntPtr.Zero)
            {
                return 0;
            }

            long outVal = 0;
            return CFNumberGetValue(val, 4 /*kCFNumberSInt64Type*/, ref outVal) ? outVal : 0;
        }
        finally
        {
            CFRelease(keyStr);
        }
    }

    private delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLengthW(nint hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint hWnd, char[] lpString, int nMaxCount);

    [Flags]
    private enum CGWindowListOption : uint
    {
        OnScreenOnly = 1 << 0,
        ExcludeDesktopElements = 1 << 4,
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern IntPtr CGWindowListCopyWindowInfo(CGWindowListOption option, uint relativeToWindow);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFArrayGetCount(IntPtr array);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFArrayGetValueAtIndex(IntPtr array, nint index);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDictionaryGetValue(IntPtr dict, IntPtr key);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, string cStr, int encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFStringGetLength(IntPtr str);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern int CFStringGetMaximumSizeForEncoding(nint length, int encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(IntPtr str, IntPtr buffer, int bufferSize, int encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFNumberGetValue(IntPtr number, int theType, ref long valuePtr);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);
}

