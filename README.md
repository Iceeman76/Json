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

`FindNode` accepts an identity path and returns the matching node, or `null` if not found. Lookup is case-insensitive.

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
| `$.items[id,name]` | Same as above — `[*]` is inferred when the target is an array |
| `$.order[id,customer[name,email]]` | Nested sub-projection inside an object |
| `$[id,user[name,posts[title,date]]]` | Sub-projection at any depth, including arrays nested inside objects |
| `$.events[description,league.name]` | Dotted path inside a projection — descends into nested objects |
| `$.events[description,league:league.name]` | Renames the projected field — emits as `"league"` instead of `"name"` |

Projections compose to arbitrary depth: each inner field may itself be plain, `field[*]`, `field[*][...]`, or `field[...]`. When the resolved node is an array, `field[...]` is treated as a per-element projection, so the explicit `[*]` is optional.

Any field inside a projection can be aliased with `alias:path` — the source path resolves normally and the emitted property name becomes `alias`. The alias may be combined with sub-projections, e.g. `info:person[name,age]` emits `{"info":{...}}`.

Field names in projections and path segments are matched **case-insensitively**.

### Examples

```csharp
// Wildcard — returns ["Allan","Bertil"]
selector.Select("$.crew.members[*].name");

// Projection — returns {"name":"Alice","age":30}
selector.Select("$.person[name,age]");

// Nested wildcard + projection — returns [{...},{...}]
selector.Select("$.store.book[*][title,price]");

// Array auto-detect — same result as $.crew[*][id,name]
selector.Select("$.crew[id,name]");

// Sub-projection nested inside another sub-projection
selector.Select("$[requestId,draw[drawComment,events[eventNumber,description]]]");

// Aliasing — emits {"members":[{"id":1,"name":"Allan"},...]}
selector.Select("$[members:crew[id,name]]");
```
