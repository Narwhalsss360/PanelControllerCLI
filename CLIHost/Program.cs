using CLIApplication;
using CLIHost;
using PanelController.Controller;
using PanelControllerCLI;
using CLI = PanelControllerCLI.PanelControllerCLI;

Main.Initialize();
Context context = CLI.Initialize(new CLIInterpreter() { InterfaceName = "CLIHost", IgnoreCase = true });
context.Interpreter.Commands.Add(new(Exit));

Persistence.LoadPanels();
Persistence.LoadProfiles();
Persistence.LoadState()?.Apply();
context.Interpreter.Run();

Persistence.SavePanels();
Persistence.SaveProfiles();
Persistence.SaveState();
Main.Deinitialize();

void Exit() => context.Interpreter.Stop();
