using System.ComponentModel;
using System.Reflection;
using Bannerlord.UIExtenderEx.Extensions;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.ViewModels;

internal class ViewModelWrapper : ViewModel
{
	public ViewModel? Object { get; }

	protected ViewModelWrapper(ViewModel @object)
	{
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Expected O, but got Unknown
		Object = @object;
		foreach (PropertyInfo viewModelProperty in ((ViewModel)(object)this).GetViewModelProperties())
		{
			((ViewModel)(object)this).AddProperty(viewModelProperty.Name, viewModelProperty);
		}
		foreach (MethodInfo viewModelMethod in ((ViewModel)(object)this).GetViewModelMethods())
		{
			((ViewModel)(object)this).AddMethod(viewModelMethod.Name, viewModelMethod);
		}
		IViewModel val = (IViewModel)(object)Object;
		if (val != null)
		{
			val.PropertyChangedWithValue += new PropertyChangedWithValueEventHandler(OnPropertyChangedWithValueEventHandler);
			return;
		}
		INotifyPropertyChanged notifyPropertyChanged = (INotifyPropertyChanged)Object;
		if (notifyPropertyChanged != null)
		{
			notifyPropertyChanged.PropertyChanged += OnPropertyChangedEventHandler;
		}
	}

	private void OnPropertyChangedEventHandler(object? sender, PropertyChangedEventArgs args)
	{
		((ViewModel)this).OnPropertyChanged(args.PropertyName);
	}

	private void OnPropertyChangedWithValueEventHandler(object? sender, PropertyChangedWithValueEventArgs args)
	{
		((ViewModel)this).OnPropertyChangedWithValue(args.Value, args.PropertyName);
	}

	public override void RefreshValues()
	{
		ViewModel? obj = Object;
		if (obj != null)
		{
			obj.RefreshValues();
		}
		((ViewModel)this).RefreshValues();
	}

	public override void OnFinalize()
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		if (Object != null)
		{
			Object.PropertyChanged -= OnPropertyChangedEventHandler;
			Object.PropertyChangedWithValue -= new PropertyChangedWithValueEventHandler(OnPropertyChangedWithValueEventHandler);
			Object.OnFinalize();
		}
		((ViewModel)this).OnFinalize();
	}
}
