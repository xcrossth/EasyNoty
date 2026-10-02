using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace EasyNoty.Infrastructure.Migrations;

[DbContext(typeof(EasyNotyDbContext))]
public sealed class EasyNotyDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder) => new EasyNotyDbContext(new DbContextOptionsBuilder<EasyNotyDbContext>().UseSqlite("Data Source=:memory:").Options).Model.GetEntityTypes();
}
