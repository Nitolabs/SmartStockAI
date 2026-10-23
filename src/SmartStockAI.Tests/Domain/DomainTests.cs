using SmartStockAI.Application.Common;
using SmartStockAI.Domain.Entities;
using Xunit;

namespace SmartStockAI.Tests.Domain;

public class ProductoDomainTests
{
    [Theory]
    [InlineData(5, 10, true)]   // por debajo del mínimo
    [InlineData(10, 10, true)]  // exactamente en el mínimo
    [InlineData(11, 10, false)] // por encima
    [InlineData(0, 0, true)]    // sin stock y sin mínimo
    public void TieneStockBajo_SeEvaluaConLaReglaStockActualMenorOIgualAlMinimo(int stock, int minimo, bool esperado)
    {
        var producto = new Producto { StockActual = stock, StockMinimo = minimo };

        Assert.Equal(esperado, producto.TieneStockBajo);
    }
}

public class DetallesDomainTests
{
    [Fact]
    public void VentaDetalle_SubtotalEsCantidadPorPrecioMenosDescuento()
    {
        var d = new VentaDetalle { Cantidad = 3, PrecioUnitario = 2.5m, Descuento = 1m };

        Assert.Equal(6.5m, d.Subtotal);
    }

    [Fact]
    public void CompraDetalle_SubtotalEsCantidadPorCostoUnitario()
    {
        var d = new CompraDetalle { Cantidad = 4, CostoUnitario = 1.25m };

        Assert.Equal(5m, d.Subtotal);
    }

    [Fact]
    public void EntidadesNuevas_NacenActivasYConFechaDeCreacion()
    {
        var p = new Producto();

        Assert.True(p.Activo);
        Assert.True((DateTime.UtcNow - p.FechaCreacion).TotalSeconds < 5);
    }
}

public class ResultadosTests
{
    [Fact]
    public void Result_OkYFail()
    {
        var ok = Result.Ok();
        var fail = Result.Fail("mal");

        Assert.True(ok.Success);
        Assert.False(fail.Success);
        Assert.Equal("mal", fail.Error);
    }

    [Fact]
    public void ResultGenerico_LlevaElValorSoloSiEsExitoso()
    {
        var ok = Result<int>.Ok(7);
        var fail = Result<int>.Fail("mal");

        Assert.Equal(7, ok.Value);
        Assert.False(fail.Success);
        Assert.Equal(0, fail.Value);
    }

    [Theory]
    [InlineData(25, 10, 3)]
    [InlineData(20, 10, 2)]
    [InlineData(0, 10, 0)]
    public void PagedResult_CalculaElTotalDePaginas(int total, int tamano, int paginas)
    {
        var r = new PagedResult<int> { TotalCount = total, PageSize = tamano, Page = 1 };

        Assert.Equal(paginas, r.TotalPages);
    }

    [Fact]
    public void PagedResult_ConTamanoCero_NoDividePorCero()
    {
        var r = new PagedResult<int> { TotalCount = 5, PageSize = 0 };

        Assert.Equal(0, r.TotalPages);
    }
}
