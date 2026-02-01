using System;

namespace TradingAi.Core.Models
{
    public interface ITimeSeriesData
    {
        public string SymbolCode { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime created_at { get; set; }
    }
}
