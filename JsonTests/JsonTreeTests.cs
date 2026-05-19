using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Json;

namespace JsonTests;

public class JsonTreeTests
{
    private readonly JsonWriterOptions _options = new() { Indented = true };

    [Test]
    public void Large_json()
    {
        var jsonString = File.ReadAllText("example.json", Encoding.UTF8);
        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes, Has.Length.EqualTo(564));
            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void GetValue_with_undefined_json_element_throws()
    {
        var element = default(JsonElement);

        Assert.Throws<JsonException>(() => element.GetValue());
    }

    [Test]
    public void Simple_flat_object()
    {
        const string jsonString = """
                                  {"a":1,"b":true}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.a"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$.b"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void Nested_object()
    {
        const string jsonString = """
                                  {"person":{"name":"Alice","age":30}}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.person"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$.person.name"));
            Assert.That(nodes[2].JsonPath, Is.EqualTo("$.person.age"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void Array_of_primitives()
    {
        const string jsonString = """
                                  {"numbers":[10,20,30]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.numbers"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$.numbers[0]"));
            Assert.That(nodes[2].JsonPath, Is.EqualTo("$.numbers[1]"));
            Assert.That(nodes[3].JsonPath, Is.EqualTo("$.numbers[2]"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void Array_of_objects()
    {
        const string jsonString = """
                                  {"users":[{"id":1},{"id":2}]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.users"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$.users[0]"));
            Assert.That(nodes[2].JsonPath, Is.EqualTo("$.users[0].id"));
            Assert.That(nodes[3].JsonPath, Is.EqualTo("$.users[1]"));
            Assert.That(nodes[4].JsonPath, Is.EqualTo("$.users[1].id"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void Mixed_types_inside_array()
    {
        const string jsonString = """
                                  {"mixed":[1,"two",{"three":3}]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.mixed"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$.mixed[0]"));
            Assert.That(nodes[2].JsonPath, Is.EqualTo("$.mixed[1]"));
            Assert.That(nodes[3].JsonPath, Is.EqualTo("$.mixed[2]"));
            Assert.That(nodes[4].JsonPath, Is.EqualTo("$.mixed[2].three"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void Invalid_java_script_identifiers()
    {
        const string jsonString = """
                                  {"first name":"Alice","123key":"value","key.with.dot":"ok"}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$['first name']"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$['123key']"));
            Assert.That(nodes[2].JsonPath, Is.EqualTo("$['key.with.dot']"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void Empty_string_key_uses_bracket_notation()
    {
        const string jsonString = """{"":42}""";

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].Name, Is.EqualTo(""));
            Assert.That(nodes[0].PathSegment, Is.EqualTo("['']"));
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$['']"));
        }
    }

    [Test]
    public void Double_values_round_trip_in_object_and_array()
    {
        const string jsonString = """{"big":1e30,"items":[2e30]}""";

        var tree = JsonTree.Parse(jsonString);
        var output = tree.ToJsonString();

        using var document = JsonDocument.Parse(output);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(document.RootElement.GetProperty("big").GetDouble(), Is.EqualTo(1e30));
            Assert.That(document.RootElement.GetProperty("items")[0].GetDouble(), Is.EqualTo(2e30));
        }
    }

    [Test]
    public void Boolean_values_round_trip_in_object_and_array()
    {
        const string jsonString = """{"enabled":true,"flags":[true,false]}""";

        var tree = JsonTree.Parse(jsonString);

        Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
    }

    [Test]
    public void Decimal_values_round_trip_in_object_and_array()
    {
        const string jsonString = """{"price":1.5,"prices":[2.5,3.5]}""";

        var tree = JsonTree.Parse(jsonString);

        Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
    }

    [Test]
    public void Null_values_round_trip_in_object_and_array()
    {
        const string jsonString = """{"value":null,"items":[null,null]}""";

        var tree = JsonTree.Parse(jsonString);

        Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
    }

    [Test]
    public void Nested_arrays()
    {
        const string jsonString = """
                                  {"matrix":[[1,2],[3,4]]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.matrix"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$.matrix[0]"));
            Assert.That(nodes[2].JsonPath, Is.EqualTo("$.matrix[0][0]"));
            Assert.That(nodes[3].JsonPath, Is.EqualTo("$.matrix[0][1]"));
            Assert.That(nodes[4].JsonPath, Is.EqualTo("$.matrix[1]"));
            Assert.That(nodes[5].JsonPath, Is.EqualTo("$.matrix[1][0]"));
            Assert.That(nodes[6].JsonPath, Is.EqualTo("$.matrix[1][1]"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void Nulls_booleans_numbers_strings()
    {
        const string jsonString = """
                                  {"n":null,"b":false,"s":"hi","x":42}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.n"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$.b"));
            Assert.That(nodes[2].JsonPath, Is.EqualTo("$.s"));
            Assert.That(nodes[3].JsonPath, Is.EqualTo("$.x"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void Empty_object_and_array()
    {
        const string jsonString = """
                                  {"emptyObj":{},"emptyArr":[]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.emptyObj"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$.emptyArr"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void Deep_nesting()
    {
        const string jsonString = """
                                  {"a":{"b":{"c":[{"d":1}]}}}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var nodes = tree.Flatten();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.a"));
            Assert.That(nodes[1].JsonPath, Is.EqualTo("$.a.b"));
            Assert.That(nodes[2].JsonPath, Is.EqualTo("$.a.b.c"));
            Assert.That(nodes[3].JsonPath, Is.EqualTo("$.a.b.c[0]"));
            Assert.That(nodes[4].JsonPath, Is.EqualTo("$.a.b.c[0].d"));

            Assert.That(tree.ToJsonString(), Is.EqualTo(jsonString));
        }
    }

    [Test]
    public void FindNode_returns_top_level_property()
    {
        const string jsonString = """
                                  {"a":1,"b":true}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.a")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.Name, Is.EqualTo("a"));
            Assert.That(node.Value, Is.EqualTo(1));
            Assert.That(node.Kind, Is.EqualTo(JsonValueKind.Number));
        }
    }

    [Test]
    public void FindNode_returns_nested_property()
    {
        const string jsonString = """
                                  {"person":{"name":"Alice","age":30}}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.person.name")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.Value, Is.EqualTo("Alice"));
            Assert.That(node.Kind, Is.EqualTo(JsonValueKind.String));
        }
    }

    [Test]
    public void FindNode_returns_array_element()
    {
        const string jsonString = """
                                  {"numbers":[10,20,30]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.numbers[1]")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.Value, Is.EqualTo(20));
            Assert.That(node.InArray, Is.True);
        }
    }

    [Test]
    public void FindNode_returns_property_inside_array_object()
    {
        const string jsonString = """
                                  {"users":[{"id":1},{"id":2}]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.users[1].id")!;

        Assert.That(node.Value, Is.EqualTo(2));
    }

    [Test]
    public void FindNode_returns_node_in_deeply_nested_structure()
    {
        const string jsonString = """
                                  {"a":{"b":{"c":[{"d":1}]}}}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.a.b.c[0].d")!;

        Assert.That(node.Value, Is.EqualTo(1));
    }

    [Test]
    public void FindNode_returns_node_with_bracket_notation_for_invalid_identifier()
    {
        const string jsonString = """
                                  {"first name":"Alice"}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$['first name']")!;

        Assert.That(node.Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void FindNode_is_case_insensitive()
    {
        const string jsonString = """
                                  {"Person":{"Name":"Alice"}}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.PERSON.name")!;

        Assert.That(node.Value, Is.EqualTo("Alice"));
    }

    [Test]
    public void FindNode_returns_object_node()
    {
        const string jsonString = """
                                  {"person":{"name":"Alice","age":30}}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.person")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.Kind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(node.Children, Has.Exactly(2).Items);
        }
    }

    [Test]
    public void FindNode_returns_array_node()
    {
        const string jsonString = """
                                  {"numbers":[10,20,30]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.numbers")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.Kind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(node.Children, Has.Exactly(3).Items);
        }
    }

    [Test]
    public void FindNode_returns_null_when_JsonPath_not_found()
    {
        const string jsonString = """
                                  {"a":1,"b":2}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.does_not_exist");

        Assert.That(node, Is.Null);
    }

    [Test]
    public void FindNode_returns_root_node_for_root_JsonPath()
    {
        const string jsonString = """
                                  {"a":1}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$")!;
        var nodes = node.Flatten();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.JsonPath, Is.EqualTo("$"));
            Assert.That(node.Kind, Is.EqualTo(JsonValueKind.Object));

            Assert.That(nodes[0].JsonPath, Is.EqualTo("$.a"));
            Assert.That(nodes[0].Value, Is.EqualTo(1));
            Assert.That(nodes[0].Kind, Is.EqualTo(JsonValueKind.Number));
        }
    }

    [Test]
    public void FindNode_returns_null_for_empty_JsonPath()
    {
        const string jsonString = """
                                  {"a":1}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode(string.Empty);

        Assert.That(node, Is.Null);
    }

    [Test]
    public void FindNode_returns_null_when_path_is_partially_correct()
    {
        const string jsonString = """
                                  {"a":{"b":1}}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.a.b.c");

        Assert.That(node, Is.Null);
    }

    [Test]
    public void FindNode_returns_null_for_array_index_out_of_range()
    {
        const string jsonString = """
                                  {"numbers":[10,20]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.numbers[5]");

        Assert.That(node, Is.Null);
    }

    [Test]
    public void FindNode_finds_null_value_node()
    {
        const string jsonString = """
                                  {"n":null}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.n")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.Kind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(node.Value, Is.Null);
        }
    }

    [Test]
    public void FindNode_finds_element_in_nested_array()
    {
        const string jsonString = """
                                  {"matrix":[[1,2],[3,4]]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.matrix[1][0]")!;

        Assert.That(node.Value, Is.EqualTo(3));
    }

    [Test]
    public void Node_ToString_returns_expected_format()
    {
        const string jsonString = """
                                  {"name":"Alice"}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.name")!;

        Assert.That(node.ToString(), Is.EqualTo("Name = name, Value = Alice, Kind = String, JsonPath = $.name"));
    }

    [Test]
    public void ToJsonString_throws_when_node_kind_is_undefined()
    {
        const string jsonString = """
                                  {"a":1}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.a");

        Assert.That(node, Is.Not.Null);
        node.Kind = JsonValueKind.Undefined;

        Assert.That(() => tree.ToJsonString(), Throws.TypeOf<InvalidOperationException>()
            .With.Message.EqualTo("Node '$.a' has Kind = Undefined and cannot be written."));
    }

    [Test]
    public void ToJsonString_throws_when_number_node_has_unsupported_value_type()
    {
        const string jsonString = """
                                  {"a":1}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.a");

        Assert.That(node, Is.Not.Null);
        node.Value = "not a number";

        Assert.That(() => tree.ToJsonString(), Throws.TypeOf<InvalidOperationException>()
            .With.Message.EqualTo("Node '$.a' has unsupported number type: System.String."));
    }

    [Test]
    public void Create_throws_when_json_exceeds_default_max_depth()
    {
        var jsonString = BuildNestedJson(65);

        Assert.Catch<JsonException>(() => JsonTree.Parse(jsonString));
    }

    [Test]
    public void Create_accepts_json_at_default_max_depth()
    {
        var jsonString = BuildNestedJson(64);

        Assert.That(() => JsonTree.Parse(jsonString), Throws.Nothing);
    }

    [Test]
    public void Create_respects_custom_max_depth()
    {
        var jsonString = BuildNestedJson(10);

        Assert.Catch(() => JsonTree.Parse(jsonString, maxDepth: 5));
    }

    [Test]
    public void ToJsonString_applies_relaxed_encoder_when_options_lack_one()
    {
        const string jsonString = """
                                  {"html":"<b>&hello</b>"}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var customOptions = new JsonWriterOptions { Indented = false };

        var output = tree.ToJsonString(customOptions);

        Assert.That(output, Does.Contain("<b>&hello</b>"));
        Assert.That(output, Does.Not.Contain("\\u003C"));
    }

    [Test]
    public void ToJsonString_respects_explicit_encoder_in_options()
    {
        const string jsonString = """
                                  {"html":"<b>"}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var strictOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.Default };

        var output = tree.ToJsonString(strictOptions);

        Assert.That(output, Does.Contain("\\u003C"));
    }

    [Test]
    public void Name_is_raw_key_for_invalid_identifier()
    {
        const string jsonString = """
                                  {"first name":"Alice"}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$['first name']")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.Name, Is.EqualTo("first name"));
            Assert.That(node.PathSegment, Is.EqualTo("['first name']"));
            Assert.That(node.JsonPath, Is.EqualTo("$['first name']"));
        }
    }

    [Test]
    public void Name_is_raw_index_for_array_element()
    {
        const string jsonString = """
                                  {"numbers":[10,20]}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.numbers[1]")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.Name, Is.EqualTo("1"));
            Assert.That(node.PathSegment, Is.EqualTo("[1]"));
            Assert.That(node.JsonPath, Is.EqualTo("$.numbers[1]"));
        }
    }

    [Test]
    public void Name_equals_path_segment_for_valid_identifier()
    {
        const string jsonString = """
                                  {"name":"Alice"}
                                  """;

        var tree = JsonTree.Parse(jsonString);
        var node = tree.FindNode("$.name")!;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(node.Name, Is.EqualTo("name"));
            Assert.That(node.PathSegment, Is.EqualTo("name"));
        }
    }

    private static string BuildNestedJson(int depth)
    {
        var sb = new StringBuilder();

        for (var i = 0; i < depth; i++)
        {
            sb.Append("{\"a\":");
        }

        sb.Append('1');

        for (var i = 0; i < depth; i++)
        {
            sb.Append('}');
        }

        return sb.ToString();
    }
}
