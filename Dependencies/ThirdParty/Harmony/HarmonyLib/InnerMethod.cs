using System;
using System.Linq;
using System.Reflection;

namespace HarmonyLib;

/// <summary>Occcurances of a method that is called inside some outer method</summary>
[Serializable]
public class InnerMethod
{
	[NonSerialized]
	private MethodInfo method;

	private int methodToken;

	private string moduleGUID;

	/// <summary>Which occcurances (1-based) of the method, negative numbers are counting from the end, empty array means all occurances</summary>
	public int[] positions;

	/// <summary>The inner method</summary>
	public MethodInfo Method
	{
		get
		{
			if ((object)method == null)
			{
				method = AccessTools.GetMethodByModuleAndToken(moduleGUID, methodToken);
			}
			return method;
		}
		set
		{
			method = value;
			methodToken = method.MetadataToken;
			moduleGUID = method.Module.ModuleVersionId.ToString();
		}
	}

	/// <summary>Creates an InnerMethod</summary>
	/// <param name="method">The inner method</param>
	/// <param name="positions">Which occcurances (1-based) of the method, negative numbers are counting from the end, empty array means all occurances</param>
	public InnerMethod(MethodInfo method, params int[] positions)
	{
		if (method == null)
		{
			throw new ArgumentNullException("method");
		}
		if (positions.Any((int p) => p == 0))
		{
			throw new ArgumentException("positions cannot contain zeros");
		}
		Method = method;
		this.positions = positions;
	}

	internal InnerMethod(int methodToken, string moduleGUID, int[] positions)
	{
		this.methodToken = methodToken;
		this.moduleGUID = moduleGUID;
		this.positions = positions;
	}
}
