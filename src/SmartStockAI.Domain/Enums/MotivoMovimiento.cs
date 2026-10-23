namespace SmartStockAI.Domain.Enums;

/// <summary>
/// Motivo/origen que generó un movimiento de inventario, útil para
/// trazabilidad (una compra, una venta, o un ajuste manual del usuario).
/// </summary>
public enum MotivoMovimiento
{
    Compra = 1,
    Venta = 2,
    AjusteManual = 3,
    DevolucionCliente = 4,
    DevolucionProveedor = 5,
    Merma = 6
}
