# SubList

Collapsible foldout groups for inspector fields. Replaces `[Header]`.

## Usage

1. Add `[SubList("Name")]` to the first field of each group in your serializable class.
2. Create a one-line drawer in an `Editor` folder:

```csharp
[CustomPropertyDrawer(typeof(YourClass))]
public class YourClassDrawer : SubListDrawer { }
```

Fields between `[SubList]` attributes are grouped under that foldout. Use `startClosed: true` to collapse by default.
