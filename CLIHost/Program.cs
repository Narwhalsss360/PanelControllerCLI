using CLIApplication;
using CLIHost;
using PanelController.Controller;
using PanelControllerCLI;
using PanelControllerCLI.CLIFatalExceptions;
using PanelControllerCLI.DataErrorExceptions;
using PanelControllerCLI.UserErrorExceptions;
using System.Reflection;
using CLI = PanelControllerCLI.PanelControllerCLI;

Main.Initialize();
Context context = CLI.Initialize(new CLIInterpreter() { InterfaceName = "CLIHost", IgnoreCase = true });
context.Interpreter.Commands.Add(new(Exit));

Persistence.LoadPanels();
Persistence.LoadProfiles();
Persistence.LoadState()?.Apply();

while (true)
{
    try
    {
        context.Interpreter.Run();
        break;
    }
    catch (Exception exc)
    {
        context.Interpreter.Error.WriteLine("An error has occurred.");
        if (ProcessTargetException(exc))
        {
            context.Interpreter.Out.WriteLine("Gracefully exiting.");
            break;
        }
    }
}


Persistence.SavePanels();
Persistence.SaveProfiles();
Persistence.SaveState();
Main.Deinitialize();

bool ProcessTargetException(Exception exception)
{
    if (exception is CLIFatalException fatal)
    {
        string message = $"Fatal error occurred({fatal.GetType().Name}): {fatal.Message}";
        Logger.Log(message, Logger.Levels.Error, "PanelControllerCLI-Host");
        context.Interpreter.Error.WriteLine(message);
        context.Interpreter.Error.WriteLine($"Trace:\n{fatal.StackTrace}");
        return true;
    }
    else if (exception is UserErrorException userError)
    {
        context.Interpreter.Error.WriteLine($"{userError.GetType().Name}: {userError.Message}");
    }
    else if (exception is DataErrorException dataError)
    {
        context.Interpreter.Error.WriteLine($"{dataError.GetType().Name}: {dataError.Message}");
    }
    else if (exception is UserCancelException)
    {
        context.Interpreter.Out.WriteLine("Canceled.");
    }
    else if (exception is NotImplementedException exc)
    {
        context.Interpreter.Error.WriteLine($"The requested operation is not implemented ({exc.TargetSite?.DeclaringType}.{exc.TargetSite?.Name}). {exc.Message}");
    }
    else if (exception is TargetInvocationException target)
    {
        if (target.InnerException is null)
            throw new InvalidProgramException("Unkown exception occurred.", target);
        return ProcessTargetException(target.InnerException);
    }
    else
    {
        context.Interpreter.Error.WriteLine($"An exception occurred of type {exception.GetType().Name}:{exception.Message}\nTrace:\n{exception.StackTrace}");
        return true;
    }

    return false;
}

void Exit() => context.Interpreter.Stop();
