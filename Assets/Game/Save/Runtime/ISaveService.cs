namespace MSC.Save
{
    /// <summary>
    /// Reports save-system activity without coupling consumers to storage or DTO implementations.
    /// </summary>
    public interface ISaveService
    {
        bool IsOperationInProgress { get; }
    }
}
