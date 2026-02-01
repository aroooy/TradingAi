using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Diagnostics;
using TorchSharp;
using TradingAi.Core.Data;
using TradingAi.Core.Pipelines;
using TradingAi.Core.Models;
using static TorchSharp.torch;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

// 実行ファイルの場所を基準にappsettings.jsonを読み込む設定
builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// DbContextの登録
builder.Services.AddDbContext<TradingDbContext>(options =>
{
    string connectionString = builder.Configuration.GetConnectionString("MariaDbConnection") ?? throw new InvalidOperationException("Connection string 'MariaDbConnection' not found.");
    options.UseMySql(connectionString,
        ServerVersion.AutoDetect(connectionString) // MariaDBのバージョンを自動検出
        );
});

// DataPipelineServiceとTradingDataPipelineの登録
builder.Services.AddTransient<TradingDataPipeline>();

using IHost host = builder.Build();

try
{
    var scope = host.Services.CreateScope();
    var services = scope.ServiceProvider;
    
    // 既存のDIコンテナからサービスを取得
    var config = services.GetRequiredService<IConfiguration>();
    var dbContext = services.GetRequiredService<TradingDbContext>();
    var pipeline = services.GetRequiredService<TradingDataPipeline>();

    // 初期設定
    var device = torch.cuda.is_available() ? torch.CUDA : torch.CPU;
    Console.WriteLine($"[Trainer] Using Device: {device}"); // RTX 5090なら "CUDA" と出るはず

    // 学習データの期間設定
    var daysToLookback = config.GetValue<int>("TrainingSettings:DaysToLookback", 365);
    var endDate = DateTime.UtcNow;
    var startDate = endDate.AddDays(-daysToLookback);
    Console.WriteLine($"[Trainer] Data Range: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} ({daysToLookback} days)");

    // 1. データセット作成
    Console.WriteLine("[Trainer] Fetching active options...");
    var activeSymbols = await pipeline.GetTopActiveOptionsAsync(startDate, endDate, limit: 10);

    if (!activeSymbols.Any())
    {
        Console.WriteLine("No active option symbols found to train. Exiting.");
        return;
    }

    Console.WriteLine($"Found {activeSymbols.Count} active option symbols: {string.Join(", ", activeSymbols)}");
    Console.WriteLine("Loading universal dataset...");
    var (x_train, y_train) = await pipeline.LoadUniversalDatasetAsync(activeSymbols);

    // データをGPUに転送 (Pipeline内でやっていなければここでやる)
    x_train = x_train.to(device);
    y_train = y_train.to(device);

    // 2. モデルの作成
    Console.WriteLine($"[Trainer] Creating Model... (Input: {ModelDefinition.InputSize}, Hidden: {ModelDefinition.HiddenSize})");
    var model = new LstmModel("Predictor", ModelDefinition.InputSize, ModelDefinition.HiddenSize);
    model.to(device); // モデルをGPUに転送

    // 3. 学習設定
    var optimizer = optim.Adam(model.parameters(), lr: 0.001); // 最適化アルゴリズム
    var lossFunc = nn.BCELoss(); // 損失関数 (2値分類なのでBinary Cross Entropy)

    // 4. 学習ループ (Epoch)
    int epochs = 100; // 繰り返す回数
    int batchSize = 64; // 一度に学習するデータ数
    long dataCount = x_train.shape[0];

    Console.WriteLine($"[Trainer] Start Training... (Data: {dataCount}, Epochs: {epochs})");
    var sw = Stopwatch.StartNew();

    model.train(); // 学習モードへ

    for (int epoch = 1; epoch <= epochs; epoch++)
    {
        float totalLoss = 0;
        int batches = 0;

        // ミニバッチ学習
        for (long i = 0; i < dataCount; i += batchSize)
        {
            // バッチの切り出し
            long size = Math.Min(batchSize, dataCount - i);
            using var x_batch = x_train.narrow(0, i, size);
            using var y_batch = y_train.narrow(0, i, size);

            // 前回の勾配をリセット
            optimizer.zero_grad();

            // 順伝播 (予測)
            using var prediction = model.forward(x_batch);

            // 損失計算 (正解とのズレを計算)
            using var loss = lossFunc.forward(prediction, y_batch);

            // 逆伝播 (パラメータ更新)
            loss.backward();
            optimizer.step();

            totalLoss += loss.item<float>();
            batches++;
        }

        // 10エポックごとに進捗表示
        if (epoch % 10 == 0 || epoch == 1)
        {
            Console.WriteLine($"Epoch {epoch}/{epochs} | Avg Loss: {totalLoss / batches:F4} | Time: {sw.Elapsed.TotalSeconds:F1}s");
        }
    }

    Console.WriteLine($"[Trainer] Training Finished in {sw.Elapsed.TotalSeconds:F1}s");

    // 5. モデルの保存
    string modelPath = config.GetValue<string>("ModelSettings:OutputPath") ?? throw new InvalidOperationException("ModelSettings:OutputPath not found.");
    string modelPathAbs = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, modelPath));

    // 保存先ディレクトリの確認と作成
    string? directory = Path.GetDirectoryName(modelPathAbs);
    if (directory != null && !Directory.Exists(directory))
    {
        Directory.CreateDirectory(directory);
    }
    
    model.save(modelPathAbs);
    Console.WriteLine($"[Trainer] Model saved to '{modelPathAbs}'");

}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred during training process: {ex.Message}");
    Console.WriteLine(ex.ToString());
}

await Task.CompletedTask;
