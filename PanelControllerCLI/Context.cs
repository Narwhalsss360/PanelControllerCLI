using CLIApplication;
using PanelController.Profiling;
using System.Collections;
using System.Diagnostics.Contracts;

namespace PanelControllerCLI
{
    public class Context
    {
        private class ContainerKey
        {
            public object key = null;

            public ContainerKey(object key)
            {
                if (key is null)
                    throw new InvalidProgramException("ContainterKey.ContainerKey(object?): ContainerKey.key cannot be null.");
                this.key = key;
            }
        }

        private CLIInterpreter _interpreter;

        private Stack _selectionStack = new();

        public CLIInterpreter Interpreter { get => _interpreter; }

        public object? SelectedObject
        {
            get
            {
                if (_selectionStack.Count == 0)
                {
                    return null;
                }
                return _selectionStack.Peek();
            }
        }

        public object? ContainingObject
        {
            get => Highest(IsObject);
        }

        public object? ContainingCollection
        {
            get => Highest(IsCollection);
        }

        public object? Key
        {
            get => Highest(IsContainerKey);
        }

        public bool HasParent
        {
            get => _selectionStack.Count > 1;
        }

        public object? Parent
        {
            get
            {
                if (!HasParent)
                    return false;
                object? selected = _selectionStack.Pop();
                object? parent = _selectionStack.Peek();
                _selectionStack.Push(selected);
                return parent;
            }
        }

        public object? CurrentKey
        {
            get => (Highest(IsContainerKey) as ContainerKey)?.key;
        }

        public object? Highest(Predicate<object?> predicate)
        {
            object? containingObject = null;
            Stack repush = new Stack();
            for (int i = _selectionStack.Count - 1; i >= 0; i--)
            {
                repush.Push(_selectionStack.Pop());
                if (predicate(repush.Peek()))
                {
                    containingObject = _selectionStack.Peek();
                    break;
                }
            }

            while (repush.Count > 0)
                _selectionStack.Push(repush.Pop());
            return containingObject;
        }

        private static bool IsContainerKey(object? @object) => @object as ContainerKey is not null;

        public Context(CLIInterpreter interpreter)
        {
            _interpreter = interpreter;
        }

        public void SetNewSelectionStack(params object[] objects)
        {
            _selectionStack.Clear();
            foreach (object @object in objects)
                _selectionStack.Push(@object);
        }

        public void SelectedInnerProperty(object property)
        {
            _selectionStack.Push(property);
        }

        public void SelectedInnerCollectionItem(object key, object item)
        {
            _selectionStack.Push(new ContainerKey(key));
            _selectionStack.Push(item);
        }


        public void SelectedInnerCollectionAndItem(object collection, object key, object item)
        {
            SelectedInnerProperty(collection);
            SelectedInnerCollectionItem(key, item);
        }

        public object? SelectedBack()
        {
            if (_selectionStack.Count == 0)
                return null;
            return _selectionStack.Pop();
        }

        public static object SelectionKey<T>(T key) => new ContainerKey(key);

        public static bool IsCollection(object? @object) => (@object as IList is not null || @object as IDictionary is not null) && !IsContainerKey(@object);

        public static bool IsObject(object? @object) => !IsCollection(@object) && !IsContainerKey(@object);
    }
}
