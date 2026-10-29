using System.Text.Json;
using DotNetLab.Json;

namespace DotNetLab.JsonTests;

public class JsonTreeBuilderTests
{
    [Test]
    public void Object_tree_can_be_built_programmatically()
    {
        var tree = JsonTree.Create()
            .AddObject("person", person => person
                .Add("Name", "Molly")
                .Add("Age", 30)
                .AddObject("Address", address => address
                    .Add("Country", "Sweden")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.FindNode("$.person.Name")!.Value, Is.EqualTo("Molly"));
            Assert.That(tree.FindNode("$.person.Age")!.Value, Is.EqualTo(30L));
            Assert.That(tree.FindNode("$.person.Address.Country")!.Kind, Is.EqualTo(JsonValueKind.String));

            Assert.That(tree.ToJsonString(), Is.EqualTo("""{"person":{"Name":"Molly","Age":30,"Address":{"Country":"Sweden"}}}"""));
        }
    }

    [Test]
    public void Root_object_can_be_configured_with_lambda()
    {
        var tree = JsonTree.Create()
            .Add(person => person
                .Add("Name", "Molly")
                .Add("Age", 30)
                .AddObject("Address", address => address
                    .Add("Country", "Sweden")));

        Assert.That(tree.ToJsonString(), Is.EqualTo("""{"Name":"Molly","Age":30,"Address":{"Country":"Sweden"}}"""));
    }

    [Test]
    public void Builder_supports_all_json_value_kinds()
    {
        var tree = JsonTree.Create()
            .Add("string", "value")
            .Add("integer", 30)
            .Add("decimal", 12.34m)
            .Add("double", 56.78d)
            .Add("trueValue", true)
            .Add("falseValue", false)
            .AddNull("nullValue")
            .AddObject("objectValue", obj => obj.Add("nested", "yes"))
            .AddArray("arrayValue", array => array.Add("item"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.FindNode("$.string")!.Kind, Is.EqualTo(JsonValueKind.String));
            Assert.That(tree.FindNode("$.integer")!.Kind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(tree.FindNode("$.trueValue")!.Kind, Is.EqualTo(JsonValueKind.True));
            Assert.That(tree.FindNode("$.falseValue")!.Kind, Is.EqualTo(JsonValueKind.False));
            Assert.That(tree.FindNode("$.nullValue")!.Kind, Is.EqualTo(JsonValueKind.Null));
            Assert.That(tree.FindNode("$.objectValue")!.Kind, Is.EqualTo(JsonValueKind.Object));
            Assert.That(tree.FindNode("$.arrayValue")!.Kind, Is.EqualTo(JsonValueKind.Array));

            Assert.That(tree.ToJsonString(), Is.EqualTo("""{"string":"value","integer":30,"decimal":12.34,"double":56.78,"trueValue":true,"falseValue":false,"nullValue":null,"objectValue":{"nested":"yes"},"arrayValue":["item"]}"""));
        }
    }

    [Test]
    public void Builder_supports_ulong_values()
    {
        var tree = JsonTree.Create().Add("value", ulong.MaxValue);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.FindNode("$.value")!.Value, Is.EqualTo(18446744073709551615m));
            Assert.That(tree.ToJsonString(), Is.EqualTo("""{"value":18446744073709551615}"""));
        }
    }

    [Test]
    public void Builder_supports_float_values()
    {
        var tree = JsonTree.Create().Add("value", 12.5f);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.FindNode("$.value")!.Value, Is.EqualTo(12.5d));
            Assert.That(tree.ToJsonString(), Is.EqualTo("""{"value":12.5}"""));
        }
    }

    [Test]
    public void Unsupported_value_type_cannot_be_added()
    {
        var tree = JsonTree.Create();

        Assert.That(() => tree.Add("value", DateTime.UnixEpoch), Throws.TypeOf<ArgumentException>()
            .With.Message.EqualTo("Unsupported JSON value type: System.DateTime (Parameter 'value')"));
    }

    [Test]
    public void Root_array_can_be_built_programmatically()
    {
        var tree = JsonTree.CreateArray()
            .Add(array => array
                .Add("Molly")
                .Add(30)
                .Add(true)
                .Add(false)
                .AddNull()
                .AddObject(person => person.Add("Name", "Molly"))
                .AddArray(values => values.Add("nested")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(tree.FindNode("$[0]")!.Value, Is.EqualTo("Molly"));
            Assert.That(tree.FindNode("$[5].Name")!.Value, Is.EqualTo("Molly"));
            Assert.That(tree.FindNode("$[6][0]")!.Value, Is.EqualTo("nested"));

            Assert.That(tree.ToJsonString(), Is.EqualTo("""["Molly",30,true,false,null,{"Name":"Molly"},["nested"]]"""));
        }
    }

    [Test]
    public void Lookup_cache_is_invalidated_when_tree_is_mutated()
    {
        var tree = JsonTree.Create().Add("Name", "Molly");

        Assert.That(tree.FindNode("$.Age"), Is.Null);

        tree.Add("Age", 30);

        Assert.That(tree.FindNode("$.Age"), Is.Not.Null);
    }

    [Test]
    public void Property_cannot_be_added_to_array_node()
    {
        var tree = JsonTree.CreateArray();

        Assert.That(() => tree.Add(array => array.Add("Name", "Molly")), Throws.TypeOf<InvalidOperationException>()
            .With.Message.EqualTo("Node '$' has Kind = Array; object properties can only be added to object nodes."));
    }

    [Test]
    public void Array_item_cannot_be_added_to_object_node()
    {
        var tree = JsonTree.Create();

        Assert.That(() => tree.Add(root => root.Add("Molly")), Throws.TypeOf<InvalidOperationException>()
            .With.Message.EqualTo("Node '$' has Kind = Object; array items can only be added to array nodes."));
    }
}
