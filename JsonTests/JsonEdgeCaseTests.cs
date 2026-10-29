using System.Text.Json;
using DotNetLab.Json;

namespace DotNetLab.JsonTests;

public sealed class JsonEdgeCaseTests
{
    [Test]
    public void Distinct_compares_projected_properties_when_aliases_share_a_name()
    {
        var selector = new JsonFieldSelector("""[{"a":1},{"b":1}]""");

        Assert.That(selector.Select("$[*][value:a,value:b]|distinct"), Is.EqualTo("""[{"value":1}]"""));
    }

    [Test]
    public void Distinct_compares_arrays_after_their_implicit_projection_is_deduplicated()
    {
        var selector = new JsonFieldSelector("""{"groups":[[{"id":1},{"id":1}],[{"id":1}]]}""");

        Assert.That(selector.Select("$.groups[*][id]|distinct"), Is.EqualTo("""[[{"id":1}]]"""));
    }

    [TestCase("$[items[*]]", "{\"items\":[]}")]
    [TestCase("$[items[*][id]]", "{\"items\":[]}")]
    public void Explicit_array_fields_do_not_enumerate_object_properties(string expression, string expected)
    {
        var selector = new JsonFieldSelector("""{"items":{"first":{"id":1}}}""");

        Assert.That(selector.Select(expression), Is.EqualTo(expected));
    }

    [TestCase("42", "$ = 42")]
    [TestCase("null", "$ = null")]
    [TestCase("{}", "$ = {}")]
    [TestCase("[]", "$ = []")]
    public void Tree_text_includes_scalar_and_empty_roots(string json, string expected)
    {
        Assert.That(JsonTree.Parse(json).ToString(), Does.StartWith(expected));
    }

    [TestCase("null")]
    [TestCase("true")]
    [TestCase("\"text\"")]
    [TestCase("{}")]
    [TestCase("[]")]
    public void Exact_numbers_reject_values_of_other_json_types(string text)
    {
        Assert.That(() => new JsonNumber(text), Throws.ArgumentException);
    }

    [TestCase("1e")]
    [TestCase("01")]
    [TestCase("NaN")]
    [TestCase("1,2")]
    public void Exact_numbers_reject_invalid_json(string text)
    {
        Assert.That(() => new JsonNumber(text), Throws.InstanceOf<JsonException>());
    }

    [Test]
    public void Exact_numbers_accept_surrounding_whitespace_without_changing_the_value()
    {
        var number = new JsonNumber("  1e400 \r\n");

        Assert.That(number.Text, Is.EqualTo("1e400"));
        Assert.That(JsonTree.Create().Add("number", number).ToJsonString(), Is.EqualTo("""{"number":1e400}"""));
    }

    [TestCase("$.first name")]
    [TestCase("$[first name]")]
    [TestCase("$.items[1 0]")]
    [TestCase("$.items[- 1]")]
    [TestCase("$.items[*]|dis tinct")]
    public void Whitespace_does_not_silently_join_separate_tokens(string expression)
    {
        var selector = new JsonFieldSelector("""{"firstname":42,"items":[1,2]}""");

        Assert.That(() => selector.Select(expression), Throws.TypeOf<FormatException>());
    }

    [TestCase("$[*][value:a,value:b]")]
    [TestCase("$[*][value:items[*]]")]
    [TestCase("$[*][value:items[*][id]]")]
    [TestCase("$[*][value:items[id]]")]
    [TestCase("$[*][value:items]")]
    public void Distinct_matches_the_objects_actually_written_by_the_projection(string expression)
    {
        const string json = """[{"a":1},{"b":1},{"items":[{"id":1,"extra":2}]},{"items":[{"id":1,"extra":3}]},{"items":{}},{"items":null},{}]""";
        var selector = new JsonFieldSelector(json);
        using var projectedDocument = JsonDocument.Parse(selector.Select(expression));
        var expected = new List<JsonElement>();

        foreach (var element in projectedDocument.RootElement.EnumerateArray())
        {
            if (expected.Any(previous => JsonElement.DeepEquals(previous, element)) is false)
            {
                expected.Add(element);
            }
        }

        using var actualDocument = JsonDocument.Parse(selector.Select(expression + "|distinct"));
        using var expectedDocument = JsonDocument.Parse(JsonSerializer.Serialize(expected));

        Assert.That(JsonElement.DeepEquals(actualDocument.RootElement, expectedDocument.RootElement), Is.True);
    }
}
