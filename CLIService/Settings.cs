using PanelController.Controller;
using PanelController.Profiling;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CLIService
{
    public class Settings
    {
        public static string CWD = new FileInfo(Environment.ProcessPath ?? "").Directory?.FullName ?? Environment.CurrentDirectory;


        public static readonly string SettingsFile = Path.Combine(CWD, "Settings.json");

        public string ExtensionsFolder { get; set; } = Path.Combine(CWD, "Extensions");

        public string SavesFolder { get; set; } = Path.Combine(CWD, "Persistent");

        public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            IncludeFields = true
        };

        private Profile? _currentProfile;

        public string? CurrentProfileName
        {
            get
            {
                _currentProfile = Main.CurrentProfile;
                return _currentProfile?.Name;
            }
            init
            {
                if (Main.Profiles.Find(profile => profile.Name == value) is not Profile profile)
                    return;
                Main.CurrentProfile = profile;
                _currentProfile = profile;
            }
        }

        private string PanelsFile { get => Path.Combine(SavesFolder, "Panels.json"); }

        private string ProfilesFile { get => Path.Combine(SavesFolder, "Profiles.json"); }

        public static Settings Load()
        {
            if (!File.Exists(SettingsFile))
                return new();
            using FileStream file = File.Open(SettingsFile, FileMode.Open);
            return JsonSerializer.Deserialize<Settings>(file) ?? new();
        }

        public void LoadExtensions()
        {
            if (!Directory.Exists(ExtensionsFolder))
                return;

            foreach (string file in Directory.GetFiles(ExtensionsFolder))
            {
                try
                {
                    Extensions.Load(Assembly.LoadFrom(file));
                }
                catch (Exception exc)
                {
                    Console.Error.WriteLine(exc.ToString());
                }
            }
        }

        public void LoadPanels()
        {
            if (!File.Exists(PanelsFile))
                return;
            using FileStream panelsFile = File.OpenRead(PanelsFile);
            foreach (PanelInfo info in JsonSerializer.Deserialize<PanelInfo[]>(panelsFile, SerializerOptions) ?? [])
                Main.PanelsInfo.Add(info);
        }

        public static void ParsePropertiesOf(Mapping.SerializableMapping.MappedObjectSerializable serializableObject, JsonSerializerOptions options)
        {
            if (Array.Find(Extensions.AllExtensions, extension => extension.FullName == serializableObject.FullName) is not Type type)
                return;

            for (int i = 0; i < serializableObject.Properties.Length; i++)
            {
                var serializableProperty = serializableObject.Properties[i];
                if (serializableProperty.Value is not JsonElement element)
                    continue;
                if (type.GetProperty(serializableProperty.Name) is not PropertyInfo info)
                    throw new NotImplementedException();
                serializableObject.Properties[i].Value = element.Deserialize(JsonTypeInfo.CreateJsonTypeInfo(info.PropertyType, options));
            }
        }

        public void LoadProfiles()
        {
            if (!File.Exists(ProfilesFile))
                return;
            using FileStream profilesFile = File.OpenRead(ProfilesFile);
            foreach (Profile.SerializableProfile profile in JsonSerializer.Deserialize<Profile.SerializableProfile[]>(profilesFile, SerializerOptions) ?? [])
            {
                foreach (var mapped in profile.Mappings)
                    foreach (var @object in mapped.Objects)
                        ParsePropertiesOf(@object, SerializerOptions);
                Main.Profiles.Add(new(profile));
            }
        }

        public void EnsurePersistentFolder()
        {
            if (!Directory.Exists(SavesFolder))
                Directory.CreateDirectory(SavesFolder);
        }

        public void SavePanels()
        {
            EnsurePersistentFolder();
            using FileStream panelsFile = File.Create(PanelsFile);
            using StreamWriter writer = new(panelsFile);
            writer.Write(JsonSerializer.Serialize(Main.PanelsInfo, SerializerOptions));
        }

        public void SaveProfiles()
        {
            EnsurePersistentFolder();
            using FileStream profilesFile = File.Create(ProfilesFile);
            using StreamWriter writer = new(profilesFile);
            Profile.SerializableProfile[] serializables = Main.Profiles.Select(profile => new Profile.SerializableProfile(profile)).ToArray();
            writer.Write(JsonSerializer.Serialize(serializables, SerializerOptions));
        }

        public void SaveSettings()
        {
            using FileStream settingsFile = File.Create(SettingsFile);
            using StreamWriter writer = new(settingsFile);
            writer.Write(JsonSerializer.Serialize(this, SerializerOptions));
        }
    }
}
