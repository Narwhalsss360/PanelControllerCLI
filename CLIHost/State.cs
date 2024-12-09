using PanelController.Controller;
using PanelController.Profiling;

namespace CLIHost
{
    public class State
    {
        public string SelectedProfileName { get; set; } = "";
    
        public static State Current()
        {
            return new State()
            {
                SelectedProfileName = Main.CurrentProfile?.Name ?? ""
            };
        }

        public void Apply()
        {
            if (Main.Profiles.Find(profile => profile.Name == SelectedProfileName) is Profile profile)
                Main.CurrentProfile = profile;
        }
    }
}
