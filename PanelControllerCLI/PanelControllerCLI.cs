using System.ComponentModel;
using PanelController.Controller;
using PanelController.Profiling;
using CLIApplication;
using PanelController.PanelObjects;
using System.Reflection;
using PanelController.PanelObjects.Properties;
using NStreamCom;
using System.Text;

namespace PanelControllerCLI
{
    using Controller = PanelController.Controller;

    public static class PanelControllerCLI
    {
        private static readonly CLIInterpreter.Command[] _commandDelegates = [
            new(Create.Generic),
            new(Create.Channel),
            new(Create.Profile),
            new(Create.Mapping),
            new(Create.MappedObject),
            new(Create.PanelInfo),
            new(Select.Generic),
            new(Select.Profile),
            new(Select.Mapping),
            new(Select.MappedObject),
            new(Select.Panel),
            new(Select.Back),
            new(Edit.Name),
            new(Edit.Property),
            new(Edit.Collection),
            new(Edit.CollectionOrder),
            new(Show.Extensions),
            new(Show.Generic),
            new(Show.Channel),
            new(Show.Profile),
            new(Show.Mapping),
            new(Show.MappedObject),
            new(Show.PanelInfo),
            new(Show.Properties),
            new(Show.Selected),
            new(Show.Logs),
            new(Use.Profile),
            new(Use.Extension),
            new(Delete.Generic),
            new(Delete.Profile),
            new(Delete.Mapping),
            new(Delete.MappedObject),
            new(Delete.PanelInfo),
            new(VirtualPanel.Initialize),
            new(VirtualPanel.SendStroke),
            new(VirtualPanel.SetAnalogValue),
            new(VirtualPanel.Display),
            new(VirtualPanel.Deinitialize)
        ];

        private static Context? _context = new(new CLIInterpreter());

        public static Context CurrentContext
        {
            get
            {
                if (_context is null)
                    throw new NotImplementedException();
                return _context;
            }
        }

        public static Context Initialize(CLIInterpreter? interpreter = null)
        {
            _context ??= new(interpreter ?? new CLIInterpreter());
            _context.Interpreter.Commands.AddRange(_commandDelegates);
            Extensions.Load<OutputToConsole>();
            return _context;
        }

        public static List<Func<object, Func<string>?>> SingleLineCustomTypeFormatters = new()
        {
            { obj => ReferenceEquals(obj, Main.Profiles) ? () => "Profiles List" : null },
            { obj => ReferenceEquals(obj, Main.PanelsInfo) ? () => "Panels List" : null },
            { obj => ReferenceEquals(obj, Extensions.Objects) ? () => "Generics List" : null },
            { obj => obj.GetType().IsAssignableTo(typeof(IList<Mapping.MappedObject>)) ? () => "MappedObjects List" : null },
            { obj => obj is Mapping.MappedObject mapped ? (() => $"{mapped.Object.GetType().Name} {mapped.Object.GetItemName()}") : null },
            { obj => obj is Profile profile ? (Main.CurrentProfile == profile ? () => $">{profile.Name}<" : () => profile.Name ) : null }
        };

        public static string FormatSingleLine(this object? @object)
        {
            if (@object is null)
                return "null";

            Func<string>? generate = null;
            foreach (Func<object, Func<string>?> formatter in SingleLineCustomTypeFormatters)
            {
                if (formatter(@object) is Func<string> generator)
                {
                    generate = generator;
                    break;
                }           
            }

            if (generate is null)
                return $"{@object.GetType().Name} {@object}";
            return generate();
        }

        public static string FormatMultiLine(this object? @object) => $"{@object}";

        public static T Ask<T>(Func<string, T> parser)
        {
            Console.Write("(use .cancel to cancel)");
            string entry = Console.ReadLine() ?? "";

            if (entry == ".cancel")
                throw new UserCancelException();

            try
            {
                return parser(entry);
            }
            catch (UserEntryParseException exc)
            {
                if (exc.AllowRetry)
                    return Ask<T>(parser);
                throw new UserEntryParseException(false);
            }
        }

        public static T SelectFrom<T>(this IList<T> list)
        {
            if (list.Count == 0)
                throw new EmptyCollectionException();

            Console.WriteLine("Select index:");
            for (int i = 0; i < list.Count; i++)
                Console.WriteLine($"{i}: {FormatSingleLine(list[i])}");

            int index = Ask<int>((s) =>
            {
                if (!int.TryParse(s, out int value))
                    throw new UserEntryParseException(true);
                return value;
            });

            return list[index];
        }

        public static T FindOne<T>(this IList<T> list, Predicate<T> predicate, out int index)
        {
            if (list.Count == 0)
            {
                throw new EmptyCollectionException();
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (!predicate(list[i]))
                    continue;

                for (int j = i + 1; j < list.Count; j++)
                {
                    if (predicate(list[i]))
                        throw new MoreThanOneMatchException();
                }

                index = i;
                return list[i];
            }

            throw new NotFoundException();
        }

        public static T FindOne<T>(this IList<T> list, Predicate<T> predicate)
        {
            return FindOne<T>(list, predicate, out int _);
        }

        public static Type? FindType(this string type)
        {
            Type? shortName = null;
            foreach (Type extension in Extensions.AllExtensions)
            {
                if (extension.Name == type)
                {
                    if (shortName is not null)
                        throw new NotImplementedException(null, new MoreThanOneMatchException());
                    shortName = extension;
                }
                else if (extension.FullName == type)
                {
                    return extension;
                }
            }
            return shortName;
        }

        public static string[] ParamsToStrings(object[] @params) => Array.ConvertAll(@params, param => param.ToString() ?? "");

        public static IPanelObject Instantiate(this Type type, string[] arguments)
        {
            if (!type.Implements<IPanelObject>())
                throw new NotImplementedException(null, new InvalidProgramException());

            ConstructorInfo? ctor = type.GetUserConstructor();

            if (ctor is null && arguments.Length != 0)
                throw new NotImplementedException("Please enter no arguments",  new NonConstructableException());

            if (Activator.CreateInstance(type, ctor is null ? [] : ctor.GetParameters().ParseArguments(arguments)) is not IPanelObject @object)
                throw new NotImplementedException(null, new InvalidProgramException());
            return @object;
        }

        public static Profile? GetContextualProfile()
        {
            if (CurrentContext.SelectedObject is Profile selected)
            {
                return selected;
            }

            return Main.CurrentProfile;
        }

        public static IPanelObject RequireSelectionAsPanelObject()
        {
            if (CurrentContext.SelectedObject is IPanelObject @object)
                return @object;
            if (CurrentContext.SelectedObject is Mapping.MappedObject mapped)
                return mapped.Object;
            throw new NotImplementedException(null, new MissingSelectionException());
        }

        public static class Create
        {
            [DisplayName("Create-Generic")]
            public static void Generic(string typeName, string[]? flags = null, params object[] constructArguments)
            {
                Type type;
                try
                {
                    if (typeName.FindType() is not Type found)
                        throw new NotImplementedException();
                    type = found;
                }
                catch (MoreThanOneMatchException)
                {
                    throw new NotImplementedException();
                }

                IPanelObject @object;
                try
                {
                    @object = Instantiate(type, ParamsToStrings(constructArguments));
                }
                catch (NonConstructableException)
                {
                    throw new NotImplementedException();
                }
                catch (ArgumentException)
                {
                    throw new NotImplementedException();
                }

                Extensions.Objects.Add(@object);

                if (flags?.Contains("--select") ?? false)
                {
                    CurrentContext.SetNewSelectionStack(
                        Extensions.Objects,
                        Context.SelectionKey(Extensions.Objects.Count),
                        @object
                    );
                }
            }

            [DisplayName("Create-Channel")]
            public static void Channel(string typeName, string[]? flags = null, params object[] constructArguments)
            {
                Type type;
                try
                {
                    if (typeName.FindType() is not Type found)
                        throw new NotImplementedException();
                    type = found;
                }
                catch (MoreThanOneMatchException)
                {
                    throw new NotImplementedException();
                }

                IChannel channel;
                try
                {
                    if (Instantiate(type, ParamsToStrings(constructArguments)) is not IChannel asChannel)
                        throw new NotImplementedException();
                    channel = asChannel;
                }
                catch (NonConstructableException)
                {
                    throw new NotImplementedException();
                }
                catch (ArgumentException)
                {
                    throw new NotImplementedException();
                }

                if (flags?.Contains("--wait-for-handshake") ?? false)
                    Main.Handshake(channel);
                else
                    Main.HandshakeAsync(channel);
            }

            [DisplayName("Create-Profile")]
            public static void Profile(string name, string[]? flags = null)
            {
                Profile newProfile = new() { Name = name };
                Main.Profiles.Add(newProfile);

                if (flags?.Contains("--select") ?? false)
                {
                    CurrentContext.SetNewSelectionStack(
                        Main.Profiles,
                        Context.SelectionKey(Main.Profiles.IndexOf(newProfile)),
                        newProfile
                    );
                }

                if (flags?.Contains("--use") ?? false)
                    Main.CurrentProfile = newProfile;
            }

            [DisplayName("Create-Mapping")]
            public static void Mapping(string name, string panel, InterfaceTypes interfaceType, uint interfaceID, string[]? flags = null)
            {
                if (GetContextualProfile() is not Profile profile)
                    throw new NotImplementedException(null, new MissingSelectionException());

                Guid guid;

                try
                {
                    guid = Main.PanelsInfo.FindOne(info => info.Name == panel).PanelGuid;
                }
                catch (MoreThanOneMatchException)
                {
                    throw new NotImplementedException();
                }
                catch (NotFoundException)
                {
                    throw new NotImplementedException();
                }

                Mapping newMapping = new()
                {
                    Name = name,
                    PanelGuid = guid,
                    InterfaceType = interfaceType,
                    InterfaceID = interfaceID,
                    InterfaceOption = interfaceType == InterfaceTypes.Digital ? flags?.Contains("--on-activate") : null
                };

                profile.AddMapping(newMapping);

                if (flags?.Contains("--select") ?? false)
                {
                    CurrentContext.SetNewSelectionStack(
                        profile,
                        newMapping
                    );
                }
            }

            [DisplayName("Create-MappedObject")]
            public static void MappedObject(string typeName, string[]? flags = null, params object[] constructArguments)
            {
                if (CurrentContext.SelectedObject is not Mapping mapping)
                    throw new NotImplementedException(null, new MissingSelectionException());

                Type type;
                try
                {
                    if (typeName.FindType() is not Type found)
                        throw new NotImplementedException();
                    type = found;
                }
                catch (MoreThanOneMatchException)
                {
                    throw new NotImplementedException();
                }

                Mapping.MappedObject newMappedObject;
                try
                {
                    newMappedObject = new Mapping.MappedObject()
                    { Object = Instantiate(type, ParamsToStrings(constructArguments)) };   
                }
                catch (NonConstructableException)
                {
                    throw new NotImplementedException();
                }
                catch (ArgumentException)
                {
                    throw new NotImplementedException();
                }

                mapping.Objects.Add(newMappedObject);

                if (flags?.Contains("--select") ?? false)
                {
                    CurrentContext.SelectedInnerCollectionAndItem(mapping.Objects, mapping.Objects.Count - 1, newMappedObject);
                }
            }

            [DisplayName("Create-PanelInfo")]
            public static void PanelInfo(string[]? flags = null)
            {
                throw new NotImplementedException();
            }
        }

        public static class Select
        {
            [DisplayName("Select-Generic")]
            public static void Generic(string identifier, string[]? flags = null)
            {
                IPanelObject generic;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= Extensions.Objects.Count)
                        throw new NotImplementedException();
                    generic = Extensions.Objects[index];
                }
                else
                {
                    try
                    {
                        generic = Extensions.Objects.FindOne(ext => ext.GetItemName() == identifier, out index);
                    }
                    catch (EmptyCollectionException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (MoreThanOneMatchException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (NotFoundException)
                    {
                        throw new NotImplementedException();
                    }
                }

                CurrentContext.SetNewSelectionStack(
                    Extensions.Objects,
                    Context.SelectionKey(index),
                    generic
                );
            }

            [DisplayName("Select-Profile")]
            public static void Profile(string name)
            {
                try
                {
                    Profile profile = Main.Profiles.FindOne(profile => profile.Name == name, out int index);
                    CurrentContext.SetNewSelectionStack(
                        Main.Profiles,
                        Context.SelectionKey(index),
                        profile
                    );
                }
                catch (EmptyCollectionException)
                {
                    throw new NotImplementedException();
                }
                catch (MoreThanOneMatchException)
                {
                    throw new NotImplementedException();
                }
                catch (NotFoundException)
                {
                    throw new NotImplementedException();
                }
            }

            [DisplayName("Select-Mapping")]
            public static void Mapping(string identifier, string[]? flags = null)
            {
                if (GetContextualProfile() is not Profile profile)
                    throw new NotImplementedException(null, new MissingSelectionException());

                Mapping mapping;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    Mapping[] mappings = profile.Mappings;
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= mappings.Length)
                        throw new NotImplementedException();
                    mapping = mappings[index];
                }
                else
                {
                    try
                    {
                        mapping = profile.Mappings.FindOne(mapping => mapping.Name == identifier, out index);
                    }
                    catch (EmptyCollectionException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (MoreThanOneMatchException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (NotFoundException)
                    {
                        throw new NotImplementedException();
                    }
                }


                CurrentContext.SetNewSelectionStack(
                    Main.Profiles,
                    Context.SelectionKey(Main.Profiles.IndexOf(profile)),
                    profile,
                    mapping
                );
            }

            [DisplayName("Select-MappedObject")]
            public static void MappedObject(string identifier, string[]? flags = null)
            {
                if (CurrentContext.SelectedObject is not Mapping mapping)
                    throw new NotImplementedException(null, new MissingSelectionException());

                Mapping.MappedObject mapped;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= mapping.Objects.Count)
                        throw new NotImplementedException();
                    mapped = mapping.Objects[index];
                }
                else
                {
                    try
                    {
                        mapped = mapping.Objects.FindOne(mapping => mapping.Object.GetItemName() == identifier, out index);
                    }
                    catch (EmptyCollectionException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (MoreThanOneMatchException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (NotFoundException)
                    {
                        throw new NotImplementedException();
                    }
                }

                CurrentContext.SelectedInnerCollectionAndItem(
                    mapping.Objects,
                    index,
                    mapped
                );
            }

            [DisplayName("Select-Panel")]
            public static void Panel(string identifier, string[]? flags = null)
            {
                PanelInfo panelInfo;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= Main.PanelsInfo.Count)
                        throw new NotImplementedException();
                    panelInfo = Main.PanelsInfo[index];
                }
                else
                {
                    try
                    {
                        panelInfo = Main.PanelsInfo.FindOne(info => info.Name == identifier, out index);
                    }
                    catch (EmptyCollectionException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (MoreThanOneMatchException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (NotFoundException)
                    {
                        throw new NotImplementedException();
                    }
                }

                CurrentContext.SetNewSelectionStack(
                    Main.PanelsInfo,
                    Context.SelectionKey(index),
                    panelInfo
                );
            }

            [DisplayName("Select-Back")]
            public static void Back() => CurrentContext.SelectedBack();
        }

        public static class Edit
        {
            [DisplayName("Edit-Name")]
            public static void Name(string name)
            {
                if (CurrentContext.SelectedObject is IPanelObject panelObject)
                {
                    if (!panelObject.TrySetItemName(name))
                        throw new NotImplementedException();
                }
                else if (CurrentContext.SelectedObject is Profile profile)
                {
                    profile.Name = name;
                }
                else if (CurrentContext.SelectedObject is Mapping mapping)
                {
                    mapping.Name = name;
                }
                else if (CurrentContext.SelectedObject is Mapping.MappedObject mapped)
                {
                    CurrentContext.SelectedInnerProperty(mapped.Object);
                    Name(name);
                    CurrentContext.SelectedBack();
                }
                else if (CurrentContext.SelectedObject is PanelInfo panel)
                {
                    panel.Name = name;
                }
                else
                {
                    throw new NotImplementedException(null, new MissingSelectionException());
                }
            }

            [DisplayName("Edit-Property")]
            public static void Property(string property, string value)
            {
                IPanelObject @object = RequireSelectionAsPanelObject();
                PropertyInfo propInfo;
                try
                {
                    propInfo = @object.GetUserProperties().FindOne(prop => prop.Name == property);
                }
                catch (EmptyCollectionException)
                {
                    throw new NotImplementedException();
                }
                catch (MoreThanOneMatchException)
                {
                    throw new NotImplementedException();
                }
                catch (NotFoundException)
                {
                    throw new NotImplementedException();
                }

                if (!ParameterInfoExtensions.IsSupported(propInfo.PropertyType))
                    throw new NotImplementedException();

                if (value.ParseAs(propInfo.PropertyType) is not object parsed)
                    throw new NotImplementedException();

                propInfo.SetValue(@object, parsed);
            }

            [DisplayName("Edit-Collection")]
            public static void Collection(string property, string key, string value)
            {
                if (CurrentContext.SelectedObject is not IPanelObject @object)
                    throw new NotImplementedException();

                PropertyInfo propInfo;
                try
                {
                    propInfo = @object.GetUserProperties().FindOne(prop => prop.Name == property);
                }
                catch (EmptyCollectionException)
                {
                    throw new NotImplementedException();
                }
                catch (MoreThanOneMatchException)
                {
                    throw new NotImplementedException();
                }
                catch (NotFoundException)
                {
                    throw new NotImplementedException();
                }
                throw new NotImplementedException();
            }

            [DisplayName("Edit-CollectionOrder")]
            public static void CollectionOrder(string property, string keyA, string keyB)
            {
                if (CurrentContext.SelectedObject is not IPanelObject @object)
                    throw new NotImplementedException();

                PropertyInfo propInfo;
                try
                {
                    propInfo = @object.GetUserProperties().FindOne(prop => prop.Name == property);
                }
                catch (EmptyCollectionException)
                {
                    throw new NotImplementedException();
                }
                catch (MoreThanOneMatchException)
                {
                    throw new NotImplementedException();
                }
                catch (NotFoundException)
                {
                    throw new NotImplementedException();
                }
                throw new NotImplementedException();
            }
        }

        public static class Show
        {
            [DisplayName("Show-Extensions")]
            public static void Extensions()
            {
                CurrentContext.Interpreter.Out.WriteLine("Extensions:");
                foreach (Extensions.ExtensionCategories category in Enum.GetValues<Extensions.ExtensionCategories>())
                {
                    if (Controller.Extensions.ExtensionsByCategory[category].Count == 0)
                        continue;
                    CurrentContext.Interpreter.Out.WriteLine("\t" + category.ToString());
                    foreach (Type type in Controller.Extensions.ExtensionsByCategory[category])
                        CurrentContext.Interpreter.Out.WriteLine("\t\t" + FormatSingleLine(type));
                }
            }

            [DisplayName("Show-Generics")]
            public static void Generic()
            {
                if (Controller.Extensions.Objects.Count == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Generic Objects");
                foreach (IPanelObject @object in Controller.Extensions.Objects)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(@object));
            }

            [DisplayName("Show-Channels")]
            public static void Channel()
            {
                if (Main.ConnectedPanels.Count == 0)
                    return;
                CurrentContext.Interpreter.Out.WriteLine("Channels:");
                foreach (ConnectedPanel connected in Main.ConnectedPanels)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(connected));
            }

            [DisplayName("Show-Profiles")]
            public static void Profile()
            {
                if (Main.Profiles.Count == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Profiles:");
                foreach (Profile profile in Main.Profiles)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(profile));
            }

            [DisplayName("Show-Mappings")]
            public static void Mapping()
            {
                if (GetContextualProfile() is not Profile profile)
                    throw new NotImplementedException(null, new MissingSelectionException());

                if (profile.Mappings.Length == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Mappings:");
                foreach (Mapping mapping in profile.Mappings)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(mapping));
            }

            [DisplayName("Show-MappedObjects")]
            public static void MappedObject()
            {
                if (CurrentContext.SelectedObject is not Mapping mapping)
                    throw new NotImplementedException(null, new MissingSelectionException());

                if (mapping.Objects.Count == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Mapped Objects:");
                foreach (Mapping.MappedObject mapped in mapping.Objects)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(mapped));
            }

            [DisplayName("Show-PanelInfos")]
            public static void PanelInfo()
            {
                if (Main.PanelsInfo.Count == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Panel Infos:");
                foreach (PanelInfo info in Main.PanelsInfo)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(info));
            }

            [DisplayName("Show-Properties")]
            public static void Properties()
            {
                IPanelObject @object = RequireSelectionAsPanelObject();

                Dictionary<PropertyInfo, object?> properties = @object.GetAllPropertiesValues();
                if (properties.Count == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine($"{@object.GetItemName()}:");
                foreach (KeyValuePair<PropertyInfo, object?> pair in properties)
                    CurrentContext.Interpreter.Out.WriteLine($"\t{pair.Key.Name}: {pair.Value}");
            }

            [DisplayName("Show-Selected")]
            public static void Selected()
            {
                TextWriter Out = CurrentContext.Interpreter.Out;
                object?[] currentSelections = CurrentContext.CurrentSelectionStack();
                int depth = 0;
                for (int i = currentSelections.Length - 1; i >= 0; i--)
                {
                    object? current = currentSelections[i];
                    if (depth == 0)
                    {
                        Out.WriteLine(FormatSingleLine(current));
                        depth++;
                        continue;
                    }
                    else
                    {
                        Out.Write("|");
                    }

                    for (int j = 0; true; j++)
                    {
                        if (j == depth)
                        {
                            Out.Write(' ');
                            break;
                        }
                        Out.Write('-');
                    }

                    if (Context.IsContainerKey(current))
                    {
                        Out.Write($"[{FormatSingleLine(Context.GetContainerKey(current ?? ""))}]: ");
                        i--;
                        current = currentSelections[i];
                    }

                    Out.WriteLine(FormatSingleLine(current));
                    depth++;
                }
            }

            [DisplayName("Show-Logs")]
            public static void Logs(Logger.Levels maximumLevel = Logger.Levels.Debug, string format = "/T [/L][/F] /M")
            {
                foreach (Logger.HistoricalLog log in Logger.Logs)
                    if (log.Level <= maximumLevel)
                        CurrentContext.Interpreter.Out.WriteLine(log.ToString(format));
            }
        }

        public static class Use
        {
            [DisplayName("Use-Profile")]
            public static void Profile(string name)
            {
                try
                {
                    Main.CurrentProfile = Main.Profiles.FindOne(profile => profile.Name == name);
                }
                catch (EmptyCollectionException)
                {
                    throw new NotImplementedException();
                }
                catch (MoreThanOneMatchException)
                {
                    throw new NotImplementedException();
                }
                catch (NotFoundException)
                {
                    throw new NotImplementedException();
                }
            }
        
            public static void Extension(string path)
            {
                static void LoadAssemblyFromPath(string filePath)
                {
                    Assembly assembly;
                    try
                    {
                        assembly = Assembly.LoadFrom(filePath);
                    }
                    catch (BadImageFormatException)
                    {
                        throw new NotImplementedException();
                    }
                    Extensions.Load(assembly);
                }

                if (Directory.Exists(path))
                {
                    foreach (string file in Directory.GetFiles(path))
                        LoadAssemblyFromPath(file);
                }
                else if (File.Exists(path))
                {
                    LoadAssemblyFromPath(path);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }
        }

        public static class Delete
        {
            [DisplayName("Delete-Generic")]
            public static void Generic(string identifier, string[]? flags = null)
            {
                int index;
                IPanelObject @object;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= Extensions.Objects.Count)
                        throw new NotImplementedException();
                    @object = Extensions.Objects[index];
                }
                else
                {
                    try
                    {
                        @object = Extensions.Objects.FindOne(ext => ext.GetItemName() == identifier, out index);
                    }
                    catch (EmptyCollectionException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (MoreThanOneMatchException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (NotFoundException)
                    {
                        throw new NotImplementedException();
                    }
                }

                Extensions.Objects.RemoveAt(index);
                int stepsBack = CurrentContext.StepsBack(@object);
                while (stepsBack >= 0)
                    CurrentContext.SelectedBack();
            }

            [DisplayName("Delete-Profile")]
            public static void Profile(string identifier, string[]? flags = null)
            {
                int index;
                Profile profile;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= Main.Profiles.Count)
                        throw new NotImplementedException();
                    profile = Main.Profiles[index];
                }
                else
                {
                    try
                    {
                        profile = Main.Profiles.FindOne(profile => profile.Name == identifier, out index);
                    }
                    catch (EmptyCollectionException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (MoreThanOneMatchException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (NotFoundException)
                    {
                        throw new NotImplementedException();
                    }
                }

                if (Main.SelectedProfileIndex == index)
                    Main.SelectedProfileIndex = -1;
                Main.Profiles.RemoveAt(index);
                int stepsBack = CurrentContext.StepsBack(profile);
                while (stepsBack >= 0)
                    CurrentContext.SelectedBack();
            }

            [DisplayName("Delete-Mapping")]
            public static void Mapping(string identifier, string[]? flags = null)
            {
                if (GetContextualProfile() is not Profile profile)
                    throw new NotImplementedException(null, new MissingSelectionException());

                Mapping mapping;
                if (flags?.Contains("--index") ?? false)
                {
                    Mapping[] mappings = profile.Mappings;
                    if (!int.TryParse(identifier, out int index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= mappings.Length)
                        throw new NotImplementedException();
                    mapping = mappings[index];
                }
                else
                {
                    try
                    {
                        mapping = profile.Mappings.FindOne(mapping => mapping.Name == identifier);
                    }
                    catch (EmptyCollectionException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (MoreThanOneMatchException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (NotFoundException)
                    {
                        throw new NotImplementedException();
                    }
                }

                profile.RemoveMapping(mapping);
                int stepsBack = CurrentContext.StepsBack(mapping);
                while (stepsBack >= 0)
                    CurrentContext.SelectedBack();
            }

            [DisplayName("Delete-MappedObject")]
            public static void MappedObject(string identifier, string[]? flags = null)
            {
                if (CurrentContext.SelectedObject is not Mapping mapping)
                    throw new NotImplementedException(null, new MissingSelectionException());

                int index;
                Mapping.MappedObject mapped;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= mapping.Objects.Count)
                        throw new NotImplementedException();
                    mapped = mapping.Objects[index];
                }
                else
                {
                    try
                    {
                        mapped = mapping.Objects.FindOne(mapped => mapped.Object.GetItemName() == identifier, out index);
                    }
                    catch (EmptyCollectionException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (MoreThanOneMatchException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (NotFoundException)
                    {
                        throw new NotImplementedException();
                    }
                }

                mapping.Objects.RemoveAt(index);
                int stepsBack = CurrentContext.StepsBack(mapping);
                while (stepsBack >= 0)
                    CurrentContext.SelectedBack();
            }

            [DisplayName("Delete-PanelInfo")]
            public static void PanelInfo(string identifier, string[]? flags = null)
            {
                int index;
                PanelInfo info;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= Main.PanelsInfo.Count)
                        throw new NotImplementedException();
                    info = Main.PanelsInfo[index];
                }
                else
                {
                    try
                    {
                        info = Main.PanelsInfo.FindOne(info => info.Name == identifier, out index);
                    }
                    catch (EmptyCollectionException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (MoreThanOneMatchException)
                    {
                        throw new NotImplementedException();
                    }
                    catch (NotFoundException)
                    {
                        throw new NotImplementedException();
                    }
                }

                Main.PanelsInfo.RemoveAt(index);
                int stepsBack = CurrentContext.StepsBack(info);
                while (stepsBack >= 0)
                    CurrentContext.SelectedBack();
            }
        }
    
        public static class VirtualPanel
        {
            private static readonly Guid VirtualPanelGuid = new([228, 67, 132, 24, 63, 182, 20, 64, 167, 143, 248, 141, 250, 253, 118, 38]);

            private class VirtualChannel : IChannel
            {
                public PanelInfo PanelInfo;

                private bool _opened = false;

                private readonly PacketCollector _collector = new();

                public VirtualChannel(PanelInfo info)
                {
                    PanelInfo = info;
                    _collector.PacketsReady += PacketsReady;
                }

                public bool IsOpen => _opened;

                public event EventHandler<byte[]>? BytesReceived;

                public void Close()
                {
                    _opened = false;
                }

                public object? Open()
                {
                    _opened = true;
                    _collector.Discard();
                    return null;
                }

                public void SendInterfaceUpdate(InterfaceTypes interfaceType, uint id, object? state = null)
                {
                    ushort messageID = 0xFFFF;
                    byte[] data;
                    switch (interfaceType)
                    {
                        case InterfaceTypes.Digital:
                            if (state is not bool activated)
                                throw new NotImplementedException();
                            messageID = (int)ConnectedPanel.ReceiveIDs.DigitalStateUpdate;
                            data = new byte[5];
                            BitConverter.GetBytes(id).CopyTo(data, 0);
                            data[4] = (byte)(activated ? 1 : 0);
                            break;
                        case InterfaceTypes.Analog:
                            if (state is not string value)
                                throw new NotImplementedException();
                            messageID = (int)ConnectedPanel.ReceiveIDs.AnalogStateUpdate;
                            data = Encoding.UTF8.GetBytes(value);
                            break;
                        default:
                            data = [];
                            break;
                    }
                    if (messageID == 0xFFFF)
                        return;
                    data = new Message(messageID, data).GetPackets((ushort)data.Length)[0].GetStreamBytes();
                    BytesReceived?.Invoke(this, data);
                }

                private void SendHandshake()
                {
                    byte[] data = new byte[28];
                    PanelInfo.PanelGuid.ToByteArray().CopyTo(data, 0);
                    BitConverter.GetBytes(PanelInfo.DigitalCount).CopyTo(data, 16);
                    BitConverter.GetBytes(PanelInfo.AnalogCount).CopyTo(data, 20);
                    BitConverter.GetBytes(PanelInfo.DisplayCount).CopyTo(data, 24);
                    BytesReceived?.Invoke(this, data);
                }

                private void PacketsReady(object sender, PacketsReadyEventArgs e)
                {
                    Message message = new(e.Packets);
                    switch ((ConnectedPanel.ReceiveIDs)message.ID)
                    {
                        case ConnectedPanel.ReceiveIDs.Handshake:
                            SendHandshake();
                            break;
                        default:
                            break;
                    }
                }

                private void VirtualPanelReceive(byte[] data)
                {
                    _collector.Collect(data);
                }

                public object? Send(byte[] data)
                {
                    VirtualPanelReceive(data);
                    return null;
                }
            }

            private static VirtualChannel? _channel = null;

            [DisplayName("Virtual-Initialize")]
            public static void Initialize(string name, uint digitalCount, uint analogCount, uint displayCount)
            {
                Deinitialize();
                _channel = new(
                    new()
                    {
                        DigitalCount = digitalCount,
                        AnalogCount = analogCount,
                        DisplayCount = displayCount,
                        PanelGuid = VirtualPanelGuid
                    }
                );

                Main.Handshake(_channel);
                if (Main.ConnectedPanels.Find(connected => connected.PanelGuid == _channel.PanelInfo.PanelGuid) is not ConnectedPanel connected)
                    throw new NotImplementedException();
                int index = Main.PanelsInfo.IndexOf(_channel.PanelInfo);
                Main.PanelsInfo[index].Name = name;
            }

            public static void UseExisting()
            {
                if (_channel is not null)
                    throw new NotImplementedException();

                if (Main.PanelsInfo.Find(panel => panel.PanelGuid == VirtualPanelGuid) is not PanelInfo info)
                    throw new NotImplementedException();
                Main.PanelsInfo.Remove(info);
                Initialize(info.Name, info.DigitalCount, info.AnalogCount, info.DisplayCount);
            }

            [DisplayName("Virtual-SendStroke")]
            public static void SendStroke(uint id)
            {
                if (_channel is null)
                    throw new NotImplementedException();
                if (_channel.PanelInfo.DigitalCount <= id)
                    throw new NotImplementedException();
                _channel.SendInterfaceUpdate(InterfaceTypes.Digital, id, true);
                _channel.SendInterfaceUpdate(InterfaceTypes.Digital, id, false);
            }

            [DisplayName("Virtual-Set")]
            public static void SetAnalogValue(uint id, string value)
            {
                if (_channel is null)
                    throw new NotImplementedException();
                if (_channel.PanelInfo.DigitalCount <= id)
                    throw new NotImplementedException();
                _channel.SendInterfaceUpdate(InterfaceTypes.Analog, id, value);
            }

            [DisplayName("Virtual-Display")]
            public static void Display(uint id)
            {
                if (_channel is null)
                    throw new NotImplementedException();
                if (_channel.PanelInfo.DigitalCount <= id)
                    throw new NotImplementedException();
                throw new NotImplementedException();
            }

            [DisplayName("Virtual-Deinitialize")]
            public static void Deinitialize()
            {
                if (_channel is not null)
                {
                    if (Main.ConnectedPanels.Find(connected => connected.Channel == _channel) is ConnectedPanel connected)
                    {
                        Main.ConnectedPanels.Remove(connected);
                        _channel.Close();
                    }

                    if (Main.PanelsInfo.Find(info => info.PanelGuid == _channel.PanelInfo.PanelGuid) is PanelInfo info)
                    {
                        Main.PanelsInfo.Remove(info);
                    }

                    _channel = null;
                }
            }
        }
    }
}
