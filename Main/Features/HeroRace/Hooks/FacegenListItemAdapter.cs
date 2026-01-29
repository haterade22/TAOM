using TaleWorlds.MountAndBlade.ViewModelCollection.FaceGenerator;

namespace TAOM.Features.HeroRace.Hooks;

public class FacegenListItemAdapter : IFaceGenItem
{
    private readonly FacegenListItemVM _item;

    public int Index => _item.Index;

    public string ImagePath
    {
        get => _item.ImagePath;
        set => _item.ImagePath = value;
    }

    public FacegenListItemAdapter(FacegenListItemVM item)
    {
        _item = item;
    }
}
