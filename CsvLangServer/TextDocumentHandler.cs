namespace CsvLangServer;

using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;

using MediatR;

public class TextDocumentSyncHandler : TextDocumentSyncHandlerBase
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
                    item = item with { Text = string.Join("\n", c) };
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
    private bool ApplyChange(in Range range, in string newText, ref List<string> contents)
    {
        int startLine = range.Start.Line;
        int startChar = range.Start.Character;
        int endLine = range.End.Line;
        int endChar = range.End.Character;
	IEnumerable<string> linedNewText = newText.Split("\n").Select(str => str.TrimEnd('\r'));
	
	if (!ValidateRange(range, contents)) return false;

        if (IsAppendingRange(range, contents))
        {
            contents.AddRange(linedNewText);
            return true;
        }


        if (startLine == endLine)
        {
            string oldContent = contents[startLine];
	    string pre = startChar == 0 ? "" : oldContent.Substring(0, startChar - 1);
	    string post = oldContent.Substring(endChar);
	    contents.RemoveAt(startLine);
	    contents.Insert(startLine, $"{pre}{newText}{post}");

        } else
        {
	    string oldStartContent = contents[startLine];
	    string oldEndContent = contents[endLine];
            contents.RemoveRange(startLine, endLine - startLine + 1);
            contents.Insert(startLine, $"{oldStartContent.Substring(0, startChar - 1)}{linedNewText[0]}");

            if (endChar == 0)
            {
                contents.InsertRange(startLine + 1, linedNewText.Skip(1).Append(contents[endLine]));
            } else
            {
                contents.InsertRange(startLine + 1, linedNewText.Skip(1).Take(linedNewText.Count() - 2)
                             .Append($"{linedNewText[linedNewText.Count() - 1]}{oldEndContent.Substring(endChar)}"));
            }
        }


        return true;
    }

    private bool IsAppendingRange(in Range range, in List<string> contents) => contents.Count <= range.Start.Line || contents.Count <= range.End.Line;

    private bool ValidateRange(in Range range, in List<string> contents)
    {
        int startLine = range.Start.Line;
        int startChar = range.Start.Character;
        int endLine = range.End.Line;
        int endChar = range.End.Character;

        if (!IsAppendingRange(range, contents))
        {
	    bool endCharValidation = startChar < contents[startLine].Length && endChar < contents[endLine].Length;

            return 0 <= startLine && 0 <= endLine
                && startLine <= endLine
                && !(startLine == endLine && endChar < startChar)
                && 0 <= startChar && 0 <= endChar
                && endCharValidation;
        } else
        {
	    // when appending new line at end of current contents
	    return 0 <= startLine && 0 <= endLine
		&& startLine <= endLine
		&& !(startLine == endLine && endChar < startChar)
		&& 0 <= startChar && 0 <= endChar;
        }
    }
}
