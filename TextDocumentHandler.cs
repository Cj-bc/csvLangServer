namespace CsvLangServer;

using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

using MediatR;

internal class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    private IDocumentsRepositoryService documentRepo;
    public TextDocumentSyncHandler(IDocumentsRepositoryService documentRepository)
    {
        documentRepo = documentRepository;
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri doc) => new TextDocumentAttributes(doc, "csv");

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability textSyncCaps, ClientCapabilities clientCaps)
    {
        TextDocumentSyncRegistrationOptions opts = new(TextDocumentSyncKind.Full);
        return opts;
    }

    public async override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
    {
        // TODO: What to do if given TextDocumentIdentifier is already registered?
        await documentRepo.Handle(new PutTextDocumentItemRequest(notification.TextDocument), token);
        return Unit.Value;
    }

    public async override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
    {
	// TODO: What to do if given TextDocumentIdentifier is not in the map?
        await documentRepo.Handle(new DeleteTextDocumentItemRequest(notification.TextDocument.Uri), token);
        return Unit.Value;
    }

    public async override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
    {
        TextDocumentIdentifier identifier = new(notification.TextDocument.Uri);
        if (await documentRepo.Handle(new GetTextDocumentItemRequest(identifier), token) is TextDocumentItem item)
        {
	    foreach (TextDocumentContentChangeEvent? change in notification.ContentChanges)
	    {
		List<string> c = item.Text.Split("\n").ToList();
		if (change.Range is Range modifiedRange)
		{
		    ApplyChange(modifiedRange, change.Text, ref c);
		    item = item with { Text = c.Aggregate("", (a, b) => $"{a}\n{b}") };
		} else
		{
		    item = item with { Text = change.Text };
		}
	    }
            await documentRepo.Handle(new PutTextDocumentItemRequest(item), token);
        }
        return Unit.Value;
    }

    // TODO: What to do when it saves file?
    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken token) => Unit.Task;

    /// <returns>true if change is applied successfully. False otherwise.</returns>
    private bool ApplyChange(Range range, string newText, ref List<string> contents)
    {
        int startLine = range.Start.Line;
        int startChar = range.Start.Character;
        int endLine = range.End.Line;
        int endChar = range.End.Character;
	
	if (!ValidateRange(range, contents)) return false;

        string[] linedNewText = newText.Split("\n");

	contents[startLine] = $"{contents[startLine].Substring(0, startChar-1)}{newText[0]}";
	contents.InsertRange(startLine+1, linedNewText.Skip(1).Append(contents[endLine].Substring(endChar)));

	return true;
    }

    private bool ValidateRange(Range range, in List<string> contents)
    {
        int startLine = range.Start.Line;
        int startChar = range.Start.Character;
        int endLine = range.End.Line;
        int endChar = range.End.Character;

        return (0 <= startLine && startLine < contents.Count+1
                && 0 <= endLine && endLine < contents.Count+1
		&& startLine <= endLine
		&& !(startLine == endLine && endChar < startChar)
		&& 0 <= startChar && startChar < contents[startLine].Length
		&& 0 <= endChar && endChar < contents[endLine].Length
        );
    }
}
