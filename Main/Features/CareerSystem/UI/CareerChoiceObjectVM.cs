using TaleWorlds.Library;
using TAOM.Features.CareerSystem.Domain;

namespace TAOM.Features.CareerSystem.UI;

public class CareerChoiceObjectVM : ViewModel
{
    private readonly CareerChoiceDefinition _choice;
    private bool _isTaken;
    private bool _isFreeToTake;

    public CareerChoiceObjectVM(CareerChoiceDefinition choice, bool isTaken, bool isFreeToTake)
    {
        _choice = choice;
        _isTaken = isTaken;
        _isFreeToTake = isFreeToTake && !isTaken;
    }

    [DataSourceProperty]
    public string Name => _choice.Id;

    [DataSourceProperty]
    public string Description => _choice.Description;

    [DataSourceProperty]
    public string IconSprite => _choice.IconSprite;

    [DataSourceProperty]
    public bool IsKeystone => _choice.Type == ChoiceType.Keystone;

    [DataSourceProperty]
    public bool IsTaken
    {
        get => _isTaken;
        set
        {
            if (_isTaken != value)
            {
                _isTaken = value;
                OnPropertyChangedWithValue(value, nameof(IsTaken));
            }
        }
    }

    [DataSourceProperty]
    public bool IsFreeToTake
    {
        get => _isFreeToTake;
        set
        {
            if (_isFreeToTake != value)
            {
                _isFreeToTake = value;
                OnPropertyChangedWithValue(value, nameof(IsFreeToTake));
            }
        }
    }

    public string ChoiceId => _choice.Id;
}
