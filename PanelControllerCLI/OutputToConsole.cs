using PanelController.PanelObjects;
using PanelController.PanelObjects.Properties;
using PanelControllerCLI.CLIFatalExceptions;

namespace PanelControllerCLI
{
    public class OutputToConsole : IPanelAction
    {
        private static readonly string CSI = "\x1B[";
        
        private static readonly string SAVE_CURRENT_CURSOR_POSITION = "s";
        
        private static readonly string SCROLL_DOWN = "S";
        
        private static readonly string CURSOR_UP = "A";
        
        private static readonly string INSERT_NEW_LINE = "L";
        
        private static readonly string RESTORE_SAVED_CURRENT_CURSOR_POSITION = "u";

        private TextWriter Out
        {
            get
            {
                try
                {
                    return PanelControllerCLI.CurrentContext.Interpreter.Out;
                }
                catch (UninitializedContextException)
                {
                    return TextWriter.Null;
                }
            }
        }

        [ItemName]
        public string Name { get; set; } = "";

        [UserProperty]
        public string Message { get; set; }

        [UserConstructor("The `message` argument corresponds to what would be printed when this action runs.")]
        public OutputToConsole(string message)
        {
            Message = message;
            if (Name == "")
                Name = $"Output: {message}";
        }

        public OutputToConsole()
            : this("")
        {
        }

        private void SendANSI(string str, int repeat = 1)
        {
            for (int i = 0; i < repeat; i++)
            {
                Out.Write(CSI + str);
                Out.Flush();
            }
        }

        private void PrintAbove(string str)
        {
            if (str.EndsWith('\n'))
                str.Remove(str.Length - 1, 1);

            int lineCount = 1;
            foreach (char c in str)
                if (c == '\n')
                    lineCount++;

            SendANSI(SAVE_CURRENT_CURSOR_POSITION);
            SendANSI(SCROLL_DOWN, lineCount);
            SendANSI(CURSOR_UP, lineCount);
            SendANSI(INSERT_NEW_LINE, lineCount);
            Out.Write(str);
            SendANSI(RESTORE_SAVED_CURRENT_CURSOR_POSITION);
        }

        public object? Run()
        {
            if (Message == "")
                return "OutputToConsole: Cannot have empty message string";
            PrintAbove(Message);
            return null;
        }
    }
}
