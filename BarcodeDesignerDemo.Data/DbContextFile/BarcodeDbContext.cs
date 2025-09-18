using BarcodeDesignerDemo.Data.Contaract;
using BarcodeDesignerDemo.Data.Entity;
using Microsoft.EntityFrameworkCore;

namespace BarcodeDesignerDemo.Data.DbContextFile
{
    /// <summary>
    /// BarcodeDbContext
    /// </summary>
    public class BarcodeDbContext : DbContext, IBarcodeDbContext
    {
        public BarcodeDbContext(DbContextOptions<BarcodeDbContext> dbContextOptions) : base(dbContextOptions) { }

        //public DbSet<Student> Students { get; set; }

        public DbSet<LabelTemplate> LabelTemplates { get; set; }

        public Task<int> SaveChangesToDb()
        {
            return base.SaveChangesAsync();
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(BarcodeDbContext).Assembly);
            base.OnModelCreating(modelBuilder);
        }
    }
}
