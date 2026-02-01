using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingAi.Core.Models
{
    public abstract class BaseEntity : ITimeSeriesData
    {
        [Required]
        [StringLength(9)]
        [Column(TypeName = "CHAR(9)")]
        public string SymbolCode { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "DATETIME(0)")]
        public DateTime Timestamp { get; set; }

        [Required]
        public DateTime created_at { get; set; }
    }
}