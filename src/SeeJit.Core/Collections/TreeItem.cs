namespace SeeJit.Collections
{
    using System.Collections.Generic;

    internal class TreeItem<T>
    {
        public readonly T Value;

        public readonly List<TreeItem<T>> Children;

        public TreeItem(T value, List<TreeItem<T>> children)
        {
            Value = value;
            Children = children;
        }

        public TreeItem(T value, bool hasChildren = false)
            : this(value, hasChildren ? new List<TreeItem<T>>() : null) { }
    }
}
