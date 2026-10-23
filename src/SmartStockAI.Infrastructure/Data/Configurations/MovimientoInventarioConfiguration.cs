using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartStockAI.Domain.Entities;

namespace SmartStockAI.Infrastructure.Data.Configurations;

public class MovimientoInventarioConfiguration : IEntityTypeConfiguration<MovimientoInventario>
{
    public void Configure(EntityTypeBuilder<MovimientoInventario> b)
    {
        b.ToTable("MovimientosInventario");
        b.Property(x => x.UsuarioId).HasMaxLength(450);
        b.Property(x => x.UsuarioNombre).HasMaxLength(150);
        b.Property(x => x.Observaciones).HasMaxLength(500);

        b.HasOne(x => x.Business)
            .WithMany(x => x.Movimientos)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Producto)
            .WithMany(x => x.Movimientos)
            .HasForeignKey(x => x.ProductoId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Venta)
            .WithMany(x => x.Movimientos)
            .HasForeignKey(x => x.VentaId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasOne(x => x.Compra)
            .WithMany(x => x.Movimientos)
            .HasForeignKey(x => x.CompraId)
            .OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.BusinessId, x.Fecha });
    }
}
