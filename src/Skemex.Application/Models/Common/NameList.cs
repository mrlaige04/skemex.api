namespace Skemex.Application.Models.Common;

public class NameList
{
    public IList<string> Names { get; set; } = [];
    public static NameList Empty => new NameList();
}