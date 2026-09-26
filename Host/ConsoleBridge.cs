using System.Runtime.InteropServices;
using System.Text;

namespace LEAudioRouter.Host;

internal static class ConsoleBridge
{
    private const uint AttachParentProcess = 0xFFFFFFFF;

    public static void AttachToParent()
    {
        if (!AttachConsole(AttachParentProcess))
        {
            return;
        }

        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        Console.SetOut(
            new StreamWriter(
                Console.OpenStandardOutput(),
                Console.OutputEncoding)
            {
                AutoFlush = true
            });

        Console.SetError(
            new StreamWriter(
                Console.OpenStandardError(),
                Console.OutputEncoding)
            {
                AutoFlush = true
            });
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);
}
