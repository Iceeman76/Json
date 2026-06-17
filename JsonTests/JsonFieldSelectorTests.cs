using System.Buffers;
using System.Text;
using System.Text.Json;
using Json;

namespace JsonTests;

public class JsonFieldSelectorTests
{
    [Test]
    public void Large_json()
    {
        var jsonString = File.ReadAllText("example.json", Encoding.UTF8);
        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[id:requestId,info:draw[turnover,comment:drawComment,matches:events[number:eventNumber,country:league.country.name,home:participants[0].name,away:participants[1].name]]]");
        var tree = JsonTree.Parse(result);

        TestContext.Progress.WriteLine(tree.ToJsonString(new JsonWriterOptions { Indented = true }));
    }

    [Test]
    public void Should_project_array_elements_inside_object_sub_projection()
    {
        const string jsonString = """{"requestId":"abc","draw":{"drawComment":"v1","events":[{"eventNumber":1,"description":"A","extra":"skip"},{"eventNumber":2,"description":"B","extra":"skip"}]}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[requestId,draw[drawComment,events[*][eventNumber,description]]]");

        const string expected = """{"requestId":"abc","draw":{"drawComment":"v1","events":[{"eventNumber":1,"description":"A"},{"eventNumber":2,"description":"B"}]}}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_auto_detect_array_in_sub_projection()
    {
        const string jsonString = """{"requestId":"abc","draw":{"drawComment":"v1","events":[{"eventNumber":1,"description":"A","extra":"skip"},{"eventNumber":2,"description":"B","extra":"skip"}]}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[requestId,draw[drawComment,events[eventNumber,description]]]");

        const string expected = """{"requestId":"abc","draw":{"drawComment":"v1","events":[{"eventNumber":1,"description":"A"},{"eventNumber":2,"description":"B"}]}}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_auto_detect_array_at_root_sub_projection()
    {
        const string jsonString = """{"crew":[{"id":1,"name":"Allan","age":50},{"id":2,"name":"Bertil","age":75}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.crew[id,name]");

        const string expected = """[{"id":1,"name":"Allan"},{"id":2,"name":"Bertil"}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_three_levels_deep()
    {
        const string jsonString = """{"a":{"b":{"c":{"x":1,"y":2,"z":"skip"},"other":"skip"},"more":"skip"}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.a[b[c[x,y]]]");

        const string expected = """{"b":{"c":{"x":1,"y":2}}}""";

        Assert.That(result, Is.EqualTo(expected));
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
    public void Should_select_single_field_with_bracket_notation()
    {
        const string jsonString = """
                                  {"itemCount":12}
                                  """;

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[itemCount]");

        const string expected = """
                                {"itemCount":12}
                                """;

        Assert.That(result, Is.EqualTo(expected));
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
    public void Should_select_all_properties_with_wildcard_on_root()
    {
        const string jsonString = """{"name":"Alice","age":30}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.*");

        const string expected = """["Alice",30]""";

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

    [Test]
    public void Should_project_root_fields_with_wildcard_sub_selector()
    {
        const string jsonString = """{"requestId":"abc-123","draw":[{"id":1,"color":"red"},{"id":2,"color":"blue"}],"extra":"skip"}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[requestId,draw[*]]");

        const string expected = """{"requestId":"abc-123","draw":[{"id":1,"color":"red"},{"id":2,"color":"blue"}]}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_root_fields_with_nested_sub_projection()
    {
        const string jsonString = """{"count":12,"entries":[{"id":"a1","label":"Item A","active":true,"details":{"id":"d1","name":"Detail One","description":"Some description"}}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[count,entries[*][id,label,details[id,name]]]");

        const string expected = """{"count":12,"entries":[{"id":"a1","label":"Item A","details":{"id":"d1","name":"Detail One"}}]}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_sub_fields_from_nested_object()
    {
        const string jsonString = """{"order":{"id":1,"customer":{"name":"Alice","email":"a@b.com","phone":"123"},"total":100}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.order[id,customer[name,email]]");

        const string expected = """{"id":1,"customer":{"name":"Alice","email":"a@b.com"}}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_array_elements_with_sub_projection_on_nested_field()
    {
        const string jsonString = """{"people":[{"name":"Alice","address":{"city":"Stockholm","zip":"111"}},{"name":"Bob","address":{"city":"Göteborg","zip":"222"}}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.people[*][name,address[city]]");

        const string expected = """[{"name":"Alice","address":{"city":"Stockholm"}},{"name":"Bob","address":{"city":"Göteborg"}}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_multiple_fields_with_wildcard_sub_selectors()
    {
        const string jsonString = """{"a":"keep","b":[1,2,3],"c":[4,5],"d":"skip"}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[a,b[*],c[*]]");

        const string expected = """{"a":"keep","b":[1,2,3],"c":[4,5]}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_alias_plain_field()
    {
        const string jsonString = """{"name":"Alice","age":30}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[displayName:name,age]");

        const string expected = """{"displayName":"Alice","age":30}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_alias_dotted_path()
    {
        const string jsonString = """{"events":[{"description":"A","league":{"name":"Premier League"}},{"description":"B","league":{"name":"Championship"}}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.events[description,league:league.name]");

        const string expected = """[{"description":"A","league":"Premier League"},{"description":"B","league":"Championship"}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_alias_object_sub_projection()
    {
        const string jsonString = """{"person":{"name":"Alice","age":30,"ssn":"secret"}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[info:person[name,age]]");

        const string expected = """{"info":{"name":"Alice","age":30}}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_alias_array_projection()
    {
        const string jsonString = """{"crew":[{"id":1,"name":"Allan","age":50},{"id":2,"name":"Bertil","age":75}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[members:crew[id,name]]");

        const string expected = """{"members":[{"id":1,"name":"Allan"},{"id":2,"name":"Bertil"}]}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_array_element_by_index()
    {
        const string jsonString = """{"participants":[{"id":7,"name":"KHK"},{"id":5,"name":"FBK"}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.participants[0]");

        const string expected = """{"id":7,"name":"KHK"}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_property_via_indexed_path()
    {
        const string jsonString = """{"participants":[{"id":7,"name":"KHK"},{"id":5,"name":"FBK"}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.participants[1].name");

        const string expected = "\"FBK\"";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_indexed_paths_with_aliases()
    {
        const string jsonString = """{"participants":[{"id":7,"name":"KHK"},{"id":5,"name":"FBK"}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[home:participants[0].name,away:participants[1].name]");

        const string expected = """{"home":"KHK","away":"FBK"}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_via_chained_indices_in_nested_arrays()
    {
        const string jsonString = """{"matrix":[[1,2],[3,4]]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.matrix[1][0]");

        const string expected = "3";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_return_empty_array_when_path_segment_is_missing()
    {
        const string jsonString = """{"a":{"b":1}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.a.missing");

        const string expected = "[]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_throw_when_identifier_missing_after_dot()
    {
        var selector = new JsonFieldSelector("""{"a":1}""");

        Assert.That(() => selector.Select("$."), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void Should_throw_when_expression_does_not_start_with_dollar()
    {
        var selector = new JsonFieldSelector("""{"a":1}""");

        Assert.That(() => selector.Select("a"), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void Should_select_last_array_element_with_negative_index()
    {
        const string jsonString = """{"participants":[{"id":7,"name":"KHK"},{"id":5,"name":"FBK"}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.participants[-1].name");

        const string expected = "\"FBK\"";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_second_to_last_array_element_with_negative_index()
    {
        const string jsonString = """{"items":[10,20,30,40]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.items[-2]");

        const string expected = "30";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_return_empty_array_when_negative_index_out_of_range()
    {
        const string jsonString = """{"items":[1,2,3]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.items[-99]");

        const string expected = "[]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_combine_negative_index_with_alias_in_projection()
    {
        const string jsonString = """{"participants":[{"name":"KHK"},{"name":"FBK"},{"name":"AIK"}]}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$[winner:participants[-1].name]");

        const string expected = """{"winner":"AIK"}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_from_utf8_byte_constructor()
    {
        const string jsonString = """{"person":{"name":"Alice","age":30}}""";
        var utf8Json = Encoding.UTF8.GetBytes(jsonString);

        var selector = new JsonFieldSelector(new ReadOnlyMemory<byte>(utf8Json));
        var result = selector.Select("$.person[name]");

        const string expected = """{"name":"Alice"}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_array_from_utf8_byte_constructor()
    {
        const string jsonString = """{"crew":[{"id":1,"name":"Allan","age":50},{"id":2,"name":"Bertil","age":75}]}""";
        var utf8Json = Encoding.UTF8.GetBytes(jsonString);

        var selector = new JsonFieldSelector(new ReadOnlyMemory<byte>(utf8Json));
        var result = selector.Select("$.crew[id,name]");

        const string expected = """[{"id":1,"name":"Allan"},{"id":2,"name":"Bertil"}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_select_from_stream_constructor()
    {
        const string jsonString = """{"person":{"name":"Alice","age":30}}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(jsonString));

        var selector = new JsonFieldSelector(stream);
        var result = selector.Select("$.person[name]");

        const string expected = """{"name":"Alice"}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Byte_and_string_constructors_produce_identical_results()
    {
        var jsonString = File.ReadAllText("example.json", Encoding.UTF8);
        const string pathExpression = "$[id:requestId,info:draw[turnover,comment:drawComment]]";

        var fromString = new JsonFieldSelector(jsonString).Select(pathExpression);
        var fromBytes = new JsonFieldSelector(new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(jsonString))).Select(pathExpression);

        Assert.That(fromBytes, Is.EqualTo(fromString));
    }

    [Test]
    public void Should_write_projection_to_buffer_writer()
    {
        const string jsonString = """{"person":{"name":"Alice","age":30}}""";

        var selector = new JsonFieldSelector(jsonString);
        var buffer = new ArrayBufferWriter<byte>();
        selector.Select("$.person[name]", buffer);

        var result = Encoding.UTF8.GetString(buffer.WrittenSpan);

        const string expected = """{"name":"Alice"}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Buffer_writer_overload_matches_string_overload()
    {
        const string jsonString = """{"crew":[{"id":1,"name":"Allan","age":50},{"id":2,"name":"Bertil","age":75}]}""";
        const string pathExpression = "$.crew[id,name]";

        var selector = new JsonFieldSelector(jsonString);

        var fromString = selector.Select(pathExpression);

        var buffer = new ArrayBufferWriter<byte>();
        selector.Select(pathExpression, buffer);
        var fromBuffer = Encoding.UTF8.GetString(buffer.WrittenSpan);

        Assert.That(fromBuffer, Is.EqualTo(fromString));
    }

    [Test]
    public void Should_skip_unknown_field_in_projection()
    {
        const string jsonString = """{"person":{"name":"Alice","age":30}}""";

        var selector = new JsonFieldSelector(jsonString);

        var result = selector.Select("$.person[name,Qname]");

        const string expected = """{"name":"Alice"}""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_skip_unknown_field_in_array_projection()
    {
        const string jsonString = """{"crew":[{"id":1,"name":"Allan"},{"id":2,"name":"Bertil"}]}""";

        var selector = new JsonFieldSelector(jsonString);

        var result = selector.Select("$.crew[id,Qname]");

        const string expected = """[{"id":1},{"id":2}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_skip_field_missing_in_some_objects_only()
    {
        const string jsonString = """{"crew":[{"id":1,"name":"Allan"},{"id":2}]}""";

        var selector = new JsonFieldSelector(jsonString);

        var result = selector.Select("$.crew[id,name]");

        const string expected = """[{"id":1,"name":"Allan"},{"id":2}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_collect_all_prices_with_recursive_descent_in_pre_order()
    {
        const string jsonString = """{"store":{"price":100,"book":{"price":20,"details":{"price":5}},"bicycle":{"price":200}}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..price");

        const string expected = "[100,20,5,200]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_match_recursive_name_at_multiple_depths()
    {
        const string jsonString = """{"name":"root","child":{"name":"inner","items":[{"name":"a"},{"name":"b"}]}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..name");

        const string expected = """["root","inner","a","b"]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_match_recursive_name_case_insensitively()
    {
        const string jsonString = """{"store":{"price":100,"book":{"price":20}}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..PRICE");

        const string expected = "[100,20]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_return_single_match_as_array()
    {
        const string jsonString = """{"store":{"price":100}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..price");

        const string expected = "[100]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_return_empty_array_when_recursive_name_not_found()
    {
        const string jsonString = """{"store":{"book":{"title":"X"}}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..price");

        const string expected = "[]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_apply_projection_to_each_recursively_matched_node()
    {
        const string jsonString = """{"draw":{"events":[{"number":1,"name":"A","extra":"skip"},{"number":2,"name":"B","extra":"skip"}]}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..events[number,name]");

        const string expected = """[[{"number":1,"name":"A"},{"number":2,"name":"B"}]]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_project_over_recursive_descent_with_chained_wildcard()
    {
        const string jsonString = """{"store":{"books":[{"title":"A","price":100,"isbn":"x1"},{"title":"B","price":150,"isbn":"x2"}],"warehouse":{"books":[{"title":"C","price":50,"isbn":"x3"}]}}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..books[*][title,price]");

        const string expected = """[{"title":"A","price":100},{"title":"B","price":150},{"title":"C","price":50}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_throw_format_exception_for_recursive_descent_without_identifier()
    {
        const string jsonString = """{"store":{"price":100}}""";

        var selector = new JsonFieldSelector(jsonString);

        Assert.That(() => selector.Select("$.."), Throws.TypeOf<FormatException>());
    }

    [Test]
    public void Should_collect_every_descendant_node_with_recursive_wildcard()
    {
        const string jsonString = """{"a":[1,2,3],"b":{"c":[4,5],"d":9}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..*");

        const string expected = """[[1,2,3],1,2,3,{"c":[4,5],"d":9},[4,5],4,5,9]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_scope_recursive_wildcard_to_a_branch()
    {
        const string jsonString = """{"store":{"price":100,"book":{"price":20}},"other":{"price":999}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.store..*");

        const string expected = """[100,{"price":20},20]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_collect_all_array_elements_with_recursive_element_wildcard()
    {
        const string jsonString = """{"a":[1,2,3],"b":{"c":[4,5],"d":9}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..[*]");

        const string expected = "[1,2,3,4,5]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_collect_first_element_of_every_array_at_any_depth()
    {
        const string jsonString = """{"a":[1,2,3],"b":{"c":[4,5]}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..[0]");

        const string expected = "[1,4]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_collect_last_element_of_every_array_at_any_depth()
    {
        const string jsonString = """{"a":[1,2,3],"b":{"c":[4,5]}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..[-1]");

        const string expected = "[3,5]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_support_recursive_descent_in_the_middle_of_a_path()
    {
        const string jsonString = """{"store":{"price":100,"book":{"price":20}},"other":{"price":999}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$.store..price");

        const string expected = "[100,20]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_support_multiple_recursive_descents_in_one_expression()
    {
        const string jsonString = """{"a":{"orders":{"price":1,"nested":{"price":2}}},"b":{"price":999}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..orders..price");

        const string expected = "[1,2]";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_rename_recursively_matched_fields_with_bracket_alias()
    {
        const string jsonString = """{"store":{"price":100,"book":{"price":20}},"other":{"price":999}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..[cost:price]");

        const string expected = """[{"cost":100},{"cost":20},{"cost":999}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_match_bracket_alias_case_insensitively()
    {
        const string jsonString = """{"a":{"Price":100},"b":{"PRICE":20}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..[cost:price]");

        const string expected = """[{"cost":100},{"cost":20}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_preserve_value_shape_when_renaming_with_bracket_alias()
    {
        const string jsonString = """{"a":{"items":[1,2]},"b":{"items":[3]}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..[it:items]");

        const string expected = """[{"it":[1,2]},{"it":[3]}]""";

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Should_return_empty_array_when_bracket_alias_matches_nothing()
    {
        const string jsonString = """{"store":{"price":100}}""";

        var selector = new JsonFieldSelector(jsonString);
        var result = selector.Select("$..[cost:missing]");

        Assert.That(result, Is.EqualTo("[]"));
    }

    [Test]
    public void Should_trim_whitespace_from_path_expression()
    {
        const string jsonString = """{"store":{"books":[{"title":"A","price":100},{"title":"B","price":150}]}}""";

        var selector = new JsonFieldSelector(jsonString);

        var expected = selector.Select("$.store.books[title,price]");
        var withWhitespace = selector.Select("  \t $.store . books [ title , price ] \r\n");

        Assert.That(withWhitespace, Is.EqualTo(expected));
        Assert.That(withWhitespace, Is.EqualTo("""[{"title":"A","price":100},{"title":"B","price":150}]"""));
    }
}
