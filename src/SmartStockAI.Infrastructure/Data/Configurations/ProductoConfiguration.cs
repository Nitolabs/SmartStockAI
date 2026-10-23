using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Domain.Entities;

namespace SmartStockAI.Infrastructure.Data.Configurations;

public class ProductoConfiguration : IEntityTypeConfiguration<Producto>
{
    public void Configure(EntityTypeBuilder<Producto> b)
    {
        b.ToTable("Productos");
        b.Property(x => x.Nombre).IsRequired().HasMaxLength(150);
        b.Property(x => x.Descripcion).HasMaxLength(500);
        b.Property(x => x.Sku).IsRequired().HasMaxLength(50);
        b.Property(x => x.PrecioCompra).HasColumnType("decimal(18,2)");
        b.Property(x => x.PrecioVenta).HasColumnType("decimal(18,2)");
        b.Ignore(x => x.TieneStockBajo);

        b.HasOne(x => x.Business)
            .WithMany(x => x.Productos)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Categoria)
            .WithMany(x => x.Productos)
            .HasForeignKey(x => x.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Proveedor)
            .WithMany(x => x.Productos)
            .HasForeignKey(x => x.ProveedorId)
            .OnDelete(DeleteBehavior.SetNull);

        b.HasIndex(x => new { x.BusinessId, x.Sku }).IsUnique();
    }
}
