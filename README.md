# SubLists

<img width="281" height="361" alt="Screenshot 2026-02-28 at 3 41 30 PM" src="https://github.com/user-attachments/assets/de72d375-ff33-4a96-bdd9-22da969f6f50" />


Collapsible foldout groups for inspector fields. Essentially replacing `[Header]`.

## Usage

1. Add `[SubList("Name")]` to the first field of each group in your serializable class.
2. Create a one-line drawer in an `Editor` folder:

```csharp
[CustomPropertyDrawer(typeof(YourClass))]
public class YourClassDrawer : SubListDrawer { }
```

Fields between `[SubList]` attributes are grouped under that foldout. Use `startClosed: true` to collapse by default.
