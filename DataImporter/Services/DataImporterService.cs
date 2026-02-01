using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TradingAi.Core.Models;
using TradingAi.Core.Data;
using EFCore.BulkExtensions; // EFCore.BulkExtensionsのusingを追加

namespace TradingAi.DataImporter.Services
{
    public class DataImporterService
    {
        private readonly TradingDbContext _context;

        public DataImporterService(TradingDbContext context)
        {
            _context = context;
        }

        public async Task ImportCsvData<TEntity, TMap>(string filePath, string completeDirectory, int batchSize = 5000)
            where TEntity : class, ITimeSeriesData
            where TMap : ClassMap<TEntity>
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"Error: File not found at {filePath}");
                return;
            }

            Console.WriteLine($"Importing data from {filePath} into {typeof(TEntity).Name}...");

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ",",
                MissingFieldFound = null, // ヘッダーに存在しないフィールドがあってもエラーにしない
                PrepareHeaderForMatch = args => args.Header.ToLower() // ヘッダーの大文字/小文字を区別しない
            };

            try
            {
                using (var reader = new StreamReader(filePath))
                using (var csv = new CsvReader(reader, csvConfig))
                {
                    csv.Context.RegisterClassMap<TMap>(); // カスタムマッピングを登録

                    var records = new List<TEntity>();
                    await foreach (var record in csv.GetRecordsAsync<TEntity>())
                    {
                        records.Add(record);

                        if (records.Count >= batchSize)
                        {
                            await ProcessBatch(records);
                            records.Clear();
                        }
                    }

                    if (records.Any())
                    {
                        await ProcessBatch(records);
                    }
                }

                Console.WriteLine($"Finished importing data for {typeof(TEntity).Name} from {filePath}.");

                // ファイルを完了ディレクトリに移動
                Directory.CreateDirectory(completeDirectory); // ディレクトリがなければ作成
                string fileName = Path.GetFileName(filePath);
                string destPath = Path.Combine(completeDirectory, fileName);

                // もし移動先に同名ファイルが存在する場合、上書きはせず、ファイル名の末尾に日時を追加する
                if (File.Exists(destPath))
                {
                    string newFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(fileName)}";
                    destPath = Path.Combine(completeDirectory, newFileName);
                }

                File.Move(filePath, destPath);
                Console.WriteLine($"Moved file to {destPath}");
            }
            catch (Exception ex)
            {
                // エラーが発生した場合、ファイルは移動しない
                Console.WriteLine($"An error occurred while processing {filePath}. The file has not been moved. Error: {ex.Message}");
                // throw; // 必要に応じて例外を再スローする
            }
        }

        private async Task ProcessBatch<TEntity>(List<TEntity> records)
            where TEntity : class, ITimeSeriesData
        {
            var now = DateTime.UtcNow;
            foreach (var record in records)
            {
                record.created_at = now;
            }

            // BulkInsertOrUpdateAsync は主キーに基づいて
            // 存在しない場合はINSERT、存在する場合はUPDATEを実行します。
            // これにより、重複チェックが不要になります。
            await _context.BulkInsertOrUpdateAsync(records);
            Console.WriteLine($"  - Processed {records.Count} records (Upserted).");
        }
    }
}