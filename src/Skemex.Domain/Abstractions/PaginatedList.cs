namespace Skemex.Domain.Abstractions;

public class PaginatedList<T>
{
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalItems { get; set; }

    public IReadOnlyCollection<T> Items { get; set; } = [];

    public PaginatedList() { }

    public PaginatedList(IReadOnlyCollection<T> items, int totalItems, int pageNumber, int pageSize)
    {
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalItems = totalItems;
        Items = items;
    }
}