using System;
using System.Collections.Generic;
using System.Diagnostics.SymbolStore;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils.Cil;

namespace MonoMod.Utils;

internal static class _DMDEmit
{
	private abstract class TokenCreator
	{
		public abstract int GetTokenForType(Type type);

		public abstract int GetTokenForSig(byte[] sig);
	}

	private sealed class NetTokenCreator : TokenCreator
	{
		private readonly List<object> tokens;

		public NetTokenCreator(ILGenerator il)
		{
			Helpers.Assert((object)f_DynScope_m_tokens != null, null, "f_DynScope_m_tokens is not null");
			Helpers.Assert((object)f_DynILGen_m_scope != null, null, "f_DynILGen_m_scope is not null");
			List<object> list = (List<object>)f_DynScope_m_tokens.GetValue(f_DynILGen_m_scope.GetValue(il));
			Helpers.Assert(list != null, "DynamicMethod object list is null!", "list is not null");
			tokens = list;
		}

		public override int GetTokenForType(Type type)
		{
			tokens.Add(type.TypeHandle);
			return (tokens.Count - 1) | 0x2000000;
		}

		public override int GetTokenForSig(byte[] sig)
		{
			tokens.Add(sig);
			return (tokens.Count - 1) | 0x11000000;
		}
	}

	private sealed class MonoTokenCreator : TokenCreator
	{
		private readonly DynamicMethod dm;

		private readonly Func<DynamicMethod, object?, int> addRef;

		public MonoTokenCreator(DynamicMethod dm)
		{
			Helpers.Assert(DynamicMethod_AddRef != null, null, "DynamicMethod_AddRef is not null");
			addRef = DynamicMethod_AddRef;
			this.dm = dm;
		}

		public override int GetTokenForType(Type type)
		{
			return addRef(dm, type);
		}

		public override int GetTokenForSig(byte[] sig)
		{
			return addRef(dm, sig);
		}
	}

	private abstract class CallSiteEmitter
	{
		public abstract void EmitCallSite(DynamicMethod dm, ILGenerator il, System.Reflection.Emit.OpCode opcode, CallSite csite);
	}

	private sealed class NetCallSiteEmitter : CallSiteEmitter
	{
		public override void EmitCallSite(DynamicMethod dm, ILGenerator il, System.Reflection.Emit.OpCode opcode, CallSite csite)
		{
			TokenCreator tokenCreator = ((DynamicMethod_AddRef != null) ? ((TokenCreator)new MonoTokenCreator(dm)) : ((TokenCreator)new NetTokenCreator(il)));
			byte[] signature = new byte[32];
			int currSig = 0;
			int num = -1;
			AddData((int)((uint)csite.CallingConvention | (uint)(csite.HasThis ? 32 : 0)) | (csite.ExplicitThis ? 64 : 0));
			num = currSig++;
			List<Type> modReq = new List<Type>();
			List<Type> modOpt = new List<Type>();
			ResolveWithModifiers(csite.ReturnType, out Type type, out Type[] typeModReq, out Type[] typeModOpt, modReq, modOpt);
			AddArgument(type, typeModReq, typeModOpt);
			foreach (ParameterDefinition parameter in csite.Parameters)
			{
				if (parameter.ParameterType.IsSentinel)
				{
					AddElementType(65);
				}
				if (parameter.ParameterType.IsPinned)
				{
					AddElementType(69);
				}
				ResolveWithModifiers(parameter.ParameterType, out Type type2, out Type[] typeModReq2, out Type[] typeModOpt2, modReq, modOpt);
				AddArgument(type2, typeModReq2, typeModOpt2);
			}
			AddElementType(0);
			int num2 = currSig;
			int num3 = ((csite.Parameters.Count < 128) ? 1 : ((csite.Parameters.Count >= 16384) ? 4 : 2));
			byte[] array = new byte[currSig + num3 - 1];
			array[0] = signature[0];
			Buffer.BlockCopy(signature, num + 1, array, num + num3, num2 - (num + 1));
			signature = array;
			currSig = num;
			AddData(csite.Parameters.Count);
			currSig = num2 + (num3 - 1);
			if (signature.Length > currSig)
			{
				array = new byte[currSig];
				Array.Copy(signature, array, currSig);
				signature = array;
			}
			if (_ILGen_emit_int != null)
			{
				_ILGen_make_room.Invoke(il, new object[1] { 6 });
				_ILGen_ll_emit.Invoke(il, new object[1] { opcode });
				_ILGen_emit_int.Invoke(il, new object[1] { tokenCreator.GetTokenForSig(signature) });
				return;
			}
			_ILGen_EnsureCapacity.Invoke(il, new object[1] { 7 });
			_ILGen_InternalEmit.Invoke(il, new object[1] { opcode });
			if (opcode.StackBehaviourPop == System.Reflection.Emit.StackBehaviour.Varpop)
			{
				_ILGen_UpdateStackSize.Invoke(il, new object[2]
				{
					opcode,
					-csite.Parameters.Count - 1
				});
			}
			_ILGen_PutInteger4.Invoke(il, new object[1] { tokenCreator.GetTokenForSig(signature) });
			void AddArgument(Type clsArgument, Type[] requiredCustomModifiers, Type[] optionalCustomModifiers)
			{
				if (optionalCustomModifiers != null)
				{
					Type[] array2 = optionalCustomModifiers;
					foreach (Type type3 in array2)
					{
						InternalAddTypeToken(tokenCreator.GetTokenForType(type3), 32);
					}
				}
				if (requiredCustomModifiers != null)
				{
					Type[] array2 = requiredCustomModifiers;
					foreach (Type type4 in array2)
					{
						InternalAddTypeToken(tokenCreator.GetTokenForType(type4), 31);
					}
				}
				AddOneArgTypeHelper(clsArgument);
			}
			void AddData(int data)
			{
				if (currSig + 4 > signature.Length)
				{
					signature = ExpandArray(signature);
				}
				if (data <= 127)
				{
					signature[currSig++] = (byte)(data & 0xFF);
				}
				else if (data <= 16383)
				{
					signature[currSig++] = (byte)((data >> 8) | 0x80);
					signature[currSig++] = (byte)(data & 0xFF);
				}
				else
				{
					if (data > 536870911)
					{
						throw new ArgumentException("Integer or token was too large to be encoded.");
					}
					signature[currSig++] = (byte)((data >> 24) | 0xC0);
					signature[currSig++] = (byte)((data >> 16) & 0xFF);
					signature[currSig++] = (byte)((data >> 8) & 0xFF);
					signature[currSig++] = (byte)(data & 0xFF);
				}
			}
			void AddElementType(byte cvt)
			{
				if (currSig + 1 > signature.Length)
				{
					signature = ExpandArray(signature);
				}
				signature[currSig++] = cvt;
			}
			void AddOneArgTypeHelper(Type clsArgument)
			{
				AddOneArgTypeHelperWorker(clsArgument, lastWasGenericInst: false);
			}
			void AddOneArgTypeHelperWorker(Type clsArgument, bool lastWasGenericInst)
			{
				if (clsArgument.IsGenericType && (!clsArgument.IsGenericTypeDefinition || !lastWasGenericInst))
				{
					AddElementType(21);
					AddOneArgTypeHelperWorker(clsArgument.GetGenericTypeDefinition(), lastWasGenericInst: true);
					Type[] genericArguments = clsArgument.GetGenericArguments();
					AddData(genericArguments.Length);
					Type[] array2 = genericArguments;
					for (int i = 0; i < array2.Length; i++)
					{
						AddOneArgTypeHelper(array2[i]);
					}
				}
				else if (clsArgument.IsByRef)
				{
					AddElementType(16);
					clsArgument = clsArgument.GetElementType() ?? clsArgument;
					AddOneArgTypeHelper(clsArgument);
				}
				else if (clsArgument.IsPointer)
				{
					AddElementType(15);
					AddOneArgTypeHelper(clsArgument.GetElementType() ?? clsArgument);
				}
				else if (clsArgument.IsArray)
				{
					AddElementType(20);
					AddOneArgTypeHelper(clsArgument.GetElementType() ?? clsArgument);
					int arrayRank = clsArgument.GetArrayRank();
					AddData(arrayRank);
					AddData(0);
					AddData(arrayRank);
					for (int j = 0; j < arrayRank; j++)
					{
						AddData(0);
					}
				}
				else
				{
					byte b = 0;
					for (int k = 0; k < CorElementTypes.Length; k++)
					{
						if (clsArgument == CorElementTypes[k])
						{
							b = (byte)k;
							break;
						}
					}
					if (b == 0)
					{
						b = (byte)((clsArgument == typeof(object)) ? 28 : ((!clsArgument.IsValueType) ? 18 : 17));
					}
					if (b <= 14 || b == 22 || b == 24 || b == 25 || b == 28)
					{
						AddElementType(b);
					}
					else
					{
						InternalAddRuntimeType(clsArgument);
					}
				}
			}
			void AddToken(int token)
			{
				int num4 = token & 0xFFFFFF;
				int num5 = token & -16777216;
				if (num4 > 67108863)
				{
					throw new ArgumentException("Integer or token was too large to be encoded.");
				}
				num4 <<= 2;
				switch (num5)
				{
				case 16777216:
					num4 |= 1;
					break;
				case 452984832:
					num4 |= 2;
					break;
				}
				AddData(num4);
			}
			static byte[] ExpandArray(byte[] inArray, int requiredLength = -1)
			{
				if (requiredLength < inArray.Length)
				{
					requiredLength = inArray.Length * 2;
				}
				byte[] array2 = new byte[requiredLength];
				Buffer.BlockCopy(inArray, 0, array2, 0, inArray.Length);
				return array2;
			}
			unsafe void InternalAddRuntimeType(Type type3)
			{
				AddElementType(33);
				IntPtr value = type3.TypeHandle.Value;
				if (currSig + sizeof(void*) > signature.Length)
				{
					signature = ExpandArray(signature);
				}
				byte* ptr = (byte*)(&value);
				for (int i = 0; i < sizeof(void*); i++)
				{
					signature[currSig++] = ptr[i];
				}
			}
			void InternalAddTypeToken(int clsToken, byte CorType)
			{
				AddElementType(CorType);
				AddToken(clsToken);
			}
		}
	}

	private sealed class MonoCallSiteEmitter : CallSiteEmitter
	{
		private FieldInfo SigHelper_callConv;

		private FieldInfo SigHelper_unmanagedCallConv;

		private FieldInfo SigHelper_arguments;

		private FieldInfo SigHelper_modreqs;

		private FieldInfo SigHelper_modopts;

		public MonoCallSiteEmitter()
		{
			FieldInfo field = typeof(SignatureHelper).GetField("callConv", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo field2 = typeof(SignatureHelper).GetField("unmanagedCallConv", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo field3 = typeof(SignatureHelper).GetField("arguments", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo field4 = typeof(SignatureHelper).GetField("modreqs", BindingFlags.Instance | BindingFlags.NonPublic);
			FieldInfo field5 = typeof(SignatureHelper).GetField("modopts", BindingFlags.Instance | BindingFlags.NonPublic);
			Helpers.Assert((object)field != null, null, "callConv is not null");
			Helpers.Assert((object)field2 != null, null, "unmanagedCallConv is not null");
			Helpers.Assert((object)field3 != null, null, "arguments is not null");
			Helpers.Assert((object)field4 != null, null, "modreqs is not null");
			Helpers.Assert((object)field5 != null, null, "modopts is not null");
			SigHelper_callConv = field;
			SigHelper_unmanagedCallConv = field2;
			SigHelper_arguments = field3;
			SigHelper_modreqs = field4;
			SigHelper_modopts = field5;
		}

		public override void EmitCallSite(DynamicMethod dm, ILGenerator il, System.Reflection.Emit.OpCode opcode, CallSite csite)
		{
			List<Type> modReq = new List<Type>();
			List<Type> modOpt = new List<Type>();
			ResolveWithModifiers(csite.ReturnType, out Type type, out Type[] _, out Type[] _, modReq, modOpt);
			SignatureHelper methodSigHelper = SignatureHelper.GetMethodSigHelper(CallingConventions.Standard, type);
			Type[] array = new Type[csite.Parameters.Count];
			Type[][] array2 = new Type[csite.Parameters.Count][];
			Type[][] array3 = new Type[csite.Parameters.Count][];
			CallingConventions callingConventions = ((csite.CallingConvention != MethodCallingConvention.VarArg) ? CallingConventions.Standard : CallingConventions.VarArgs);
			CallingConventions callingConventions2 = callingConventions;
			if (csite.HasThis)
			{
				callingConventions2 |= CallingConventions.HasThis;
			}
			if (csite.ExplicitThis)
			{
				callingConventions2 |= CallingConventions.ExplicitThis;
			}
			CallingConvention callingConvention = csite.CallingConvention switch
			{
				MethodCallingConvention.C => CallingConvention.Cdecl, 
				MethodCallingConvention.StdCall => CallingConvention.StdCall, 
				MethodCallingConvention.ThisCall => CallingConvention.ThisCall, 
				MethodCallingConvention.FastCall => CallingConvention.FastCall, 
				_ => (CallingConvention)0, 
			};
			for (int i = 0; i < csite.Parameters.Count; i++)
			{
				ResolveWithModifiers(csite.Parameters[i].ParameterType, out array[i], out array2[i], out array3[i], modReq, modOpt);
			}
			SigHelper_callConv.SetValue(methodSigHelper, callingConventions2);
			SigHelper_unmanagedCallConv.SetValue(methodSigHelper, callingConvention);
			SigHelper_arguments.SetValue(methodSigHelper, array);
			SigHelper_modreqs.SetValue(methodSigHelper, array2);
			SigHelper_modopts.SetValue(methodSigHelper, array3);
			_ILGen_make_room.Invoke(il, new object[1] { 6 });
			_ILGen_ll_emit.Invoke(il, new object[1] { opcode });
			_ILGen_emit_int.Invoke(il, new object[1] { DynamicMethod_AddRef(dm, methodSigHelper) });
		}
	}

	private static readonly MethodInfo m_MethodBase_InvokeSimple;

	private static readonly Dictionary<short, System.Reflection.Emit.OpCode> _ReflOpCodes;

	private static readonly Dictionary<short, Mono.Cecil.Cil.OpCode> _CecilOpCodes;

	private static readonly MethodInfo? _ILGen_make_room;

	private static readonly MethodInfo? _ILGen_emit_int;

	private static readonly MethodInfo? _ILGen_ll_emit;

	private static readonly MethodInfo? mDynamicMethod_AddRef;

	private static readonly Func<DynamicMethod, object?, int>? DynamicMethod_AddRef;

	private static readonly Type? TRuntimeILGenerator;

	private static readonly MethodInfo? _ILGen_EnsureCapacity;

	private static readonly MethodInfo? _ILGen_PutInteger4;

	private static readonly MethodInfo? _ILGen_InternalEmit;

	private static readonly MethodInfo? _ILGen_UpdateStackSize;

	private static readonly FieldInfo? f_DynILGen_m_scope;

	private static readonly FieldInfo? f_DynScope_m_tokens;

	private static readonly Type?[] CorElementTypes;

	private static readonly CallSiteEmitter callSiteEmitter;

	private static MethodBuilder _CreateMethodProxy(MethodBuilder context, MethodInfo target)
	{
		TypeBuilder obj = (TypeBuilder)context.DeclaringType;
		string name = $".dmdproxy<{target.Name.Replace('.', '_')}>?{target.GetHashCode()}";
		Type[] array = (from param in target.GetParameters()
			select param.ParameterType).ToArray();
		MethodBuilder methodBuilder = obj.DefineMethod(name, System.Reflection.MethodAttributes.Private | System.Reflection.MethodAttributes.Static | System.Reflection.MethodAttributes.HideBySig, CallingConventions.Standard, target.ReturnType, array);
		ILGenerator iLGenerator = methodBuilder.GetILGenerator();
		iLGenerator.EmitNewTypedReference(target, out var _);
		iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldnull);
		iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldc_I4, array.Length);
		iLGenerator.Emit(System.Reflection.Emit.OpCodes.Newarr, typeof(object));
		for (int num = 0; num < array.Length; num++)
		{
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Dup);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldc_I4, num);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg, num);
			Type type = array[num];
			if (type.IsByRef)
			{
				type = type.GetElementType() ?? type;
			}
			if (type.IsValueType)
			{
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Box, type);
			}
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Stelem_Ref);
		}
		iLGenerator.Emit(System.Reflection.Emit.OpCodes.Callvirt, m_MethodBase_InvokeSimple);
		if (target.ReturnType == typeof(void))
		{
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Pop);
		}
		else if (target.ReturnType.IsValueType)
		{
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, target.ReturnType);
		}
		iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
		return methodBuilder;
	}

	static _DMDEmit()
	{
		m_MethodBase_InvokeSimple = typeof(MethodBase).GetMethod("Invoke", BindingFlags.Instance | BindingFlags.Public, null, new Type[2]
		{
			typeof(object),
			typeof(object[])
		}, null);
		_ReflOpCodes = new Dictionary<short, System.Reflection.Emit.OpCode>();
		_CecilOpCodes = new Dictionary<short, Mono.Cecil.Cil.OpCode>();
		_ILGen_make_room = typeof(ILGenerator).GetMethod("make_room", BindingFlags.Instance | BindingFlags.NonPublic);
		_ILGen_emit_int = typeof(ILGenerator).GetMethod("emit_int", BindingFlags.Instance | BindingFlags.NonPublic);
		_ILGen_ll_emit = typeof(ILGenerator).GetMethod("ll_emit", BindingFlags.Instance | BindingFlags.NonPublic);
		mDynamicMethod_AddRef = typeof(DynamicMethod).GetMethod("AddRef", BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[1] { typeof(object) }, null);
		DynamicMethod_AddRef = mDynamicMethod_AddRef?.CreateDelegate<Func<DynamicMethod, object, int>>();
		TRuntimeILGenerator = Type.GetType("System.Reflection.Emit.RuntimeILGenerator");
		_ILGen_EnsureCapacity = typeof(ILGenerator).GetMethod("EnsureCapacity", BindingFlags.Instance | BindingFlags.NonPublic) ?? TRuntimeILGenerator?.GetMethod("EnsureCapacity", BindingFlags.Instance | BindingFlags.NonPublic);
		_ILGen_PutInteger4 = typeof(ILGenerator).GetMethod("PutInteger4", BindingFlags.Instance | BindingFlags.NonPublic) ?? TRuntimeILGenerator?.GetMethod("PutInteger4", BindingFlags.Instance | BindingFlags.NonPublic);
		_ILGen_InternalEmit = typeof(ILGenerator).GetMethod("InternalEmit", BindingFlags.Instance | BindingFlags.NonPublic) ?? TRuntimeILGenerator?.GetMethod("InternalEmit", BindingFlags.Instance | BindingFlags.NonPublic);
		_ILGen_UpdateStackSize = typeof(ILGenerator).GetMethod("UpdateStackSize", BindingFlags.Instance | BindingFlags.NonPublic) ?? TRuntimeILGenerator?.GetMethod("UpdateStackSize", BindingFlags.Instance | BindingFlags.NonPublic);
		f_DynILGen_m_scope = typeof(ILGenerator).Assembly.GetType("System.Reflection.Emit.DynamicILGenerator")?.GetField("m_scope", BindingFlags.Instance | BindingFlags.NonPublic);
		f_DynScope_m_tokens = typeof(ILGenerator).Assembly.GetType("System.Reflection.Emit.DynamicScope")?.GetField("m_tokens", BindingFlags.Instance | BindingFlags.NonPublic);
		CorElementTypes = new Type[29]
		{
			null,
			typeof(void),
			typeof(bool),
			typeof(char),
			typeof(sbyte),
			typeof(byte),
			typeof(short),
			typeof(ushort),
			typeof(int),
			typeof(uint),
			typeof(long),
			typeof(ulong),
			typeof(float),
			typeof(double),
			typeof(string),
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			null,
			typeof(IntPtr),
			typeof(UIntPtr),
			null,
			null,
			typeof(object)
		};
		callSiteEmitter = ((DynamicMethod_AddRef != null) ? ((CallSiteEmitter)new MonoCallSiteEmitter()) : ((CallSiteEmitter)new NetCallSiteEmitter()));
		FieldInfo[] fields = typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public);
		for (int i = 0; i < fields.Length; i++)
		{
			System.Reflection.Emit.OpCode value = (System.Reflection.Emit.OpCode)fields[i].GetValue(null);
			_ReflOpCodes[value.Value] = value;
		}
		fields = typeof(Mono.Cecil.Cil.OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public);
		for (int i = 0; i < fields.Length; i++)
		{
			Mono.Cecil.Cil.OpCode value2 = (Mono.Cecil.Cil.OpCode)fields[i].GetValue(null);
			_CecilOpCodes[value2.Value] = value2;
		}
	}

	public static void Generate(DynamicMethodDefinition dmd, MethodBase _mb, ILGenerator il)
	{
		MethodDefinition methodDefinition = dmd.Definition ?? throw new InvalidOperationException();
		DynamicMethod dynamicMethod = _mb as DynamicMethod;
		MethodBuilder mb = _mb as MethodBuilder;
		ModuleBuilder moduleBuilder = mb?.Module as ModuleBuilder;
		AssemblyBuilder assemblyBuilder = (mb?.DeclaringType as TypeBuilder)?.Assembly as AssemblyBuilder;
		HashSet<Assembly> hashSet = null;
		if (mb != null)
		{
			hashSet = new HashSet<Assembly>();
		}
		MethodDebugInformation defInfo = (dmd.Debug ? methodDefinition.DebugInformation : null);
		if (dynamicMethod != null)
		{
			foreach (ParameterDefinition parameter in methodDefinition.Parameters)
			{
				dynamicMethod.DefineParameter(parameter.Index + 1, (System.Reflection.ParameterAttributes)parameter.Attributes, parameter.Name);
			}
		}
		if (mb != null)
		{
			foreach (ParameterDefinition parameter2 in methodDefinition.Parameters)
			{
				mb.DefineParameter(parameter2.Index + 1, (System.Reflection.ParameterAttributes)parameter2.Attributes, parameter2.Name);
			}
		}
		LocalBuilder[] array = methodDefinition.Body.Variables.Select(delegate(VariableDefinition var)
		{
			LocalBuilder localBuilder = il.DeclareLocal(var.VariableType.ResolveReflection(), var.IsPinned);
			if (mb != null && defInfo != null && defInfo.TryGetName(var, out var name))
			{
				localBuilder.SetLocalSymInfo(name);
			}
			return localBuilder;
		}).ToArray();
		Dictionary<Instruction, Label> labelMap = new Dictionary<Instruction, Label>();
		foreach (Instruction instruction in methodDefinition.Body.Instructions)
		{
			if (instruction.Operand is Instruction[] array2)
			{
				Instruction[] array3 = array2;
				foreach (Instruction key in array3)
				{
					if (!labelMap.ContainsKey(key))
					{
						labelMap[key] = il.DefineLabel();
					}
				}
			}
			else if (instruction.Operand is Instruction key2 && !labelMap.ContainsKey(key2))
			{
				labelMap[key2] = il.DefineLabel();
			}
		}
		Dictionary<Document, ISymbolDocumentWriter> dictionary = ((mb == null) ? null : new Dictionary<Document, ISymbolDocumentWriter>());
		int num2 = (methodDefinition.HasThis ? 1 : 0);
		_ = new object[2];
		bool flag = false;
		foreach (Instruction instruction2 in methodDefinition.Body.Instructions)
		{
			if (labelMap.TryGetValue(instruction2, out var value))
			{
				il.MarkLabel(value);
			}
			SequencePoint sequencePoint = defInfo?.GetSequencePoint(instruction2);
			if ((object)mb != null && sequencePoint != null && dictionary != null && (object)moduleBuilder != null)
			{
				if (!dictionary.TryGetValue(sequencePoint.Document, out var value2))
				{
					value2 = (dictionary[sequencePoint.Document] = moduleBuilder.DefineDocument(sequencePoint.Document.Url, sequencePoint.Document.LanguageGuid, sequencePoint.Document.LanguageVendorGuid, sequencePoint.Document.TypeGuid));
				}
				il.MarkSequencePoint(value2, sequencePoint.StartLine, sequencePoint.StartColumn, sequencePoint.EndLine, sequencePoint.EndColumn);
			}
			foreach (Mono.Cecil.Cil.ExceptionHandler exceptionHandler in methodDefinition.Body.ExceptionHandlers)
			{
				if (flag && exceptionHandler.HandlerEnd == instruction2)
				{
					il.EndExceptionBlock();
				}
				if (exceptionHandler.TryStart == instruction2)
				{
					il.BeginExceptionBlock();
				}
				else if (exceptionHandler.FilterStart == instruction2)
				{
					il.BeginExceptFilterBlock();
				}
				else if (exceptionHandler.HandlerStart == instruction2)
				{
					switch (exceptionHandler.HandlerType)
					{
					case ExceptionHandlerType.Filter:
						il.BeginCatchBlock(null);
						break;
					case ExceptionHandlerType.Catch:
						il.BeginCatchBlock(exceptionHandler.CatchType.ResolveReflection());
						break;
					case ExceptionHandlerType.Finally:
						il.BeginFinallyBlock();
						break;
					case ExceptionHandlerType.Fault:
						il.BeginFaultBlock();
						break;
					}
				}
				if (exceptionHandler.HandlerStart != instruction2.Next)
				{
					continue;
				}
				switch (exceptionHandler.HandlerType)
				{
				case ExceptionHandlerType.Filter:
					if (!(instruction2.OpCode == Mono.Cecil.Cil.OpCodes.Endfilter))
					{
						break;
					}
					goto IL_08a3;
				case ExceptionHandlerType.Finally:
					if (!(instruction2.OpCode == Mono.Cecil.Cil.OpCodes.Endfinally))
					{
						break;
					}
					goto IL_08a3;
				}
			}
			if (instruction2.OpCode.OperandType == Mono.Cecil.Cil.OperandType.InlineNone)
			{
				il.Emit(_ReflOpCodes[instruction2.OpCode.Value]);
			}
			else
			{
				Mono.Cecil.Cil.OpCode opCode = instruction2.OpCode;
				object obj = instruction2.Operand;
				if (obj is Instruction[] source)
				{
					obj = source.Select((Instruction target) => labelMap[target]).ToArray();
					opCode = opCode.ToLongOp();
				}
				else if (obj is Instruction key3)
				{
					obj = labelMap[key3];
					opCode = opCode.ToLongOp();
				}
				else if (obj is VariableDefinition variableDefinition)
				{
					obj = array[variableDefinition.Index];
				}
				else if (obj is ParameterDefinition parameterDefinition)
				{
					obj = parameterDefinition.Index + num2;
				}
				else if (obj is MemberReference memberReference)
				{
					MemberInfo memberInfo = ((memberReference == methodDefinition) ? _mb : memberReference.ResolveReflection());
					obj = memberInfo;
					if (mb != null && memberInfo != null)
					{
						Module module = memberInfo.Module;
						if (module == null)
						{
							continue;
						}
						Assembly assembly = module.Assembly;
						if (assembly != null && hashSet != null && (object)assemblyBuilder != null && !hashSet.Contains(assembly))
						{
							assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(DynamicMethodDefinition.c_IgnoresAccessChecksToAttribute, new object[1] { assembly.GetName().Name }));
							hashSet.Add(assembly);
						}
					}
				}
				else if (obj is CallSite csite)
				{
					if (dynamicMethod != null)
					{
						_EmitCallSite(dynamicMethod, il, _ReflOpCodes[opCode.Value], csite);
						continue;
					}
					if ((object)mb == null)
					{
						throw new NotSupportedException();
					}
					obj = csite.ResolveReflection(mb.Module);
				}
				if (mb != null && obj is MethodBase methodBase && methodBase.DeclaringType == null)
				{
					if (!(opCode == Mono.Cecil.Cil.OpCodes.Call))
					{
						throw new NotSupportedException("Unsupported global method operand on opcode " + opCode.Name);
					}
					if (methodBase is MethodInfo methodInfo && methodInfo.IsDynamicMethod())
					{
						obj = _CreateMethodProxy(mb, methodInfo);
					}
					else
					{
						IntPtr ldftnPointer = methodBase.GetLdftnPointer();
						if (IntPtr.Size == 4)
						{
							il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4, (int)ldftnPointer);
						}
						else
						{
							il.Emit(System.Reflection.Emit.OpCodes.Ldc_I8, (long)ldftnPointer);
						}
						il.Emit(System.Reflection.Emit.OpCodes.Conv_I);
						opCode = Mono.Cecil.Cil.OpCodes.Calli;
						obj = ((MethodReference)instruction2.Operand).ResolveReflectionSignature(mb.Module);
					}
				}
				if (obj == null)
				{
					throw new InvalidOperationException($"Unexpected null in {methodDefinition} @ {instruction2}");
				}
				il.DynEmit(_ReflOpCodes[opCode.Value], obj);
			}
			if (!flag)
			{
				foreach (Mono.Cecil.Cil.ExceptionHandler exceptionHandler2 in methodDefinition.Body.ExceptionHandlers)
				{
					if (exceptionHandler2.HandlerEnd == instruction2.Next)
					{
						il.EndExceptionBlock();
					}
				}
			}
			flag = false;
			continue;
			IL_08a3:
			flag = true;
		}
	}

	public static void ResolveWithModifiers(TypeReference typeRef, out Type type, out Type[] typeModReq, out Type[] typeModOpt, List<Type>? modReq = null, List<Type>? modOpt = null)
	{
		if (modReq == null)
		{
			modReq = new List<Type>();
		}
		else
		{
			modReq.Clear();
		}
		if (modOpt == null)
		{
			modOpt = new List<Type>();
		}
		else
		{
			modOpt.Clear();
		}
		for (TypeReference typeReference = typeRef; typeReference is TypeSpecification typeSpecification; typeReference = typeSpecification.ElementType)
		{
			if (!(typeReference is RequiredModifierType requiredModifierType))
			{
				if (typeReference is OptionalModifierType optionalModifierType)
				{
					modOpt.Add(optionalModifierType.ModifierType.ResolveReflection());
				}
			}
			else
			{
				modReq.Add(requiredModifierType.ModifierType.ResolveReflection());
			}
		}
		type = typeRef.ResolveReflection();
		typeModReq = modReq.ToArray();
		typeModOpt = modOpt.ToArray();
	}

	internal static void _EmitCallSite(DynamicMethod dm, ILGenerator il, System.Reflection.Emit.OpCode opcode, CallSite csite)
	{
		callSiteEmitter.EmitCallSite(dm, il, opcode, csite);
	}
}
