namespace Aivora.Services.Base;

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
