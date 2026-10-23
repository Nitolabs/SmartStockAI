using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Domain.Entities;

namespace SmartStockAI.Infrastructure.Data.Configurations;

public class ProveedorConfiguration : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> b)
    {
        b.ToTable("Proveedores");
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
        b.Property(x => x.Contacto).HasMaxLength(150);
        b.Property(x => x.Telefono).HasMaxLength(30);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.Direccion).HasMaxLength(250);

        b.HasOne(x => x.Business)
            .WithMany(x => x.Proveedores)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
