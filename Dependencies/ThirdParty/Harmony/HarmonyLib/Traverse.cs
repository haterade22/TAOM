using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace HarmonyLib;

/// <summary>A reflection helper to read and write private elements</summary>
/// <typeparam name="T">The result type defined by GetValue()</typeparam>
public class Traverse<T>
{
	private readonly Traverse traverse;

	/// <summary>Gets/Sets the current value</summary>
	/// <value>The value to read or write</value>
	public T Value
	{
		get
		{
			return traverse.GetValue<T>();
		}
		set
		{
			traverse.SetValue(value);
		}
	}

	private Traverse()
	{
	}

	/// <summary>Creates a traverse instance from an existing instance</summary>
	/// <param name="traverse">The existing <see cref="T:HarmonyLib.Traverse" /> instance</param>
	public Traverse(Traverse traverse)
	{
		this.traverse = traverse;
	}
}
/// <summary>A reflection helper to read and write private elements</summary>
public class Traverse
{
	private static readonly AccessCache Cache;

	private readonly Type _type;

	private readonly object _root;

	private readonly MemberInfo _info;

	private readonly MethodBase _method;

	private readonly object[] _params;

	/// <summary>A default field action that copies fields to fields</summary>
	public static Action<Traverse, Traverse> CopyFields;

	/// <summary>Checks if the current traverse instance is for a field</summary>
	/// <returns>True if its a field</returns>
	public bool IsField => _info is FieldInfo;

	/// <summary>Checks if the current traverse instance is for a property</summary>
	/// <returns>True if its a property</returns>
	public bool IsProperty => _info is PropertyInfo;

	/// <summary>Checks if the current field or property is writeable</summary>
	/// <returns>True if writing is possible</returns>
	public bool IsWriteable
	{
		get
		{
			if (_info is FieldInfo fieldInfo)
			{
				bool flag = fieldInfo.IsLiteral && !fieldInfo.IsInitOnly && fieldInfo.IsStatic;
				bool flag2 = !fieldInfo.IsLiteral && fieldInfo.IsInitOnly && fieldInfo.IsStatic;
				if (!flag)
				{
					return !flag2;
				}
				return false;
			}
			if (_info is PropertyInfo propertyInfo)
			{
				return propertyInfo.CanWrite;
			}
			return false;
		}
	}

	[MethodImpl(MethodImplOptions.Synchronized)]
	static Traverse()
	{
		CopyFields = delegate(Traverse from, Traverse to)
		{
			if (to.IsWriteable)
			{
				to.SetValue(from.GetValue());
			}
		};
		if (Cache == null)
		{
			Cache = new AccessCache();
		}
	}

	/// <summary>Creates a new traverse instance from a class/type</summary>
	/// <param name="type">The class/type</param>
	/// <returns>A <see cref="T:HarmonyLib.Traverse" /> instance</returns>
	public static Traverse Create(Type type)
	{
		return new Traverse(type);
	}

	/// <summary>Creates a new traverse instance from a class T</summary>
	/// <typeparam name="T">The class</typeparam>
	/// <returns>A <see cref="T:HarmonyLib.Traverse" /> instance</returns>
	public static Traverse Create<T>()
	{
		return Create(typeof(T));
	}

	/// <summary>Creates a new traverse instance from an instance</summary>
	/// <param name="root">The object</param>
	/// <returns>A <see cref="T:HarmonyLib.Traverse" /> instance</returns>
	public static Traverse Create(object root)
	{
		return new Traverse(root);
	}

	/// <summary>Creates a new traverse instance from a named type</summary>
	/// <param name="name">The type name, for format see <see cref="M:HarmonyLib.AccessTools.TypeByName(System.String)" /></param>
	/// <returns>A <see cref="T:HarmonyLib.Traverse" /> instance</returns>
	public static Traverse CreateWithType(string name)
	{
		return new Traverse(AccessTools.TypeByName(name));
	}

	/// <summary>Creates a new and empty traverse instance</summary>
	private Traverse()
	{
	}

	/// <summary>Creates a new traverse instance from a class/type</summary>
	/// <param name="type">The class/type</param>
	public Traverse(Type type)
	{
		_type = type;
	}

	/// <summary>Creates a new traverse instance from an instance</summary>
	/// <param name="root">The object</param>
	public Traverse(object root)
	{
		_root = root;
		_type = root?.GetType();
	}

	private Traverse(object root, MemberInfo info, object[] index)
	{
		_root = root;
		_type = root?.GetType() ?? info.GetUnderlyingType();
		_info = info;
		_params = index;
	}

	private Traverse(object root, MethodInfo method, object[] parameter)
	{
		_root = root;
		_type = method.ReturnType;
		_method = method;
		_params = parameter;
	}

	/// <summary>Gets the current value</summary>
	/// <value>The value</value>
	public object GetValue()
	{
		if (_info is FieldInfo)
		{
			return ((FieldInfo)_info).GetValue(_root);
		}
		if (_info is PropertyInfo)
		{
			return ((PropertyInfo)_info).GetValue(_root, AccessTools.all, null, _params, CultureInfo.CurrentCulture);
		}
		if ((object)_method != null)
		{
			return _method.Invoke(_root, _params);
		}
		if (_root == null && (object)_type != null)
		{
			return _type;
		}
		return _root;
	}

	/// <summary>Gets the current value</summary>
	/// <typeparam name="T">The type of the value</typeparam>
	/// <value>The value</value>
	public T GetValue<T>()
	{
		object value = GetValue();
		if (value == null)
		{
			return default(T);
		}
		return (T)value;
	}

	/// <summary>Invokes the current method with arguments and returns the result</summary>
	/// <param name="arguments">The method arguments</param>
	/// <value>The value returned by the method</value>
	public object GetValue(params object[] arguments)
	{
		if ((object)_method == null)
		{
			throw new Exception("cannot get method value without method");
		}
		return _method.Invoke(_root, arguments);
	}

	/// <summary>Invokes the current method with arguments and returns the result</summary>
	/// <typeparam name="T">The type of the value</typeparam>
	/// <param name="arguments">The method arguments</param>
	/// <value>The value returned by the method</value>
	public T GetValue<T>(params object[] arguments)
	{
		if ((object)_method == null)
		{
			throw new Exception("cannot get method value without method");
		}
		return (T)_method.Invoke(_root, arguments);
	}

	/// <summary>Sets a value of the current field or property</summary>
	/// <param name="value">The value</param>
	/// <returns>The same traverse instance</returns>
	public Traverse SetValue(object value)
	{
		if (_info is FieldInfo)
		{
			((FieldInfo)_info).SetValue(_root, value, AccessTools.all, null, CultureInfo.CurrentCulture);
		}
		if (_info is PropertyInfo)
		{
			((PropertyInfo)_info).SetValue(_root, value, AccessTools.all, null, _params, CultureInfo.CurrentCulture);
		}
		if ((object)_method != null)
		{
			throw new Exception("cannot set value of method " + _method.FullDescription());
		}
		return this;
	}

	/// <summary>Gets the type of the current field or property</summary>
	/// <returns>The type</returns>
	public Type GetValueType()
	{
		if (_info is FieldInfo)
		{
			return ((FieldInfo)_info).FieldType;
		}
		if (_info is PropertyInfo)
		{
			return ((PropertyInfo)_info).PropertyType;
		}
		return null;
	}

	private Traverse Resolve()
	{
		if (_root == null)
		{
			if (_info is FieldInfo { IsStatic: not false })
			{
				return new Traverse(GetValue());
			}
			if (_info is PropertyInfo propertyInfo && propertyInfo.GetGetMethod().IsStatic)
			{
				return new Traverse(GetValue());
			}
			if ((object)_method != null && _method.IsStatic)
			{
				return new Traverse(GetValue());
			}
			if ((object)_type != null)
			{
				return this;
			}
		}
		return new Traverse(GetValue());
	}

	/// <summary>Moves the current traverse instance to a inner type</summary>
	/// <param name="name">The type name</param>
	/// <returns>A traverse instance</returns>
	public Traverse Type(string name)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		if ((object)_type == null)
		{
			return new Traverse();
		}
		Type type = AccessTools.Inner(_type, name);
		if ((object)type == null)
		{
			return new Traverse();
		}
		return new Traverse(type);
	}

	/// <summary>Moves the current traverse instance to a field</summary>
	/// <param name="name">The type name</param>
	/// <returns>A traverse instance</returns>
	public Traverse Field(string name)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		Traverse traverse = Resolve();
		if ((object)traverse._type == null)
		{
			return new Traverse();
		}
		FieldInfo fieldInfo = Cache.GetFieldInfo(traverse._type, name);
		if ((object)fieldInfo == null)
		{
			return new Traverse();
		}
		if (!fieldInfo.IsStatic && traverse._root == null)
		{
			return new Traverse();
		}
		return new Traverse(traverse._root, fieldInfo, null);
	}

	/// <summary>Moves the current traverse instance to a field</summary>
	/// <typeparam name="T">The type of the field</typeparam>
	/// <param name="name">The type name</param>
	/// <returns>A traverse instance</returns>
	public Traverse<T> Field<T>(string name)
	{
		return new Traverse<T>(Field(name));
	}

	/// <summary>Gets all fields of the current type</summary>
	/// <returns>A list of field names</returns>
	public List<string> Fields()
	{
		Traverse traverse = Resolve();
		return AccessTools.GetFieldNames(traverse._type);
	}

	/// <summary>Moves the current traverse instance to a property</summary>
	/// <param name="name">The type name</param>
	/// <param name="index">Optional property index</param>
	/// <returns>A traverse instance</returns>
	public Traverse Property(string name, object[] index = null)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		Traverse traverse = Resolve();
		if ((object)traverse._type == null)
		{
			return new Traverse();
		}
		PropertyInfo propertyInfo = Cache.GetPropertyInfo(traverse._type, name);
		if ((object)propertyInfo == null)
		{
			return new Traverse();
		}
		return new Traverse(traverse._root, propertyInfo, index);
	}

	/// <summary>Moves the current traverse instance to a field</summary>
	/// <typeparam name="T">The type of the property</typeparam>
	/// <param name="name">The type name</param>
	/// <param name="index">Optional property index</param>
	/// <returns>A traverse instance</returns>
	public Traverse<T> Property<T>(string name, object[] index = null)
	{
		return new Traverse<T>(Property(name, index));
	}

	/// <summary>Gets all properties of the current type</summary>
	/// <returns>A list of property names</returns>
	public List<string> Properties()
	{
		Traverse traverse = Resolve();
		return AccessTools.GetPropertyNames(traverse._type);
	}

	/// <summary>Moves the current traverse instance to a method</summary>
	/// <param name="name">The name of the method</param>
	/// <param name="arguments">The arguments defining the argument types of the method overload</param>
	/// <returns>A traverse instance</returns>
	public Traverse Method(string name, params object[] arguments)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		Traverse traverse = Resolve();
		if ((object)traverse._type == null)
		{
			return new Traverse();
		}
		Type[] types = AccessTools.GetTypes(arguments);
		MethodBase methodInfo = Cache.GetMethodInfo(traverse._type, name, types);
		if ((object)methodInfo == null)
		{
			return new Traverse();
		}
		return new Traverse(traverse._root, (MethodInfo)methodInfo, arguments);
	}

	/// <summary>Moves the current traverse instance to a method</summary>
	/// <param name="name">The name of the method</param>
	/// <param name="paramTypes">The argument types of the method</param>
	/// <param name="arguments">The arguments for the method</param>
	/// <returns>A traverse instance</returns>
	public Traverse Method(string name, Type[] paramTypes, object[] arguments = null)
	{
		if (name == null)
		{
			throw new ArgumentNullException("name");
		}
		Traverse traverse = Resolve();
		if ((object)traverse._type == null)
		{
			return new Traverse();
		}
		MethodBase methodInfo = Cache.GetMethodInfo(traverse._type, name, paramTypes);
		if ((object)methodInfo == null)
		{
			return new Traverse();
		}
		return new Traverse(traverse._root, (MethodInfo)methodInfo, arguments);
	}

	/// <summary>Gets all methods of the current type</summary>
	/// <returns>A list of method names</returns>
	public List<string> Methods()
	{
		Traverse traverse = Resolve();
		return AccessTools.GetMethodNames(traverse._type);
	}

	/// <summary>Checks if the current traverse instance is for a field</summary>
	/// <returns>True if its a field</returns>
	public bool FieldExists()
	{
		if ((object)_info != null)
		{
			return _info is FieldInfo;
		}
		return false;
	}

	/// <summary>Checks if the current traverse instance is for a property</summary>
	/// <returns>True if its a property</returns>
	public bool PropertyExists()
	{
		if ((object)_info != null)
		{
			return _info is PropertyInfo;
		}
		return false;
	}

	/// <summary>Checks if the current traverse instance is for a method</summary>
	/// <returns>True if its a method</returns>
	public bool MethodExists()
	{
		return (object)_method != null;
	}

	/// <summary>Checks if the current traverse instance is for a type</summary>
	/// <returns>True if its a type</returns>
	public bool TypeExists()
	{
		return (object)_type != null;
	}

	/// <summary>Iterates over all fields of the current type and executes a traverse action</summary>
	/// <param name="source">Original object</param>
	/// <param name="action">The action receiving a <see cref="T:HarmonyLib.Traverse" /> instance for each field</param>
	public static void IterateFields(object source, Action<Traverse> action)
	{
		Traverse sourceTrv = Create(source);
		AccessTools.GetFieldNames(source).ForEach(delegate(string f)
		{
			action(sourceTrv.Field(f));
		});
	}

	/// <summary>Iterates over all fields of the current type and executes a traverse action</summary>
	/// <param name="source">Original object</param>
	/// <param name="target">Target object</param>
	/// <param name="action">The action receiving a pair of <see cref="T:HarmonyLib.Traverse" /> instances for each field pair</param>
	public static void IterateFields(object source, object target, Action<Traverse, Traverse> action)
	{
		Traverse sourceTrv = Create(source);
		Traverse targetTrv = Create(target);
		AccessTools.GetFieldNames(source).ForEach(delegate(string f)
		{
			action(sourceTrv.Field(f), targetTrv.Field(f));
		});
	}

	/// <summary>Iterates over all fields of the current type and executes a traverse action</summary>
	/// <param name="source">Original object</param>
	/// <param name="target">Target object</param>
	/// <param name="action">The action receiving a dot path representing the field pair and the <see cref="T:HarmonyLib.Traverse" /> instances</param>
	public static void IterateFields(object source, object target, Action<string, Traverse, Traverse> action)
	{
		Traverse sourceTrv = Create(source);
		Traverse targetTrv = Create(target);
		AccessTools.GetFieldNames(source).ForEach(delegate(string f)
		{
			action(f, sourceTrv.Field(f), targetTrv.Field(f));
		});
	}

	/// <summary>Iterates over all properties of the current type and executes a traverse action</summary>
	/// <param name="source">Original object</param>
	/// <param name="action">The action receiving a <see cref="T:HarmonyLib.Traverse" /> instance for each property</param>
	public static void IterateProperties(object source, Action<Traverse> action)
	{
		Traverse sourceTrv = Create(source);
		AccessTools.GetPropertyNames(source).ForEach(delegate(string f)
		{
			action(sourceTrv.Property(f));
		});
	}

	/// <summary>Iterates over all properties of the current type and executes a traverse action</summary>
	/// <param name="source">Original object</param>
	/// <param name="target">Target object</param>
	/// <param name="action">The action receiving a pair of <see cref="T:HarmonyLib.Traverse" /> instances for each property pair</param>
	public static void IterateProperties(object source, object target, Action<Traverse, Traverse> action)
	{
		Traverse sourceTrv = Create(source);
		Traverse targetTrv = Create(target);
		AccessTools.GetPropertyNames(source).ForEach(delegate(string f)
		{
			action(sourceTrv.Property(f), targetTrv.Property(f));
		});
	}

	/// <summary>Iterates over all properties of the current type and executes a traverse action</summary>
	/// <param name="source">Original object</param>
	/// <param name="target">Target object</param>
	/// <param name="action">The action receiving a dot path representing the property pair and the <see cref="T:HarmonyLib.Traverse" /> instances</param>
	public static void IterateProperties(object source, object target, Action<string, Traverse, Traverse> action)
	{
		Traverse sourceTrv = Create(source);
		Traverse targetTrv = Create(target);
		AccessTools.GetPropertyNames(source).ForEach(delegate(string f)
		{
			action(f, sourceTrv.Property(f), targetTrv.Property(f));
		});
	}

	/// <summary>Returns a string that represents the current traverse</summary>
	/// <returns>A string representation</returns>
	public override string ToString()
	{
		return (_method ?? GetValue())?.ToString();
	}
}
