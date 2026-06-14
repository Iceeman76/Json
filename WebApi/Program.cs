using FastEndpoints;
using WebApi.Selector;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddFastEndpoints();

var app = builder.Build();
app.UseMiddleware<SelectorMiddleware>();
app.UseFastEndpoints();
app.Run();
