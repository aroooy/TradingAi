using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TradingAi.Core.Models; // モデルのネームスペース
using TradingAi.Core.Data; // TradingDbContextのネームスペース
using TradingAi.DataImporter.Mapping; // マッピングのネームスペース
using TradingAi.DataImporter.Services; // サービスのネームスペース
using Pomelo.EntityFrameworkCore.MySql.Infrastructure; // MySqlOptionsActionのネームスペース
using System;
using System.Threading.Tasks;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// 実行ファイルの場所を基準にappsettings.jsonを読み込む設定
builder.Configuration.SetBasePath(AppContext.BaseDirectory);

// 設定ファイルの読み込み
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// DbContextの登録
builder.Services.AddDbContext<TradingDbContext>(options =>
{
    string connectionString = builder.Configuration.GetConnectionString("MariaDbConnection") ?? throw new InvalidOperationException("Connection string 'MariaDbConnection' not found.");
    options.UseMySql(connectionString,
        ServerVersion.AutoDetect(connectionString) // MariaDBのバージョンを自動検出
        );
});

// DataImporterServiceの登録
builder.Services.AddTransient<DataImporterService>();

using IHost host = builder.Build();

// マイグレーションの適用 (開発時のみ、本番環境では別途考慮)
using (var scope = host.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<TradingDbContext>();
        context.Database.Migrate();
        Console.WriteLine("Database migration complete.");

        // データインポートロジックの呼び出し
        var dataImporter = services.GetRequiredService<DataImporterService>();
        var config = services.GetRequiredService<IConfiguration>();

        // 設定ファイルからルートパスを読み込み
        string importRoot = config.GetValue<string>("DirectorySettings:ImportRoot") ?? throw new InvalidOperationException("DirectorySettings:ImportRoot not found.");
        string completeRoot = config.GetValue<string>("DirectorySettings:CompleteRoot") ?? throw new InvalidOperationException("DirectorySettings:CompleteRoot not found.");

        // ルートパスを絶対パスに変換
        string importRootAbs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, importRoot));
        string completeRootAbs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, completeRoot));

        // future と option のための具体的なパスを構築
        string futureSourceDir = Path.Combine(importRootAbs, "future");
        string optionSourceDir = Path.Combine(importRootAbs, "option");
        string completeFutureDir = Path.Combine(completeRootAbs, "future");
        string completeOptionDir = Path.Combine(completeRootAbs, "option");
        
        // 必要なすべてのディレクトリが存在することを確認（なければ作成）
        Directory.CreateDirectory(futureSourceDir);
        Directory.CreateDirectory(optionSourceDir);
        Directory.CreateDirectory(completeFutureDir);
        Directory.CreateDirectory(completeOptionDir);
        

        // Futureファイルの処理
        if (!Directory.Exists(futureSourceDir))
        {
            Console.WriteLine($"Warning: Future source directory not found at {futureSourceDir}");
        }
        else
        {
            var futureFiles = Directory.GetFiles(futureSourceDir, "*.csv", SearchOption.AllDirectories);
            Console.WriteLine($"Found {futureFiles.Length} files in {futureSourceDir}.");
            foreach (var file in futureFiles)
            {
                // 事前チェック：完了フォルダに同名ファイルが存在すればスキップ
                string fileName = Path.GetFileName(file);
                if (File.Exists(Path.Combine(completeFutureDir, fileName)))
                {
                    Console.WriteLine($"Skipping already completed file: {fileName}");
                    continue;
                }
                await dataImporter.ImportCsvData<FutureBarFull, FutureBarFullMap>(file, completeFutureDir);
            }
        }
        
        // Optionファイルの処理
        if (!Directory.Exists(optionSourceDir))
        {
            Console.WriteLine($"Warning: Option source directory not found at {optionSourceDir}");
        }
        else
        {
            var optionFiles = Directory.GetFiles(optionSourceDir, "*.csv", SearchOption.AllDirectories);
            Console.WriteLine($"Found {optionFiles.Length} files in {optionSourceDir}.");
            foreach (var file in optionFiles)
            {
                // 事前チェック：完了フォルダに同名ファイルが存在すればスキップ
                string fileName = Path.GetFileName(file);
                if (File.Exists(Path.Combine(completeOptionDir, fileName)))
                {
                    Console.WriteLine($"Skipping already completed file: {fileName}");
                    continue;
                }
                await dataImporter.ImportCsvData<OptionBarFull, OptionBarFullMap>(file, completeOptionDir);
            }
        }

        Console.WriteLine("Data import process finished successfully.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred during data import: {ex.Message}");
        // 詳細なエラー情報も表示
        Console.WriteLine(ex.ToString());
    }
}

// host.RunAsync(); // インポート完了後にアプリケーションを終了させるため削除

// アプリケーションがすぐに終了するように変更
await Task.CompletedTask;
