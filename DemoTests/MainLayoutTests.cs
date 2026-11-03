using Bunit;
using Demo.Layout;

namespace DemoTests;

public sealed class MainLayoutTests
{
    [Test]
    public void Header_uses_transparent_Csharp_brand_mark()
    {
        using var context = new BunitContext();
        var layout = context.Render<MainLayout>();

        var brand = layout.Find("a.app-brand");
        var icon = layout.Find(".app-brand img.brand-icon");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(brand.GetAttribute("href"), Is.EqualTo("https://dotnetlab.se/"));
            Assert.That(icon.GetAttribute("src"), Is.EqualTo("brand-mark.png"));
            Assert.That(icon.GetAttribute("alt"), Is.EqualTo(""));
        }
    }
}
