using OmniSharp.Extensions.LanguageServer.Server;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

var server = await LanguageServer.From(
    options =>
    options
    .WithInput(Console.OpenStandardInput())
    .WithOutput(Console.OpenStandardOutput())
    .WithHandler<SignatureHelpHandler>()
);

await server.WaitForExit.ConfigureAwait(false);


internal class SignatureHelpHandler : ISignatureHelpHandler
{
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

        string[] content = await File.ReadAllLinesAsync(param.TextDocument.Uri.GetFileSystemPath());
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
