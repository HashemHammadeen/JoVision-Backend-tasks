public enum FilterType
{
    ByModificationDate,
    ByCreationDateDescending,
    ByCreationDateAscending,
    ByOwner
}

public class FilterRequest
{
    public DateTime? CreationDate { get; set; }
    public DateTime? ModificationDate { get; set; }
    public string? Owner { get; set; }
    public FilterType FilterType { get; set; }
}

public class FileSummary
{
    public required string FileName { get; set; }
    public required string OwnerName { get; set; }
}