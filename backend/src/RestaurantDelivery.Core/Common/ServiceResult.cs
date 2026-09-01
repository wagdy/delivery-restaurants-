namespace RestaurantDelivery.Core.Common;

public class ServiceResult<T>
{
    public bool Succeeded { get; private init; }
    public T? Data { get; private init; }
    public IReadOnlyList<string> Errors { get; private init; } = Array.Empty<string>();

    public static ServiceResult<T> Success(T data) => new() { Succeeded = true, Data = data };

    public static ServiceResult<T> Failure(params string[] errors) => new() { Succeeded = false, Errors = errors };
}
