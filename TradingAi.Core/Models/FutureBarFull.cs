using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TradingAi.Core.Models
{
    // テーブル名: future_bars_full
    [Table("future_bars_full")]
    [Index(nameof(ContractMonth))] // 限月切り替え（ロールオーバー）判定用
    public class FutureBarFull : ITimeSeriesData
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

        // --- 以下、J-Quants 先物一分足 仕様完全準拠 (No.1～15) ---

        // No.1 取引日 (Trade_Date)
        [Column(TypeName = "DATE")]
        public DateTime TradeDate { get; set; }

        // No.2 約定日 (Execution_Date)
        [Column(TypeName = "DATE")]
        public DateTime? ExecutionDate { get; set; }

        // No.3 指数区分 (Index_Type)
        [StringLength(3)]
        [Column(TypeName = "CHAR(3)")]
        public string IndexType { get; set; } = string.Empty;


        // No.5 セッションID
        public int SessionId { get; set; }

        // No.7 始値
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal Open { get; set; }

        // No.8 高値
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal High { get; set; }

        // No.9 安値
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal Low { get; set; }

        // No.10 終値
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal Close { get; set; }

        // No.11 取引高
        public long Volume { get; set; }

        // No.12 VWAP
        [Column(TypeName = "DECIMAL(12, 4)")]
        public decimal VWAP { get; set; }

        // No.13 約定回数
        public int NumberOfTrade { get; set; }

        // No.14 収録順
        public long RecordNo { get; set; }

        // No.15 限月
        public int ContractMonth { get; set; }
        
        // --- 独自項目 ---
        [Required]
        [Column(TypeName = "DATETIME")]
        public DateTime created_at { get; set; }
    }
}