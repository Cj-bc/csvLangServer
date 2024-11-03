namespace CsvLangServer;

using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using MediatR;

public record GetTextDocumentItemRequest(TextDocumentIdentifier identifier) : IRequest<TextDocumentItem?>;
public record PutTextDocumentItemRequest(TextDocumentItem item) : IRequest<bool>;
public record DeleteTextDocumentItemRequest(TextDocumentIdentifier identifier) : IRequest<bool>;

/// <summary>
/// Manage TextDocuments registered in this program.
/// </summary>
public interface IDocumentsRepositoryService : IRequestHandler<GetTextDocumentItemRequest, TextDocumentItem?>
                                             , IRequestHandler<PutTextDocumentItemRequest, bool>
                                             , IRequestHandler<DeleteTextDocumentItemRequest, bool>;

public class OpenedDocumentsRepositoryService : IDocumentsRepositoryService
{
    private Dictionary<TextDocumentIdentifier, TextDocumentItem> openedDocuments = new();

    public Task<TextDocumentItem?> Handle(GetTextDocumentItemRequest request, CancellationToken token)
    => Task.FromResult(openedDocuments.GetValueOrDefault(request.identifier));

    public Task<bool> Handle(PutTextDocumentItemRequest request, CancellationToken token)
    {
        openedDocuments.Add(new(request.item.Uri), request.item);
        return Task.FromResult(true);
    }

    public Task<bool> Handle(DeleteTextDocumentItemRequest request, CancellationToken token)
    {
        if (!openedDocuments.ContainsKey(request.identifier))
        {
            // TODO: What should we do if it receives "Close" message before "Open"?
            return Task.FromResult(true);
        }
        return Task.FromResult(openedDocuments.Remove(request.identifier));
    }
}
