namespace Aivora.Services.Base;

public class Request
{
    public class PageRequest
    {
        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;

        private int _pageSize = DefaultPageSize;
        private int _pageIndex = 1;

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = Math.Clamp(value, 1, MaxPageSize);
        }

        public int PageIndex
        {
            get => _pageIndex;
            set => _pageIndex = value < 1 ? 1 : value;
        }

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

        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)PageSize);
    }
}
