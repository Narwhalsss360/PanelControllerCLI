using System.ComponentModel;
using PanelController.Controller;
using PanelController.Profiling;
using CLIApplication;
using PanelController.PanelObjects;
using System.Reflection;
using PanelController.PanelObjects.Properties;
using NStreamCom;
using System.Text;
using PanelControllerCLI.CLIFatalExceptions;
using PanelControllerCLI.UserErrorExceptions;
using PanelControllerCLI.DataErrorExceptions;

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
            new(VirtualPanel.Deinitialize),
            new(Help.CommandHelp),
            new(Help.ConstructorHelp),
            new(Help.TypeHelp),
            new(Help.ExtensionHelp)
        ];

        private static Context? _context = null;

        public static Context CurrentContext
        {
            get
            {
                if (_context is null)
                    throw new UninitializedContextException("The current context for PanelControllerCLI is not initialized.");
                return _context;
            }
        }

        public static Context Initialize(CLIInterpreter interpreter)
        {
            if (_context is not null)
                throw new AlreadyInitializedException("PanelControllerCLI is already initialized", new InvalidProgramException());
            _context = new(interpreter);
            _context.Interpreter.Commands.AddRange(_commandDelegates);
            Extensions.Load<OutputToConsole>();
            return _context;
        }

        public static List<Func<object, Func<string>?>> SingleLineCustomTypeFormatters { get; set; } = new()
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
                throw new UserEntryParseException("Parse error, see cause.", false, exc);
            }
        }

        public static T SelectFrom<T>(this IList<T> list)
        {
            if (list.Count == 0)
                throw new EmptyCollectionException("Collection was empty.");

            Console.WriteLine("Select index:");
            for (int i = 0; i < list.Count; i++)
                Console.WriteLine($"{i}: {FormatSingleLine(list[i])}");

            int index = Ask<int>((s) =>
            {
                if (!int.TryParse(s, out int value))
                    throw new UserEntryParseException(typeof(int), s, true);
                return value;
            });

            return list[index];
        }

        public static T FindOne<T>(this IList<T> list, Predicate<T> predicate, out int index)
        {
            if (list.Count == 0)
                throw new EmptyCollectionException("Collection was empty");

            for (int i = 0; i < list.Count; i++)
            {
                if (!predicate(list[i]))
                    continue;

                for (int j = i + 1; j < list.Count; j++)
                {
                    if (predicate(list[i]))
                        throw new MoreThanOneMatchException("More than one element matched the predicate.");
                }

                index = i;
                return list[i];
            }

            throw new NotFoundException("No element matched the predicate.");
        }

        public static T FindOne<T>(this IList<T> list, Predicate<T> predicate)
        {
            return FindOne<T>(list, predicate, out int _);
        }

        public static Type? FindType(this string type)
        {
            Type? fromShortName = null;
            foreach (Type extension in Extensions.AllExtensions)
            {
                if (extension.Name == type)
                {
                    if (fromShortName is not null)
                        throw new NameCollisionException($"More than one type matched the name {type}");
                    fromShortName = extension;
                }
                else if (extension.FullName == type)
                {
                    return extension;
                }
            }
            return fromShortName;
        }

        public static string[] ParamsToStrings(object[] @params) => Array.ConvertAll(@params, param => param.ToString() ?? "");

        public static IPanelObject Instantiate(this Type type, string[] arguments)
        {
            if (!type.Implements<IPanelObject>())
                throw new UnsupportedTypeException(type, "Constructing");

            if (type.GetUserConstructor() is not ConstructorInfo ctor)
                throw new NonConstructableException(type, typeof(IPanelObject), "No user constructor");

            object?[] parsed;
            try
            {
                parsed = ctor.GetParameters().ParseArguments(arguments);
            }
            catch (ArgumentException exc)
            {
                throw new UserEntryParseException($"An error occured parsing constructor arguments for {type.Name}", false, exc);
            }

            if (Activator.CreateInstance(type, parsed) is not IPanelObject @object)
                throw new UnsupportedTypeException($"The type {type.Name} did not construct to a IPanelObject", new InvalidProgramException("'type' should be verified to implement IPanelObject"));
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
            throw new MissingSelectionException(typeof(IPanelObject));
        }

        public static class Create
        {
            [DisplayName("Create-Generic")]
            [Description("Create a generic IPanelObject object, and supply it's arguments. IPanelObjects are general extensions that do not fit into any pre-defined categories. Use --select flag to select.")]
            public static void Generic(string typeName, string[]? flags = null, params object[] constructArguments)
            {
                if (typeName.FindType() is not Type type)
                    throw new NotFoundException(typeName, "Extensions");

                IPanelObject @object = Instantiate(type, ParamsToStrings(constructArguments));
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
            [Description("Create (open) a channel, and supply it's arguments. Use --wait-for-handshake to make terminal wait.")]
            public static void Channel(string typeName, string[]? flags = null, params object[] constructArguments)
            {
                if (typeName.FindType() is not Type type)
                    throw new NotFoundException(typeName, "Extensions");

                object? instantiated = Instantiate(type, ParamsToStrings(constructArguments));
                if (instantiated is not IChannel channel)
                    throw new WrongTypeException(instantiated.GetType(), typeof(IChannel));

                if (flags?.Contains("--wait-for-handshake") ?? false)
                {
                    Main.Handshake(channel);
                }
                else
                {
                    Main.HandshakeAsync(channel).ContinueWith(task =>
                    {
                        if (task.Exception is not null)
                            throw new CLIFatalException("An exception was thrown during handshake", task.Exception);
                    });
                }
            }

            [DisplayName("Create-Profile")]
            [Description("Create a profile with specified name. Use --select flag to select.")]
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
            [Description("Create a mapping in the *current contextual profile*. Use --select flag to select.")]
            public static void Mapping(string name, string panel, InterfaceTypes interfaceType, uint interfaceID, string[]? flags = null)
            {
                if (GetContextualProfile() is not Profile profile)
                    throw new MissingSelectionException(typeof(Profile));

                Guid guid = Main.PanelsInfo.FindOne(info => info.Name == panel).PanelGuid;

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
            [Description("Create a MappedObject in the currently *selected* Mapping, and supply it's arguments. Use --select flag to select.")]
            public static void MappedObject(string typeName, string[]? flags = null, params object[] constructArguments)
            {
                if (CurrentContext.SelectedObject is not Mapping mapping)
                    throw new MissingSelectionException(typeof(Mapping));

                if (typeName.FindType() is not Type type)
                    throw new NotFoundException(typeName, "Extensions");

                Mapping.MappedObject newMappedObject = new()
                {
                    Object = Instantiate(type, ParamsToStrings(constructArguments))
                };

                mapping.Objects.Add(newMappedObject);

                if (flags?.Contains("--select") ?? false)
                    CurrentContext.SelectedInnerCollectionAndItem(mapping.Objects, mapping.Objects.Count - 1, newMappedObject);
            }

            [DisplayName("Create-PanelInfo")]
            [Description("Not implemented...")]
            public static void PanelInfo(string[]? flags = null)
            {
                throw new NotImplementedException();
            }
        }

        public static class Select
        {
            [DisplayName("Select-Generic")]
            [Description("Select generic by name/index (IPanelObject) object. Use --index flag to identify by index.")]
            public static void Generic(string identifier, string[]? flags = null)
            {
                IPanelObject generic;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new UserEntryParseException(typeof(int), identifier, false);
                    if (index < 0 || index >= Extensions.Objects.Count)
                        throw new OutOfBoundsException(index, Extensions.Objects.Count);
                    generic = Extensions.Objects[index];
                }
                else
                {
                    try
                    {
                        generic = Extensions.Objects.FindOne(ext => ext.GetItemName() == identifier, out index);
                    }
                    catch (MoreThanOneMatchException exc)
                    {
                        throw new NameCollisionException(typeof(IPanelObject), identifier, "Generics", exc);
                    }
                    catch (NotFoundException exc)
                    {
                        throw new NotFoundException(identifier, "Generics", exc);
                    }
                }

                CurrentContext.SetNewSelectionStack(
                    Extensions.Objects,
                    Context.SelectionKey(index),
                    generic
                );
            }

            [DisplayName("Select-Profile")]
            [Description("Select profile by name.")]
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
                catch (MoreThanOneMatchException exc)
                {
                    throw new NameCollisionException(typeof(Profile), name, "Profiles", exc);
                }
                catch (NotFoundException exc)
                {
                    throw new NotFoundException(name, "Profiles", exc);
                }
            }

            [DisplayName("Select-Mapping")]
            [Description("Select Mapping by name/index in *current contextual profile*. Use --index flag to identify by index.")]
            public static void Mapping(string identifier, string[]? flags = null)
            {
                if (GetContextualProfile() is not Profile profile)
                    throw new MissingSelectionException(typeof(Profile));

                Mapping mapping;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    Mapping[] mappings = profile.Mappings;
                    if (!int.TryParse(identifier, out index))
                        throw new UserEntryParseException(typeof(int), identifier, false);
                    if (index < 0 || index >= mappings.Length)
                        throw new OutOfBoundsException(index, mappings.Length);
                    mapping = mappings[index];
                }
                else
                {
                    try
                    {
                        mapping = profile.Mappings.FindOne(mapping => mapping.Name == identifier, out index);
                    }
                    catch (MoreThanOneMatchException exc)
                    {
                        throw new NameCollisionException(typeof(Mapping), identifier, "Mappings", exc);
                    }
                    catch (NotFoundException exc)
                    {
                        throw new NotFoundException(identifier, "Mappings", exc);
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
            [Description("Select MappedObject by name/index in current *selected* Mapping. Use --index flag to identify by index.")]
            public static void MappedObject(string identifier, string[]? flags = null)
            {
                if (CurrentContext.SelectedObject is not Mapping mapping)
                    throw new MissingSelectionException(typeof(Mapping));

                Mapping.MappedObject mapped;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new UserEntryParseException(typeof(int), identifier, false);
                    if (index < 0 || index >= mapping.Objects.Count)
                        throw new OutOfBoundsException(index, mapping.Objects.Count);
                    mapped = mapping.Objects[index];
                }
                else
                {
                    try
                    {
                        mapped = mapping.Objects.FindOne(mapping => mapping.Object.GetItemName() == identifier, out index);
                    }
                    catch (MoreThanOneMatchException exc)
                    {
                        throw new NameCollisionException(typeof(Mapping.MappedObject), identifier, "MappedObjects", exc);
                    }
                    catch (NotFoundException exc)
                    {
                        throw new NotFoundException(identifier, "MappedObjects", exc);
                    }
                }

                CurrentContext.SelectedInnerCollectionAndItem(
                    mapping.Objects,
                    index,
                    mapped
                );
            }

            [DisplayName("Select-Panel")]
            [Description("Select Panel by name/index. Use --index flag to identify by index.")]
            public static void Panel(string identifier, string[]? flags = null)
            {
                PanelInfo panelInfo;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new UserEntryParseException(typeof(int), identifier, false);
                    if (index < 0 || index >= Main.PanelsInfo.Count)
                        throw new OutOfBoundsException(index, Main.PanelsInfo.Count);
                    panelInfo = Main.PanelsInfo[index];
                }
                else
                {
                    try
                    {
                        panelInfo = Main.PanelsInfo.FindOne(info => info.Name == identifier, out index);
                    }
                    catch (MoreThanOneMatchException exc)
                    {
                        throw new NameCollisionException(typeof(PanelInfo), identifier, "PanelInfos", exc);
                    }
                    catch (NotFoundException exc)
                    {
                        throw new NotFoundException(identifier, "PanelInfos", exc);
                    }
                }

                CurrentContext.SetNewSelectionStack(
                    Main.PanelsInfo,
                    Context.SelectionKey(index),
                    panelInfo
                );
            }

            [DisplayName("Select-Back")]
            [Description("Deselect currently selected, and select the containing object/collection.")]
            public static void Back() => CurrentContext.SelectedBack();
        }

        public static class Edit
        {
            [DisplayName("Edit-Name")]
            [Description("Edit name of currently *selected* object.")]
            public static void Name(string name)
            {
                if (CurrentContext.SelectedObject is null)
                    throw new MissingSelectionException("Nothing is selected.");

                if (CurrentContext.SelectedObject is IPanelObject panelObject)
                {
                    if (!panelObject.TrySetItemName(name))
                        throw new NotNamableException(panelObject.GetType());
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
                    throw new NotNamableException(CurrentContext.SelectedObject.GetType());
                }
            }

            [DisplayName("Edit-Property")]
            [Description("Edit a property of currently *selected* IPanelObject.")]
            public static void Property(string property, string value)
            {
                IPanelObject @object = RequireSelectionAsPanelObject();
                PropertyInfo propInfo;
                try
                {
                    propInfo = @object.GetUserProperties().FindOne(prop => prop.Name == property);
                }
                catch (MoreThanOneMatchException exc)
                {
                    throw new NameCollisionException(typeof(PropertyInfo), property, "Properties", exc);
                }
                catch (NotFoundException exc)
                {
                    throw new NotFoundException(property, "Properties", exc);
                }

                if (!ParameterInfoExtensions.IsSupported(propInfo.PropertyType))
                    throw new UnsupportedTypeException(propInfo.PropertyType, "Property Editting");

                if (value.ParseAs(propInfo.PropertyType) is not object parsed)
                    throw new UserEntryParseException(propInfo.PropertyType, value, false);

                propInfo.SetValue(@object, parsed);
            }

            [DisplayName("Edit-Collection")]
            [Description("Not implemented")]
            public static void Collection(string property, string key, string value)
            {
                IPanelObject @object = RequireSelectionAsPanelObject();
                PropertyInfo propInfo;
                try
                {
                    propInfo = @object.GetUserProperties().FindOne(prop => prop.Name == property);
                }
                catch (MoreThanOneMatchException exc)
                {
                    throw new NameCollisionException(typeof(PropertyInfo), property, "Properties", exc);
                }
                catch (NotFoundException exc)
                {
                    throw new NotFoundException(property, "Properties", exc);
                }

                throw new NotImplementedException();
            }

            [DisplayName("Edit-CollectionOrder")]
            [Description("Not implemented")]
            public static void CollectionOrder(string property, string keyA, string keyB)
            {
                IPanelObject @object = RequireSelectionAsPanelObject();
                PropertyInfo propInfo;
                try
                {
                    propInfo = @object.GetUserProperties().FindOne(prop => prop.Name == property);
                }
                catch (MoreThanOneMatchException exc)
                {
                    throw new NameCollisionException(typeof(PropertyInfo), property, "Properties", exc);
                }
                catch (NotFoundException exc)
                {
                    throw new NotFoundException(property, "Properties", exc);
                }

                throw new NotImplementedException();
            }
        }

        public static class Show
        {
            [DisplayName("Show-Extensions")]
            [Description("Show all extension types.")]
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
            [Description("Show all enable/created generic IPanelObjects.")]
            public static void Generic()
            {
                if (Controller.Extensions.Objects.Count == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Generic Objects");
                foreach (IPanelObject @object in Controller.Extensions.Objects)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(@object));
            }

            [DisplayName("Show-Channels")]
            [Description("Show all open channels.")]
            public static void Channel()
            {
                if (Main.ConnectedPanels.Count == 0)
                    return;
                CurrentContext.Interpreter.Out.WriteLine("Channels:");
                foreach (ConnectedPanel connected in Main.ConnectedPanels)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(connected));
            }

            [DisplayName("Show-Profiles")]
            [Description("Show all open profiles.")]
            public static void Profile()
            {
                if (Main.Profiles.Count == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Profiles:");
                foreach (Profile profile in Main.Profiles)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(profile));
            }

            [DisplayName("Show-Mappings")]
            [Description("Show all mappings of *current contextual profile*.")]
            public static void Mapping()
            {
                if (GetContextualProfile() is not Profile profile)
                    throw new MissingSelectionException(typeof(Profile));

                if (profile.Mappings.Length == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Mappings:");
                foreach (Mapping mapping in profile.Mappings)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(mapping));
            }

            [DisplayName("Show-MappedObjects")]
            [Description("Show MappedObject of currently *selected* Mapping.")]
            public static void MappedObject()
            {
                if (CurrentContext.SelectedObject is not Mapping mapping)
                    throw new MissingSelectionException(typeof(Mapping));

                if (mapping.Objects.Count == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Mapped Objects:");
                foreach (Mapping.MappedObject mapped in mapping.Objects)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(mapped));
            }

            [DisplayName("Show-PanelInfos")]
            [Description("Show all information of all known panels.")]
            public static void PanelInfo()
            {
                if (Main.PanelsInfo.Count == 0)
                    return;

                CurrentContext.Interpreter.Out.WriteLine("Panel Infos:");
                foreach (PanelInfo info in Main.PanelsInfo)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(info));
            }

            [DisplayName("Show-Properties")]
            [Description("Show properties of currently *selected* IPanelObject.")]
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
            [Description("Show what is currently selected.")]
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
            [Description("Show PanelController logs. Supply a format with keys: /T:Time /L:Level /F:Sender /M:Message")]
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
            [Description("Use the profile, but not select it.")]
            public static void Profile(string name)
            {
                try
                {
                    Main.CurrentProfile = Main.Profiles.FindOne(profile => profile.Name == name, out int index);
                }
                catch (MoreThanOneMatchException exc)
                {
                    throw new NameCollisionException(typeof(Profile), name, "Profiles", exc);
                }
                catch (NotFoundException exc)
                {
                    throw new NotFoundException(name, "Profiles", exc);
                }
            }

            [DisplayName("Use-Extension")]
            [Description("Use an extension file (.dll).")]
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
                    throw new NotFoundException($"File {path} not found.");
                }
            }
        }

        public static class Delete
        {
            [DisplayName("Delete-Generic")]
            [Description("Delete (Disable) a generic IPanelObject. Use --index to flag to identify by index.")]
            public static void Generic(string identifier, string[]? flags = null)
            {
                IPanelObject generic;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new UserEntryParseException(typeof(int), identifier, false);
                    if (index < 0 || index >= Extensions.Objects.Count)
                        throw new OutOfBoundsException(index, Extensions.Objects.Count);
                    generic = Extensions.Objects[index];
                }
                else
                {
                    try
                    {
                        generic = Extensions.Objects.FindOne(ext => ext.GetItemName() == identifier, out index);
                    }
                    catch (MoreThanOneMatchException exc)
                    {
                        throw new NameCollisionException(typeof(IPanelObject), identifier, "Generics", exc);
                    }
                    catch (NotFoundException exc)
                    {
                        throw new NotFoundException(identifier, "Generics", exc);
                    }
                }

                Extensions.Objects.RemoveAt(index);
                int stepsBack = CurrentContext.StepsBack(generic);
                while (stepsBack >= 0)
                    CurrentContext.SelectedBack();
            }

            [DisplayName("Delete-Profile")]
            [Description("Delete a Profile. Use --index to flag to identify by index.")]
            public static void Profile(string identifier, string[]? flags = null)
            {
                int index;
                Profile profile;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new UserEntryParseException(typeof(int), identifier, false);
                    if (index < 0 || index >= Main.Profiles.Count)
                        throw new OutOfBoundsException(index, Main.Profiles.Count);
                    profile = Main.Profiles[index];
                }
                else
                {
                    try
                    {
                        profile = Main.Profiles.FindOne(profile => profile.Name == identifier, out index);
                    }
                    catch (MoreThanOneMatchException exc)
                    {
                        throw new NameCollisionException(typeof(Profile), identifier, "Profiles", exc);
                    }
                    catch (NotFoundException exc)
                    {
                        throw new NotFoundException(identifier, "Profiles", exc);
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
            [Description("Delete a Mapping of *current contextual profile*. Use --index to flag to identify by index.")]
            public static void Mapping(string identifier, string[]? flags = null)
            {
                if (GetContextualProfile() is not Profile profile)
                    throw new MissingSelectionException(typeof(Profile));

                Mapping mapping;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    Mapping[] mappings = profile.Mappings;
                    if (!int.TryParse(identifier, out index))
                        throw new UserEntryParseException(typeof(int), identifier, false);
                    if (index < 0 || index >= mappings.Length)
                        throw new OutOfBoundsException(index, mappings.Length);
                    mapping = mappings[index];
                }
                else
                {
                    try
                    {
                        mapping = profile.Mappings.FindOne(mapping => mapping.Name == identifier, out index);
                    }
                    catch (MoreThanOneMatchException exc)
                    {
                        throw new NameCollisionException(typeof(Mapping), identifier, "Mappings", exc);
                    }
                    catch (NotFoundException exc)
                    {
                        throw new NotFoundException(identifier, "Mappings", exc);
                    }
                }

                profile.RemoveMapping(mapping);
                int stepsBack = CurrentContext.StepsBack(mapping);
                while (stepsBack >= 0)
                    CurrentContext.SelectedBack();
            }

            [DisplayName("Delete-MappedObject")]
            [Description("Delete a MappedObject of currently *selected* Mapping. Use --index to flag to identify by index.")]
            public static void MappedObject(string identifier, string[]? flags = null)
            {
                if (CurrentContext.SelectedObject is not Mapping mapping)
                    throw new MissingSelectionException(typeof(Mapping));

                Mapping.MappedObject mapped;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new UserEntryParseException(typeof(int), identifier, false);
                    if (index < 0 || index >= mapping.Objects.Count)
                        throw new OutOfBoundsException(index, mapping.Objects.Count);
                    mapped = mapping.Objects[index];
                }
                else
                {
                    try
                    {
                        mapped = mapping.Objects.FindOne(mapping => mapping.Object.GetItemName() == identifier, out index);
                    }
                    catch (MoreThanOneMatchException exc)
                    {
                        throw new NameCollisionException(typeof(Mapping.MappedObject), identifier, "MappedObjects", exc);
                    }
                    catch (NotFoundException exc)
                    {
                        throw new NotFoundException(identifier, "MappedObjects", exc);
                    }
                }

                mapping.Objects.RemoveAt(index);
                int stepsBack = CurrentContext.StepsBack(mapping);
                while (stepsBack >= 0)
                    CurrentContext.SelectedBack();
            }

            [DisplayName("Delete-PanelInfo")]
            [Description("Delete PanelInfo from known panels. Use --index to flag to identify by index.")]
            public static void PanelInfo(string identifier, string[]? flags = null)
            {
                PanelInfo info;
                int index;
                if (flags?.Contains("--index") ?? false)
                {
                    if (!int.TryParse(identifier, out index))
                        throw new UserEntryParseException(typeof(int), identifier, false);
                    if (index < 0 || index >= Main.PanelsInfo.Count)
                        throw new OutOfBoundsException(index, Main.PanelsInfo.Count);
                    info = Main.PanelsInfo[index];
                }
                else
                {
                    try
                    {
                        info = Main.PanelsInfo.FindOne(info => info.Name == identifier, out index);
                    }
                    catch (MoreThanOneMatchException exc)
                    {
                        throw new NameCollisionException(typeof(PanelInfo), identifier, "PanelInfos", exc);
                    }
                    catch (NotFoundException exc)
                    {
                        throw new NotFoundException(identifier, "PanelInfos", exc);
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
                                throw new CLIFatalException("Expected state object to be of type bool");
                            messageID = (int)ConnectedPanel.ReceiveIDs.DigitalStateUpdate;
                            data = new byte[5];
                            BitConverter.GetBytes(id).CopyTo(data, 0);
                            data[4] = (byte)(activated ? 1 : 0);
                            break;
                        case InterfaceTypes.Analog:
                            if (state is not string value)
                                throw new CLIFatalException("Expected state object to be of type string");
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
            [Description("Initialize a virtual panel to be controlled through CLI commands.")]
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
                    throw new CLIFatalException("Virtual panel handshake failed.");
                int index = Main.PanelsInfo.IndexOf(_channel.PanelInfo);
                Main.PanelsInfo[index].Name = name;
            }

            [Description("Use a virtual panel whose information is already known to be controlled through CLI commands.")]
            public static void UseExisting()
            {
                if (_channel is not null)
                    throw new UserErrorException("Virtual is already initialized.");

                if (Main.PanelsInfo.Find(panel => panel.PanelGuid == VirtualPanelGuid) is not PanelInfo info)
                    throw new NotFoundException("Virtual Panel", "PanelInfos");
                Main.PanelsInfo.Remove(info);
                Initialize(info.Name, info.DigitalCount, info.AnalogCount, info.DisplayCount);
            }

            [DisplayName("Virtual-SendStroke")]
            [Description("Send a button (digital) stroke (push and release) to Controller.")]
            public static void SendStroke(uint id)
            {
                if (_channel is null)
                    throw new UserErrorException("Virtual panel was not initialized");
                if (_channel.PanelInfo.DigitalCount <= id)
                    throw new OutOfBoundsException((int)id, (int)_channel.PanelInfo.DigitalCount);
                _channel.SendInterfaceUpdate(InterfaceTypes.Digital, id, true);
                _channel.SendInterfaceUpdate(InterfaceTypes.Digital, id, false);
            }

            [DisplayName("Virtual-Set")]
            [Description("Set an analog value and send to Controller.")]
            public static void SetAnalogValue(uint id, string value)
            {
                if (_channel is null)
                    throw new UserErrorException("Virtual panel was not initialized");
                if (_channel.PanelInfo.AnalogCount <= id)
                    throw new OutOfBoundsException((int)id, (int)_channel.PanelInfo.AnalogCount);
                _channel.SendInterfaceUpdate(InterfaceTypes.Analog, id, value);
            }

            [DisplayName("Virtual-Display")]
            [Description("Not implemented...")]
            public static void Display(uint id)
            {
                if (_channel is null)
                    throw new NotImplementedException();
                if (_channel.PanelInfo.DigitalCount <= id)
                    throw new NotImplementedException();
                throw new NotImplementedException();
            }

            [DisplayName("Virtual-Deinitialize")]
            [Description("Deinitialize the Virtual Panel. Deletes panel info.")]
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
    
        public static class Help
        {
            private static void ShowConstructorHelp(ConstructorInfo ctor)
            {
                TextWriter Out = CurrentContext.Interpreter.Out;
                ParameterInfo[] parameters = ctor.GetParameters();

                if (parameters.Length == 0)
                {
                    Out.WriteLine("No arguments, default constructor.");
                    return;
                }

                Out.Write($"{ctor.DeclaringType?.FullName}(");
                for (int i = 0; i < parameters.Length; i++)
                {
                    Out.Write($"{parameters[i].ParameterType.Name}{(parameters[i].HasDefaultValue ? "?" : "")} {parameters[i].Name}");
                    if (i != parameters.Length - 1)
                        Out.Write(", ");
                }
                Out.WriteLine(')');
            }


            [DisplayName("Help")]
            [Description("Get information of a command.")]
            public static void CommandHelp(string? command = null)
            {
                if (command is null)
                {
                    foreach (CLIInterpreter.Command icmd in CurrentContext.Interpreter.Commands)
                        CurrentContext.Interpreter.Out.WriteLine(icmd.GetCommandDescription());
                    return;
                }

                if (CurrentContext.Interpreter.Commands.Find(cmd => cmd.Info.Name == command) is CLIInterpreter.Command cmd)
                {
                    CurrentContext.Interpreter.Out.WriteLine(cmd.GetFullDescription());
                    return;
                }

                CurrentContext.Interpreter.Error.WriteLine("Not a command");
            }

            [DisplayName("Help-Constructor")]
            [Description("Get information of an extension's type constructor.")]
            public static void ConstructorHelp(string typeName)
            {
                TextWriter Out = CurrentContext.Interpreter.Out;
                if (typeName.FindType() is not Type type)
                    throw new NotFoundException(typeName, "Extensions");

                if (type.GetUserConstructor() is not ConstructorInfo ctor)
                {
                    Out.WriteLine($"{type.FullName} is a non-constructable type.");
                    return;
                }

                ShowConstructorHelp(ctor);
            }

            [DisplayName("Help-Type")]
            [Description("Get information of an extension's type.")]
            public static void TypeHelp(string typeName)
            {
                TextWriter Out = CurrentContext.Interpreter.Out;
                if (typeName.FindType() is not Type type)
                    throw new NotFoundException(typeName, "Extensions");

                Out.WriteLine($"Assembly: {type.Assembly.FullName}");
                Out.WriteLine(type.FullName);

                if (type.GetUserConstructor() is ConstructorInfo ctor)
                    ShowConstructorHelp(ctor);
            }

            [DisplayName("Help-Extension")]
            [Description("Get information of an extension assembly (dll).")]
            public static void ExtensionHelp(string assemblyName)
            {
                if (Array.Find(AppDomain.CurrentDomain.GetAssemblies(), assy => assy.GetName().Name == assemblyName || Path.GetFileName(assy.Location) == assemblyName) is not Assembly assembly)
                {
                    CurrentContext.Interpreter.Error.WriteLine("Not an assembly");
                    return;
                }

                CurrentContext.Interpreter.Out.WriteLine($"{assembly.GetName().FullName}({assembly.Location}):");
                foreach (Type type in assembly.GetTypes())
                {
                    if (type.Implements<IPanelObject>())
                        CurrentContext.Interpreter.Out.WriteLine($"{type.GetInterfaces()[0]} {type.Name}");
                }
            }
        }
    }
}
