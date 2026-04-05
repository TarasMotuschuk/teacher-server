using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace StudentAgent.UIHost.DesktopIcons;

internal static class DesktopListView
{
    private const int LVM_FIRST = 0x1000;
    private const int LVM_GETITEMCOUNT = LVM_FIRST + 4;
    private const int LVM_GETITEMPOSITION = LVM_FIRST + 16;
    private const int LVM_SETITEMPOSITION = LVM_FIRST + 15;
    private const int LVM_GETITEMTEXTW = LVM_FIRST + 115;
    private const int LVIF_TEXT = 0x0001;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct LVITEM
    {
        public uint mask;
        public int iItem;
        public int iSubItem;
        public uint state;
        public uint stateMask;
        public IntPtr pszText;
        public int cchTextMax;
        public int iImage;
        public IntPtr lParam;
        public int iIndent;
        public int iGroupId;
        public uint cColumns;
        public IntPtr puColumns;
        public IntPtr piColFmt;
        public int iGroup;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [Flags]
    private enum ProcessAccess : uint
    {
        VmOperation = 0x0008,
        VmRead = 0x0010,
        VmWrite = 0x0020,
        QueryInformation = 0x0400
    }

    public static IReadOnlyList<DesktopIconInfo> CaptureIcons()
    {
        var count = GetItemCount();
        var icons = new List<DesktopIconInfo>(count);
        for (var index = 0; index < count; index++)
        {
            var name = GetItemText(index);
            var position = GetItemPosition(index);
            if (string.IsNullOrWhiteSpace(name) || position is null)
            {
                continue;
            }

            icons.Add(new DesktopIconInfo(name, position.Value.X, position.Value.Y));
        }

        return icons;
    }

    public static int RestoreIcons(IReadOnlyList<DesktopIconInfo> icons)
    {
        var currentNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var count = GetItemCount();
        for (var index = 0; index < count; index++)
        {
            var name = GetItemText(index);
            if (!string.IsNullOrWhiteSpace(name) && !currentNames.ContainsKey(name))
            {
                currentNames[name] = index;
            }
        }

        var restored = 0;
        foreach (var icon in icons)
        {
            if (!currentNames.TryGetValue(icon.Name, out var index))
            {
                continue;
            }

            if (SetItemPosition(index, icon.X, icon.Y))
            {
                restored++;
            }
        }

        return restored;
    }

    private static int GetItemCount()
    {
        var (listViewHandle, explorerHandle) = GetHandlesOrThrow();
        CloseHandle(explorerHandle);
        return (int)SendMessage(listViewHandle, LVM_GETITEMCOUNT, IntPtr.Zero, IntPtr.Zero);
    }

    private static string? GetItemText(int index)
    {
        const int maxChars = 512;
        var textBytes = maxChars * 2;
        var lvSize = Marshal.SizeOf<LVITEM>();

        var (listViewHandle, explorerHandle) = GetHandlesOrThrow();
        var remoteText = IntPtr.Zero;
        var remoteItem = IntPtr.Zero;

        try
        {
            remoteText = VirtualAllocEx(explorerHandle, IntPtr.Zero, (UIntPtr)textBytes, MEM_COMMIT, PAGE_READWRITE);
            remoteItem = VirtualAllocEx(explorerHandle, IntPtr.Zero, (UIntPtr)lvSize, MEM_COMMIT, PAGE_READWRITE);
            if (remoteText == IntPtr.Zero || remoteItem == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx failed.");
            }

            var local = new LVITEM
            {
                mask = LVIF_TEXT,
                iItem = index,
                iSubItem = 0,
                pszText = remoteText,
                cchTextMax = maxChars
            };

            var lvBytes = new byte[lvSize];
            var localPointer = Marshal.AllocHGlobal(lvSize);
            try
            {
                Marshal.StructureToPtr(local, localPointer, fDeleteOld: false);
                Marshal.Copy(localPointer, lvBytes, 0, lvSize);
            }
            finally
            {
                Marshal.FreeHGlobal(localPointer);
            }

            if (!WriteProcessMemory(explorerHandle, remoteItem, lvBytes, (UIntPtr)lvBytes.Length, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "WriteProcessMemory(LVITEM) failed.");
            }

            SendMessage(listViewHandle, LVM_GETITEMTEXTW, new IntPtr(index), remoteItem);

            var textBuffer = new byte[textBytes];
            if (!ReadProcessMemory(explorerHandle, remoteText, textBuffer, (UIntPtr)textBuffer.Length, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ReadProcessMemory(text) failed.");
            }

            return Encoding.Unicode.GetString(textBuffer).TrimEnd('\0');
        }
        catch
        {
            return null;
        }
        finally
        {
            FreeIf(explorerHandle, remoteItem);
            FreeIf(explorerHandle, remoteText);
            CloseHandle(explorerHandle);
        }
    }

    private static (int X, int Y)? GetItemPosition(int index)
    {
        var (listViewHandle, explorerHandle) = GetHandlesOrThrow();
        var remotePoint = IntPtr.Zero;

        try
        {
            var size = Marshal.SizeOf<POINT>();
            remotePoint = VirtualAllocEx(explorerHandle, IntPtr.Zero, (UIntPtr)size, MEM_COMMIT, PAGE_READWRITE);
            if (remotePoint == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "VirtualAllocEx(POINT) failed.");
            }

            SendMessage(listViewHandle, LVM_GETITEMPOSITION, new IntPtr(index), remotePoint);

            var buffer = new byte[size];
            if (!ReadProcessMemory(explorerHandle, remotePoint, buffer, (UIntPtr)buffer.Length, out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "ReadProcessMemory(POINT) failed.");
            }

            var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var point = Marshal.PtrToStructure<POINT>(handle.AddrOfPinnedObject());
                return (point.X, point.Y);
            }
            finally
            {
                handle.Free();
            }
        }
        catch
        {
            return null;
        }
        finally
        {
            FreeIf(explorerHandle, remotePoint);
            CloseHandle(explorerHandle);
        }
    }

    private static bool SetItemPosition(int index, int x, int y)
    {
        var (listViewHandle, explorerHandle) = GetHandlesOrThrow();
        try
        {
            return SendMessage(listViewHandle, LVM_SETITEMPOSITION, new IntPtr(index), MakeLParam(x, y)) != IntPtr.Zero;
        }
        finally
        {
            CloseHandle(explorerHandle);
        }
    }

    private static (IntPtr ListViewHandle, IntPtr ExplorerHandle) GetHandlesOrThrow()
    {
        var listViewHandle = GetDesktopListViewHandle();
        if (listViewHandle == IntPtr.Zero)
        {
            throw new Win32Exception("Desktop ListView not found.");
        }

        GetWindowThreadProcessId(listViewHandle, out var processId);
        var explorerHandle = OpenProcess(
            ProcessAccess.VmOperation | ProcessAccess.VmRead | ProcessAccess.VmWrite | ProcessAccess.QueryInformation,
            false,
            processId);

        if (explorerHandle == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess(explorer) failed.");
        }

        return (listViewHandle, explorerHandle);
    }

    private static IntPtr GetDesktopListViewHandle()
    {
        var progman = FindWindow("Progman", "Program Manager");
        var defView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

        if (defView == IntPtr.Zero)
        {
            var workerWindow = IntPtr.Zero;
            while (true)
            {
                workerWindow = FindWindowEx(IntPtr.Zero, workerWindow, "WorkerW", null);
                if (workerWindow == IntPtr.Zero)
                {
                    break;
                }

                defView = FindWindowEx(workerWindow, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (defView != IntPtr.Zero)
                {
                    break;
                }
            }
        }

        return defView == IntPtr.Zero
            ? IntPtr.Zero
            : FindWindowEx(defView, IntPtr.Zero, "SysListView32", "FolderView");
    }

    private static void FreeIf(IntPtr processHandle, IntPtr memory)
    {
        if (memory != IntPtr.Zero)
        {
            _ = VirtualFreeEx(processHandle, memory, UIntPtr.Zero, MEM_RELEASE);
        }
    }

    private static IntPtr MakeLParam(int low, int high)
        => (IntPtr)((high << 16) | (low & 0xFFFF));

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string? window);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(ProcessAccess access, bool inherit, uint pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr addr, UIntPtr size, uint allocType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr addr, UIntPtr size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr baseAddr, byte[] buffer, UIntPtr size, out UIntPtr read);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr baseAddr, byte[] buffer, UIntPtr size, out UIntPtr written);
}
