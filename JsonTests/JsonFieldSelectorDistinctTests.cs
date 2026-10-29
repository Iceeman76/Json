using DotNetLab.Json;

namespace DotNetLab.JsonTests;

public class JsonFieldSelectorDistinctTests
{
    [Test]
    public void Should_remove_duplicate_scalar_values_and_preserve_first_occurrence_order()
    {
        var result = Select("""{"values":["b","a","b","c","a"]}""", "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("""["b","a","c"]"""));
    }

    [Test]
    public void Should_compare_numbers_by_numeric_value()
    {
        var result = Select("""{"values":[1,1.0,1e0,2.0,2]}""", "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("[1,2.0]"));
    }

    [Test]
    public void Should_keep_values_of_different_json_types_distinct()
    {
        var result = Select("""{"values":[0,null,false,1,"1",true,"true",null,false]}""", "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("""[0,null,false,1,"1",true,"true"]"""));
    }

    [Test]
    public void Should_compare_strings_case_sensitively()
    {
        var result = Select("""{"values":["a","A","a"]}""", "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("""["a","A"]"""));
    }

    [Test]
    public void Should_compare_arrays_structurally_and_in_order()
    {
        var result = Select("""{"values":[[1,2],[1,2],[2,1],[],[]]}""", "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("[[1,2],[2,1],[]]"));
    }

    [Test]
    public void Should_compare_objects_structurally_regardless_of_property_order()
    {
        const string json = """{"values":[{"a":1,"b":2},{"b":2,"a":1},{"a":1,"b":3}]}""";

        var result = Select(json, "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("""[{"a":1,"b":2},{"a":1,"b":3}]"""));
    }

    [Test]
    public void Should_compare_object_property_names_case_sensitively()
    {
        var result = Select("""{"values":[{"name":1},{"Name":1},{"name":1}]}""", "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("""[{"name":1},{"Name":1}]"""));
    }

    [Test]
    public void Should_compare_nested_objects_and_arrays_structurally()
    {
        const string json = """{"values":[{"x":{"a":1},"items":[1,2]},{"items":[1,2],"x":{"a":1}},{"x":{"a":1},"items":[2,1]}]}""";

        var result = Select(json, "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("""[{"x":{"a":1},"items":[1,2]},{"x":{"a":1},"items":[2,1]}]"""));
    }

    [Test]
    public void Should_apply_distinct_to_recursive_descent_results()
    {
        const string json = """{"a":{"name":"Alice"},"b":{"name":"Bob"},"c":{"name":"Alice"}}""";

        var result = Select(json, "$..name|distinct");

        Assert.That(result, Is.EqualTo("""["Alice","Bob"]"""));
    }

    [Test]
    public void Should_apply_distinct_before_recursive_alias_is_written()
    {
        const string json = """{"a":{"price":10},"b":{"price":20},"c":{"price":10}}""";

        var result = Select(json, "$..[cost:price]|distinct");

        Assert.That(result, Is.EqualTo("""[{"cost":10},{"cost":20}]"""));
    }

    [Test]
    public void Should_compare_the_projected_result_instead_of_the_source_object()
    {
        const string json = """{"items":[{"id":1,"name":"A"},{"id":1,"name":"B"},{"id":2,"name":"C"}]}""";

        var result = Select(json, "$.items[*][id]|distinct");

        Assert.That(result, Is.EqualTo("""[{"id":1},{"id":2}]"""));
    }

    [Test]
    public void Should_compare_nested_projected_results()
    {
        const string json = """{"items":[{"user":{"id":1,"name":"A"}},{"user":{"id":1,"name":"B"}},{"user":{"id":2,"name":"A"}}]}""";

        var result = Select(json, "$.items[*][user[id]]|distinct");

        Assert.That(result, Is.EqualTo("""[{"user":{"id":1}},{"user":{"id":2}}]"""));
    }

    [Test]
    public void Should_include_projected_array_contents_in_comparison()
    {
        const string json = """{"items":[{"tags":[{"id":1},{"id":2}]},{"tags":[{"id":1},{"id":2}]},{"tags":[{"id":2},{"id":1}]}]}""";

        var result = Select(json, "$.items[*][tags[id]]|distinct");

        Assert.That(result, Is.EqualTo("""[{"tags":[{"id":1},{"id":2}]},{"tags":[{"id":2},{"id":1}]}]"""));
    }

    [Test]
    public void Should_treat_equally_missing_projected_fields_as_duplicates()
    {
        const string json = """{"items":[{"id":1},{"id":2},{"name":"A"},{"name":"B"}]}""";

        var result = Select(json, "$.items[*][name]|distinct");

        Assert.That(result, Is.EqualTo("""[{}, {"name":"A"},{"name":"B"}]""".Replace(" ", "")));
    }

    [Test]
    public void Should_keep_missing_and_null_projected_fields_distinct()
    {
        const string json = """{"items":[{}, {"value":null},{}]}""";

        var result = Select(json, "$.items[*][value]|distinct");

        Assert.That(result, Is.EqualTo("""[{}, {"value":null}]""".Replace(" ", "")));
    }

    [Test]
    public void Should_return_empty_array_when_nothing_matches()
    {
        var result = Select("""{"value":1}""", "$..missing|distinct");

        Assert.That(result, Is.EqualTo("[]"));
    }

    [Test]
    public void Should_leave_a_single_non_nodelist_result_unwrapped()
    {
        var result = Select("""{"value":1}""", "$.value|distinct");

        Assert.That(result, Is.EqualTo("1"));
    }

    [Test]
    public void Should_support_distinct_on_a_root_array()
    {
        var result = Select("[1,2,1,3,2]", "$[*]|distinct");

        Assert.That(result, Is.EqualTo("[1,2,3]"));
    }

    [Test]
    public void Should_allow_whitespace_around_distinct_operator()
    {
        var result = Select("""{"values":[1,1,2]}""", " $.values[*] | distinct ");

        Assert.That(result, Is.EqualTo("[1,2]"));
    }

    [Test]
    public void Should_keep_objects_with_different_property_counts_on_hash_collision()
    {
        const string json = """{"values":[{}, {"value":1,"value":1}]}""";

        var result = Select(json, "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("""[{}, {"value":1,"value":1}]""".Replace(" ", "")));
    }

    [Test]
    public void Should_keep_objects_with_different_properties_on_hash_collision()
    {
        const string json = """{"values":[{"left":1,"left":1},{"right":1,"right":1}]}""";

        var result = Select(json, "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("""[{"left":1,"left":1},{"right":1,"right":1}]"""));
    }

    [Test]
    public void Should_compare_numbers_outside_the_decimal_range_as_doubles()
    {
        var result = Select("""{"values":[1e100,1e100,2e100]}""", "$.values[*]|distinct");

        Assert.That(result, Is.EqualTo("[1E+100,2E+100]"));
    }

    [TestCase("$.values[*]|distinctly")]
    [TestCase("$.values[*]|distinct|distinct")]
    [TestCase("$.values[*]|Distinct")]
    [TestCase("$.values[*]|")]
    public void Should_reject_invalid_distinct_syntax(string expression)
    {
        var selector = new JsonFieldSelector("""{"values":[1,2]}""");

        Assert.That(() => selector.Select(expression), Throws.TypeOf<FormatException>());
    }

    private static string Select(string json, string expression) => new JsonFieldSelector(json).Select(expression);
}
