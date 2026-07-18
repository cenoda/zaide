using Zaide.Features.Editor.Domain;

namespace Zaide.Features.Editor.Presentation;

/// <summary>
/// Factory for creating EditorViewModel instances. Encapsulates the
/// dependencies required to construct an editor session.
/// </summary>
public interface IEditorSessionFactory
{
    /// <summary>
    /// Creates a new EditorViewModel for the given document.
    /// </summary>
    EditorViewModel Create(Document document);
}
