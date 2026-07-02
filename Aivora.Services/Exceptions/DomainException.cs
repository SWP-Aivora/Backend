namespace Aivora.Services.Exceptions;

public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}

public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message) { }
}

public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }
}

public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message) : base(message) { }
}

public class ServiceUnavailableException : DomainException
{
    public ServiceUnavailableException(string message) : base(message) { }
}
