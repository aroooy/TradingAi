using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TorchSharp;
using TradingAi.Core.Data;
using TradingAi.Core.Models;
using static TorchSharp.torch;

namespace TradingAi.Core.Pipelines
{
    public class TradingDataPipeline
    {
        private readonly TradingDbContext _dbContext;

        public TradingDataPipeline(TradingDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <summary>
        /// 指定期間内で、取引が活発だったオプション銘柄の上位N件を取得します。
        /// 学習データの「選抜メンバー」を決めるのに使います。
        /// </summary>
        public async Task<List<string>> GetTopActiveOptionsAsync(DateTime startDate, DateTime endDate, int limit = 50)
        {
            return await _dbContext.OptionBarsFull // Options を OptionBarsFull に修正
                .AsNoTracking()
                .Where(o => o.Timestamp >= startDate && o.Timestamp <= endDate)
                .GroupBy(o => o.SymbolCode)
                .Select(g => new
                {
                    SymbolCode = g.Key,
                    TotalVolume = g.Sum(o => o.Volume)
                })
                .OrderByDescending(x => x.TotalVolume) // 出来高が多い順
                .Take(limit)
                .Select(x => x.SymbolCode)
                .ToListAsync();
        }

        /// <summary>
        /// 複数のオプション銘柄を一括で読み込み、1つの巨大な学習用Tensorに結合して返します。
        /// これが「汎用モデル」の源になります。
        /// </summary>
        public async Task<(Tensor x, Tensor y)> LoadUniversalDatasetAsync(List<string> optionSymbols)
        {
            var x_list = new List<Tensor>();
            var y_list = new List<Tensor>();
            
            Console.WriteLine($"[UniversalLoader] Starting to load {optionSymbols.Count} symbols...");

            int successCount = 0;

            foreach (var optionSymbol in optionSymbols)
            {
                try
                {
                    // 1. 親となる先物を自動検索
                    string futureSymbol = await FindMatchingFutureAsync(optionSymbol);

                    // 2. 1銘柄分のデータをTensorとして作成 (既存ロジックの再利用)
                    // ※内部でLoadDatasetAsyncを呼ぶ形にリファクタリングしても良いですが、
                    //   今回はロジックをここに展開して結合します。
                    var (x_single, y_single) = await LoadSinglePairTensorAsync(futureSymbol, optionSymbol);

                    if (x_single.shape[0] > 0)
                    {
                        x_list.Add(x_single);
                        y_list.Add(y_single);
                        successCount++;
                        Console.Write("."); // 進捗表示
                    }
                }
                catch (Exception ex)
                {
                    // データ不足などで失敗しても、全体の学習は止めずにスキップする
                    Console.WriteLine($"\n[Skip] {optionSymbol}: {ex.Message}");
                }
            }

            Console.WriteLine($"\n[UniversalLoader] Successfully loaded {successCount}/{optionSymbols.Count} symbols.");

            if (successCount == 0) throw new Exception("有効な学習データが1つも作れませんでした。");

            // 3. 全銘柄のTensorを縦に結合 (Concatenate)
            // [銘柄Aのデータ]
            // [銘柄Bのデータ]
            //      ...
            // [銘柄Zのデータ]  -> これらを縦につなげる
            
            var x_universal = torch.cat(x_list, dim: 0); // dim=0 は「行」方向の結合
            var y_universal = torch.cat(y_list, dim: 0);

            Console.WriteLine($"[UniversalLoader] Total Dataset Shape: X={string.Join(", ", x_universal.shape)}, Y={string.Join(", ", y_universal.shape)}"); // shape.Stringify() を修正

            // 4. GPU転送 (RTX 5090)
            if (torch.cuda.is_available())
            {
                x_universal = x_universal.cuda();
                y_universal = y_universal.cuda();
            }

            return (x_universal, y_universal);
        }

        // --- 以下、前回作成したメソッドの流用・部品化 ---

        public async Task<string> FindMatchingFutureAsync(string optionSymbol)
        {
            var targetOption = await _dbContext.OptionBarsFull // Options を OptionBarsFull に修正
                .AsNoTracking()
                .Where(o => o.SymbolCode == optionSymbol)
                .Select(o => new { o.ContractMonth })
                .FirstOrDefaultAsync();

            if (targetOption == null) throw new Exception($"Option not found: {optionSymbol}");

            var bestFuture = await _dbContext.FutureBarsFull // Futures を FutureBarsFull に修正
                .AsNoTracking()
                .Where(f => f.ContractMonth == targetOption.ContractMonth)
                .GroupBy(f => f.SymbolCode)
                .Select(g => new { SymbolCode = g.Key, TotalVolume = g.Sum(f => f.Volume) })
                .OrderByDescending(x => x.TotalVolume)
                .FirstOrDefaultAsync();

            if (bestFuture == null) throw new Exception("Future not found");

            return bestFuture.SymbolCode;
        }

        // 1ペア分のデータをTensor化するコアロジック
        private async Task<(Tensor x, Tensor y)> LoadSinglePairTensorAsync(string futureSymbol, string optionSymbol)
        {
            // 設定値 (本来は引数やConfigから貰う)
            int lookback = 30;
            int horizon = 5;

            // DB読み込み (JOIN)
            var futureData = await _dbContext.FutureBarsFull.AsNoTracking().Where(f => f.SymbolCode == futureSymbol).OrderBy(f => f.Timestamp).ToListAsync(); // Futures を FutureBarsFull に修正
            var optionData = await _dbContext.OptionBarsFull.AsNoTracking().Where(o => o.SymbolCode == optionSymbol).OrderBy(o => o.Timestamp).ToListAsync(); // Options を OptionBarsFull に修正

            var merged = from f in futureData
                         join o in optionData on f.Timestamp equals o.Timestamp
                         select new { F = f, O = o };
            
            var rawList = merged.ToList();
            if (rawList.Count < lookback + horizon + 1) return (torch.empty(0), torch.empty(0));

            // 特徴量計算 (対数収益率)
            var features = new List<float[]>();
            for(int i=1; i<rawList.Count; i++)
            {
                var cur = rawList[i];
                var prev = rawList[i-1];
                
                // 0除算対策をしつつ変化率計算
                float f_ret = MathF.Log((float)cur.F.Close / (float)prev.F.Close);
                float o_ret = MathF.Log((float)cur.O.Close / (float)prev.O.Close);
                float f_vol = prev.F.Volume == 0 ? 0 : ((float)cur.F.Volume - prev.F.Volume) / prev.F.Volume;
                float o_vol = prev.O.Volume == 0 ? 0 : ((float)cur.O.Volume - prev.O.Volume) / prev.F.Volume; // prev.F.Volume に修正
                
                features.Add(new float[] { f_ret, o_ret, f_vol, o_vol });
            }

            // スライディングウィンドウ
            var x_data = new List<float[]>();
            var y_data = new List<float>();
            int available = features.Count;

            for(int i = lookback; i < available - horizon; i++)
            {
                // X: 過去30分
                var window = new List<float>();
                for(int j=0; j<lookback; j++) window.AddRange(features[i - lookback + j]);
                x_data.Add(window.ToArray());

                // Y: 未来5分後の価格変動 (Up=1, Down=0)
                float currentPrice = (float)rawList[i+1].O.Close; // featuresのindexとrawListはずれているので注意
                float futurePrice = (float)rawList[i+1+horizon].O.Close;
                y_data.Add(futurePrice > currentPrice ? 1.0f : 0.0f);
            }

            if (x_data.Count == 0) return (torch.empty(0), torch.empty(0));

            // Tensor化
            long[] shape = new long[] { x_data.Count, lookback, 4 };
            return (
                torch.tensor(x_data.SelectMany(a=>a).ToArray()).reshape(shape),
                torch.tensor(y_data.ToArray()).reshape(x_data.Count, 1)
            );
        }
    }
}