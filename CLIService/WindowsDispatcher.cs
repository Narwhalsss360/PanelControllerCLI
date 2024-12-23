#if WINDOWS
using PanelControllerCLI;
using System.Reflection;

namespace CLIService
{
    public static class WindowsDispatcher
    {
        public static Assembly? WindowsBase
        {
            get
            {
                try
                {
                    return Assembly.Load(new AssemblyName("WindowsBase"));
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public static Assembly? PresentationFramework
        {
            get
            {
                try
                {
                    return Assembly.Load(new AssemblyName("PresentationFramework"));
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public static Type? DispatcherType
        {
            get
            {
                try
                {
                    return WindowsBase?.GetType("System.Windows.Threading.Dispatcher");
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }

        public static bool DispatcherAvailable
        {
            get => DispatcherType is not null;
        }

        public static object? CurrentDispatcher
        {
            get => DispatcherType?.GetProperty("CurrentDispatcher")?.GetValue(null);
        }

        private static MethodInfo? _fromThreadMethod = null;

        public static MethodInfo? FromThreadMethod
        {
            get
            {
                if (_fromThreadMethod is null)
                    _fromThreadMethod = DispatcherType?.GetMethod("FromThread", BindingFlags.Static | BindingFlags.Public, [typeof(Thread)]);
                return _fromThreadMethod;
            }
        }

        private static MethodInfo? _invokeActionMethod = null;

        public static MethodInfo? InvokeActionMethod
        {
            get
            {
                if (_invokeActionMethod is null)
                    _invokeActionMethod = DispatcherType?.GetMethod("Invoke", [typeof(Action)]);
                return _invokeActionMethod;
            }
        }

        private static MethodInfo? _invokeFuncMethod = null;

        public static MethodInfo? InvokeFuncMethod
        {
            get
            {
                if (_invokeFuncMethod is null)
                    _invokeFuncMethod = DispatcherType?.GetMethods()
                        .FirstOrDefault(info => info.Name == "Invoke" && info.IsGenericMethod && info.GetParameters().Length == 1);
                return _invokeFuncMethod;
            }
        }

        private static object? _staDispatcher = null;

        private static object? STADispatcher
        {
            get
            {
#if WINDOWS
                if (_sta.ThreadState == ThreadState.Unstarted)
                {
                    _sta.SetApartmentState(ApartmentState.STA);
                    _sta.Start();
                    Task getTask = Task.Run(async () => { while (_staDispatcher is null) await Task.Delay(20); });
                    if (!getTask.Wait(1000))
                        return null;
                }
#endif
                return _staDispatcher;
            }
        }

        private static Thread _sta = new(() => { _staDispatcher = CurrentDispatcher; DispatcherType?.GetMethod("Run")?.Invoke(null, null); }) { Name = "STA thread for WPF extensions." };

        public static void Invoke(Action action, object? dispatcher = null)
        {
            dispatcher ??= STADispatcher;
            if (dispatcher is null)
                throw new InvalidOperationException("There is no dispatcher");
            if (InvokeActionMethod is not MethodInfo invoke)
                throw new InvalidOperationException("There is no InvokeActionMethod");

            invoke.Invoke(dispatcher, [action]);
        }

        public static T Invoke<T>(Func<T> function, object? dispatcher = null)
        {
            dispatcher ??= STADispatcher;
            if (dispatcher is null)
                throw new InvalidOperationException("There is no dispatcher");
            if (InvokeFuncMethod is not MethodInfo genericInvoke)
                throw new InvalidOperationException("There is no InvokeFuncMethod");

            PanelControllerCLI.PanelControllerCLI.CurrentContext.Interpreter.Out.WriteLine("Note: Creating a window requires to not be installed as service");
            return (T)genericInvoke.MakeGenericMethod([typeof(T)]).Invoke(dispatcher, [function])!;
        }
    }
}
#endif
