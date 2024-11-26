using System;
using System.Collections.Generic;

namespace Qatalyst.Objects;

[Serializable]
public class SerializableTreeNode
{
    public string Content { get; set; }
    public bool IsExpanded { get; set; }
    public string Tag { get; set; }
    public List<SerializableTreeNode> Children { get; set; } = new List<SerializableTreeNode>();
}
