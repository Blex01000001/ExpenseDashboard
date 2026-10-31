using Bunit;
using Microsoft.AspNetCore.Components.Forms;
using ImportPage = ExpenseDashboard.Web.Components.Pages.Import;

namespace ExpenseDashboard.Tests.Components;

public class ImportPageTests : ComponentTestContext
{
    private const string Header = "日期,類別,大類別,金額,幣別,成員,帳戶,標籤,備註,收支區分,上次更新,UUID";
    private const string Lunch = "20261001,午餐,Diet ,120,TWD,自己,現金,,,支,2026-10-01 12:00:00,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9";
    private const string Dinner = "20261002,晚餐,Diet ,300,TWD,自己,現金,,,支,2026-10-02 19:00:00,2B3C4D5E-6F70-4182-93A4-B5C6D7E8F901";
    private const string Snack = "20261003,點心,Diet ,60,TWD,自己,現金,,,支,2026-10-03 15:00:00,3C4D5E6F-7081-4293-A4B5-C6D7E8F90112";
    private const string EditedLunch = "20261001,午餐,Diet ,150,TWD,自己,現金,,加蛋,支,2026-10-05 08:00:00,1A2B3C4D-5E6F-4071-8293-A4B5C6D7E8F9";

    [Fact]
    public void Uploading_a_csv_shows_the_summary()
    {
        var cut = Render<ImportPage>();

        Upload(cut, Header, Lunch, Dinner);

        cut.WaitForAssertion(() => Assert.Contains("新增 2 筆、更新 0 筆、未變更 0 筆", cut.Find(".import-summary").TextContent));
    }

    [Fact]
    public void Reimporting_an_edited_record_lists_the_changed_fields()
    {
        var cut = Render<ImportPage>();
        Upload(cut, Header, Lunch, Dinner);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".import-summary")));

        Upload(cut, Header, EditedLunch, Dinner);

        cut.WaitForAssertion(() => Assert.Contains("更新 1 筆", cut.Find(".import-summary").TextContent));
        var change = cut.Find(".change-list li");
        Assert.Contains("2026/10/01 午餐 150 TWD", change.TextContent);
        Assert.Equal(
            ["金額：120 → 150", "備註：（空） → 加蛋"],
            cut.FindAll(".field-change").Select(e => e.TextContent.Trim()));
    }

    [Fact]
    public void Missing_columns_are_reported()
    {
        var cut = Render<ImportPage>();

        Upload(cut, "日期,類別,金額", "20261001,午餐,120");

        cut.WaitForAssertion(() => Assert.Contains("缺少欄位", cut.Markup));
        Assert.Contains("無法匯入", cut.Find(".import-failure").TextContent);
        Assert.Empty(cut.FindAll(".import-summary"));
    }

    [Fact]
    public void Sync_delete_previews_and_deletes_only_after_confirmation()
    {
        var cut = Render<ImportPage>();
        Upload(cut, Header, Lunch, Dinner);
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".import-summary")));

        cut.Find(".sync-option input").Change(true);
        // 新匯出檔涵蓋 10/01–10/03，晚餐（10/02）不見了＝在 App 裡被刪除。
        Upload(cut, Header, Lunch, Snack);

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find(".delete-preview")));
        Assert.Contains("晚餐", cut.Find(".delete-preview tbody").TextContent);
        using (var db = Database.CreateDbContext())
        {
            Assert.Equal(3, db.Transactions.Count());
        }

        cut.Find(".delete-preview .btn-danger").Click();

        cut.WaitForAssertion(() => Assert.Contains("已刪除 1 筆", cut.Markup));
        using (var db = Database.CreateDbContext())
        {
            Assert.Equal(["午餐", "點心"], db.Transactions.OrderBy(t => t.Date).Select(t => t.Category).ToList());
        }
    }

    private static void Upload(IRenderedComponent<ImportPage> cut, params string[] lines) =>
        cut.FindComponent<InputFile>().UploadFiles(
            InputFileContent.CreateFromText("\uFEFF" + string.Join("\n", lines) + "\n", "export.csv"));
}
