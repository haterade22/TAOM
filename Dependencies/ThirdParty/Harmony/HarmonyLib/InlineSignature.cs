using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Mono.Cecil;
using MonoMod.Utils;

namespace HarmonyLib;

/// <summary>
///  A mutable representation of an inline signature, similar to Mono.Cecil's CallSite.
///  Used by the calli instruction, can be used by transpilers
///  </summary>
internal class InlineSignature : ICallSiteGenerator
{
	/// <summary>
	/// A mutable representation of a parameter type with an attached type modifier,
	/// similar to Mono.Cecil's OptionalModifierType / RequiredModifierType and C#'s modopt / modreq
	/// </summary>
	public class ModifierType
	{
		/// <summary>Whether this is a modopt (optional modifier type) or a modreq (required modifier type)</summary>
		public bool IsOptional;

		/// <summary>The modifier type attached to the parameter type</summary>
		public Type Modifier;

		/// <summary>The modified parameter type</summary>
		public object Type;

		/// <summary>Returns a string representation of the modifier type</summary>
		/// <returns>A string representation of the modifier type</returns>
		public override string ToString()
		{
			return $"{((Type is Type type) ? type.FullDescription() : Type?.ToString())} mod{(IsOptional ? "opt" : "req")}({Modifier?.FullDescription()})";
		}

		internal TypeReference ToTypeReference(ModuleDefinition module)
		{
			if (IsOptional)
			{
				return new OptionalModifierType(module.ImportReference(Modifier), GetTypeReference(module, Type));
			}
			return new RequiredModifierType(module.ImportReference(Modifier), GetTypeReference(module, Type));
		}
	}

	/// <summary>See <see cref="F:System.Reflection.CallingConventions.HasThis" /></summary>
	public bool HasThis { get; set; }

	/// <summary>See <see cref="F:System.Reflection.CallingConventions.ExplicitThis" /></summary>
	public bool ExplicitThis { get; set; }

	/// <summary>See <see cref="T:System.Runtime.InteropServices.CallingConvention" /></summary>
	public CallingConvention CallingConvention { get; set; } = CallingConvention.Winapi;

	/// <summary>The list of all parameter types or function pointer signatures received by the call site</summary>
	public List<object> Parameters { get; set; } = new List<object>();

	/// <summary>The return type or function pointer signature returned by the call site</summary>
	public object ReturnType { get; set; } = typeof(void);

	/// <summary>Returns a string representation of the inline signature</summary>
	/// <returns>A string representation of the inline signature</returns>
	public override string ToString()
	{
		return ((ReturnType is Type type) ? type.FullDescription() : ReturnType?.ToString()) + " (" + Parameters.Join((object p) => (!(p is Type type2)) ? p?.ToString() : type2.FullDescription()) + ")";
	}

	internal static TypeReference GetTypeReference(ModuleDefinition module, object param)
	{
		if (!(param is Type type))
		{
			if (!(param is InlineSignature inlineSignature))
			{
				if (param is ModifierType modifierType)
				{
					return modifierType.ToTypeReference(module);
				}
				throw new NotSupportedException($"Unsupported inline signature parameter type: {param} ({param?.GetType().FullDescription()})");
			}
			return inlineSignature.ToFunctionPointer(module);
		}
		return module.ImportReference(type);
	}

	CallSite ICallSiteGenerator.ToCallSite(ModuleDefinition module)
	{
		CallSite callSite = new CallSite(GetTypeReference(module, ReturnType))
		{
			HasThis = HasThis,
			ExplicitThis = ExplicitThis,
			CallingConvention = (MethodCallingConvention)((byte)CallingConvention - 1)
		};
		foreach (object parameter in Parameters)
		{
			callSite.Parameters.Add(new ParameterDefinition(GetTypeReference(module, parameter)));
		}
		return callSite;
	}

	private FunctionPointerType ToFunctionPointer(ModuleDefinition module)
	{
		FunctionPointerType functionPointerType = new FunctionPointerType
		{
			ReturnType = GetTypeReference(module, ReturnType),
			HasThis = HasThis,
			ExplicitThis = ExplicitThis,
			CallingConvention = (MethodCallingConvention)((byte)CallingConvention - 1)
		};
		foreach (object parameter in Parameters)
		{
			functionPointerType.Parameters.Add(new ParameterDefinition(GetTypeReference(module, parameter)));
		}
		return functionPointerType;
	}
}
