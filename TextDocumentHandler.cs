namespace CsvLangServer;

using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

using MediatR;

internal class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
{
    public Dictionary<TextDocumentIdentifier, TextDocumentItem> documents { get; private set; }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri doc) => new TextDocumentAttributes(doc, "csv");

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(TextSynchronizationCapability textSyncCaps, ClientCapabilities clientCaps)
    {
        TextDocumentSyncRegistrationOptions opts = new(TextDocumentSyncKind.Full);
        return opts;
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken token)
    {
	// TODO: What to do if given TextDocumentIdentifier is already registered?
        documents.Add(new (notification.TextDocument.Uri), notification.TextDocument);

        return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken token)
    {
	// TODO: What to do if given TextDocumentIdentifier is not in the map?
        documents.Remove(new(notification.TextDocument.Uri));
        return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken token)
    {
        TextDocumentIdentifier identifier = new(notification.TextDocument.Uri);
        if (!documents.ContainsKey(identifier))
        {
            return Unit.Task;
        }
        TextDocumentItem item = documents[identifier];

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

        documents[identifier] = item;

        return Unit.Task;
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

        return (0 <= startLine && startLine < contents.Count
                && 0 <= endLine && endLine < contents.Count
		&& startLine <= endLine
		&& !(startLine == endLine && endChar < startChar)
		&& 0 <= startChar && startChar < contents[startLine].Length
		&& 0 <= endChar && endChar < contents[endLine].Length
        );
    }
}
