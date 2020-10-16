# Serialized Component Tool


## Description

### Why it's designed

- Fast in getting a node with its components. No dragging, no defining in your code and no finding is needed.
- Convenient in getting and recycling an instance of a template. Make it easy to develop a bag-UI or a scroll list.
- A container could be a node in its parent container. Thus nodes of various categories could be managed in different layers.
- It's specially useful when each GameObject or prefab are of different hierarchies, and a bunch of its nodes or components should be referenced by your logic, game UI panel for example.

### Important Concepts

#### Container

A container is a component added on a node. It is generated and added automatically by this tool. It stores all the references to the key nodes and their components. The nodes that a container contains are all its child nodes.

#### Root Container

A root container is a container that is not referenced by any other containers. The node name of root container will be used as the class name of container-component. So it should follow the rules that a class name does.

#### Sub Container

Sub container is a special kind of key node. The node name of it starts with "i\_". A container-component will be added on it automatically with all the features that a container has.

The type name of container-component consists of it's container's type name, an underline and the sub container node name without "i\_".  So the node name of a sub container should follow the rules that a class name does.

#### Node

A node is a gameObject in hierarchy of a container.

#### Key Node

Child gameObjects of a container with their names start with "m\_" or "i\_". The "m\_" nodes are normal key nodes, and the reference to the nodes with their components will be stored in their container. The "i\_" nodes will be treated as sub container with all features that normal key nodes have. Sub container component will be added to "i\_" nodes automatically, in order to store the references to the key nodes and their components.

The container of a key node is the nearest container in its parents.

All the NONE key nodes will be ignored. So "node" in the following words represents "key node".

The node name without "m\_" or "i\_" will be used as field name or property name. So the name without "m\_" or "i\_" should follow the rules of variable name.

#### Node Component

The functional components added on node during your logic development, such as UnityEngine.UI.Image, UnityEngine.UI.Text. The references to them will be stored in container together with their nodes.

Only supported components will be referenced. Other than the components that already supported, you can make any other component supported by typing a few lines of editor code, to make the component referenced by container.



## Work with It

### Add or Update Container of GameObject

1. Create or modify your asset to satisfy your needs, arrange its key nodes and sub containers, name them properly.
2. Right click your prefab in "Project" window and select "GreatClock -> Serialize Tools -> C# Serialized Component" to open tool window. Or open tool window by clicking "GreatClock -> Serialize Tools -> C# Serialized Component" in menu bar, and drag the gameObject you want to process to the "GameObject" field.
3. Check nodes, sub containers and components in tool window.
4. Check base classes and partial configuration and public configuration of each container or sub container.
5. Click "Generate & Mount" button. The container C-Sharp file will be generated or updated. And then the references to nodes and components will be built and stored in containers.
6. Write your runtime code to get the root container on the gameObject, or complete the container class with your logic code via "partial class".

### Make Your Component Type Supported

Place the following snippet in any of your Editor folders, to make "MyComponent" supported by this tool.

```c#
using GreatClock.Common.SerializeTools;

// class name does not matter, but make sure it works in Unity Editor
public class SerializeToolExtend {
    
    // The attribute is always needed. The method should be marked as static.
    // The return type should be SupportedTypeData, make sure return value is valid
    // The method name does not matter. Just make it proper in your project.
    [SupportedComponentType]
    static SupportedTypeData DefineTypeMyComponent() {
        // Your should specify the component type to be supported, sorting priority,
        // show name in tool window, using namespace in generated code,
        // type name in generated code, and variable name in generated code.
        // The last 4 parameters could be null to make them configurated automatically.
        return new SupportedTypeData(typeof(MyComponent), 1000, null, null, null, null);
    }
    
    [SupportedComponentType]
    static SupportedTypeData DefineTypeTween() {
        // Make "MyTweenComponent" component supported.
        // And specify its variable name "tween" in generated code.
        return new SupportedTypeData(typeof(MyTweenComponent), 1000, null, null, null, "tween");
    }
    
}
```



## Notes

- DO NOT edit generated code manually.
- The generated code is referenced by gameObject prefab. So hotfix for runtime code will not work. Adding, renaming or removing nodes in hotfix is invalid.
- Duplicated key node names is not allowed in ONE container.
- This tool will only create or update the container or sub container  if its name is the same as the container name that match the rules. Obsolete containers will be treated common components and will not be cleaned up. So if a container or sub container is moved renamed or changed into normal key nodes, manual cleaning up is required.
- The key nodes and their components you'll get are member variables of container instance. It makes it convenient for your logic code to access what you want, but also makes more compile errors if the node or component you are using is deleted or modified. The compile errors inform you of logic issues in accessing invalid nodes or components.
- If you want to implement your logic of a container with a partial class, "private property" is recommended. If you want the container to be just a container, non partial class and public property are good choices.



## FAQs

**Q:** How to deal with the compile error after processing "Generate & Mount"?

**A:** If compile errors occur, the container and references to nodes or components will NOT be guaranteed. To make sure the container and its references are accurate, "Generate & Mount" should be executed again after compile errors are resolved. Compile errors could be caused by illegal container or node name, deleting or renaming nodes, removing components. So make sure your the names of containers and nodes are legal, and there is no reference to any nodes or components that do not exist.

------

**Q:** What's the relationship between containers and generated codes?

**A:** In this tool each container refers to a corresponding generated code. The name of generated file and its class name will be the same as ROOT container node name, or consists of it's container's class name and '_' and self node name if it's a sub container. All the generated codes of a specific gameObject or prefab are stored in the same directory.

------

**Q:** How is the (gameObject or prefab) - (directory containing generated codes) map managed?

**A:** The directory of each gameObject or prefab can be specified individually. But there's no configuration that keeps the relationship. When the tool window is opened and "Game Object" is assigned, it searches the code that match your gameObject, and fetch it's base class, check if it's partial class and whether the nodes are of public properties. The directory is the one where the matched codes stay in. So all the configuration are stored by relative generated codes.

------

**Q:** How to work with tool when dealing with bag slot or item in scroll view?

**A:** In these situations, a vast number of nodes which are of the same hierarchy will be listed in the same parent node. And each node has its own logic instance that controls its own contents. When you are modifying your asset in Unity editor, editing the template including its sub nodes is the only one to modify. A layout component is needed in UI, or you can implement your own arrangement. Treat the template as a sub container with its name starts with "i\_", to make sure of getting multiple individual instances at runtime. In runtime, it recommended to hide the template by calling SetActive(false) of the template gameObject. By calling GetInstance() method of the template, you'll get an instance of the template to fulfill your logic of an list item. Remember to call SetActive(true) method of gameObject on the instance you just got. When refreshing or recycling item removal may be needed by cleaning up the instance and calling CacheInstance() method of the template to cache the each of the unused instances for further use.

