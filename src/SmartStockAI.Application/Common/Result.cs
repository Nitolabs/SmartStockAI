namespace SmartStockAI.Application.Common;

/// <summary>
/// Envoltorio estándar para el resultado de una operación de negocio, de
/// forma que los controladores nunca dependan de excepciones para el
/// control de flujo (por ejemplo: "stock insuficiente" es un resultado
/// esperado, no un error del sistema).
/// </summary>
public class Result
{
    public bool Success { get; init; }
    public string? Error { get; init; }

    public static Result Ok() => new() { Success = true };
    public static Result Fail(string error) => new() { Success = false, Error = error };
}

public class Result<T> : Result
{
    public T? Value { get; init; }

    public static Result<T> Ok(T value) => new() { Success = true, Value = value };
    public static new Result<T> Fail(string error) => new() { Success = false, Error = error };
}
