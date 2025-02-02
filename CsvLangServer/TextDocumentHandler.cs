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

        if (!RemoveRange(range, ref contents))
        {
            return false;
        }

        string oldStartLine = contents[startLine];
        contents.RemoveAt(startLine);
        contents.InsertRange(startLine, linedNewText.Select((c, idx) => (idx) switch
	{
	    0 when linedNewText.Count() == 1 => oldStartLine.Substring(0, startChar) + c + oldStartLine.Substring(startChar),
	    0 => oldStartLine.Substring(0, startChar) + c,
	    _ when idx == linedNewText.Count() - 1 => c + oldStartLine.Substring(startChar),
	    _ => c
	}));

        return true;
    }

    /// Remove given range from <param name="contents">contents</param>
    private bool RemoveRange(in Range range, ref List<string> contents)
    {
        int startLine = range.Start.Line;
        int startChar = range.Start.Character;
        int endLine = range.End.Line;
        int endChar = range.End.Character;

        if (!ValidateRange(range, contents))
        {
            return false;
        }

        if (IsAppendingRange(range, contents))
        {
            return true;
        }

        string startLineContent = contents[startLine];
        string endLineContent = contents[endLine];


	// when range is within one line
        if (startLine == endLine)
        {
	    // startChar = 1, endchar = 7
	    // 01234567
	    // abcdefgh
	    //  +----/
	    // --> ah
	    contents[startLine] = startLineContent.Substring(0, startChar) + startLineContent.Substring(endChar);
	    return true;
        }

	// when range indicates "until end of line"
        if (startLine + 1 == endLine && endChar == 0)
        {
            contents[startLine] = startLineContent.Substring(0, startChar);
	    return true;
        }

	// when range continue across several lines
        contents[startLine] = startLineContent.Substring(0, startChar);
        contents.RemoveRange(startLine+1, endLine - startLine - 1);
        contents[endLine] = endLineContent.Substring(endChar);

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
