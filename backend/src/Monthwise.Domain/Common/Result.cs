namespace Monthwise.Domain.Common;

/// <summary>How the API reports an error: a field problem, a state conflict, or a missing item.</summary>
public enum ErrorKind
{
    Validation,
    Conflict,
    NotFound,
}

/// <summary>
/// A business rule that was not met. <see cref="Field"/> names the request field the error belongs
/// to, so the UI can show it next to that field. Messages never contain the rejected value.
/// </summary>
public sealed record DomainError(string Code, string Message, string? Field = null, ErrorKind Kind = ErrorKind.Validation);

/// <summary>Outcome of an operation that can fail on business rules.</summary>
public class Result
{
    private static readonly Result SuccessResult = new([]);

    protected Result(IReadOnlyList<DomainError> errors) => Errors = errors;

    public IReadOnlyList<DomainError> Errors { get; }
    public bool IsSuccess => Errors.Count == 0;
    public bool IsFailure => !IsSuccess;

    public static Result Success() => SuccessResult;
    public static Result Failure(params DomainError[] errors) => new(RequireErrors(errors));
    public static Result Failure(IEnumerable<DomainError> errors) => new(RequireErrors(errors.ToArray()));

    public static Result<T> Success<T>(T value) => new(value, []);
    public static Result<T> Failure<T>(params DomainError[] errors) => new(default, RequireErrors(errors));
    public static Result<T> Failure<T>(IEnumerable<DomainError> errors) => new(default, RequireErrors(errors.ToArray()));

    private static DomainError[] RequireErrors(DomainError[] errors)
    {
        if (errors.Length == 0)
        {
            throw new ArgumentException("A failure needs at least one error.", nameof(errors));
        }
        return errors;
    }
}

/// <summary>Outcome with a value when successful.</summary>
public sealed class Result<T> : Result
{
    private readonly T? _value;

    internal Result(T? value, IReadOnlyList<DomainError> errors) : base(errors) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");
}
