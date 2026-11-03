using System.Globalization;
using ExpenseDashboard.Web.Components;

namespace ExpenseDashboard.Tests.Components;

public class DisplayTests
{
    [Fact]
    public void Formats_ignore_the_server_culture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            // de-DE 用句點當千分位、% 前有空白；畫面一律要用逗號且不空白。
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("12,346 TWD", Display.Amount(12345.6m, "TWD"));
            Assert.Equal("1,234.5", Display.Exact(1234.5m));
            Assert.Equal("1,234", Display.Count(1234));
            Assert.Equal("2025/03/09", Display.Date(new DateOnly(2025, 3, 9)));
            Assert.Equal("12%", Display.Percent(0.123m));
            Assert.Equal("12.3%", Display.Percent(0.123m, 1));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
