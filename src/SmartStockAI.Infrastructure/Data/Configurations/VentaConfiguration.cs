using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Domain.Entities;

namespace SmartStockAI.Infrastructure.Data.Configurations;

public class VentaConfiguration : IEntityTypeConfiguration<Venta>
{
    public void Configure(EntityTypeBuilder<Venta> b)
    {
        b.ToTable("Ventas");
        b.Property(x => x.NumeroDocumento).IsRequired().HasMaxLength(50);
        b.Property(x => x.UsuarioId).HasMaxLength(450);
        b.Property(x => x.UsuarioNombre).HasMaxLength(150);
        b.Property(x => x.Subtotal).HasColumnType("decimal(18,2)");
        b.Property(x => x.Descuento).HasColumnType("decimal(18,2)");
        b.Property(x => x.Total).HasColumnType("decimal(18,2)");

        b.HasOne(x => x.Business)
            .WithMany(x => x.Ventas)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Detalles)
            .WithOne(x => x.Venta)
            .HasForeignKey(x => x.VentaId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.BusinessId, x.Fecha });
    }
}

public class VentaDetalleConfiguration : IEntityTypeConfiguration<VentaDetalle>
{
    public void Configure(EntityTypeBuilder<VentaDetalle> b)
    {
        b.ToTable("VentaDetalles");
        b.Property(x => x.PrecioUnitario).HasColumnType("decimal(18,2)");
        b.Property(x => x.Descuento).HasColumnType("decimal(18,2)");
        b.Ignore(x => x.Subtotal);

        b.HasOne(x => x.Producto)
            .WithMany(x => x.VentaDetalles)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
