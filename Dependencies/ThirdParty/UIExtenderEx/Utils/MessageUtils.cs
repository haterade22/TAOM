#define TRACE
using System.Diagnostics;
using TaleWorlds.Library;

namespace Bannerlord.UIExtenderEx.Utils;

internal static class MessageUtils
{
	public static void Fail(string text)
	{
		Trace.Fail(text);
		DisplayUserError(text);
	}

	public static void Assert(bool condition, string text = "no description")
	{
		Trace.Assert(condition, "UIExtenderEx failure: " + text + ".");
	}

	public static void CompatAssert(bool condition, string text = "no description")
	{
		Trace.Assert(condition, "Bannerlord compatibility failure: " + text + ".");
	}

	public static void DisplayUserError(string text, params object[] args)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		Trace.TraceError(text, args);
		InformationManager.DisplayMessage(new InformationMessage("UIExtenderEx: " + string.Format(text, args), Colors.Red));
	}

	public static void DisplayUserWarning(string text, params object[] args)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Expected O, but got Unknown
		Trace.TraceWarning(text, args);
		InformationManager.DisplayMessage(new InformationMessage("UIExtender: " + string.Format(text, args), Colors.Yellow));
	}
}
