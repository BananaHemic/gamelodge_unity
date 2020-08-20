using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModelTreeElement
{
    public bool IsModel { get; private set; }
    public string Name { get; private set; }
    // Only usable if this is a folder
    public ModelTree Tree { get; private set; }
    // Only usable if this is an item
    public ushort BundleIndex { get; private set; }
    public BundleItem BundleItem { get; private set; }

    // Folder init
    public ModelTreeElement(ModelTree modelTree)
    {
        Tree = modelTree;
        IsModel = false;
        Name = modelTree.Name;
    }
    // Model init
    public ModelTreeElement(string objectName, BundleItem bundleItem, ushort subBundleIndex)
    {
        IsModel = true;
        Name = objectName;
        BundleItem = bundleItem;
        BundleIndex = subBundleIndex;
    }
}
public class ModelTree : IEnumerable<ModelTreeElement>
{
    private readonly Dictionary<string, ModelTreeElement> _children =
                                        new Dictionary<string, ModelTreeElement>();
    private readonly List<ModelTreeElement> _childrenList =
                                        new List<ModelTreeElement>();

    public readonly string Name;
    public ModelTree Parent { get; private set; }

    public ModelTree(string name, ModelTree parent)
    {
        this.Name = name;
        this.Parent = parent;
    }

    public ModelTreeElement GetChild(string id)
    {
        return this._children[id];
    }
    public List<ModelTreeElement> GetAllChildren()
    {
        return _childrenList;
    }
    public void AddModel(string modelName, ushort bundleIndex, BundleItem bundleItem)
    {
        ModelTreeElement treeElement = new ModelTreeElement(modelName, bundleItem, bundleIndex);
        _children.Add(modelName, treeElement);
        _childrenList.Add(treeElement);
    }
    public ModelTree GetOrAddFolder(string folderName)
    {
        ModelTreeElement treeElement;
        if(_children.TryGetValue(folderName, out treeElement))
            return treeElement.Tree;

        ModelTree tree = new ModelTree(folderName, this);
        treeElement = new ModelTreeElement(tree);
        _children.Add(folderName, treeElement);
        _childrenList.Add(treeElement);
        return tree;
    }

    public IEnumerator<ModelTreeElement> GetEnumerator()
    {
        return this._children.Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return this.GetEnumerator();
    }

    public int Count
    {
        get { return this._children.Count; }
    }
}
