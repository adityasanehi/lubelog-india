namespace CarCareTracker.Models
{
    public class SettingsViewModel
    {
        public UserConfig UserConfig { get; set; }
        public List<string> UILanguages { get; set; } = new List<string>();
        public List<string> UIThemes { get; set; } = new List<string>();
    }
}
