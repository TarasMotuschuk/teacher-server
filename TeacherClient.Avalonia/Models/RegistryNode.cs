using System.Collections.ObjectModel;

namespace TeacherClient.CrossPlatform;

public sealed class RegistryNode
{
    public RegistryNode(string name, string path, bool hasChildren)
    {
        Name = name;
        Path = path;
        if (hasChildren)
        {
            Children.Add(new RegistryNode("...", path, hasChildren: false));
        }
    }

    public string Name { get; }

    public string Path { get; }

    public bool IsLoaded { get; set; }

    public ObservableCollection<RegistryNode> Children { get; } = [];
}
