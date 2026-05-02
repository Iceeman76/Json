using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Json;

namespace JsonTests;

[TestFixture]
public class JsonTreeTests
{
    private readonly JsonWriterOptions _options = new() { Indented = true };

    [Test]
    public void Large_json()
    {
        var jsonString = File.ReadAllText("example.json", Encoding.UTF8);
        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        nodes.Should().HaveCount(564);
        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Simple_flat_object()
    {
        const string jsonString = """
                                  {"a":1,"b":true}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$.a");
        nodes[1].Identity.Should().Be("$.b");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Nested_object()
    {
        const string jsonString = """
                                  {"person":{"name":"Alice","age":30}}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$.person");
        nodes[1].Identity.Should().Be("$.person.name");
        nodes[2].Identity.Should().Be("$.person.age");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Array_of_primitives()
    {
        const string jsonString = """
                                  {"numbers":[10,20,30]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$.numbers");
        nodes[1].Identity.Should().Be("$.numbers[0]");
        nodes[2].Identity.Should().Be("$.numbers[1]");
        nodes[3].Identity.Should().Be("$.numbers[2]");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Array_of_objects()
    {
        const string jsonString = """
                                  {"users":[{"id":1},{"id":2}]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$.users");
        nodes[1].Identity.Should().Be("$.users[0]");
        nodes[2].Identity.Should().Be("$.users[0].id");
        nodes[3].Identity.Should().Be("$.users[1]");
        nodes[4].Identity.Should().Be("$.users[1].id");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Mixed_types_inside_array()
    {
        const string jsonString = """
                                  {"mixed":[1,"two",{"three":3}]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$.mixed");
        nodes[1].Identity.Should().Be("$.mixed[0]");
        nodes[2].Identity.Should().Be("$.mixed[1]");
        nodes[3].Identity.Should().Be("$.mixed[2]");
        nodes[4].Identity.Should().Be("$.mixed[2].three");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Invalid_java_script_identifiers()
    {
        const string jsonString = """
                                  {"first name":"Alice","123key":"value","key.with.dot":"ok"}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$['first name']");
        nodes[1].Identity.Should().Be("$['123key']");
        nodes[2].Identity.Should().Be("$['key.with.dot']");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Nested_arrays()
    {
        const string jsonString = """
                                  {"matrix":[[1,2],[3,4]]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$.matrix");
        nodes[1].Identity.Should().Be("$.matrix[0]");
        nodes[2].Identity.Should().Be("$.matrix[0][0]");
        nodes[3].Identity.Should().Be("$.matrix[0][1]");
        nodes[4].Identity.Should().Be("$.matrix[1]");
        nodes[5].Identity.Should().Be("$.matrix[1][0]");
        nodes[6].Identity.Should().Be("$.matrix[1][1]");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Nulls_booleans_numbers_strings()
    {
        const string jsonString = """
                                  {"n":null,"b":false,"s":"hi","x":42}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$.n");
        nodes[1].Identity.Should().Be("$.b");
        nodes[2].Identity.Should().Be("$.s");
        nodes[3].Identity.Should().Be("$.x");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Empty_object_and_array()
    {
        const string jsonString = """
                                  {"emptyObj":{},"emptyArr":[]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$.emptyObj");
        nodes[1].Identity.Should().Be("$.emptyArr");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void Deep_nesting()
    {
        const string jsonString = """
                                  {"a":{"b":{"c":[{"d":1}]}}}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var nodes = tree.Flatten().ToArray();

        TestContext.Progress.WriteLine(tree);
        TestContext.Progress.WriteLine(tree.ToJsonString(_options));

        nodes[0].Identity.Should().Be("$.a");
        nodes[1].Identity.Should().Be("$.a.b");
        nodes[2].Identity.Should().Be("$.a.b.c");
        nodes[3].Identity.Should().Be("$.a.b.c[0]");
        nodes[4].Identity.Should().Be("$.a.b.c[0].d");

        tree.ToJsonString().Should().Be(jsonString);
    }

    [Test]
    public void FindNode_returns_top_level_property()
    {
        const string jsonString = """
                                  {"a":1,"b":true}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.a");

        node.Should().NotBeNull();
        node.Name.Should().Be("a");
        node.Value.Should().Be("1");
        node.Kind.Should().Be(JsonValueKind.Number);
    }

    [Test]
    public void FindNode_returns_nested_property()
    {
        const string jsonString = """
                                  {"person":{"name":"Alice","age":30}}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.person.name");

        node.Should().NotBeNull();
        node.Value.Should().Be("Alice");
        node.Kind.Should().Be(JsonValueKind.String);
    }

    [Test]
    public void FindNode_returns_array_element()
    {
        const string jsonString = """
                                  {"numbers":[10,20,30]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.numbers[1]");

        node.Should().NotBeNull();
        node.Value.Should().Be("20");
        node.InArray.Should().BeTrue();
    }

    [Test]
    public void FindNode_returns_property_inside_array_object()
    {
        const string jsonString = """
                                  {"users":[{"id":1},{"id":2}]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.users[1].id");

        node.Should().NotBeNull();
        node.Value.Should().Be("2");
    }

    [Test]
    public void FindNode_returns_node_in_deeply_nested_structure()
    {
        const string jsonString = """
                                  {"a":{"b":{"c":[{"d":1}]}}}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.a.b.c[0].d");

        node.Should().NotBeNull();
        node.Value.Should().Be("1");
    }

    [Test]
    public void FindNode_returns_node_with_bracket_notation_for_invalid_identifier()
    {
        const string jsonString = """
                                  {"first name":"Alice"}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$['first name']");

        node.Should().NotBeNull();
        node.Value.Should().Be("Alice");
    }

    [Test]
    public void FindNode_is_case_insensitive()
    {
        const string jsonString = """
                                  {"Person":{"Name":"Alice"}}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.PERSON.name");

        node.Should().NotBeNull();
        node.Value.Should().Be("Alice");
    }

    [Test]
    public void FindNode_returns_object_node()
    {
        const string jsonString = """
                                  {"person":{"name":"Alice","age":30}}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.person");

        node.Should().NotBeNull();
        node.Kind.Should().Be(JsonValueKind.Object);
        node.Children.Should().HaveCount(2);
    }

    [Test]
    public void FindNode_returns_array_node()
    {
        const string jsonString = """
                                  {"numbers":[10,20,30]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.numbers");

        node.Should().NotBeNull();
        node.Kind.Should().Be(JsonValueKind.Array);
        node.Children.Should().HaveCount(3);
    }

    [Test]
    public void FindNode_returns_null_when_identity_not_found()
    {
        const string jsonString = """
                                  {"a":1,"b":2}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.does_not_exist");

        node.Should().BeNull();
    }

    [Test]
    public void FindNode_returns_null_for_root_identity()
    {
        const string jsonString = """
                                  {"a":1}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$");

        node.Should().BeNull();
    }

    [Test]
    public void FindNode_returns_null_for_empty_identity()
    {
        const string jsonString = """
                                  {"a":1}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode(string.Empty);

        node.Should().BeNull();
    }

    [Test]
    public void FindNode_returns_null_when_path_is_partially_correct()
    {
        const string jsonString = """
                                  {"a":{"b":1}}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.a.b.c");

        node.Should().BeNull();
    }

    [Test]
    public void FindNode_returns_null_for_array_index_out_of_range()
    {
        const string jsonString = """
                                  {"numbers":[10,20]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.numbers[5]");

        node.Should().BeNull();
    }

    [Test]
    public void FindNode_finds_null_value_node()
    {
        const string jsonString = """
                                  {"n":null}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.n");

        node.Should().NotBeNull();
        node.Kind.Should().Be(JsonValueKind.Null);
        node.Value.Should().BeNull();
    }

    [Test]
    public void FindNode_finds_element_in_nested_array()
    {
        const string jsonString = """
                                  {"matrix":[[1,2],[3,4]]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.matrix[1][0]");

        node.Should().NotBeNull();
        node.Value.Should().Be("3");
    }

    [Test]
    public void Node_ToString_returns_expected_format()
    {
        const string jsonString = """
                                  {"name":"Alice"}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.name");

        node.Should().NotBeNull();
        node.ToString().Should().Be("Name = name, Value = Alice, Kind = String, Identity = $.name");
    }

    [Test]
    public void ToJsonString_throws_when_node_kind_is_undefined()
    {
        const string jsonString = """
                                  {"a":1}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.a");

        node.Should().NotBeNull();
        node.Kind = JsonValueKind.Undefined;

        var act = () => tree.ToJsonString();

        act.Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage("Node '$.a' has Kind = Undefined and cannot be written.");
    }

    [Test]
    public void Create_throws_when_json_exceeds_default_max_depth()
    {
        var jsonString = BuildNestedJson(JsonTree.DefaultMaxDepth + 1);

        var act = () => JsonTree.Create(jsonString);

        act.Should().Throw<JsonException>();
    }

    [Test]
    public void Create_accepts_json_at_default_max_depth()
    {
        var jsonString = BuildNestedJson(JsonTree.DefaultMaxDepth);

        var act = () => JsonTree.Create(jsonString);

        act.Should().NotThrow();
    }

    [Test]
    public void Create_respects_custom_max_depth()
    {
        var jsonString = BuildNestedJson(10);

        var act = () => JsonTree.Create(jsonString, maxDepth: 5);

        act.Should().Throw<JsonException>();
    }

    [Test]
    public void ToJsonString_applies_relaxed_encoder_when_options_lack_one()
    {
        const string jsonString = """
                                  {"html":"<b>&hello</b>"}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var customOptions = new JsonWriterOptions { Indented = false };

        var output = tree.ToJsonString(customOptions);

        output.Should().Contain("<b>&hello</b>");
        output.Should().NotContain("\\u003C");
    }

    [Test]
    public void ToJsonString_respects_explicit_encoder_in_options()
    {
        const string jsonString = """
                                  {"html":"<b>"}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var strictOptions = new JsonWriterOptions { Encoder = JavaScriptEncoder.Default };

        var output = tree.ToJsonString(strictOptions);

        output.Should().Contain("\\u003C");
    }

    [Test]
    public void Name_is_raw_key_for_invalid_identifier()
    {
        const string jsonString = """
                                  {"first name":"Alice"}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$['first name']");

        node.Should().NotBeNull();
        node.Name.Should().Be("first name");
        node.PathSegment.Should().Be("['first name']");
        node.Identity.Should().Be("$['first name']");
    }

    [Test]
    public void Name_is_raw_index_for_array_element()
    {
        const string jsonString = """
                                  {"numbers":[10,20]}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.numbers[1]");

        node.Should().NotBeNull();
        node.Name.Should().Be("1");
        node.PathSegment.Should().Be("[1]");
        node.Identity.Should().Be("$.numbers[1]");
    }

    [Test]
    public void Name_equals_path_segment_for_valid_identifier()
    {
        const string jsonString = """
                                  {"name":"Alice"}
                                  """;

        var tree = JsonTree.Create(jsonString);
        var node = tree.FindNode("$.name");

        node.Should().NotBeNull();
        node.Name.Should().Be("name");
        node.PathSegment.Should().Be("name");
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
