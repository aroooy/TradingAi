using Microsoft.EntityFrameworkCore;
using TradingAi.Core.Models; // モデルのネームスペース

namespace TradingAi.Core.Data
{
    public class TradingDbContext : DbContext
    {
        public TradingDbContext(DbContextOptions<TradingDbContext> options)
            : base(options)
        {
        }

        public DbSet<FutureBarFull> FutureBarsFull { get; set; }
        public DbSet<OptionBarFull> OptionBarsFull { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // FutureBarFullの複合主キーを設定
            modelBuilder.Entity<FutureBarFull>()
                .HasKey(o => new { o.SymbolCode, o.Timestamp });

            // OptionBarFullの複合主キーを設定
            modelBuilder.Entity<OptionBarFull>()
                .HasKey(o => new { o.SymbolCode, o.Timestamp });
        }
    }
}