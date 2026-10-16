namespace NutriCare.Domain.Exceptions;

/// <summary>
/// Base application exception
/// </summary>
public class ApplicationException : Exception
{
    public ApplicationException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when entity is not found
/// </summary>
public class EntityNotFoundException : ApplicationException
{
    public EntityNotFoundException(string entityName, long id)
        : base($"{entityName} with ID {id} not found") { }
}

/// <summary>
/// Exception thrown when validation fails
/// </summary>
public class ValidationException : ApplicationException
{
    public ValidationException(string message) : base(message) { }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("Validation failure")
    {
        Errors = errors;
    }

    public IDictionary<string, string[]>? Errors { get; }
}

/// <summary>
/// Exception thrown when access is forbidden
/// </summary>
public class ForbiddenAccessException : ApplicationException
{
    public ForbiddenAccessException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when user is unauthorized
/// </summary>
public class UnauthorizedException : ApplicationException
{
    public UnauthorizedException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when user already exists
/// </summary>
public class UserAlreadyExistsException : ApplicationException
{
    public UserAlreadyExistsException(string email)
        : base($"User with email '{email}' already exists") { }
}

/// <summary>
/// Exception thrown when invalid credentials
/// </summary>
public class InvalidCredentialsException : ApplicationException
{
    public InvalidCredentialsException()
        : base("Invalid email or password") { }
}

/// <summary>
/// Exception thrown when invalid token
/// </summary>
public class InvalidTokenException : ApplicationException
{
    public InvalidTokenException(string message) : base(message) { }
}

/// <summary>
/// Exception thrown when operation conflicts with existing data
/// </summary>
public class ConflictException : ApplicationException
{
    public ConflictException(string message) : base(message) { }
}
