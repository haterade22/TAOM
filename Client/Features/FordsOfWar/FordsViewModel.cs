using TaleWorlds.Library;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// Base of the command panel's view models. <c>GauntletView</c> subscribes to the plain property-changed event and to the typed ones;
/// the plain one makes it read the value back by property name through reflection, and the typed ones carry the value and skip that
/// read, so each setter goes through one of these.
/// </summary>
public abstract class FordsViewModel : ViewModel
{
    protected void Set(ref string field, string value, string name)
    {
        if (field == value)
            return;

        field = value;
        OnPropertyChangedWithValue(value, name);
    }

    protected void Set(ref bool field, bool value, string name)
    {
        if (field == value)
            return;

        field = value;
        OnPropertyChangedWithValue(value, name);
    }

    protected void Set(ref float field, float value, string name)
    {
        if (field == value)
            return;

        field = value;
        OnPropertyChangedWithValue(value, name);
    }
}
