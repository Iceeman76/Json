using System.Text;
using System.Text.Json;
using DotNetLab.Json;

namespace DotNetLab.JsonTests;

public sealed class JsonReviewRegressionTests
{
    [Test]
    public void Rejected_object_value_leaves_the_tree_unchanged()
    {
        var tree = JsonTree.Create().Add("valid", 1);
        var nodes = tree.Flatten();

        Assert.That(() => tree.Add("invalid", DateTime.UnixEpoch), Throws.ArgumentException);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.ToJsonString(), Is.EqualTo("""{"valid":1}"""));
            Assert.That(tree.Flatten(), Is.SameAs(nodes));
        }

        tree.Add("next", 2);

        Assert.That(tree.ToJsonString(), Is.EqualTo("""{"valid":1,"next":2}"""));
    }

    [Test]
    public void Rejected_array_value_does_not_consume_an_index()
    {
        var tree = JsonTree.CreateArray().Add(array => array.Add(1));

        Assert.That(() => tree.Add(array => array.Add(DateTime.UnixEpoch)), Throws.ArgumentException);
        Assert.That(tree.ToJsonString(), Is.EqualTo("[1]"));

        tree.Add(array => array.Add(2));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.ToJsonString(), Is.EqualTo("[1,2]"));
            Assert.That(tree.FindNode("$[1]")!.Value, Is.EqualTo(2L));
            Assert.That(tree.FindNode("$[1]")!.JsonPath, Is.EqualTo("$[1]"));
        }
    }

    [TestCase("{}")]
    [TestCase("[]")]
    [TestCase("42")]
    [TestCase("1e400")]
    [TestCase("null")]
    [TestCase("true")]
    [TestCase("false")]
    [TestCase("\"hello\"")]
    [TestCase("{\"items\":[1,{\"nested\":true}]}")]
    [TestCase("[1,{\"items\":[null,\"hello\"]}]")]
    public void Root_can_be_written_directly(string json)
    {
        var tree = JsonTree.Parse(json);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        tree.FindNode("$")!.BuildRecursiveJson(writer);
        writer.Flush();

        Assert.That(Encoding.UTF8.GetString(stream.ToArray()), Is.EqualTo(json));
    }

    [TestCase("[{\"a\":1,\"a\":2},{\"a\":2.0,\"a\":1.0}]", "[{\"a\":1,\"a\":2}]")]
    [TestCase("[{\"a\":1,\"a\":2},{\"a\":1,\"a\":1}]", "[{\"a\":1,\"a\":2},{\"a\":1,\"a\":1}]")]
    [TestCase("[{\"a\":1,\"a\":1},{\"a\":1}]", "[{\"a\":1,\"a\":1},{\"a\":1}]")]
    [TestCase("[{\"a\":1,\"a\":1,\"a\":2},{\"a\":1,\"a\":2,\"a\":2}]", "[{\"a\":1,\"a\":1,\"a\":2},{\"a\":1,\"a\":2,\"a\":2}]")]
    [TestCase("[{\"a\":1,\"a\":1},{\"A\":1,\"A\":1}]", "[{\"a\":1,\"a\":1},{\"A\":1,\"A\":1}]")]
    [TestCase("[{\"a\":{\"x\":[1,2]},\"a\":null},{\"a\":null,\"a\":{\"x\":[1.0,2.0]}}]", "[{\"a\":{\"x\":[1,2]},\"a\":null}]")]
    public void Distinct_preserves_duplicate_property_semantics(string json, string expected)
    {
        var selector = new JsonFieldSelector(json);

        Assert.That(selector.Select("$[*]|distinct"), Is.EqualTo(expected));
    }

    [Test]
    public void Distinct_compares_wide_objects_regardless_of_property_order()
    {
        var indices = Enumerable.Range(0, 1000).ToArray();
        var firstObject = "{" + string.Join(',', indices.Select(index => $"\"property{index}\":{index}")) + "}";
        var reorderedObject = "{" + string.Join(',', indices.Reverse().Select(index => $"\"property{index}\":{index}")) + "}";
        var selector = new JsonFieldSelector($"[{firstObject},{reorderedObject},{firstObject}]");

        Assert.That(selector.Select("$[*]|distinct"), Is.EqualTo($"[{firstObject}]"));
    }

    [Test]
    public void Distinct_compares_deep_objects_with_reordered_properties()
    {
        var firstObject = "1";
        var reorderedObject = "1.0";

        for (var depth = 0; depth < 32; depth++)
        {
            firstObject = "{\"nested\":" + firstObject + ",\"other\":0}";
            reorderedObject = "{\"other\":0,\"nested\":" + reorderedObject + "}";
        }

        var selector = new JsonFieldSelector($"[{firstObject},{reorderedObject}]");

        Assert.That(selector.Select("$[*]|distinct"), Is.EqualTo($"[{firstObject}]"));
    }
}
