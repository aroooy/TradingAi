using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TradingAi.Core.Models
{
    // テーブル名: option_bars_full
    // インデックス: 
    // 1. 分析用: 限月 + 権利行使価格 + P/C区分 (スマイルカーブ分析等で高速化)
    [Table("option_bars_full")]
    [Index(nameof(ContractMonth), nameof(ExercisePrice), nameof(PutCallType))]
    public class OptionBarFull : ITimeSeriesData
    {
        // --- ここから複合主キー ---
        [Required]
        [StringLength(9)]
        [Column(TypeName = "CHAR(9)")]
        public string SymbolCode { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "DATETIME")]
        public DateTime Timestamp { get; set; }
        // --- ここまで複合主キー ---


        // --- 以下、J-Quants オプション一分足 仕様完全準拠 (No.1～18) ---

        // No.1 取引日 (Trade_Date)
        [Column(TypeName = "DATE")]
        public DateTime TradeDate { get; set; }

        // No.2 約定日 (Execution_Date)
        // ※2022/9以前はデータがないため Null許容(DateTime?) が必須
        [Column(TypeName = "DATE")]
        public DateTime? ExecutionDate { get; set; }

        // No.3 指数区分／対象証券コード
        [StringLength(4)]
        [Column(TypeName = "CHAR(4)")]
        public string UnderlyingCode { get; set; } = string.Empty;

        // No.4 プット・コール区分 (1:Put, 2:Call)
        public byte PutCallType { get; set; }


        // No.6 セッションID (999:日中, 003:夜間)
        public int SessionId { get; set; }

        // No.8 始値 (Open_Price)
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal Open { get; set; }

        // No.9 高値 (High_Price)
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal High { get; set; }

        // No.10 安値 (Low_Price)
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal Low { get; set; }

        // No.11 終値 (Close_Price)
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal Close { get; set; }

        // No.12 取引高 (Trade_Volume)
        public long Volume { get; set; }

        // No.13 VWAP (小数点以下の可能性があるためDECIMAL)
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal VWAP { get; set; }

        // No.14 約定回数 (Number_of_Trade)
        public int NumberOfTrade { get; set; }

        // No.15 収録順 (Record_No)
        // ※2022/9以降は12桁になるため long (Int64) が必須
        public long RecordNo { get; set; }

        // No.16 限月 (Contract_Month) : YYYYMM
        public int ContractMonth { get; set; }

        // No.17 権利行使価格 (Exercise_Price)
        // 個別株OP等で小数の可能性があるためDECIMAL推奨
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal ExercisePrice { get; set; }

        // No.18 現物・先物区分 (1:現物, 2:先物)
        public byte CashFuturesType { get; set; }
        
        // --- 独自項目 ---
        [Required]
        [Column(TypeName = "DATETIME")]
        public DateTime created_at { get; set; }
    }
}