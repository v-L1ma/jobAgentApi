namespace jobAgentApi.Application.Abstractions;

public interface IKeywordNormalizer
{
    List<string> Normalize(IEnumerable<string> keywords);
}
