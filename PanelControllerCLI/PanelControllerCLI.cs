using System.ComponentModel;
using PanelController.Controller;
using PanelController.Profiling;
using CLIApplication;
using PanelController.PanelObjects;
using System.Reflection;
using PanelController.PanelObjects.Properties;

namespace PanelControllerCLI
{
    using Profiling = PanelController.Profiling;

    using Controller = PanelController.Controller;

    public static class PanelControllerCLI
    {
        private static readonly string[] EMPTY_FLAGS = new string[0];

        private static readonly Context _context = new(new CLIInterpreter());

        private static Context CurrentContext { get => _context; }

        public static string FormatSingleLine(this object? @object) => $"{@object}";

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

                for (int j = i; j < list.Count; j++)
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
            return FindOne<T>(list, predicate, out int unused);
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

        public static IPanelObject Instantiate(this Type type, string[] arguments)
        {
            if (type.IsAssignableTo(typeof(IPanelObject)))
                throw new NotImplementedException(null, new InvalidProgramException());

            ConstructorInfo? ctor = null;
            //ConstructorInfo? ctor = type.GetUserConstructor(); Not Implemented in PanelController
            throw new NotImplementedException();

            if (ctor is null && arguments.Length != 0)
                throw new NotImplementedException("Please enter no arguments",  new NonConstructableException());

            if (Activator.CreateInstance(type, ctor.GetParameters().ParseArguments(arguments)) is not IPanelObject @object)
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

        public static class Create
        {
            [DisplayName("Create-Generic")]
            public static void Generic(string[]? flags = null)
            {
                throw new NotImplementedException();
            }

            [DisplayName("Create-Channel")]
            public static void Channel(string[]? flags = null)
            {
                throw new NotImplementedException();
            }

            [DisplayName("Create-Profile")]
            public static void Profile(string name, string[]? flags = null)
            {
                Profile newProfile = new Profile() { Name = name };
                Main.Profiles.Add(newProfile);

                if (flags?.Contains("") is not null)
                {
                    CurrentContext.SetNewSelectionStack(
                        Main.Profiles,
                        Context.SelectionKey(Main.Profiles.IndexOf(newProfile)),
                        newProfile
                    );
                }
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
                    { Object = Instantiate(type, (string[])constructArguments) };
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
                else
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= Extensions.Objects.Count)
                        throw new NotImplementedException();
                    generic = Extensions.Objects[index];
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
                    int index;
                    Profile profile = Main.Profiles.FindOne(profile => profile.Name == name, out index);
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
                else
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= Extensions.Objects.Count)
                        throw new NotImplementedException();
                    mapping = profile.Mappings[index];
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
                else
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= Extensions.Objects.Count)
                        throw new NotImplementedException();
                    mapped = mapping.Objects[index];
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
                else
                {
                    if (!int.TryParse(identifier, out index))
                        throw new NotImplementedException();
                    if (index < 0 || index >= Extensions.Objects.Count)
                        throw new NotImplementedException();
                    panelInfo = Main.PanelsInfo[index];
                }

                CurrentContext.SetNewSelectionStack(
                    Main.PanelsInfo,
                    Context.SelectionKey(index),
                    panelInfo
                );
            }
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

                throw new NotImplementedException();
            }

            [DisplayName("Edit-Property")]
            public static void Property(string property, string value)
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
            public static void CollectionOrder()
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
                    CurrentContext.Interpreter.Out.WriteLine("\t" + category.ToString());
                    foreach (Type type in Controller.Extensions.ExtensionsByCategory[category])
                        CurrentContext.Interpreter.Out.WriteLine("\t\t" + FormatSingleLine(type));
                }
            }

            [DisplayName("Show-Generics")]
            public static void Generic()
            {
                CurrentContext.Interpreter.Out.WriteLine("Generic Objects");
                foreach (IPanelObject @object in Controller.Extensions.Objects)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(@object));
            }

            [DisplayName("Show-Channels")]
            public static void Channel()
            {
                CurrentContext.Interpreter.Out.WriteLine("Channels:");
                foreach (ConnectedPanel connected in Main.ConnectedPanels)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(connected));
            }

            [DisplayName("Show-Profiles")]
            public static void Profile()
            {
                CurrentContext.Interpreter.Out.WriteLine("Profiles:");
                foreach (Profile profile in Main.Profiles)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(profile));
            }

            [DisplayName("Show-Mappings")]
            public static void Mapping()
            {
                if (GetContextualProfile() is not Profile profile)
                    throw new NotImplementedException(null, new MissingSelectionException());

                CurrentContext.Interpreter.Out.WriteLine("Mappings:");
                foreach (Mapping mapping in profile.Mappings)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(mapping));
            }

            [DisplayName("Show-MappedObjects")]
            public static void MappedObject()
            {
                if (CurrentContext.SelectedObject is not Mapping mapping)
                    throw new NotImplementedException(null, new MissingSelectionException());

                CurrentContext.Interpreter.Out.WriteLine("Mapped Objects:");
                foreach (Mapping.MappedObject mapped in mapping.Objects)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(mapped));
            }

            [DisplayName("Show-PanelInfos")]
            public static void PanelInfo()
            {
                CurrentContext.Interpreter.Out.WriteLine("Panel Infos:");
                foreach (PanelInfo info in Main.PanelsInfo)
                    CurrentContext.Interpreter.Out.WriteLine("\t" + FormatSingleLine(info));
            }
        }

        public static class Use
        {
            [DisplayName("Use-Profile")]
            public static void Profile()
            {
                throw new NotImplementedException();
            }
        }

        public static class Delete
        {
            [DisplayName("Delete-Generic")]
            public static void Generic()
            {
                throw new NotImplementedException();
            }

            [DisplayName("Delete-Channel")]
            public static void Channel()
            {
                throw new NotImplementedException();
            }

            [DisplayName("Delete-Profile")]
            public static void Profile()
            {
                throw new NotImplementedException();
            }

            [DisplayName("Delete-Mapping")]
            public static void Mapping()
            {
                throw new NotImplementedException();
            }

            [DisplayName("Delete-MappedObject")]
            public static void MappedObject()
            {
                throw new NotImplementedException();
            }

            [DisplayName("Delete-PanelInfo")]
            public static void PanelInfo()
            {
                throw new NotImplementedException();
            }

        }
    }
}
