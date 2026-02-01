using CsvHelper;
using CsvHelper.Configuration;
using System;
using TradingAi.Core.Models;

namespace TradingAi.DataImporter.Mapping
{
    public class OptionBarFullMap : ClassMap<OptionBarFull>
    {
        public OptionBarFullMap()
        {
            Map(m => m.TradeDate).Name("Trade_Date").TypeConverterOption.Format("yyyyMMdd");
            Map(m => m.ExecutionDate).Name("Execution_Date").TypeConverterOption.Format("yyyyMMdd");
            Map(m => m.UnderlyingCode).Name("index_type/underlying_security_code");
            Map(m => m.PutCallType).Name("Put_Call_Type");
            Map(m => m.SymbolCode).Name("Security_Code");
            Map(m => m.SessionId).Name("Session_ID");
            Map(m => m.Open).Name("Open_Price");
            Map(m => m.High).Name("High_Price");
            Map(m => m.Low).Name("Low_Price");
            Map(m => m.Close).Name("Close_Price");
            Map(m => m.Volume).Name("Trade_Volume");
            Map(m => m.VWAP).Name("VWAP");
            Map(m => m.NumberOfTrade).Name("Number_of_Trade");
            Map(m => m.RecordNo).Name("Record_No");
            Map(m => m.ContractMonth).Name("Contract_Month");
            Map(m => m.ExercisePrice).Name("Exercise_Price");
            Map(m => m.CashFuturesType).Name("Cash_Futures_Type");

            // Timestamp のカスタムマッピング
            Map(m => m.Timestamp).Convert(args =>
            {
                var row = args.Row;
                var tradeDateStr = row.GetField<string>("Trade_Date");
                var executionDateStr = row.GetField<string>("Execution_Date");
                var intervalTimeStr = row.GetField<string>("Interval_Time");

                // Execution_Date があればそれを使用、なければ Trade_Date を使用
                var dateStr = !string.IsNullOrEmpty(executionDateStr) ? executionDateStr : tradeDateStr;
                
                // IntervalTimeRaw が null または空の場合は処理しない
                if (string.IsNullOrEmpty(intervalTimeStr) || string.IsNullOrEmpty(dateStr))
                {
                    return default(DateTime);
                }

                // 例: "2023/01/01" + "0900" -> "2023/01/01 09:00"
                string dateTimeStr = $"{dateStr} {intervalTimeStr.Substring(0, 2)}:{intervalTimeStr.Substring(2, 2)}";

                return DateTime.ParseExact(dateTimeStr, "yyyyMMdd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            });
        }
    }
}