namespace Skemex.Application.Models.Common;

public class IdList
{
    public IList<Guid> Ids { get; set; } = [];
    public static IdList Empty => new IdList();
}