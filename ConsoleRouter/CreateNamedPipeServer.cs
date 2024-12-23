using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ConsoleRouter
{
    public static class CreateNamedPipeServer
    {
        public static NamedPipeServerStream CreateStandard(string pipeName, PipeDirection direction, int maxNumberOfServerInstances, PipeTransmissionMode transmissionMode, PipeOptions options)
            => new(pipeName, direction, maxNumberOfServerInstances, transmissionMode, options);

        [SupportedOSPlatform("windows")]
        public static NamedPipeServerStream CreateAcl(string pipeName, PipeDirection direction, int maxNumberOfServerInstances, PipeTransmissionMode transmissionMode, PipeOptions options)
        {
            PipeSecurity pipeSecurity = new();

            pipeSecurity.AddAccessRule
            (
                new PipeAccessRule
                (
                    "Users",
                    PipeAccessRights.ReadWrite,
                    AccessControlType.Allow
                )
            );

            pipeSecurity.AddAccessRule
            (
                new PipeAccessRule
                (
                    WindowsIdentity.GetCurrent().Name,
                    PipeAccessRights.FullControl,
                    AccessControlType.Allow
                )
            );

            pipeSecurity.AddAccessRule
            (
                new PipeAccessRule
                (
                    "SYSTEM", PipeAccessRights.FullControl,
                    AccessControlType.Allow
                )
            );

            return NamedPipeServerStreamAcl.Create(pipeName, direction, maxNumberOfServerInstances, transmissionMode, options, default, default, pipeSecurity);
        }

        public static NamedPipeServerStream Create(string pipeName, PipeDirection direction, int maxNumberOfServerInstances, PipeTransmissionMode transmissionMode, PipeOptions options)
        {
            return
#if WINDOWS
                CreateAcl(pipeName, direction, maxNumberOfServerInstances, transmissionMode, options);
#else
                CreateStandard(pipeName, direction, maxNumberOfServerInstances, transmissionMode, options);
#endif
        }
    }
}
