using Zaide.Features.Editor.Contracts;
using Zaide.Features.Editor.Domain;
using Zaide.Features.Language.Contracts;
using Zaide.Features.Settings.Contracts;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Internal implementation of IEditorSessionFactory. Constructs EditorViewModel
/// instances with the required dependencies.
/// </summary>
internal class EditorSessionFactory : IEditorSessionFactory
{
    private readonly IFileService _fileService;
    private readonly ISettingsService? _settingsService;
    private readonly ILanguageFormattingService? _formattingService;

    public EditorSessionFactory(
        IFileService fileService,
        ISettingsService? settingsService = null,
        ILanguageFormattingService? formattingService = null)
    {
        _fileService = fileService;
        _settingsService = settingsService;
        _formattingService = formattingService;
    }

    public EditorViewModel Create(Document document)
    {
        return new EditorViewModel(
            document,
            _fileService,
            _settingsService,
            _formattingService);
    }
}
