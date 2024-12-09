﻿using CLIApplication;
using PanelController.Profiling;
using System.Collections;
using System.Diagnostics.Contracts;
using System.Linq;

namespace PanelControllerCLI
{
    public class Context(CLIInterpreter interpreter)
    {
        private class ContainerKey(object key)
        {
            public readonly object key = key;
        }

        private readonly CLIInterpreter _interpreter = interpreter;

        private readonly Stack _selectionStack = new();

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
            Stack repush = new();
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

        public static bool IsContainerKey(object? @object) => @object as ContainerKey is not null;

        public static object GetContainerKey(object key)
        {
            if (key is not ContainerKey containerKey)
                throw new NotImplementedException();
            return containerKey.key;
        }

        public object?[] CurrentSelectionStack() => _selectionStack.ToArray();

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

            object? newTop = _selectionStack.Pop();
            if (newTop is ContainerKey)
                return SelectedBack();
            return newTop;
        }

        public int StepsBack(object @object)
        {
            int stepsBack = -1;
            Stack repush = new();
            for (int i = _selectionStack.Count - 1; i >= 0; i--)
            {
                repush.Push(_selectionStack.Pop());
                stepsBack++;
                if (repush.Peek() == @object)
                    break;
            }
            while (repush.Count > 0)
                _selectionStack.Push(repush.Pop());
            return stepsBack;
        }

        public static object SelectionKey<T>(T key)
        {
            if (key is null)
                throw new InvalidProgramException("ContainterKey.ContainerKey(object?): ContainerKey.key cannot be null.");
            return new ContainerKey(key);
        }

        public static bool IsCollection(object? @object) => (@object as IList is not null || @object as IDictionary is not null) && !IsContainerKey(@object);

        public static bool IsObject(object? @object) => !IsCollection(@object) && !IsContainerKey(@object);
    }
}
