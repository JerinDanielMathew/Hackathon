using BarcodeDesignerDemo.Data.Entity;
using Microsoft.EntityFrameworkCore;

namespace BarcodeDesignerDemo.Data.Contaract
{
    /// <summary>
    /// Db interface
    /// </summary>
    public interface IBarcodeDbContext
    {
        DbSet<LabelTemplate> LabelTemplates { get; set; }

        Task<int> SaveChangesToDb();
    }
}
