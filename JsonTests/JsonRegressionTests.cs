using DotNetLab.Json;

namespace DotNetLab.JsonTests;

public sealed class JsonRegressionTests
{
    [TestCase("42")]
    [TestCase("null")]
    [TestCase("true")]
    [TestCase("false")]
    [TestCase("\"hello\"")]
    public void Round_trips_scalar_roots(string json)
    {
        Assert.That(JsonTree.Parse(json).ToJsonString(), Is.EqualTo(json));
    }

    [TestCase("name", "1")]
    [TestCase("Name", "2")]
    [TestCase("NAME", "1")]
    [TestCase("other", "3")]
    public void Prefers_exact_names_and_falls_back_to_the_first_case_insensitive_match(string name, string expected)
    {
        const string json = """{"name":1,"Name":2,"other":3}""";
        var tree = JsonTree.Parse(json);

        Assert.That(new JsonFieldSelector(tree).Select($"$.{name}"), Is.EqualTo(expected));
        Assert.That(tree.FindNode($"$.{name}")!.Value!.ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void Uses_the_same_parent_resolution_for_tree_and_selector()
    {
        const string json = """{"person":{"name":1},"Person":{"other":2}}""";
        var tree = JsonTree.Parse(json);

        Assert.That(tree.FindNode("$.person.other"), Is.Null);
        Assert.That(new JsonFieldSelector(tree).Select("$.person.other"), Is.EqualTo("[]"));
    }

    [Test]
    public void Repeated_identical_property_names_resolve_to_the_first_occurrence()
    {
        var tree = JsonTree.Parse("""{"name":1,"name":2}""");

        Assert.That(tree.FindNode("$.name")!.Value, Is.EqualTo(1));
        Assert.That(new JsonFieldSelector(tree).Select("$[name]"), Is.EqualTo("""{"name":1}"""));
    }

    [Test]
    public void Recursive_quoted_names_match_properties_instead_of_array_indices()
    {
        var selector = new JsonFieldSelector("""{"0":1,"items":[2,{"0":3}]}""");

        Assert.That(selector.Select("$..['0']"), Is.EqualTo("[1,3]"));
    }

    [TestCase("first name")]
    [TestCase("a'b")]
    [TestCase("a\"b")]
    [TestCase("a\\b")]
    [TestCase("a\nb")]
    [TestCase("a\tb")]
    [TestCase("")]
    [TestCase("åäö")]
    [TestCase("$value")]
    [TestCase("a.b[*]|distinct")]
    public void Generated_paths_can_be_used_in_both_tree_and_selector(string name)
    {
        var tree = JsonTree.Create().Add(name, 42);
        var path = tree.Flatten()[0].JsonPath;

        Assert.That(tree.FindNode(path)!.Value, Is.EqualTo(42));
        Assert.That(new JsonFieldSelector(tree).Select(path), Is.EqualTo("42"));
    }

    [TestCase(" $ [ 'first name' ] ")]
    [TestCase("$[\"first name\"]")]
    [TestCase("$['first\\u0020name']")]
    public void Preserves_whitespace_inside_quoted_names(string expression)
    {
        var selector = new JsonFieldSelector("""{"first name":"Alice"}""");

        Assert.That(selector.Select(expression), Is.EqualTo("\"Alice\""));
    }

    [TestCase("$['first name',age]", "{\"first name\":\"Alice\",\"age\":30}")]
    [TestCase("$[name:'first name']", "{\"name\":\"Alice\"}")]
    [TestCase("$['full name':'first name']", "{\"full name\":\"Alice\"}")]
    public void Supports_quoted_names_in_projections(string expression, string expected)
    {
        var selector = new JsonFieldSelector("""{"first name":"Alice","age":30}""");

        Assert.That(selector.Select(expression), Is.EqualTo(expected));
    }

    [Test]
    public void Distinct_compares_the_names_written_by_projections()
    {
        var selector = new JsonFieldSelector("""[{"name":1},{"Name":1}]""");

        Assert.That(selector.Select("$[*][name]|distinct"), Is.EqualTo("""[{"name":1},{"Name":1}]"""));
        Assert.That(selector.Select("$[*][value:name]|distinct"), Is.EqualTo("""[{"value":1}]"""));
    }

    [TestCase("{}", "[]")]
    [TestCase("{\"name\":\"Alice\"}", "[\"Alice\"]")]
    [TestCase("{\"items\":[1,2]}", "[[1,2]]")]
    public void Property_wildcards_always_return_an_array(string json, string expected)
    {
        Assert.That(new JsonFieldSelector(json).Select("$.*"), Is.EqualTo(expected));
    }

    [TestCase("$.items[id]|distinct")]
    [TestCase("$.items[*][id]|distinct")]
    public void Distinct_works_for_implicit_and_explicit_array_projections(string expression)
    {
        var selector = new JsonFieldSelector("""{"items":[{"id":1},{"id":1},{"id":2}]}""");

        Assert.That(selector.Select(expression), Is.EqualTo("""[{"id":1},{"id":2}]"""));
    }

    [TestCase("$[items[0]]", "{\"items\":10}")]
    [TestCase("$[first:items[0]]", "{\"first\":10}")]
    [TestCase("$[items[-1]]", "{\"items\":20}")]
    public void Indexed_projection_fields_use_the_array_property_name_unless_aliased(string expression, string expected)
    {
        Assert.That(new JsonFieldSelector("""{"items":[10,20]}""").Select(expression), Is.EqualTo(expected));
    }

    [TestCase("$[-1]")]
    [TestCase("$[0]")]
    [TestCase("$[*]")]
    public void Array_selectors_do_not_select_object_properties(string expression)
    {
        Assert.That(new JsonFieldSelector("""{"name":"Alice","age":30}""").Select(expression), Is.EqualTo("[]"));
    }

    [TestCase("$[-]")]
    [TestCase("$['unterminated]")]
    [TestCase("$['invalid\\x']")]
    [TestCase("$['invalid\\uZZZZ']")]
    public void Invalid_paths_produce_syntax_errors(string expression)
    {
        Assert.That(() => new JsonFieldSelector("{}").Select(expression), Throws.TypeOf<FormatException>());
    }

    [TestCase("0.123456789012345678901234567891")]
    [TestCase("12345678901234567890123456789123456789")]
    [TestCase("1e400")]
    [TestCase("1e-400")]
    public void Preserves_numbers_that_cannot_be_represented_by_builtin_types(string number)
    {
        var json = $"{{\"number\":{number}}}";
        var tree = JsonTree.Parse(json);

        Assert.That(tree.ToJsonString(), Is.EqualTo(json));
        Assert.That(new JsonFieldSelector(tree).Select("$.number"), Is.EqualTo(number));
        Assert.That(tree.FindNode("$.number")!.Value, Is.TypeOf<JsonNumber>());
    }

    [TestCase("[0.123456789012345678901234567890,0.123456789012345678901234567891]")]
    [TestCase("[12345678901234567890123456789123456789,12345678901234567890123456789123456790]")]
    public void Distinct_keeps_numbers_with_different_exact_values(string json)
    {
        Assert.That(new JsonFieldSelector(json).Select("$[*]|distinct"), Is.EqualTo(json));
    }

    [TestCase("[1e400,10e399,2e400]", "[1e400,2e400]")]
    [TestCase("[1e-400,10e-401,2e-400]", "[1e-400,2e-400]")]
    [TestCase("[1,1.0,1e0,-0,0]", "[1,0]")]
    public void Distinct_compares_exact_numeric_values_across_representations(string json, string expected)
    {
        Assert.That(new JsonFieldSelector(json).Select("$[*]|distinct"), Is.EqualTo(expected));
    }

    [Test]
    public void Parsed_exact_numbers_can_be_replaced()
    {
        var tree = JsonTree.Parse("""{"number":1e400}""");
        tree.FindNode("$.number")!.Value = 42L;

        Assert.That(tree.ToJsonString(), Is.EqualTo("""{"number":42}"""));
    }

    [Test]
    public void Builder_accepts_exact_numbers()
    {
        var tree = JsonTree.Create().Add("number", new JsonNumber("1e400"));

        Assert.That(tree.ToJsonString(), Is.EqualTo("""{"number":1e400}"""));
    }
}
