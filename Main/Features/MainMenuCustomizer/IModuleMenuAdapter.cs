namespace TAOM.Features.MainMenuCustomizer;

public interface IModuleMenuAdapter
{
    void HideOption(string id);
    void RenameOption(string id, string newName);
}
