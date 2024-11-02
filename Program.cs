using MediatR;
using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using CsvLangServer;

var server = await LanguageServer.From(
    options =>
    options
    .WithInput(Console.OpenStandardInput())
    .WithOutput(Console.OpenStandardOutput())
    .WithHandler<SignatureHelpHandler>()
    .WithHandler<TextDocumentSyncHandler>()
);

await server.WaitForExit.ConfigureAwait(false);


internal class SignatureHelpHandler : ISignatureHelpHandler
{
    private IMediator mediator;

    public SignatureHelpHandler(ILanguageServerFacade langserver)
    {
        this.mediator = mediator;
    }

    public SignatureHelpRegistrationOptions GetRegistrationOptions(SignatureHelpCapability capability, ClientCapabilities clientCapabilities)
    {
        var opts = new SignatureHelpRegistrationOptions();
        opts.DocumentSelector = TextDocumentSelector.ForPattern("*.csv");
        opts.TriggerCharacters = Container.From(",");
        opts.RetriggerCharacters = Container.From(",");

        return opts;
    }

    public async Task<SignatureHelp?> Handle(SignatureHelpParams param, CancellationToken cancellationToken)
    {
	// when on the first line, no need to return field name
	if (param.Position.Line == 0)
            return null;

        TextDocumentIdentifier id = new TextDocumentIdentifier(param.TextDocument.Uri);
        TextDocumentItem? item = await mediator.Send(new RequestTextDocumentItem(id));
        if (item is null)
        {
            return null;
        }
        string[] content = item.Text.Split("\n");

        string? currentLine = content.ElementAtOrDefault(param.Position.Line);
	if (currentLine is null)
            return null;

        int fieldNum = currentLine.Take(param.Position.Character).Where(c => c == ',').Count();
        string? fieldName = content.ElementAtOrDefault(0)?.Split(',')?.ElementAtOrDefault(fieldNum);

	if (fieldName is null)
            return null;

        SignatureInformation signature = new SignatureInformation() with { Label = fieldName };
        SignatureHelp ret = new SignatureHelp() with { Signatures = Container<SignatureInformation>.From(signature) };
        return ret;
    }
}
