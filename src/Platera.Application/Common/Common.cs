namespace Platera.Application.Common;

public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}
public class NotFoundException : AppException { public NotFoundException(string message) : base(message) { } }
public class ForbiddenException : AppException { public ForbiddenException(string message = "You do not have permission to do that.") : base(message) { } }
public class ConflictException : AppException { public ConflictException(string message) : base(message) { } }
public class UnauthorizedAppException : AppException { public UnauthorizedAppException(string message = "Invalid credentials.") : base(message) { } }
public class BadRequestException : AppException { public BadRequestException(string message) : base(message) { } }

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class PageQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int SafePage => Page < 1 ? 1 : Page;
    public int SafePageSize => PageSize < 1 ? 12 : Math.Min(PageSize, 50);
}

public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}
