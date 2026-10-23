using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Domain.Entities;

namespace SmartStockAI.Infrastructure.Data.Configurations;

public class CompraConfiguration : IEntityTypeConfiguration<Compra>
{
    public void Configure(EntityTypeBuilder<Compra> b)
    {
        b.ToTable("Compras");
        b.Property(x => x.NumeroDocumento).IsRequired().HasMaxLength(50);
        b.Property(x => x.UsuarioId).HasMaxLength(450);
        b.Property(x => x.UsuarioNombre).HasMaxLength(150);
        b.Property(x => x.Total).HasColumnType("decimal(18,2)");

        b.HasOne(x => x.Business)
            .WithMany(x => x.Compras)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Proveedor)
            .WithMany(x => x.Compras)
            .HasForeignKey(x => x.ProveedorId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasMany(x => x.Detalles)
            .WithOne(x => x.Compra)
            .HasForeignKey(x => x.CompraId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.BusinessId, x.Fecha });
    }
}

public class CompraDetalleConfiguration : IEntityTypeConfiguration<CompraDetalle>
{
    public void Configure(EntityTypeBuilder<CompraDetalle> b)
    {
        b.ToTable("CompraDetalles");
        b.Property(x => x.CostoUnitario).HasColumnType("decimal(18,2)");
        b.Ignore(x => x.Subtotal);

        b.HasOne(x => x.Producto)
            .WithMany(x => x.CompraDetalles)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
