using System.Reflection;

namespace HarmonyLib;

/// <summary>General extensions for collections</summary>
public static class MethodBaseExtensions
{
	/// <summary>Tests a class member if it has an IL method body (external methods for example don't have a body)</summary>
	/// <param name="member">The member to test</param>
	/// <returns>Returns true if the member has an IL body or false if not</returns>
	public static bool HasMethodBody(this MethodBase member)
	{
		return (member.GetMethodBody()?.GetILAsByteArray()?.Length).GetValueOrDefault() > 0;
	}
}
