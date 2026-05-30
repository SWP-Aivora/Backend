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
