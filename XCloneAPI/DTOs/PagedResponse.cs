namespace XCloneAPI.DTOs
{
    // One page of a list that is read with cursors.
    public class PagedResponse<T>
    {
        public List<T> Items { get; set; } = new();

        // Pass this back as ?cursor= to get the page after this one; null when this was the last page.
        public string? NextCursor { get; set; }
    }
}
