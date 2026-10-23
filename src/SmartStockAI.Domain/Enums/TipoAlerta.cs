namespace SmartStockAI.Domain.Enums;

/// <summary>
/// Tipos de alerta/recomendación que el sistema puede generar.
/// Los primeros tres (deterministas) los calcula siempre el backend;
/// los relacionados a IA (fase 9) reutilizarán este mismo catálogo.
/// </summary>
public enum TipoAlerta
{
    StockBajo = 1,
    SinStock = 2,
    AltaDemanda = 3,
    BajaRotacion = 4,
    RecomendacionCompra = 5,
    TendenciaVentas = 6,
    AlertaGeneral = 7
}
