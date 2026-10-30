using ApexCharts;
using ExpenseDashboard.Web.Components;
using ExpenseDashboard.Web.Dashboard;
using ExpenseDashboard.Web.Data;
using ExpenseDashboard.Web.Import;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Blazor Server 的 circuit 生命週期很長，用 factory 讓每個操作各自建立短命的 DbContext。
builder.Services.AddDbContextFactory<ExpenseDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Expenses")));

builder.Services.AddScoped<TransactionImporter>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddApexCharts();

var app = builder.Build();

// 本機單人使用：啟動時自動套用 migration。
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "App_Data"));
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ExpenseDbContext>>();
    using var db = factory.CreateDbContext();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
