using Domovoy.Api.Bot;
using Domovoy.Api.Endpoints;
using Domovoy.Core.Data;
using Domovoy.Core.Max;
using Domovoy.Core.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Конфигурация читается из переменных окружения: секреты в репозиторий не попадают.
builder.Configuration.AddEnvironmentVariables();

builder.Services.AddDbContext<DomovoyDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException(
            "Не задана строка подключения ConnectionStrings__Default")));

builder.Services.AddScoped<BuildingSearchService>();
builder.Services.AddScoped<AddressLookupService>();
builder.Services.AddScoped<BindingScenario>();

// Подсказки по адресам из государственного адресного реестра. Без ключа сервис
// работает вхолостую: поиск остаётся только по своей базе, сценарий не ломается.
builder.Services.Configure<DaDataOptions>(builder.Configuration.GetSection(DaDataOptions.SectionName));
builder.Services.AddHttpClient<IAddressSuggestService, DaDataAddressSuggestService>((sp, http) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DaDataOptions>>().Value;
    http.BaseAddress = new Uri(options.BaseUrl);
    http.Timeout = TimeSpan.FromSeconds(10);

    if (options.IsConfigured)
    {
        http.DefaultRequestHeaders.Add("Authorization", $"Token {options.ApiKey}");
        http.DefaultRequestHeaders.Add("Accept", "application/json");
    }
});

builder.Services.Configure<MaxBotOptions>(builder.Configuration.GetSection(MaxBotOptions.SectionName));

builder.Services.AddHttpClient<IMaxBotClient, MaxBotClient>((sp, http) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MaxBotOptions>>().Value;

    if (string.IsNullOrWhiteSpace(options.Token))
    {
        throw new InvalidOperationException("Не задан токен бота: Max__Token или MAX_BOT_TOKEN");
    }

    http.BaseAddress = new Uri(options.BaseUrl);
    http.DefaultRequestHeaders.Add("Authorization", options.Token);
    // Таймаут больше окна long polling, иначе штатное ожидание будет выглядеть как сбой.
    http.Timeout = TimeSpan.FromSeconds(options.PollTimeoutSeconds + 60);
});

builder.Services.AddHostedService<LongPollingService>();

// Мини-приложение раздаётся с другого домена, поэтому нужен явный список источников.
const string CorsPolicy = "webapp";
builder.Services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
{
    var origins = builder.Configuration["WebApp:Origins"]?
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

    if (origins.Length > 0)
    {
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    }
}));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DomovoyDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.ApplyAsync(db);
}

app.UseCors(CorsPolicy);
app.MapApiEndpoints();

app.Run();
