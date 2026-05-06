# Json

A .NET library for parsing JSON into a navigable tree and extracting fields via a JSONPath-like query syntax.

## JsonTree

Parses a JSON string into an in-memory tree where each node has a full identity path (e.g. `$.person.name`, `$['first name']`).

```csharp
var tree = JsonTree.Create("""{"person":{"name":"Alice","age":30}}""");

var node = tree.FindNode("$.person.name");
// node.Value    → "Alice"
// node.Kind     → JsonValueKind.String
// node.Identity → "$.person.name"

var nodes = tree.Flatten();        // all nodes as a flat array
var json  = tree.ToJsonString();   // round-trip back to JSON
```

Node identities use dot notation for object properties and bracket notation for array indices and non-identifier keys:

```
$.person          →  regular object property
$.matrix[0][1]    →  array indices
$['first name']   →  key that is not a valid identifier
```

`FindNode` accepts an identity path and returns the matching node, or `null` if not found. Lookup is case-sensitive by default.

## JsonFieldSelector

Extracts and projects fields from a JSON document. Only wildcards and named properties are supported — specific array indices such as `[0]` are **not** part of the selector syntax.

```csharp
var selector = new JsonFieldSelector(jsonString);
var result   = selector.Select("$.person.name");
```

### Supported syntax

| Expression | Returns |
|---|---|
| `$` | Entire document |
| `$.a.b` | Scalar value or sub-object at path |
| `$.items[*].name` | Array of `name` values from every element |
| `$.items.*` | Array of all property values of an object |
| `$.a[*].b[*].c` | Chained wildcards across nested arrays |
| `$.person[name,age]` | Object projected to the listed fields only |
| `$[name,age]` | Projection from the root object |
| `$.items[*][id,name]` | Array of projected objects |

Field names in projections and path segments are matched **case-insensitively**.

### Examples

```csharp
// Wildcard — returns ["Allan","Bertil"]
selector.Select("$.crew.members[*].name");

// Projection — returns {"name":"Alice","age":30}
selector.Select("$.person[name,age]");

// Nested wildcard + projection — returns [{...},{...}]
selector.Select("$.store.book[*][title,price]");
```
