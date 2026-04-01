using System;
using System.Reflection;
using TaleWorlds.Engine.GauntletUI;
using TAOM.Core.Logging;

namespace TAOM.Features.FactionMap;

public sealed class CultureStageProgressionService : ICultureStageProgressionService
{
    private readonly IModLogger _logger;

    private const BindingFlags AnyInstance =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public CultureStageProgressionService(IModLogger logger)
    {
        _logger = logger;
    }

    public void InvokeNextStage(object viewInstance, string cultureId, bool isFemale)
    {
        if (viewInstance == null)
        {
            _logger.LogError("InvokeNextStage: viewInstance is null");
            return;
        }

        try
        {
            var charCreationMgrField = viewInstance.GetType().GetField("_characterCreationManager", AnyInstance);
            var charCreationMgr = charCreationMgrField?.GetValue(viewInstance);
            if (charCreationMgr == null)
            {
                _logger.LogError("InvokeNextStage: _characterCreationManager not found on view");
                return;
            }

            var affirmativeField = viewInstance.GetType().GetField("_affirmativeAction", AnyInstance);
            var affirmativeAction = affirmativeField?.GetValue(viewInstance) as Delegate;
            affirmativeAction?.DynamicInvoke();
        }
        catch (Exception ex)
        {
            _logger.LogError($"NextStage error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public void LoadFactionMapResources()
    {
        try
        {
            UIResourceManager.BrushFactory.LoadBrushFile("FactionMap");
        }
        catch (Exception brushEx)
        {
            _logger.LogError($"Could not load FactionMap brushes: {brushEx.Message}");
        }

        try
        {
            var spriteData = UIResourceManager.SpriteData;
            if (spriteData != null && spriteData.SpriteCategories.ContainsKey("ui_group1"))
            {
                var cat = spriteData.SpriteCategories["ui_group1"];
                if (!cat.IsLoaded)
                    cat.Load(UIResourceManager.ResourceContext, UIResourceManager.ResourceDepot);
            }
        }
        catch (Exception spriteEx)
        {
            _logger.LogError($"Could not load sprite category: {spriteEx.Message}");
        }
    }
}
