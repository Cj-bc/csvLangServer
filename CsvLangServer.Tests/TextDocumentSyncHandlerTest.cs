namespace CsvLangServer.Tests;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using NUnit.Framework;

public class TextDocumentSyncHandlerTest
{
    private const string headerText = "date,type,amount,description";

    private IDocumentsRepositoryService docRepo;
    private TextDocumentSyncHandler documentHandler;
    private TextDocumentIdentifier docIdentifier;

    [SetUp]
    public void Setup()
    {
        docRepo = new OpenedDocumentsRepositoryService();
        documentHandler = new(docRepo);
    }

    [Test]
    public async Task TextDocument_OpenedTextShouldBeStored()
    {
        var toStore = new TextDocumentItem() with { Text = headerText };
        docIdentifier = new(toStore.Uri);
        DidOpenTextDocumentParams param = new();
        param.TextDocument = toStore;

        await documentHandler.Handle(param, default);
        TextDocumentItem? stored = await docRepo.Handle(new GetTextDocumentItemRequest(docIdentifier), default);

        Assert.That(stored, Is.Not.Null);
        Assert.That(stored.Text, Is.EqualTo(toStore.Text));
    }

    [Test]
    public async Task InsertInsideLine()
    {
        var item = new TextDocumentItem() with { Text = headerText };
        DidOpenTextDocumentParams registerOpen = new();
        registerOpen.TextDocument = item;
        await documentHandler.Handle(registerOpen, default);

        var id = new OptionalVersionedTextDocumentIdentifier() with { Uri = item.Uri };
        var changeEvent = new TextDocumentContentChangeEvent() with { Range = new(0, 5, 0, 9), Text = "kind" };
        var param = new DidChangeTextDocumentParams() with {
	    TextDocument = id, ContentChanges = new(changeEvent)
        };
        await documentHandler.Handle(param, default);
        TextDocumentItem? stored = await docRepo.Handle(new GetTextDocumentItemRequest(new(item.Uri)), default);

        Assert.That(stored, Is.Not.Null);
        Assert.That(stored.Text, Is.EqualTo("date,kind,amount,description"));
    }

    [Test]
    public async Task AppendNewLine()
    {
        var item = new TextDocumentItem() with { Text = headerText };
        docIdentifier = new(item.Uri);
        DidOpenTextDocumentParams registerOpen = new();
        registerOpen.TextDocument = item;
        await documentHandler.Handle(registerOpen, default);

        var identifier = new OptionalVersionedTextDocumentIdentifier() with { Uri = item.Uri };
        var changeEvent = new TextDocumentContentChangeEvent() with { Range = new(1,0,1,0), Text = "2024-01-01,deposite,100,salary"};
        var param = new DidChangeTextDocumentParams()
	    with { TextDocument = identifier, ContentChanges = new(changeEvent)
        };

        await documentHandler.Handle(param, default);
        await documentHandler.Handle(param, default);
        TextDocumentItem? stored = await docRepo.Handle(new GetTextDocumentItemRequest(new(item.Uri)), default);
	
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored.Text, Is.EqualTo(item.Text + "\n2024-01-01,deposite,100,salary"));
    }

    // List<string> original = new() {"date,type,amount,description",
    // 			       "2024-01-01,deposite,100,newInput",
    // };

    // List<string> expected = new() {"date,type,amount,description",
    // 			       "2024-01-01,deposite,100,modified",
    // 			       "2024-01-02,deposite,100,newInput"
    // };
    // Range modifiedRange = new(2, 0, 2, 0);
    // string appendedString = "2024-01-02,deposite,100,newInput";

    // TextDocumentSyncHandler.ApplyChange(modifiedRange, appendedString, ref original);
    // Assert.That(original, Is.EqualTo(expected));
}
