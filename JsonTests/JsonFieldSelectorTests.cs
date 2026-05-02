using System.Text;
using Json;

namespace JsonTests;

[TestFixture]
public class JsonFieldSelectorTests
{
    [Test]
    public void Large_json()
    {
        var jsonString = File.ReadAllText("example.json", Encoding.UTF8);
        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.draw.events[*][eventNumber,description]");

        Console.WriteLine(result);
    }

    [Test]
    public void Should_return_entire_document()
    {
        const string jsonString = """
                                  {"person":{"name":"Alice","age":30}}
                                  """;

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$");

        Assert.That(result, Is.EqualTo(jsonString));
    }

    [Test]
    public void Should_select_nested_object()
    {
        const string jsonString = """
                                  {"person":{"name":"Alice","age":30}}
                                  """;

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.person");

        const string expected = """
                                {"name":"Alice","age":30}
                                """;

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_scalar_value()
    {
        const string jsonString = """
                                  {"person":{"name":"Alice","age":30}}
                                  """;

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.person.age");

        const string expected = "30";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_property_from_array()
    {
        const string jsonString = """
                                  {"crew":{"members":[{"id":1,"name":"Allan"},{"id":2,"name":"Bertil"}]}}
                                  """;

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.crew.members[*].name");

        const string expected = """["Allan","Bertil"]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_using_property_wildcard()
    {
        const string jsonString = """
                                  {"crew":{"members":[{"id":1,"name":"Allan"},{"id":2,"name":"Bertil"}]}}
                                  """;

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.crew.*");

        const string expected = """[{"id":1,"name":"Allan"},{"id":2,"name":"Bertil"}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_from_nested_arrays()
    {
        const string jsonString = """
                                  {"departments":[{"name":"Engineering","members":[{"name":"Alice"},{"name":"Bob"}]},{"name":"Marketing","members":[{"name":"Charlie"},{"name":"Dave"}]}]}
                                  """;

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.departments[*].members[*].name");

        const string expected = """["Alice","Bob","Charlie","Dave"]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_fields_from_single_object()
    {
        const string jsonString = """{"person":{"name":"Alice","age":30,"ssn":"secret"}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.person[name,age]");

        const string expected = """{"name":"Alice","age":30}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_fields_from_root()
    {
        const string jsonString = """{"name":"Alice","age":30,"ssn":"secret"}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[name,age]");

        const string expected = """{"name":"Alice","age":30}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_fields_from_nested_array()
    {
        const string jsonString = """{"store":{"book":[{"title":"Sayings of the Century","price":8.95},{"title":"Sword of Honour","price":12.99},{"title":"Moby Dick","price":8.99},{"title":"The Lord of the Rings","price":22.99}]}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.store.book[*][title,price]");

        const string expected = """[{"title":"Sayings of the Century","price":8.95},{"title":"Sword of Honour","price":12.99},{"title":"Moby Dick","price":8.99},{"title":"The Lord of the Rings","price":22.99}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_from_array_elements()
    {
        const string jsonString = """
                                  {"crew":[{"id":1,"name":"Allan","age":50},{"id":2,"name":"Bertil","age":75}]}
                                  """;

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.crew[*][id,name]");

        const string expected = """[{"id":1,"name":"Allan"},{"id":2,"name":"Bertil"}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_be_case_insensitive()
    {
        const string jsonString = """{"name":"Alice","age":30,"ssn":"secret"}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[NAME,AGE]");

        const string expected = """{"name":"Alice","age":30}""";

        Assert.That(result, Is.EqualTo(expected));
    }
}
