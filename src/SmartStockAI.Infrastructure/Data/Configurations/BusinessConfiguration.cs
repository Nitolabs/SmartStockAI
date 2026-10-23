using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Domain.Entities;

namespace SmartStockAI.Infrastructure.Data.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> b)
    {
        b.ToTable("Businesses");
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
        b.Property(x => x.Rubro).HasMaxLength(100);
        b.Property(x => x.Direccion).HasMaxLength(250);
        b.Property(x => x.Telefono).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(150);
    }
}
