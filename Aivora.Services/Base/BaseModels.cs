namespace Aivora.Services.Base;

public class Request
{
    public class PageRequest
    {
        public int PageSize { get; set; } = 20;
        public int PageIndex { get; set; } = 1;
        public string? SearchTerm { get; set; }
    }
}

public class Response
{
    public class PageResult<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int TotalItems { get; set; }
        public int PageSize { get; set; }
        public int PageIndex { get; set; }

        public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
    }
}
