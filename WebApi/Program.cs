using System.Text.Json.Serialization;
using FastEndpoints;
using WebApi.Selector;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFastEndpoints();

var app = builder.Build();
app.UseDefaultExceptionHandler();
app.UseMiddleware<SelectorMiddleware>();
app.UseFastEndpoints(c =>
    c.Serializer.Options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull);
app.Run();
