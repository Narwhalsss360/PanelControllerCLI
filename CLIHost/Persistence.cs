using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using PanelController.Controller;
using PanelController.Profiling;

namespace CLIHost
{
    public static class Persistence
    {
        public static readonly string SaveFilesExtensions = ".json";

        public static readonly string ExtensionsFolder = "Extensions";

        public static readonly string PersistentFolder = "Persistent";

        public static readonly string PanelsFile = Path.Combine(PersistentFolder, "Panels" + SaveFilesExtensions);

        public static readonly string ProfilesFile = Path.Combine(PersistentFolder, "Profiles" + SaveFilesExtensions);

        public static readonly string StateFile = Path.Combine(PersistentFolder, "State" + SaveFilesExtensions);

        public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            IncludeFields = true
        };

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

        public static void LoadExtensions()
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

        public static void LoadPanels()
        {
            if (!File.Exists(PanelsFile))
                return;
            using FileStream panelsFile = File.OpenRead(PanelsFile);
            foreach (PanelInfo info in JsonSerializer.Deserialize<PanelInfo[]>(panelsFile, SerializerOptions) ?? [])
                Main.PanelsInfo.Add(info);
        }

        public static void LoadProfiles()
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

        public static State? LoadState()
        {
            if (!File.Exists(StateFile))
                return null;
            using FileStream stateFile = File.OpenRead(StateFile);
            return JsonSerializer.Deserialize<State>(stateFile, SerializerOptions);
        }

        public static void EnsurePersistentFolder()
        {
            if (!Directory.Exists(PersistentFolder))
                Directory.CreateDirectory(PersistentFolder);
        }

        public static void SavePanels()
        {
            EnsurePersistentFolder();
            using FileStream panelsFile = File.Create(PanelsFile);
            using StreamWriter writer = new(panelsFile);
            writer.Write(JsonSerializer.Serialize(Main.PanelsInfo, SerializerOptions));
        }

        public static void SaveProfiles()
        {
            EnsurePersistentFolder();
            using FileStream profilesFile = File.Create(ProfilesFile);
            using StreamWriter writer = new(profilesFile);
            Profile.SerializableProfile[] serializables = Main.Profiles.Select(profile => new Profile.SerializableProfile(profile)).ToArray();
            writer.Write(JsonSerializer.Serialize(serializables, SerializerOptions));
        }
    
        public static void SaveState()
        {
            EnsurePersistentFolder();
            using FileStream stateFile = File.Create(StateFile);
            using StreamWriter writer = new(stateFile);
            writer.Write(JsonSerializer.Serialize(State.Current(), SerializerOptions));
        }
    }
}
