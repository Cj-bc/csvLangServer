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
        string[] content = await File.ReadAllLinesAsync(param.TextDocument.Uri.GetFileSystemPath());
        SignatureInformation signature = new SignatureInformation() with { Label = "Example Signature" };
        SignatureHelp ret = new SignatureHelp() with { Signatures = Container<SignatureInformation>.From(signature) };
        return ret;
    }
}
