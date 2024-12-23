#if WINDOWS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace CLIService
{
    internal class WindowUtility
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_HIDE = 0;
        const int SW_SHOW = 5;

        public static IntPtr ConsoleHandle
        {
            get => GetConsoleWindow();
        }

        public static void ShowServiceWindow() => ShowWindow(ConsoleHandle, SW_SHOW);

        public static void HideServiceWindow() => ShowWindow(ConsoleHandle, SW_HIDE);
    }
}
#endif
