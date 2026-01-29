using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Mdb;
using Mono.Cecil.Pdb;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;
using Mono.CompilerServices.SymbolWriter;
using MonoMod;
using MonoMod.Core;
using MonoMod.Core.Platforms;
using MonoMod.Utils;
using MonoMod.Utils.Cil;

[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
[assembly: Debuggable(DebuggableAttribute.DebuggingModes.IgnoreSymbolStoreSequencePoints)]
[assembly: TargetFramework(".NETFramework,Version=v4.7.2", FrameworkDisplayName = ".NET Framework 4.7.2")]
[assembly: AssemblyCompany("Andreas Pardeike")]
[assembly: AssemblyConfiguration("Release")]
[assembly: AssemblyCopyright("Copyright © 2016")]
[assembly: AssemblyDescription("A general non-destructive patch library for .NET and Mono modules.")]
[assembly: AssemblyFileVersion("2.4.2.0")]
[assembly: AssemblyInformationalVersion("2.4.2.0+a264a1bf1ce689e4589e8dcc54b1e2818602a90a")]
[assembly: AssemblyProduct("Harmony")]
[assembly: AssemblyTitle("0Harmony")]
[assembly: AssemblyMetadata("RepositoryUrl", "https://github.com/pardeike/Harmony")]
[assembly: InternalsVisibleTo("HarmonyTests")]
[assembly: InternalsVisibleTo("MonoMod.Utils.Cil.ILGeneratorProxy")]
[assembly: SecurityPermission(System.Security.Permissions.SecurityAction.RequestMinimum, SkipVerification = true)]
[assembly: AssemblyVersion("2.4.2.0")]
[assembly: TypeForwardedTo(typeof(AssemblyAttributes))]
[assembly: TypeForwardedTo(typeof(AssemblyDefinition))]
[assembly: TypeForwardedTo(typeof(AssemblyHashAlgorithm))]
[assembly: TypeForwardedTo(typeof(AssemblyNameDefinition))]
[assembly: TypeForwardedTo(typeof(AssemblyNameReference))]
[assembly: TypeForwardedTo(typeof(AssemblyResolutionException))]
[assembly: TypeForwardedTo(typeof(ByReferenceType))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.CallSite))]
[assembly: TypeForwardedTo(typeof(Code))]
[assembly: TypeForwardedTo(typeof(ConstantDebugInformation))]
[assembly: TypeForwardedTo(typeof(CustomDebugInformation))]
[assembly: TypeForwardedTo(typeof(CustomDebugInformationKind))]
[assembly: TypeForwardedTo(typeof(DebugInformation))]
[assembly: TypeForwardedTo(typeof(Document))]
[assembly: TypeForwardedTo(typeof(DocumentHashAlgorithm))]
[assembly: TypeForwardedTo(typeof(DocumentLanguage))]
[assembly: TypeForwardedTo(typeof(DocumentLanguageVendor))]
[assembly: TypeForwardedTo(typeof(DocumentType))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Cil.ExceptionHandler))]
[assembly: TypeForwardedTo(typeof(ExceptionHandlerType))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Cil.FlowControl))]
[assembly: TypeForwardedTo(typeof(ILProcessor))]
[assembly: TypeForwardedTo(typeof(ImageDebugDirectory))]
[assembly: TypeForwardedTo(typeof(ImageDebugHeader))]
[assembly: TypeForwardedTo(typeof(ImageDebugHeaderEntry))]
[assembly: TypeForwardedTo(typeof(ImageDebugType))]
[assembly: TypeForwardedTo(typeof(ImportDebugInformation))]
[assembly: TypeForwardedTo(typeof(ImportTarget))]
[assembly: TypeForwardedTo(typeof(ImportTargetKind))]
[assembly: TypeForwardedTo(typeof(Instruction))]
[assembly: TypeForwardedTo(typeof(InstructionOffset))]
[assembly: TypeForwardedTo(typeof(ISymbolReader))]
[assembly: TypeForwardedTo(typeof(ISymbolReaderProvider))]
[assembly: TypeForwardedTo(typeof(ISymbolWriter))]
[assembly: TypeForwardedTo(typeof(ISymbolWriterProvider))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Cil.MethodBody))]
[assembly: TypeForwardedTo(typeof(MethodDebugInformation))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Cil.OpCode))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Cil.OpCodes))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Cil.OpCodeType))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Cil.OperandType))]
[assembly: TypeForwardedTo(typeof(ScopeDebugInformation))]
[assembly: TypeForwardedTo(typeof(SequencePoint))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.Cil.StackBehaviour))]
[assembly: TypeForwardedTo(typeof(SymbolsNotFoundException))]
[assembly: TypeForwardedTo(typeof(SymbolsNotMatchingException))]
[assembly: TypeForwardedTo(typeof(VariableAttributes))]
[assembly: TypeForwardedTo(typeof(VariableDebugInformation))]
[assembly: TypeForwardedTo(typeof(VariableDefinition))]
[assembly: TypeForwardedTo(typeof(VariableReference))]
[assembly: TypeForwardedTo(typeof(CustomAttribute))]
[assembly: TypeForwardedTo(typeof(CustomAttributeArgument))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.CustomAttributeNamedArgument))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.EventAttributes))]
[assembly: TypeForwardedTo(typeof(EventDefinition))]
[assembly: TypeForwardedTo(typeof(EventReference))]
[assembly: TypeForwardedTo(typeof(ExportedType))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.FieldAttributes))]
[assembly: TypeForwardedTo(typeof(FieldDefinition))]
[assembly: TypeForwardedTo(typeof(FieldReference))]
[assembly: TypeForwardedTo(typeof(GenericInstanceMethod))]
[assembly: TypeForwardedTo(typeof(GenericInstanceType))]
[assembly: TypeForwardedTo(typeof(GenericParameter))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.GenericParameterAttributes))]
[assembly: TypeForwardedTo(typeof(GenericParameterConstraint))]
[assembly: TypeForwardedTo(typeof(GenericParameterType))]
[assembly: TypeForwardedTo(typeof(IAssemblyResolver))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.ICustomAttributeProvider))]
[assembly: TypeForwardedTo(typeof(IGenericParameterProvider))]
[assembly: TypeForwardedTo(typeof(IMemberDefinition))]
[assembly: TypeForwardedTo(typeof(IMetadataImporter))]
[assembly: TypeForwardedTo(typeof(IMetadataImporterProvider))]
[assembly: TypeForwardedTo(typeof(IMetadataResolver))]
[assembly: TypeForwardedTo(typeof(IMetadataScope))]
[assembly: TypeForwardedTo(typeof(IMetadataTokenProvider))]
[assembly: TypeForwardedTo(typeof(IMethodSignature))]
[assembly: TypeForwardedTo(typeof(InterfaceImplementation))]
[assembly: TypeForwardedTo(typeof(IReflectionImporter))]
[assembly: TypeForwardedTo(typeof(IReflectionImporterProvider))]
[assembly: TypeForwardedTo(typeof(ManifestResourceAttributes))]
[assembly: TypeForwardedTo(typeof(MarshalInfo))]
[assembly: TypeForwardedTo(typeof(MdbReader))]
[assembly: TypeForwardedTo(typeof(MemberReference))]
[assembly: TypeForwardedTo(typeof(MetadataKind))]
[assembly: TypeForwardedTo(typeof(MetadataScopeType))]
[assembly: TypeForwardedTo(typeof(MetadataToken))]
[assembly: TypeForwardedTo(typeof(MetadataType))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.MethodAttributes))]
[assembly: TypeForwardedTo(typeof(MethodCallingConvention))]
[assembly: TypeForwardedTo(typeof(MethodDefinition))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.MethodImplAttributes))]
[assembly: TypeForwardedTo(typeof(MethodReference))]
[assembly: TypeForwardedTo(typeof(MethodReturnType))]
[assembly: TypeForwardedTo(typeof(MethodSemanticsAttributes))]
[assembly: TypeForwardedTo(typeof(ModuleAttributes))]
[assembly: TypeForwardedTo(typeof(ModuleCharacteristics))]
[assembly: TypeForwardedTo(typeof(ModuleDefinition))]
[assembly: TypeForwardedTo(typeof(ModuleKind))]
[assembly: TypeForwardedTo(typeof(ModuleParameters))]
[assembly: TypeForwardedTo(typeof(ModuleReference))]
[assembly: TypeForwardedTo(typeof(NativeType))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.ParameterAttributes))]
[assembly: TypeForwardedTo(typeof(ParameterDefinition))]
[assembly: TypeForwardedTo(typeof(ParameterReference))]
[assembly: TypeForwardedTo(typeof(NativePdbReader))]
[assembly: TypeForwardedTo(typeof(NativePdbWriter))]
[assembly: TypeForwardedTo(typeof(PInvokeAttributes))]
[assembly: TypeForwardedTo(typeof(PInvokeInfo))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.PropertyAttributes))]
[assembly: TypeForwardedTo(typeof(PropertyDefinition))]
[assembly: TypeForwardedTo(typeof(PropertyReference))]
[assembly: TypeForwardedTo(typeof(ReaderParameters))]
[assembly: TypeForwardedTo(typeof(ReadingMode))]
[assembly: TypeForwardedTo(typeof(ResolutionException))]
[assembly: TypeForwardedTo(typeof(Resource))]
[assembly: TypeForwardedTo(typeof(ResourceType))]
[assembly: TypeForwardedTo(typeof(IILVisitor))]
[assembly: TypeForwardedTo(typeof(ILParser))]
[assembly: TypeForwardedTo(typeof(ModuleDefinitionRocks))]
[assembly: TypeForwardedTo(typeof(TypeDefinitionRocks))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.SecurityAction))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.SecurityAttribute))]
[assembly: TypeForwardedTo(typeof(SecurityDeclaration))]
[assembly: TypeForwardedTo(typeof(TargetArchitecture))]
[assembly: TypeForwardedTo(typeof(TargetRuntime))]
[assembly: TypeForwardedTo(typeof(TokenType))]
[assembly: TypeForwardedTo(typeof(Mono.Cecil.TypeAttributes))]
[assembly: TypeForwardedTo(typeof(TypeDefinition))]
[assembly: TypeForwardedTo(typeof(TypeReference))]
[assembly: TypeForwardedTo(typeof(TypeSpecification))]
[assembly: TypeForwardedTo(typeof(TypeSystem))]
[assembly: TypeForwardedTo(typeof(WriterParameters))]
[assembly: TypeForwardedTo(typeof(Mono.Collections.Generic.Collection<>))]
[assembly: TypeForwardedTo(typeof(AnonymousScopeEntry))]
[assembly: TypeForwardedTo(typeof(CapturedScope))]
[assembly: TypeForwardedTo(typeof(CapturedVariable))]
[assembly: TypeForwardedTo(typeof(CodeBlockEntry))]
[assembly: TypeForwardedTo(typeof(CompileUnitEntry))]
[assembly: TypeForwardedTo(typeof(LineNumberEntry))]
[assembly: TypeForwardedTo(typeof(LineNumberTable))]
[assembly: TypeForwardedTo(typeof(LocalVariableEntry))]
[assembly: TypeForwardedTo(typeof(MethodEntry))]
[assembly: TypeForwardedTo(typeof(MonoSymbolFile))]
[assembly: TypeForwardedTo(typeof(NamespaceEntry))]
[assembly: TypeForwardedTo(typeof(OffsetTable))]
[assembly: TypeForwardedTo(typeof(ScopeVariable))]
[assembly: TypeForwardedTo(typeof(SourceFileEntry))]
[assembly: TypeForwardedTo(typeof(CecilILGenerator))]
[assembly: TypeForwardedTo(typeof(ILGeneratorShim))]
[assembly: TypeForwardedTo(typeof(DMDEmitDynamicMethodGenerator))]
[assembly: TypeForwardedTo(typeof(DMDGenerator<>))]
[assembly: TypeForwardedTo(typeof(DynamicMethodDefinition))]
[assembly: TypeForwardedTo(typeof(Extensions))]
[assembly: TypeForwardedTo(typeof(ICallSiteGenerator))]
[assembly: TypeForwardedTo(typeof(ReflectionHelper))]
[assembly: TypeForwardedTo(typeof(Relinker))]
[module: UnverifiableCode]
[module: System.Runtime.CompilerServices.RefSafetyRules(11)]
namespace Microsoft.CodeAnalysis
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	internal sealed class EmbeddedAttribute : Attribute
	{
	}
}
namespace System.Runtime.CompilerServices
{
	[CompilerGenerated]
	[Microsoft.CodeAnalysis.Embedded]
	[AttributeUsage(AttributeTargets.Module, AllowMultiple = false, Inherited = false)]
	internal sealed class RefSafetyRulesAttribute : Attribute
	{
		public readonly int Version;

		public RefSafetyRulesAttribute(int P_0)
		{
			Version = P_0;
		}
	}
}
namespace HarmonyLib
{
	public class DelegateTypeFactory
	{
		private readonly ModuleBuilder module;

		private static int counter;

		public DelegateTypeFactory()
		{
			counter++;
			string name = $"HarmonyDTFAssembly{counter}";
			AssemblyBuilder assemblyBuilder = PatchTools.DefineDynamicAssembly(name);
			module = assemblyBuilder.DefineDynamicModule($"HarmonyDTFModule{counter}");
		}

		public Type CreateDelegateType(MethodInfo method)
		{
			System.Reflection.TypeAttributes attr = System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Sealed;
			TypeBuilder typeBuilder = module.DefineType($"HarmonyDTFType{counter}", attr, typeof(MulticastDelegate));
			ConstructorBuilder constructorBuilder = typeBuilder.DefineConstructor(System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.HideBySig | System.Reflection.MethodAttributes.RTSpecialName, CallingConventions.Standard, new Type[2]
			{
				typeof(object),
				typeof(IntPtr)
			});
			constructorBuilder.SetImplementationFlags(System.Reflection.MethodImplAttributes.CodeTypeMask);
			ParameterInfo[] parameters = method.GetParameters();
			MethodBuilder methodBuilder = typeBuilder.DefineMethod("Invoke", System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Virtual | System.Reflection.MethodAttributes.HideBySig, method.ReturnType, parameters.Types());
			methodBuilder.SetImplementationFlags(System.Reflection.MethodImplAttributes.CodeTypeMask);
			for (int i = 0; i < parameters.Length; i++)
			{
				methodBuilder.DefineParameter(i + 1, System.Reflection.ParameterAttributes.None, parameters[i].Name);
			}
			return typeBuilder.CreateType();
		}
	}
	[Obsolete("Use AccessTools.FieldRefAccess<T, S> for fields and AccessTools.MethodDelegate<Func<T, S>> for property getters")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public delegate S GetterHandler<in T, out S>(T source);
	[Obsolete("Use AccessTools.FieldRefAccess<T, S> for fields and AccessTools.MethodDelegate<Action<T, S>> for property setters")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	public delegate void SetterHandler<in T, in S>(T source, S value);
	public delegate T InstantiationHandler<out T>();
	public static class FastAccess
	{
		public static InstantiationHandler<T> CreateInstantiationHandler<T>()
		{
			ConstructorInfo constructor = typeof(T).GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Array.Empty<Type>(), null);
			if ((object)constructor == null)
			{
				throw new ApplicationException($"The type {typeof(T)} must declare an empty constructor (the constructor may be private, internal, protected, protected internal, or public).");
			}
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("InstantiateObject_" + typeof(T).Name, typeof(T), null);
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Newobj, constructor);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
			return dynamicMethodDefinition.Generate().CreateDelegate<InstantiationHandler<T>>();
		}

		[Obsolete("Use AccessTools.MethodDelegate<Func<T, S>>(PropertyInfo.GetGetMethod(true))")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static GetterHandler<T, S> CreateGetterHandler<T, S>(PropertyInfo propertyInfo)
		{
			MethodInfo getMethod = propertyInfo.GetGetMethod(nonPublic: true);
			DynamicMethodDefinition dynamicMethodDefinition = CreateGetDynamicMethod<T, S>(propertyInfo.DeclaringType);
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Call, getMethod);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
			return dynamicMethodDefinition.Generate().CreateDelegate<GetterHandler<T, S>>();
		}

		[Obsolete("Use AccessTools.FieldRefAccess<T, S>(fieldInfo)")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static GetterHandler<T, S> CreateGetterHandler<T, S>(FieldInfo fieldInfo)
		{
			DynamicMethodDefinition dynamicMethodDefinition = CreateGetDynamicMethod<T, S>(fieldInfo.DeclaringType);
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldfld, fieldInfo);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
			return dynamicMethodDefinition.Generate().CreateDelegate<GetterHandler<T, S>>();
		}

		[Obsolete("Use AccessTools.FieldRefAccess<T, S>(name) for fields and AccessTools.MethodDelegate<Func<T, S>>(AccessTools.PropertyGetter(typeof(T), name)) for properties")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static GetterHandler<T, S> CreateFieldGetter<T, S>(params string[] names)
		{
			foreach (string name in names)
			{
				FieldInfo field = typeof(T).GetField(name, AccessTools.all);
				if ((object)field != null)
				{
					return CreateGetterHandler<T, S>(field);
				}
				PropertyInfo property = typeof(T).GetProperty(name, AccessTools.all);
				if ((object)property != null)
				{
					return CreateGetterHandler<T, S>(property);
				}
			}
			return null;
		}

		[Obsolete("Use AccessTools.MethodDelegate<Action<T, S>>(PropertyInfo.GetSetMethod(true))")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static SetterHandler<T, S> CreateSetterHandler<T, S>(PropertyInfo propertyInfo)
		{
			MethodInfo setMethod = propertyInfo.GetSetMethod(nonPublic: true);
			DynamicMethodDefinition dynamicMethodDefinition = CreateSetDynamicMethod<T, S>(propertyInfo.DeclaringType);
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Call, setMethod);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
			return dynamicMethodDefinition.Generate().CreateDelegate<SetterHandler<T, S>>();
		}

		[Obsolete("Use AccessTools.FieldRefAccess<T, S>(fieldInfo)")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static SetterHandler<T, S> CreateSetterHandler<T, S>(FieldInfo fieldInfo)
		{
			DynamicMethodDefinition dynamicMethodDefinition = CreateSetDynamicMethod<T, S>(fieldInfo.DeclaringType);
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg_1);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Stfld, fieldInfo);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
			return dynamicMethodDefinition.Generate().CreateDelegate<SetterHandler<T, S>>();
		}

		private static DynamicMethodDefinition CreateGetDynamicMethod<T, S>(Type type)
		{
			return new DynamicMethodDefinition("DynamicGet_" + type.Name, typeof(S), new Type[1] { typeof(T) });
		}

		private static DynamicMethodDefinition CreateSetDynamicMethod<T, S>(Type type)
		{
			return new DynamicMethodDefinition("DynamicSet_" + type.Name, typeof(void), new Type[2]
			{
				typeof(T),
				typeof(S)
			});
		}
	}
	public delegate object FastInvokeHandler(object target, params object[] parameters);
	public static class MethodInvoker
	{
		public static FastInvokeHandler GetHandler(MethodInfo methodInfo, bool directBoxValueAccess = false)
		{
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("FastInvoke_" + methodInfo.Name + "_" + (directBoxValueAccess ? "direct" : "indirect"), typeof(object), new Type[2]
			{
				typeof(object),
				typeof(object[])
			});
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			if (!methodInfo.IsStatic)
			{
				Emit(iLGenerator, System.Reflection.Emit.OpCodes.Ldarg_0);
				EmitUnboxIfNeeded(iLGenerator, methodInfo.DeclaringType);
			}
			bool flag = true;
			ParameterInfo[] parameters = methodInfo.GetParameters();
			for (int i = 0; i < parameters.Length; i++)
			{
				Type type = parameters[i].ParameterType;
				bool isByRef = type.IsByRef;
				if (isByRef)
				{
					type = type.GetElementType();
				}
				bool isValueType = type.IsValueType;
				if (isByRef && isValueType && !directBoxValueAccess)
				{
					Emit(iLGenerator, System.Reflection.Emit.OpCodes.Ldarg_1);
					EmitFastInt(iLGenerator, i);
				}
				Emit(iLGenerator, System.Reflection.Emit.OpCodes.Ldarg_1);
				EmitFastInt(iLGenerator, i);
				if (isByRef && !isValueType)
				{
					Emit(iLGenerator, System.Reflection.Emit.OpCodes.Ldelema, typeof(object));
					continue;
				}
				Emit(iLGenerator, System.Reflection.Emit.OpCodes.Ldelem_Ref);
				if (!isValueType)
				{
					continue;
				}
				if (!isByRef || !directBoxValueAccess)
				{
					Emit(iLGenerator, System.Reflection.Emit.OpCodes.Unbox_Any, type);
					if (isByRef)
					{
						Emit(iLGenerator, System.Reflection.Emit.OpCodes.Box, type);
						Emit(iLGenerator, System.Reflection.Emit.OpCodes.Dup);
						if (flag)
						{
							flag = false;
							iLGenerator.DeclareLocal(typeof(object), pinned: false);
						}
						Emit(iLGenerator, System.Reflection.Emit.OpCodes.Stloc_0);
						Emit(iLGenerator, System.Reflection.Emit.OpCodes.Stelem_Ref);
						Emit(iLGenerator, System.Reflection.Emit.OpCodes.Ldloc_0);
						Emit(iLGenerator, System.Reflection.Emit.OpCodes.Unbox, type);
					}
				}
				else
				{
					Emit(iLGenerator, System.Reflection.Emit.OpCodes.Unbox, type);
				}
			}
			if (methodInfo.IsStatic)
			{
				EmitCall(iLGenerator, System.Reflection.Emit.OpCodes.Call, methodInfo);
			}
			else
			{
				EmitCall(iLGenerator, System.Reflection.Emit.OpCodes.Callvirt, methodInfo);
			}
			if (methodInfo.ReturnType == typeof(void))
			{
				Emit(iLGenerator, System.Reflection.Emit.OpCodes.Ldnull);
			}
			else
			{
				EmitBoxIfNeeded(iLGenerator, methodInfo.ReturnType);
			}
			Emit(iLGenerator, System.Reflection.Emit.OpCodes.Ret);
			return dynamicMethodDefinition.Generate().CreateDelegate<FastInvokeHandler>();
		}

		internal static void Emit(ILGenerator il, System.Reflection.Emit.OpCode opcode)
		{
			il.Emit(opcode);
		}

		internal static void Emit(ILGenerator il, System.Reflection.Emit.OpCode opcode, Type type)
		{
			il.Emit(opcode, type);
		}

		internal static void EmitCall(ILGenerator il, System.Reflection.Emit.OpCode opcode, MethodInfo methodInfo)
		{
			il.EmitCall(opcode, methodInfo, null);
		}

		private static void EmitUnboxIfNeeded(ILGenerator il, Type type)
		{
			if (type.IsValueType)
			{
				Emit(il, System.Reflection.Emit.OpCodes.Unbox_Any, type);
			}
		}

		private static void EmitBoxIfNeeded(ILGenerator il, Type type)
		{
			if (type.IsValueType)
			{
				Emit(il, System.Reflection.Emit.OpCodes.Box, type);
			}
		}

		internal static void EmitFastInt(ILGenerator il, int value)
		{
			switch (value)
			{
			case -1:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_M1);
				return;
			case 0:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0);
				return;
			case 1:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1);
				return;
			case 2:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_2);
				return;
			case 3:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_3);
				return;
			case 4:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_4);
				return;
			case 5:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_5);
				return;
			case 6:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_6);
				return;
			case 7:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_7);
				return;
			case 8:
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_8);
				return;
			}
			if (value > -129 && value < 128)
			{
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4_S, (sbyte)value);
			}
			else
			{
				il.Emit(System.Reflection.Emit.OpCodes.Ldc_I4, value);
			}
		}
	}
	public delegate ref T RefResult<T>();
	internal class AccessCache
	{
		internal enum MemberType
		{
			Any,
			Static,
			Instance
		}

		private const BindingFlags BasicFlags = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty;

		private static readonly Dictionary<MemberType, BindingFlags> declaredOnlyBindingFlags = new Dictionary<MemberType, BindingFlags>
		{
			{
				MemberType.Any,
				BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty
			},
			{
				MemberType.Instance,
				BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty
			},
			{
				MemberType.Static,
				BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty
			}
		};

		private readonly Dictionary<Type, Dictionary<string, FieldInfo>> declaredFields = new Dictionary<Type, Dictionary<string, FieldInfo>>();

		private readonly Dictionary<Type, Dictionary<string, PropertyInfo>> declaredProperties = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

		private readonly Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>> declaredMethods = new Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>>();

		private readonly Dictionary<Type, Dictionary<string, FieldInfo>> inheritedFields = new Dictionary<Type, Dictionary<string, FieldInfo>>();

		private readonly Dictionary<Type, Dictionary<string, PropertyInfo>> inheritedProperties = new Dictionary<Type, Dictionary<string, PropertyInfo>>();

		private readonly Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>> inheritedMethods = new Dictionary<Type, Dictionary<string, Dictionary<int, MethodBase>>>();

		private static T Get<T>(Dictionary<Type, Dictionary<string, T>> dict, Type type, string name, Func<T> fetcher)
		{
			lock (dict)
			{
				if (!dict.TryGetValue(type, out var value))
				{
					value = (dict[type] = new Dictionary<string, T>());
				}
				if (!value.TryGetValue(name, out var value2))
				{
					value2 = (value[name] = fetcher());
				}
				return value2;
			}
		}

		private static T Get<T>(Dictionary<Type, Dictionary<string, Dictionary<int, T>>> dict, Type type, string name, Type[] arguments, Func<T> fetcher)
		{
			lock (dict)
			{
				if (!dict.TryGetValue(type, out var value))
				{
					value = (dict[type] = new Dictionary<string, Dictionary<int, T>>());
				}
				if (!value.TryGetValue(name, out var value2))
				{
					value2 = (value[name] = new Dictionary<int, T>());
				}
				int key = AccessTools.CombinedHashCode(arguments);
				if (!value2.TryGetValue(key, out var value3))
				{
					value3 = (value2[key] = fetcher());
				}
				return value3;
			}
		}

		internal FieldInfo GetFieldInfo(Type type, string name, MemberType memberType = MemberType.Any, bool declaredOnly = false)
		{
			FieldInfo fieldInfo = Get(declaredFields, type, name, () => type.GetField(name, declaredOnlyBindingFlags[memberType]));
			if ((object)fieldInfo == null && !declaredOnly)
			{
				fieldInfo = Get(inheritedFields, type, name, () => AccessTools.FindIncludingBaseTypes(type, (Type t) => t.GetField(name, AccessTools.all)));
			}
			return fieldInfo;
		}

		internal PropertyInfo GetPropertyInfo(Type type, string name, MemberType memberType = MemberType.Any, bool declaredOnly = false)
		{
			PropertyInfo propertyInfo = Get(declaredProperties, type, name, () => type.GetProperty(name, declaredOnlyBindingFlags[memberType]));
			if ((object)propertyInfo == null && !declaredOnly)
			{
				propertyInfo = Get(inheritedProperties, type, name, () => AccessTools.FindIncludingBaseTypes(type, (Type t) => t.GetProperty(name, AccessTools.all)));
			}
			return propertyInfo;
		}

		internal MethodBase GetMethodInfo(Type type, string name, Type[] arguments, MemberType memberType = MemberType.Any, bool declaredOnly = false)
		{
			MethodBase methodBase = Get(declaredMethods, type, name, arguments, () => type.GetMethod(name, declaredOnlyBindingFlags[memberType], null, arguments, null));
			if ((object)methodBase == null && !declaredOnly)
			{
				methodBase = Get(inheritedMethods, type, name, arguments, () => AccessTools.Method(type, name, arguments));
			}
			return methodBase;
		}
	}
	internal class ByteBuffer
	{
		internal byte[] buffer;

		internal int position;

		internal ByteBuffer(byte[] buffer)
		{
			this.buffer = buffer;
		}

		internal byte ReadByte()
		{
			CheckCanRead(1);
			return buffer[position++];
		}

		internal byte[] ReadBytes(int length)
		{
			CheckCanRead(length);
			byte[] array = new byte[length];
			Buffer.BlockCopy(buffer, position, array, 0, length);
			position += length;
			return array;
		}

		internal short ReadInt16()
		{
			CheckCanRead(2);
			short result = (short)(buffer[position] | (buffer[position + 1] << 8));
			position += 2;
			return result;
		}

		internal int ReadInt32()
		{
			CheckCanRead(4);
			int result = buffer[position] | (buffer[position + 1] << 8) | (buffer[position + 2] << 16) | (buffer[position + 3] << 24);
			position += 4;
			return result;
		}

		internal long ReadInt64()
		{
			CheckCanRead(8);
			uint num = (uint)(buffer[position] | (buffer[position + 1] << 8) | (buffer[position + 2] << 16) | (buffer[position + 3] << 24));
			uint num2 = (uint)(buffer[position + 4] | (buffer[position + 5] << 8) | (buffer[position + 6] << 16) | (buffer[position + 7] << 24));
			long result = (long)(((ulong)num2 << 32) | num);
			position += 8;
			return result;
		}

		internal float ReadSingle()
		{
			if (!BitConverter.IsLittleEndian)
			{
				byte[] array = ReadBytes(4);
				Array.Reverse(array);
				return BitConverter.ToSingle(array, 0);
			}
			CheckCanRead(4);
			float result = BitConverter.ToSingle(buffer, position);
			position += 4;
			return result;
		}

		internal double ReadDouble()
		{
			if (!BitConverter.IsLittleEndian)
			{
				byte[] array = ReadBytes(8);
				Array.Reverse(array);
				return BitConverter.ToDouble(array, 0);
			}
			CheckCanRead(8);
			double result = BitConverter.ToDouble(buffer, position);
			position += 8;
			return result;
		}

		private void CheckCanRead(int count)
		{
			if (position + count > buffer.Length)
			{
				throw new ArgumentOutOfRangeException("count", $"position({position}) + count({count}) > buffer.Length({buffer.Length})");
			}
		}
	}
	internal class CodeTranspiler
	{
		private readonly IEnumerable<CodeInstruction> codeInstructions;

		private readonly List<MethodInfo> transpilers = new List<MethodInfo>();

		private static readonly Dictionary<System.Reflection.Emit.OpCode, System.Reflection.Emit.OpCode> allJumpCodes = new Dictionary<System.Reflection.Emit.OpCode, System.Reflection.Emit.OpCode>
		{
			{
				System.Reflection.Emit.OpCodes.Beq_S,
				System.Reflection.Emit.OpCodes.Beq
			},
			{
				System.Reflection.Emit.OpCodes.Bge_S,
				System.Reflection.Emit.OpCodes.Bge
			},
			{
				System.Reflection.Emit.OpCodes.Bge_Un_S,
				System.Reflection.Emit.OpCodes.Bge_Un
			},
			{
				System.Reflection.Emit.OpCodes.Bgt_S,
				System.Reflection.Emit.OpCodes.Bgt
			},
			{
				System.Reflection.Emit.OpCodes.Bgt_Un_S,
				System.Reflection.Emit.OpCodes.Bgt_Un
			},
			{
				System.Reflection.Emit.OpCodes.Ble_S,
				System.Reflection.Emit.OpCodes.Ble
			},
			{
				System.Reflection.Emit.OpCodes.Ble_Un_S,
				System.Reflection.Emit.OpCodes.Ble_Un
			},
			{
				System.Reflection.Emit.OpCodes.Blt_S,
				System.Reflection.Emit.OpCodes.Blt
			},
			{
				System.Reflection.Emit.OpCodes.Blt_Un_S,
				System.Reflection.Emit.OpCodes.Blt_Un
			},
			{
				System.Reflection.Emit.OpCodes.Bne_Un_S,
				System.Reflection.Emit.OpCodes.Bne_Un
			},
			{
				System.Reflection.Emit.OpCodes.Brfalse_S,
				System.Reflection.Emit.OpCodes.Brfalse
			},
			{
				System.Reflection.Emit.OpCodes.Brtrue_S,
				System.Reflection.Emit.OpCodes.Brtrue
			},
			{
				System.Reflection.Emit.OpCodes.Br_S,
				System.Reflection.Emit.OpCodes.Br
			},
			{
				System.Reflection.Emit.OpCodes.Leave_S,
				System.Reflection.Emit.OpCodes.Leave
			}
		};

		internal CodeTranspiler(List<ILInstruction> ilInstructions)
		{
			codeInstructions = ilInstructions.Select((ILInstruction ilInstruction) => ilInstruction.GetCodeInstruction()).ToList().AsEnumerable();
		}

		internal void Add(MethodInfo transpiler)
		{
			transpilers.Add(transpiler);
		}

		internal static object ConvertInstruction(Type type, object instruction, out Dictionary<string, object> unassigned)
		{
			Dictionary<string, object> nonExisting = new Dictionary<string, object>();
			object result = AccessTools.MakeDeepCopy(instruction, type, delegate(string namePath, Traverse trvSrc, Traverse trvDest)
			{
				object value = trvSrc.GetValue();
				if (!trvDest.FieldExists())
				{
					nonExisting[namePath] = value;
					return (object)null;
				}
				return (namePath == "opcode") ? ((object)ReplaceShortJumps((System.Reflection.Emit.OpCode)value)) : value;
			});
			unassigned = nonExisting;
			return result;
		}

		internal static bool ShouldAddExceptionInfo(object op, int opIndex, List<object> originalInstructions, List<object> newInstructions, Dictionary<object, Dictionary<string, object>> unassignedValues)
		{
			int num = originalInstructions.IndexOf(op);
			if (num == -1)
			{
				return false;
			}
			if (!unassignedValues.TryGetValue(op, out var unassigned))
			{
				return false;
			}
			if (!unassigned.TryGetValue("blocks", out var blocksObject))
			{
				return false;
			}
			List<ExceptionBlock> blocks = blocksObject as List<ExceptionBlock>;
			int num2 = newInstructions.Count((object instr) => instr == op);
			if (num2 <= 1)
			{
				return true;
			}
			ExceptionBlock exceptionBlock = blocks.FirstOrDefault((ExceptionBlock block) => block.blockType != ExceptionBlockType.EndExceptionBlock);
			ExceptionBlock exceptionBlock2 = blocks.FirstOrDefault((ExceptionBlock block) => block.blockType == ExceptionBlockType.EndExceptionBlock);
			if (exceptionBlock != null && exceptionBlock2 == null)
			{
				object obj = originalInstructions.Skip(num + 1).FirstOrDefault(delegate(object instr)
				{
					if (!unassignedValues.TryGetValue(instr, out unassigned))
					{
						return false;
					}
					if (!unassigned.TryGetValue("blocks", out blocksObject))
					{
						return false;
					}
					blocks = blocksObject as List<ExceptionBlock>;
					return blocks.Count > 0;
				});
				if (obj != null)
				{
					int num3 = num + 1;
					int num4 = num3 + originalInstructions.Skip(num3).ToList().IndexOf(obj) - 1;
					IEnumerable<object> first = originalInstructions.GetRange(num3, num4 - num3).Intersect(newInstructions);
					obj = newInstructions.Skip(opIndex + 1).FirstOrDefault(delegate(object instr)
					{
						if (!unassignedValues.TryGetValue(instr, out unassigned))
						{
							return false;
						}
						if (!unassigned.TryGetValue("blocks", out blocksObject))
						{
							return false;
						}
						blocks = blocksObject as List<ExceptionBlock>;
						return blocks.Count > 0;
					});
					if (obj != null)
					{
						num3 = opIndex + 1;
						num4 = num3 + newInstructions.Skip(opIndex + 1).ToList().IndexOf(obj) - 1;
						List<object> range = newInstructions.GetRange(num3, num4 - num3);
						List<object> list = first.Except(range).ToList();
						return list.Count == 0;
					}
				}
			}
			if (exceptionBlock == null && exceptionBlock2 != null)
			{
				object obj2 = originalInstructions.GetRange(0, num).LastOrDefault(delegate(object instr)
				{
					if (!unassignedValues.TryGetValue(instr, out unassigned))
					{
						return false;
					}
					if (!unassigned.TryGetValue("blocks", out blocksObject))
					{
						return false;
					}
					blocks = blocksObject as List<ExceptionBlock>;
					return blocks.Count > 0;
				});
				if (obj2 != null)
				{
					int num5 = originalInstructions.GetRange(0, num).LastIndexOf(obj2);
					int num6 = num;
					IEnumerable<object> first2 = originalInstructions.GetRange(num5, num6 - num5).Intersect(newInstructions);
					obj2 = newInstructions.GetRange(0, opIndex).LastOrDefault(delegate(object instr)
					{
						if (!unassignedValues.TryGetValue(instr, out unassigned))
						{
							return false;
						}
						if (!unassigned.TryGetValue("blocks", out blocksObject))
						{
							return false;
						}
						blocks = blocksObject as List<ExceptionBlock>;
						return blocks.Count > 0;
					});
					if (obj2 != null)
					{
						num5 = newInstructions.GetRange(0, opIndex).LastIndexOf(obj2);
						num6 = opIndex;
						List<object> range2 = newInstructions.GetRange(num5, num6 - num5);
						IEnumerable<object> source = first2.Except(range2);
						return !source.Any();
					}
				}
			}
			return true;
		}

		internal static IEnumerable ConvertInstructionsAndUnassignedValues(Type type, IEnumerable enumerable, out Dictionary<object, Dictionary<string, object>> unassignedValues)
		{
			Assembly assembly = type.GetGenericTypeDefinition().Assembly;
			Type type2 = assembly.GetType(typeof(List<>).FullName);
			Type type3 = type.GetGenericArguments()[0];
			Type type4 = type2.MakeGenericType(type3);
			Type type5 = assembly.GetType(type4.FullName);
			object obj = Activator.CreateInstance(type5);
			MethodInfo method = obj.GetType().GetMethod("Add");
			unassignedValues = new Dictionary<object, Dictionary<string, object>>();
			foreach (object item in enumerable)
			{
				Dictionary<string, object> unassigned;
				object obj2 = ConvertInstruction(type3, item, out unassigned);
				unassignedValues.Add(obj2, unassigned);
				method.Invoke(obj, new object[1] { obj2 });
			}
			return obj as IEnumerable;
		}

		internal static IEnumerable ConvertToOurInstructions(IEnumerable instructions, Type codeInstructionType, List<object> originalInstructions, Dictionary<object, Dictionary<string, object>> unassignedValues)
		{
			List<object> newInstructions = instructions.Cast<object>().ToList();
			int index = -1;
			foreach (object item in newInstructions)
			{
				index++;
				object obj = AccessTools.MakeDeepCopy(item, codeInstructionType);
				if (unassignedValues.TryGetValue(item, out var value))
				{
					bool flag = ShouldAddExceptionInfo(item, index, originalInstructions, newInstructions, unassignedValues);
					Traverse traverse = Traverse.Create(obj);
					foreach (KeyValuePair<string, object> item2 in value)
					{
						if (flag || item2.Key != "blocks")
						{
							traverse.Field(item2.Key).SetValue(item2.Value);
						}
					}
				}
				yield return obj;
			}
		}

		private static bool IsCodeInstructionsParameter(Type type)
		{
			if (type.IsGenericType)
			{
				return type.GetGenericTypeDefinition().Name.StartsWith("IEnumerable", StringComparison.Ordinal);
			}
			return false;
		}

		internal static IEnumerable ConvertToGeneralInstructions(MethodInfo transpiler, IEnumerable enumerable, out Dictionary<object, Dictionary<string, object>> unassignedValues)
		{
			Type type = (from p in transpiler.GetParameters()
				select p.ParameterType).FirstOrDefault(IsCodeInstructionsParameter);
			if (type == typeof(IEnumerable<CodeInstruction>))
			{
				unassignedValues = null;
				object obj = enumerable as IList<CodeInstruction>;
				if (obj == null)
				{
					List<CodeInstruction> list = new List<CodeInstruction>();
					list.AddRange((enumerable as IEnumerable<CodeInstruction>) ?? enumerable.Cast<CodeInstruction>());
					obj = list;
				}
				return (IEnumerable)obj;
			}
			return ConvertInstructionsAndUnassignedValues(type, enumerable, out unassignedValues);
		}

		internal static List<object> GetTranspilerCallParameters(ILGenerator generator, MethodInfo transpiler, MethodBase method, IEnumerable instructions)
		{
			List<object> parameter = new List<object>();
			(from param in transpiler.GetParameters()
				select param.ParameterType).Do(delegate(Type type)
			{
				if (type.IsAssignableFrom(typeof(ILGenerator)))
				{
					parameter.Add(generator);
				}
				else if (type.IsAssignableFrom(typeof(MethodBase)))
				{
					parameter.Add(method);
				}
				else if (IsCodeInstructionsParameter(type))
				{
					parameter.Add(instructions);
				}
			});
			return parameter;
		}

		internal List<CodeInstruction> GetResult(ILGenerator generator, MethodBase method)
		{
			IEnumerable instructions = codeInstructions;
			transpilers.ForEach(delegate(MethodInfo transpiler)
			{
				instructions = ConvertToGeneralInstructions(transpiler, instructions, out var unassignedValues);
				List<object> originalInstructions = null;
				if (unassignedValues != null)
				{
					originalInstructions = instructions.Cast<object>().ToList();
				}
				List<object> transpilerCallParameters = GetTranspilerCallParameters(generator, transpiler, method, instructions);
				if (transpiler.Invoke(null, transpilerCallParameters.ToArray()) is IEnumerable enumerable)
				{
					instructions = enumerable;
				}
				if (unassignedValues != null)
				{
					instructions = ConvertToOurInstructions(instructions, typeof(CodeInstruction), originalInstructions, unassignedValues);
				}
			});
			return (instructions as List<CodeInstruction>) ?? instructions.Cast<CodeInstruction>().ToList();
		}

		private static System.Reflection.Emit.OpCode ReplaceShortJumps(System.Reflection.Emit.OpCode opcode)
		{
			foreach (KeyValuePair<System.Reflection.Emit.OpCode, System.Reflection.Emit.OpCode> allJumpCode in allJumpCodes)
			{
				if (opcode == allJumpCode.Key)
				{
					return allJumpCode.Value;
				}
			}
			return opcode;
		}
	}
	internal class LeaveTry
	{
		public override string ToString()
		{
			return "(autogenerated)";
		}
	}
	internal class Emitter
	{
		private readonly ILGenerator iLGenerator;

		private readonly CecilILGenerator il;

		private readonly Dictionary<int, CodeInstruction> instructions = new Dictionary<int, CodeInstruction>();

		internal Emitter(ILGenerator il)
		{
			iLGenerator = il;
			this.il = il.GetProxiedShim<CecilILGenerator>();
		}

		internal Dictionary<int, CodeInstruction> GetInstructions()
		{
			return instructions;
		}

		internal void AddInstruction(System.Reflection.Emit.OpCode opcode, object operand = null)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, operand));
		}

		internal int CurrentPos()
		{
			return il.ILOffset;
		}

		internal static string CodePos(int offset)
		{
			return $"IL_{offset:X4}: ";
		}

		internal string CodePos()
		{
			return CodePos(CurrentPos());
		}

		internal IEnumerable<VariableDefinition> Variables()
		{
			return il.IL.Body.Variables;
		}

		internal static string FormatOperand(object argument)
		{
			if (argument == null)
			{
				return "NULL";
			}
			Type type = argument.GetType();
			if (argument is MethodBase member)
			{
				return member.FullDescription();
			}
			if (argument is FieldInfo fieldInfo)
			{
				return $"{fieldInfo.FieldType.FullDescription()} {fieldInfo.DeclaringType.FullDescription()}::{fieldInfo.Name}";
			}
			if (type == typeof(Label))
			{
				return $"Label{((Label)argument/*cast due to .constrained prefix*/).GetHashCode()}";
			}
			if (type == typeof(Label[]))
			{
				return "Labels" + string.Join(",", ((Label[])argument).Select((Label l) => l.GetHashCode().ToString()).ToArray());
			}
			if (type == typeof(LocalBuilder))
			{
				return $"{((LocalBuilder)argument).LocalIndex} ({((LocalBuilder)argument).LocalType})";
			}
			if (type == typeof(string))
			{
				return argument.ToString().ToLiteral();
			}
			return argument.ToString().Trim();
		}

		internal LocalBuilder DeclareLocalVariable(Type type, bool isReturnValue = false)
		{
			if (type.IsByRef)
			{
				if (isReturnValue)
				{
					LocalBuilder localBuilder = il.DeclareLocal(type);
					Emit(System.Reflection.Emit.OpCodes.Ldc_I4_1);
					Emit(System.Reflection.Emit.OpCodes.Newarr, type.GetElementType());
					Emit(System.Reflection.Emit.OpCodes.Ldc_I4_0);
					Emit(System.Reflection.Emit.OpCodes.Ldelema, type.GetElementType());
					Emit(System.Reflection.Emit.OpCodes.Stloc, localBuilder);
					return localBuilder;
				}
				type = type.GetElementType();
			}
			if (type.IsEnum)
			{
				type = Enum.GetUnderlyingType(type);
			}
			if (AccessTools.IsClass(type))
			{
				LocalBuilder localBuilder2 = il.DeclareLocal(type);
				Emit(System.Reflection.Emit.OpCodes.Ldnull);
				Emit(System.Reflection.Emit.OpCodes.Stloc, localBuilder2);
				return localBuilder2;
			}
			if (AccessTools.IsStruct(type))
			{
				LocalBuilder localBuilder3 = il.DeclareLocal(type);
				Emit(System.Reflection.Emit.OpCodes.Ldloca, localBuilder3);
				Emit(System.Reflection.Emit.OpCodes.Initobj, type);
				return localBuilder3;
			}
			if (AccessTools.IsValue(type))
			{
				LocalBuilder localBuilder4 = il.DeclareLocal(type);
				if (type == typeof(float))
				{
					Emit(System.Reflection.Emit.OpCodes.Ldc_R4, 0f);
				}
				else if (type == typeof(double))
				{
					Emit(System.Reflection.Emit.OpCodes.Ldc_R8, 0.0);
				}
				else if (type == typeof(long) || type == typeof(ulong))
				{
					Emit(System.Reflection.Emit.OpCodes.Ldc_I8, 0L);
				}
				else
				{
					Emit(System.Reflection.Emit.OpCodes.Ldc_I4, 0);
				}
				Emit(System.Reflection.Emit.OpCodes.Stloc, localBuilder4);
				return localBuilder4;
			}
			return null;
		}

		internal void InitializeOutParameter(int argIndex, Type type)
		{
			if (type.IsByRef)
			{
				type = type.GetElementType();
			}
			Emit(System.Reflection.Emit.OpCodes.Ldarg, argIndex);
			if (AccessTools.IsStruct(type))
			{
				Emit(System.Reflection.Emit.OpCodes.Initobj, type);
			}
			else if (AccessTools.IsValue(type))
			{
				if (type == typeof(float))
				{
					Emit(System.Reflection.Emit.OpCodes.Ldc_R4, 0f);
					Emit(System.Reflection.Emit.OpCodes.Stind_R4);
				}
				else if (type == typeof(double))
				{
					Emit(System.Reflection.Emit.OpCodes.Ldc_R8, 0.0);
					Emit(System.Reflection.Emit.OpCodes.Stind_R8);
				}
				else if (type == typeof(long))
				{
					Emit(System.Reflection.Emit.OpCodes.Ldc_I8, 0L);
					Emit(System.Reflection.Emit.OpCodes.Stind_I8);
				}
				else
				{
					Emit(System.Reflection.Emit.OpCodes.Ldc_I4, 0);
					Emit(System.Reflection.Emit.OpCodes.Stind_I4);
				}
			}
			else
			{
				Emit(System.Reflection.Emit.OpCodes.Ldnull);
				Emit(System.Reflection.Emit.OpCodes.Stind_Ref);
			}
		}

		internal void PrepareArgumentArray(MethodBase original)
		{
			ParameterInfo[] parameters = original.GetParameters();
			int num = 0;
			ParameterInfo[] array = parameters;
			foreach (ParameterInfo parameterInfo in array)
			{
				int argIndex = num++ + ((!original.IsStatic) ? 1 : 0);
				if (parameterInfo.IsOut || parameterInfo.IsRetval)
				{
					InitializeOutParameter(argIndex, parameterInfo.ParameterType);
				}
			}
			Emit(System.Reflection.Emit.OpCodes.Ldc_I4, parameters.Length);
			Emit(System.Reflection.Emit.OpCodes.Newarr, typeof(object));
			num = 0;
			int num2 = 0;
			ParameterInfo[] array2 = parameters;
			foreach (ParameterInfo parameterInfo2 in array2)
			{
				int arg = num++ + ((!original.IsStatic) ? 1 : 0);
				Type type = parameterInfo2.ParameterType;
				bool isByRef = type.IsByRef;
				if (isByRef)
				{
					type = type.GetElementType();
				}
				Emit(System.Reflection.Emit.OpCodes.Dup);
				Emit(System.Reflection.Emit.OpCodes.Ldc_I4, num2++);
				Emit(System.Reflection.Emit.OpCodes.Ldarg, arg);
				if (isByRef)
				{
					if (AccessTools.IsStruct(type))
					{
						Emit(System.Reflection.Emit.OpCodes.Ldobj, type);
					}
					else
					{
						Emit(MethodPatcherTools.LoadIndOpCodeFor(type));
					}
				}
				if (type.IsValueType)
				{
					Emit(System.Reflection.Emit.OpCodes.Box, type);
				}
				Emit(System.Reflection.Emit.OpCodes.Stelem_Ref);
			}
		}

		internal void RestoreArgumentArray(MethodBase original, LocalBuilderState localState)
		{
			ParameterInfo[] parameters = original.GetParameters();
			int num = 0;
			int num2 = 0;
			ParameterInfo[] array = parameters;
			foreach (ParameterInfo parameterInfo in array)
			{
				int arg = num++ + ((!original.IsStatic) ? 1 : 0);
				Type parameterType = parameterInfo.ParameterType;
				if (parameterType.IsByRef)
				{
					parameterType = parameterType.GetElementType();
					Emit(System.Reflection.Emit.OpCodes.Ldarg, arg);
					Emit(System.Reflection.Emit.OpCodes.Ldloc, localState["__args"]);
					Emit(System.Reflection.Emit.OpCodes.Ldc_I4, num2);
					Emit(System.Reflection.Emit.OpCodes.Ldelem_Ref);
					if (parameterType.IsValueType)
					{
						Emit(System.Reflection.Emit.OpCodes.Unbox_Any, parameterType);
						if (AccessTools.IsStruct(parameterType))
						{
							Emit(System.Reflection.Emit.OpCodes.Stobj, parameterType);
						}
						else
						{
							Emit(MethodPatcherTools.StoreIndOpCodeFor(parameterType));
						}
					}
					else
					{
						Emit(System.Reflection.Emit.OpCodes.Castclass, parameterType);
						Emit(System.Reflection.Emit.OpCodes.Stind_Ref);
					}
				}
				else
				{
					Emit(System.Reflection.Emit.OpCodes.Ldloc, localState["__args"]);
					Emit(System.Reflection.Emit.OpCodes.Ldc_I4, num2);
					Emit(System.Reflection.Emit.OpCodes.Ldelem_Ref);
					if (parameterType.IsValueType)
					{
						Emit(System.Reflection.Emit.OpCodes.Unbox_Any, parameterType);
					}
					else
					{
						Emit(System.Reflection.Emit.OpCodes.Castclass, parameterType);
					}
					Emit(System.Reflection.Emit.OpCodes.Starg, arg);
				}
				num2++;
			}
		}

		internal void MarkLabel(Label label)
		{
			il.MarkLabel(label);
		}

		internal void MarkBlockBefore(ExceptionBlock block, out Label? label)
		{
			label = null;
			switch (block.blockType)
			{
			case ExceptionBlockType.BeginExceptionBlock:
				label = il.BeginExceptionBlock();
				break;
			case ExceptionBlockType.BeginCatchBlock:
				il.BeginCatchBlock(block.catchType);
				break;
			case ExceptionBlockType.BeginExceptFilterBlock:
				il.BeginExceptFilterBlock();
				break;
			case ExceptionBlockType.BeginFaultBlock:
				il.BeginFaultBlock();
				break;
			case ExceptionBlockType.BeginFinallyBlock:
				il.BeginFinallyBlock();
				break;
			}
		}

		internal void MarkBlockAfter(ExceptionBlock block)
		{
			ExceptionBlockType blockType = block.blockType;
			if (blockType == ExceptionBlockType.EndExceptionBlock)
			{
				il.EndExceptionBlock();
			}
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode));
			il.Emit(opcode);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, LocalBuilder local)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, local));
			il.Emit(opcode, local);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, FieldInfo field)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, field));
			il.Emit(opcode, field);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, Label[] labels)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, labels));
			il.Emit(opcode, labels);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, Label label)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, label));
			il.Emit(opcode, label);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, string str)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, str));
			il.Emit(opcode, str);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, float arg)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, arg));
			il.Emit(opcode, arg);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, byte arg)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, arg));
			il.Emit(opcode, arg);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, sbyte arg)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, arg));
			il.Emit(opcode, arg);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, double arg)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, arg));
			il.Emit(opcode, arg);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, int arg)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, arg));
			il.Emit(opcode, arg);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, MethodInfo meth)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, meth));
			il.Emit(opcode, meth);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, short arg)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, arg));
			il.Emit(opcode, arg);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, SignatureHelper signature)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, signature));
			il.Emit(opcode, signature);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, ConstructorInfo con)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, con));
			il.Emit(opcode, con);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, Type cls)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, cls));
			il.Emit(opcode, cls);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, long arg)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, arg));
			il.Emit(opcode, arg);
		}

		internal void Emit(System.Reflection.Emit.OpCode opcode, ICallSiteGenerator operand)
		{
			il.Emit(opcode, operand);
		}

		internal void EmitCall(System.Reflection.Emit.OpCode opcode, MethodInfo methodInfo)
		{
			instructions.Add(CurrentPos(), new CodeInstruction(opcode, methodInfo));
			il.EmitCall(opcode, methodInfo, null);
		}

		internal void DynEmit(System.Reflection.Emit.OpCode opcode, object operand)
		{
			iLGenerator.DynEmit(opcode, operand);
		}
	}
	internal class FaultBlockRewriter
	{
		private static int FindMatchingBeginException(List<CodeInstruction> rewritten)
		{
			int num = rewritten.Count - 1;
			int num2 = 0;
			while (num >= 0)
			{
				if (rewritten[num].HasBlock(ExceptionBlockType.EndExceptionBlock))
				{
					num2++;
				}
				if (rewritten[num].HasBlock(ExceptionBlockType.BeginExceptionBlock))
				{
					if (num2 == 0)
					{
						return num;
					}
					num2--;
				}
				num--;
			}
			return -1;
		}

		private static int FindMatchingEndException(List<CodeInstruction> source, int start)
		{
			int i = start;
			int num = 0;
			for (; i < source.Count; i++)
			{
				if (source[i].HasBlock(ExceptionBlockType.BeginExceptionBlock))
				{
					num++;
				}
				if (source[i].HasBlock(ExceptionBlockType.EndExceptionBlock))
				{
					if (num == 0)
					{
						return i;
					}
					num--;
				}
			}
			return -1;
		}

		private static CodeInstruction CloneWithoutFaultMarker(CodeInstruction original)
		{
			CodeInstruction codeInstruction = new CodeInstruction(original);
			codeInstruction.blocks.RemoveAll((ExceptionBlock b) => b.blockType == ExceptionBlockType.BeginFaultBlock);
			return codeInstruction;
		}

		internal static List<CodeInstruction> Rewrite(List<CodeInstruction> instructions, ILGenerator generator)
		{
			if (instructions == null)
			{
				throw new ArgumentNullException("instructions");
			}
			if (generator == null)
			{
				throw new ArgumentNullException("generator");
			}
			int num = 0;
			List<CodeInstruction> list = new List<CodeInstruction>(instructions.Count * 2);
			while (num < instructions.Count)
			{
				CodeInstruction codeInstruction = instructions[num];
				if (!codeInstruction.HasBlock(ExceptionBlockType.BeginFaultBlock))
				{
					list.Add(new CodeInstruction(codeInstruction));
					num++;
					continue;
				}
				int num2 = FindMatchingBeginException(list);
				int num3 = FindMatchingEndException(instructions, num + 1);
				if (num2 < 0 || num3 < 0)
				{
					throw new InvalidOperationException("Unbalanced exception markers – cannot rewrite.");
				}
				List<CodeInstruction> list2 = new List<CodeInstruction>();
				for (int i = num; i < num3; i++)
				{
					list2.Add(CloneWithoutFaultMarker(instructions[i]));
				}
				num = num3 + 1;
				LocalBuilder localBuilder = generator.DeclareLocal(typeof(bool));
				Label label = generator.DefineLabel();
				list.AddRange(new <>z__ReadOnlyArray<CodeInstruction>(new CodeInstruction[10]
				{
					Code.Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, typeof(object))),
					Code.Pop,
					Code.Ldc_I4_1,
					Code.Stloc[localBuilder.LocalIndex, null],
					Code.Rethrow,
					Code.Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock)),
					Code.Ldloc[localBuilder.LocalIndex, null],
					Code.Brfalse_S[label, null],
					Code.Nop.WithLabels(label),
					Code.Nop.WithBlocks(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock))
				}));
			}
			return list;
		}
	}
	internal static class HarmonySharedState
	{
		private const string name = "HarmonySharedState";

		internal const int internalVersion = 102;

		private static readonly Dictionary<MethodBase, byte[]> state;

		private static readonly Dictionary<MethodInfo, MethodBase> originals;

		private static readonly Dictionary<long, MethodBase[]> originalsMono;

		private static readonly AccessTools.FieldRef<StackFrame, long> methodAddressRef;

		internal static readonly int actualVersion;

		static HarmonySharedState()
		{
			Type orCreateSharedStateType = GetOrCreateSharedStateType();
			if (AccessTools.IsMonoRuntime)
			{
				FieldInfo fieldInfo = AccessTools.Field(typeof(StackFrame), "methodAddress");
				if ((object)fieldInfo != null)
				{
					methodAddressRef = AccessTools.FieldRefAccess<StackFrame, long>(fieldInfo);
				}
			}
			FieldInfo field = orCreateSharedStateType.GetField("version");
			if ((int)field.GetValue(null) == 0)
			{
				field.SetValue(null, 102);
			}
			actualVersion = (int)field.GetValue(null);
			FieldInfo field2 = orCreateSharedStateType.GetField("state");
			if (field2.GetValue(null) == null)
			{
				field2.SetValue(null, new Dictionary<MethodBase, byte[]>());
			}
			FieldInfo field3 = orCreateSharedStateType.GetField("originals");
			if (field3 != null && field3.GetValue(null) == null)
			{
				field3.SetValue(null, new Dictionary<MethodInfo, MethodBase>());
			}
			FieldInfo field4 = orCreateSharedStateType.GetField("originalsMono");
			if (field4 != null && field4.GetValue(null) == null)
			{
				field4.SetValue(null, new Dictionary<long, MethodBase[]>());
			}
			state = (Dictionary<MethodBase, byte[]>)field2.GetValue(null);
			originals = new Dictionary<MethodInfo, MethodBase>();
			if (field3 != null)
			{
				originals = (Dictionary<MethodInfo, MethodBase>)field3.GetValue(null);
			}
			originalsMono = new Dictionary<long, MethodBase[]>();
			if (field4 != null)
			{
				originalsMono = (Dictionary<long, MethodBase[]>)field4.GetValue(null);
			}
		}

		private static Type GetOrCreateSharedStateType()
		{
			Type type = Type.GetType("HarmonySharedState", throwOnError: false);
			if (type != null)
			{
				return type;
			}
			using ModuleDefinition moduleDefinition = ModuleDefinition.CreateModule("HarmonySharedState", new ModuleParameters
			{
				Kind = ModuleKind.Dll,
				ReflectionImporterProvider = MMReflectionImporter.Provider
			});
			Mono.Cecil.TypeAttributes attributes = Mono.Cecil.TypeAttributes.Public | Mono.Cecil.TypeAttributes.Abstract | Mono.Cecil.TypeAttributes.Sealed;
			TypeDefinition typeDefinition = new TypeDefinition("", "HarmonySharedState", attributes)
			{
				BaseType = moduleDefinition.TypeSystem.Object
			};
			moduleDefinition.Types.Add(typeDefinition);
			typeDefinition.Fields.Add(new FieldDefinition("state", Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static, moduleDefinition.ImportReference(typeof(Dictionary<MethodBase, byte[]>))));
			typeDefinition.Fields.Add(new FieldDefinition("originals", Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static, moduleDefinition.ImportReference(typeof(Dictionary<MethodInfo, MethodBase>))));
			typeDefinition.Fields.Add(new FieldDefinition("originalsMono", Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static, moduleDefinition.ImportReference(typeof(Dictionary<long, MethodBase[]>))));
			typeDefinition.Fields.Add(new FieldDefinition("version", Mono.Cecil.FieldAttributes.Public | Mono.Cecil.FieldAttributes.Static, moduleDefinition.ImportReference(typeof(int))));
			return ReflectionHelper.Load(moduleDefinition).GetType("HarmonySharedState");
		}

		internal static PatchInfo GetPatchInfo(MethodBase method)
		{
			byte[] valueSafe;
			lock (state)
			{
				valueSafe = state.GetValueSafe(method);
			}
			if (valueSafe == null)
			{
				return null;
			}
			return PatchInfoSerialization.Deserialize(valueSafe);
		}

		internal static IEnumerable<MethodBase> GetPatchedMethods()
		{
			lock (state)
			{
				return state.Keys.ToArray();
			}
		}

		internal static void UpdatePatchInfo(MethodBase original, MethodInfo replacement, PatchInfo patchInfo)
		{
			patchInfo.VersionCount++;
			byte[] value = patchInfo.Serialize();
			lock (state)
			{
				state[original] = value;
			}
			lock (originals)
			{
				originals[replacement.Identifiable()] = original;
			}
			if (AccessTools.IsMonoRuntime)
			{
				long key = (long)replacement.MethodHandle.GetFunctionPointer();
				lock (originalsMono)
				{
					originalsMono[key] = new MethodBase[2] { original, replacement };
				}
			}
		}

		internal static MethodBase GetRealMethod(MethodInfo method, bool useReplacement)
		{
			MethodInfo key = method.Identifiable();
			lock (originals)
			{
				if (originals.TryGetValue(key, out var value))
				{
					return value;
				}
			}
			if (AccessTools.IsMonoRuntime)
			{
				long key2 = (long)method.MethodHandle.GetFunctionPointer();
				lock (originalsMono)
				{
					if (originalsMono.TryGetValue(key2, out var value2))
					{
						return useReplacement ? value2[1] : value2[0];
					}
				}
			}
			return method;
		}

		internal static MethodBase GetStackFrameMethod(StackFrame frame, bool useReplacement)
		{
			MethodInfo methodInfo = frame.GetMethod() as MethodInfo;
			if (methodInfo != null)
			{
				return GetRealMethod(methodInfo, useReplacement);
			}
			if (methodAddressRef != null)
			{
				long key = methodAddressRef(frame);
				lock (originalsMono)
				{
					if (originalsMono.TryGetValue(key, out var value))
					{
						return useReplacement ? value[1] : value[0];
					}
				}
			}
			return null;
		}
	}
	internal class ILInstruction
	{
		internal int offset;

		internal System.Reflection.Emit.OpCode opcode;

		internal object operand;

		internal object argument;

		internal List<Label> labels = new List<Label>();

		internal List<ExceptionBlock> blocks = new List<ExceptionBlock>();

		internal ILInstruction(System.Reflection.Emit.OpCode opcode, object operand = null)
		{
			this.opcode = opcode;
			this.operand = operand;
			argument = operand;
		}

		internal CodeInstruction GetCodeInstruction()
		{
			CodeInstruction codeInstruction = new CodeInstruction(opcode, argument);
			if (opcode.OperandType == System.Reflection.Emit.OperandType.InlineNone)
			{
				codeInstruction.operand = null;
			}
			codeInstruction.labels = labels;
			codeInstruction.blocks = blocks;
			return codeInstruction;
		}

		internal int GetSize()
		{
			int num = opcode.Size;
			switch (opcode.OperandType)
			{
			case System.Reflection.Emit.OperandType.InlineSwitch:
				num += (1 + ((Array)operand).Length) * 4;
				break;
			case System.Reflection.Emit.OperandType.InlineI8:
			case System.Reflection.Emit.OperandType.InlineR:
				num += 8;
				break;
			case System.Reflection.Emit.OperandType.InlineBrTarget:
			case System.Reflection.Emit.OperandType.InlineField:
			case System.Reflection.Emit.OperandType.InlineI:
			case System.Reflection.Emit.OperandType.InlineMethod:
			case System.Reflection.Emit.OperandType.InlineSig:
			case System.Reflection.Emit.OperandType.InlineString:
			case System.Reflection.Emit.OperandType.InlineTok:
			case System.Reflection.Emit.OperandType.InlineType:
			case System.Reflection.Emit.OperandType.ShortInlineR:
				num += 4;
				break;
			case System.Reflection.Emit.OperandType.InlineVar:
				num += 2;
				break;
			case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
			case System.Reflection.Emit.OperandType.ShortInlineI:
			case System.Reflection.Emit.OperandType.ShortInlineVar:
				num++;
				break;
			}
			return num;
		}

		public override string ToString()
		{
			string str = "";
			AppendLabel(ref str, this);
			str = str + ": " + opcode.Name;
			if (operand == null)
			{
				return str;
			}
			str += " ";
			switch (opcode.OperandType)
			{
			case System.Reflection.Emit.OperandType.InlineBrTarget:
			case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
				AppendLabel(ref str, operand);
				break;
			case System.Reflection.Emit.OperandType.InlineSwitch:
			{
				ILInstruction[] array = (ILInstruction[])operand;
				for (int i = 0; i < array.Length; i++)
				{
					if (i > 0)
					{
						str += ",";
					}
					AppendLabel(ref str, array[i]);
				}
				break;
			}
			case System.Reflection.Emit.OperandType.InlineString:
				str += $"\"{operand}\"";
				break;
			default:
				str += operand;
				break;
			}
			return str;
		}

		private static void AppendLabel(ref string str, object argument)
		{
			ILInstruction iLInstruction = argument as ILInstruction;
			str += $"IL_{iLInstruction?.offset.ToString("X4") ?? argument}";
		}
	}
	internal class Infix
	{
		internal Patch patch;

		internal MethodInfo OuterMethod => patch.PatchMethod;

		internal MethodBase InnerMethod => patch.innerMethod.Method;

		internal int[] Positions => patch.innerMethod.positions;

		internal Infix(Patch patch)
		{
			this.patch = patch;
		}

		internal bool Matches(MethodBase method, int index, int total)
		{
			if (method != InnerMethod)
			{
				return false;
			}
			if (Positions.Length == 0)
			{
				return true;
			}
			int[] positions = Positions;
			foreach (int num in positions)
			{
				if (num > 0 && num == index)
				{
					return true;
				}
				if (num < 0 && index == total + num + 1)
				{
					return true;
				}
			}
			return false;
		}

		internal IEnumerable<CodeInstruction> Apply(MethodCreatorConfig config, bool isPrefix)
		{
			_ = config;
			yield return Code.Nop[isPrefix ? "inner-prefix" : "inner-postfix", null];
		}
	}
	internal static class InfixExtensions
	{
		internal static Infix[] FilterAndSort(this IEnumerable<Infix> infixes, MethodInfo innerMethod, int index, int total, bool debug)
		{
			return (from p in new PatchSorter((from fix in infixes
					where fix.Matches(innerMethod, index, total)
					select fix.patch).ToArray(), debug).Sort()
				select new Infix(p)).ToArray();
		}
	}
	internal enum InjectionType
	{
		Unknown,
		Instance,
		OriginalMethod,
		ArgsArray,
		Result,
		ResultRef,
		State,
		Exception,
		RunOriginal
	}
	internal class InjectedParameter
	{
		internal ParameterInfo parameterInfo;

		internal string realName;

		internal InjectionType injectionType;

		internal const string INSTANCE_PARAM = "__instance";

		internal const string ORIGINAL_METHOD_PARAM = "__originalMethod";

		internal const string ARGS_ARRAY_VAR = "__args";

		internal const string RESULT_VAR = "__result";

		internal const string RESULT_REF_VAR = "__resultRef";

		internal const string STATE_VAR = "__state";

		internal const string EXCEPTION_VAR = "__exception";

		internal const string RUN_ORIGINAL_VAR = "__runOriginal";

		private static readonly Dictionary<string, InjectionType> types = new Dictionary<string, InjectionType>
		{
			{
				"__instance",
				InjectionType.Instance
			},
			{
				"__originalMethod",
				InjectionType.OriginalMethod
			},
			{
				"__args",
				InjectionType.ArgsArray
			},
			{
				"__result",
				InjectionType.Result
			},
			{
				"__resultRef",
				InjectionType.ResultRef
			},
			{
				"__state",
				InjectionType.State
			},
			{
				"__exception",
				InjectionType.Exception
			},
			{
				"__runOriginal",
				InjectionType.RunOriginal
			}
		};

		internal InjectedParameter(MethodInfo method, ParameterInfo parameterInfo)
		{
			this.parameterInfo = parameterInfo;
			realName = CalculateRealName(method);
			injectionType = Type(realName);
		}

		private string CalculateRealName(MethodInfo method)
		{
			IEnumerable<HarmonyArgument> enumerable = method.GetArgumentAttributes();
			if ((object)method.DeclaringType != null)
			{
				enumerable = enumerable.Union(method.DeclaringType.GetArgumentAttributes());
			}
			HarmonyArgument argumentAttribute = parameterInfo.GetArgumentAttribute();
			if (argumentAttribute != null)
			{
				return argumentAttribute.OriginalName ?? parameterInfo.Name;
			}
			return enumerable.GetRealName(parameterInfo.Name, null) ?? parameterInfo.Name;
		}

		private static InjectionType Type(string name)
		{
			if (types.TryGetValue(name, out var value))
			{
				return value;
			}
			return InjectionType.Unknown;
		}
	}
	internal class InlineSignature : ICallSiteGenerator
	{
		public class ModifierType
		{
			public bool IsOptional;

			public Type Modifier;

			public object Type;

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

		public bool HasThis { get; set; }

		public bool ExplicitThis { get; set; }

		public CallingConvention CallingConvention { get; set; } = CallingConvention.Winapi;

		public List<object> Parameters { get; set; } = new List<object>();

		public object ReturnType { get; set; } = typeof(void);

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

		Mono.Cecil.CallSite ICallSiteGenerator.ToCallSite(ModuleDefinition module)
		{
			Mono.Cecil.CallSite callSite = new Mono.Cecil.CallSite(GetTypeReference(module, ReturnType))
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
	internal static class InlineSignatureParser
	{
		internal static InlineSignature ImportCallSite(Module moduleFrom, byte[] data)
		{
			InlineSignature inlineSignature = new InlineSignature();
			BinaryReader reader;
			using (MemoryStream input = new MemoryStream(data, writable: false))
			{
				reader = new BinaryReader(input);
				try
				{
					ReadMethodSignature(inlineSignature);
					return inlineSignature;
				}
				finally
				{
					if (reader != null)
					{
						((IDisposable)reader).Dispose();
					}
				}
			}
			Type GetTypeDefOrRef()
			{
				uint num = ReadCompressedUInt();
				uint num2 = num >> 2;
				return moduleFrom.ResolveType((num & 3) switch
				{
					0u => (int)(0x2000000 | num2), 
					1u => (int)(0x1000000 | num2), 
					2u => (int)(0x1B000000 | num2), 
					_ => 0, 
				});
			}
			int ReadCompressedInt()
			{
				byte b = reader.ReadByte();
				reader.BaseStream.Seek(-1L, SeekOrigin.Current);
				int num = (int)ReadCompressedUInt();
				int num2 = num >> 1;
				if ((num & 1) == 0)
				{
					return num2;
				}
				switch (b & 0xC0)
				{
				case 0:
				case 64:
					return num2 - 64;
				case 128:
					return num2 - 8192;
				default:
					return num2 - 268435456;
				}
			}
			uint ReadCompressedUInt()
			{
				byte b = reader.ReadByte();
				if ((b & 0x80) == 0)
				{
					return b;
				}
				if ((b & 0x40) == 0)
				{
					return (uint)(((b & -129) << 8) | reader.ReadByte());
				}
				return (uint)(((b & -193) << 24) | (reader.ReadByte() << 16) | (reader.ReadByte() << 8) | reader.ReadByte());
			}
			void ReadMethodSignature(InlineSignature method)
			{
				byte b = reader.ReadByte();
				if ((b & 0x20) != 0)
				{
					method.HasThis = true;
					b = (byte)(b & -33);
				}
				if ((b & 0x40) != 0)
				{
					method.ExplicitThis = true;
					b = (byte)(b & -65);
				}
				method.CallingConvention = (CallingConvention)(b + 1);
				if ((b & 0x10) != 0)
				{
					ReadCompressedUInt();
				}
				uint num = ReadCompressedUInt();
				method.ReturnType = ReadTypeSignature();
				for (int i = 0; i < num; i++)
				{
					method.Parameters.Add(ReadTypeSignature());
				}
			}
			object ReadTypeSignature()
			{
				MetadataType metadataType = (MetadataType)reader.ReadByte();
				switch (metadataType)
				{
				case MetadataType.ValueType:
				case MetadataType.Class:
					return GetTypeDefOrRef();
				case MetadataType.Pointer:
					return ((Type)ReadTypeSignature()).MakePointerType();
				case MetadataType.FunctionPointer:
				{
					InlineSignature inlineSignature2 = new InlineSignature();
					ReadMethodSignature(inlineSignature2);
					return inlineSignature2;
				}
				case MetadataType.ByReference:
					return ((Type)ReadTypeSignature()).MakePointerType();
				case (MetadataType)29:
					return ((Type)ReadTypeSignature()).MakeArrayType();
				case MetadataType.Array:
				{
					Type type2 = (Type)ReadTypeSignature();
					uint rank = ReadCompressedUInt();
					uint num = ReadCompressedUInt();
					for (int num2 = 0; num2 < num; num2++)
					{
						ReadCompressedUInt();
					}
					uint num3 = ReadCompressedUInt();
					for (int num4 = 0; num4 < num3; num4++)
					{
						ReadCompressedInt();
					}
					return type2.MakeArrayType((int)rank);
				}
				case MetadataType.OptionalModifier:
					return new InlineSignature.ModifierType
					{
						IsOptional = true,
						Modifier = GetTypeDefOrRef(),
						Type = ReadTypeSignature()
					};
				case MetadataType.RequiredModifier:
					return new InlineSignature.ModifierType
					{
						IsOptional = false,
						Modifier = GetTypeDefOrRef(),
						Type = ReadTypeSignature()
					};
				case MetadataType.Var:
				case MetadataType.MVar:
					throw new NotSupportedException($"Unsupported generic callsite element: {metadataType}");
				case MetadataType.GenericInstance:
				{
					reader.ReadByte();
					Type type = GetTypeDefOrRef();
					int count = (int)ReadCompressedUInt();
					return type.MakeGenericType((from _ in Enumerable.Range(0, count)
						select (Type)ReadTypeSignature()).ToArray());
				}
				case MetadataType.Object:
					return typeof(object);
				case MetadataType.Void:
					return typeof(void);
				case MetadataType.TypedByReference:
					return typeof(TypedReference);
				case MetadataType.IntPtr:
					return typeof(IntPtr);
				case MetadataType.UIntPtr:
					return typeof(UIntPtr);
				case MetadataType.Boolean:
					return typeof(bool);
				case MetadataType.Char:
					return typeof(char);
				case MetadataType.SByte:
					return typeof(sbyte);
				case MetadataType.Byte:
					return typeof(byte);
				case MetadataType.Int16:
					return typeof(short);
				case MetadataType.UInt16:
					return typeof(ushort);
				case MetadataType.Int32:
					return typeof(int);
				case MetadataType.UInt32:
					return typeof(uint);
				case MetadataType.Int64:
					return typeof(long);
				case MetadataType.UInt64:
					return typeof(ulong);
				case MetadataType.Single:
					return typeof(float);
				case MetadataType.Double:
					return typeof(double);
				case MetadataType.String:
					return typeof(string);
				default:
					throw new NotSupportedException($"Unsupported callsite element: {metadataType}");
				}
			}
		}
	}
	internal class LocalBuilderState
	{
		private readonly Dictionary<string, LocalBuilder> locals = new Dictionary<string, LocalBuilder>();

		public LocalBuilder this[string key]
		{
			get
			{
				return locals[key];
			}
			set
			{
				locals[key] = value;
			}
		}

		public void Add(string key, LocalBuilder local)
		{
			locals[key] = local;
		}

		public bool TryGetValue(string key, out LocalBuilder local)
		{
			return locals.TryGetValue(key, out local);
		}
	}
	internal class MethodCopier
	{
		private readonly MethodBodyReader reader;

		private readonly List<MethodInfo> transpilers = new List<MethodInfo>();

		internal MethodCopier(MethodBase fromMethod, ILGenerator toILGenerator, LocalBuilder[] existingVariables = null)
		{
			if ((object)fromMethod == null)
			{
				throw new ArgumentNullException("fromMethod");
			}
			reader = new MethodBodyReader(fromMethod, toILGenerator);
			reader.DeclareVariables(existingVariables);
			reader.GenerateInstructions();
		}

		internal MethodCopier(MethodCreatorConfig config)
		{
			if ((object)config.MethodBase == null)
			{
				throw new ArgumentNullException("config.methodbase");
			}
			reader = new MethodBodyReader(config.MethodBase, config.il);
			reader.DeclareVariables(config.originalVariables);
			reader.GenerateInstructions();
			reader.SetDebugging(config.debug);
		}

		internal void AddTranspiler(MethodInfo transpiler)
		{
			transpilers.Add(transpiler);
		}

		internal List<CodeInstruction> Finalize(bool stripLastReturn, out bool hasReturnCode, out bool methodEndsInDeadCode, List<Label> endLabels)
		{
			return reader.FinalizeILCodes(transpilers, stripLastReturn, out hasReturnCode, out methodEndsInDeadCode, endLabels);
		}

		internal static List<CodeInstruction> GetInstructions(ILGenerator generator, MethodBase method, int maxTranspilers)
		{
			if (generator == null)
			{
				throw new ArgumentNullException("generator");
			}
			if ((object)method == null)
			{
				throw new ArgumentNullException("method");
			}
			LocalBuilder[] existingVariables = MethodPatcherTools.DeclareOriginalLocalVariables(generator, method);
			MethodCopier methodCopier = new MethodCopier(method, generator, existingVariables);
			Patches patchInfo = Harmony.GetPatchInfo(method);
			if (patchInfo != null)
			{
				List<MethodInfo> sortedPatchMethods = PatchFunctions.GetSortedPatchMethods(method, patchInfo.Transpilers.ToArray(), debug: false);
				for (int i = 0; i < maxTranspilers && i < sortedPatchMethods.Count; i++)
				{
					methodCopier.AddTranspiler(sortedPatchMethods[i]);
				}
			}
			bool hasReturnCode;
			bool methodEndsInDeadCode;
			return methodCopier.Finalize(stripLastReturn: false, out hasReturnCode, out methodEndsInDeadCode, null);
		}
	}
	internal class MethodBodyReader
	{
		private class ThisParameter : ParameterInfo
		{
			internal ThisParameter(MethodBase method)
			{
				MemberImpl = method;
				ClassImpl = method.DeclaringType;
				NameImpl = "this";
				PositionImpl = -1;
			}
		}

		private readonly ILGenerator generator;

		private readonly MethodBase method;

		private bool debug;

		private readonly Module module;

		private readonly Type[] typeArguments;

		private readonly Type[] methodArguments;

		private readonly ByteBuffer ilBytes;

		private readonly ParameterInfo this_parameter;

		private readonly ParameterInfo[] parameters;

		private readonly IList<ExceptionHandlingClause> exceptions;

		private readonly List<ILInstruction> ilInstructions;

		private readonly List<LocalVariableInfo> localVariables;

		private LocalBuilder[] variables;

		private static readonly System.Reflection.Emit.OpCode[] one_byte_opcodes;

		private static readonly System.Reflection.Emit.OpCode[] two_bytes_opcodes;

		internal static List<ILInstruction> GetInstructions(ILGenerator generator, MethodBase method)
		{
			if ((object)method == null)
			{
				throw new ArgumentNullException("method");
			}
			MethodBodyReader methodBodyReader = new MethodBodyReader(method, generator);
			methodBodyReader.DeclareVariables(null);
			methodBodyReader.GenerateInstructions();
			return methodBodyReader.ilInstructions;
		}

		internal MethodBodyReader(MethodBase method, ILGenerator generator)
		{
			this.generator = generator;
			this.method = method;
			module = method.Module;
			System.Reflection.MethodBody methodBody = method.GetMethodBody();
			if ((methodBody?.GetILAsByteArray()?.Length).GetValueOrDefault() == 0)
			{
				ilBytes = new ByteBuffer(Array.Empty<byte>());
				ilInstructions = new List<ILInstruction>();
			}
			else
			{
				byte[] iLAsByteArray = methodBody.GetILAsByteArray();
				if (iLAsByteArray == null)
				{
					throw new ArgumentException("Can not get IL bytes of method " + method.FullDescription());
				}
				ilBytes = new ByteBuffer(iLAsByteArray);
				ilInstructions = new List<ILInstruction>((iLAsByteArray.Length + 1) / 2);
			}
			Type declaringType = method.DeclaringType;
			if ((object)declaringType != null && declaringType.IsGenericType)
			{
				try
				{
					typeArguments = declaringType.GetGenericArguments();
				}
				catch
				{
					typeArguments = null;
				}
			}
			if (method.IsGenericMethod)
			{
				try
				{
					methodArguments = method.GetGenericArguments();
				}
				catch
				{
					methodArguments = null;
				}
			}
			if (!method.IsStatic)
			{
				this_parameter = new ThisParameter(method);
			}
			parameters = method.GetParameters();
			localVariables = methodBody?.LocalVariables?.ToList() ?? new List<LocalVariableInfo>();
			exceptions = methodBody?.ExceptionHandlingClauses ?? new List<ExceptionHandlingClause>();
		}

		internal void SetDebugging(bool debug)
		{
			this.debug = debug;
		}

		internal void GenerateInstructions()
		{
			while (ilBytes.position < ilBytes.buffer.Length)
			{
				int position = ilBytes.position;
				ILInstruction iLInstruction = new ILInstruction(ReadOpCode())
				{
					offset = position
				};
				ReadOperand(iLInstruction);
				ilInstructions.Add(iLInstruction);
			}
			HandleNativeMethod();
			ResolveBranches();
			ParseExceptions();
		}

		internal void HandleNativeMethod()
		{
			if (!(method is MethodInfo { ReflectedType: null } methodInfo))
			{
				return;
			}
			DllImportAttribute dllImportAttribute = methodInfo.GetCustomAttributes(inherit: false).OfType<DllImportAttribute>().FirstOrDefault();
			if (dllImportAttribute != null)
			{
				string[] value = (from p in methodInfo.GetParameters()
					select p.ParameterType.FullName ?? p.ParameterType.Name).ToArray();
				string text = string.Join("_", value);
				string value2 = ((text.Length > 0) ? text.GetHashCode().ToString("X") : "0");
				string assemblyName = $"{(methodInfo.DeclaringType?.FullName ?? "").Replace(".", "_")}_{methodInfo.Name}_{value2}";
				AssemblyName assemblyName2 = new AssemblyName(assemblyName);
				AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName2, AssemblyBuilderAccess.Run);
				ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule(assemblyName2.Name);
				TypeBuilder typeBuilder = moduleBuilder.DefineType("NativeMethodHolder", System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.UnicodeClass);
				MethodBuilder methodBuilder = typeBuilder.DefinePInvokeMethod(methodInfo.Name, dllImportAttribute.Value, System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static | System.Reflection.MethodAttributes.PinvokeImpl, CallingConventions.Standard, methodInfo.ReturnType, (from x in methodInfo.GetParameters()
					select x.ParameterType).ToArray(), dllImportAttribute.CallingConvention, dllImportAttribute.CharSet);
				methodBuilder.SetImplementationFlags(methodBuilder.GetMethodImplementationFlags() | System.Reflection.MethodImplAttributes.PreserveSig);
				Type type = typeBuilder.CreateType();
				MethodInfo operand = type.GetMethod(methodInfo.Name);
				int num = method.GetParameters().Length;
				for (int num2 = 0; num2 < num; num2++)
				{
					ilInstructions.Add(new ILInstruction(System.Reflection.Emit.OpCodes.Ldarg, num2)
					{
						offset = 0
					});
				}
				ilInstructions.Add(new ILInstruction(System.Reflection.Emit.OpCodes.Call, operand)
				{
					offset = num
				});
				ilInstructions.Add(new ILInstruction(System.Reflection.Emit.OpCodes.Ret)
				{
					offset = num + 5
				});
			}
		}

		internal void DeclareVariables(LocalBuilder[] existingVariables)
		{
			if (generator == null)
			{
				return;
			}
			if (existingVariables != null)
			{
				variables = existingVariables;
				return;
			}
			variables = localVariables.Select((LocalVariableInfo lvi) => generator.DeclareLocal(lvi.LocalType, lvi.IsPinned)).ToArray();
		}

		private void ResolveBranches()
		{
			foreach (ILInstruction ilInstruction in ilInstructions)
			{
				switch (ilInstruction.opcode.OperandType)
				{
				case System.Reflection.Emit.OperandType.InlineBrTarget:
				case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
					ilInstruction.operand = GetInstruction((int)ilInstruction.operand, isEndOfInstruction: false);
					break;
				case System.Reflection.Emit.OperandType.InlineSwitch:
				{
					int[] array = (int[])ilInstruction.operand;
					ILInstruction[] array2 = new ILInstruction[array.Length];
					for (int i = 0; i < array.Length; i++)
					{
						array2[i] = GetInstruction(array[i], isEndOfInstruction: false);
					}
					ilInstruction.operand = array2;
					break;
				}
				}
			}
		}

		private void ParseExceptions()
		{
			foreach (ExceptionHandlingClause exception in exceptions)
			{
				int tryOffset = exception.TryOffset;
				int handlerOffset = exception.HandlerOffset;
				int offset = exception.HandlerOffset + exception.HandlerLength - 1;
				ILInstruction instruction = GetInstruction(tryOffset, isEndOfInstruction: false);
				instruction.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
				ILInstruction instruction2 = GetInstruction(offset, isEndOfInstruction: true);
				instruction2.blocks.Add(new ExceptionBlock(ExceptionBlockType.EndExceptionBlock));
				switch (exception.Flags)
				{
				case ExceptionHandlingClauseOptions.Filter:
				{
					ILInstruction instruction6 = GetInstruction(exception.FilterOffset, isEndOfInstruction: false);
					instruction6.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptFilterBlock));
					break;
				}
				case ExceptionHandlingClauseOptions.Finally:
				{
					ILInstruction instruction5 = GetInstruction(handlerOffset, isEndOfInstruction: false);
					instruction5.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFinallyBlock));
					break;
				}
				case ExceptionHandlingClauseOptions.Clause:
				{
					ILInstruction instruction4 = GetInstruction(handlerOffset, isEndOfInstruction: false);
					instruction4.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginCatchBlock, exception.CatchType));
					break;
				}
				case ExceptionHandlingClauseOptions.Fault:
				{
					ILInstruction instruction3 = GetInstruction(handlerOffset, isEndOfInstruction: false);
					instruction3.blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginFaultBlock));
					break;
				}
				}
			}
		}

		private bool EndsInDeadCode(List<CodeInstruction> list)
		{
			int count = list.Count;
			if (count < 2 || list.Last().opcode != System.Reflection.Emit.OpCodes.Throw)
			{
				return false;
			}
			return list.GetRange(0, count - 1).All((CodeInstruction code) => code.opcode != System.Reflection.Emit.OpCodes.Ret);
		}

		internal List<CodeInstruction> FinalizeILCodes(List<MethodInfo> transpilers, bool stripLastReturn, out bool hasReturnCode, out bool methodEndsInDeadCode, List<Label> endLabels)
		{
			hasReturnCode = false;
			methodEndsInDeadCode = false;
			if (generator == null)
			{
				return null;
			}
			foreach (ILInstruction ilInstruction in ilInstructions)
			{
				switch (ilInstruction.opcode.OperandType)
				{
				case System.Reflection.Emit.OperandType.InlineSwitch:
					if (ilInstruction.operand is ILInstruction[] array)
					{
						List<Label> list = new List<Label>();
						ILInstruction[] array2 = array;
						foreach (ILInstruction iLInstruction2 in array2)
						{
							Label item = generator.DefineLabel();
							iLInstruction2.labels.Add(item);
							list.Add(item);
						}
						ilInstruction.argument = list.ToArray();
					}
					break;
				case System.Reflection.Emit.OperandType.InlineBrTarget:
				case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
					if (ilInstruction.operand is ILInstruction iLInstruction)
					{
						Label label = generator.DefineLabel();
						iLInstruction.labels.Add(label);
						ilInstruction.argument = label;
					}
					break;
				}
			}
			CodeTranspiler codeTranspiler = new CodeTranspiler(ilInstructions);
			transpilers.Do(codeTranspiler.Add);
			List<CodeInstruction> result = codeTranspiler.GetResult(generator, method);
			hasReturnCode = result.Any((CodeInstruction code) => code.opcode == System.Reflection.Emit.OpCodes.Ret);
			methodEndsInDeadCode = EndsInDeadCode(result);
			while (stripLastReturn)
			{
				CodeInstruction codeInstruction = result.LastOrDefault();
				if (codeInstruction == null || codeInstruction.opcode != System.Reflection.Emit.OpCodes.Ret)
				{
					break;
				}
				endLabels?.AddRange(codeInstruction.labels);
				result.RemoveAt(result.Count - 1);
			}
			return result;
		}

		private static void GetMemberInfoValue(MemberInfo info, out object result)
		{
			result = null;
			switch (info.MemberType)
			{
			case MemberTypes.Constructor:
				result = (ConstructorInfo)info;
				break;
			case MemberTypes.Event:
				result = (EventInfo)info;
				break;
			case MemberTypes.Field:
				result = (FieldInfo)info;
				break;
			case MemberTypes.Method:
				result = (MethodInfo)info;
				break;
			case MemberTypes.TypeInfo:
			case MemberTypes.NestedType:
				result = (Type)info;
				break;
			case MemberTypes.Property:
				result = (PropertyInfo)info;
				break;
			}
		}

		private void ReadOperand(ILInstruction instruction)
		{
			switch (instruction.opcode.OperandType)
			{
			case System.Reflection.Emit.OperandType.InlineNone:
				instruction.argument = null;
				break;
			case System.Reflection.Emit.OperandType.InlineSwitch:
			{
				int num6 = ilBytes.ReadInt32();
				int num7 = ilBytes.position + 4 * num6;
				int[] array3 = new int[num6];
				for (int i = 0; i < num6; i++)
				{
					array3[i] = ilBytes.ReadInt32() + num7;
				}
				instruction.operand = array3;
				break;
			}
			case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
			{
				sbyte b4 = (sbyte)ilBytes.ReadByte();
				instruction.operand = b4 + ilBytes.position;
				break;
			}
			case System.Reflection.Emit.OperandType.InlineBrTarget:
			{
				int num8 = ilBytes.ReadInt32();
				instruction.operand = num8 + ilBytes.position;
				break;
			}
			case System.Reflection.Emit.OperandType.ShortInlineI:
				if (instruction.opcode == System.Reflection.Emit.OpCodes.Ldc_I4_S)
				{
					sbyte b = (sbyte)ilBytes.ReadByte();
					instruction.operand = b;
					instruction.argument = (sbyte)instruction.operand;
				}
				else
				{
					byte b2 = ilBytes.ReadByte();
					instruction.operand = b2;
					instruction.argument = (byte)instruction.operand;
				}
				break;
			case System.Reflection.Emit.OperandType.InlineI:
			{
				int num5 = ilBytes.ReadInt32();
				instruction.operand = num5;
				instruction.argument = (int)instruction.operand;
				break;
			}
			case System.Reflection.Emit.OperandType.ShortInlineR:
			{
				float num4 = ilBytes.ReadSingle();
				instruction.operand = num4;
				instruction.argument = (float)instruction.operand;
				break;
			}
			case System.Reflection.Emit.OperandType.InlineR:
			{
				double num3 = ilBytes.ReadDouble();
				instruction.operand = num3;
				instruction.argument = (double)instruction.operand;
				break;
			}
			case System.Reflection.Emit.OperandType.InlineI8:
			{
				long num2 = ilBytes.ReadInt64();
				instruction.operand = num2;
				instruction.argument = (long)instruction.operand;
				break;
			}
			case System.Reflection.Emit.OperandType.InlineSig:
			{
				int metadataToken3 = ilBytes.ReadInt32();
				byte[] data = module.ResolveSignature(metadataToken3);
				instruction.argument = (instruction.operand = InlineSignatureParser.ImportCallSite(module, data));
				break;
			}
			case System.Reflection.Emit.OperandType.InlineString:
			{
				int metadataToken6 = ilBytes.ReadInt32();
				instruction.operand = module.ResolveString(metadataToken6);
				instruction.argument = (string)instruction.operand;
				break;
			}
			case System.Reflection.Emit.OperandType.InlineTok:
			{
				int metadataToken5 = ilBytes.ReadInt32();
				instruction.operand = module.ResolveMember(metadataToken5, typeArguments, methodArguments);
				((MemberInfo)instruction.operand).DeclaringType?.FixReflectionCacheAuto();
				GetMemberInfoValue((MemberInfo)instruction.operand, out instruction.argument);
				break;
			}
			case System.Reflection.Emit.OperandType.InlineType:
			{
				int metadataToken4 = ilBytes.ReadInt32();
				instruction.operand = module.ResolveType(metadataToken4, typeArguments, methodArguments);
				((Type)instruction.operand).FixReflectionCacheAuto();
				instruction.argument = (Type)instruction.operand;
				break;
			}
			case System.Reflection.Emit.OperandType.InlineMethod:
			{
				int metadataToken2 = ilBytes.ReadInt32();
				instruction.operand = module.ResolveMethod(metadataToken2, typeArguments, methodArguments);
				((MemberInfo)instruction.operand).DeclaringType?.FixReflectionCacheAuto();
				if (instruction.operand is ConstructorInfo)
				{
					instruction.argument = (ConstructorInfo)instruction.operand;
				}
				else
				{
					instruction.argument = (MethodInfo)instruction.operand;
				}
				break;
			}
			case System.Reflection.Emit.OperandType.InlineField:
			{
				int metadataToken = ilBytes.ReadInt32();
				instruction.operand = module.ResolveField(metadataToken, typeArguments, methodArguments);
				((MemberInfo)instruction.operand).DeclaringType?.FixReflectionCacheAuto();
				instruction.argument = (FieldInfo)instruction.operand;
				break;
			}
			case System.Reflection.Emit.OperandType.ShortInlineVar:
			{
				byte b3 = ilBytes.ReadByte();
				if (TargetsLocalVariable(instruction.opcode))
				{
					LocalVariableInfo localVariable2 = GetLocalVariable(b3);
					if (localVariable2 == null)
					{
						instruction.argument = b3;
						break;
					}
					instruction.operand = localVariable2;
					LocalBuilder[] array2 = variables;
					instruction.argument = ((array2 != null) ? array2[localVariable2.LocalIndex] : null) ?? localVariable2;
				}
				else
				{
					instruction.operand = GetParameter(b3);
					instruction.argument = b3;
				}
				break;
			}
			case System.Reflection.Emit.OperandType.InlineVar:
			{
				short num = ilBytes.ReadInt16();
				if (TargetsLocalVariable(instruction.opcode))
				{
					LocalVariableInfo localVariable = GetLocalVariable(num);
					if (localVariable == null)
					{
						instruction.argument = num;
						break;
					}
					instruction.operand = localVariable;
					LocalBuilder[] array = variables;
					instruction.argument = ((array != null) ? array[localVariable.LocalIndex] : null) ?? localVariable;
				}
				else
				{
					instruction.operand = GetParameter(num);
					instruction.argument = num;
				}
				break;
			}
			default:
				throw new NotSupportedException();
			}
		}

		private ILInstruction GetInstruction(int offset, bool isEndOfInstruction)
		{
			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException("offset", offset, $"Instruction offset {offset} is less than 0");
			}
			int num = ilInstructions.Count - 1;
			ILInstruction iLInstruction = ilInstructions[num];
			if (offset > iLInstruction.offset + iLInstruction.GetSize() - 1)
			{
				throw new ArgumentOutOfRangeException("offset", offset, $"Instruction offset {offset} is outside valid range 0 - {iLInstruction.offset + iLInstruction.GetSize() - 1}");
			}
			int num2 = 0;
			int num3 = num;
			while (num2 <= num3)
			{
				int num4 = num2 + (num3 - num2) / 2;
				iLInstruction = ilInstructions[num4];
				if (isEndOfInstruction)
				{
					if (offset == iLInstruction.offset + iLInstruction.GetSize() - 1)
					{
						return iLInstruction;
					}
				}
				else if (offset == iLInstruction.offset)
				{
					return iLInstruction;
				}
				if (offset < iLInstruction.offset)
				{
					num3 = num4 - 1;
				}
				else
				{
					num2 = num4 + 1;
				}
			}
			throw new Exception($"Cannot find instruction for {offset:X4}");
		}

		private static bool TargetsLocalVariable(System.Reflection.Emit.OpCode opcode)
		{
			return opcode.Name.Contains("loc");
		}

		private LocalVariableInfo GetLocalVariable(int index)
		{
			return localVariables?[index];
		}

		private ParameterInfo GetParameter(int index)
		{
			if (index == 0)
			{
				return this_parameter;
			}
			return parameters[index - 1];
		}

		private System.Reflection.Emit.OpCode ReadOpCode()
		{
			byte b = ilBytes.ReadByte();
			if (b == 254)
			{
				return two_bytes_opcodes[ilBytes.ReadByte()];
			}
			return one_byte_opcodes[b];
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		static MethodBodyReader()
		{
			one_byte_opcodes = new System.Reflection.Emit.OpCode[225];
			two_bytes_opcodes = new System.Reflection.Emit.OpCode[31];
			FieldInfo[] fields = typeof(System.Reflection.Emit.OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public);
			FieldInfo[] array = fields;
			foreach (FieldInfo fieldInfo in array)
			{
				System.Reflection.Emit.OpCode opCode = (System.Reflection.Emit.OpCode)fieldInfo.GetValue(null);
				if (opCode.OpCodeType != System.Reflection.Emit.OpCodeType.Nternal)
				{
					if (opCode.Size == 1)
					{
						one_byte_opcodes[opCode.Value] = opCode;
					}
					else
					{
						two_bytes_opcodes[opCode.Value & 0xFF] = opCode;
					}
				}
			}
		}
	}
	internal class MethodCreator
	{
		internal MethodCreatorConfig config;

		internal MethodCreator(MethodCreatorConfig config)
		{
			if ((object)config.original == null)
			{
				throw new ArgumentNullException("config.original");
			}
			this.config = config;
			if (config.debug)
			{
				FileLog.LogBuffered("### Patch: " + config.original.FullDescription());
				FileLog.FlushBuffer();
			}
			if (!config.Prepare())
			{
				throw new Exception("Could not create replacement method");
			}
		}

		internal (MethodInfo, Dictionary<int, CodeInstruction>) CreateReplacement()
		{
			config.originalVariables = this.DeclareOriginalLocalVariables(config.MethodBase);
			config.localVariables = new VariableState();
			if (config.Fixes.Any() && config.returnType != typeof(void))
			{
				config.resultVariable = config.DeclareLocal(config.returnType);
				config.AddLocal(InjectionType.Result, config.resultVariable);
				config.AddCodes(this.GenerateVariableInit(config.resultVariable, isReturnValue: true));
			}
			if (config.AnyFixHas(InjectionType.ResultRef) && config.returnType.IsByRef)
			{
				Type type = typeof(RefResult<>).MakeGenericType(config.returnType.GetElementType());
				LocalBuilder localBuilder = config.DeclareLocal(type);
				config.AddLocal(InjectionType.ResultRef, localBuilder);
				config.AddCodes(new <>z__ReadOnlyArray<CodeInstruction>(new CodeInstruction[2]
				{
					Code.Ldnull,
					Code.Stloc[localBuilder, null]
				}));
			}
			if (config.AnyFixHas(InjectionType.ArgsArray))
			{
				LocalBuilder localBuilder2 = config.DeclareLocal(typeof(object[]));
				config.AddLocal(InjectionType.ArgsArray, localBuilder2);
				config.AddCodes(this.PrepareArgumentArray());
				config.AddCode(Code.Stloc[localBuilder2, null]);
			}
			config.skipOriginalLabel = null;
			bool flag = config.prefixes.Any(this.AffectsOriginal);
			bool flag2 = config.AnyFixHas(InjectionType.RunOriginal);
			if (flag || flag2)
			{
				config.runOriginalVariable = config.DeclareLocal(typeof(bool));
				config.AddCodes(new <>z__ReadOnlyArray<CodeInstruction>(new CodeInstruction[2]
				{
					Code.Ldc_I4_1,
					Code.Stloc[config.runOriginalVariable, null]
				}));
				if (flag)
				{
					config.skipOriginalLabel = config.DefineLabel();
				}
			}
			config.WithFixes(delegate(MethodInfo fix)
			{
				Type declaringType = fix.DeclaringType;
				if ((object)declaringType == null)
				{
					return;
				}
				string assemblyQualifiedName = declaringType.AssemblyQualifiedName;
				config.localVariables.TryGetValue(assemblyQualifiedName, out var local2);
				foreach (InjectedParameter item2 in config.InjectionsFor(fix, InjectionType.State))
				{
					Type parameterType = item2.parameterInfo.ParameterType;
					Type type2 = (parameterType.IsByRef ? parameterType.GetElementType() : parameterType);
					if (local2 != null)
					{
						if (!type2.IsAssignableFrom(local2.LocalType))
						{
							string message = $"__state type mismatch in patch \"{fix.DeclaringType.FullName}.{fix.Name}\": previous __state was declared as \"{local2.LocalType.FullName}\" but this patch expects \"{type2.FullName}\"";
							throw new HarmonyException(message);
						}
					}
					else
					{
						LocalBuilder localBuilder3 = config.DeclareLocal(type2);
						config.AddLocal(assemblyQualifiedName, localBuilder3);
						config.AddCodes(this.GenerateVariableInit(localBuilder3));
					}
				}
			});
			config.finalizedVariable = null;
			if (config.finalizers.Count > 0)
			{
				config.finalizedVariable = config.DeclareLocal(typeof(bool));
				config.AddCodes(this.GenerateVariableInit(config.finalizedVariable));
				config.exceptionVariable = config.DeclareLocal(typeof(Exception));
				config.AddLocal(InjectionType.Exception, config.exceptionVariable);
				config.AddCodes(this.GenerateVariableInit(config.exceptionVariable));
				config.AddCode(this.MarkBlock(ExceptionBlockType.BeginExceptionBlock));
			}
			AddPrefixes();
			if (config.skipOriginalLabel.HasValue)
			{
				config.AddCodes(new <>z__ReadOnlyArray<CodeInstruction>(new CodeInstruction[2]
				{
					Code.Ldloc[config.runOriginalVariable, null],
					Code.Brfalse[config.skipOriginalLabel.Value, null]
				}));
			}
			MethodCopier methodCopier = new MethodCopier(config);
			foreach (MethodInfo transpiler in config.transpilers)
			{
				methodCopier.AddTranspiler(transpiler);
			}
			methodCopier.AddTranspiler(PatchTools.m_GetExecutingAssemblyReplacementTranspiler);
			List<Label> list = new List<Label>();
			List<CodeInstruction> instructions = methodCopier.Finalize(stripLastReturn: true, out var hasReturnCode, out var methodEndsInDeadCode, list);
			instructions = AddInfixes(instructions).ToList();
			config.AddCode(Code.Nop["start original", null]);
			config.AddCodes(this.CleanupCodes(instructions, list));
			config.AddCode(Code.Nop["end original", null]);
			if (list.Count > 0)
			{
				config.AddCode(Code.Nop.WithLabels(list));
			}
			if (config.resultVariable != null && hasReturnCode)
			{
				config.AddCode(Code.Stloc[config.resultVariable, null]);
			}
			if (config.skipOriginalLabel.HasValue)
			{
				config.AddCode(Code.Nop.WithLabels(config.skipOriginalLabel.Value));
			}
			AddPostfixes(passthroughPatches: false);
			if (config.resultVariable != null && (hasReturnCode || (methodEndsInDeadCode && config.skipOriginalLabel.HasValue)))
			{
				config.AddCode(Code.Ldloc[config.resultVariable, null]);
			}
			bool flag3 = AddPostfixes(passthroughPatches: true);
			if (config.finalizers.Count > 0)
			{
				LocalBuilder local = config.GetLocal(InjectionType.Exception);
				if (flag3)
				{
					config.AddCode(Code.Stloc[config.resultVariable, null]);
					config.AddCode(Code.Ldloc[config.resultVariable, null]);
				}
				AddFinalizers(catchExceptions: false);
				config.AddCode(Code.Ldc_I4_1);
				config.AddCode(Code.Stloc[config.finalizedVariable, null]);
				Label label = config.DefineLabel();
				config.AddCode(Code.Ldloc[local, null]);
				config.AddCode(Code.Brfalse[label, null]);
				config.AddCode(Code.Ldloc[local, null]);
				config.AddCode(Code.Throw);
				config.AddCode(Code.Nop.WithLabels(label));
				config.AddCode(this.MarkBlock(ExceptionBlockType.BeginCatchBlock));
				config.AddCode(Code.Stloc[local, null]);
				config.AddCode(Code.Ldloc[config.finalizedVariable, null]);
				Label label2 = config.DefineLabel();
				config.AddCode(Code.Brtrue[label2, null]);
				bool flag4 = AddFinalizers(catchExceptions: true);
				config.AddCode(Code.Nop.WithLabels(label2));
				Label label3 = config.DefineLabel();
				config.AddCode(Code.Ldloc[local, null]);
				config.AddCode(Code.Brfalse[label3, null]);
				if (flag4)
				{
					config.AddCode(Code.Rethrow);
				}
				else
				{
					config.AddCode(Code.Ldloc[local, null]);
					config.AddCode(Code.Throw);
				}
				config.AddCode(Code.Nop.WithLabels(label3));
				config.AddCode(this.MarkBlock(ExceptionBlockType.EndExceptionBlock));
				if (config.resultVariable != null)
				{
					config.AddCode(Code.Ldloc[config.resultVariable, null]);
				}
			}
			if (methodEndsInDeadCode)
			{
				Label? skipOriginalLabel = config.skipOriginalLabel;
				if (!skipOriginalLabel.HasValue && config.finalizers.Count <= 0 && config.postfixes.Count <= 0)
				{
					goto IL_0860;
				}
			}
			config.AddCode(Code.Ret);
			goto IL_0860;
			IL_0860:
			config.instructions = FaultBlockRewriter.Rewrite(config.instructions, config.il);
			if (config.debug)
			{
				Emitter emitter = new Emitter(config.il);
				this.LogCodes(emitter, config.instructions);
			}
			Emitter emitter2 = new Emitter(config.il);
			this.EmitCodes(emitter2, config.instructions);
			MethodInfo item = config.patch.Generate();
			if (config.debug)
			{
				FileLog.LogBuffered("DONE");
				FileLog.LogBuffered("");
				FileLog.FlushBuffer();
			}
			return (item, emitter2.GetInstructions());
		}

		internal void AddPrefixes()
		{
			foreach (MethodInfo prefix in config.prefixes)
			{
				Label? label = (this.AffectsOriginal(prefix) ? new Label?(config.DefineLabel()) : ((Label?)null));
				if (label.HasValue)
				{
					config.AddCodes(new <>z__ReadOnlyArray<CodeInstruction>(new CodeInstruction[2]
					{
						Code.Ldloc[config.runOriginalVariable, null],
						Code.Brfalse[label.Value, null]
					}));
				}
				List<KeyValuePair<LocalBuilder, Type>> list = new List<KeyValuePair<LocalBuilder, Type>>();
				config.AddCodes(this.EmitCallParameter(prefix, allowFirsParamPassthrough: false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, list));
				config.AddCode(Code.Call[prefix, null]);
				if (MethodPatcherTools.OriginalParameters(prefix).Any(((ParameterInfo info, string realName) pair) => pair.realName == "__args"))
				{
					config.AddCodes(this.RestoreArgumentArray());
				}
				if (tmpInstanceBoxingVar != null)
				{
					config.AddCode(Code.Ldarg_0);
					config.AddCode(Code.Ldloc[tmpInstanceBoxingVar, null]);
					config.AddCode(Code.Unbox_Any[config.original.DeclaringType, null]);
					config.AddCode(Code.Stobj[config.original.DeclaringType, null]);
				}
				if (refResultUsed)
				{
					Label label2 = config.DefineLabel();
					config.AddCode(Code.Ldloc[config.GetLocal(InjectionType.ResultRef), null]);
					config.AddCode(Code.Brfalse_S[label2, null]);
					config.AddCode(Code.Ldloc[config.GetLocal(InjectionType.ResultRef), null]);
					config.AddCode(Code.Callvirt[AccessTools.Method(config.GetLocal(InjectionType.ResultRef).LocalType, "Invoke"), null]);
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.Result), null]);
					config.AddCode(Code.Ldnull);
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.ResultRef), null]);
					config.AddCode(Code.Nop.WithLabels(label2));
				}
				else if (tmpObjectVar != null)
				{
					config.AddCode(Code.Ldloc[tmpObjectVar, null]);
					config.AddCode(Code.Unbox_Any[AccessTools.GetReturnedType(config.original), null]);
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.Result), null]);
				}
				list.Do(delegate(KeyValuePair<LocalBuilder, Type> tmpBoxVar)
				{
					config.AddCode(new CodeInstruction(config.OriginalIsStatic ? System.Reflection.Emit.OpCodes.Ldarg_0 : System.Reflection.Emit.OpCodes.Ldarg_1));
					config.AddCode(Code.Ldloc[tmpBoxVar.Key, null]);
					config.AddCode(Code.Unbox_Any[tmpBoxVar.Value, null]);
					config.AddCode(Code.Stobj[tmpBoxVar.Value, null]);
				});
				Type returnType = prefix.ReturnType;
				if (returnType != typeof(void))
				{
					if (returnType != typeof(bool))
					{
						throw new Exception($"Prefix patch {prefix} has not \"bool\" or \"void\" return type: {prefix.ReturnType}");
					}
					config.AddCode(Code.Stloc[config.runOriginalVariable, null]);
				}
				if (label.HasValue)
				{
					config.AddCode(Code.Nop.WithLabels(label.Value));
				}
			}
		}

		internal bool AddPostfixes(bool passthroughPatches)
		{
			bool result = false;
			MethodBase original = config.original;
			bool originalIsStatic = original.IsStatic;
			foreach (MethodInfo item in config.postfixes.Where((MethodInfo fix) => passthroughPatches == (fix.ReturnType != typeof(void))))
			{
				List<KeyValuePair<LocalBuilder, Type>> list = new List<KeyValuePair<LocalBuilder, Type>>();
				config.AddCodes(this.EmitCallParameter(item, allowFirsParamPassthrough: true, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, list));
				config.AddCode(Code.Call[item, null]);
				if (MethodPatcherTools.OriginalParameters(item).Any(((ParameterInfo info, string realName) pair) => pair.realName == "__args"))
				{
					config.AddCodes(this.RestoreArgumentArray());
				}
				if (tmpInstanceBoxingVar != null)
				{
					config.AddCode(Code.Ldarg_0);
					config.AddCode(Code.Ldloc[tmpInstanceBoxingVar, null]);
					config.AddCode(Code.Unbox_Any[original.DeclaringType, null]);
					config.AddCode(Code.Stobj[original.DeclaringType, null]);
				}
				if (refResultUsed)
				{
					Label label = config.DefineLabel();
					config.AddCode(Code.Ldloc[config.GetLocal(InjectionType.ResultRef), null]);
					config.AddCode(Code.Brfalse_S[label, null]);
					config.AddCode(Code.Ldloc[config.GetLocal(InjectionType.ResultRef), null]);
					config.AddCode(Code.Callvirt[AccessTools.Method(config.GetLocal(InjectionType.ResultRef).LocalType, "Invoke"), null]);
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.Result), null]);
					config.AddCode(Code.Ldnull);
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.ResultRef), null]);
					config.AddCode(Code.Nop.WithLabels(label));
				}
				else if (tmpObjectVar != null)
				{
					config.AddCode(Code.Ldloc[tmpObjectVar, null]);
					config.AddCode(Code.Unbox_Any[AccessTools.GetReturnedType(original), null]);
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.Result), null]);
				}
				list.Do(delegate(KeyValuePair<LocalBuilder, Type> tmpBoxVar)
				{
					config.AddCode(new CodeInstruction(originalIsStatic ? System.Reflection.Emit.OpCodes.Ldarg_0 : System.Reflection.Emit.OpCodes.Ldarg_1));
					config.AddCode(Code.Ldloc[tmpBoxVar.Key, null]);
					config.AddCode(Code.Unbox_Any[tmpBoxVar.Value, null]);
					config.AddCode(Code.Stobj[tmpBoxVar.Value, null]);
				});
				if (!(item.ReturnType != typeof(void)))
				{
					continue;
				}
				ParameterInfo parameterInfo = item.GetParameters().FirstOrDefault();
				if (parameterInfo != null && item.ReturnType == parameterInfo.ParameterType)
				{
					result = true;
					continue;
				}
				if (parameterInfo != null)
				{
					throw new Exception($"Return type of pass through postfix {item} does not match type of its first parameter");
				}
				throw new Exception($"Postfix patch {item} must have a \"void\" return type");
			}
			return result;
		}

		internal bool AddFinalizers(bool catchExceptions)
		{
			bool rethrowPossible = true;
			MethodBase original = config.original;
			bool originalIsStatic = original.IsStatic;
			config.finalizers.Do(delegate(MethodInfo fix)
			{
				if (catchExceptions)
				{
					config.AddCode(this.MarkBlock(ExceptionBlockType.BeginExceptionBlock));
				}
				List<KeyValuePair<LocalBuilder, Type>> list = new List<KeyValuePair<LocalBuilder, Type>>();
				config.AddCodes(this.EmitCallParameter(fix, allowFirsParamPassthrough: false, out var tmpInstanceBoxingVar, out var tmpObjectVar, out var refResultUsed, list));
				config.AddCode(Code.Call[fix, null]);
				if (MethodPatcherTools.OriginalParameters(fix).Any(((ParameterInfo info, string realName) pair) => pair.realName == "__args"))
				{
					config.AddCodes(this.RestoreArgumentArray());
				}
				if (tmpInstanceBoxingVar != null)
				{
					config.AddCode(Code.Ldarg_0);
					config.AddCode(Code.Ldloc[tmpInstanceBoxingVar, null]);
					config.AddCode(Code.Unbox_Any[original.DeclaringType, null]);
					config.AddCode(Code.Stobj[original.DeclaringType, null]);
				}
				if (refResultUsed)
				{
					Label label = config.DefineLabel();
					config.AddCode(Code.Ldloc[config.GetLocal(InjectionType.ResultRef), null]);
					config.AddCode(Code.Brfalse_S[label, null]);
					config.AddCode(Code.Ldloc[config.GetLocal(InjectionType.ResultRef), null]);
					config.AddCode(Code.Callvirt[AccessTools.Method(config.GetLocal(InjectionType.ResultRef).LocalType, "Invoke"), null]);
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.Result), null]);
					config.AddCode(Code.Ldnull);
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.ResultRef), null]);
					config.AddCode(Code.Nop.WithLabels(label));
				}
				else if (tmpObjectVar != null)
				{
					config.AddCode(Code.Ldloc[tmpObjectVar, null]);
					config.AddCode(Code.Unbox_Any[AccessTools.GetReturnedType(original), null]);
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.Result), null]);
				}
				list.Do(delegate(KeyValuePair<LocalBuilder, Type> tmpBoxVar)
				{
					config.AddCode(new CodeInstruction(originalIsStatic ? System.Reflection.Emit.OpCodes.Ldarg_0 : System.Reflection.Emit.OpCodes.Ldarg_1));
					config.AddCode(Code.Ldloc[tmpBoxVar.Key, null]);
					config.AddCode(Code.Unbox_Any[tmpBoxVar.Value, null]);
					config.AddCode(Code.Stobj[tmpBoxVar.Value, null]);
				});
				if (fix.ReturnType != typeof(void))
				{
					config.AddCode(Code.Stloc[config.GetLocal(InjectionType.Exception), null]);
					rethrowPossible = false;
				}
				if (catchExceptions)
				{
					config.AddCode(this.MarkBlock(ExceptionBlockType.BeginCatchBlock));
					config.AddCode(Code.Pop);
					config.AddCode(this.MarkBlock(ExceptionBlockType.EndExceptionBlock));
				}
			});
			return rethrowPossible;
		}

		private IEnumerable<CodeInstruction> AddInfixes(IEnumerable<CodeInstruction> instructions)
		{
			IEnumerable<IGrouping<MethodInfo, CodeInstruction>> source = from ins in instructions
				where ins.opcode == System.Reflection.Emit.OpCodes.Call || ins.opcode == System.Reflection.Emit.OpCodes.Callvirt
				where ins.operand is MethodInfo
				group ins by (MethodInfo)ins.operand;
			Dictionary<CodeInstruction, CodeInstruction[]> replacements = new Dictionary<CodeInstruction, CodeInstruction[]>();
			foreach (var item3 in source.Select((IGrouping<MethodInfo, CodeInstruction> g) => (Key: g.Key, Calls: g.ToList())))
			{
				MethodInfo item = item3.Key;
				List<CodeInstruction> item2 = item3.Calls;
				int count = item2.Count;
				for (int num = 0; num < count; num++)
				{
					CodeInstruction codeInstruction = item2[num];
					IEnumerable<CodeInstruction> collection = config.innerprefixes.FilterAndSort(item, num + 1, count, config.debug).SelectMany((Infix fix) => fix.Apply(config, isPrefix: true));
					IEnumerable<CodeInstruction> collection2 = config.innerpostfixes.FilterAndSort(item, num + 1, count, config.debug).SelectMany((Infix fix) => fix.Apply(config, isPrefix: false));
					Dictionary<CodeInstruction, CodeInstruction[]> dictionary = replacements;
					List<CodeInstruction> list = new List<CodeInstruction>();
					list.AddRange(collection);
					list.Add(codeInstruction);
					list.AddRange(collection2);
					dictionary[codeInstruction] = list.ToArray();
				}
			}
			CodeInstruction[] value;
			return instructions.SelectMany((CodeInstruction instruction) => (!replacements.TryGetValue(instruction, out value)) ? new CodeInstruction[1] { instruction } : value);
		}
	}
	internal class MethodCreatorConfig
	{
		internal readonly MethodBase original;

		internal readonly MethodBase source;

		internal readonly List<MethodInfo> prefixes;

		internal readonly List<MethodInfo> postfixes;

		internal readonly List<MethodInfo> transpilers;

		internal readonly List<MethodInfo> finalizers;

		internal readonly List<Infix> innerprefixes;

		internal readonly List<Infix> innerpostfixes;

		internal readonly bool debug;

		internal int patchIndex;

		internal DynamicMethodDefinition patch;

		internal Dictionary<MethodInfo, List<InjectedParameter>> injections;

		internal Type returnType;

		internal ILGenerator il;

		internal List<CodeInstruction> instructions;

		internal LocalBuilder[] originalVariables;

		internal VariableState localVariables;

		internal LocalBuilder resultVariable;

		internal Label? skipOriginalLabel;

		internal LocalBuilder runOriginalVariable;

		internal LocalBuilder exceptionVariable;

		internal LocalBuilder finalizedVariable;

		internal MethodBase MethodBase => source ?? original;

		internal bool OriginalIsStatic => original.IsStatic;

		internal IEnumerable<MethodInfo> Fixes => prefixes.Union(postfixes).Union(finalizers);

		internal IEnumerable<Infix> InnerFixes => innerprefixes.Union(innerpostfixes);

		internal MethodCreatorConfig(MethodBase original, MethodBase source, List<MethodInfo> prefixes, List<MethodInfo> postfixes, List<MethodInfo> transpilers, List<MethodInfo> finalizers, List<Infix> innerprefixes, List<Infix> innerpostfixes, bool debug)
		{
			this.original = original;
			this.source = source;
			this.prefixes = prefixes;
			this.postfixes = postfixes;
			this.transpilers = transpilers;
			this.finalizers = finalizers;
			this.innerprefixes = innerprefixes;
			this.innerpostfixes = innerpostfixes;
			this.debug = debug;
		}

		internal bool Prepare()
		{
			PatchInfo patchInfo = HarmonySharedState.GetPatchInfo(original) ?? new PatchInfo();
			patchIndex = patchInfo.VersionCount + 1;
			patch = MethodPatcherTools.CreateDynamicMethod(original, $"_Patch{patchIndex}", debug);
			if (patch == null)
			{
				return false;
			}
			injections = Fixes.Union(InnerFixes.Select((Infix fix) => fix.OuterMethod)).ToDictionary((MethodInfo fix) => fix, (MethodInfo fix) => (from p in fix.GetParameters()
				select new InjectedParameter(fix, p)).ToList());
			returnType = AccessTools.GetReturnedType(original);
			il = patch.GetILGenerator();
			instructions = new List<CodeInstruction>();
			return true;
		}

		internal void AddCode(CodeInstruction code)
		{
			instructions.Add(code);
		}

		internal void AddCodes(IEnumerable<CodeInstruction> codes)
		{
			instructions.AddRange(codes);
		}

		internal void AddLocal(InjectionType type, LocalBuilder local)
		{
			localVariables.Add(type, local);
		}

		internal void AddLocal(string name, LocalBuilder local)
		{
			localVariables.Add(name, local);
		}

		internal LocalBuilder GetLocal(InjectionType type)
		{
			return localVariables[type];
		}

		internal LocalBuilder GetLocal(string name)
		{
			return localVariables[name];
		}

		internal bool HasLocal(string name)
		{
			LocalBuilder local;
			return localVariables.TryGetValue(name, out local);
		}

		internal LocalBuilder DeclareLocal(Type type, bool isPinned = false)
		{
			return il.DeclareLocal(type, isPinned);
		}

		internal Label DefineLabel()
		{
			return il.DefineLabel();
		}

		internal IEnumerable<InjectedParameter> InjectionsFor(MethodInfo fix, InjectionType type = InjectionType.Unknown)
		{
			if (injections.TryGetValue(fix, out var value))
			{
				if (type != InjectionType.Unknown)
				{
					return value.Where((InjectedParameter pair) => pair.injectionType == type);
				}
				return value;
			}
			return Array.Empty<InjectedParameter>();
		}

		internal bool AnyFixHas(InjectionType type)
		{
			return injections.Values.SelectMany((List<InjectedParameter> list) => list).Any((InjectedParameter pair) => pair.injectionType == type);
		}

		internal void WithFixes(Action<MethodInfo> action)
		{
			foreach (MethodInfo fix in Fixes)
			{
				action(fix);
			}
			foreach (Infix innerFix in InnerFixes)
			{
				action(innerFix.OuterMethod);
			}
		}
	}
	internal static class MethodCreatorTools
	{
		internal const string PARAM_INDEX_PREFIX = "__";

		private const string INSTANCE_FIELD_PREFIX = "___";

		private static readonly Dictionary<System.Reflection.Emit.OpCode, System.Reflection.Emit.OpCode> shortJumps = new Dictionary<System.Reflection.Emit.OpCode, System.Reflection.Emit.OpCode>
		{
			{
				System.Reflection.Emit.OpCodes.Leave_S,
				System.Reflection.Emit.OpCodes.Leave
			},
			{
				System.Reflection.Emit.OpCodes.Brfalse_S,
				System.Reflection.Emit.OpCodes.Brfalse
			},
			{
				System.Reflection.Emit.OpCodes.Brtrue_S,
				System.Reflection.Emit.OpCodes.Brtrue
			},
			{
				System.Reflection.Emit.OpCodes.Beq_S,
				System.Reflection.Emit.OpCodes.Beq
			},
			{
				System.Reflection.Emit.OpCodes.Bge_S,
				System.Reflection.Emit.OpCodes.Bge
			},
			{
				System.Reflection.Emit.OpCodes.Bgt_S,
				System.Reflection.Emit.OpCodes.Bgt
			},
			{
				System.Reflection.Emit.OpCodes.Ble_S,
				System.Reflection.Emit.OpCodes.Ble
			},
			{
				System.Reflection.Emit.OpCodes.Blt_S,
				System.Reflection.Emit.OpCodes.Blt
			},
			{
				System.Reflection.Emit.OpCodes.Bne_Un_S,
				System.Reflection.Emit.OpCodes.Bne_Un
			},
			{
				System.Reflection.Emit.OpCodes.Bge_Un_S,
				System.Reflection.Emit.OpCodes.Bge_Un
			},
			{
				System.Reflection.Emit.OpCodes.Bgt_Un_S,
				System.Reflection.Emit.OpCodes.Bgt_Un
			},
			{
				System.Reflection.Emit.OpCodes.Ble_Un_S,
				System.Reflection.Emit.OpCodes.Ble_Un
			},
			{
				System.Reflection.Emit.OpCodes.Br_S,
				System.Reflection.Emit.OpCodes.Br
			},
			{
				System.Reflection.Emit.OpCodes.Blt_Un_S,
				System.Reflection.Emit.OpCodes.Blt_Un
			}
		};

		private static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[1] { typeof(RuntimeMethodHandle) });

		private static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[2]
		{
			typeof(RuntimeMethodHandle),
			typeof(RuntimeTypeHandle)
		});

		private static readonly HashSet<Type> PrimitivesWithObjectTypeCode = new HashSet<Type>
		{
			typeof(IntPtr),
			typeof(UIntPtr),
			typeof(IntPtr),
			typeof(UIntPtr)
		};

		internal static List<CodeInstruction> GenerateVariableInit(this MethodCreator _, LocalBuilder variable, bool isReturnValue = false)
		{
			List<CodeInstruction> list = new List<CodeInstruction>();
			Type type = variable.LocalType;
			if (type.IsByRef)
			{
				if (isReturnValue)
				{
					list.Add(Code.Ldc_I4_1);
					list.Add(Code.Newarr[type.GetElementType(), null]);
					list.Add(Code.Ldc_I4_0);
					list.Add(Code.Ldelema[type.GetElementType(), null]);
					list.Add(Code.Stloc[variable, null]);
					return list;
				}
				type = type.GetElementType();
			}
			if (type.IsEnum)
			{
				type = Enum.GetUnderlyingType(type);
			}
			if (AccessTools.IsClass(type))
			{
				list.Add(Code.Ldnull);
				list.Add(Code.Stloc[variable, null]);
				return list;
			}
			if (AccessTools.IsStruct(type))
			{
				list.Add(Code.Ldloca[variable, null]);
				list.Add(Code.Initobj[type, null]);
				return list;
			}
			if (AccessTools.IsValue(type))
			{
				if (type == typeof(float))
				{
					list.Add(Code.Ldc_R4[0f, null]);
				}
				else if (type == typeof(double))
				{
					list.Add(Code.Ldc_R8[0.0, null]);
				}
				else if (type == typeof(long) || type == typeof(ulong))
				{
					list.Add(Code.Ldc_I8[0L, null]);
				}
				else
				{
					list.Add(Code.Ldc_I4[0, null]);
				}
				list.Add(Code.Stloc[variable, null]);
				return list;
			}
			return list;
		}

		internal static List<CodeInstruction> PrepareArgumentArray(this MethodCreator creator)
		{
			List<CodeInstruction> list = new List<CodeInstruction>();
			MethodBase original = creator.config.original;
			bool isStatic = original.IsStatic;
			ParameterInfo[] parameters = original.GetParameters();
			int num = 0;
			ParameterInfo[] array = parameters;
			foreach (ParameterInfo parameterInfo in array)
			{
				int argIndex = num++ + ((!isStatic) ? 1 : 0);
				if (parameterInfo.IsOut || parameterInfo.IsRetval)
				{
					list.AddRange(InitializeOutParameter(argIndex, parameterInfo.ParameterType));
				}
			}
			list.Add(Code.Ldc_I4[parameters.Length, null]);
			list.Add(Code.Newarr[typeof(object), null]);
			num = 0;
			int num2 = 0;
			ParameterInfo[] array2 = parameters;
			foreach (ParameterInfo parameterInfo2 in array2)
			{
				int num3 = num++ + ((!isStatic) ? 1 : 0);
				Type type = parameterInfo2.ParameterType;
				bool isByRef = type.IsByRef;
				if (isByRef)
				{
					type = type.GetElementType();
				}
				list.Add(Code.Dup);
				list.Add(Code.Ldc_I4[num2++, null]);
				list.Add(Code.Ldarg[num3, null]);
				if (isByRef)
				{
					if (AccessTools.IsStruct(type))
					{
						list.Add(Code.Ldobj[type, null]);
					}
					else
					{
						list.Add(LoadIndOpCodeFor(type));
					}
				}
				if (type.IsValueType)
				{
					list.Add(Code.Box[type, null]);
				}
				list.Add(Code.Stelem_Ref);
			}
			return list;
		}

		internal static bool AffectsOriginal(this MethodCreator creator, MethodInfo fix)
		{
			if (fix.ReturnType == typeof(bool))
			{
				return true;
			}
			if (!creator.config.injections.TryGetValue(fix, out var value))
			{
				return false;
			}
			return value.Any(delegate(InjectedParameter parameter)
			{
				if (parameter.injectionType == InjectionType.Instance)
				{
					return false;
				}
				if (parameter.injectionType == InjectionType.OriginalMethod)
				{
					return false;
				}
				if (parameter.injectionType == InjectionType.State)
				{
					return false;
				}
				ParameterInfo parameterInfo = parameter.parameterInfo;
				if (parameterInfo.IsOut || parameterInfo.IsRetval)
				{
					return true;
				}
				Type parameterType = parameterInfo.ParameterType;
				if (parameterType.IsByRef)
				{
					return true;
				}
				return (!AccessTools.IsValue(parameterType) && !AccessTools.IsStruct(parameterType)) ? true : false;
			});
		}

		internal static CodeInstruction MarkBlock(this MethodCreator _, ExceptionBlockType blockType)
		{
			return Code.Nop.WithBlocks(new ExceptionBlock(blockType));
		}

		internal static List<CodeInstruction> EmitCallParameter(this MethodCreator creator, MethodInfo patch, bool allowFirsParamPassthrough, out LocalBuilder tmpInstanceBoxingVar, out LocalBuilder tmpObjectVar, out bool refResultUsed, List<KeyValuePair<LocalBuilder, Type>> tmpBoxVars)
		{
			tmpInstanceBoxingVar = null;
			tmpObjectVar = null;
			refResultUsed = false;
			List<CodeInstruction> list = new List<CodeInstruction>();
			MethodCreatorConfig config = creator.config;
			MethodBase original = config.original;
			bool isStatic = original.IsStatic;
			Type returnType = config.returnType;
			List<InjectedParameter> list2 = config.injections[patch].ToList();
			bool flag = !isStatic;
			ParameterInfo[] parameters = original.GetParameters();
			string[] originalParameterNames = parameters.Select((ParameterInfo p) => p.Name).ToArray();
			Type declaringType = original.DeclaringType;
			List<ParameterInfo> list3 = patch.GetParameters().ToList();
			if (allowFirsParamPassthrough && patch.ReturnType != typeof(void) && list3.Count > 0 && list3[0].ParameterType == patch.ReturnType)
			{
				list2.RemoveAt(0);
				list3.RemoveAt(0);
			}
			foreach (InjectedParameter item in list2)
			{
				InjectionType injectionType = item.injectionType;
				string realName = item.realName;
				Type parameterType = item.parameterInfo.ParameterType;
				switch (injectionType)
				{
				case InjectionType.OriginalMethod:
					if (!EmitOriginalBaseMethod(original, list))
					{
						list.Add(Code.Ldnull);
					}
					continue;
				case InjectionType.Exception:
					if (config.exceptionVariable != null)
					{
						list.Add(Code.Ldloc[config.exceptionVariable, null]);
					}
					else
					{
						list.Add(Code.Ldnull);
					}
					continue;
				case InjectionType.RunOriginal:
					if (config.runOriginalVariable != null)
					{
						list.Add(Code.Ldloc[config.runOriginalVariable, null]);
					}
					else
					{
						list.Add(Code.Ldc_I4_0);
					}
					continue;
				case InjectionType.Instance:
				{
					if (isStatic)
					{
						list.Add(Code.Ldnull);
						continue;
					}
					bool isByRef = parameterType.IsByRef;
					bool flag2 = parameterType == typeof(object) || parameterType == typeof(object).MakeByRefType();
					if (AccessTools.IsStruct(declaringType))
					{
						if (flag2)
						{
							if (isByRef)
							{
								list.Add(Code.Ldarg_0);
								list.Add(Code.Ldobj[declaringType, null]);
								list.Add(Code.Box[declaringType, null]);
								tmpInstanceBoxingVar = config.DeclareLocal(typeof(object));
								list.Add(Code.Stloc[tmpInstanceBoxingVar, null]);
								list.Add(Code.Ldloca[tmpInstanceBoxingVar, null]);
							}
							else
							{
								list.Add(Code.Ldarg_0);
								list.Add(Code.Ldobj[declaringType, null]);
								list.Add(Code.Box[declaringType, null]);
							}
						}
						else if (isByRef)
						{
							list.Add(Code.Ldarg_0);
						}
						else
						{
							list.Add(Code.Ldarg_0);
							list.Add(Code.Ldobj[declaringType, null]);
						}
					}
					else if (isByRef)
					{
						list.Add(Code.Ldarga[0, null]);
					}
					else
					{
						list.Add(Code.Ldarg_0);
					}
					continue;
				}
				case InjectionType.ArgsArray:
				{
					if (config.localVariables.TryGetValue(InjectionType.ArgsArray, out var local))
					{
						list.Add(Code.Ldloc[local, null]);
					}
					else
					{
						list.Add(Code.Ldnull);
					}
					continue;
				}
				}
				if (realName.StartsWith("___", StringComparison.Ordinal))
				{
					string text = realName.Substring("___".Length);
					FieldInfo fieldInfo;
					if (text.All(char.IsDigit))
					{
						fieldInfo = AccessTools.DeclaredField(declaringType, int.Parse(text));
						if ((object)fieldInfo == null)
						{
							throw new ArgumentException("No field found at given index in class " + (declaringType?.AssemblyQualifiedName ?? "null"), text);
						}
					}
					else
					{
						fieldInfo = AccessTools.Field(declaringType, text);
						if ((object)fieldInfo == null)
						{
							throw new ArgumentException("No such field defined in class " + (declaringType?.AssemblyQualifiedName ?? "null"), text);
						}
					}
					if (fieldInfo.IsStatic)
					{
						list.Add(parameterType.IsByRef ? ((CodeMatch)Code.Ldsflda[fieldInfo, null]) : ((CodeMatch)Code.Ldsfld[fieldInfo, null]));
						continue;
					}
					list.Add(Code.Ldarg_0);
					list.Add(parameterType.IsByRef ? ((CodeMatch)Code.Ldflda[fieldInfo, null]) : ((CodeMatch)Code.Ldfld[fieldInfo, null]));
					continue;
				}
				switch (injectionType)
				{
				case InjectionType.State:
				{
					System.Reflection.Emit.OpCode opcode2 = (parameterType.IsByRef ? System.Reflection.Emit.OpCodes.Ldloca : System.Reflection.Emit.OpCodes.Ldloc);
					if (config.localVariables.TryGetValue(patch.DeclaringType?.AssemblyQualifiedName ?? "null", out var local2))
					{
						list.Add(new CodeInstruction(opcode2, local2));
					}
					else
					{
						list.Add(Code.Ldnull);
					}
					continue;
				}
				case InjectionType.Result:
				{
					if (returnType == typeof(void))
					{
						throw new Exception("Cannot get result from void method " + original.FullDescription());
					}
					Type type = parameterType;
					if (type.IsByRef && !returnType.IsByRef)
					{
						type = type.GetElementType();
					}
					if (!type.IsAssignableFrom(returnType))
					{
						throw new Exception($"Cannot assign method return type {returnType.FullName} to {"__result"} type {type.FullName} for method {original.FullDescription()}");
					}
					System.Reflection.Emit.OpCode opcode = ((parameterType.IsByRef && !returnType.IsByRef) ? System.Reflection.Emit.OpCodes.Ldloca : System.Reflection.Emit.OpCodes.Ldloc);
					if (returnType.IsValueType && parameterType == typeof(object).MakeByRefType())
					{
						opcode = System.Reflection.Emit.OpCodes.Ldloc;
					}
					list.Add(new CodeInstruction(opcode, config.GetLocal(InjectionType.Result)));
					if (returnType.IsValueType)
					{
						if (parameterType == typeof(object))
						{
							list.Add(Code.Box[returnType, null]);
						}
						else if (parameterType == typeof(object).MakeByRefType())
						{
							list.Add(Code.Box[returnType, null]);
							tmpObjectVar = config.DeclareLocal(typeof(object));
							list.Add(Code.Stloc[tmpObjectVar, null]);
							list.Add(Code.Ldloca[tmpObjectVar, null]);
						}
					}
					continue;
				}
				case InjectionType.ResultRef:
				{
					if (!returnType.IsByRef)
					{
						throw new Exception($"Cannot use {5} with non-ref return type {returnType.FullName} of method {original.FullDescription()}");
					}
					Type type2 = parameterType;
					Type type3 = typeof(RefResult<>).MakeGenericType(returnType.GetElementType()).MakeByRefType();
					if (type2 != type3)
					{
						throw new Exception($"Wrong type of {"__resultRef"} for method {original.FullDescription()}. Expected {type3.FullName}, got {type2.FullName}");
					}
					list.Add(Code.Ldloca[config.GetLocal(InjectionType.ResultRef), null]);
					refResultUsed = true;
					continue;
				}
				}
				if (config.localVariables.TryGetValue(realName, out var local3))
				{
					System.Reflection.Emit.OpCode opcode3 = (parameterType.IsByRef ? System.Reflection.Emit.OpCodes.Ldloca : System.Reflection.Emit.OpCodes.Ldloc);
					list.Add(new CodeInstruction(opcode3, local3));
					continue;
				}
				int result;
				if (realName.StartsWith("__", StringComparison.Ordinal))
				{
					string s = realName.Substring("__".Length);
					if (!int.TryParse(s, out result))
					{
						throw new Exception("Parameter " + realName + " does not contain a valid index");
					}
					if (result < 0 || result >= parameters.Length)
					{
						throw new Exception($"No parameter found at index {result}");
					}
				}
				else
				{
					result = patch.GetArgumentIndex(originalParameterNames, item.parameterInfo);
					if (result == -1)
					{
						HarmonyMethod mergedFromType = HarmonyMethodExtensions.GetMergedFromType(parameterType);
						HarmonyMethod harmonyMethod = mergedFromType;
						MethodType valueOrDefault = harmonyMethod.methodType.GetValueOrDefault();
						if (!harmonyMethod.methodType.HasValue)
						{
							valueOrDefault = MethodType.Normal;
							harmonyMethod.methodType = valueOrDefault;
						}
						MethodBase originalMethod = mergedFromType.GetOriginalMethod();
						if (originalMethod is MethodInfo methodInfo)
						{
							ConstructorInfo constructor = parameterType.GetConstructor(new Type[2]
							{
								typeof(object),
								typeof(IntPtr)
							});
							if ((object)constructor != null)
							{
								if (methodInfo.IsStatic)
								{
									list.Add(Code.Ldnull);
								}
								else
								{
									list.Add(Code.Ldarg_0);
									if (declaringType != null && declaringType.IsValueType)
									{
										list.Add(Code.Ldobj[declaringType, null]);
										list.Add(Code.Box[declaringType, null]);
									}
								}
								if (!methodInfo.IsStatic && !mergedFromType.nonVirtualDelegate)
								{
									list.Add(Code.Dup);
									list.Add(Code.Ldvirtftn[methodInfo, null]);
								}
								else
								{
									list.Add(Code.Ldftn[methodInfo, null]);
								}
								list.Add(Code.Newobj[constructor, null]);
								continue;
							}
						}
						throw new Exception("Parameter \"" + realName + "\" not found in method " + original.FullDescription());
					}
				}
				Type parameterType2 = parameters[result].ParameterType;
				Type type4 = (parameterType2.IsByRef ? parameterType2.GetElementType() : parameterType2);
				Type type5 = parameterType;
				Type type6 = (type5.IsByRef ? type5.GetElementType() : type5);
				bool flag3 = !parameters[result].IsOut && !parameterType2.IsByRef;
				bool flag4 = !item.parameterInfo.IsOut && !type5.IsByRef;
				bool flag5 = type4.IsValueType && !type6.IsValueType;
				int num = result + (flag ? 1 : 0);
				if (flag3 == flag4)
				{
					list.Add(Code.Ldarg[num, null]);
					if (flag5)
					{
						if (flag4)
						{
							list.Add(Code.Box[type4, null]);
							continue;
						}
						list.Add(Code.Ldobj[type4, null]);
						list.Add(Code.Box[type4, null]);
						LocalBuilder localBuilder = config.DeclareLocal(type6);
						list.Add(Code.Stloc[localBuilder, null]);
						list.Add(Code.Ldloca_S[localBuilder, null]);
						tmpBoxVars.Add(new KeyValuePair<LocalBuilder, Type>(localBuilder, type4));
					}
				}
				else if (flag3 && !flag4)
				{
					if (flag5)
					{
						list.Add(Code.Ldarg[num, null]);
						list.Add(Code.Box[type4, null]);
						LocalBuilder operand = config.DeclareLocal(type6);
						list.Add(Code.Stloc[operand, null]);
						list.Add(Code.Ldloca_S[operand, null]);
					}
					else
					{
						list.Add(Code.Ldarga[num, null]);
					}
				}
				else
				{
					list.Add(Code.Ldarg[num, null]);
					if (flag5)
					{
						list.Add(Code.Ldobj[type4, null]);
						list.Add(Code.Box[type4, null]);
					}
					else if (type4.IsValueType)
					{
						list.Add(Code.Ldobj[type4, null]);
					}
					else
					{
						list.Add(new CodeInstruction(LoadIndOpCodeFor(parameters[result].ParameterType)));
					}
				}
			}
			return list;
		}

		internal static LocalBuilder[] DeclareOriginalLocalVariables(this MethodCreator creator, MethodBase member)
		{
			IList<LocalVariableInfo> list = member.GetMethodBody()?.LocalVariables;
			if (list == null)
			{
				return Array.Empty<LocalBuilder>();
			}
			return list.Select((LocalVariableInfo lvi) => creator.config.il.DeclareLocal(lvi.LocalType, lvi.IsPinned)).ToArray();
		}

		internal static List<CodeInstruction> RestoreArgumentArray(this MethodCreator creator)
		{
			List<CodeInstruction> list = new List<CodeInstruction>();
			MethodBase original = creator.config.original;
			bool isStatic = original.IsStatic;
			ParameterInfo[] parameters = original.GetParameters();
			int num = 0;
			int num2 = 0;
			ParameterInfo[] array = parameters;
			foreach (ParameterInfo parameterInfo in array)
			{
				int num3 = num++ + ((!isStatic) ? 1 : 0);
				Type parameterType = parameterInfo.ParameterType;
				if (parameterType.IsByRef)
				{
					parameterType = parameterType.GetElementType();
					list.Add(Code.Ldarg[num3, null]);
					list.Add(Code.Ldloc[creator.config.GetLocal(InjectionType.ArgsArray), null]);
					list.Add(Code.Ldc_I4[num2, null]);
					list.Add(Code.Ldelem_Ref);
					if (parameterType.IsValueType)
					{
						list.Add(Code.Unbox_Any[parameterType, null]);
						if (AccessTools.IsStruct(parameterType))
						{
							list.Add(Code.Stobj[parameterType, null]);
						}
						else
						{
							list.Add(StoreIndOpCodeFor(parameterType));
						}
					}
					else
					{
						list.Add(Code.Castclass[parameterType, null]);
						list.Add(Code.Stind_Ref);
					}
				}
				else
				{
					list.Add(Code.Ldloc[creator.config.GetLocal(InjectionType.ArgsArray), null]);
					list.Add(Code.Ldc_I4[num2, null]);
					list.Add(Code.Ldelem_Ref);
					if (parameterType.IsValueType)
					{
						list.Add(Code.Unbox_Any[parameterType, null]);
					}
					else
					{
						list.Add(Code.Castclass[parameterType, null]);
					}
					list.Add(Code.Starg[num3, null]);
				}
				num2++;
			}
			return list;
		}

		internal static IEnumerable<CodeInstruction> CleanupCodes(this MethodCreator creator, IEnumerable<CodeInstruction> instructions, List<Label> endLabels)
		{
			foreach (CodeInstruction instruction in instructions)
			{
				System.Reflection.Emit.OpCode opcode = instruction.opcode;
				System.Reflection.Emit.OpCode value;
				if (opcode == System.Reflection.Emit.OpCodes.Ret)
				{
					Label endLabel = creator.config.DefineLabel();
					yield return Code.Br[endLabel, null].WithLabels(instruction.labels).WithBlocks(instruction.blocks);
					endLabels.Add(endLabel);
				}
				else if (shortJumps.TryGetValue(opcode, out value))
				{
					yield return new CodeInstruction(value, instruction.operand).WithLabels(instruction.labels).WithBlocks(instruction.blocks);
				}
				else
				{
					yield return instruction;
				}
			}
		}

		internal static void LogCodes(this MethodCreator _, Emitter emitter, List<CodeInstruction> codeInstructions)
		{
			int codePos = emitter.CurrentPos();
			emitter.Variables().Do(FileLog.LogIL);
			codeInstructions.Do(delegate(CodeInstruction codeInstruction)
			{
				codeInstruction.labels.Do(delegate(Label label)
				{
					FileLog.LogIL(codePos, label);
				});
				codeInstruction.blocks.Do(delegate(ExceptionBlock block)
				{
					FileLog.LogILBlockBegin(codePos, block);
				});
				System.Reflection.Emit.OpCode opcode = codeInstruction.opcode;
				object operand = codeInstruction.operand;
				bool flag = true;
				switch (opcode.OperandType)
				{
				case System.Reflection.Emit.OperandType.InlineNone:
				{
					string text = codeInstruction.IsAnnotation();
					if (text != null)
					{
						FileLog.LogILComment(codePos, text);
						flag = false;
					}
					else
					{
						FileLog.LogIL(codePos, opcode);
					}
					break;
				}
				case System.Reflection.Emit.OperandType.InlineSig:
					FileLog.LogIL(codePos, opcode, (ICallSiteGenerator)operand);
					break;
				default:
					FileLog.LogIL(codePos, opcode, operand);
					break;
				}
				codeInstruction.blocks.Do(delegate(ExceptionBlock block)
				{
					FileLog.LogILBlockEnd(codePos, block);
				});
				if (flag)
				{
					codePos += codeInstruction.GetSize();
				}
			});
			FileLog.FlushBuffer();
		}

		internal static void EmitCodes(this MethodCreator _, Emitter emitter, List<CodeInstruction> codeInstructions)
		{
			codeInstructions.Do(delegate(CodeInstruction codeInstruction)
			{
				codeInstruction.labels.Do(delegate(Label label)
				{
					emitter.MarkLabel(label);
				});
				codeInstruction.blocks.Do(delegate(ExceptionBlock block)
				{
					emitter.MarkBlockBefore(block, out var _);
				});
				System.Reflection.Emit.OpCode opcode = codeInstruction.opcode;
				object operand = codeInstruction.operand;
				switch (opcode.OperandType)
				{
				case System.Reflection.Emit.OperandType.InlineNone:
					if (codeInstruction.IsAnnotation() == null)
					{
						emitter.Emit(opcode);
					}
					break;
				case System.Reflection.Emit.OperandType.InlineSig:
					if (operand == null)
					{
						throw new Exception($"Wrong null argument: {codeInstruction}");
					}
					if (!(operand is ICallSiteGenerator))
					{
						throw new Exception($"Wrong Emit argument type {operand.GetType()} in {codeInstruction}");
					}
					emitter.Emit(opcode, (ICallSiteGenerator)operand);
					break;
				default:
					if (operand == null)
					{
						throw new Exception($"Wrong null argument: {codeInstruction}");
					}
					emitter.DynEmit(opcode, operand);
					break;
				}
				codeInstruction.blocks.Do(delegate(ExceptionBlock block)
				{
					emitter.MarkBlockAfter(block);
				});
			});
		}

		private static List<CodeInstruction> InitializeOutParameter(int argIndex, Type type)
		{
			List<CodeInstruction> list = new List<CodeInstruction>();
			if (type.IsByRef)
			{
				type = type.GetElementType();
			}
			list.Add(Code.Ldarg[argIndex, null]);
			if (AccessTools.IsStruct(type))
			{
				list.Add(Code.Initobj[type, null]);
				return list;
			}
			if (AccessTools.IsValue(type))
			{
				if (type == typeof(float))
				{
					list.Add(Code.Ldc_R4[0f, null]);
					list.Add(Code.Stind_R4);
					return list;
				}
				if (type == typeof(double))
				{
					list.Add(Code.Ldc_R8[0.0, null]);
					list.Add(Code.Stind_R8);
					return list;
				}
				if (type == typeof(long))
				{
					list.Add(Code.Ldc_I8[0L, null]);
					list.Add(Code.Stind_I8);
					return list;
				}
				list.Add(Code.Ldc_I4[0, null]);
				list.Add(Code.Stind_I4);
				return list;
			}
			list.Add(Code.Ldnull);
			list.Add(Code.Stind_Ref);
			return list;
		}

		private static CodeInstruction LoadIndOpCodeFor(Type type)
		{
			if (PrimitivesWithObjectTypeCode.Contains(type))
			{
				return Code.Ldind_I;
			}
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Byte:
				return Code.Ldind_I1;
			case TypeCode.Char:
			case TypeCode.Int16:
			case TypeCode.UInt16:
				return Code.Ldind_I2;
			case TypeCode.Int32:
			case TypeCode.UInt32:
				return Code.Ldind_I4;
			case TypeCode.Int64:
			case TypeCode.UInt64:
				return Code.Ldind_I8;
			case TypeCode.Single:
				return Code.Ldind_R4;
			case TypeCode.Double:
				return Code.Ldind_R8;
			case TypeCode.Decimal:
			case TypeCode.DateTime:
				throw new NotSupportedException();
			case TypeCode.Empty:
			case TypeCode.Object:
			case TypeCode.DBNull:
			case TypeCode.String:
				return Code.Ldind_Ref;
			default:
				return Code.Ldind_Ref;
			}
		}

		private static bool EmitOriginalBaseMethod(MethodBase original, List<CodeInstruction> codes)
		{
			if (original is MethodInfo operand)
			{
				codes.Add(Code.Ldtoken[operand, null]);
			}
			else
			{
				if (!(original is ConstructorInfo operand2))
				{
					return false;
				}
				codes.Add(Code.Ldtoken[operand2, null]);
			}
			Type reflectedType = original.ReflectedType;
			if (reflectedType.IsGenericType)
			{
				codes.Add(Code.Ldtoken[reflectedType, null]);
			}
			codes.Add(Code.Call[reflectedType.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1, null]);
			return true;
		}

		private static CodeInstruction StoreIndOpCodeFor(Type type)
		{
			if (PrimitivesWithObjectTypeCode.Contains(type))
			{
				return Code.Stind_I;
			}
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Byte:
				return Code.Stind_I1;
			case TypeCode.Char:
			case TypeCode.Int16:
			case TypeCode.UInt16:
				return Code.Stind_I2;
			case TypeCode.Int32:
			case TypeCode.UInt32:
				return Code.Stind_I4;
			case TypeCode.Int64:
			case TypeCode.UInt64:
				return Code.Stind_I8;
			case TypeCode.Single:
				return Code.Stind_R4;
			case TypeCode.Double:
				return Code.Stind_R8;
			case TypeCode.Decimal:
			case TypeCode.DateTime:
				throw new NotSupportedException();
			case TypeCode.Empty:
			case TypeCode.Object:
			case TypeCode.DBNull:
			case TypeCode.String:
				return Code.Stind_Ref;
			default:
				return Code.Stind_Ref;
			}
		}
	}
	internal class MethodPatcherTools
	{
		internal const string INSTANCE_PARAM = "__instance";

		internal const string ORIGINAL_METHOD_PARAM = "__originalMethod";

		internal const string ARGS_ARRAY_VAR = "__args";

		internal const string RESULT_VAR = "__result";

		internal const string RESULT_REF_VAR = "__resultRef";

		internal const string STATE_VAR = "__state";

		internal const string EXCEPTION_VAR = "__exception";

		internal const string RUN_ORIGINAL_VAR = "__runOriginal";

		internal const string PARAM_INDEX_PREFIX = "__";

		internal const string INSTANCE_FIELD_PREFIX = "___";

		private static readonly MethodInfo m_GetMethodFromHandle1 = typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[1] { typeof(RuntimeMethodHandle) });

		private static readonly MethodInfo m_GetMethodFromHandle2 = typeof(MethodBase).GetMethod("GetMethodFromHandle", new Type[2]
		{
			typeof(RuntimeMethodHandle),
			typeof(RuntimeTypeHandle)
		});

		private static readonly HashSet<Type> PrimitivesWithObjectTypeCode = new HashSet<Type>
		{
			typeof(IntPtr),
			typeof(UIntPtr),
			typeof(IntPtr),
			typeof(UIntPtr)
		};

		internal static DynamicMethodDefinition CreateDynamicMethod(MethodBase original, string suffix, bool debug)
		{
			if ((object)original == null)
			{
				throw new ArgumentNullException("original");
			}
			string text = (original.DeclaringType?.FullName ?? "GLOBALTYPE") + "." + original.Name + suffix;
			text = text.Replace("<>", "");
			ParameterInfo[] parameters = original.GetParameters();
			List<Type> list = new List<Type>();
			list.AddRange(parameters.Types());
			if (!original.IsStatic)
			{
				if (AccessTools.IsStruct(original.DeclaringType))
				{
					list.Insert(0, original.DeclaringType.MakeByRefType());
				}
				else
				{
					list.Insert(0, original.DeclaringType);
				}
			}
			Type returnedType = AccessTools.GetReturnedType(original);
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition(text, returnedType, list.ToArray());
			int num = ((!original.IsStatic) ? 1 : 0);
			if (!original.IsStatic)
			{
				dynamicMethodDefinition.Definition.Parameters[0].Name = "this";
			}
			for (int i = 0; i < parameters.Length; i++)
			{
				ParameterDefinition parameterDefinition = dynamicMethodDefinition.Definition.Parameters[i + num];
				parameterDefinition.Attributes = (Mono.Cecil.ParameterAttributes)parameters[i].Attributes;
				parameterDefinition.Name = parameters[i].Name;
			}
			if (debug)
			{
				List<string> list2 = list.Select((Type p) => p.FullDescription()).ToList();
				if (list.Count == dynamicMethodDefinition.Definition.Parameters.Count)
				{
					for (int num2 = 0; num2 < list.Count; num2++)
					{
						List<string> list3 = list2;
						int index = num2;
						list3[index] = list3[index] + " " + dynamicMethodDefinition.Definition.Parameters[num2].Name;
					}
				}
				FileLog.Log($"### Replacement: static {returnedType.FullDescription()} {original.DeclaringType?.FullName ?? "GLOBALTYPE"}::{text}({list2.Join()})");
			}
			return dynamicMethodDefinition;
		}

		internal static IEnumerable<(ParameterInfo info, string realName)> OriginalParameters(MethodInfo method)
		{
			IEnumerable<HarmonyArgument> baseArgs = method.GetArgumentAttributes();
			if ((object)method.DeclaringType != null)
			{
				baseArgs = baseArgs.Union(method.DeclaringType.GetArgumentAttributes()).OfType<HarmonyArgument>();
			}
			return method.GetParameters().Select(delegate(ParameterInfo p)
			{
				HarmonyArgument argumentAttribute = p.GetArgumentAttribute();
				return (argumentAttribute != null) ? (p: p, argumentAttribute.OriginalName ?? p.Name) : (p: p, baseArgs.GetRealName(p.Name, null) ?? p.Name);
			});
		}

		internal static Dictionary<string, string> RealNames(MethodInfo method)
		{
			return OriginalParameters(method).ToDictionary(((ParameterInfo info, string realName) pair) => pair.info.Name, ((ParameterInfo info, string realName) pair) => pair.realName);
		}

		internal static LocalBuilder[] DeclareOriginalLocalVariables(ILGenerator il, MethodBase member)
		{
			IList<LocalVariableInfo> list = member.GetMethodBody()?.LocalVariables;
			if (list == null)
			{
				return Array.Empty<LocalBuilder>();
			}
			return list.Select((LocalVariableInfo lvi) => il.DeclareLocal(lvi.LocalType, lvi.IsPinned)).ToArray();
		}

		internal static bool PrefixAffectsOriginal(MethodInfo fix)
		{
			if (fix.ReturnType == typeof(bool))
			{
				return true;
			}
			return OriginalParameters(fix).Any(delegate((ParameterInfo info, string realName) pair)
			{
				ParameterInfo item = pair.info;
				string item2 = pair.realName;
				Type parameterType = item.ParameterType;
				switch (item2)
				{
				case "__instance":
					return false;
				case "__originalMethod":
					return false;
				case "__state":
					return false;
				default:
					if (item.IsOut || item.IsRetval)
					{
						return true;
					}
					if (parameterType.IsByRef)
					{
						return true;
					}
					if (!AccessTools.IsValue(parameterType) && !AccessTools.IsStruct(parameterType))
					{
						return true;
					}
					return false;
				}
			});
		}

		internal static bool EmitOriginalBaseMethod(MethodBase original, Emitter emitter)
		{
			if (original is MethodInfo meth)
			{
				emitter.Emit(System.Reflection.Emit.OpCodes.Ldtoken, meth);
			}
			else
			{
				if (!(original is ConstructorInfo con))
				{
					return false;
				}
				emitter.Emit(System.Reflection.Emit.OpCodes.Ldtoken, con);
			}
			Type reflectedType = original.ReflectedType;
			if (reflectedType.IsGenericType)
			{
				emitter.Emit(System.Reflection.Emit.OpCodes.Ldtoken, reflectedType);
			}
			emitter.Emit(System.Reflection.Emit.OpCodes.Call, reflectedType.IsGenericType ? m_GetMethodFromHandle2 : m_GetMethodFromHandle1);
			return true;
		}

		internal static System.Reflection.Emit.OpCode LoadIndOpCodeFor(Type type)
		{
			if (PrimitivesWithObjectTypeCode.Contains(type))
			{
				return System.Reflection.Emit.OpCodes.Ldind_I;
			}
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Byte:
				return System.Reflection.Emit.OpCodes.Ldind_I1;
			case TypeCode.Char:
			case TypeCode.Int16:
			case TypeCode.UInt16:
				return System.Reflection.Emit.OpCodes.Ldind_I2;
			case TypeCode.Int32:
			case TypeCode.UInt32:
				return System.Reflection.Emit.OpCodes.Ldind_I4;
			case TypeCode.Int64:
			case TypeCode.UInt64:
				return System.Reflection.Emit.OpCodes.Ldind_I8;
			case TypeCode.Single:
				return System.Reflection.Emit.OpCodes.Ldind_R4;
			case TypeCode.Double:
				return System.Reflection.Emit.OpCodes.Ldind_R8;
			case TypeCode.Decimal:
			case TypeCode.DateTime:
				throw new NotSupportedException();
			case TypeCode.Empty:
			case TypeCode.Object:
			case TypeCode.DBNull:
			case TypeCode.String:
				return System.Reflection.Emit.OpCodes.Ldind_Ref;
			default:
				return System.Reflection.Emit.OpCodes.Ldind_Ref;
			}
		}

		internal static System.Reflection.Emit.OpCode StoreIndOpCodeFor(Type type)
		{
			if (PrimitivesWithObjectTypeCode.Contains(type))
			{
				return System.Reflection.Emit.OpCodes.Stind_I;
			}
			switch (Type.GetTypeCode(type))
			{
			case TypeCode.Boolean:
			case TypeCode.SByte:
			case TypeCode.Byte:
				return System.Reflection.Emit.OpCodes.Stind_I1;
			case TypeCode.Char:
			case TypeCode.Int16:
			case TypeCode.UInt16:
				return System.Reflection.Emit.OpCodes.Stind_I2;
			case TypeCode.Int32:
			case TypeCode.UInt32:
				return System.Reflection.Emit.OpCodes.Stind_I4;
			case TypeCode.Int64:
			case TypeCode.UInt64:
				return System.Reflection.Emit.OpCodes.Stind_I8;
			case TypeCode.Single:
				return System.Reflection.Emit.OpCodes.Stind_R4;
			case TypeCode.Double:
				return System.Reflection.Emit.OpCodes.Stind_R8;
			case TypeCode.Decimal:
			case TypeCode.DateTime:
				throw new NotSupportedException();
			case TypeCode.Empty:
			case TypeCode.Object:
			case TypeCode.DBNull:
			case TypeCode.String:
				return System.Reflection.Emit.OpCodes.Stind_Ref;
			default:
				return System.Reflection.Emit.OpCodes.Stind_Ref;
			}
		}
	}
	internal static class PatchArgumentExtensions
	{
		private static IEnumerable<HarmonyArgument> AllHarmonyArguments(object[] attributes)
		{
			return attributes.Select((object attr) => (attr.GetType().Name != "HarmonyArgument") ? null : AccessTools.MakeDeepCopy<HarmonyArgument>(attr)).OfType<HarmonyArgument>();
		}

		internal static HarmonyArgument GetArgumentAttribute(this ParameterInfo parameter)
		{
			try
			{
				object[] customAttributes = parameter.GetCustomAttributes(inherit: true);
				return AllHarmonyArguments(customAttributes).FirstOrDefault();
			}
			catch (NotSupportedException)
			{
				return null;
			}
		}

		internal static IEnumerable<HarmonyArgument> GetArgumentAttributes(this MethodInfo method)
		{
			try
			{
				object[] customAttributes = method.GetCustomAttributes(inherit: true);
				return AllHarmonyArguments(customAttributes);
			}
			catch (NotSupportedException)
			{
				return Array.Empty<HarmonyArgument>();
			}
		}

		internal static IEnumerable<HarmonyArgument> GetArgumentAttributes(this Type type)
		{
			try
			{
				object[] customAttributes = type.GetCustomAttributes(inherit: true);
				return AllHarmonyArguments(customAttributes);
			}
			catch (NotSupportedException)
			{
				return Array.Empty<HarmonyArgument>();
			}
		}

		internal static string GetRealName(this IEnumerable<HarmonyArgument> attributes, string name, string[] originalParameterNames)
		{
			HarmonyArgument harmonyArgument = attributes.FirstOrDefault((HarmonyArgument p) => p.OriginalName == name);
			if (harmonyArgument == null)
			{
				return null;
			}
			if (!string.IsNullOrEmpty(harmonyArgument.NewName))
			{
				return harmonyArgument.NewName;
			}
			if (originalParameterNames != null && harmonyArgument.Index >= 0 && harmonyArgument.Index < originalParameterNames.Length)
			{
				return originalParameterNames[harmonyArgument.Index];
			}
			return null;
		}

		private static string GetRealParameterName(this MethodInfo method, string[] originalParameterNames, string name)
		{
			if ((object)method == null || method is DynamicMethod)
			{
				return name;
			}
			string realName = method.GetArgumentAttributes().GetRealName(name, originalParameterNames);
			if (realName != null)
			{
				return realName;
			}
			Type declaringType = method.DeclaringType;
			if ((object)declaringType != null)
			{
				realName = declaringType.GetArgumentAttributes().GetRealName(name, originalParameterNames);
				if (realName != null)
				{
					return realName;
				}
			}
			return name;
		}

		private static string GetRealParameterName(this ParameterInfo parameter, string[] originalParameterNames)
		{
			HarmonyArgument argumentAttribute = parameter.GetArgumentAttribute();
			if (argumentAttribute == null)
			{
				return null;
			}
			if (!string.IsNullOrEmpty(argumentAttribute.OriginalName))
			{
				return argumentAttribute.OriginalName;
			}
			if (argumentAttribute.Index >= 0 && argumentAttribute.Index < originalParameterNames.Length)
			{
				return originalParameterNames[argumentAttribute.Index];
			}
			return null;
		}

		internal static int GetArgumentIndex(this MethodInfo patch, string[] originalParameterNames, ParameterInfo patchParam)
		{
			if (patch is DynamicMethod)
			{
				return Array.IndexOf(originalParameterNames, patchParam.Name);
			}
			string realParameterName = patchParam.GetRealParameterName(originalParameterNames);
			if (realParameterName != null)
			{
				return Array.IndexOf(originalParameterNames, realParameterName);
			}
			realParameterName = patch.GetRealParameterName(originalParameterNames, patchParam.Name);
			if (realParameterName != null)
			{
				return Array.IndexOf(originalParameterNames, realParameterName);
			}
			return -1;
		}
	}
	internal static class PatchFunctions
	{
		internal static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches, bool debug)
		{
			return (from p in new PatchSorter(patches, debug).Sort()
				select p.GetMethod(original)).ToList();
		}

		private static List<Infix> GetInfixes(Patch[] patches)
		{
			return patches.Select((Patch p) => new Infix(p)).ToList();
		}

		internal static MethodInfo UpdateWrapper(MethodBase original, PatchInfo patchInfo)
		{
			bool debug = patchInfo.Debugging || Harmony.DEBUG;
			List<MethodInfo> sortedPatchMethods = GetSortedPatchMethods(original, patchInfo.prefixes, debug);
			List<MethodInfo> sortedPatchMethods2 = GetSortedPatchMethods(original, patchInfo.postfixes, debug);
			List<MethodInfo> sortedPatchMethods3 = GetSortedPatchMethods(original, patchInfo.transpilers, debug);
			List<MethodInfo> sortedPatchMethods4 = GetSortedPatchMethods(original, patchInfo.finalizers, debug);
			List<Infix> infixes = GetInfixes(patchInfo.innerprefixes);
			List<Infix> infixes2 = GetInfixes(patchInfo.innerpostfixes);
			MethodCreator methodCreator = new MethodCreator(new MethodCreatorConfig(original, null, sortedPatchMethods, sortedPatchMethods2, sortedPatchMethods3, sortedPatchMethods4, infixes, infixes2, debug));
			var (methodInfo, finalInstructions) = methodCreator.CreateReplacement();
			if ((object)methodInfo == null)
			{
				throw new MissingMethodException("Cannot create replacement for " + original.FullDescription());
			}
			try
			{
				PatchTools.DetourMethod(original, methodInfo);
				return methodInfo;
			}
			catch (Exception ex)
			{
				throw HarmonyException.Create(ex, finalInstructions);
			}
		}

		internal static MethodInfo ReversePatch(HarmonyMethod standin, MethodBase original, MethodInfo postTranspiler)
		{
			if (standin == null)
			{
				throw new ArgumentNullException("standin");
			}
			if ((object)standin.method == null)
			{
				throw new ArgumentNullException("standin", "standin.method is NULL");
			}
			bool debug = standin.debug == true || Harmony.DEBUG;
			List<MethodInfo> list = new List<MethodInfo>();
			if (standin.reversePatchType == HarmonyReversePatchType.Snapshot)
			{
				Patches patchInfo = Harmony.GetPatchInfo(original);
				list.AddRange(GetSortedPatchMethods(original, patchInfo.Transpilers.ToArray(), debug));
			}
			if ((object)postTranspiler != null)
			{
				list.Add(postTranspiler);
			}
			List<MethodInfo> list2 = new List<MethodInfo>();
			List<Infix> list3 = new List<Infix>();
			MethodCreator methodCreator = new MethodCreator(new MethodCreatorConfig(standin.method, original, list2, list2, list, list2, list3, list3, debug));
			var (methodInfo, finalInstructions) = methodCreator.CreateReplacement();
			if ((object)methodInfo == null)
			{
				throw new MissingMethodException("Cannot create replacement for " + standin.method.FullDescription());
			}
			try
			{
				PatchTools.DetourMethod(standin.method, methodInfo);
				return methodInfo;
			}
			catch (Exception ex)
			{
				throw HarmonyException.Create(ex, finalInstructions);
			}
		}
	}
	internal class PatchJobs<T>
	{
		internal class Job
		{
			internal MethodBase original;

			internal T replacement;

			internal List<HarmonyMethod> prefixes = new List<HarmonyMethod>();

			internal List<HarmonyMethod> postfixes = new List<HarmonyMethod>();

			internal List<HarmonyMethod> transpilers = new List<HarmonyMethod>();

			internal List<HarmonyMethod> finalizers = new List<HarmonyMethod>();

			internal List<HarmonyMethod> innerprefixes = new List<HarmonyMethod>();

			internal List<HarmonyMethod> innerpostfixes = new List<HarmonyMethod>();

			internal void AddPatch(AttributePatch patch)
			{
				HarmonyPatchType? type = patch.type;
				if (type.HasValue)
				{
					switch (type.GetValueOrDefault())
					{
					case HarmonyPatchType.Prefix:
						prefixes.Add(patch.info);
						break;
					case HarmonyPatchType.Postfix:
						postfixes.Add(patch.info);
						break;
					case HarmonyPatchType.Transpiler:
						transpilers.Add(patch.info);
						break;
					case HarmonyPatchType.Finalizer:
						finalizers.Add(patch.info);
						break;
					case HarmonyPatchType.InnerPrefix:
						innerprefixes.Add(patch.info);
						break;
					case HarmonyPatchType.InnerPostfix:
						innerpostfixes.Add(patch.info);
						break;
					case HarmonyPatchType.ReversePatch:
						break;
					}
				}
			}
		}

		internal Dictionary<MethodBase, Job> state = new Dictionary<MethodBase, Job>();

		internal Job GetJob(MethodBase method)
		{
			if ((object)method == null)
			{
				return null;
			}
			if (!state.TryGetValue(method, out var value))
			{
				value = new Job
				{
					original = method
				};
				state[method] = value;
			}
			return value;
		}

		internal List<Job> GetJobs()
		{
			return state.Values.Where((Job job) => job.prefixes.Count + job.postfixes.Count + job.transpilers.Count + job.finalizers.Count + job.innerprefixes.Count + job.innerpostfixes.Count > 0).ToList();
		}

		internal List<T> GetReplacements()
		{
			return state.Values.Select((Job job) => job.replacement).ToList();
		}
	}
	internal class AttributePatch
	{
		private static readonly HarmonyPatchType[] allPatchTypes = new HarmonyPatchType[7]
		{
			HarmonyPatchType.Prefix,
			HarmonyPatchType.Postfix,
			HarmonyPatchType.Transpiler,
			HarmonyPatchType.Finalizer,
			HarmonyPatchType.ReversePatch,
			HarmonyPatchType.InnerPrefix,
			HarmonyPatchType.InnerPostfix
		};

		internal HarmonyMethod info;

		internal HarmonyPatchType? type;

		internal static AttributePatch Create(MethodInfo patch)
		{
			if ((object)patch == null)
			{
				throw new NullReferenceException("Patch method cannot be null");
			}
			object[] customAttributes = patch.GetCustomAttributes(inherit: true);
			string name = patch.Name;
			HarmonyPatchType? patchType = GetPatchType(name, customAttributes);
			if (!patchType.HasValue)
			{
				return null;
			}
			if (patchType != HarmonyPatchType.ReversePatch && !patch.IsStatic)
			{
				throw new ArgumentException("Patch method " + patch.FullDescription() + " must be static");
			}
			List<HarmonyMethod> attributes = customAttributes.Where((object attr) => attr.GetType().BaseType.FullName == PatchTools.harmonyAttributeFullName).Select(delegate(object attr)
			{
				FieldInfo fieldInfo = AccessTools.Field(attr.GetType(), "info");
				return fieldInfo.GetValue(attr);
			}).Select(AccessTools.MakeDeepCopy<HarmonyMethod>)
				.ToList();
			HarmonyMethod harmonyMethod = HarmonyMethod.Merge(attributes);
			harmonyMethod.method = patch;
			return new AttributePatch
			{
				info = harmonyMethod,
				type = patchType
			};
		}

		private static HarmonyPatchType? GetPatchType(string methodName, object[] allAttributes)
		{
			HashSet<string> hashSet = new HashSet<string>(from attr in allAttributes
				select attr.GetType().FullName into name
				where name.StartsWith("Harmony")
				select name);
			HarmonyPatchType? result = null;
			HarmonyPatchType[] array = allPatchTypes;
			for (int num = 0; num < array.Length; num++)
			{
				HarmonyPatchType value = array[num];
				string text = value.ToString();
				if (text == methodName || hashSet.Contains("HarmonyLib.Harmony" + text))
				{
					result = value;
					break;
				}
			}
			return result;
		}
	}
	internal class PatchSorter
	{
		internal class PatchSortingWrapper : IComparable
		{
			internal readonly HashSet<PatchSortingWrapper> after;

			internal readonly HashSet<PatchSortingWrapper> before;

			internal readonly Patch innerPatch;

			internal PatchSortingWrapper(Patch patch)
			{
				innerPatch = patch;
				before = new HashSet<PatchSortingWrapper>();
				after = new HashSet<PatchSortingWrapper>();
			}

			public int CompareTo(object obj)
			{
				return PatchInfoSerialization.PriorityComparer((obj is PatchSortingWrapper patchSortingWrapper) ? patchSortingWrapper.innerPatch : null, innerPatch.index, innerPatch.priority);
			}

			public override bool Equals(object obj)
			{
				if (obj is PatchSortingWrapper patchSortingWrapper)
				{
					return innerPatch.PatchMethod == patchSortingWrapper.innerPatch.PatchMethod;
				}
				return false;
			}

			public override int GetHashCode()
			{
				return innerPatch.PatchMethod.GetHashCode();
			}

			internal void AddBeforeDependency(IEnumerable<PatchSortingWrapper> dependencies)
			{
				foreach (PatchSortingWrapper dependency in dependencies)
				{
					before.Add(dependency);
					dependency.after.Add(this);
				}
			}

			internal void AddAfterDependency(IEnumerable<PatchSortingWrapper> dependencies)
			{
				foreach (PatchSortingWrapper dependency in dependencies)
				{
					after.Add(dependency);
					dependency.before.Add(this);
				}
			}

			internal void RemoveAfterDependency(PatchSortingWrapper afterNode)
			{
				after.Remove(afterNode);
				afterNode.before.Remove(this);
			}

			private void RemoveBeforeDependency(PatchSortingWrapper beforeNode)
			{
				before.Remove(beforeNode);
				beforeNode.after.Remove(this);
			}
		}

		private class PatchDetailedComparer : IEqualityComparer<Patch>
		{
			public bool Equals(Patch x, Patch y)
			{
				if (y != null && x != null && x.owner == y.owner && x.PatchMethod == y.PatchMethod && x.index == y.index && x.priority == y.priority && x.before.Length == y.before.Length && x.after.Length == y.after.Length && x.before.All(((IEnumerable<string>)y.before).Contains<string>))
				{
					return x.after.All(((IEnumerable<string>)y.after).Contains<string>);
				}
				return false;
			}

			public int GetHashCode(Patch obj)
			{
				return obj.GetHashCode();
			}
		}

		private List<PatchSortingWrapper> patches;

		private HashSet<PatchSortingWrapper> handledPatches;

		private List<PatchSortingWrapper> result;

		private List<PatchSortingWrapper> waitingList;

		private Patch[] sortedPatchArray;

		private readonly bool debug;

		internal PatchSorter(Patch[] patches, bool debug)
		{
			this.patches = patches.Select((Patch x) => new PatchSortingWrapper(x)).ToList();
			this.debug = debug;
			foreach (PatchSortingWrapper node in this.patches)
			{
				node.AddBeforeDependency(this.patches.Where((PatchSortingWrapper x) => node.innerPatch.before.Contains(x.innerPatch.owner)));
				node.AddAfterDependency(this.patches.Where((PatchSortingWrapper x) => node.innerPatch.after.Contains(x.innerPatch.owner)));
			}
			this.patches.Sort();
		}

		internal Patch[] Sort()
		{
			if (sortedPatchArray != null)
			{
				return sortedPatchArray;
			}
			handledPatches = new HashSet<PatchSortingWrapper>();
			waitingList = new List<PatchSortingWrapper>();
			result = new List<PatchSortingWrapper>(patches.Count);
			Queue<PatchSortingWrapper> queue = new Queue<PatchSortingWrapper>(patches);
			while (queue.Count != 0)
			{
				foreach (PatchSortingWrapper item in queue)
				{
					if (item.after.All((PatchSortingWrapper x) => handledPatches.Contains(x)))
					{
						AddNodeToResult(item);
						if (item.before.Count != 0)
						{
							ProcessWaitingList();
						}
					}
					else
					{
						waitingList.Add(item);
					}
				}
				CullDependency();
				queue = new Queue<PatchSortingWrapper>(waitingList);
				waitingList.Clear();
			}
			sortedPatchArray = result.Select((PatchSortingWrapper x) => x.innerPatch).ToArray();
			handledPatches = null;
			waitingList = null;
			patches = null;
			return sortedPatchArray;
		}

		internal bool ComparePatchLists(Patch[] patches)
		{
			if (sortedPatchArray == null)
			{
				Sort();
			}
			if (patches != null && sortedPatchArray.Length == patches.Length)
			{
				return sortedPatchArray.All((Patch x) => patches.Contains(x, new PatchDetailedComparer()));
			}
			return false;
		}

		private void CullDependency()
		{
			for (int num = waitingList.Count - 1; num >= 0; num--)
			{
				foreach (PatchSortingWrapper item in waitingList[num].after)
				{
					if (!handledPatches.Contains(item))
					{
						waitingList[num].RemoveAfterDependency(item);
						if (debug)
						{
							string text = item.innerPatch.PatchMethod.FullDescription();
							string text2 = waitingList[num].innerPatch.PatchMethod.FullDescription();
							FileLog.LogBuffered("Breaking dependance between " + text + " and " + text2);
						}
						return;
					}
				}
			}
		}

		private void ProcessWaitingList()
		{
			int num = waitingList.Count;
			int num2 = 0;
			while (num2 < num)
			{
				PatchSortingWrapper patchSortingWrapper = waitingList[num2];
				if (patchSortingWrapper.after.All(handledPatches.Contains))
				{
					waitingList.Remove(patchSortingWrapper);
					AddNodeToResult(patchSortingWrapper);
					num--;
					num2 = 0;
				}
				else
				{
					num2++;
				}
			}
		}

		internal void AddNodeToResult(PatchSortingWrapper node)
		{
			result.Add(node);
			handledPatches.Add(node);
		}
	}
	internal static class PatchTools
	{
		private static readonly Dictionary<MethodBase, ICoreDetour> detours = new Dictionary<MethodBase, ICoreDetour>();

		internal static readonly string harmonyMethodFullName = typeof(HarmonyMethod).FullName;

		internal static readonly string harmonyAttributeFullName = typeof(HarmonyAttribute).FullName;

		internal static readonly string harmonyPatchAllFullName = typeof(HarmonyPatchAll).FullName;

		internal static readonly MethodInfo m_GetExecutingAssemblyReplacementTranspiler = SymbolExtensions.GetMethodInfo(() => GetExecutingAssemblyTranspiler(null));

		internal static readonly MethodInfo m_GetExecutingAssembly = SymbolExtensions.GetMethodInfo(() => Assembly.GetExecutingAssembly());

		internal static readonly MethodInfo m_GetExecutingAssemblyReplacement = SymbolExtensions.GetMethodInfo(() => GetExecutingAssemblyReplacement());

		internal static void DetourMethod(MethodBase method, MethodBase replacement)
		{
			lock (detours)
			{
				if (detours.TryGetValue(method, out var value))
				{
					value.Dispose();
				}
				detours[method] = DetourFactory.Current.CreateDetour(method, replacement);
			}
		}

		private static Assembly GetExecutingAssemblyReplacement()
		{
			StackFrame stackFrame = new StackTrace().GetFrames()?.Skip(1).FirstOrDefault();
			if (stackFrame != null)
			{
				MethodBase methodFromStackframe = Harmony.GetMethodFromStackframe(stackFrame);
				if ((object)methodFromStackframe != null)
				{
					return methodFromStackframe.Module.Assembly;
				}
			}
			return Assembly.GetExecutingAssembly();
		}

		internal static IEnumerable<CodeInstruction> GetExecutingAssemblyTranspiler(IEnumerable<CodeInstruction> instructions)
		{
			return instructions.MethodReplacer(m_GetExecutingAssembly, m_GetExecutingAssemblyReplacement);
		}

		public static MethodInfo CreateMethod(string name, Type returnType, List<KeyValuePair<string, Type>> parameters, Action<ILGenerator> generator)
		{
			Type[] parameterTypes = parameters.Select((KeyValuePair<string, Type> p) => p.Value).ToArray();
			if (AccessTools.IsMonoRuntime && !Tools.isWindows)
			{
				AssemblyName name2 = new AssemblyName("TempAssembly");
				AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(name2, AssemblyBuilderAccess.Run);
				ModuleBuilder moduleBuilder = assemblyBuilder.DefineDynamicModule("TempModule");
				TypeBuilder typeBuilder = moduleBuilder.DefineType("TempType", System.Reflection.TypeAttributes.Public);
				MethodBuilder methodBuilder = typeBuilder.DefineMethod(name, System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.Static, returnType, parameterTypes);
				for (int num = 0; num < parameters.Count; num++)
				{
					methodBuilder.DefineParameter(num + 1, System.Reflection.ParameterAttributes.None, parameters[num].Key);
				}
				generator(methodBuilder.GetILGenerator());
				Type type = typeBuilder.CreateType();
				return type.GetMethod(name, BindingFlags.Static | BindingFlags.Public);
			}
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition(name, returnType, parameterTypes);
			for (int num2 = 0; num2 < parameters.Count; num2++)
			{
				dynamicMethodDefinition.Definition.Parameters[num2].Name = parameters[num2].Key;
			}
			generator(dynamicMethodDefinition.GetILGenerator());
			return dynamicMethodDefinition.Generate();
		}

		internal static MethodInfo GetPatchMethod(Type patchType, string attributeName)
		{
			MethodInfo methodInfo = patchType.GetMethods(AccessTools.all).FirstOrDefault((MethodInfo m) => m.GetCustomAttributes(inherit: true).Any((object a) => a.GetType().FullName == attributeName));
			if ((object)methodInfo == null)
			{
				string name = attributeName.Replace("HarmonyLib.Harmony", "");
				methodInfo = patchType.GetMethod(name, AccessTools.all);
			}
			return methodInfo;
		}

		internal static AssemblyBuilder DefineDynamicAssembly(string name)
		{
			AssemblyName name2 = new AssemblyName(name);
			return AppDomain.CurrentDomain.DefineDynamicAssembly(name2, AssemblyBuilderAccess.Run);
		}

		internal static List<AttributePatch> GetPatchMethods(Type type)
		{
			return (from attributePatch in AccessTools.GetDeclaredMethods(type).Select(AttributePatch.Create)
				where attributePatch != null
				select attributePatch).ToList();
		}

		internal static MethodBase GetOriginalMethod(this HarmonyMethod attr)
		{
			try
			{
				MethodType? methodType = attr.methodType;
				if (methodType.HasValue)
				{
					switch (methodType.GetValueOrDefault())
					{
					case MethodType.Normal:
						if (string.IsNullOrEmpty(attr.methodName))
						{
							return null;
						}
						return AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes);
					case MethodType.Getter:
						if (string.IsNullOrEmpty(attr.methodName))
						{
							return AccessTools.DeclaredIndexerGetter(attr.declaringType, attr.argumentTypes);
						}
						return AccessTools.DeclaredPropertyGetter(attr.declaringType, attr.methodName);
					case MethodType.Setter:
						if (string.IsNullOrEmpty(attr.methodName))
						{
							return AccessTools.DeclaredIndexerSetter(attr.declaringType, attr.argumentTypes);
						}
						return AccessTools.DeclaredPropertySetter(attr.declaringType, attr.methodName);
					case MethodType.Constructor:
						return AccessTools.DeclaredConstructor(attr.declaringType, attr.argumentTypes);
					case MethodType.StaticConstructor:
						return (from c in AccessTools.GetDeclaredConstructors(attr.declaringType)
							where c.IsStatic
							select c).FirstOrDefault();
					case MethodType.Enumerator:
					{
						if (string.IsNullOrEmpty(attr.methodName))
						{
							return null;
						}
						MethodInfo method = AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes);
						return AccessTools.EnumeratorMoveNext(method);
					}
					case MethodType.Async:
					{
						if (string.IsNullOrEmpty(attr.methodName))
						{
							return null;
						}
						MethodInfo method2 = AccessTools.DeclaredMethod(attr.declaringType, attr.methodName, attr.argumentTypes);
						return AccessTools.AsyncMoveNext(method2);
					}
					case MethodType.Finalizer:
						return AccessTools.DeclaredFinalizer(attr.declaringType);
					case MethodType.EventAdd:
						if (string.IsNullOrEmpty(attr.methodName))
						{
							return null;
						}
						return AccessTools.DeclaredEventAdder(attr.declaringType, attr.methodName);
					case MethodType.EventRemove:
						if (string.IsNullOrEmpty(attr.methodName))
						{
							return null;
						}
						return AccessTools.DeclaredEventRemover(attr.declaringType, attr.methodName);
					case MethodType.OperatorImplicit:
					case MethodType.OperatorExplicit:
					case MethodType.OperatorUnaryPlus:
					case MethodType.OperatorUnaryNegation:
					case MethodType.OperatorLogicalNot:
					case MethodType.OperatorOnesComplement:
					case MethodType.OperatorIncrement:
					case MethodType.OperatorDecrement:
					case MethodType.OperatorTrue:
					case MethodType.OperatorFalse:
					case MethodType.OperatorAddition:
					case MethodType.OperatorSubtraction:
					case MethodType.OperatorMultiply:
					case MethodType.OperatorDivision:
					case MethodType.OperatorModulus:
					case MethodType.OperatorBitwiseAnd:
					case MethodType.OperatorBitwiseOr:
					case MethodType.OperatorExclusiveOr:
					case MethodType.OperatorLeftShift:
					case MethodType.OperatorRightShift:
					case MethodType.OperatorEquality:
					case MethodType.OperatorInequality:
					case MethodType.OperatorGreaterThan:
					case MethodType.OperatorLessThan:
					case MethodType.OperatorGreaterThanOrEqual:
					case MethodType.OperatorLessThanOrEqual:
					case MethodType.OperatorComma:
					{
						string name = "op_" + attr.methodType.ToString().Replace("Operator", "");
						return AccessTools.DeclaredMethod(attr.declaringType, name, attr.argumentTypes);
					}
					}
				}
			}
			catch (AmbiguousMatchException ex)
			{
				throw new HarmonyException("Ambiguous match for HarmonyMethod[" + attr.Description() + "]", ex.InnerException ?? ex);
			}
			return null;
		}
	}
	internal class VariableState
	{
		private readonly Dictionary<InjectionType, LocalBuilder> injected = new Dictionary<InjectionType, LocalBuilder>();

		private readonly Dictionary<string, LocalBuilder> other = new Dictionary<string, LocalBuilder>();

		public LocalBuilder this[InjectionType type]
		{
			get
			{
				if (injected.TryGetValue(type, out var value))
				{
					return value;
				}
				throw new ArgumentException($"VariableState: variable of type {type} not found");
			}
			set
			{
				injected[type] = value;
			}
		}

		public LocalBuilder this[string name]
		{
			get
			{
				if (other.TryGetValue(name, out var value))
				{
					return value;
				}
				throw new ArgumentException("VariableState: variable named '" + name + "' not found");
			}
			set
			{
				other[name] = value;
			}
		}

		public void Add(InjectionType type, LocalBuilder local)
		{
			injected[type] = local;
		}

		public void Add(string name, LocalBuilder local)
		{
			other[name] = local;
		}

		public bool TryGetValue(InjectionType type, out LocalBuilder local)
		{
			return injected.TryGetValue(type, out local);
		}

		public bool TryGetValue(string name, out LocalBuilder local)
		{
			return other.TryGetValue(name, out local);
		}
	}
	public enum MethodType
	{
		Normal,
		Getter,
		Setter,
		Constructor,
		StaticConstructor,
		Enumerator,
		Async,
		Finalizer,
		EventAdd,
		EventRemove,
		OperatorImplicit,
		OperatorExplicit,
		OperatorUnaryPlus,
		OperatorUnaryNegation,
		OperatorLogicalNot,
		OperatorOnesComplement,
		OperatorIncrement,
		OperatorDecrement,
		OperatorTrue,
		OperatorFalse,
		OperatorAddition,
		OperatorSubtraction,
		OperatorMultiply,
		OperatorDivision,
		OperatorModulus,
		OperatorBitwiseAnd,
		OperatorBitwiseOr,
		OperatorExclusiveOr,
		OperatorLeftShift,
		OperatorRightShift,
		OperatorEquality,
		OperatorInequality,
		OperatorGreaterThan,
		OperatorLessThan,
		OperatorGreaterThanOrEqual,
		OperatorLessThanOrEqual,
		OperatorComma
	}
	public enum ArgumentType
	{
		Normal,
		Ref,
		Out,
		Pointer
	}
	public enum HarmonyPatchType
	{
		All,
		Prefix,
		Postfix,
		Transpiler,
		Finalizer,
		ReversePatch,
		InnerPrefix,
		InnerPostfix
	}
	public enum HarmonyReversePatchType
	{
		Original,
		Snapshot
	}
	public enum MethodDispatchType
	{
		VirtualCall,
		Call
	}
	public class HarmonyAttribute : Attribute
	{
		public HarmonyMethod info = new HarmonyMethod();
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
	public class HarmonyPatchCategory : HarmonyAttribute
	{
		public HarmonyPatchCategory(string category)
		{
			info.category = category;
		}
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Delegate, AllowMultiple = true)]
	public class HarmonyPatch : HarmonyAttribute
	{
		public HarmonyPatch()
		{
		}

		public HarmonyPatch(Type declaringType)
		{
			info.declaringType = declaringType;
		}

		public HarmonyPatch(Type declaringType, Type[] argumentTypes)
		{
			info.declaringType = declaringType;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type declaringType, string methodName)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
		}

		public HarmonyPatch(Type declaringType, string methodName, params Type[] argumentTypes)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(Type declaringType, MethodType methodType)
		{
			info.declaringType = declaringType;
			info.methodType = methodType;
		}

		public HarmonyPatch(Type declaringType, MethodType methodType, params Type[] argumentTypes)
		{
			info.declaringType = declaringType;
			info.methodType = methodType;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type declaringType, MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.declaringType = declaringType;
			info.methodType = methodType;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(Type declaringType, string methodName, MethodType methodType)
		{
			info.declaringType = declaringType;
			info.methodName = methodName;
			info.methodType = methodType;
		}

		public HarmonyPatch(string methodName)
		{
			info.methodName = methodName;
		}

		public HarmonyPatch(string methodName, params Type[] argumentTypes)
		{
			info.methodName = methodName;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.methodName = methodName;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(string methodName, MethodType methodType)
		{
			info.methodName = methodName;
			info.methodType = methodType;
		}

		public HarmonyPatch(MethodType methodType)
		{
			info.methodType = methodType;
		}

		public HarmonyPatch(MethodType methodType, params Type[] argumentTypes)
		{
			info.methodType = methodType;
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			info.methodType = methodType;
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(Type[] argumentTypes)
		{
			info.argumentTypes = argumentTypes;
		}

		public HarmonyPatch(Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			ParseSpecialArguments(argumentTypes, argumentVariations);
		}

		public HarmonyPatch(string typeName, string methodName, MethodType methodType = MethodType.Normal)
		{
			info.declaringType = AccessTools.TypeByName(typeName);
			info.methodName = methodName;
			info.methodType = methodType;
		}

		private void ParseSpecialArguments(Type[] argumentTypes, ArgumentType[] argumentVariations)
		{
			if (argumentVariations == null || argumentVariations.Length == 0)
			{
				info.argumentTypes = argumentTypes;
				return;
			}
			if (argumentTypes.Length < argumentVariations.Length)
			{
				throw new ArgumentException("argumentVariations contains more elements than argumentTypes", "argumentVariations");
			}
			List<Type> list = new List<Type>();
			for (int i = 0; i < argumentTypes.Length; i++)
			{
				Type type = argumentTypes[i];
				switch (argumentVariations[i])
				{
				case ArgumentType.Ref:
				case ArgumentType.Out:
					type = type.MakeByRefType();
					break;
				case ArgumentType.Pointer:
					type = type.MakePointerType();
					break;
				}
				list.Add(type);
			}
			info.argumentTypes = list.ToArray();
		}
	}
	[AttributeUsage(AttributeTargets.Delegate, AllowMultiple = true)]
	public class HarmonyDelegate : HarmonyPatch
	{
		public HarmonyDelegate(Type declaringType)
			: base(declaringType)
		{
		}

		public HarmonyDelegate(Type declaringType, Type[] argumentTypes)
			: base(declaringType, argumentTypes)
		{
		}

		public HarmonyDelegate(Type declaringType, string methodName)
			: base(declaringType, methodName)
		{
		}

		public HarmonyDelegate(Type declaringType, string methodName, params Type[] argumentTypes)
			: base(declaringType, methodName, argumentTypes)
		{
		}

		public HarmonyDelegate(Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
			: base(declaringType, methodName, argumentTypes, argumentVariations)
		{
		}

		public HarmonyDelegate(Type declaringType, MethodDispatchType methodDispatchType)
			: base(declaringType, MethodType.Normal)
		{
			info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
		}

		public HarmonyDelegate(Type declaringType, MethodDispatchType methodDispatchType, params Type[] argumentTypes)
			: base(declaringType, MethodType.Normal, argumentTypes)
		{
			info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
		}

		public HarmonyDelegate(Type declaringType, MethodDispatchType methodDispatchType, Type[] argumentTypes, ArgumentType[] argumentVariations)
			: base(declaringType, MethodType.Normal, argumentTypes, argumentVariations)
		{
			info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
		}

		public HarmonyDelegate(Type declaringType, string methodName, MethodDispatchType methodDispatchType)
			: base(declaringType, methodName, MethodType.Normal)
		{
			info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
		}

		public HarmonyDelegate(string methodName)
			: base(methodName)
		{
		}

		public HarmonyDelegate(string methodName, params Type[] argumentTypes)
			: base(methodName, argumentTypes)
		{
		}

		public HarmonyDelegate(string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations)
			: base(methodName, argumentTypes, argumentVariations)
		{
		}

		public HarmonyDelegate(string methodName, MethodDispatchType methodDispatchType)
			: base(methodName, MethodType.Normal)
		{
			info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
		}

		public HarmonyDelegate(MethodDispatchType methodDispatchType)
		{
			info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
		}

		public HarmonyDelegate(MethodDispatchType methodDispatchType, params Type[] argumentTypes)
			: base(MethodType.Normal, argumentTypes)
		{
			info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
		}

		public HarmonyDelegate(MethodDispatchType methodDispatchType, Type[] argumentTypes, ArgumentType[] argumentVariations)
			: base(MethodType.Normal, argumentTypes, argumentVariations)
		{
			info.nonVirtualDelegate = methodDispatchType == MethodDispatchType.Call;
		}

		public HarmonyDelegate(Type[] argumentTypes)
			: base(argumentTypes)
		{
		}

		public HarmonyDelegate(Type[] argumentTypes, ArgumentType[] argumentVariations)
			: base(argumentTypes, argumentVariations)
		{
		}
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method, AllowMultiple = true)]
	public class HarmonyReversePatch : HarmonyAttribute
	{
		public HarmonyReversePatch(HarmonyReversePatchType type = HarmonyReversePatchType.Original)
		{
			info.reversePatchType = type;
		}
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public class HarmonyPatchAll : HarmonyAttribute
	{
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
	public class HarmonyPriority : HarmonyAttribute
	{
		public HarmonyPriority(int priority)
		{
			info.priority = priority;
		}
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
	public class HarmonyBefore : HarmonyAttribute
	{
		public HarmonyBefore(params string[] before)
		{
			info.before = before;
		}
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
	public class HarmonyAfter : HarmonyAttribute
	{
		public HarmonyAfter(params string[] after)
		{
			info.after = after;
		}
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method)]
	public class HarmonyDebug : HarmonyAttribute
	{
		public HarmonyDebug()
		{
			info.debug = true;
		}
	}
	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyPrepare : Attribute
	{
	}
	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyCleanup : Attribute
	{
	}
	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyTargetMethod : Attribute
	{
	}
	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyTargetMethods : Attribute
	{
	}
	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyPrefix : Attribute
	{
	}
	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyPostfix : Attribute
	{
	}
	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyTranspiler : Attribute
	{
	}
	[AttributeUsage(AttributeTargets.Method)]
	public class HarmonyFinalizer : Attribute
	{
	}
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true)]
	public class HarmonyArgument : Attribute
	{
		public string OriginalName { get; private set; }

		public int Index { get; private set; }

		public string NewName { get; private set; }

		public HarmonyArgument(string originalName)
			: this(originalName, null)
		{
		}

		public HarmonyArgument(int index)
			: this(index, null)
		{
		}

		public HarmonyArgument(string originalName, string newName)
		{
			OriginalName = originalName;
			Index = -1;
			NewName = newName;
		}

		public HarmonyArgument(int index, string name)
		{
			OriginalName = null;
			Index = index;
			NewName = name;
		}
	}
	public class CodeInstruction
	{
		internal static class State
		{
			internal static readonly List<Delegate> closureCache = new List<Delegate>();
		}

		public System.Reflection.Emit.OpCode opcode;

		public object operand;

		public List<Label> labels = new List<Label>();

		public List<ExceptionBlock> blocks = new List<ExceptionBlock>();

		internal CodeInstruction()
		{
		}

		internal static CodeInstruction Annotation(string annotation)
		{
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Nop, annotation);
		}

		internal string IsAnnotation()
		{
			if (!(opcode == System.Reflection.Emit.OpCodes.Nop))
			{
				return null;
			}
			return operand as string;
		}

		public CodeInstruction(System.Reflection.Emit.OpCode opcode, object operand = null)
		{
			this.opcode = opcode;
			this.operand = operand;
		}

		public CodeInstruction(CodeInstruction instruction)
		{
			opcode = instruction.opcode;
			operand = instruction.operand;
			labels = instruction.labels.ToList();
			blocks = instruction.blocks.ToList();
		}

		public CodeInstruction Clone()
		{
			return new CodeInstruction(this)
			{
				labels = new List<Label>(),
				blocks = new List<ExceptionBlock>()
			};
		}

		public CodeInstruction Clone(System.Reflection.Emit.OpCode opcode)
		{
			CodeInstruction codeInstruction = Clone();
			codeInstruction.opcode = opcode;
			return codeInstruction;
		}

		public CodeInstruction Clone(object operand)
		{
			CodeInstruction codeInstruction = Clone();
			codeInstruction.operand = operand;
			return codeInstruction;
		}

		public static CodeInstruction Call(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			MethodInfo methodInfo = AccessTools.Method(type, name, parameters, generics);
			if ((object)methodInfo == null)
			{
				throw new ArgumentException($"No method found for type={type}, name={name}, parameters={parameters.Description()}, generics={generics.Description()}");
			}
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, methodInfo);
		}

		public static CodeInstruction Call(string typeColonMethodname, Type[] parameters = null, Type[] generics = null)
		{
			MethodInfo methodInfo = AccessTools.Method(typeColonMethodname, parameters, generics);
			if ((object)methodInfo == null)
			{
				throw new ArgumentException($"No method found for {typeColonMethodname}, parameters={parameters.Description()}, generics={generics.Description()}");
			}
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, methodInfo);
		}

		public static CodeInstruction Call(Expression<Action> expression)
		{
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));
		}

		public static CodeInstruction Call<T>(Expression<Action<T>> expression)
		{
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));
		}

		public static CodeInstruction Call<T, TResult>(Expression<Func<T, TResult>> expression)
		{
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));
		}

		public static CodeInstruction Call(LambdaExpression expression)
		{
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, SymbolExtensions.GetMethodInfo(expression));
		}

		public static CodeInstruction CallClosure<T>(T closure) where T : Delegate
		{
			if (closure.Method.IsStatic && closure.Target == null)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, closure.Method);
			}
			Type[] array = (from x in closure.Method.GetParameters()
				select x.ParameterType).ToArray();
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition(closure.Method.Name, closure.Method.ReturnType, array);
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			Type type = closure.Target.GetType();
			if (closure.Target != null && type.GetFields().Any((FieldInfo x) => !x.IsStatic))
			{
				State.closureCache.Add(closure);
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldsfld, AccessTools.Field(typeof(State), "closureCache"));
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldc_I4, State.closureCache.Count - 1);
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(List<Delegate>), "Item"));
			}
			else
			{
				if (closure.Target == null)
				{
					iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldnull);
				}
				else
				{
					iLGenerator.Emit(System.Reflection.Emit.OpCodes.Newobj, AccessTools.FirstConstructor(type, (ConstructorInfo x) => !x.IsStatic && x.GetParameters().Length == 0));
				}
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldftn, closure.Method);
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Newobj, AccessTools.Constructor(typeof(T), new Type[2]
				{
					typeof(object),
					typeof(IntPtr)
				}));
			}
			for (int num = 0; num < array.Length; num++)
			{
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg, num);
			}
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Callvirt, AccessTools.Method(typeof(T), "Invoke"));
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, dynamicMethodDefinition.Generate());
		}

		public static CodeInstruction LoadField(Type type, string name, bool useAddress = false)
		{
			FieldInfo fieldInfo = AccessTools.Field(type, name);
			if ((object)fieldInfo == null)
			{
				throw new ArgumentException($"No field found for {type} and {name}");
			}
			return new CodeInstruction((!useAddress) ? (fieldInfo.IsStatic ? System.Reflection.Emit.OpCodes.Ldsfld : System.Reflection.Emit.OpCodes.Ldfld) : (fieldInfo.IsStatic ? System.Reflection.Emit.OpCodes.Ldsflda : System.Reflection.Emit.OpCodes.Ldflda), fieldInfo);
		}

		public static CodeInstruction StoreField(Type type, string name)
		{
			FieldInfo fieldInfo = AccessTools.Field(type, name);
			if ((object)fieldInfo == null)
			{
				throw new ArgumentException($"No field found for {type} and {name}");
			}
			return new CodeInstruction(fieldInfo.IsStatic ? System.Reflection.Emit.OpCodes.Stsfld : System.Reflection.Emit.OpCodes.Stfld, fieldInfo);
		}

		public static CodeInstruction LoadLocal(int index, bool useAddress = false)
		{
			if (useAddress)
			{
				if (index < 256)
				{
					return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldloca_S, Convert.ToByte(index));
				}
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldloca, index);
			}
			if (index == 0)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldloc_0);
			}
			if (index == 1)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldloc_1);
			}
			if (index == 2)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldloc_2);
			}
			if (index == 3)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldloc_3);
			}
			if (index < 256)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldloc_S, Convert.ToByte(index));
			}
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldloc, index);
		}

		public static CodeInstruction StoreLocal(int index)
		{
			if (index == 0)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Stloc_0);
			}
			if (index == 1)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Stloc_1);
			}
			if (index == 2)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Stloc_2);
			}
			if (index == 3)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Stloc_3);
			}
			if (index < 256)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Stloc_S, Convert.ToByte(index));
			}
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Stloc, index);
		}

		public static CodeInstruction LoadArgument(int index, bool useAddress = false)
		{
			if (useAddress)
			{
				if (index < 256)
				{
					return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarga_S, Convert.ToByte(index));
				}
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarga, index);
			}
			if (index == 0)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_0);
			}
			if (index == 1)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_1);
			}
			if (index == 2)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_2);
			}
			if (index == 3)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_3);
			}
			if (index < 256)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg_S, Convert.ToByte(index));
			}
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldarg, index);
		}

		public static CodeInstruction StoreArgument(int index)
		{
			if (index < 256)
			{
				return new CodeInstruction(System.Reflection.Emit.OpCodes.Starg_S, Convert.ToByte(index));
			}
			return new CodeInstruction(System.Reflection.Emit.OpCodes.Starg, index);
		}

		public bool HasBlock(ExceptionBlockType type)
		{
			return blocks?.Any((ExceptionBlock block) => block.blockType == type) ?? false;
		}

		public override string ToString()
		{
			List<string> list = new List<string>();
			foreach (Label label in labels)
			{
				list.Add($"Label{label.GetHashCode()}");
			}
			foreach (ExceptionBlock block in blocks)
			{
				list.Add("EX_" + block.blockType.ToString().Replace("Block", ""));
			}
			string text = ((list.Count > 0) ? (" [" + string.Join(", ", list.ToArray()) + "]") : "");
			string text2 = Emitter.FormatOperand(operand);
			if (text2.Length > 0)
			{
				text2 = " " + text2;
			}
			System.Reflection.Emit.OpCode opCode = opcode;
			return opCode.ToString() + text2 + text;
		}
	}
	public enum ExceptionBlockType
	{
		BeginExceptionBlock,
		BeginCatchBlock,
		BeginExceptFilterBlock,
		BeginFaultBlock,
		BeginFinallyBlock,
		EndExceptionBlock
	}
	public class ExceptionBlock
	{
		public ExceptionBlockType blockType;

		public Type catchType;

		public ExceptionBlock(ExceptionBlockType blockType, Type catchType = null)
		{
			this.blockType = blockType;
			this.catchType = catchType ?? typeof(object);
		}
	}
	public class Harmony
	{
		public static bool DEBUG;

		private static readonly ConditionalWeakTable<Assembly, Dictionary<string, List<Type>>> AssemblyCachedCategories = new ConditionalWeakTable<Assembly, Dictionary<string, List<Type>>>();

		public string Id { get; private set; }

		public Harmony(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				throw new ArgumentException("id cannot be null or empty");
			}
			try
			{
				string environmentVariable = Environment.GetEnvironmentVariable("HARMONY_DEBUG");
				if (environmentVariable != null && environmentVariable.Length > 0)
				{
					environmentVariable = environmentVariable.Trim();
					DEBUG = environmentVariable == "1" || bool.Parse(environmentVariable);
				}
			}
			catch
			{
			}
			if (DEBUG)
			{
				Assembly assembly = typeof(Harmony).Assembly;
				Version version = assembly.GetName().Version;
				string value = assembly.Location;
				string value2 = Environment.Version.ToString();
				string value3 = Environment.OSVersion.Platform.ToString();
				if (string.IsNullOrEmpty(value))
				{
					value = new Uri(assembly.CodeBase).LocalPath;
				}
				FileLog.Log($"### Harmony id={id}, version={version}, location={value}, env/clr={value2}, platform={value3}");
				MethodBase outsideCaller = AccessTools.GetOutsideCaller();
				if ((object)outsideCaller.DeclaringType != null)
				{
					Assembly assembly2 = outsideCaller.DeclaringType.Assembly;
					value = assembly2.Location;
					if (string.IsNullOrEmpty(value))
					{
						value = new Uri(assembly2.CodeBase).LocalPath;
					}
					FileLog.Log("### Started from " + outsideCaller.FullDescription() + ", location " + value);
					FileLog.Log($"### At {DateTime.Now:yyyy-MM-dd hh.mm.ss}");
				}
			}
			Id = id;
		}

		public void PatchAll()
		{
			MethodBase method = new StackTrace().GetFrame(1).GetMethod();
			Assembly assembly = method.ReflectedType.Assembly;
			PatchAll(assembly);
		}

		public PatchProcessor CreateProcessor(MethodBase original)
		{
			return new PatchProcessor(this, original);
		}

		public PatchClassProcessor CreateClassProcessor(Type type)
		{
			return new PatchClassProcessor(this, type);
		}

		public ReversePatcher CreateReversePatcher(MethodBase original, HarmonyMethod standin)
		{
			return new ReversePatcher(this, original, standin);
		}

		public void PatchAll(Assembly assembly)
		{
			AccessTools.GetTypesFromAssembly(assembly).DoIf((Type type) => type.HasHarmonyAttribute(), delegate(Type type)
			{
				CreateClassProcessor(type).Patch();
			});
		}

		public void PatchAllUncategorized()
		{
			MethodBase method = new StackTrace().GetFrame(1).GetMethod();
			Assembly assembly = method.ReflectedType.Assembly;
			PatchAllUncategorized(assembly);
		}

		public void PatchAllUncategorized(Assembly assembly)
		{
			PatchClassProcessor[] sequence = (from type in AccessTools.GetTypesFromAssembly(assembly)
				where type.HasHarmonyAttribute()
				select type).Select(CreateClassProcessor).ToArray();
			sequence.DoIf((PatchClassProcessor patchClass) => string.IsNullOrEmpty(patchClass.Category), delegate(PatchClassProcessor patchClass)
			{
				patchClass.Patch();
			});
		}

		public void PatchCategory(string category)
		{
			MethodBase method = new StackTrace().GetFrame(1).GetMethod();
			Assembly assembly = method.ReflectedType.Assembly;
			PatchCategory(assembly, category);
		}

		public void PatchCategory(Assembly assembly, string category)
		{
			Dictionary<string, List<Type>> value = AssemblyCachedCategories.GetValue(assembly, BuildCategoryCache);
			if (value.TryGetValue(category, out var value2))
			{
				value2.Do(delegate(Type type)
				{
					CreateClassProcessor(type).Patch();
				});
			}
		}

		private static Dictionary<string, List<Type>> BuildCategoryCache(Assembly assembly)
		{
			Dictionary<string, List<Type>> dictionary = new Dictionary<string, List<Type>>();
			Type[] typesFromAssembly = AccessTools.GetTypesFromAssembly(assembly);
			foreach (Type type in typesFromAssembly)
			{
				List<HarmonyMethod> fromType = HarmonyMethodExtensions.GetFromType(type);
				if (fromType.Count == 0)
				{
					continue;
				}
				HarmonyMethod harmonyMethod = HarmonyMethod.Merge(fromType);
				string category = harmonyMethod.category;
				if (!string.IsNullOrEmpty(category))
				{
					if (!dictionary.TryGetValue(category, out var value) && value == null)
					{
						value = new List<Type>();
					}
					value.Add(type);
					dictionary[category] = value;
				}
			}
			return dictionary;
		}

		public MethodInfo Patch(MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null, HarmonyMethod finalizer = null)
		{
			PatchProcessor patchProcessor = CreateProcessor(original);
			patchProcessor.AddPrefix(prefix);
			patchProcessor.AddPostfix(postfix);
			patchProcessor.AddTranspiler(transpiler);
			patchProcessor.AddFinalizer(finalizer);
			return patchProcessor.Patch();
		}

		public static MethodInfo ReversePatch(MethodBase original, HarmonyMethod standin, MethodInfo transpiler = null)
		{
			return PatchFunctions.ReversePatch(standin, original, transpiler);
		}

		public void UnpatchAll(string harmonyID = null)
		{
			List<MethodBase> list = GetAllPatchedMethods().ToList();
			foreach (MethodBase original in list)
			{
				bool flag = original.HasMethodBody();
				Patches patchInfo = GetPatchInfo(original);
				if (flag)
				{
					patchInfo.Postfixes.DoIf(IDCheck, delegate(Patch patch)
					{
						Unpatch(original, patch.PatchMethod);
					});
					patchInfo.Prefixes.DoIf(IDCheck, delegate(Patch patch)
					{
						Unpatch(original, patch.PatchMethod);
					});
					patchInfo.InnerPostfixes.DoIf(IDCheck, delegate(Patch patch)
					{
						Unpatch(original, patch.PatchMethod);
					});
					patchInfo.InnerPrefixes.DoIf(IDCheck, delegate(Patch patch)
					{
						Unpatch(original, patch.PatchMethod);
					});
				}
				patchInfo.Transpilers.DoIf(IDCheck, delegate(Patch patch)
				{
					Unpatch(original, patch.PatchMethod);
				});
				if (flag)
				{
					patchInfo.Finalizers.DoIf(IDCheck, delegate(Patch patch)
					{
						Unpatch(original, patch.PatchMethod);
					});
				}
			}
			bool IDCheck(Patch patch)
			{
				if (harmonyID != null)
				{
					return patch.owner == harmonyID;
				}
				return true;
			}
		}

		public void Unpatch(MethodBase original, HarmonyPatchType type, string harmonyID = "*")
		{
			PatchProcessor patchProcessor = CreateProcessor(original);
			patchProcessor.Unpatch(type, harmonyID);
		}

		public void Unpatch(MethodBase original, MethodInfo patch)
		{
			PatchProcessor patchProcessor = CreateProcessor(original);
			patchProcessor.Unpatch(patch);
		}

		public void UnpatchCategory(string category)
		{
			MethodBase method = new StackTrace().GetFrame(1).GetMethod();
			Assembly assembly = method.ReflectedType.Assembly;
			UnpatchCategory(assembly, category);
		}

		public void UnpatchCategory(Assembly assembly, string category)
		{
			Dictionary<string, List<Type>> value = AssemblyCachedCategories.GetValue(assembly, BuildCategoryCache);
			if (value.TryGetValue(category, out var value2))
			{
				value2.Do(delegate(Type type)
				{
					CreateClassProcessor(type).Unpatch();
				});
			}
		}

		public static bool HasAnyPatches(string harmonyID)
		{
			return GetAllPatchedMethods().Select(GetPatchInfo).Any((Patches info) => info.Owners.Contains(harmonyID));
		}

		public static Patches GetPatchInfo(MethodBase method)
		{
			return PatchProcessor.GetPatchInfo(method);
		}

		public IEnumerable<MethodBase> GetPatchedMethods()
		{
			return from original in GetAllPatchedMethods()
				where GetPatchInfo(original).Owners.Contains(Id)
				select original;
		}

		public static IEnumerable<MethodBase> GetAllPatchedMethods()
		{
			return PatchProcessor.GetAllPatchedMethods();
		}

		public static MethodBase GetOriginalMethod(MethodInfo replacement)
		{
			if (replacement == null)
			{
				throw new ArgumentNullException("replacement");
			}
			return HarmonySharedState.GetRealMethod(replacement, useReplacement: false);
		}

		public static MethodBase GetMethodFromStackframe(StackFrame frame)
		{
			if (frame == null)
			{
				throw new ArgumentNullException("frame");
			}
			return HarmonySharedState.GetStackFrameMethod(frame, useReplacement: true);
		}

		public static MethodBase GetOriginalMethodFromStackframe(StackFrame frame)
		{
			if (frame == null)
			{
				throw new ArgumentNullException("frame");
			}
			return HarmonySharedState.GetStackFrameMethod(frame, useReplacement: false);
		}

		public static Dictionary<string, Version> VersionInfo(out Version currentVersion)
		{
			return PatchProcessor.VersionInfo(out currentVersion);
		}

		public static void SetSwitch(string name, object value)
		{
			Switches.SetSwitchValue(name, value);
		}

		public static void ClearSwitch(string name)
		{
			Switches.ClearSwitchValue(name);
		}

		public static bool TryGetSwitch(string name, out object value)
		{
			return Switches.TryGetSwitchValue(name, out value);
		}

		public static bool TryIsSwitchEnabled(string name, out bool isEnabled)
		{
			return Switches.TryGetSwitchEnabled(name, out isEnabled);
		}
	}
	public class HarmonyException : Exception
	{
		private Dictionary<int, CodeInstruction> instructions;

		private int errorOffset;

		internal HarmonyException()
		{
		}

		internal HarmonyException(string message)
			: base(message)
		{
		}

		internal HarmonyException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		internal HarmonyException(Exception innerException, Dictionary<int, CodeInstruction> instructions, int errorOffset)
			: base("IL Compile Error", innerException)
		{
			this.instructions = instructions;
			this.errorOffset = errorOffset;
		}

		internal static Exception Create(Exception ex, Dictionary<int, CodeInstruction> finalInstructions)
		{
			Match match = Regex.Match(ex.Message.TrimEnd(), "Reason: Invalid IL code in.+: IL_(\\d{4}): (.+)$");
			if (!match.Success)
			{
				return ex;
			}
			int num = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber);
			Regex.Replace(match.Groups[2].Value, " {2,}", " ");
			if (ex is HarmonyException ex2)
			{
				ex2.instructions = finalInstructions;
				ex2.errorOffset = num;
				return ex2;
			}
			return new HarmonyException(ex, finalInstructions, num);
		}

		public List<KeyValuePair<int, CodeInstruction>> GetInstructionsWithOffsets()
		{
			return instructions.OrderBy((KeyValuePair<int, CodeInstruction> ins) => ins.Key).ToList();
		}

		public List<CodeInstruction> GetInstructions()
		{
			return (from ins in instructions
				orderby ins.Key
				select ins.Value).ToList();
		}

		public int GetErrorOffset()
		{
			return errorOffset;
		}

		public int GetErrorIndex()
		{
			if (instructions.TryGetValue(errorOffset, out var value))
			{
				return GetInstructions().IndexOf(value);
			}
			return -1;
		}
	}
	public class HarmonyMethod
	{
		public MethodInfo method;

		public string category;

		public Type declaringType;

		public string methodName;

		public MethodType? methodType;

		public Type[] argumentTypes;

		public int priority = -1;

		public string[] before;

		public string[] after;

		public HarmonyReversePatchType? reversePatchType;

		public bool? debug;

		public bool nonVirtualDelegate;

		public HarmonyMethod()
		{
		}

		private void ImportMethod(MethodInfo theMethod)
		{
			method = theMethod;
			if ((object)method != null)
			{
				List<HarmonyMethod> fromMethod = HarmonyMethodExtensions.GetFromMethod(method);
				if (fromMethod != null)
				{
					Merge(fromMethod).CopyTo(this);
				}
			}
		}

		public HarmonyMethod(MethodInfo method)
		{
			if ((object)method == null)
			{
				throw new ArgumentNullException("method");
			}
			ImportMethod(method);
		}

		public HarmonyMethod(Delegate @delegate)
			: this(@delegate.Method)
		{
		}

		public HarmonyMethod(MethodInfo method, int priority = -1, string[] before = null, string[] after = null, bool? debug = null)
		{
			if ((object)method == null)
			{
				throw new ArgumentNullException("method");
			}
			ImportMethod(method);
			this.priority = priority;
			this.before = before;
			this.after = after;
			this.debug = debug;
		}

		public HarmonyMethod(Delegate @delegate, int priority = -1, string[] before = null, string[] after = null, bool? debug = null)
			: this(@delegate.Method, priority, before, after, debug)
		{
		}

		public HarmonyMethod(Type methodType, string methodName, Type[] argumentTypes = null)
		{
			MethodInfo methodInfo = AccessTools.Method(methodType, methodName, argumentTypes);
			if ((object)methodInfo == null)
			{
				throw new ArgumentException($"Cannot not find method for type {methodType} and name {methodName} and parameters {argumentTypes?.Description()}");
			}
			ImportMethod(methodInfo);
		}

		public static List<string> HarmonyFields()
		{
			return (from s in AccessTools.GetFieldNames(typeof(HarmonyMethod))
				where s != "method"
				select s).ToList();
		}

		public static HarmonyMethod Merge(List<HarmonyMethod> attributes)
		{
			HarmonyMethod harmonyMethod = new HarmonyMethod();
			if (attributes == null || attributes.Count == 0)
			{
				return harmonyMethod;
			}
			Traverse resultTrv = Traverse.Create(harmonyMethod);
			attributes.ForEach(delegate(HarmonyMethod attribute)
			{
				Traverse trv = Traverse.Create(attribute);
				HarmonyFields().ForEach(delegate(string f)
				{
					object value = trv.Field(f).GetValue();
					if (value != null && (f != "priority" || (int)value != -1))
					{
						HarmonyMethodExtensions.SetValue(resultTrv, f, value);
					}
				});
			});
			return harmonyMethod;
		}

		public override string ToString()
		{
			string result = "";
			Traverse trv = Traverse.Create(this);
			HarmonyFields().ForEach(delegate(string f)
			{
				if (result.Length > 0)
				{
					result += ", ";
				}
				result += $"{f}={trv.Field(f).GetValue()}";
			});
			return "HarmonyMethod[" + result + "]";
		}

		internal string Description()
		{
			string value = (((object)declaringType != null) ? declaringType.FullName : "undefined");
			string value2 = methodName ?? "undefined";
			string value3 = (methodType.HasValue ? methodType.Value.ToString() : "undefined");
			string value4 = ((argumentTypes != null) ? argumentTypes.Description() : "undefined");
			return $"(class={value}, methodname={value2}, type={value3}, args={value4})";
		}

		public static implicit operator HarmonyMethod(MethodInfo method)
		{
			return new HarmonyMethod(method);
		}

		public static implicit operator HarmonyMethod(Delegate @delegate)
		{
			return new HarmonyMethod(@delegate);
		}
	}
	public static class HarmonyMethodExtensions
	{
		internal static void SetValue(Traverse trv, string name, object val)
		{
			if (val != null)
			{
				Traverse traverse = trv.Field(name);
				if (name == "methodType" || name == "reversePatchType")
				{
					Type underlyingType = Nullable.GetUnderlyingType(traverse.GetValueType());
					val = Enum.ToObject(underlyingType, (int)val);
				}
				traverse.SetValue(val);
			}
		}

		public static void CopyTo(this HarmonyMethod from, HarmonyMethod to)
		{
			if (to == null)
			{
				return;
			}
			Traverse fromTrv = Traverse.Create(from);
			Traverse toTrv = Traverse.Create(to);
			HarmonyMethod.HarmonyFields().ForEach(delegate(string f)
			{
				object value = fromTrv.Field(f).GetValue();
				if (value != null)
				{
					SetValue(toTrv, f, value);
				}
			});
		}

		public static HarmonyMethod Clone(this HarmonyMethod original)
		{
			HarmonyMethod harmonyMethod = new HarmonyMethod();
			original.CopyTo(harmonyMethod);
			return harmonyMethod;
		}

		public static HarmonyMethod Merge(this HarmonyMethod master, HarmonyMethod detail)
		{
			if (detail == null)
			{
				return master;
			}
			HarmonyMethod harmonyMethod = new HarmonyMethod();
			Traverse resultTrv = Traverse.Create(harmonyMethod);
			Traverse masterTrv = Traverse.Create(master);
			Traverse detailTrv = Traverse.Create(detail);
			HarmonyMethod.HarmonyFields().ForEach(delegate(string f)
			{
				object value = masterTrv.Field(f).GetValue();
				object value2 = detailTrv.Field(f).GetValue();
				if (f != "priority")
				{
					SetValue(resultTrv, f, value2 ?? value);
				}
				else
				{
					int num = (int)value;
					int num2 = (int)value2;
					int num3 = Math.Max(num, num2);
					if (num == -1 && num2 != -1)
					{
						num3 = num2;
					}
					if (num != -1 && num2 == -1)
					{
						num3 = num;
					}
					SetValue(resultTrv, f, num3);
				}
			});
			return harmonyMethod;
		}

		private static HarmonyMethod GetHarmonyMethodInfo(object attribute)
		{
			FieldInfo field = attribute.GetType().GetField("info", AccessTools.all);
			if ((object)field == null)
			{
				return null;
			}
			if (field.FieldType.FullName != PatchTools.harmonyMethodFullName)
			{
				return null;
			}
			object value = field.GetValue(attribute);
			return AccessTools.MakeDeepCopy<HarmonyMethod>(value);
		}

		public static List<HarmonyMethod> GetFromType(Type type)
		{
			return (from info in type.GetCustomAttributes(inherit: true).Select(GetHarmonyMethodInfo)
				where info != null
				select info).ToList();
		}

		public static HarmonyMethod GetMergedFromType(Type type)
		{
			return HarmonyMethod.Merge(GetFromType(type));
		}

		public static List<HarmonyMethod> GetFromMethod(MethodBase method)
		{
			return (from info in method.GetCustomAttributes(inherit: true).Select(GetHarmonyMethodInfo)
				where info != null
				select info).ToList();
		}

		public static HarmonyMethod GetMergedFromMethod(MethodBase method)
		{
			return HarmonyMethod.Merge(GetFromMethod(method));
		}
	}
	[Serializable]
	public class InnerMethod
	{
		[NonSerialized]
		private MethodInfo method;

		private int methodToken;

		private string moduleGUID;

		public int[] positions;

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
	[Serializable]
	public class Patch : IComparable
	{
		public readonly int index;

		public readonly string owner;

		public readonly int priority;

		public readonly string[] before;

		public readonly string[] after;

		public readonly bool debug;

		[NonSerialized]
		private MethodInfo patchMethod;

		private int methodToken;

		private string moduleGUID;

		public readonly InnerMethod innerMethod;

		public MethodInfo PatchMethod
		{
			get
			{
				if ((object)patchMethod == null)
				{
					patchMethod = AccessTools.GetMethodByModuleAndToken(moduleGUID, methodToken);
				}
				return patchMethod;
			}
			set
			{
				patchMethod = value;
				methodToken = patchMethod.MetadataToken;
				moduleGUID = patchMethod.Module.ModuleVersionId.ToString();
			}
		}

		public Patch(MethodInfo patch, int index, string owner, int priority, string[] before, string[] after, bool debug)
		{
			if (patch is DynamicMethod)
			{
				throw new Exception("Cannot directly reference dynamic method \"" + patch.FullDescription() + "\" in Harmony. Use a factory method instead that will return the dynamic method.");
			}
			this.index = index;
			this.owner = owner;
			this.priority = ((priority == -1) ? 400 : priority);
			this.before = before ?? Array.Empty<string>();
			this.after = after ?? Array.Empty<string>();
			this.debug = debug;
			PatchMethod = patch;
		}

		public Patch(HarmonyMethod method, int index, string owner)
			: this(method.method, index, owner, method.priority, method.before, method.after, method.debug == true)
		{
		}

		internal Patch(int index, string owner, int priority, string[] before, string[] after, bool debug, int methodToken, string moduleGUID)
		{
			this.index = index;
			this.owner = owner;
			this.priority = ((priority == -1) ? 400 : priority);
			this.before = before ?? Array.Empty<string>();
			this.after = after ?? Array.Empty<string>();
			this.debug = debug;
			this.methodToken = methodToken;
			this.moduleGUID = moduleGUID;
		}

		public MethodInfo GetMethod(MethodBase original)
		{
			MethodInfo methodInfo = PatchMethod;
			if (methodInfo.ReturnType != typeof(DynamicMethod) && methodInfo.ReturnType != typeof(MethodInfo))
			{
				return methodInfo;
			}
			if (!methodInfo.IsStatic)
			{
				return methodInfo;
			}
			ParameterInfo[] parameters = methodInfo.GetParameters();
			if (parameters.Length != 1)
			{
				return methodInfo;
			}
			if (parameters[0].ParameterType != typeof(MethodBase))
			{
				return methodInfo;
			}
			return methodInfo.Invoke(null, new object[1] { original }) as MethodInfo;
		}

		public override bool Equals(object obj)
		{
			if (obj != null && obj is Patch)
			{
				return PatchMethod == ((Patch)obj).PatchMethod;
			}
			return false;
		}

		public int CompareTo(object obj)
		{
			return PatchInfoSerialization.PriorityComparer(obj, index, priority);
		}

		public override int GetHashCode()
		{
			return PatchMethod.GetHashCode();
		}
	}
	public class PatchClassProcessor
	{
		private readonly Harmony instance;

		private readonly Type containerType;

		private readonly HarmonyMethod containerAttributes;

		private readonly Dictionary<Type, MethodInfo> auxilaryMethods;

		private readonly List<AttributePatch> patchMethods;

		private static readonly List<Type> auxilaryTypes = new List<Type>(4)
		{
			typeof(HarmonyPrepare),
			typeof(HarmonyCleanup),
			typeof(HarmonyTargetMethod),
			typeof(HarmonyTargetMethods)
		};

		public string Category { get; set; }

		public PatchClassProcessor(Harmony instance, Type type)
		{
			if (instance == null)
			{
				throw new ArgumentNullException("instance");
			}
			if ((object)type == null)
			{
				throw new ArgumentNullException("type");
			}
			this.instance = instance;
			containerType = type;
			List<HarmonyMethod> fromType = HarmonyMethodExtensions.GetFromType(type);
			containerAttributes = HarmonyMethod.Merge(fromType);
			HarmonyMethod harmonyMethod = containerAttributes;
			MethodType valueOrDefault = harmonyMethod.methodType.GetValueOrDefault();
			if (!harmonyMethod.methodType.HasValue)
			{
				valueOrDefault = MethodType.Normal;
				harmonyMethod.methodType = valueOrDefault;
			}
			Category = containerAttributes.category;
			auxilaryMethods = new Dictionary<Type, MethodInfo>();
			foreach (Type auxilaryType in auxilaryTypes)
			{
				MethodInfo patchMethod = PatchTools.GetPatchMethod(containerType, auxilaryType.FullName);
				if ((object)patchMethod != null)
				{
					auxilaryMethods[auxilaryType] = patchMethod;
				}
			}
			patchMethods = PatchTools.GetPatchMethods(containerType);
			foreach (AttributePatch patchMethod2 in patchMethods)
			{
				MethodInfo method = patchMethod2.info.method;
				patchMethod2.info = containerAttributes.Merge(patchMethod2.info);
				patchMethod2.info.method = method;
			}
		}

		public List<MethodInfo> Patch()
		{
			Exception exception = null;
			if (!RunMethod<HarmonyPrepare, bool>(defaultIfNotExisting: true, defaultIfFailing: false, null, Array.Empty<object>()))
			{
				RunMethod<HarmonyCleanup>(ref exception, Array.Empty<object>());
				ReportException(exception, null);
				return new List<MethodInfo>();
			}
			List<MethodInfo> result = new List<MethodInfo>();
			MethodBase lastOriginal = null;
			try
			{
				List<MethodBase> bulkMethods = GetBulkMethods();
				if (bulkMethods.Count == 1)
				{
					lastOriginal = bulkMethods[0];
				}
				ReversePatch(ref lastOriginal);
				result = ((bulkMethods.Count > 0) ? BulkPatch(bulkMethods, ref lastOriginal, unpatch: false) : PatchWithAttributes(ref lastOriginal, unpatch: false));
			}
			catch (Exception ex)
			{
				exception = ex;
			}
			RunMethod<HarmonyCleanup>(ref exception, new object[1] { exception });
			ReportException(exception, lastOriginal);
			return result;
		}

		public void Unpatch()
		{
			List<MethodBase> bulkMethods = GetBulkMethods();
			MethodBase lastOriginal = null;
			if (bulkMethods.Count > 0)
			{
				BulkPatch(bulkMethods, ref lastOriginal, unpatch: true);
			}
			else
			{
				PatchWithAttributes(ref lastOriginal, unpatch: true);
			}
		}

		private void ReversePatch(ref MethodBase lastOriginal)
		{
			for (int i = 0; i < patchMethods.Count; i++)
			{
				AttributePatch attributePatch = patchMethods[i];
				if (attributePatch.type == HarmonyPatchType.ReversePatch)
				{
					MethodBase originalMethod = attributePatch.info.GetOriginalMethod();
					if ((object)originalMethod != null)
					{
						lastOriginal = originalMethod;
					}
					ReversePatcher reversePatcher = instance.CreateReversePatcher(lastOriginal, attributePatch.info);
					lock (PatchProcessor.locker)
					{
						reversePatcher.Patch();
					}
				}
			}
		}

		private List<MethodInfo> BulkPatch(List<MethodBase> originals, ref MethodBase lastOriginal, bool unpatch)
		{
			PatchJobs<MethodInfo> patchJobs = new PatchJobs<MethodInfo>();
			for (int i = 0; i < originals.Count; i++)
			{
				lastOriginal = originals[i];
				PatchJobs<MethodInfo>.Job job = patchJobs.GetJob(lastOriginal);
				foreach (AttributePatch patchMethod in patchMethods)
				{
					string text = "You cannot combine TargetMethod, TargetMethods or [HarmonyPatchAll] with individual annotations";
					HarmonyMethod info = patchMethod.info;
					if (info.methodName != null)
					{
						throw new ArgumentException(text + " [" + info.methodName + "]");
					}
					if (info.methodType.HasValue && info.methodType.Value != MethodType.Normal)
					{
						throw new ArgumentException($"{text} [{info.methodType}]");
					}
					if (info.argumentTypes != null)
					{
						throw new ArgumentException(text + " [" + info.argumentTypes.Description() + "]");
					}
					job.AddPatch(patchMethod);
				}
			}
			foreach (PatchJobs<MethodInfo>.Job job2 in patchJobs.GetJobs())
			{
				lastOriginal = job2.original;
				if (unpatch)
				{
					ProcessUnpatchJob(job2);
				}
				else
				{
					ProcessPatchJob(job2);
				}
			}
			return patchJobs.GetReplacements();
		}

		private List<MethodInfo> PatchWithAttributes(ref MethodBase lastOriginal, bool unpatch)
		{
			PatchJobs<MethodInfo> patchJobs = new PatchJobs<MethodInfo>();
			foreach (AttributePatch patchMethod in patchMethods)
			{
				lastOriginal = patchMethod.info.GetOriginalMethod();
				if ((object)lastOriginal == null)
				{
					throw new ArgumentException("Undefined target method for patch method " + patchMethod.info.method.FullDescription());
				}
				PatchJobs<MethodInfo>.Job job = patchJobs.GetJob(lastOriginal);
				job.AddPatch(patchMethod);
			}
			foreach (PatchJobs<MethodInfo>.Job job2 in patchJobs.GetJobs())
			{
				lastOriginal = job2.original;
				if (unpatch)
				{
					ProcessUnpatchJob(job2);
				}
				else
				{
					ProcessPatchJob(job2);
				}
			}
			return patchJobs.GetReplacements();
		}

		private void ProcessPatchJob(PatchJobs<MethodInfo>.Job job)
		{
			MethodInfo replacement = null;
			bool flag = RunMethod<HarmonyPrepare, bool>(defaultIfNotExisting: true, defaultIfFailing: false, null, new object[1] { job.original });
			Exception exception = null;
			if (flag)
			{
				lock (PatchProcessor.locker)
				{
					try
					{
						PatchInfo patchInfo = HarmonySharedState.GetPatchInfo(job.original) ?? new PatchInfo();
						patchInfo.AddPrefixes(instance.Id, job.prefixes.ToArray());
						patchInfo.AddPostfixes(instance.Id, job.postfixes.ToArray());
						patchInfo.AddTranspilers(instance.Id, job.transpilers.ToArray());
						patchInfo.AddFinalizers(instance.Id, job.finalizers.ToArray());
						patchInfo.AddInnerPrefixes(instance.Id, job.innerprefixes.ToArray());
						patchInfo.AddInnerPostfixes(instance.Id, job.innerpostfixes.ToArray());
						replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
						HarmonySharedState.UpdatePatchInfo(job.original, replacement, patchInfo);
					}
					catch (Exception ex)
					{
						exception = ex;
					}
				}
			}
			RunMethod<HarmonyCleanup>(ref exception, new object[2] { job.original, exception });
			ReportException(exception, job.original);
			job.replacement = replacement;
		}

		private void ProcessUnpatchJob(PatchJobs<MethodInfo>.Job job)
		{
			PatchInfo patchInfo = HarmonySharedState.GetPatchInfo(job.original) ?? new PatchInfo();
			bool flag = job.original.HasMethodBody();
			if (flag)
			{
				job.postfixes.Do(delegate(HarmonyMethod patch)
				{
					patchInfo.RemovePatch(patch.method);
				});
				job.prefixes.Do(delegate(HarmonyMethod patch)
				{
					patchInfo.RemovePatch(patch.method);
				});
			}
			job.transpilers.Do(delegate(HarmonyMethod patch)
			{
				patchInfo.RemovePatch(patch.method);
			});
			if (flag)
			{
				job.finalizers.Do(delegate(HarmonyMethod patch)
				{
					patchInfo.RemovePatch(patch.method);
				});
			}
			MethodInfo replacement = PatchFunctions.UpdateWrapper(job.original, patchInfo);
			HarmonySharedState.UpdatePatchInfo(job.original, replacement, patchInfo);
		}

		private List<MethodBase> GetBulkMethods()
		{
			if (containerType.GetCustomAttributes(inherit: true).Any((object a) => a.GetType().FullName == PatchTools.harmonyPatchAllFullName))
			{
				Type declaringType = containerAttributes.declaringType;
				if ((object)declaringType == null)
				{
					throw new ArgumentException("Using " + PatchTools.harmonyPatchAllFullName + " requires an additional attribute for specifying the Class/Type");
				}
				List<MethodBase> list = new List<MethodBase>();
				list.AddRange(AccessTools.GetDeclaredConstructors(declaringType).Cast<MethodBase>());
				list.AddRange(AccessTools.GetDeclaredMethods(declaringType).Cast<MethodBase>());
				List<PropertyInfo> declaredProperties = AccessTools.GetDeclaredProperties(declaringType);
				list.AddRange((from prop in declaredProperties
					select prop.GetGetMethod(nonPublic: true) into method
					where (object)method != null
					select method).Cast<MethodBase>());
				list.AddRange((from prop in declaredProperties
					select prop.GetSetMethod(nonPublic: true) into method
					where (object)method != null
					select method).Cast<MethodBase>());
				return list;
			}
			List<MethodBase> list2 = new List<MethodBase>();
			IEnumerable<MethodBase> enumerable = RunMethod<HarmonyTargetMethods, IEnumerable<MethodBase>>(null, null, null, Array.Empty<object>());
			if (enumerable != null)
			{
				string text = null;
				list2 = enumerable.ToList();
				if (list2 == null)
				{
					text = "null";
				}
				else if (list2.Any((MethodBase m) => (object)m == null))
				{
					text = "some element was null";
				}
				if (text != null)
				{
					if (auxilaryMethods.TryGetValue(typeof(HarmonyTargetMethods), out var value))
					{
						throw new Exception("Method " + value.FullDescription() + " returned an unexpected result: " + text);
					}
					throw new Exception("Some method returned an unexpected result: " + text);
				}
				return list2;
			}
			MethodBase methodBase = RunMethod<HarmonyTargetMethod, MethodBase>(null, null, (MethodBase method) => ((object)method != null) ? null : "null", Array.Empty<object>());
			if ((object)methodBase != null)
			{
				list2.Add(methodBase);
			}
			return list2;
		}

		private void ReportException(Exception exception, MethodBase original)
		{
			if (exception == null)
			{
				return;
			}
			if (containerAttributes.debug == true || Harmony.DEBUG)
			{
				Harmony.VersionInfo(out var currentVersion);
				FileLog.indentLevel = 0;
				FileLog.Log($"### Exception from user \"{instance.Id}\", Harmony v{currentVersion}");
				FileLog.Log("### Original: " + (original?.FullDescription() ?? "NULL"));
				FileLog.Log("### Patch class: " + containerType.FullDescription());
				Exception ex = exception;
				if (ex is HarmonyException ex2)
				{
					ex = ex2.InnerException;
				}
				string text = ex.ToString();
				while (text.Contains("\n\n"))
				{
					text = text.Replace("\n\n", "\n");
				}
				text = text.Split('\n').Join((string line) => "### " + line, "\n");
				FileLog.Log(text.Trim());
			}
			if (exception is HarmonyException)
			{
				throw exception;
			}
			throw new HarmonyException("Patching exception in method " + original.FullDescription(), exception);
		}

		private T RunMethod<S, T>(T defaultIfNotExisting, T defaultIfFailing, Func<T, string> failOnResult = null, params object[] parameters)
		{
			if (auxilaryMethods.TryGetValue(typeof(S), out var value))
			{
				object[] inputs = (parameters ?? Array.Empty<object>()).Union(new object[1] { instance }).ToArray();
				object[] parameters2 = AccessTools.ActualParameters(value, inputs);
				if (value.ReturnType != typeof(void) && !typeof(T).IsAssignableFrom(value.ReturnType))
				{
					throw new Exception($"Method {value.FullDescription()} has wrong return type (should be assignable to {typeof(T).FullName})");
				}
				T val = defaultIfFailing;
				try
				{
					if (value.ReturnType == typeof(void))
					{
						value.Invoke(null, parameters2);
						val = defaultIfNotExisting;
					}
					else
					{
						val = (T)value.Invoke(null, parameters2);
					}
					if (failOnResult != null)
					{
						string text = failOnResult(val);
						if (text != null)
						{
							throw new Exception("Method " + value.FullDescription() + " returned an unexpected result: " + text);
						}
					}
				}
				catch (Exception exception)
				{
					ReportException(exception, value);
				}
				return val;
			}
			return defaultIfNotExisting;
		}

		private void RunMethod<S>(ref Exception exception, params object[] parameters)
		{
			if (!auxilaryMethods.TryGetValue(typeof(S), out var value))
			{
				return;
			}
			object[] inputs = (parameters ?? Array.Empty<object>()).Union(new object[1] { instance }).ToArray();
			object[] parameters2 = AccessTools.ActualParameters(value, inputs);
			try
			{
				object obj = value.Invoke(null, parameters2);
				if (value.ReturnType == typeof(Exception))
				{
					exception = obj as Exception;
				}
			}
			catch (Exception exception2)
			{
				ReportException(exception2, value);
			}
		}
	}
	public class Patches
	{
		public readonly System.Collections.ObjectModel.ReadOnlyCollection<Patch> Prefixes;

		public readonly System.Collections.ObjectModel.ReadOnlyCollection<Patch> Postfixes;

		public readonly System.Collections.ObjectModel.ReadOnlyCollection<Patch> Transpilers;

		public readonly System.Collections.ObjectModel.ReadOnlyCollection<Patch> Finalizers;

		public readonly System.Collections.ObjectModel.ReadOnlyCollection<Patch> InnerPrefixes;

		public readonly System.Collections.ObjectModel.ReadOnlyCollection<Patch> InnerPostfixes;

		public System.Collections.ObjectModel.ReadOnlyCollection<string> Owners
		{
			get
			{
				HashSet<string> hashSet = new HashSet<string>();
				hashSet.UnionWith(Prefixes.Select((Patch p) => p.owner));
				hashSet.UnionWith(Postfixes.Select((Patch p) => p.owner));
				hashSet.UnionWith(Transpilers.Select((Patch p) => p.owner));
				hashSet.UnionWith(Finalizers.Select((Patch p) => p.owner));
				hashSet.UnionWith(InnerPrefixes.Select((Patch p) => p.owner));
				hashSet.UnionWith(InnerPostfixes.Select((Patch p) => p.owner));
				return hashSet.ToList().AsReadOnly();
			}
		}

		public Patches(Patch[] prefixes, Patch[] postfixes, Patch[] transpilers, Patch[] finalizers, Patch[] innerprefixes, Patch[] innerpostfixes)
		{
			if (prefixes == null)
			{
				prefixes = Array.Empty<Patch>();
			}
			if (postfixes == null)
			{
				postfixes = Array.Empty<Patch>();
			}
			if (transpilers == null)
			{
				transpilers = Array.Empty<Patch>();
			}
			if (finalizers == null)
			{
				finalizers = Array.Empty<Patch>();
			}
			if (innerprefixes == null)
			{
				innerprefixes = Array.Empty<Patch>();
			}
			if (innerpostfixes == null)
			{
				innerpostfixes = Array.Empty<Patch>();
			}
			Prefixes = prefixes.ToList().AsReadOnly();
			Postfixes = postfixes.ToList().AsReadOnly();
			Transpilers = transpilers.ToList().AsReadOnly();
			Finalizers = finalizers.ToList().AsReadOnly();
			InnerPrefixes = innerprefixes.ToList().AsReadOnly();
			InnerPostfixes = innerpostfixes.ToList().AsReadOnly();
		}
	}
	[Serializable]
	public class PatchInfo
	{
		public Patch[] prefixes = Array.Empty<Patch>();

		public Patch[] postfixes = Array.Empty<Patch>();

		public Patch[] transpilers = Array.Empty<Patch>();

		public Patch[] finalizers = Array.Empty<Patch>();

		public Patch[] innerprefixes = Array.Empty<Patch>();

		public Patch[] innerpostfixes = Array.Empty<Patch>();

		public int VersionCount;

		public bool Debugging
		{
			get
			{
				if (!prefixes.Any((Patch p) => p.debug) && !postfixes.Any((Patch p) => p.debug) && !transpilers.Any((Patch p) => p.debug) && !finalizers.Any((Patch p) => p.debug) && !innerprefixes.Any((Patch p) => p.debug))
				{
					return innerpostfixes.Any((Patch p) => p.debug);
				}
				return true;
			}
		}

		internal void AddPrefixes(string owner, params HarmonyMethod[] methods)
		{
			prefixes = Add(owner, methods, prefixes);
		}

		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void AddPrefix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			AddPrefixes(owner, new HarmonyMethod(patch, priority, before, after, debug));
		}

		public void RemovePrefix(string owner)
		{
			prefixes = Remove(owner, prefixes);
		}

		internal void AddPostfixes(string owner, params HarmonyMethod[] methods)
		{
			postfixes = Add(owner, methods, postfixes);
		}

		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void AddPostfix(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			AddPostfixes(owner, new HarmonyMethod(patch, priority, before, after, debug));
		}

		public void RemovePostfix(string owner)
		{
			postfixes = Remove(owner, postfixes);
		}

		internal void AddTranspilers(string owner, params HarmonyMethod[] methods)
		{
			transpilers = Add(owner, methods, transpilers);
		}

		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void AddTranspiler(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			AddTranspilers(owner, new HarmonyMethod(patch, priority, before, after, debug));
		}

		public void RemoveTranspiler(string owner)
		{
			transpilers = Remove(owner, transpilers);
		}

		internal void AddFinalizers(string owner, params HarmonyMethod[] methods)
		{
			finalizers = Add(owner, methods, finalizers);
		}

		[Obsolete("This method only exists for backwards compatibility since the class is public.")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public void AddFinalizer(MethodInfo patch, string owner, int priority, string[] before, string[] after, bool debug)
		{
			AddFinalizers(owner, new HarmonyMethod(patch, priority, before, after, debug));
		}

		public void RemoveFinalizer(string owner)
		{
			finalizers = Remove(owner, finalizers);
		}

		internal void AddInnerPrefixes(string owner, params HarmonyMethod[] methods)
		{
			innerprefixes = Add(owner, methods, innerprefixes);
		}

		public void RemoveInnerPrefix(string owner)
		{
			innerprefixes = Remove(owner, innerprefixes);
		}

		internal void AddInnerPostfixes(string owner, params HarmonyMethod[] methods)
		{
			innerpostfixes = Add(owner, methods, innerpostfixes);
		}

		public void RemoveInnerPostfix(string owner)
		{
			innerpostfixes = Remove(owner, innerpostfixes);
		}

		public void RemovePatch(MethodInfo patch)
		{
			prefixes = prefixes.Where((Patch p) => p.PatchMethod != patch).ToArray();
			postfixes = postfixes.Where((Patch p) => p.PatchMethod != patch).ToArray();
			transpilers = transpilers.Where((Patch p) => p.PatchMethod != patch).ToArray();
			finalizers = finalizers.Where((Patch p) => p.PatchMethod != patch).ToArray();
			innerprefixes = innerprefixes.Where((Patch p) => p.PatchMethod != patch).ToArray();
			innerpostfixes = innerpostfixes.Where((Patch p) => p.PatchMethod != patch).ToArray();
		}

		private static Patch[] Add(string owner, HarmonyMethod[] add, Patch[] current)
		{
			if (add.Length == 0)
			{
				return current;
			}
			int initialIndex = current.Length;
			List<Patch> list = new List<Patch>();
			list.AddRange(current);
			list.AddRange(add.Where((HarmonyMethod method) => method != null).Select((HarmonyMethod method, int i) => new Patch(method, i + initialIndex, owner)));
			return list.ToArray();
		}

		private static Patch[] Remove(string owner, Patch[] current)
		{
			if (!(owner == "*"))
			{
				return current.Where((Patch patch) => patch.owner != owner).ToArray();
			}
			return Array.Empty<Patch>();
		}
	}
	public class PatchProcessor
	{
		private readonly Harmony instance;

		private readonly MethodBase original;

		private HarmonyMethod prefix;

		private HarmonyMethod postfix;

		private HarmonyMethod transpiler;

		private HarmonyMethod finalizer;

		private HarmonyMethod innerprefix;

		private HarmonyMethod innerpostfix;

		internal static readonly object locker = new object();

		public PatchProcessor(Harmony instance, MethodBase original)
		{
			this.instance = instance;
			this.original = original;
		}

		public PatchProcessor AddPrefix(HarmonyMethod prefix)
		{
			this.prefix = prefix;
			return this;
		}

		public PatchProcessor AddPrefix(MethodInfo fixMethod)
		{
			prefix = new HarmonyMethod(fixMethod);
			return this;
		}

		public PatchProcessor AddPostfix(HarmonyMethod postfix)
		{
			this.postfix = postfix;
			return this;
		}

		public PatchProcessor AddPostfix(MethodInfo fixMethod)
		{
			postfix = new HarmonyMethod(fixMethod);
			return this;
		}

		public PatchProcessor AddTranspiler(HarmonyMethod transpiler)
		{
			this.transpiler = transpiler;
			return this;
		}

		public PatchProcessor AddTranspiler(MethodInfo fixMethod)
		{
			transpiler = new HarmonyMethod(fixMethod);
			return this;
		}

		public PatchProcessor AddFinalizer(HarmonyMethod finalizer)
		{
			this.finalizer = finalizer;
			return this;
		}

		public PatchProcessor AddFinalizer(MethodInfo fixMethod)
		{
			finalizer = new HarmonyMethod(fixMethod);
			return this;
		}

		public PatchProcessor AddInnerPrefix(HarmonyMethod innerPrefix)
		{
			innerprefix = innerPrefix;
			return this;
		}

		public PatchProcessor AddInnerPrefix(MethodInfo fixMethod)
		{
			innerprefix = new HarmonyMethod(fixMethod);
			return this;
		}

		public PatchProcessor AddInnerPostfix(HarmonyMethod innerPostfix)
		{
			innerpostfix = innerPostfix;
			return this;
		}

		public PatchProcessor AddInnerPostfix(MethodInfo fixMethod)
		{
			innerpostfix = new HarmonyMethod(fixMethod);
			return this;
		}

		public static IEnumerable<MethodBase> GetAllPatchedMethods()
		{
			lock (locker)
			{
				return HarmonySharedState.GetPatchedMethods();
			}
		}

		public MethodInfo Patch()
		{
			if ((object)original == null)
			{
				throw new NullReferenceException("Null method for " + instance.Id);
			}
			if (!original.IsDeclaredMember())
			{
				MethodBase declaredMember = original.GetDeclaredMember();
				throw new ArgumentException("You can only patch implemented methods/constructors. Patch the declared method " + declaredMember.FullDescription() + " instead.");
			}
			lock (locker)
			{
				PatchInfo patchInfo = HarmonySharedState.GetPatchInfo(original) ?? new PatchInfo();
				patchInfo.AddPrefixes(instance.Id, prefix);
				patchInfo.AddPostfixes(instance.Id, postfix);
				patchInfo.AddTranspilers(instance.Id, transpiler);
				patchInfo.AddFinalizers(instance.Id, finalizer);
				patchInfo.AddInnerPrefixes(instance.Id, innerprefix);
				patchInfo.AddInnerPostfixes(instance.Id, innerpostfix);
				MethodInfo methodInfo = PatchFunctions.UpdateWrapper(original, patchInfo);
				HarmonySharedState.UpdatePatchInfo(original, methodInfo, patchInfo);
				return methodInfo;
			}
		}

		public PatchProcessor Unpatch(HarmonyPatchType type, string harmonyID)
		{
			if ((object)original == null)
			{
				throw new NullReferenceException("Null method for " + instance.Id);
			}
			lock (locker)
			{
				PatchInfo patchInfo = HarmonySharedState.GetPatchInfo(original);
				if (patchInfo == null)
				{
					patchInfo = new PatchInfo();
				}
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Prefix)
				{
					patchInfo.RemovePrefix(harmonyID);
				}
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Postfix)
				{
					patchInfo.RemovePostfix(harmonyID);
				}
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Transpiler)
				{
					patchInfo.RemoveTranspiler(harmonyID);
				}
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.Finalizer)
				{
					patchInfo.RemoveFinalizer(harmonyID);
				}
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.InnerPrefix)
				{
					patchInfo.RemoveInnerPrefix(harmonyID);
				}
				if (type == HarmonyPatchType.All || type == HarmonyPatchType.InnerPostfix)
				{
					patchInfo.RemoveInnerPostfix(harmonyID);
				}
				MethodInfo replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
				HarmonySharedState.UpdatePatchInfo(original, replacement, patchInfo);
				return this;
			}
		}

		public PatchProcessor Unpatch(MethodInfo patch)
		{
			if ((object)original == null)
			{
				throw new NullReferenceException("Null method for " + instance.Id);
			}
			lock (locker)
			{
				PatchInfo patchInfo = HarmonySharedState.GetPatchInfo(original);
				if (patchInfo == null)
				{
					patchInfo = new PatchInfo();
				}
				patchInfo.RemovePatch(patch);
				MethodInfo replacement = PatchFunctions.UpdateWrapper(original, patchInfo);
				HarmonySharedState.UpdatePatchInfo(original, replacement, patchInfo);
				return this;
			}
		}

		public static Patches GetPatchInfo(MethodBase method)
		{
			PatchInfo patchInfo;
			lock (locker)
			{
				patchInfo = HarmonySharedState.GetPatchInfo(method);
			}
			if (patchInfo == null)
			{
				return null;
			}
			return new Patches(patchInfo.prefixes, patchInfo.postfixes, patchInfo.transpilers, patchInfo.finalizers, patchInfo.innerprefixes, patchInfo.innerpostfixes);
		}

		public static List<MethodInfo> GetSortedPatchMethods(MethodBase original, Patch[] patches)
		{
			return PatchFunctions.GetSortedPatchMethods(original, patches, debug: false);
		}

		public static Dictionary<string, Version> VersionInfo(out Version currentVersion)
		{
			currentVersion = typeof(Harmony).Assembly.GetName().Version;
			Dictionary<string, Assembly> assemblies = new Dictionary<string, Assembly>();
			GetAllPatchedMethods().Do(delegate(MethodBase method)
			{
				PatchInfo patchInfo;
				lock (locker)
				{
					patchInfo = HarmonySharedState.GetPatchInfo(method);
				}
				patchInfo.prefixes.Do(delegate(Patch fix)
				{
					assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
				});
				patchInfo.postfixes.Do(delegate(Patch fix)
				{
					assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
				});
				patchInfo.transpilers.Do(delegate(Patch fix)
				{
					assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
				});
				patchInfo.finalizers.Do(delegate(Patch fix)
				{
					assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
				});
				patchInfo.innerprefixes.Do(delegate(Patch fix)
				{
					assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
				});
				patchInfo.innerpostfixes.Do(delegate(Patch fix)
				{
					assemblies[fix.owner] = fix.PatchMethod.DeclaringType.Assembly;
				});
			});
			Dictionary<string, Version> result = new Dictionary<string, Version>();
			assemblies.Do(delegate(KeyValuePair<string, Assembly> info)
			{
				AssemblyName assemblyName = info.Value.GetReferencedAssemblies().FirstOrDefault((AssemblyName a) => a.FullName.StartsWith("0Harmony, Version", StringComparison.Ordinal));
				if (assemblyName != null)
				{
					result[info.Key] = assemblyName.Version;
				}
			});
			return result;
		}

		public static ILGenerator CreateILGenerator()
		{
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition($"ILGenerator_{Guid.NewGuid()}", typeof(void), Array.Empty<Type>());
			return dynamicMethodDefinition.GetILGenerator();
		}

		public static ILGenerator CreateILGenerator(MethodBase original)
		{
			Type returnType = ((original is MethodInfo methodInfo) ? methodInfo.ReturnType : typeof(void));
			List<Type> list = (from pi in original.GetParameters()
				select pi.ParameterType).ToList();
			if (!original.IsStatic)
			{
				list.Insert(0, original.DeclaringType);
			}
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("ILGenerator_" + original.Name, returnType, list.ToArray());
			return dynamicMethodDefinition.GetILGenerator();
		}

		public static List<CodeInstruction> GetOriginalInstructions(MethodBase original, ILGenerator generator = null)
		{
			return MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, 0);
		}

		public static List<CodeInstruction> GetOriginalInstructions(MethodBase original, out ILGenerator generator)
		{
			generator = CreateILGenerator(original);
			return MethodCopier.GetInstructions(generator, original, 0);
		}

		public static List<CodeInstruction> GetCurrentInstructions(MethodBase original, int maxTranspilers = int.MaxValue, ILGenerator generator = null)
		{
			return MethodCopier.GetInstructions(generator ?? CreateILGenerator(original), original, maxTranspilers);
		}

		public static List<CodeInstruction> GetCurrentInstructions(MethodBase original, out ILGenerator generator, int maxTranspilers = int.MaxValue)
		{
			generator = CreateILGenerator(original);
			return MethodCopier.GetInstructions(generator, original, maxTranspilers);
		}

		public static IEnumerable<KeyValuePair<System.Reflection.Emit.OpCode, object>> ReadMethodBody(MethodBase method)
		{
			return from instr in MethodBodyReader.GetInstructions(CreateILGenerator(method), method)
				select new KeyValuePair<System.Reflection.Emit.OpCode, object>(instr.opcode, instr.operand);
		}

		public static IEnumerable<KeyValuePair<System.Reflection.Emit.OpCode, object>> ReadMethodBody(MethodBase method, ILGenerator generator)
		{
			return from instr in MethodBodyReader.GetInstructions(generator, method)
				select new KeyValuePair<System.Reflection.Emit.OpCode, object>(instr.opcode, instr.operand);
		}
	}
	public static class Priority
	{
		public const int Last = 0;

		public const int VeryLow = 100;

		public const int Low = 200;

		public const int LowerThanNormal = 300;

		public const int Normal = 400;

		public const int HigherThanNormal = 500;

		public const int High = 600;

		public const int VeryHigh = 700;

		public const int First = 800;
	}
	public class ReversePatcher
	{
		private readonly Harmony instance;

		private readonly MethodBase original;

		private readonly HarmonyMethod standin;

		public ReversePatcher(Harmony instance, MethodBase original, HarmonyMethod standin)
		{
			this.instance = instance;
			this.original = original;
			this.standin = standin;
		}

		public MethodInfo Patch(HarmonyReversePatchType type = HarmonyReversePatchType.Original)
		{
			if ((object)original == null)
			{
				throw new NullReferenceException("Null method for " + instance.Id);
			}
			standin.reversePatchType = type;
			MethodInfo transpiler = GetTranspiler(standin.method);
			return PatchFunctions.ReversePatch(standin, original, transpiler);
		}

		internal static MethodInfo GetTranspiler(MethodInfo method)
		{
			string methodName = method.Name;
			Type declaringType = method.DeclaringType;
			List<MethodInfo> declaredMethods = AccessTools.GetDeclaredMethods(declaringType);
			Type ici = typeof(IEnumerable<CodeInstruction>);
			return declaredMethods.FirstOrDefault((MethodInfo m) => !(m.ReturnType != ici) && m.Name.StartsWith("<" + methodName + ">"));
		}
	}
	public static class Transpilers
	{
		public static IEnumerable<CodeInstruction> MethodReplacer(this IEnumerable<CodeInstruction> instructions, MethodBase from, MethodBase to)
		{
			if ((object)from == null)
			{
				throw new ArgumentException("Unexpected null argument", "from");
			}
			if ((object)to == null)
			{
				throw new ArgumentException("Unexpected null argument", "to");
			}
			foreach (CodeInstruction instruction in instructions)
			{
				MethodBase methodBase = instruction.operand as MethodBase;
				if (methodBase == from)
				{
					instruction.opcode = (to.IsConstructor ? System.Reflection.Emit.OpCodes.Newobj : System.Reflection.Emit.OpCodes.Call);
					instruction.operand = to;
				}
				yield return instruction;
			}
		}

		public static IEnumerable<CodeInstruction> Manipulator(this IEnumerable<CodeInstruction> instructions, Func<CodeInstruction, bool> predicate, Action<CodeInstruction> action)
		{
			if (predicate == null)
			{
				throw new ArgumentNullException("predicate");
			}
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}
			return instructions.Select(delegate(CodeInstruction instruction)
			{
				if (predicate(instruction))
				{
					action(instruction);
				}
				return instruction;
			}).AsEnumerable();
		}

		public static IEnumerable<CodeInstruction> DebugLogger(this IEnumerable<CodeInstruction> instructions, string text)
		{
			yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Ldstr, text);
			yield return new CodeInstruction(System.Reflection.Emit.OpCodes.Call, AccessTools.Method(typeof(FileLog), "Debug"));
			foreach (CodeInstruction instruction in instructions)
			{
				yield return instruction;
			}
		}
	}
	internal static class PatchInfoSerialization
	{
		private class Binder : SerializationBinder
		{
			public override Type BindToType(string assemblyName, string typeName)
			{
				Type[] array = new Type[3]
				{
					typeof(PatchInfo),
					typeof(Patch[]),
					typeof(Patch)
				};
				Type[] array2 = array;
				foreach (Type type in array2)
				{
					if (typeName == type.FullName)
					{
						return type;
					}
				}
				return Type.GetType($"{typeName}, {assemblyName}");
			}
		}

		internal static readonly BinaryFormatter binaryFormatter = new BinaryFormatter
		{
			Binder = new Binder()
		};

		internal static byte[] Serialize(this PatchInfo patchInfo)
		{
			using MemoryStream memoryStream = new MemoryStream();
			binaryFormatter.Serialize(memoryStream, patchInfo);
			return memoryStream.ToArray();
		}

		internal static PatchInfo Deserialize(byte[] bytes)
		{
			using MemoryStream serializationStream = new MemoryStream(bytes);
			return (PatchInfo)binaryFormatter.Deserialize(serializationStream);
		}

		internal static int PriorityComparer(object obj, int index, int priority)
		{
			Traverse traverse = Traverse.Create(obj);
			int value = traverse.Field("priority").GetValue<int>();
			int value2 = traverse.Field("index").GetValue<int>();
			if (priority != value)
			{
				return -priority.CompareTo(value);
			}
			return index.CompareTo(value2);
		}
	}
	public static class AccessTools
	{
		public delegate ref F FieldRef<in T, F>(T instance = default(T));

		public delegate ref F StructFieldRef<T, F>(ref T instance) where T : struct;

		public delegate ref F FieldRef<F>();

		private static Type[] allTypesCached = null;

		public static readonly BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.SetField | BindingFlags.GetProperty | BindingFlags.SetProperty;

		public static readonly BindingFlags allDeclared = all | BindingFlags.DeclaredOnly;

		private static readonly Dictionary<Type, FastInvokeHandler> addHandlerCache = new Dictionary<Type, FastInvokeHandler>();

		private static readonly ReaderWriterLockSlim addHandlerCacheLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);

		public static bool IsMonoRuntime { get; } = (object)Type.GetType("Mono.Runtime") != null;

		public static bool IsNetFrameworkRuntime { get; } = Type.GetType("System.Runtime.InteropServices.RuntimeInformation", throwOnError: false)?.GetProperty("FrameworkDescription").GetValue(null, null).ToString()
			.StartsWith(".NET Framework") ?? (!IsMonoRuntime);

		public static bool IsNetCoreRuntime { get; } = Type.GetType("System.Runtime.InteropServices.RuntimeInformation", throwOnError: false)?.GetProperty("FrameworkDescription").GetValue(null, null).ToString()
			.StartsWith(".NET Core") ?? false;

		public static IEnumerable<Assembly> AllAssemblies()
		{
			return from a in AppDomain.CurrentDomain.GetAssemblies()
				where !a.FullName.StartsWith("Microsoft.VisualStudio")
				select a;
		}

		public static Type TypeByName(string name)
		{
			Type type = Type.GetType(name, throwOnError: false);
			if ((object)type != null)
			{
				return type;
			}
			foreach (Assembly item in AllAssemblies())
			{
				Type type2 = item.GetType(name, throwOnError: false);
				if ((object)type2 != null)
				{
					return type2;
				}
			}
			Type[] source = AllTypes().ToArray();
			Type type3 = source.FirstOrDefault((Type t) => t.FullName == name);
			if ((object)type3 != null)
			{
				return type3;
			}
			Type type4 = source.FirstOrDefault((Type t) => t.Name == name);
			if ((object)type4 != null)
			{
				return type4;
			}
			FileLog.Debug("AccessTools.TypeByName: Could not find type named " + name);
			return null;
		}

		public static Type TypeSearch(Regex search, bool invalidateCache = false)
		{
			if (allTypesCached == null || invalidateCache)
			{
				allTypesCached = AllTypes().ToArray();
			}
			Type type = allTypesCached.FirstOrDefault((Type t) => search.IsMatch(t.FullName));
			if ((object)type != null)
			{
				return type;
			}
			Type type2 = allTypesCached.FirstOrDefault((Type t) => search.IsMatch(t.Name));
			if ((object)type2 != null)
			{
				return type2;
			}
			FileLog.Debug($"AccessTools.TypeSearch: Could not find type with regular expression {search}");
			return null;
		}

		public static void ClearTypeSearchCache()
		{
			allTypesCached = null;
		}

		public static Type[] GetTypesFromAssembly(Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException ex)
			{
				FileLog.Debug($"AccessTools.GetTypesFromAssembly: assembly {assembly} => {ex}");
				return ex.Types.Where((Type type) => (object)type != null).ToArray();
			}
		}

		public static IEnumerable<Type> AllTypes()
		{
			return AllAssemblies().SelectMany(GetTypesFromAssembly);
		}

		public static IEnumerable<Type> InnerTypes(Type type)
		{
			return type.GetNestedTypes(all);
		}

		public static T FindIncludingBaseTypes<T>(Type type, Func<Type, T> func) where T : class
		{
			do
			{
				T val = func(type);
				if (val != null)
				{
					return val;
				}
				type = type.BaseType;
			}
			while ((object)type != null);
			return null;
		}

		public static T FindIncludingInnerTypes<T>(Type type, Func<Type, T> func) where T : class
		{
			T val = func(type);
			if (val != null)
			{
				return val;
			}
			Type[] nestedTypes = type.GetNestedTypes(all);
			foreach (Type type2 in nestedTypes)
			{
				val = FindIncludingInnerTypes(type2, func);
				if (val != null)
				{
					break;
				}
			}
			return val;
		}

		public static MethodInfo Identifiable(this MethodInfo method)
		{
			return (PlatformTriple.Current.GetIdentifiable(method) as MethodInfo) ?? method;
		}

		public static FieldInfo DeclaredField(Type type, string name)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.DeclaredField: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.DeclaredField: name is null/empty");
				return null;
			}
			FieldInfo field = type.GetField(name, allDeclared);
			if ((object)field == null)
			{
				FileLog.Debug($"AccessTools.DeclaredField: Could not find field for type {type} and name {name}");
			}
			return field;
		}

		public static FieldInfo DeclaredField(string typeColonName)
		{
			Tools.TypeAndName typeAndName = Tools.TypColonName(typeColonName);
			FieldInfo field = typeAndName.type.GetField(typeAndName.name, allDeclared);
			if ((object)field == null)
			{
				FileLog.Debug($"AccessTools.DeclaredField: Could not find field for type {typeAndName.type} and name {typeAndName.name}");
			}
			return field;
		}

		public static FieldInfo Field(Type type, string name)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.Field: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.Field: name is null/empty");
				return null;
			}
			FieldInfo fieldInfo = FindIncludingBaseTypes(type, (Type t) => t.GetField(name, all));
			if ((object)fieldInfo == null)
			{
				FileLog.Debug($"AccessTools.Field: Could not find field for type {type} and name {name}");
			}
			return fieldInfo;
		}

		public static FieldInfo Field(string typeColonName)
		{
			Tools.TypeAndName info = Tools.TypColonName(typeColonName);
			FieldInfo fieldInfo = FindIncludingBaseTypes(info.type, (Type t) => t.GetField(info.name, all));
			if ((object)fieldInfo == null)
			{
				FileLog.Debug($"AccessTools.Field: Could not find field for type {info.type} and name {info.name}");
			}
			return fieldInfo;
		}

		public static FieldInfo DeclaredField(Type type, int idx)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.DeclaredField: type is null");
				return null;
			}
			FieldInfo fieldInfo = GetDeclaredFields(type).ElementAtOrDefault(idx);
			if ((object)fieldInfo == null)
			{
				FileLog.Debug($"AccessTools.DeclaredField: Could not find field for type {type} and idx {idx}");
			}
			return fieldInfo;
		}

		public static PropertyInfo DeclaredProperty(Type type, string name)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.DeclaredProperty: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.DeclaredProperty: name is null/empty");
				return null;
			}
			PropertyInfo property = type.GetProperty(name, allDeclared);
			if ((object)property == null)
			{
				FileLog.Debug($"AccessTools.DeclaredProperty: Could not find property for type {type} and name {name}");
			}
			return property;
		}

		public static PropertyInfo DeclaredProperty(string typeColonName)
		{
			Tools.TypeAndName typeAndName = Tools.TypColonName(typeColonName);
			PropertyInfo property = typeAndName.type.GetProperty(typeAndName.name, allDeclared);
			if ((object)property == null)
			{
				FileLog.Debug($"AccessTools.DeclaredProperty: Could not find property for type {typeAndName.type} and name {typeAndName.name}");
			}
			return property;
		}

		public static PropertyInfo DeclaredIndexer(Type type, Type[] parameters = null)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.DeclaredIndexer: type is null");
				return null;
			}
			try
			{
				PropertyInfo propertyInfo = ((parameters == null) ? type.GetProperties(allDeclared).SingleOrDefault((PropertyInfo property) => property.GetIndexParameters().Length != 0) : type.GetProperties(allDeclared).FirstOrDefault((PropertyInfo property) => (from param in property.GetIndexParameters()
					select param.ParameterType).SequenceEqual(parameters)));
				if ((object)propertyInfo == null)
				{
					FileLog.Debug($"AccessTools.DeclaredIndexer: Could not find indexer for type {type} and parameters {parameters?.Description()}");
				}
				return propertyInfo;
			}
			catch (InvalidOperationException inner)
			{
				throw new AmbiguousMatchException("Multiple possible indexers were found.", inner);
			}
		}

		public static MethodInfo DeclaredPropertyGetter(Type type, string name)
		{
			return DeclaredProperty(type, name)?.GetGetMethod(nonPublic: true);
		}

		public static MethodInfo DeclaredPropertyGetter(string typeColonName)
		{
			return DeclaredProperty(typeColonName)?.GetGetMethod(nonPublic: true);
		}

		public static MethodInfo DeclaredIndexerGetter(Type type, Type[] parameters = null)
		{
			return DeclaredIndexer(type, parameters)?.GetGetMethod(nonPublic: true);
		}

		public static MethodInfo DeclaredPropertySetter(Type type, string name)
		{
			return DeclaredProperty(type, name)?.GetSetMethod(nonPublic: true);
		}

		public static MethodInfo DeclaredPropertySetter(string typeColonName)
		{
			return DeclaredProperty(typeColonName)?.GetSetMethod(nonPublic: true);
		}

		public static MethodInfo DeclaredIndexerSetter(Type type, Type[] parameters)
		{
			return DeclaredIndexer(type, parameters)?.GetSetMethod(nonPublic: true);
		}

		public static PropertyInfo Property(Type type, string name)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.Property: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.Property: name is null/empty");
				return null;
			}
			PropertyInfo propertyInfo = FindIncludingBaseTypes(type, (Type t) => t.GetProperty(name, all));
			if ((object)propertyInfo == null)
			{
				FileLog.Debug($"AccessTools.Property: Could not find property for type {type} and name {name}");
			}
			return propertyInfo;
		}

		public static PropertyInfo Property(string typeColonName)
		{
			Tools.TypeAndName info = Tools.TypColonName(typeColonName);
			PropertyInfo propertyInfo = FindIncludingBaseTypes(info.type, (Type t) => t.GetProperty(info.name, all));
			if ((object)propertyInfo == null)
			{
				FileLog.Debug($"AccessTools.Property: Could not find property for type {info.type} and name {info.name}");
			}
			return propertyInfo;
		}

		public static PropertyInfo Indexer(Type type, Type[] parameters = null)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.Indexer: type is null");
				return null;
			}
			Func<Type, PropertyInfo> func = ((parameters == null) ? ((Func<Type, PropertyInfo>)((Type t) => t.GetProperties(all).SingleOrDefault((PropertyInfo property) => property.GetIndexParameters().Length != 0))) : ((Func<Type, PropertyInfo>)((Type t) => t.GetProperties(all).FirstOrDefault((PropertyInfo property) => (from param in property.GetIndexParameters()
				select param.ParameterType).SequenceEqual(parameters)))));
			try
			{
				PropertyInfo propertyInfo = FindIncludingBaseTypes(type, func);
				if ((object)propertyInfo == null)
				{
					FileLog.Debug($"AccessTools.Indexer: Could not find indexer for type {type} and parameters {parameters?.Description()}");
				}
				return propertyInfo;
			}
			catch (InvalidOperationException inner)
			{
				throw new AmbiguousMatchException("Multiple possible indexers were found.", inner);
			}
		}

		public static MethodInfo PropertyGetter(Type type, string name)
		{
			return Property(type, name)?.GetGetMethod(nonPublic: true);
		}

		public static MethodInfo PropertyGetter(string typeColonName)
		{
			return Property(typeColonName)?.GetGetMethod(nonPublic: true);
		}

		public static MethodInfo IndexerGetter(Type type, Type[] parameters = null)
		{
			return Indexer(type, parameters)?.GetGetMethod(nonPublic: true);
		}

		public static MethodInfo PropertySetter(Type type, string name)
		{
			return Property(type, name)?.GetSetMethod(nonPublic: true);
		}

		public static MethodInfo PropertySetter(string typeColonName)
		{
			return Property(typeColonName)?.GetSetMethod(nonPublic: true);
		}

		public static MethodInfo IndexerSetter(Type type, Type[] parameters = null)
		{
			return Indexer(type, parameters)?.GetSetMethod(nonPublic: true);
		}

		public static EventInfo DeclaredEvent(Type type, string name)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.DeclaredEvent: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.DeclaredEvent: name is null/empty");
				return null;
			}
			EventInfo eventInfo = type.GetEvent(name, allDeclared);
			if ((object)eventInfo == null)
			{
				FileLog.Debug($"AccessTools.DeclaredEvent: Could not find event for type {type} and name {name}");
			}
			return eventInfo;
		}

		public static EventInfo DeclaredEvent(string typeColonName)
		{
			Tools.TypeAndName typeAndName = Tools.TypColonName(typeColonName);
			EventInfo eventInfo = typeAndName.type.GetEvent(typeAndName.name, allDeclared);
			if ((object)eventInfo == null)
			{
				FileLog.Debug($"AccessTools.DeclaredEvent: Could not find event for type {typeAndName.type} and name {typeAndName.name}");
			}
			return eventInfo;
		}

		public static EventInfo Event(Type type, string name)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.Event: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.Event: name is null/empty");
				return null;
			}
			EventInfo eventInfo = FindIncludingBaseTypes(type, (Type t) => t.GetEvent(name, all));
			if ((object)eventInfo == null)
			{
				FileLog.Debug($"AccessTools.Event: Could not find event for type {type} and name {name}");
			}
			return eventInfo;
		}

		public static EventInfo Event(string typeColonName)
		{
			Tools.TypeAndName info = Tools.TypColonName(typeColonName);
			EventInfo eventInfo = FindIncludingBaseTypes(info.type, (Type t) => t.GetEvent(info.name, all));
			if ((object)eventInfo == null)
			{
				FileLog.Debug($"AccessTools.Event: Could not find event for type {info.type} and name {info.name}");
			}
			return eventInfo;
		}

		public static MethodInfo DeclaredEventAdder(Type type, string name)
		{
			return DeclaredEvent(type, name)?.GetAddMethod(nonPublic: true);
		}

		public static MethodInfo DeclaredEventAdder(string typeColonName)
		{
			return DeclaredEvent(typeColonName)?.GetAddMethod(nonPublic: true);
		}

		public static MethodInfo EventAdder(Type type, string name)
		{
			return Event(type, name)?.GetAddMethod(nonPublic: true);
		}

		public static MethodInfo EventAdder(string typeColonName)
		{
			return Event(typeColonName)?.GetAddMethod(nonPublic: true);
		}

		public static MethodInfo DeclaredEventRemover(Type type, string name)
		{
			return DeclaredEvent(type, name)?.GetRemoveMethod(nonPublic: true);
		}

		public static MethodInfo DeclaredEventRemover(string typeColonName)
		{
			return DeclaredEvent(typeColonName)?.GetRemoveMethod(nonPublic: true);
		}

		public static MethodInfo EventRemover(Type type, string name)
		{
			return Event(type, name)?.GetRemoveMethod(nonPublic: true);
		}

		public static MethodInfo EventRemover(string typeColonName)
		{
			return Event(typeColonName)?.GetRemoveMethod(nonPublic: true);
		}

		public static MethodInfo DeclaredMethod(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.DeclaredMethod: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.DeclaredMethod: name is null/empty");
				return null;
			}
			ParameterModifier[] modifiers = new ParameterModifier[0];
			MethodInfo methodInfo = ((parameters != null) ? type.GetMethod(name, allDeclared, null, parameters, modifiers) : type.GetMethod(name, allDeclared));
			if ((object)methodInfo == null)
			{
				FileLog.Debug($"AccessTools.DeclaredMethod: Could not find method for type {type} and name {name} and parameters {parameters?.Description()}");
				return null;
			}
			if (generics != null)
			{
				methodInfo = methodInfo.MakeGenericMethod(generics);
			}
			return methodInfo;
		}

		public static MethodInfo DeclaredMethod(string typeColonName, Type[] parameters = null, Type[] generics = null)
		{
			Tools.TypeAndName typeAndName = Tools.TypColonName(typeColonName);
			return DeclaredMethod(typeAndName.type, typeAndName.name, parameters, generics);
		}

		public static MethodInfo Method(Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.Method: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.Method: name is null/empty");
				return null;
			}
			ParameterModifier[] modifiers = new ParameterModifier[0];
			MethodInfo methodInfo;
			if (parameters == null)
			{
				try
				{
					methodInfo = FindIncludingBaseTypes(type, (Type t) => t.GetMethod(name, all));
				}
				catch (AmbiguousMatchException inner)
				{
					methodInfo = FindIncludingBaseTypes(type, (Type t) => t.GetMethod(name, all, null, Array.Empty<Type>(), modifiers));
					if ((object)methodInfo == null)
					{
						throw new AmbiguousMatchException($"Ambiguous match in Harmony patch for {type}:{name}", inner);
					}
				}
			}
			else
			{
				methodInfo = FindIncludingBaseTypes(type, (Type t) => t.GetMethod(name, all, null, parameters, modifiers));
			}
			if ((object)methodInfo == null)
			{
				FileLog.Debug($"AccessTools.Method: Could not find method for type {type} and name {name} and parameters {parameters?.Description()}");
				return null;
			}
			if (generics != null)
			{
				methodInfo = methodInfo.MakeGenericMethod(generics);
			}
			return methodInfo;
		}

		public static MethodInfo Method(string typeColonName, Type[] parameters = null, Type[] generics = null)
		{
			Tools.TypeAndName typeAndName = Tools.TypColonName(typeColonName);
			return Method(typeAndName.type, typeAndName.name, parameters, generics);
		}

		public static MethodInfo EnumeratorMoveNext(MethodBase method)
		{
			if ((object)method == null)
			{
				FileLog.Debug("AccessTools.EnumeratorMoveNext: method is null");
				return null;
			}
			IEnumerable<KeyValuePair<System.Reflection.Emit.OpCode, object>> source = from pair in PatchProcessor.ReadMethodBody(method)
				where pair.Key == System.Reflection.Emit.OpCodes.Newobj
				select pair;
			if (source.Count() != 1)
			{
				FileLog.Debug("AccessTools.EnumeratorMoveNext: " + method.FullDescription() + " contains no Newobj opcode");
				return null;
			}
			ConstructorInfo constructorInfo = source.First().Value as ConstructorInfo;
			if (constructorInfo == null)
			{
				FileLog.Debug("AccessTools.EnumeratorMoveNext: " + method.FullDescription() + " contains no constructor");
				return null;
			}
			Type declaringType = constructorInfo.DeclaringType;
			if (declaringType == null)
			{
				FileLog.Debug("AccessTools.EnumeratorMoveNext: " + method.FullDescription() + " refers to a global type");
				return null;
			}
			return Method(declaringType, "MoveNext");
		}

		public static MethodInfo AsyncMoveNext(MethodBase method)
		{
			if ((object)method == null)
			{
				FileLog.Debug("AccessTools.AsyncMoveNext: method is null");
				return null;
			}
			AsyncStateMachineAttribute customAttribute = method.GetCustomAttribute<AsyncStateMachineAttribute>();
			if (customAttribute == null)
			{
				FileLog.Debug("AccessTools.AsyncMoveNext: Could not find AsyncStateMachine for " + method.FullDescription());
				return null;
			}
			Type stateMachineType = customAttribute.StateMachineType;
			MethodInfo methodInfo = DeclaredMethod(stateMachineType, "MoveNext");
			if ((object)methodInfo == null)
			{
				FileLog.Debug("AccessTools.AsyncMoveNext: Could not find async method body for " + method.FullDescription());
				return null;
			}
			return methodInfo;
		}

		public static MethodInfo Finalizer(Type type)
		{
			return Method(type, "Finalize");
		}

		public static MethodInfo DeclaredFinalizer(Type type)
		{
			return DeclaredMethod(type, "Finalize");
		}

		public static List<string> GetMethodNames(Type type)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.GetMethodNames: type is null");
				return new List<string>();
			}
			return (from m in GetDeclaredMethods(type)
				select m.Name).ToList();
		}

		public static List<string> GetMethodNames(object instance)
		{
			if (instance == null)
			{
				FileLog.Debug("AccessTools.GetMethodNames: instance is null");
				return new List<string>();
			}
			return GetMethodNames(instance.GetType());
		}

		public static List<string> GetFieldNames(Type type)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.GetFieldNames: type is null");
				return new List<string>();
			}
			return (from f in GetDeclaredFields(type)
				select f.Name).ToList();
		}

		public static List<string> GetFieldNames(object instance)
		{
			if (instance == null)
			{
				FileLog.Debug("AccessTools.GetFieldNames: instance is null");
				return new List<string>();
			}
			return GetFieldNames(instance.GetType());
		}

		public static List<string> GetPropertyNames(Type type)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.GetPropertyNames: type is null");
				return new List<string>();
			}
			return (from f in GetDeclaredProperties(type)
				select f.Name).ToList();
		}

		public static List<string> GetPropertyNames(object instance)
		{
			if (instance == null)
			{
				FileLog.Debug("AccessTools.GetPropertyNames: instance is null");
				return new List<string>();
			}
			return GetPropertyNames(instance.GetType());
		}

		public static Type GetUnderlyingType(this MemberInfo member)
		{
			return member.MemberType switch
			{
				MemberTypes.Event => ((EventInfo)member).EventHandlerType, 
				MemberTypes.Field => ((FieldInfo)member).FieldType, 
				MemberTypes.Method => ((MethodInfo)member).ReturnType, 
				MemberTypes.Property => ((PropertyInfo)member).PropertyType, 
				_ => throw new ArgumentException("Member must be of type EventInfo, FieldInfo, MethodInfo, or PropertyInfo"), 
			};
		}

		public static MethodInfo GetMethodByModuleAndToken(string moduleGUID, int token)
		{
			Module module = (from a in AppDomain.CurrentDomain.GetAssemblies()
				where !a.FullName.StartsWith("Microsoft.VisualStudio")
				select a).SelectMany((Assembly a) => a.GetLoadedModules()).First((Module m) => m.ModuleVersionId.ToString() == moduleGUID);
			if (!(module == null))
			{
				return (MethodInfo)module.ResolveMethod(token);
			}
			return null;
		}

		public static bool IsDeclaredMember<T>(this T member) where T : MemberInfo
		{
			return member.DeclaringType == member.ReflectedType;
		}

		public static T GetDeclaredMember<T>(this T member) where T : MemberInfo
		{
			if ((object)member.DeclaringType == null || member.IsDeclaredMember())
			{
				return member;
			}
			int metadataToken = member.MetadataToken;
			MemberInfo[] array = member.DeclaringType?.GetMembers(all) ?? Array.Empty<MemberInfo>();
			MemberInfo[] array2 = array;
			foreach (MemberInfo memberInfo in array2)
			{
				if (memberInfo.MetadataToken == metadataToken)
				{
					return (T)memberInfo;
				}
			}
			return member;
		}

		public static ConstructorInfo DeclaredConstructor(Type type, Type[] parameters = null, bool searchForStatic = false)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.DeclaredConstructor: type is null");
				return null;
			}
			if (parameters == null)
			{
				parameters = Array.Empty<Type>();
			}
			BindingFlags bindingAttr = (searchForStatic ? (allDeclared & ~BindingFlags.Instance) : (allDeclared & ~BindingFlags.Static));
			return type.GetConstructor(bindingAttr, null, parameters, Array.Empty<ParameterModifier>());
		}

		public static ConstructorInfo Constructor(Type type, Type[] parameters = null, bool searchForStatic = false)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.ConstructorInfo: type is null");
				return null;
			}
			if (parameters == null)
			{
				parameters = Array.Empty<Type>();
			}
			BindingFlags flags = (searchForStatic ? (all & ~BindingFlags.Instance) : (all & ~BindingFlags.Static));
			return FindIncludingBaseTypes(type, (Type t) => t.GetConstructor(flags, null, parameters, Array.Empty<ParameterModifier>()));
		}

		public static List<ConstructorInfo> GetDeclaredConstructors(Type type, bool? searchForStatic = null)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.GetDeclaredConstructors: type is null");
				return new List<ConstructorInfo>();
			}
			BindingFlags bindingFlags = allDeclared;
			if (searchForStatic.HasValue)
			{
				bindingFlags = (searchForStatic.Value ? (bindingFlags & ~BindingFlags.Instance) : (bindingFlags & ~BindingFlags.Static));
			}
			return (from method in type.GetConstructors(bindingFlags)
				where method.DeclaringType == type
				select method).ToList();
		}

		public static List<MethodInfo> GetDeclaredMethods(Type type)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.GetDeclaredMethods: type is null");
				return new List<MethodInfo>();
			}
			return type.GetMethods(allDeclared).ToList();
		}

		public static List<PropertyInfo> GetDeclaredProperties(Type type)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.GetDeclaredProperties: type is null");
				return new List<PropertyInfo>();
			}
			return type.GetProperties(allDeclared).ToList();
		}

		public static List<FieldInfo> GetDeclaredFields(Type type)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.GetDeclaredFields: type is null");
				return new List<FieldInfo>();
			}
			return type.GetFields(allDeclared).ToList();
		}

		public static Type GetReturnedType(MethodBase methodOrConstructor)
		{
			if ((object)methodOrConstructor == null)
			{
				FileLog.Debug("AccessTools.GetReturnedType: methodOrConstructor is null");
				return null;
			}
			if (methodOrConstructor is ConstructorInfo)
			{
				return typeof(void);
			}
			return ((MethodInfo)methodOrConstructor).ReturnType;
		}

		public static Type Inner(Type type, string name)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.Inner: type is null");
				return null;
			}
			if (string.IsNullOrEmpty(name))
			{
				FileLog.Debug("AccessTools.Inner: name is null/empty");
				return null;
			}
			return FindIncludingBaseTypes(type, (Type t) => t.GetNestedType(name, all));
		}

		public static Type FirstInner(Type type, Func<Type, bool> predicate)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.FirstInner: type is null");
				return null;
			}
			if (predicate == null)
			{
				FileLog.Debug("AccessTools.FirstInner: predicate is null");
				return null;
			}
			return type.GetNestedTypes(all).FirstOrDefault((Type subType) => predicate(subType));
		}

		public static MethodInfo FirstMethod(Type type, Func<MethodInfo, bool> predicate)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.FirstMethod: type is null");
				return null;
			}
			if (predicate == null)
			{
				FileLog.Debug("AccessTools.FirstMethod: predicate is null");
				return null;
			}
			return type.GetMethods(allDeclared).FirstOrDefault((MethodInfo method) => predicate(method));
		}

		public static ConstructorInfo FirstConstructor(Type type, Func<ConstructorInfo, bool> predicate)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.FirstConstructor: type is null");
				return null;
			}
			if (predicate == null)
			{
				FileLog.Debug("AccessTools.FirstConstructor: predicate is null");
				return null;
			}
			return type.GetConstructors(allDeclared).FirstOrDefault((ConstructorInfo constructor) => predicate(constructor));
		}

		public static PropertyInfo FirstProperty(Type type, Func<PropertyInfo, bool> predicate)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.FirstProperty: type is null");
				return null;
			}
			if (predicate == null)
			{
				FileLog.Debug("AccessTools.FirstProperty: predicate is null");
				return null;
			}
			return type.GetProperties(allDeclared).FirstOrDefault((PropertyInfo property) => predicate(property));
		}

		public static Type[] GetTypes(object[] parameters)
		{
			if (parameters == null)
			{
				return Array.Empty<Type>();
			}
			return parameters.Select((object p) => (p != null) ? p.GetType() : typeof(object)).ToArray();
		}

		public static object[] ActualParameters(MethodBase method, object[] inputs)
		{
			List<Type> inputTypes = inputs.Select((object obj) => obj?.GetType()).ToList();
			return (from p in method.GetParameters()
				select p.ParameterType).Select(delegate(Type pType)
			{
				int num = inputTypes.FindIndex((Type inType) => (object)inType != null && pType.IsAssignableFrom(inType));
				return (num >= 0) ? inputs[num] : GetDefaultValue(pType);
			}).ToArray();
		}

		public static FieldRef<T, F> FieldRefAccess<T, F>(string fieldName)
		{
			if (fieldName == null)
			{
				throw new ArgumentNullException("fieldName");
			}
			try
			{
				Type typeFromHandle = typeof(T);
				if (typeFromHandle.IsValueType)
				{
					throw new ArgumentException("T (FieldRefAccess instance type) must not be a value type");
				}
				return Tools.FieldRefAccess<T, F>(Tools.GetInstanceField(typeFromHandle, fieldName), needCastclass: false);
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldName} caused an exception", innerException);
			}
		}

		public static ref F FieldRefAccess<T, F>(T instance, string fieldName)
		{
			if (instance == null)
			{
				throw new ArgumentNullException("instance");
			}
			if (fieldName == null)
			{
				throw new ArgumentNullException("fieldName");
			}
			try
			{
				Type typeFromHandle = typeof(T);
				if (typeFromHandle.IsValueType)
				{
					throw new ArgumentException("T (FieldRefAccess instance type) must not be a value type");
				}
				return ref Tools.FieldRefAccess<T, F>(Tools.GetInstanceField(typeFromHandle, fieldName), needCastclass: false)(instance);
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(T)}, {typeof(F)}> for {instance}, {fieldName} caused an exception", innerException);
			}
		}

		public static FieldRef<object, F> FieldRefAccess<F>(Type type, string fieldName)
		{
			if ((object)type == null)
			{
				throw new ArgumentNullException("type");
			}
			if (fieldName == null)
			{
				throw new ArgumentNullException("fieldName");
			}
			try
			{
				FieldInfo fieldInfo = Field(type, fieldName);
				if ((object)fieldInfo == null)
				{
					throw new MissingFieldException(type.Name, fieldName);
				}
				if (!fieldInfo.IsStatic)
				{
					Type declaringType = fieldInfo.DeclaringType;
					if ((object)declaringType != null && declaringType.IsValueType)
					{
						throw new ArgumentException("Either FieldDeclaringType must be a class or field must be static");
					}
				}
				return Tools.FieldRefAccess<object, F>(fieldInfo, needCastclass: true);
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(F)}> for {type}, {fieldName} caused an exception", innerException);
			}
		}

		public static FieldRef<object, F> FieldRefAccess<F>(string typeColonName)
		{
			Tools.TypeAndName typeAndName = Tools.TypColonName(typeColonName);
			return FieldRefAccess<F>(typeAndName.type, typeAndName.name);
		}

		public static FieldRef<T, F> FieldRefAccess<T, F>(FieldInfo fieldInfo)
		{
			if ((object)fieldInfo == null)
			{
				throw new ArgumentNullException("fieldInfo");
			}
			try
			{
				Type typeFromHandle = typeof(T);
				if (typeFromHandle.IsValueType)
				{
					throw new ArgumentException("T (FieldRefAccess instance type) must not be a value type");
				}
				bool needCastclass = false;
				if (!fieldInfo.IsStatic)
				{
					Type declaringType = fieldInfo.DeclaringType;
					if ((object)declaringType != null)
					{
						if (declaringType.IsValueType)
						{
							throw new ArgumentException("Either FieldDeclaringType must be a class or field must be static");
						}
						needCastclass = Tools.FieldRefNeedsClasscast(typeFromHandle, declaringType);
					}
				}
				return Tools.FieldRefAccess<T, F>(fieldInfo, needCastclass);
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldInfo} caused an exception", innerException);
			}
		}

		public static ref F FieldRefAccess<T, F>(T instance, FieldInfo fieldInfo)
		{
			if (instance == null)
			{
				throw new ArgumentNullException("instance");
			}
			if ((object)fieldInfo == null)
			{
				throw new ArgumentNullException("fieldInfo");
			}
			try
			{
				Type typeFromHandle = typeof(T);
				if (typeFromHandle.IsValueType)
				{
					throw new ArgumentException("T (FieldRefAccess instance type) must not be a value type");
				}
				if (fieldInfo.IsStatic)
				{
					throw new ArgumentException("Field must not be static");
				}
				bool needCastclass = false;
				Type declaringType = fieldInfo.DeclaringType;
				if ((object)declaringType != null)
				{
					if (declaringType.IsValueType)
					{
						throw new ArgumentException("FieldDeclaringType must be a class");
					}
					needCastclass = Tools.FieldRefNeedsClasscast(typeFromHandle, declaringType);
				}
				return ref Tools.FieldRefAccess<T, F>(fieldInfo, needCastclass)(instance);
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"FieldRefAccess<{typeof(T)}, {typeof(F)}> for {instance}, {fieldInfo} caused an exception", innerException);
			}
		}

		public static StructFieldRef<T, F> StructFieldRefAccess<T, F>(string fieldName) where T : struct
		{
			if (fieldName == null)
			{
				throw new ArgumentNullException("fieldName");
			}
			try
			{
				return Tools.StructFieldRefAccess<T, F>(Tools.GetInstanceField(typeof(T), fieldName));
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"StructFieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldName} caused an exception", innerException);
			}
		}

		public static ref F StructFieldRefAccess<T, F>(ref T instance, string fieldName) where T : struct
		{
			if (fieldName == null)
			{
				throw new ArgumentNullException("fieldName");
			}
			try
			{
				return ref Tools.StructFieldRefAccess<T, F>(Tools.GetInstanceField(typeof(T), fieldName))(ref instance);
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"StructFieldRefAccess<{typeof(T)}, {typeof(F)}> for {instance}, {fieldName} caused an exception", innerException);
			}
		}

		public static StructFieldRef<T, F> StructFieldRefAccess<T, F>(FieldInfo fieldInfo) where T : struct
		{
			if ((object)fieldInfo == null)
			{
				throw new ArgumentNullException("fieldInfo");
			}
			try
			{
				Tools.ValidateStructField<T, F>(fieldInfo);
				return Tools.StructFieldRefAccess<T, F>(fieldInfo);
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"StructFieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldInfo} caused an exception", innerException);
			}
		}

		public static ref F StructFieldRefAccess<T, F>(ref T instance, FieldInfo fieldInfo) where T : struct
		{
			if ((object)fieldInfo == null)
			{
				throw new ArgumentNullException("fieldInfo");
			}
			try
			{
				Tools.ValidateStructField<T, F>(fieldInfo);
				return ref Tools.StructFieldRefAccess<T, F>(fieldInfo)(ref instance);
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"StructFieldRefAccess<{typeof(T)}, {typeof(F)}> for {instance}, {fieldInfo} caused an exception", innerException);
			}
		}

		public static ref F StaticFieldRefAccess<T, F>(string fieldName)
		{
			return ref StaticFieldRefAccess<F>(typeof(T), fieldName);
		}

		public static ref F StaticFieldRefAccess<F>(Type type, string fieldName)
		{
			try
			{
				FieldInfo fieldInfo = Field(type, fieldName);
				if ((object)fieldInfo == null)
				{
					throw new MissingFieldException(type.Name, fieldName);
				}
				return ref Tools.StaticFieldRefAccess<F>(fieldInfo)();
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"StaticFieldRefAccess<{typeof(F)}> for {type}, {fieldName} caused an exception", innerException);
			}
		}

		public static ref F StaticFieldRefAccess<F>(string typeColonName)
		{
			Tools.TypeAndName typeAndName = Tools.TypColonName(typeColonName);
			return ref StaticFieldRefAccess<F>(typeAndName.type, typeAndName.name);
		}

		public static ref F StaticFieldRefAccess<T, F>(FieldInfo fieldInfo)
		{
			if ((object)fieldInfo == null)
			{
				throw new ArgumentNullException("fieldInfo");
			}
			try
			{
				return ref Tools.StaticFieldRefAccess<F>(fieldInfo)();
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"StaticFieldRefAccess<{typeof(T)}, {typeof(F)}> for {fieldInfo} caused an exception", innerException);
			}
		}

		public static FieldRef<F> StaticFieldRefAccess<F>(FieldInfo fieldInfo)
		{
			if ((object)fieldInfo == null)
			{
				throw new ArgumentNullException("fieldInfo");
			}
			try
			{
				return Tools.StaticFieldRefAccess<F>(fieldInfo);
			}
			catch (Exception innerException)
			{
				throw new ArgumentException($"StaticFieldRefAccess<{typeof(F)}> for {fieldInfo} caused an exception", innerException);
			}
		}

		[Obsolete("This overload only exists for runtime backwards compatibility and will be removed in Harmony 3. Use MethodDelegate(MethodInfo, object, bool, Type[]) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static DelegateType MethodDelegate<DelegateType>(MethodInfo method, object instance, bool virtualCall) where DelegateType : Delegate
		{
			return MethodDelegate<DelegateType>(method, instance, virtualCall, null);
		}

		public static DelegateType MethodDelegate<DelegateType>(MethodInfo method, object instance = null, bool virtualCall = true, Type[] delegateArgs = null) where DelegateType : Delegate
		{
			if ((object)method == null)
			{
				throw new ArgumentNullException("method");
			}
			Type typeFromHandle = typeof(DelegateType);
			if (method.IsStatic)
			{
				return (DelegateType)Delegate.CreateDelegate(typeFromHandle, method);
			}
			Type type = method.DeclaringType;
			if (type != null && type.IsInterface && !virtualCall)
			{
				throw new ArgumentException("Interface methods must be called virtually");
			}
			if (instance == null)
			{
				ParameterInfo[] parameters = typeFromHandle.GetMethod("Invoke").GetParameters();
				if (parameters.Length == 0)
				{
					Delegate.CreateDelegate(typeof(DelegateType), method);
					throw new ArgumentException("Invalid delegate type");
				}
				Type parameterType = parameters[0].ParameterType;
				if (type != null && type.IsInterface && parameterType.IsValueType)
				{
					InterfaceMapping interfaceMap = parameterType.GetInterfaceMap(type);
					method = interfaceMap.TargetMethods[Array.IndexOf(interfaceMap.InterfaceMethods, method)];
					type = parameterType;
				}
				if (type != null && virtualCall)
				{
					if (type.IsInterface)
					{
						return (DelegateType)Delegate.CreateDelegate(typeFromHandle, method);
					}
					if (parameterType.IsInterface)
					{
						InterfaceMapping interfaceMap2 = type.GetInterfaceMap(parameterType);
						MethodInfo method2 = interfaceMap2.InterfaceMethods[Array.IndexOf(interfaceMap2.TargetMethods, method)];
						return (DelegateType)Delegate.CreateDelegate(typeFromHandle, method2);
					}
					if (!type.IsValueType)
					{
						return (DelegateType)Delegate.CreateDelegate(typeFromHandle, method.GetBaseDefinition());
					}
				}
				ParameterInfo[] parameters2 = method.GetParameters();
				int num = parameters2.Length;
				Type[] array = new Type[num + 1];
				array[0] = type;
				for (int i = 0; i < num; i++)
				{
					array[i + 1] = parameters2[i].ParameterType;
				}
				Type[] array2 = delegateArgs ?? typeFromHandle.GetGenericArguments();
				Type[] parameterTypes = ((array2.Length < array.Length) ? array : array2);
				DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("OpenInstanceDelegate_" + method.Name, method.ReturnType, parameterTypes);
				ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
				if (type != null && type.IsValueType && array2.Length != 0 && !array2[0].IsByRef)
				{
					iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarga_S, 0);
				}
				else
				{
					iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
				}
				for (int j = 1; j < array.Length; j++)
				{
					iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg, j);
					if (array[j].IsValueType && j < array2.Length && !array2[j].IsValueType)
					{
						iLGenerator.Emit(System.Reflection.Emit.OpCodes.Unbox_Any, array[j]);
					}
				}
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Call, method);
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
				return (DelegateType)dynamicMethodDefinition.Generate().CreateDelegate(typeFromHandle);
			}
			if (virtualCall)
			{
				return (DelegateType)Delegate.CreateDelegate(typeFromHandle, instance, method.GetBaseDefinition());
			}
			if (type != null && !type.IsInstanceOfType(instance))
			{
				Delegate.CreateDelegate(typeof(DelegateType), instance, method);
				throw new ArgumentException("Invalid delegate type");
			}
			if (IsMonoRuntime)
			{
				DynamicMethodDefinition dynamicMethodDefinition2 = new DynamicMethodDefinition("LdftnDelegate_" + method.Name, typeFromHandle, new Type[1] { typeof(object) });
				ILGenerator iLGenerator2 = dynamicMethodDefinition2.GetILGenerator();
				iLGenerator2.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
				iLGenerator2.Emit(System.Reflection.Emit.OpCodes.Ldftn, method);
				iLGenerator2.Emit(System.Reflection.Emit.OpCodes.Newobj, typeFromHandle.GetConstructor(new Type[2]
				{
					typeof(object),
					typeof(IntPtr)
				}));
				iLGenerator2.Emit(System.Reflection.Emit.OpCodes.Ret);
				return (DelegateType)dynamicMethodDefinition2.Generate().Invoke(null, new object[1] { instance });
			}
			return (DelegateType)Activator.CreateInstance(typeFromHandle, instance, method.MethodHandle.GetFunctionPointer());
		}

		[Obsolete("This overload only exists for runtime backwards compatibility and will be removed in Harmony 3. Use MethodDelegate(string, object, bool, Type[]) instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public static DelegateType MethodDelegate<DelegateType>(string typeColonName, object instance, bool virtualCall) where DelegateType : Delegate
		{
			return MethodDelegate<DelegateType>(typeColonName, instance, virtualCall, null);
		}

		public static DelegateType MethodDelegate<DelegateType>(string typeColonName, object instance = null, bool virtualCall = true, Type[] delegateArgs = null) where DelegateType : Delegate
		{
			return MethodDelegate<DelegateType>(DeclaredMethod(typeColonName), instance, virtualCall, delegateArgs);
		}

		public static DelegateType HarmonyDelegate<DelegateType>(object instance = null) where DelegateType : Delegate
		{
			HarmonyMethod mergedFromType = HarmonyMethodExtensions.GetMergedFromType(typeof(DelegateType));
			HarmonyMethod harmonyMethod = mergedFromType;
			MethodType valueOrDefault = harmonyMethod.methodType.GetValueOrDefault();
			if (!harmonyMethod.methodType.HasValue)
			{
				valueOrDefault = MethodType.Normal;
				harmonyMethod.methodType = valueOrDefault;
			}
			if (!(mergedFromType.GetOriginalMethod() is MethodInfo method))
			{
				throw new NullReferenceException($"Delegate {typeof(DelegateType)} has no defined original method");
			}
			return MethodDelegate<DelegateType>(method, instance, !mergedFromType.nonVirtualDelegate, null);
		}

		public static MethodBase GetOutsideCaller()
		{
			StackTrace stackTrace = new StackTrace(fNeedFileInfo: true);
			StackFrame[] frames = stackTrace.GetFrames();
			foreach (StackFrame stackFrame in frames)
			{
				MethodBase method = stackFrame.GetMethod();
				if (method.DeclaringType?.Namespace != typeof(Harmony).Namespace)
				{
					return method;
				}
			}
			throw new Exception("Unexpected end of stack trace");
		}

		public static void RethrowException(Exception exception)
		{
			ExceptionDispatchInfo.Capture(exception).Throw();
			throw exception;
		}

		public static void ThrowMissingMemberException(Type type, params string[] names)
		{
			string value = string.Join(",", GetFieldNames(type).ToArray());
			string value2 = string.Join(",", GetPropertyNames(type).ToArray());
			throw new MissingMemberException($"{string.Join(",", names)}; available fields: {value}; available properties: {value2}");
		}

		public static object GetDefaultValue(Type type)
		{
			if ((object)type == null)
			{
				FileLog.Debug("AccessTools.GetDefaultValue: type is null");
				return null;
			}
			if (type == typeof(void))
			{
				return null;
			}
			if (type.IsValueType)
			{
				return Activator.CreateInstance(type);
			}
			return null;
		}

		public static object CreateInstance(Type type)
		{
			if ((object)type == null)
			{
				throw new ArgumentNullException("type");
			}
			ConstructorInfo constructor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, CallingConventions.Any, Array.Empty<Type>(), null);
			if ((object)constructor != null)
			{
				return constructor.Invoke(null);
			}
			return FormatterServices.GetUninitializedObject(type);
		}

		public static T CreateInstance<T>()
		{
			object obj = CreateInstance(typeof(T));
			if (obj is T)
			{
				return (T)obj;
			}
			return default(T);
		}

		public static T MakeDeepCopy<T>(object source) where T : class
		{
			return MakeDeepCopy(source, typeof(T)) as T;
		}

		public static void MakeDeepCopy<T>(object source, out T result, Func<string, Traverse, Traverse, object> processor = null, string pathRoot = "")
		{
			result = (T)MakeDeepCopy(source, typeof(T), processor, pathRoot);
		}

		public static object MakeDeepCopy(object source, Type resultType, Func<string, Traverse, Traverse, object> processor = null, string pathRoot = "")
		{
			if (source == null || (object)resultType == null)
			{
				return null;
			}
			resultType = Nullable.GetUnderlyingType(resultType) ?? resultType;
			Type type = source.GetType();
			if (type.IsPrimitive)
			{
				return source;
			}
			if (type.IsEnum)
			{
				return Enum.ToObject(resultType, (int)source);
			}
			if (type.IsGenericType && resultType.IsGenericType)
			{
				addHandlerCacheLock.EnterUpgradeableReadLock();
				try
				{
					if (!addHandlerCache.TryGetValue(resultType, out var value))
					{
						MethodInfo methodInfo = FirstMethod(resultType, (MethodInfo m) => m.Name == "Add" && m.GetParameters().Length == 1);
						if ((object)methodInfo != null)
						{
							value = MethodInvoker.GetHandler(methodInfo);
						}
						addHandlerCacheLock.EnterWriteLock();
						try
						{
							addHandlerCache[resultType] = value;
						}
						finally
						{
							addHandlerCacheLock.ExitWriteLock();
						}
					}
					if (value != null)
					{
						object obj = Activator.CreateInstance(resultType);
						Type resultType2 = resultType.GetGenericArguments()[0];
						int num = 0;
						foreach (object item in source as IEnumerable)
						{
							string text = num++.ToString();
							string pathRoot2 = ((pathRoot.Length > 0) ? (pathRoot + "." + text) : text);
							object obj2 = MakeDeepCopy(item, resultType2, processor, pathRoot2);
							value(obj, obj2);
						}
						return obj;
					}
				}
				finally
				{
					addHandlerCacheLock.ExitUpgradeableReadLock();
				}
			}
			if (type.IsArray && resultType.IsArray)
			{
				Type elementType = resultType.GetElementType();
				int length = ((Array)source).Length;
				object[] array = Activator.CreateInstance(resultType, length) as object[];
				object[] array2 = source as object[];
				for (int num2 = 0; num2 < length; num2++)
				{
					string text2 = num2.ToString();
					string pathRoot3 = ((pathRoot.Length > 0) ? (pathRoot + "." + text2) : text2);
					array[num2] = MakeDeepCopy(array2[num2], elementType, processor, pathRoot3);
				}
				return array;
			}
			string text3 = type.Namespace;
			if (text3 == "System" || (text3 != null && text3.StartsWith("System.")))
			{
				return source;
			}
			object obj3 = CreateInstance((resultType == typeof(object)) ? type : resultType);
			Traverse.IterateFields(source, obj3, delegate(string name, Traverse src, Traverse dst)
			{
				string text4 = ((pathRoot.Length > 0) ? (pathRoot + "." + name) : name);
				object source2 = ((processor != null) ? processor(text4, src, dst) : src.GetValue());
				if (dst.IsWriteable)
				{
					dst.SetValue(MakeDeepCopy(source2, dst.GetValueType(), processor, text4));
				}
			});
			return obj3;
		}

		public static bool IsStruct(Type type)
		{
			if (type == null)
			{
				return false;
			}
			if (type.IsValueType && !IsValue(type))
			{
				return !IsVoid(type);
			}
			return false;
		}

		public static bool IsClass(Type type)
		{
			if (type == null)
			{
				return false;
			}
			return !type.IsValueType;
		}

		public static bool IsValue(Type type)
		{
			if (type == null)
			{
				return false;
			}
			if (!type.IsPrimitive)
			{
				return type.IsEnum;
			}
			return true;
		}

		public static bool IsInteger(Type type)
		{
			if (type == null)
			{
				return false;
			}
			TypeCode typeCode = Type.GetTypeCode(type);
			if ((uint)(typeCode - 5) <= 7u)
			{
				return true;
			}
			return false;
		}

		public static bool IsFloatingPoint(Type type)
		{
			if (type == null)
			{
				return false;
			}
			TypeCode typeCode = Type.GetTypeCode(type);
			if ((uint)(typeCode - 13) <= 2u)
			{
				return true;
			}
			return false;
		}

		public static bool IsNumber(Type type)
		{
			if (!IsInteger(type))
			{
				return IsFloatingPoint(type);
			}
			return true;
		}

		public static bool IsVoid(Type type)
		{
			return type == typeof(void);
		}

		public static bool IsOfNullableType<T>(T instance)
		{
			return (object)Nullable.GetUnderlyingType(typeof(T)) != null;
		}

		public static bool IsStatic(MemberInfo member)
		{
			if ((object)member == null)
			{
				throw new ArgumentNullException("member");
			}
			switch (member.MemberType)
			{
			case MemberTypes.Constructor:
			case MemberTypes.Method:
				return ((MethodBase)member).IsStatic;
			case MemberTypes.Event:
				return IsStatic((EventInfo)member);
			case MemberTypes.Field:
				return ((FieldInfo)member).IsStatic;
			case MemberTypes.Property:
				return IsStatic((PropertyInfo)member);
			case MemberTypes.TypeInfo:
			case MemberTypes.NestedType:
				return IsStatic((Type)member);
			default:
				throw new ArgumentException($"Unknown member type: {member.MemberType}");
			}
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool IsStatic(Type type)
		{
			if ((object)type == null)
			{
				return false;
			}
			if (type.IsAbstract)
			{
				return type.IsSealed;
			}
			return false;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool IsStatic(PropertyInfo propertyInfo)
		{
			if ((object)propertyInfo == null)
			{
				throw new ArgumentNullException("propertyInfo");
			}
			return propertyInfo.GetAccessors(nonPublic: true)[0].IsStatic;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool IsStatic(EventInfo eventInfo)
		{
			if ((object)eventInfo == null)
			{
				throw new ArgumentNullException("eventInfo");
			}
			return eventInfo.GetAddMethod(nonPublic: true).IsStatic;
		}

		public static int CombinedHashCode(IEnumerable<object> objects)
		{
			int num = 352654597;
			int num2 = num;
			int num3 = 0;
			foreach (object @object in objects)
			{
				if (num3 % 2 == 0)
				{
					num = ((num << 5) + num + (num >> 27)) ^ @object.GetHashCode();
				}
				else
				{
					num2 = ((num2 << 5) + num2 + (num2 >> 27)) ^ @object.GetHashCode();
				}
				num3++;
			}
			return num + num2 * 1566083941;
		}
	}
	public static class AccessToolsExtensions
	{
		public static IEnumerable<Type> InnerTypes(this Type type)
		{
			return AccessTools.InnerTypes(type);
		}

		public static T FindIncludingBaseTypes<T>(this Type type, Func<Type, T> func) where T : class
		{
			return AccessTools.FindIncludingBaseTypes(type, func);
		}

		public static T FindIncludingInnerTypes<T>(this Type type, Func<Type, T> func) where T : class
		{
			return AccessTools.FindIncludingInnerTypes(type, func);
		}

		public static FieldInfo DeclaredField(this Type type, string name)
		{
			return AccessTools.DeclaredField(type, name);
		}

		public static FieldInfo Field(this Type type, string name)
		{
			return AccessTools.Field(type, name);
		}

		public static FieldInfo DeclaredField(this Type type, int idx)
		{
			return AccessTools.DeclaredField(type, idx);
		}

		public static PropertyInfo DeclaredProperty(this Type type, string name)
		{
			return AccessTools.DeclaredProperty(type, name);
		}

		public static PropertyInfo DeclaredIndexer(this Type type, Type[] parameters = null)
		{
			return AccessTools.DeclaredIndexer(type, parameters);
		}

		public static MethodInfo DeclaredPropertyGetter(this Type type, string name)
		{
			return AccessTools.DeclaredPropertyGetter(type, name);
		}

		public static MethodInfo DeclaredIndexerGetter(this Type type, Type[] parameters = null)
		{
			return AccessTools.DeclaredIndexerGetter(type, parameters);
		}

		public static MethodInfo DeclaredPropertySetter(this Type type, string name)
		{
			return AccessTools.DeclaredPropertySetter(type, name);
		}

		public static MethodInfo DeclaredIndexerSetter(this Type type, Type[] parameters)
		{
			return AccessTools.DeclaredIndexerSetter(type, parameters);
		}

		public static PropertyInfo Property(this Type type, string name)
		{
			return AccessTools.Property(type, name);
		}

		public static PropertyInfo Indexer(this Type type, Type[] parameters = null)
		{
			return AccessTools.Indexer(type, parameters);
		}

		public static MethodInfo PropertyGetter(this Type type, string name)
		{
			return AccessTools.PropertyGetter(type, name);
		}

		public static MethodInfo IndexerGetter(this Type type, Type[] parameters = null)
		{
			return AccessTools.IndexerGetter(type, parameters);
		}

		public static MethodInfo PropertySetter(this Type type, string name)
		{
			return AccessTools.PropertySetter(type, name);
		}

		public static MethodInfo IndexerSetter(this Type type, Type[] parameters = null)
		{
			return AccessTools.IndexerSetter(type, parameters);
		}

		public static EventInfo DeclaredEvent(this Type type, string name)
		{
			return AccessTools.DeclaredEvent(type, name);
		}

		public static EventInfo Event(this Type type, string name)
		{
			return AccessTools.Event(type, name);
		}

		public static MethodInfo DeclaredEventAdder(this Type type, string name)
		{
			return AccessTools.DeclaredEventAdder(type, name);
		}

		public static MethodInfo EventAdder(this Type type, string name)
		{
			return AccessTools.EventAdder(type, name);
		}

		public static MethodInfo DeclaredEventRemover(this Type type, string name)
		{
			return AccessTools.DeclaredEventRemover(type, name);
		}

		public static MethodInfo EventRemover(this Type type, string name)
		{
			return AccessTools.EventRemover(type, name);
		}

		public static MethodInfo Finalizer(this Type type)
		{
			return AccessTools.Finalizer(type);
		}

		public static MethodInfo DeclaredFinalizer(this Type type)
		{
			return AccessTools.DeclaredFinalizer(type);
		}

		public static MethodInfo DeclaredMethod(this Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			return AccessTools.DeclaredMethod(type, name, parameters, generics);
		}

		public static MethodInfo Method(this Type type, string name, Type[] parameters = null, Type[] generics = null)
		{
			return AccessTools.Method(type, name, parameters, generics);
		}

		public static List<string> GetMethodNames(this Type type)
		{
			return AccessTools.GetMethodNames(type);
		}

		public static List<string> GetFieldNames(this Type type)
		{
			return AccessTools.GetFieldNames(type);
		}

		public static List<string> GetPropertyNames(this Type type)
		{
			return AccessTools.GetPropertyNames(type);
		}

		public static ConstructorInfo DeclaredConstructor(this Type type, Type[] parameters = null, bool searchForStatic = false)
		{
			return AccessTools.DeclaredConstructor(type, parameters, searchForStatic);
		}

		public static ConstructorInfo Constructor(this Type type, Type[] parameters = null, bool searchForStatic = false)
		{
			return AccessTools.Constructor(type, parameters, searchForStatic);
		}

		public static List<ConstructorInfo> GetDeclaredConstructors(this Type type, bool? searchForStatic = null)
		{
			return AccessTools.GetDeclaredConstructors(type, searchForStatic);
		}

		public static List<MethodInfo> GetDeclaredMethods(this Type type)
		{
			return AccessTools.GetDeclaredMethods(type);
		}

		public static List<PropertyInfo> GetDeclaredProperties(this Type type)
		{
			return AccessTools.GetDeclaredProperties(type);
		}

		public static List<FieldInfo> GetDeclaredFields(this Type type)
		{
			return AccessTools.GetDeclaredFields(type);
		}

		public static Type Inner(this Type type, string name)
		{
			return AccessTools.Inner(type, name);
		}

		public static Type FirstInner(this Type type, Func<Type, bool> predicate)
		{
			return AccessTools.FirstInner(type, predicate);
		}

		public static MethodInfo FirstMethod(this Type type, Func<MethodInfo, bool> predicate)
		{
			return AccessTools.FirstMethod(type, predicate);
		}

		public static ConstructorInfo FirstConstructor(this Type type, Func<ConstructorInfo, bool> predicate)
		{
			return AccessTools.FirstConstructor(type, predicate);
		}

		public static PropertyInfo FirstProperty(this Type type, Func<PropertyInfo, bool> predicate)
		{
			return AccessTools.FirstProperty(type, predicate);
		}

		public static AccessTools.FieldRef<object, F> FieldRefAccess<F>(this Type type, string fieldName)
		{
			return AccessTools.FieldRefAccess<F>(type, fieldName);
		}

		public static ref F StaticFieldRefAccess<F>(this Type type, string fieldName)
		{
			return ref AccessTools.StaticFieldRefAccess<F>(type, fieldName);
		}

		public static void ThrowMissingMemberException(this Type type, params string[] names)
		{
			AccessTools.ThrowMissingMemberException(type, names);
		}

		public static object GetDefaultValue(this Type type)
		{
			return AccessTools.GetDefaultValue(type);
		}

		public static object CreateInstance(this Type type)
		{
			return AccessTools.CreateInstance(type);
		}

		public static bool IsStruct(this Type type)
		{
			return AccessTools.IsStruct(type);
		}

		public static bool IsClass(this Type type)
		{
			return AccessTools.IsClass(type);
		}

		public static bool IsValue(this Type type)
		{
			return AccessTools.IsValue(type);
		}

		public static bool IsInteger(this Type type)
		{
			return AccessTools.IsInteger(type);
		}

		public static bool IsFloatingPoint(this Type type)
		{
			return AccessTools.IsFloatingPoint(type);
		}

		public static bool IsNumber(this Type type)
		{
			return AccessTools.IsNumber(type);
		}

		public static bool IsVoid(this Type type)
		{
			return AccessTools.IsVoid(type);
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool IsStatic(this Type type)
		{
			return AccessTools.IsStatic(type);
		}
	}
	public static class Code
	{
		public class Operand_ : CodeMatch
		{
			public Operand_ this[object operand = null, string name = null] => (Operand_)Set(operand, name);
		}

		public class Nop_ : CodeMatch
		{
			public Nop_ this[object operand = null, string name = null] => (Nop_)Set(System.Reflection.Emit.OpCodes.Nop, operand, name);

			public Nop_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Break_ : CodeMatch
		{
			public Break_ this[object operand = null, string name = null] => (Break_)Set(System.Reflection.Emit.OpCodes.Break, operand, name);

			public Break_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldarg_0_ : CodeMatch
		{
			public Ldarg_0_ this[object operand = null, string name = null] => (Ldarg_0_)Set(System.Reflection.Emit.OpCodes.Ldarg_0, operand, name);

			public Ldarg_0_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldarg_1_ : CodeMatch
		{
			public Ldarg_1_ this[object operand = null, string name = null] => (Ldarg_1_)Set(System.Reflection.Emit.OpCodes.Ldarg_1, operand, name);

			public Ldarg_1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldarg_2_ : CodeMatch
		{
			public Ldarg_2_ this[object operand = null, string name = null] => (Ldarg_2_)Set(System.Reflection.Emit.OpCodes.Ldarg_2, operand, name);

			public Ldarg_2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldarg_3_ : CodeMatch
		{
			public Ldarg_3_ this[object operand = null, string name = null] => (Ldarg_3_)Set(System.Reflection.Emit.OpCodes.Ldarg_3, operand, name);

			public Ldarg_3_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldloc_0_ : CodeMatch
		{
			public Ldloc_0_ this[object operand = null, string name = null] => (Ldloc_0_)Set(System.Reflection.Emit.OpCodes.Ldloc_0, operand, name);

			public Ldloc_0_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldloc_1_ : CodeMatch
		{
			public Ldloc_1_ this[object operand = null, string name = null] => (Ldloc_1_)Set(System.Reflection.Emit.OpCodes.Ldloc_1, operand, name);

			public Ldloc_1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldloc_2_ : CodeMatch
		{
			public Ldloc_2_ this[object operand = null, string name = null] => (Ldloc_2_)Set(System.Reflection.Emit.OpCodes.Ldloc_2, operand, name);

			public Ldloc_2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldloc_3_ : CodeMatch
		{
			public Ldloc_3_ this[object operand = null, string name = null] => (Ldloc_3_)Set(System.Reflection.Emit.OpCodes.Ldloc_3, operand, name);

			public Ldloc_3_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stloc_0_ : CodeMatch
		{
			public Stloc_0_ this[object operand = null, string name = null] => (Stloc_0_)Set(System.Reflection.Emit.OpCodes.Stloc_0, operand, name);

			public Stloc_0_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stloc_1_ : CodeMatch
		{
			public Stloc_1_ this[object operand = null, string name = null] => (Stloc_1_)Set(System.Reflection.Emit.OpCodes.Stloc_1, operand, name);

			public Stloc_1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stloc_2_ : CodeMatch
		{
			public Stloc_2_ this[object operand = null, string name = null] => (Stloc_2_)Set(System.Reflection.Emit.OpCodes.Stloc_2, operand, name);

			public Stloc_2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stloc_3_ : CodeMatch
		{
			public Stloc_3_ this[object operand = null, string name = null] => (Stloc_3_)Set(System.Reflection.Emit.OpCodes.Stloc_3, operand, name);

			public Stloc_3_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldarg_S_ : CodeMatch
		{
			public Ldarg_S_ this[object operand = null, string name = null] => (Ldarg_S_)Set(System.Reflection.Emit.OpCodes.Ldarg_S, operand, name);

			public Ldarg_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldarga_S_ : CodeMatch
		{
			public Ldarga_S_ this[object operand = null, string name = null] => (Ldarga_S_)Set(System.Reflection.Emit.OpCodes.Ldarga_S, operand, name);

			public Ldarga_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Starg_S_ : CodeMatch
		{
			public Starg_S_ this[object operand = null, string name = null] => (Starg_S_)Set(System.Reflection.Emit.OpCodes.Starg_S, operand, name);

			public Starg_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldloc_S_ : CodeMatch
		{
			public Ldloc_S_ this[object operand = null, string name = null] => (Ldloc_S_)Set(System.Reflection.Emit.OpCodes.Ldloc_S, operand, name);

			public Ldloc_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldloca_S_ : CodeMatch
		{
			public Ldloca_S_ this[object operand = null, string name = null] => (Ldloca_S_)Set(System.Reflection.Emit.OpCodes.Ldloca_S, operand, name);

			public Ldloca_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stloc_S_ : CodeMatch
		{
			public Stloc_S_ this[object operand = null, string name = null] => (Stloc_S_)Set(System.Reflection.Emit.OpCodes.Stloc_S, operand, name);

			public Stloc_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldnull_ : CodeMatch
		{
			public Ldnull_ this[object operand = null, string name = null] => (Ldnull_)Set(System.Reflection.Emit.OpCodes.Ldnull, operand, name);

			public Ldnull_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_M1_ : CodeMatch
		{
			public Ldc_I4_M1_ this[object operand = null, string name = null] => (Ldc_I4_M1_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_M1, operand, name);

			public Ldc_I4_M1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_0_ : CodeMatch
		{
			public Ldc_I4_0_ this[object operand = null, string name = null] => (Ldc_I4_0_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_0, operand, name);

			public Ldc_I4_0_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_1_ : CodeMatch
		{
			public Ldc_I4_1_ this[object operand = null, string name = null] => (Ldc_I4_1_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_1, operand, name);

			public Ldc_I4_1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_2_ : CodeMatch
		{
			public Ldc_I4_2_ this[object operand = null, string name = null] => (Ldc_I4_2_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_2, operand, name);

			public Ldc_I4_2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_3_ : CodeMatch
		{
			public Ldc_I4_3_ this[object operand = null, string name = null] => (Ldc_I4_3_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_3, operand, name);

			public Ldc_I4_3_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_4_ : CodeMatch
		{
			public Ldc_I4_4_ this[object operand = null, string name = null] => (Ldc_I4_4_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_4, operand, name);

			public Ldc_I4_4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_5_ : CodeMatch
		{
			public Ldc_I4_5_ this[object operand = null, string name = null] => (Ldc_I4_5_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_5, operand, name);

			public Ldc_I4_5_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_6_ : CodeMatch
		{
			public Ldc_I4_6_ this[object operand = null, string name = null] => (Ldc_I4_6_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_6, operand, name);

			public Ldc_I4_6_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_7_ : CodeMatch
		{
			public Ldc_I4_7_ this[object operand = null, string name = null] => (Ldc_I4_7_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_7, operand, name);

			public Ldc_I4_7_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_8_ : CodeMatch
		{
			public Ldc_I4_8_ this[object operand = null, string name = null] => (Ldc_I4_8_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_8, operand, name);

			public Ldc_I4_8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_S_ : CodeMatch
		{
			public Ldc_I4_S_ this[object operand = null, string name = null] => (Ldc_I4_S_)Set(System.Reflection.Emit.OpCodes.Ldc_I4_S, operand, name);

			public Ldc_I4_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I4_ : CodeMatch
		{
			public Ldc_I4_ this[object operand = null, string name = null] => (Ldc_I4_)Set(System.Reflection.Emit.OpCodes.Ldc_I4, operand, name);

			public Ldc_I4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_I8_ : CodeMatch
		{
			public Ldc_I8_ this[object operand = null, string name = null] => (Ldc_I8_)Set(System.Reflection.Emit.OpCodes.Ldc_I8, operand, name);

			public Ldc_I8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_R4_ : CodeMatch
		{
			public Ldc_R4_ this[object operand = null, string name = null] => (Ldc_R4_)Set(System.Reflection.Emit.OpCodes.Ldc_R4, operand, name);

			public Ldc_R4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldc_R8_ : CodeMatch
		{
			public Ldc_R8_ this[object operand = null, string name = null] => (Ldc_R8_)Set(System.Reflection.Emit.OpCodes.Ldc_R8, operand, name);

			public Ldc_R8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Dup_ : CodeMatch
		{
			public Dup_ this[object operand = null, string name = null] => (Dup_)Set(System.Reflection.Emit.OpCodes.Dup, operand, name);

			public Dup_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Pop_ : CodeMatch
		{
			public Pop_ this[object operand = null, string name = null] => (Pop_)Set(System.Reflection.Emit.OpCodes.Pop, operand, name);

			public Pop_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Jmp_ : CodeMatch
		{
			public Jmp_ this[object operand = null, string name = null] => (Jmp_)Set(System.Reflection.Emit.OpCodes.Jmp, operand, name);

			public Jmp_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Call_ : CodeMatch
		{
			public Call_ this[object operand = null, string name = null] => (Call_)Set(System.Reflection.Emit.OpCodes.Call, operand, name);

			public Call_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Calli_ : CodeMatch
		{
			public Calli_ this[object operand = null, string name = null] => (Calli_)Set(System.Reflection.Emit.OpCodes.Calli, operand, name);

			public Calli_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ret_ : CodeMatch
		{
			public Ret_ this[object operand = null, string name = null] => (Ret_)Set(System.Reflection.Emit.OpCodes.Ret, operand, name);

			public Ret_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Br_S_ : CodeMatch
		{
			public Br_S_ this[object operand = null, string name = null] => (Br_S_)Set(System.Reflection.Emit.OpCodes.Br_S, operand, name);

			public Br_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Brfalse_S_ : CodeMatch
		{
			public Brfalse_S_ this[object operand = null, string name = null] => (Brfalse_S_)Set(System.Reflection.Emit.OpCodes.Brfalse_S, operand, name);

			public Brfalse_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Brtrue_S_ : CodeMatch
		{
			public Brtrue_S_ this[object operand = null, string name = null] => (Brtrue_S_)Set(System.Reflection.Emit.OpCodes.Brtrue_S, operand, name);

			public Brtrue_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Beq_S_ : CodeMatch
		{
			public Beq_S_ this[object operand = null, string name = null] => (Beq_S_)Set(System.Reflection.Emit.OpCodes.Beq_S, operand, name);

			public Beq_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bge_S_ : CodeMatch
		{
			public Bge_S_ this[object operand = null, string name = null] => (Bge_S_)Set(System.Reflection.Emit.OpCodes.Bge_S, operand, name);

			public Bge_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bgt_S_ : CodeMatch
		{
			public Bgt_S_ this[object operand = null, string name = null] => (Bgt_S_)Set(System.Reflection.Emit.OpCodes.Bgt_S, operand, name);

			public Bgt_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ble_S_ : CodeMatch
		{
			public Ble_S_ this[object operand = null, string name = null] => (Ble_S_)Set(System.Reflection.Emit.OpCodes.Ble_S, operand, name);

			public Ble_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Blt_S_ : CodeMatch
		{
			public Blt_S_ this[object operand = null, string name = null] => (Blt_S_)Set(System.Reflection.Emit.OpCodes.Blt_S, operand, name);

			public Blt_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bne_Un_S_ : CodeMatch
		{
			public Bne_Un_S_ this[object operand = null, string name = null] => (Bne_Un_S_)Set(System.Reflection.Emit.OpCodes.Bne_Un_S, operand, name);

			public Bne_Un_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bge_Un_S_ : CodeMatch
		{
			public Bge_Un_S_ this[object operand = null, string name = null] => (Bge_Un_S_)Set(System.Reflection.Emit.OpCodes.Bge_Un_S, operand, name);

			public Bge_Un_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bgt_Un_S_ : CodeMatch
		{
			public Bgt_Un_S_ this[object operand = null, string name = null] => (Bgt_Un_S_)Set(System.Reflection.Emit.OpCodes.Bgt_Un_S, operand, name);

			public Bgt_Un_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ble_Un_S_ : CodeMatch
		{
			public Ble_Un_S_ this[object operand = null, string name = null] => (Ble_Un_S_)Set(System.Reflection.Emit.OpCodes.Ble_Un_S, operand, name);

			public Ble_Un_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Blt_Un_S_ : CodeMatch
		{
			public Blt_Un_S_ this[object operand = null, string name = null] => (Blt_Un_S_)Set(System.Reflection.Emit.OpCodes.Blt_Un_S, operand, name);

			public Blt_Un_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Br_ : CodeMatch
		{
			public Br_ this[object operand = null, string name = null] => (Br_)Set(System.Reflection.Emit.OpCodes.Br, operand, name);

			public Br_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Brfalse_ : CodeMatch
		{
			public Brfalse_ this[object operand = null, string name = null] => (Brfalse_)Set(System.Reflection.Emit.OpCodes.Brfalse, operand, name);

			public Brfalse_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Brtrue_ : CodeMatch
		{
			public Brtrue_ this[object operand = null, string name = null] => (Brtrue_)Set(System.Reflection.Emit.OpCodes.Brtrue, operand, name);

			public Brtrue_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Beq_ : CodeMatch
		{
			public Beq_ this[object operand = null, string name = null] => (Beq_)Set(System.Reflection.Emit.OpCodes.Beq, operand, name);

			public Beq_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bge_ : CodeMatch
		{
			public Bge_ this[object operand = null, string name = null] => (Bge_)Set(System.Reflection.Emit.OpCodes.Bge, operand, name);

			public Bge_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bgt_ : CodeMatch
		{
			public Bgt_ this[object operand = null, string name = null] => (Bgt_)Set(System.Reflection.Emit.OpCodes.Bgt, operand, name);

			public Bgt_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ble_ : CodeMatch
		{
			public Ble_ this[object operand = null, string name = null] => (Ble_)Set(System.Reflection.Emit.OpCodes.Ble, operand, name);

			public Ble_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Blt_ : CodeMatch
		{
			public Blt_ this[object operand = null, string name = null] => (Blt_)Set(System.Reflection.Emit.OpCodes.Blt, operand, name);

			public Blt_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bne_Un_ : CodeMatch
		{
			public Bne_Un_ this[object operand = null, string name = null] => (Bne_Un_)Set(System.Reflection.Emit.OpCodes.Bne_Un, operand, name);

			public Bne_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bge_Un_ : CodeMatch
		{
			public Bge_Un_ this[object operand = null, string name = null] => (Bge_Un_)Set(System.Reflection.Emit.OpCodes.Bge_Un, operand, name);

			public Bge_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Bgt_Un_ : CodeMatch
		{
			public Bgt_Un_ this[object operand = null, string name = null] => (Bgt_Un_)Set(System.Reflection.Emit.OpCodes.Bgt_Un, operand, name);

			public Bgt_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ble_Un_ : CodeMatch
		{
			public Ble_Un_ this[object operand = null, string name = null] => (Ble_Un_)Set(System.Reflection.Emit.OpCodes.Ble_Un, operand, name);

			public Ble_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Blt_Un_ : CodeMatch
		{
			public Blt_Un_ this[object operand = null, string name = null] => (Blt_Un_)Set(System.Reflection.Emit.OpCodes.Blt_Un, operand, name);

			public Blt_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Switch_ : CodeMatch
		{
			public Switch_ this[object operand = null, string name = null] => (Switch_)Set(System.Reflection.Emit.OpCodes.Switch, operand, name);

			public Switch_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_I1_ : CodeMatch
		{
			public Ldind_I1_ this[object operand = null, string name = null] => (Ldind_I1_)Set(System.Reflection.Emit.OpCodes.Ldind_I1, operand, name);

			public Ldind_I1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_U1_ : CodeMatch
		{
			public Ldind_U1_ this[object operand = null, string name = null] => (Ldind_U1_)Set(System.Reflection.Emit.OpCodes.Ldind_U1, operand, name);

			public Ldind_U1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_I2_ : CodeMatch
		{
			public Ldind_I2_ this[object operand = null, string name = null] => (Ldind_I2_)Set(System.Reflection.Emit.OpCodes.Ldind_I2, operand, name);

			public Ldind_I2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_U2_ : CodeMatch
		{
			public Ldind_U2_ this[object operand = null, string name = null] => (Ldind_U2_)Set(System.Reflection.Emit.OpCodes.Ldind_U2, operand, name);

			public Ldind_U2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_I4_ : CodeMatch
		{
			public Ldind_I4_ this[object operand = null, string name = null] => (Ldind_I4_)Set(System.Reflection.Emit.OpCodes.Ldind_I4, operand, name);

			public Ldind_I4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_U4_ : CodeMatch
		{
			public Ldind_U4_ this[object operand = null, string name = null] => (Ldind_U4_)Set(System.Reflection.Emit.OpCodes.Ldind_U4, operand, name);

			public Ldind_U4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_I8_ : CodeMatch
		{
			public Ldind_I8_ this[object operand = null, string name = null] => (Ldind_I8_)Set(System.Reflection.Emit.OpCodes.Ldind_I8, operand, name);

			public Ldind_I8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_I_ : CodeMatch
		{
			public Ldind_I_ this[object operand = null, string name = null] => (Ldind_I_)Set(System.Reflection.Emit.OpCodes.Ldind_I, operand, name);

			public Ldind_I_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_R4_ : CodeMatch
		{
			public Ldind_R4_ this[object operand = null, string name = null] => (Ldind_R4_)Set(System.Reflection.Emit.OpCodes.Ldind_R4, operand, name);

			public Ldind_R4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_R8_ : CodeMatch
		{
			public Ldind_R8_ this[object operand = null, string name = null] => (Ldind_R8_)Set(System.Reflection.Emit.OpCodes.Ldind_R8, operand, name);

			public Ldind_R8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldind_Ref_ : CodeMatch
		{
			public Ldind_Ref_ this[object operand = null, string name = null] => (Ldind_Ref_)Set(System.Reflection.Emit.OpCodes.Ldind_Ref, operand, name);

			public Ldind_Ref_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stind_Ref_ : CodeMatch
		{
			public Stind_Ref_ this[object operand = null, string name = null] => (Stind_Ref_)Set(System.Reflection.Emit.OpCodes.Stind_Ref, operand, name);

			public Stind_Ref_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stind_I1_ : CodeMatch
		{
			public Stind_I1_ this[object operand = null, string name = null] => (Stind_I1_)Set(System.Reflection.Emit.OpCodes.Stind_I1, operand, name);

			public Stind_I1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stind_I2_ : CodeMatch
		{
			public Stind_I2_ this[object operand = null, string name = null] => (Stind_I2_)Set(System.Reflection.Emit.OpCodes.Stind_I2, operand, name);

			public Stind_I2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stind_I4_ : CodeMatch
		{
			public Stind_I4_ this[object operand = null, string name = null] => (Stind_I4_)Set(System.Reflection.Emit.OpCodes.Stind_I4, operand, name);

			public Stind_I4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stind_I8_ : CodeMatch
		{
			public Stind_I8_ this[object operand = null, string name = null] => (Stind_I8_)Set(System.Reflection.Emit.OpCodes.Stind_I8, operand, name);

			public Stind_I8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stind_R4_ : CodeMatch
		{
			public Stind_R4_ this[object operand = null, string name = null] => (Stind_R4_)Set(System.Reflection.Emit.OpCodes.Stind_R4, operand, name);

			public Stind_R4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stind_R8_ : CodeMatch
		{
			public Stind_R8_ this[object operand = null, string name = null] => (Stind_R8_)Set(System.Reflection.Emit.OpCodes.Stind_R8, operand, name);

			public Stind_R8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Add_ : CodeMatch
		{
			public Add_ this[object operand = null, string name = null] => (Add_)Set(System.Reflection.Emit.OpCodes.Add, operand, name);

			public Add_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Sub_ : CodeMatch
		{
			public Sub_ this[object operand = null, string name = null] => (Sub_)Set(System.Reflection.Emit.OpCodes.Sub, operand, name);

			public Sub_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Mul_ : CodeMatch
		{
			public Mul_ this[object operand = null, string name = null] => (Mul_)Set(System.Reflection.Emit.OpCodes.Mul, operand, name);

			public Mul_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Div_ : CodeMatch
		{
			public Div_ this[object operand = null, string name = null] => (Div_)Set(System.Reflection.Emit.OpCodes.Div, operand, name);

			public Div_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Div_Un_ : CodeMatch
		{
			public Div_Un_ this[object operand = null, string name = null] => (Div_Un_)Set(System.Reflection.Emit.OpCodes.Div_Un, operand, name);

			public Div_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Rem_ : CodeMatch
		{
			public Rem_ this[object operand = null, string name = null] => (Rem_)Set(System.Reflection.Emit.OpCodes.Rem, operand, name);

			public Rem_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Rem_Un_ : CodeMatch
		{
			public Rem_Un_ this[object operand = null, string name = null] => (Rem_Un_)Set(System.Reflection.Emit.OpCodes.Rem_Un, operand, name);

			public Rem_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class And_ : CodeMatch
		{
			public And_ this[object operand = null, string name = null] => (And_)Set(System.Reflection.Emit.OpCodes.And, operand, name);

			public And_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Or_ : CodeMatch
		{
			public Or_ this[object operand = null, string name = null] => (Or_)Set(System.Reflection.Emit.OpCodes.Or, operand, name);

			public Or_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Xor_ : CodeMatch
		{
			public Xor_ this[object operand = null, string name = null] => (Xor_)Set(System.Reflection.Emit.OpCodes.Xor, operand, name);

			public Xor_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Shl_ : CodeMatch
		{
			public Shl_ this[object operand = null, string name = null] => (Shl_)Set(System.Reflection.Emit.OpCodes.Shl, operand, name);

			public Shl_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Shr_ : CodeMatch
		{
			public Shr_ this[object operand = null, string name = null] => (Shr_)Set(System.Reflection.Emit.OpCodes.Shr, operand, name);

			public Shr_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Shr_Un_ : CodeMatch
		{
			public Shr_Un_ this[object operand = null, string name = null] => (Shr_Un_)Set(System.Reflection.Emit.OpCodes.Shr_Un, operand, name);

			public Shr_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Neg_ : CodeMatch
		{
			public Neg_ this[object operand = null, string name = null] => (Neg_)Set(System.Reflection.Emit.OpCodes.Neg, operand, name);

			public Neg_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Not_ : CodeMatch
		{
			public Not_ this[object operand = null, string name = null] => (Not_)Set(System.Reflection.Emit.OpCodes.Not, operand, name);

			public Not_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_I1_ : CodeMatch
		{
			public Conv_I1_ this[object operand = null, string name = null] => (Conv_I1_)Set(System.Reflection.Emit.OpCodes.Conv_I1, operand, name);

			public Conv_I1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_I2_ : CodeMatch
		{
			public Conv_I2_ this[object operand = null, string name = null] => (Conv_I2_)Set(System.Reflection.Emit.OpCodes.Conv_I2, operand, name);

			public Conv_I2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_I4_ : CodeMatch
		{
			public Conv_I4_ this[object operand = null, string name = null] => (Conv_I4_)Set(System.Reflection.Emit.OpCodes.Conv_I4, operand, name);

			public Conv_I4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_I8_ : CodeMatch
		{
			public Conv_I8_ this[object operand = null, string name = null] => (Conv_I8_)Set(System.Reflection.Emit.OpCodes.Conv_I8, operand, name);

			public Conv_I8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_R4_ : CodeMatch
		{
			public Conv_R4_ this[object operand = null, string name = null] => (Conv_R4_)Set(System.Reflection.Emit.OpCodes.Conv_R4, operand, name);

			public Conv_R4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_R8_ : CodeMatch
		{
			public Conv_R8_ this[object operand = null, string name = null] => (Conv_R8_)Set(System.Reflection.Emit.OpCodes.Conv_R8, operand, name);

			public Conv_R8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_U4_ : CodeMatch
		{
			public Conv_U4_ this[object operand = null, string name = null] => (Conv_U4_)Set(System.Reflection.Emit.OpCodes.Conv_U4, operand, name);

			public Conv_U4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_U8_ : CodeMatch
		{
			public Conv_U8_ this[object operand = null, string name = null] => (Conv_U8_)Set(System.Reflection.Emit.OpCodes.Conv_U8, operand, name);

			public Conv_U8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Callvirt_ : CodeMatch
		{
			public Callvirt_ this[object operand = null, string name = null] => (Callvirt_)Set(System.Reflection.Emit.OpCodes.Callvirt, operand, name);

			public Callvirt_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Cpobj_ : CodeMatch
		{
			public Cpobj_ this[object operand = null, string name = null] => (Cpobj_)Set(System.Reflection.Emit.OpCodes.Cpobj, operand, name);

			public Cpobj_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldobj_ : CodeMatch
		{
			public Ldobj_ this[object operand = null, string name = null] => (Ldobj_)Set(System.Reflection.Emit.OpCodes.Ldobj, operand, name);

			public Ldobj_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldstr_ : CodeMatch
		{
			public Ldstr_ this[object operand = null, string name = null] => (Ldstr_)Set(System.Reflection.Emit.OpCodes.Ldstr, operand, name);

			public Ldstr_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Newobj_ : CodeMatch
		{
			public Newobj_ this[object operand = null, string name = null] => (Newobj_)Set(System.Reflection.Emit.OpCodes.Newobj, operand, name);

			public Newobj_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Castclass_ : CodeMatch
		{
			public Castclass_ this[object operand = null, string name = null] => (Castclass_)Set(System.Reflection.Emit.OpCodes.Castclass, operand, name);

			public Castclass_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Isinst_ : CodeMatch
		{
			public Isinst_ this[object operand = null, string name = null] => (Isinst_)Set(System.Reflection.Emit.OpCodes.Isinst, operand, name);

			public Isinst_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_R_Un_ : CodeMatch
		{
			public Conv_R_Un_ this[object operand = null, string name = null] => (Conv_R_Un_)Set(System.Reflection.Emit.OpCodes.Conv_R_Un, operand, name);

			public Conv_R_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Unbox_ : CodeMatch
		{
			public Unbox_ this[object operand = null, string name = null] => (Unbox_)Set(System.Reflection.Emit.OpCodes.Unbox, operand, name);

			public Unbox_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Throw_ : CodeMatch
		{
			public Throw_ this[object operand = null, string name = null] => (Throw_)Set(System.Reflection.Emit.OpCodes.Throw, operand, name);

			public Throw_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldfld_ : CodeMatch
		{
			public Ldfld_ this[object operand = null, string name = null] => (Ldfld_)Set(System.Reflection.Emit.OpCodes.Ldfld, operand, name);

			public Ldfld_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldflda_ : CodeMatch
		{
			public Ldflda_ this[object operand = null, string name = null] => (Ldflda_)Set(System.Reflection.Emit.OpCodes.Ldflda, operand, name);

			public Ldflda_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stfld_ : CodeMatch
		{
			public Stfld_ this[object operand = null, string name = null] => (Stfld_)Set(System.Reflection.Emit.OpCodes.Stfld, operand, name);

			public Stfld_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldsfld_ : CodeMatch
		{
			public Ldsfld_ this[object operand = null, string name = null] => (Ldsfld_)Set(System.Reflection.Emit.OpCodes.Ldsfld, operand, name);

			public Ldsfld_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldsflda_ : CodeMatch
		{
			public Ldsflda_ this[object operand = null, string name = null] => (Ldsflda_)Set(System.Reflection.Emit.OpCodes.Ldsflda, operand, name);

			public Ldsflda_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stsfld_ : CodeMatch
		{
			public Stsfld_ this[object operand = null, string name = null] => (Stsfld_)Set(System.Reflection.Emit.OpCodes.Stsfld, operand, name);

			public Stsfld_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stobj_ : CodeMatch
		{
			public Stobj_ this[object operand = null, string name = null] => (Stobj_)Set(System.Reflection.Emit.OpCodes.Stobj, operand, name);

			public Stobj_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I1_Un_ : CodeMatch
		{
			public Conv_Ovf_I1_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I1_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I1_Un, operand, name);

			public Conv_Ovf_I1_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I2_Un_ : CodeMatch
		{
			public Conv_Ovf_I2_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I2_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I2_Un, operand, name);

			public Conv_Ovf_I2_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I4_Un_ : CodeMatch
		{
			public Conv_Ovf_I4_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I4_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I4_Un, operand, name);

			public Conv_Ovf_I4_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I8_Un_ : CodeMatch
		{
			public Conv_Ovf_I8_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I8_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I8_Un, operand, name);

			public Conv_Ovf_I8_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U1_Un_ : CodeMatch
		{
			public Conv_Ovf_U1_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U1_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U1_Un, operand, name);

			public Conv_Ovf_U1_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U2_Un_ : CodeMatch
		{
			public Conv_Ovf_U2_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U2_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U2_Un, operand, name);

			public Conv_Ovf_U2_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U4_Un_ : CodeMatch
		{
			public Conv_Ovf_U4_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U4_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U4_Un, operand, name);

			public Conv_Ovf_U4_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U8_Un_ : CodeMatch
		{
			public Conv_Ovf_U8_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U8_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U8_Un, operand, name);

			public Conv_Ovf_U8_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I_Un_ : CodeMatch
		{
			public Conv_Ovf_I_Un_ this[object operand = null, string name = null] => (Conv_Ovf_I_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I_Un, operand, name);

			public Conv_Ovf_I_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U_Un_ : CodeMatch
		{
			public Conv_Ovf_U_Un_ this[object operand = null, string name = null] => (Conv_Ovf_U_Un_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U_Un, operand, name);

			public Conv_Ovf_U_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Box_ : CodeMatch
		{
			public Box_ this[object operand = null, string name = null] => (Box_)Set(System.Reflection.Emit.OpCodes.Box, operand, name);

			public Box_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Newarr_ : CodeMatch
		{
			public Newarr_ this[object operand = null, string name = null] => (Newarr_)Set(System.Reflection.Emit.OpCodes.Newarr, operand, name);

			public Newarr_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldlen_ : CodeMatch
		{
			public Ldlen_ this[object operand = null, string name = null] => (Ldlen_)Set(System.Reflection.Emit.OpCodes.Ldlen, operand, name);

			public Ldlen_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelema_ : CodeMatch
		{
			public Ldelema_ this[object operand = null, string name = null] => (Ldelema_)Set(System.Reflection.Emit.OpCodes.Ldelema, operand, name);

			public Ldelema_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_I1_ : CodeMatch
		{
			public Ldelem_I1_ this[object operand = null, string name = null] => (Ldelem_I1_)Set(System.Reflection.Emit.OpCodes.Ldelem_I1, operand, name);

			public Ldelem_I1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_U1_ : CodeMatch
		{
			public Ldelem_U1_ this[object operand = null, string name = null] => (Ldelem_U1_)Set(System.Reflection.Emit.OpCodes.Ldelem_U1, operand, name);

			public Ldelem_U1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_I2_ : CodeMatch
		{
			public Ldelem_I2_ this[object operand = null, string name = null] => (Ldelem_I2_)Set(System.Reflection.Emit.OpCodes.Ldelem_I2, operand, name);

			public Ldelem_I2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_U2_ : CodeMatch
		{
			public Ldelem_U2_ this[object operand = null, string name = null] => (Ldelem_U2_)Set(System.Reflection.Emit.OpCodes.Ldelem_U2, operand, name);

			public Ldelem_U2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_I4_ : CodeMatch
		{
			public Ldelem_I4_ this[object operand = null, string name = null] => (Ldelem_I4_)Set(System.Reflection.Emit.OpCodes.Ldelem_I4, operand, name);

			public Ldelem_I4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_U4_ : CodeMatch
		{
			public Ldelem_U4_ this[object operand = null, string name = null] => (Ldelem_U4_)Set(System.Reflection.Emit.OpCodes.Ldelem_U4, operand, name);

			public Ldelem_U4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_I8_ : CodeMatch
		{
			public Ldelem_I8_ this[object operand = null, string name = null] => (Ldelem_I8_)Set(System.Reflection.Emit.OpCodes.Ldelem_I8, operand, name);

			public Ldelem_I8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_I_ : CodeMatch
		{
			public Ldelem_I_ this[object operand = null, string name = null] => (Ldelem_I_)Set(System.Reflection.Emit.OpCodes.Ldelem_I, operand, name);

			public Ldelem_I_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_R4_ : CodeMatch
		{
			public Ldelem_R4_ this[object operand = null, string name = null] => (Ldelem_R4_)Set(System.Reflection.Emit.OpCodes.Ldelem_R4, operand, name);

			public Ldelem_R4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_R8_ : CodeMatch
		{
			public Ldelem_R8_ this[object operand = null, string name = null] => (Ldelem_R8_)Set(System.Reflection.Emit.OpCodes.Ldelem_R8, operand, name);

			public Ldelem_R8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_Ref_ : CodeMatch
		{
			public Ldelem_Ref_ this[object operand = null, string name = null] => (Ldelem_Ref_)Set(System.Reflection.Emit.OpCodes.Ldelem_Ref, operand, name);

			public Ldelem_Ref_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stelem_I_ : CodeMatch
		{
			public Stelem_I_ this[object operand = null, string name = null] => (Stelem_I_)Set(System.Reflection.Emit.OpCodes.Stelem_I, operand, name);

			public Stelem_I_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stelem_I1_ : CodeMatch
		{
			public Stelem_I1_ this[object operand = null, string name = null] => (Stelem_I1_)Set(System.Reflection.Emit.OpCodes.Stelem_I1, operand, name);

			public Stelem_I1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stelem_I2_ : CodeMatch
		{
			public Stelem_I2_ this[object operand = null, string name = null] => (Stelem_I2_)Set(System.Reflection.Emit.OpCodes.Stelem_I2, operand, name);

			public Stelem_I2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stelem_I4_ : CodeMatch
		{
			public Stelem_I4_ this[object operand = null, string name = null] => (Stelem_I4_)Set(System.Reflection.Emit.OpCodes.Stelem_I4, operand, name);

			public Stelem_I4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stelem_I8_ : CodeMatch
		{
			public Stelem_I8_ this[object operand = null, string name = null] => (Stelem_I8_)Set(System.Reflection.Emit.OpCodes.Stelem_I8, operand, name);

			public Stelem_I8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stelem_R4_ : CodeMatch
		{
			public Stelem_R4_ this[object operand = null, string name = null] => (Stelem_R4_)Set(System.Reflection.Emit.OpCodes.Stelem_R4, operand, name);

			public Stelem_R4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stelem_R8_ : CodeMatch
		{
			public Stelem_R8_ this[object operand = null, string name = null] => (Stelem_R8_)Set(System.Reflection.Emit.OpCodes.Stelem_R8, operand, name);

			public Stelem_R8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stelem_Ref_ : CodeMatch
		{
			public Stelem_Ref_ this[object operand = null, string name = null] => (Stelem_Ref_)Set(System.Reflection.Emit.OpCodes.Stelem_Ref, operand, name);

			public Stelem_Ref_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldelem_ : CodeMatch
		{
			public Ldelem_ this[object operand = null, string name = null] => (Ldelem_)Set(System.Reflection.Emit.OpCodes.Ldelem, operand, name);

			public Ldelem_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stelem_ : CodeMatch
		{
			public Stelem_ this[object operand = null, string name = null] => (Stelem_)Set(System.Reflection.Emit.OpCodes.Stelem, operand, name);

			public Stelem_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Unbox_Any_ : CodeMatch
		{
			public Unbox_Any_ this[object operand = null, string name = null] => (Unbox_Any_)Set(System.Reflection.Emit.OpCodes.Unbox_Any, operand, name);

			public Unbox_Any_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I1_ : CodeMatch
		{
			public Conv_Ovf_I1_ this[object operand = null, string name = null] => (Conv_Ovf_I1_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I1, operand, name);

			public Conv_Ovf_I1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U1_ : CodeMatch
		{
			public Conv_Ovf_U1_ this[object operand = null, string name = null] => (Conv_Ovf_U1_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U1, operand, name);

			public Conv_Ovf_U1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I2_ : CodeMatch
		{
			public Conv_Ovf_I2_ this[object operand = null, string name = null] => (Conv_Ovf_I2_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I2, operand, name);

			public Conv_Ovf_I2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U2_ : CodeMatch
		{
			public Conv_Ovf_U2_ this[object operand = null, string name = null] => (Conv_Ovf_U2_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U2, operand, name);

			public Conv_Ovf_U2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I4_ : CodeMatch
		{
			public Conv_Ovf_I4_ this[object operand = null, string name = null] => (Conv_Ovf_I4_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I4, operand, name);

			public Conv_Ovf_I4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U4_ : CodeMatch
		{
			public Conv_Ovf_U4_ this[object operand = null, string name = null] => (Conv_Ovf_U4_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U4, operand, name);

			public Conv_Ovf_U4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I8_ : CodeMatch
		{
			public Conv_Ovf_I8_ this[object operand = null, string name = null] => (Conv_Ovf_I8_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I8, operand, name);

			public Conv_Ovf_I8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U8_ : CodeMatch
		{
			public Conv_Ovf_U8_ this[object operand = null, string name = null] => (Conv_Ovf_U8_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U8, operand, name);

			public Conv_Ovf_U8_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Refanyval_ : CodeMatch
		{
			public Refanyval_ this[object operand = null, string name = null] => (Refanyval_)Set(System.Reflection.Emit.OpCodes.Refanyval, operand, name);

			public Refanyval_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ckfinite_ : CodeMatch
		{
			public Ckfinite_ this[object operand = null, string name = null] => (Ckfinite_)Set(System.Reflection.Emit.OpCodes.Ckfinite, operand, name);

			public Ckfinite_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Mkrefany_ : CodeMatch
		{
			public Mkrefany_ this[object operand = null, string name = null] => (Mkrefany_)Set(System.Reflection.Emit.OpCodes.Mkrefany, operand, name);

			public Mkrefany_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldtoken_ : CodeMatch
		{
			public Ldtoken_ this[object operand = null, string name = null] => (Ldtoken_)Set(System.Reflection.Emit.OpCodes.Ldtoken, operand, name);

			public Ldtoken_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_U2_ : CodeMatch
		{
			public Conv_U2_ this[object operand = null, string name = null] => (Conv_U2_)Set(System.Reflection.Emit.OpCodes.Conv_U2, operand, name);

			public Conv_U2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_U1_ : CodeMatch
		{
			public Conv_U1_ this[object operand = null, string name = null] => (Conv_U1_)Set(System.Reflection.Emit.OpCodes.Conv_U1, operand, name);

			public Conv_U1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_I_ : CodeMatch
		{
			public Conv_I_ this[object operand = null, string name = null] => (Conv_I_)Set(System.Reflection.Emit.OpCodes.Conv_I, operand, name);

			public Conv_I_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_I_ : CodeMatch
		{
			public Conv_Ovf_I_ this[object operand = null, string name = null] => (Conv_Ovf_I_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_I, operand, name);

			public Conv_Ovf_I_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_Ovf_U_ : CodeMatch
		{
			public Conv_Ovf_U_ this[object operand = null, string name = null] => (Conv_Ovf_U_)Set(System.Reflection.Emit.OpCodes.Conv_Ovf_U, operand, name);

			public Conv_Ovf_U_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Add_Ovf_ : CodeMatch
		{
			public Add_Ovf_ this[object operand = null, string name = null] => (Add_Ovf_)Set(System.Reflection.Emit.OpCodes.Add_Ovf, operand, name);

			public Add_Ovf_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Add_Ovf_Un_ : CodeMatch
		{
			public Add_Ovf_Un_ this[object operand = null, string name = null] => (Add_Ovf_Un_)Set(System.Reflection.Emit.OpCodes.Add_Ovf_Un, operand, name);

			public Add_Ovf_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Mul_Ovf_ : CodeMatch
		{
			public Mul_Ovf_ this[object operand = null, string name = null] => (Mul_Ovf_)Set(System.Reflection.Emit.OpCodes.Mul_Ovf, operand, name);

			public Mul_Ovf_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Mul_Ovf_Un_ : CodeMatch
		{
			public Mul_Ovf_Un_ this[object operand = null, string name = null] => (Mul_Ovf_Un_)Set(System.Reflection.Emit.OpCodes.Mul_Ovf_Un, operand, name);

			public Mul_Ovf_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Sub_Ovf_ : CodeMatch
		{
			public Sub_Ovf_ this[object operand = null, string name = null] => (Sub_Ovf_)Set(System.Reflection.Emit.OpCodes.Sub_Ovf, operand, name);

			public Sub_Ovf_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Sub_Ovf_Un_ : CodeMatch
		{
			public Sub_Ovf_Un_ this[object operand = null, string name = null] => (Sub_Ovf_Un_)Set(System.Reflection.Emit.OpCodes.Sub_Ovf_Un, operand, name);

			public Sub_Ovf_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Endfinally_ : CodeMatch
		{
			public Endfinally_ this[object operand = null, string name = null] => (Endfinally_)Set(System.Reflection.Emit.OpCodes.Endfinally, operand, name);

			public Endfinally_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Leave_ : CodeMatch
		{
			public Leave_ this[object operand = null, string name = null] => (Leave_)Set(System.Reflection.Emit.OpCodes.Leave, operand, name);

			public Leave_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Leave_S_ : CodeMatch
		{
			public Leave_S_ this[object operand = null, string name = null] => (Leave_S_)Set(System.Reflection.Emit.OpCodes.Leave_S, operand, name);

			public Leave_S_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stind_I_ : CodeMatch
		{
			public Stind_I_ this[object operand = null, string name = null] => (Stind_I_)Set(System.Reflection.Emit.OpCodes.Stind_I, operand, name);

			public Stind_I_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Conv_U_ : CodeMatch
		{
			public Conv_U_ this[object operand = null, string name = null] => (Conv_U_)Set(System.Reflection.Emit.OpCodes.Conv_U, operand, name);

			public Conv_U_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Prefix7_ : CodeMatch
		{
			public Prefix7_ this[object operand = null, string name = null] => (Prefix7_)Set(System.Reflection.Emit.OpCodes.Prefix7, operand, name);

			public Prefix7_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Prefix6_ : CodeMatch
		{
			public Prefix6_ this[object operand = null, string name = null] => (Prefix6_)Set(System.Reflection.Emit.OpCodes.Prefix6, operand, name);

			public Prefix6_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Prefix5_ : CodeMatch
		{
			public Prefix5_ this[object operand = null, string name = null] => (Prefix5_)Set(System.Reflection.Emit.OpCodes.Prefix5, operand, name);

			public Prefix5_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Prefix4_ : CodeMatch
		{
			public Prefix4_ this[object operand = null, string name = null] => (Prefix4_)Set(System.Reflection.Emit.OpCodes.Prefix4, operand, name);

			public Prefix4_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Prefix3_ : CodeMatch
		{
			public Prefix3_ this[object operand = null, string name = null] => (Prefix3_)Set(System.Reflection.Emit.OpCodes.Prefix3, operand, name);

			public Prefix3_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Prefix2_ : CodeMatch
		{
			public Prefix2_ this[object operand = null, string name = null] => (Prefix2_)Set(System.Reflection.Emit.OpCodes.Prefix2, operand, name);

			public Prefix2_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Prefix1_ : CodeMatch
		{
			public Prefix1_ this[object operand = null, string name = null] => (Prefix1_)Set(System.Reflection.Emit.OpCodes.Prefix1, operand, name);

			public Prefix1_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Prefixref_ : CodeMatch
		{
			public Prefixref_ this[object operand = null, string name = null] => (Prefixref_)Set(System.Reflection.Emit.OpCodes.Prefixref, operand, name);

			public Prefixref_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Arglist_ : CodeMatch
		{
			public Arglist_ this[object operand = null, string name = null] => (Arglist_)Set(System.Reflection.Emit.OpCodes.Arglist, operand, name);

			public Arglist_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ceq_ : CodeMatch
		{
			public Ceq_ this[object operand = null, string name = null] => (Ceq_)Set(System.Reflection.Emit.OpCodes.Ceq, operand, name);

			public Ceq_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Cgt_ : CodeMatch
		{
			public Cgt_ this[object operand = null, string name = null] => (Cgt_)Set(System.Reflection.Emit.OpCodes.Cgt, operand, name);

			public Cgt_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Cgt_Un_ : CodeMatch
		{
			public Cgt_Un_ this[object operand = null, string name = null] => (Cgt_Un_)Set(System.Reflection.Emit.OpCodes.Cgt_Un, operand, name);

			public Cgt_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Clt_ : CodeMatch
		{
			public Clt_ this[object operand = null, string name = null] => (Clt_)Set(System.Reflection.Emit.OpCodes.Clt, operand, name);

			public Clt_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Clt_Un_ : CodeMatch
		{
			public Clt_Un_ this[object operand = null, string name = null] => (Clt_Un_)Set(System.Reflection.Emit.OpCodes.Clt_Un, operand, name);

			public Clt_Un_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldftn_ : CodeMatch
		{
			public Ldftn_ this[object operand = null, string name = null] => (Ldftn_)Set(System.Reflection.Emit.OpCodes.Ldftn, operand, name);

			public Ldftn_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldvirtftn_ : CodeMatch
		{
			public Ldvirtftn_ this[object operand = null, string name = null] => (Ldvirtftn_)Set(System.Reflection.Emit.OpCodes.Ldvirtftn, operand, name);

			public Ldvirtftn_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldarg_ : CodeMatch
		{
			public Ldarg_ this[object operand = null, string name = null] => (Ldarg_)Set(System.Reflection.Emit.OpCodes.Ldarg, operand, name);

			public Ldarg_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldarga_ : CodeMatch
		{
			public Ldarga_ this[object operand = null, string name = null] => (Ldarga_)Set(System.Reflection.Emit.OpCodes.Ldarga, operand, name);

			public Ldarga_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Starg_ : CodeMatch
		{
			public Starg_ this[object operand = null, string name = null] => (Starg_)Set(System.Reflection.Emit.OpCodes.Starg, operand, name);

			public Starg_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldloc_ : CodeMatch
		{
			public Ldloc_ this[object operand = null, string name = null] => (Ldloc_)Set(System.Reflection.Emit.OpCodes.Ldloc, operand, name);

			public Ldloc_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Ldloca_ : CodeMatch
		{
			public Ldloca_ this[object operand = null, string name = null] => (Ldloca_)Set(System.Reflection.Emit.OpCodes.Ldloca, operand, name);

			public Ldloca_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Stloc_ : CodeMatch
		{
			public Stloc_ this[object operand = null, string name = null] => (Stloc_)Set(System.Reflection.Emit.OpCodes.Stloc, operand, name);

			public Stloc_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Localloc_ : CodeMatch
		{
			public Localloc_ this[object operand = null, string name = null] => (Localloc_)Set(System.Reflection.Emit.OpCodes.Localloc, operand, name);

			public Localloc_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Endfilter_ : CodeMatch
		{
			public Endfilter_ this[object operand = null, string name = null] => (Endfilter_)Set(System.Reflection.Emit.OpCodes.Endfilter, operand, name);

			public Endfilter_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Unaligned_ : CodeMatch
		{
			public Unaligned_ this[object operand = null, string name = null] => (Unaligned_)Set(System.Reflection.Emit.OpCodes.Unaligned, operand, name);

			public Unaligned_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Volatile_ : CodeMatch
		{
			public Volatile_ this[object operand = null, string name = null] => (Volatile_)Set(System.Reflection.Emit.OpCodes.Volatile, operand, name);

			public Volatile_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Tailcall_ : CodeMatch
		{
			public Tailcall_ this[object operand = null, string name = null] => (Tailcall_)Set(System.Reflection.Emit.OpCodes.Tailcall, operand, name);

			public Tailcall_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Initobj_ : CodeMatch
		{
			public Initobj_ this[object operand = null, string name = null] => (Initobj_)Set(System.Reflection.Emit.OpCodes.Initobj, operand, name);

			public Initobj_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Constrained_ : CodeMatch
		{
			public Constrained_ this[object operand = null, string name = null] => (Constrained_)Set(System.Reflection.Emit.OpCodes.Constrained, operand, name);

			public Constrained_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Cpblk_ : CodeMatch
		{
			public Cpblk_ this[object operand = null, string name = null] => (Cpblk_)Set(System.Reflection.Emit.OpCodes.Cpblk, operand, name);

			public Cpblk_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Initblk_ : CodeMatch
		{
			public Initblk_ this[object operand = null, string name = null] => (Initblk_)Set(System.Reflection.Emit.OpCodes.Initblk, operand, name);

			public Initblk_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Rethrow_ : CodeMatch
		{
			public Rethrow_ this[object operand = null, string name = null] => (Rethrow_)Set(System.Reflection.Emit.OpCodes.Rethrow, operand, name);

			public Rethrow_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Sizeof_ : CodeMatch
		{
			public Sizeof_ this[object operand = null, string name = null] => (Sizeof_)Set(System.Reflection.Emit.OpCodes.Sizeof, operand, name);

			public Sizeof_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Refanytype_ : CodeMatch
		{
			public Refanytype_ this[object operand = null, string name = null] => (Refanytype_)Set(System.Reflection.Emit.OpCodes.Refanytype, operand, name);

			public Refanytype_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public class Readonly_ : CodeMatch
		{
			public Readonly_ this[object operand = null, string name = null] => (Readonly_)Set(System.Reflection.Emit.OpCodes.Readonly, operand, name);

			public Readonly_(System.Reflection.Emit.OpCode opcode)
				: base(opcode)
			{
			}
		}

		public static Operand_ Operand => new Operand_();

		public static Nop_ Nop => new Nop_(System.Reflection.Emit.OpCodes.Nop);

		public static Break_ Break => new Break_(System.Reflection.Emit.OpCodes.Break);

		public static Ldarg_0_ Ldarg_0 => new Ldarg_0_(System.Reflection.Emit.OpCodes.Ldarg_0);

		public static Ldarg_1_ Ldarg_1 => new Ldarg_1_(System.Reflection.Emit.OpCodes.Ldarg_1);

		public static Ldarg_2_ Ldarg_2 => new Ldarg_2_(System.Reflection.Emit.OpCodes.Ldarg_2);

		public static Ldarg_3_ Ldarg_3 => new Ldarg_3_(System.Reflection.Emit.OpCodes.Ldarg_3);

		public static Ldloc_0_ Ldloc_0 => new Ldloc_0_(System.Reflection.Emit.OpCodes.Ldloc_0);

		public static Ldloc_1_ Ldloc_1 => new Ldloc_1_(System.Reflection.Emit.OpCodes.Ldloc_1);

		public static Ldloc_2_ Ldloc_2 => new Ldloc_2_(System.Reflection.Emit.OpCodes.Ldloc_2);

		public static Ldloc_3_ Ldloc_3 => new Ldloc_3_(System.Reflection.Emit.OpCodes.Ldloc_3);

		public static Stloc_0_ Stloc_0 => new Stloc_0_(System.Reflection.Emit.OpCodes.Stloc_0);

		public static Stloc_1_ Stloc_1 => new Stloc_1_(System.Reflection.Emit.OpCodes.Stloc_1);

		public static Stloc_2_ Stloc_2 => new Stloc_2_(System.Reflection.Emit.OpCodes.Stloc_2);

		public static Stloc_3_ Stloc_3 => new Stloc_3_(System.Reflection.Emit.OpCodes.Stloc_3);

		public static Ldarg_S_ Ldarg_S => new Ldarg_S_(System.Reflection.Emit.OpCodes.Ldarg_S);

		public static Ldarga_S_ Ldarga_S => new Ldarga_S_(System.Reflection.Emit.OpCodes.Ldarga_S);

		public static Starg_S_ Starg_S => new Starg_S_(System.Reflection.Emit.OpCodes.Starg_S);

		public static Ldloc_S_ Ldloc_S => new Ldloc_S_(System.Reflection.Emit.OpCodes.Ldloc_S);

		public static Ldloca_S_ Ldloca_S => new Ldloca_S_(System.Reflection.Emit.OpCodes.Ldloca_S);

		public static Stloc_S_ Stloc_S => new Stloc_S_(System.Reflection.Emit.OpCodes.Stloc_S);

		public static Ldnull_ Ldnull => new Ldnull_(System.Reflection.Emit.OpCodes.Ldnull);

		public static Ldc_I4_M1_ Ldc_I4_M1 => new Ldc_I4_M1_(System.Reflection.Emit.OpCodes.Ldc_I4_M1);

		public static Ldc_I4_0_ Ldc_I4_0 => new Ldc_I4_0_(System.Reflection.Emit.OpCodes.Ldc_I4_0);

		public static Ldc_I4_1_ Ldc_I4_1 => new Ldc_I4_1_(System.Reflection.Emit.OpCodes.Ldc_I4_1);

		public static Ldc_I4_2_ Ldc_I4_2 => new Ldc_I4_2_(System.Reflection.Emit.OpCodes.Ldc_I4_2);

		public static Ldc_I4_3_ Ldc_I4_3 => new Ldc_I4_3_(System.Reflection.Emit.OpCodes.Ldc_I4_3);

		public static Ldc_I4_4_ Ldc_I4_4 => new Ldc_I4_4_(System.Reflection.Emit.OpCodes.Ldc_I4_4);

		public static Ldc_I4_5_ Ldc_I4_5 => new Ldc_I4_5_(System.Reflection.Emit.OpCodes.Ldc_I4_5);

		public static Ldc_I4_6_ Ldc_I4_6 => new Ldc_I4_6_(System.Reflection.Emit.OpCodes.Ldc_I4_6);

		public static Ldc_I4_7_ Ldc_I4_7 => new Ldc_I4_7_(System.Reflection.Emit.OpCodes.Ldc_I4_7);

		public static Ldc_I4_8_ Ldc_I4_8 => new Ldc_I4_8_(System.Reflection.Emit.OpCodes.Ldc_I4_8);

		public static Ldc_I4_S_ Ldc_I4_S => new Ldc_I4_S_(System.Reflection.Emit.OpCodes.Ldc_I4_S);

		public static Ldc_I4_ Ldc_I4 => new Ldc_I4_(System.Reflection.Emit.OpCodes.Ldc_I4);

		public static Ldc_I8_ Ldc_I8 => new Ldc_I8_(System.Reflection.Emit.OpCodes.Ldc_I8);

		public static Ldc_R4_ Ldc_R4 => new Ldc_R4_(System.Reflection.Emit.OpCodes.Ldc_R4);

		public static Ldc_R8_ Ldc_R8 => new Ldc_R8_(System.Reflection.Emit.OpCodes.Ldc_R8);

		public static Dup_ Dup => new Dup_(System.Reflection.Emit.OpCodes.Dup);

		public static Pop_ Pop => new Pop_(System.Reflection.Emit.OpCodes.Pop);

		public static Jmp_ Jmp => new Jmp_(System.Reflection.Emit.OpCodes.Jmp);

		public static Call_ Call => new Call_(System.Reflection.Emit.OpCodes.Call);

		public static Calli_ Calli => new Calli_(System.Reflection.Emit.OpCodes.Calli);

		public static Ret_ Ret => new Ret_(System.Reflection.Emit.OpCodes.Ret);

		public static Br_S_ Br_S => new Br_S_(System.Reflection.Emit.OpCodes.Br_S);

		public static Brfalse_S_ Brfalse_S => new Brfalse_S_(System.Reflection.Emit.OpCodes.Brfalse_S);

		public static Brtrue_S_ Brtrue_S => new Brtrue_S_(System.Reflection.Emit.OpCodes.Brtrue_S);

		public static Beq_S_ Beq_S => new Beq_S_(System.Reflection.Emit.OpCodes.Beq_S);

		public static Bge_S_ Bge_S => new Bge_S_(System.Reflection.Emit.OpCodes.Bge_S);

		public static Bgt_S_ Bgt_S => new Bgt_S_(System.Reflection.Emit.OpCodes.Bgt_S);

		public static Ble_S_ Ble_S => new Ble_S_(System.Reflection.Emit.OpCodes.Ble_S);

		public static Blt_S_ Blt_S => new Blt_S_(System.Reflection.Emit.OpCodes.Blt_S);

		public static Bne_Un_S_ Bne_Un_S => new Bne_Un_S_(System.Reflection.Emit.OpCodes.Bne_Un_S);

		public static Bge_Un_S_ Bge_Un_S => new Bge_Un_S_(System.Reflection.Emit.OpCodes.Bge_Un_S);

		public static Bgt_Un_S_ Bgt_Un_S => new Bgt_Un_S_(System.Reflection.Emit.OpCodes.Bgt_Un_S);

		public static Ble_Un_S_ Ble_Un_S => new Ble_Un_S_(System.Reflection.Emit.OpCodes.Ble_Un_S);

		public static Blt_Un_S_ Blt_Un_S => new Blt_Un_S_(System.Reflection.Emit.OpCodes.Blt_Un_S);

		public static Br_ Br => new Br_(System.Reflection.Emit.OpCodes.Br);

		public static Brfalse_ Brfalse => new Brfalse_(System.Reflection.Emit.OpCodes.Brfalse);

		public static Brtrue_ Brtrue => new Brtrue_(System.Reflection.Emit.OpCodes.Brtrue);

		public static Beq_ Beq => new Beq_(System.Reflection.Emit.OpCodes.Beq);

		public static Bge_ Bge => new Bge_(System.Reflection.Emit.OpCodes.Bge);

		public static Bgt_ Bgt => new Bgt_(System.Reflection.Emit.OpCodes.Bgt);

		public static Ble_ Ble => new Ble_(System.Reflection.Emit.OpCodes.Ble);

		public static Blt_ Blt => new Blt_(System.Reflection.Emit.OpCodes.Blt);

		public static Bne_Un_ Bne_Un => new Bne_Un_(System.Reflection.Emit.OpCodes.Bne_Un);

		public static Bge_Un_ Bge_Un => new Bge_Un_(System.Reflection.Emit.OpCodes.Bge_Un);

		public static Bgt_Un_ Bgt_Un => new Bgt_Un_(System.Reflection.Emit.OpCodes.Bgt_Un);

		public static Ble_Un_ Ble_Un => new Ble_Un_(System.Reflection.Emit.OpCodes.Ble_Un);

		public static Blt_Un_ Blt_Un => new Blt_Un_(System.Reflection.Emit.OpCodes.Blt_Un);

		public static Switch_ Switch => new Switch_(System.Reflection.Emit.OpCodes.Switch);

		public static Ldind_I1_ Ldind_I1 => new Ldind_I1_(System.Reflection.Emit.OpCodes.Ldind_I1);

		public static Ldind_U1_ Ldind_U1 => new Ldind_U1_(System.Reflection.Emit.OpCodes.Ldind_U1);

		public static Ldind_I2_ Ldind_I2 => new Ldind_I2_(System.Reflection.Emit.OpCodes.Ldind_I2);

		public static Ldind_U2_ Ldind_U2 => new Ldind_U2_(System.Reflection.Emit.OpCodes.Ldind_U2);

		public static Ldind_I4_ Ldind_I4 => new Ldind_I4_(System.Reflection.Emit.OpCodes.Ldind_I4);

		public static Ldind_U4_ Ldind_U4 => new Ldind_U4_(System.Reflection.Emit.OpCodes.Ldind_U4);

		public static Ldind_I8_ Ldind_I8 => new Ldind_I8_(System.Reflection.Emit.OpCodes.Ldind_I8);

		public static Ldind_I_ Ldind_I => new Ldind_I_(System.Reflection.Emit.OpCodes.Ldind_I);

		public static Ldind_R4_ Ldind_R4 => new Ldind_R4_(System.Reflection.Emit.OpCodes.Ldind_R4);

		public static Ldind_R8_ Ldind_R8 => new Ldind_R8_(System.Reflection.Emit.OpCodes.Ldind_R8);

		public static Ldind_Ref_ Ldind_Ref => new Ldind_Ref_(System.Reflection.Emit.OpCodes.Ldind_Ref);

		public static Stind_Ref_ Stind_Ref => new Stind_Ref_(System.Reflection.Emit.OpCodes.Stind_Ref);

		public static Stind_I1_ Stind_I1 => new Stind_I1_(System.Reflection.Emit.OpCodes.Stind_I1);

		public static Stind_I2_ Stind_I2 => new Stind_I2_(System.Reflection.Emit.OpCodes.Stind_I2);

		public static Stind_I4_ Stind_I4 => new Stind_I4_(System.Reflection.Emit.OpCodes.Stind_I4);

		public static Stind_I8_ Stind_I8 => new Stind_I8_(System.Reflection.Emit.OpCodes.Stind_I8);

		public static Stind_R4_ Stind_R4 => new Stind_R4_(System.Reflection.Emit.OpCodes.Stind_R4);

		public static Stind_R8_ Stind_R8 => new Stind_R8_(System.Reflection.Emit.OpCodes.Stind_R8);

		public static Add_ Add => new Add_(System.Reflection.Emit.OpCodes.Add);

		public static Sub_ Sub => new Sub_(System.Reflection.Emit.OpCodes.Sub);

		public static Mul_ Mul => new Mul_(System.Reflection.Emit.OpCodes.Mul);

		public static Div_ Div => new Div_(System.Reflection.Emit.OpCodes.Div);

		public static Div_Un_ Div_Un => new Div_Un_(System.Reflection.Emit.OpCodes.Div_Un);

		public static Rem_ Rem => new Rem_(System.Reflection.Emit.OpCodes.Rem);

		public static Rem_Un_ Rem_Un => new Rem_Un_(System.Reflection.Emit.OpCodes.Rem_Un);

		public static And_ And => new And_(System.Reflection.Emit.OpCodes.And);

		public static Or_ Or => new Or_(System.Reflection.Emit.OpCodes.Or);

		public static Xor_ Xor => new Xor_(System.Reflection.Emit.OpCodes.Xor);

		public static Shl_ Shl => new Shl_(System.Reflection.Emit.OpCodes.Shl);

		public static Shr_ Shr => new Shr_(System.Reflection.Emit.OpCodes.Shr);

		public static Shr_Un_ Shr_Un => new Shr_Un_(System.Reflection.Emit.OpCodes.Shr_Un);

		public static Neg_ Neg => new Neg_(System.Reflection.Emit.OpCodes.Neg);

		public static Not_ Not => new Not_(System.Reflection.Emit.OpCodes.Not);

		public static Conv_I1_ Conv_I1 => new Conv_I1_(System.Reflection.Emit.OpCodes.Conv_I1);

		public static Conv_I2_ Conv_I2 => new Conv_I2_(System.Reflection.Emit.OpCodes.Conv_I2);

		public static Conv_I4_ Conv_I4 => new Conv_I4_(System.Reflection.Emit.OpCodes.Conv_I4);

		public static Conv_I8_ Conv_I8 => new Conv_I8_(System.Reflection.Emit.OpCodes.Conv_I8);

		public static Conv_R4_ Conv_R4 => new Conv_R4_(System.Reflection.Emit.OpCodes.Conv_R4);

		public static Conv_R8_ Conv_R8 => new Conv_R8_(System.Reflection.Emit.OpCodes.Conv_R8);

		public static Conv_U4_ Conv_U4 => new Conv_U4_(System.Reflection.Emit.OpCodes.Conv_U4);

		public static Conv_U8_ Conv_U8 => new Conv_U8_(System.Reflection.Emit.OpCodes.Conv_U8);

		public static Callvirt_ Callvirt => new Callvirt_(System.Reflection.Emit.OpCodes.Callvirt);

		public static Cpobj_ Cpobj => new Cpobj_(System.Reflection.Emit.OpCodes.Cpobj);

		public static Ldobj_ Ldobj => new Ldobj_(System.Reflection.Emit.OpCodes.Ldobj);

		public static Ldstr_ Ldstr => new Ldstr_(System.Reflection.Emit.OpCodes.Ldstr);

		public static Newobj_ Newobj => new Newobj_(System.Reflection.Emit.OpCodes.Newobj);

		public static Castclass_ Castclass => new Castclass_(System.Reflection.Emit.OpCodes.Castclass);

		public static Isinst_ Isinst => new Isinst_(System.Reflection.Emit.OpCodes.Isinst);

		public static Conv_R_Un_ Conv_R_Un => new Conv_R_Un_(System.Reflection.Emit.OpCodes.Conv_R_Un);

		public static Unbox_ Unbox => new Unbox_(System.Reflection.Emit.OpCodes.Unbox);

		public static Throw_ Throw => new Throw_(System.Reflection.Emit.OpCodes.Throw);

		public static Ldfld_ Ldfld => new Ldfld_(System.Reflection.Emit.OpCodes.Ldfld);

		public static Ldflda_ Ldflda => new Ldflda_(System.Reflection.Emit.OpCodes.Ldflda);

		public static Stfld_ Stfld => new Stfld_(System.Reflection.Emit.OpCodes.Stfld);

		public static Ldsfld_ Ldsfld => new Ldsfld_(System.Reflection.Emit.OpCodes.Ldsfld);

		public static Ldsflda_ Ldsflda => new Ldsflda_(System.Reflection.Emit.OpCodes.Ldsflda);

		public static Stsfld_ Stsfld => new Stsfld_(System.Reflection.Emit.OpCodes.Stsfld);

		public static Stobj_ Stobj => new Stobj_(System.Reflection.Emit.OpCodes.Stobj);

		public static Conv_Ovf_I1_Un_ Conv_Ovf_I1_Un => new Conv_Ovf_I1_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_I1_Un);

		public static Conv_Ovf_I2_Un_ Conv_Ovf_I2_Un => new Conv_Ovf_I2_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_I2_Un);

		public static Conv_Ovf_I4_Un_ Conv_Ovf_I4_Un => new Conv_Ovf_I4_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_I4_Un);

		public static Conv_Ovf_I8_Un_ Conv_Ovf_I8_Un => new Conv_Ovf_I8_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_I8_Un);

		public static Conv_Ovf_U1_Un_ Conv_Ovf_U1_Un => new Conv_Ovf_U1_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_U1_Un);

		public static Conv_Ovf_U2_Un_ Conv_Ovf_U2_Un => new Conv_Ovf_U2_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_U2_Un);

		public static Conv_Ovf_U4_Un_ Conv_Ovf_U4_Un => new Conv_Ovf_U4_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_U4_Un);

		public static Conv_Ovf_U8_Un_ Conv_Ovf_U8_Un => new Conv_Ovf_U8_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_U8_Un);

		public static Conv_Ovf_I_Un_ Conv_Ovf_I_Un => new Conv_Ovf_I_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_I_Un);

		public static Conv_Ovf_U_Un_ Conv_Ovf_U_Un => new Conv_Ovf_U_Un_(System.Reflection.Emit.OpCodes.Conv_Ovf_U_Un);

		public static Box_ Box => new Box_(System.Reflection.Emit.OpCodes.Box);

		public static Newarr_ Newarr => new Newarr_(System.Reflection.Emit.OpCodes.Newarr);

		public static Ldlen_ Ldlen => new Ldlen_(System.Reflection.Emit.OpCodes.Ldlen);

		public static Ldelema_ Ldelema => new Ldelema_(System.Reflection.Emit.OpCodes.Ldelema);

		public static Ldelem_I1_ Ldelem_I1 => new Ldelem_I1_(System.Reflection.Emit.OpCodes.Ldelem_I1);

		public static Ldelem_U1_ Ldelem_U1 => new Ldelem_U1_(System.Reflection.Emit.OpCodes.Ldelem_U1);

		public static Ldelem_I2_ Ldelem_I2 => new Ldelem_I2_(System.Reflection.Emit.OpCodes.Ldelem_I2);

		public static Ldelem_U2_ Ldelem_U2 => new Ldelem_U2_(System.Reflection.Emit.OpCodes.Ldelem_U2);

		public static Ldelem_I4_ Ldelem_I4 => new Ldelem_I4_(System.Reflection.Emit.OpCodes.Ldelem_I4);

		public static Ldelem_U4_ Ldelem_U4 => new Ldelem_U4_(System.Reflection.Emit.OpCodes.Ldelem_U4);

		public static Ldelem_I8_ Ldelem_I8 => new Ldelem_I8_(System.Reflection.Emit.OpCodes.Ldelem_I8);

		public static Ldelem_I_ Ldelem_I => new Ldelem_I_(System.Reflection.Emit.OpCodes.Ldelem_I);

		public static Ldelem_R4_ Ldelem_R4 => new Ldelem_R4_(System.Reflection.Emit.OpCodes.Ldelem_R4);

		public static Ldelem_R8_ Ldelem_R8 => new Ldelem_R8_(System.Reflection.Emit.OpCodes.Ldelem_R8);

		public static Ldelem_Ref_ Ldelem_Ref => new Ldelem_Ref_(System.Reflection.Emit.OpCodes.Ldelem_Ref);

		public static Stelem_I_ Stelem_I => new Stelem_I_(System.Reflection.Emit.OpCodes.Stelem_I);

		public static Stelem_I1_ Stelem_I1 => new Stelem_I1_(System.Reflection.Emit.OpCodes.Stelem_I1);

		public static Stelem_I2_ Stelem_I2 => new Stelem_I2_(System.Reflection.Emit.OpCodes.Stelem_I2);

		public static Stelem_I4_ Stelem_I4 => new Stelem_I4_(System.Reflection.Emit.OpCodes.Stelem_I4);

		public static Stelem_I8_ Stelem_I8 => new Stelem_I8_(System.Reflection.Emit.OpCodes.Stelem_I8);

		public static Stelem_R4_ Stelem_R4 => new Stelem_R4_(System.Reflection.Emit.OpCodes.Stelem_R4);

		public static Stelem_R8_ Stelem_R8 => new Stelem_R8_(System.Reflection.Emit.OpCodes.Stelem_R8);

		public static Stelem_Ref_ Stelem_Ref => new Stelem_Ref_(System.Reflection.Emit.OpCodes.Stelem_Ref);

		public static Ldelem_ Ldelem => new Ldelem_(System.Reflection.Emit.OpCodes.Ldelem);

		public static Stelem_ Stelem => new Stelem_(System.Reflection.Emit.OpCodes.Stelem);

		public static Unbox_Any_ Unbox_Any => new Unbox_Any_(System.Reflection.Emit.OpCodes.Unbox_Any);

		public static Conv_Ovf_I1_ Conv_Ovf_I1 => new Conv_Ovf_I1_(System.Reflection.Emit.OpCodes.Conv_Ovf_I1);

		public static Conv_Ovf_U1_ Conv_Ovf_U1 => new Conv_Ovf_U1_(System.Reflection.Emit.OpCodes.Conv_Ovf_U1);

		public static Conv_Ovf_I2_ Conv_Ovf_I2 => new Conv_Ovf_I2_(System.Reflection.Emit.OpCodes.Conv_Ovf_I2);

		public static Conv_Ovf_U2_ Conv_Ovf_U2 => new Conv_Ovf_U2_(System.Reflection.Emit.OpCodes.Conv_Ovf_U2);

		public static Conv_Ovf_I4_ Conv_Ovf_I4 => new Conv_Ovf_I4_(System.Reflection.Emit.OpCodes.Conv_Ovf_I4);

		public static Conv_Ovf_U4_ Conv_Ovf_U4 => new Conv_Ovf_U4_(System.Reflection.Emit.OpCodes.Conv_Ovf_U4);

		public static Conv_Ovf_I8_ Conv_Ovf_I8 => new Conv_Ovf_I8_(System.Reflection.Emit.OpCodes.Conv_Ovf_I8);

		public static Conv_Ovf_U8_ Conv_Ovf_U8 => new Conv_Ovf_U8_(System.Reflection.Emit.OpCodes.Conv_Ovf_U8);

		public static Refanyval_ Refanyval => new Refanyval_(System.Reflection.Emit.OpCodes.Refanyval);

		public static Ckfinite_ Ckfinite => new Ckfinite_(System.Reflection.Emit.OpCodes.Ckfinite);

		public static Mkrefany_ Mkrefany => new Mkrefany_(System.Reflection.Emit.OpCodes.Mkrefany);

		public static Ldtoken_ Ldtoken => new Ldtoken_(System.Reflection.Emit.OpCodes.Ldtoken);

		public static Conv_U2_ Conv_U2 => new Conv_U2_(System.Reflection.Emit.OpCodes.Conv_U2);

		public static Conv_U1_ Conv_U1 => new Conv_U1_(System.Reflection.Emit.OpCodes.Conv_U1);

		public static Conv_I_ Conv_I => new Conv_I_(System.Reflection.Emit.OpCodes.Conv_I);

		public static Conv_Ovf_I_ Conv_Ovf_I => new Conv_Ovf_I_(System.Reflection.Emit.OpCodes.Conv_Ovf_I);

		public static Conv_Ovf_U_ Conv_Ovf_U => new Conv_Ovf_U_(System.Reflection.Emit.OpCodes.Conv_Ovf_U);

		public static Add_Ovf_ Add_Ovf => new Add_Ovf_(System.Reflection.Emit.OpCodes.Add_Ovf);

		public static Add_Ovf_Un_ Add_Ovf_Un => new Add_Ovf_Un_(System.Reflection.Emit.OpCodes.Add_Ovf_Un);

		public static Mul_Ovf_ Mul_Ovf => new Mul_Ovf_(System.Reflection.Emit.OpCodes.Mul_Ovf);

		public static Mul_Ovf_Un_ Mul_Ovf_Un => new Mul_Ovf_Un_(System.Reflection.Emit.OpCodes.Mul_Ovf_Un);

		public static Sub_Ovf_ Sub_Ovf => new Sub_Ovf_(System.Reflection.Emit.OpCodes.Sub_Ovf);

		public static Sub_Ovf_Un_ Sub_Ovf_Un => new Sub_Ovf_Un_(System.Reflection.Emit.OpCodes.Sub_Ovf_Un);

		public static Endfinally_ Endfinally => new Endfinally_(System.Reflection.Emit.OpCodes.Endfinally);

		public static Leave_ Leave => new Leave_(System.Reflection.Emit.OpCodes.Leave);

		public static Leave_S_ Leave_S => new Leave_S_(System.Reflection.Emit.OpCodes.Leave_S);

		public static Stind_I_ Stind_I => new Stind_I_(System.Reflection.Emit.OpCodes.Stind_I);

		public static Conv_U_ Conv_U => new Conv_U_(System.Reflection.Emit.OpCodes.Conv_U);

		public static Prefix7_ Prefix7 => new Prefix7_(System.Reflection.Emit.OpCodes.Prefix7);

		public static Prefix6_ Prefix6 => new Prefix6_(System.Reflection.Emit.OpCodes.Prefix6);

		public static Prefix5_ Prefix5 => new Prefix5_(System.Reflection.Emit.OpCodes.Prefix5);

		public static Prefix4_ Prefix4 => new Prefix4_(System.Reflection.Emit.OpCodes.Prefix4);

		public static Prefix3_ Prefix3 => new Prefix3_(System.Reflection.Emit.OpCodes.Prefix3);

		public static Prefix2_ Prefix2 => new Prefix2_(System.Reflection.Emit.OpCodes.Prefix2);

		public static Prefix1_ Prefix1 => new Prefix1_(System.Reflection.Emit.OpCodes.Prefix1);

		public static Prefixref_ Prefixref => new Prefixref_(System.Reflection.Emit.OpCodes.Prefixref);

		public static Arglist_ Arglist => new Arglist_(System.Reflection.Emit.OpCodes.Arglist);

		public static Ceq_ Ceq => new Ceq_(System.Reflection.Emit.OpCodes.Ceq);

		public static Cgt_ Cgt => new Cgt_(System.Reflection.Emit.OpCodes.Cgt);

		public static Cgt_Un_ Cgt_Un => new Cgt_Un_(System.Reflection.Emit.OpCodes.Cgt_Un);

		public static Clt_ Clt => new Clt_(System.Reflection.Emit.OpCodes.Clt);

		public static Clt_Un_ Clt_Un => new Clt_Un_(System.Reflection.Emit.OpCodes.Clt_Un);

		public static Ldftn_ Ldftn => new Ldftn_(System.Reflection.Emit.OpCodes.Ldftn);

		public static Ldvirtftn_ Ldvirtftn => new Ldvirtftn_(System.Reflection.Emit.OpCodes.Ldvirtftn);

		public static Ldarg_ Ldarg => new Ldarg_(System.Reflection.Emit.OpCodes.Ldarg);

		public static Ldarga_ Ldarga => new Ldarga_(System.Reflection.Emit.OpCodes.Ldarga);

		public static Starg_ Starg => new Starg_(System.Reflection.Emit.OpCodes.Starg);

		public static Ldloc_ Ldloc => new Ldloc_(System.Reflection.Emit.OpCodes.Ldloc);

		public static Ldloca_ Ldloca => new Ldloca_(System.Reflection.Emit.OpCodes.Ldloca);

		public static Stloc_ Stloc => new Stloc_(System.Reflection.Emit.OpCodes.Stloc);

		public static Localloc_ Localloc => new Localloc_(System.Reflection.Emit.OpCodes.Localloc);

		public static Endfilter_ Endfilter => new Endfilter_(System.Reflection.Emit.OpCodes.Endfilter);

		public static Unaligned_ Unaligned => new Unaligned_(System.Reflection.Emit.OpCodes.Unaligned);

		public static Volatile_ Volatile => new Volatile_(System.Reflection.Emit.OpCodes.Volatile);

		public static Tailcall_ Tailcall => new Tailcall_(System.Reflection.Emit.OpCodes.Tailcall);

		public static Initobj_ Initobj => new Initobj_(System.Reflection.Emit.OpCodes.Initobj);

		public static Constrained_ Constrained => new Constrained_(System.Reflection.Emit.OpCodes.Constrained);

		public static Cpblk_ Cpblk => new Cpblk_(System.Reflection.Emit.OpCodes.Cpblk);

		public static Initblk_ Initblk => new Initblk_(System.Reflection.Emit.OpCodes.Initblk);

		public static Rethrow_ Rethrow => new Rethrow_(System.Reflection.Emit.OpCodes.Rethrow);

		public static Sizeof_ Sizeof => new Sizeof_(System.Reflection.Emit.OpCodes.Sizeof);

		public static Refanytype_ Refanytype => new Refanytype_(System.Reflection.Emit.OpCodes.Refanytype);

		public static Readonly_ Readonly => new Readonly_(System.Reflection.Emit.OpCodes.Readonly);
	}
	public class CodeMatch : CodeInstruction
	{
		public string name;

		public HashSet<System.Reflection.Emit.OpCode> opcodeSet = new HashSet<System.Reflection.Emit.OpCode>();

		public List<object> operands = new List<object>();

		public List<int> jumpsFrom = new List<int>();

		public List<int> jumpsTo = new List<int>();

		public Func<CodeInstruction, bool> predicate;

		[Obsolete("Use opcodeSet instead")]
		[EditorBrowsable(EditorBrowsableState.Never)]
		public List<System.Reflection.Emit.OpCode> opcodes
		{
			get
			{
				return opcodeSet.ToList();
			}
			set
			{
				HashSet<System.Reflection.Emit.OpCode> hashSet = new HashSet<System.Reflection.Emit.OpCode>();
				foreach (System.Reflection.Emit.OpCode item in value)
				{
					hashSet.Add(item);
				}
				opcodeSet = hashSet;
			}
		}

		internal CodeMatch Set(object operand, string name)
		{
			if (base.operand == null)
			{
				base.operand = operand;
			}
			if (operand != null)
			{
				operands.Add(operand);
			}
			if (this.name == null)
			{
				this.name = name;
			}
			return this;
		}

		internal CodeMatch Set(System.Reflection.Emit.OpCode opcode, object operand, string name)
		{
			base.opcode = opcode;
			opcodeSet.Add(opcode);
			if (base.operand == null)
			{
				base.operand = operand;
			}
			if (operand != null)
			{
				operands.Add(operand);
			}
			if (this.name == null)
			{
				this.name = name;
			}
			return this;
		}

		public CodeMatch(System.Reflection.Emit.OpCode? opcode = null, object operand = null, string name = null)
		{
			if (opcode.HasValue)
			{
				System.Reflection.Emit.OpCode item = (base.opcode = opcode.GetValueOrDefault());
				opcodeSet.Add(item);
			}
			if (operand != null)
			{
				operands.Add(operand);
			}
			base.operand = operand;
			this.name = name;
		}

		public static CodeMatch WithOpcodes(HashSet<System.Reflection.Emit.OpCode> opcodes, object operand = null, string name = null)
		{
			return new CodeMatch(null, operand, name)
			{
				opcodeSet = opcodes
			};
		}

		public CodeMatch(Expression<Action> expression, string name = null)
		{
			opcodeSet.UnionWith(CodeInstructionExtensions.opcodesCalling);
			operand = SymbolExtensions.GetMethodInfo(expression);
			if (operand != null)
			{
				operands.Add(operand);
			}
			this.name = name;
		}

		public CodeMatch(LambdaExpression expression, string name = null)
		{
			opcodeSet.UnionWith(CodeInstructionExtensions.opcodesCalling);
			operand = SymbolExtensions.GetMethodInfo(expression);
			if (operand != null)
			{
				operands.Add(operand);
			}
			this.name = name;
		}

		public CodeMatch(CodeInstruction instruction, string name = null)
			: this(instruction.opcode, instruction.operand, name)
		{
		}

		public CodeMatch(Func<CodeInstruction, bool> predicate, string name = null)
		{
			this.predicate = predicate;
			this.name = name;
		}

		internal bool Matches(List<CodeInstruction> codes, CodeInstruction instruction)
		{
			if (predicate != null)
			{
				return predicate(instruction);
			}
			if (opcodeSet.Count > 0 && !opcodeSet.Contains(instruction.opcode))
			{
				return false;
			}
			if (operands.Count > 0 && !operands.Contains(instruction.operand))
			{
				return false;
			}
			if (labels.Count > 0 && !labels.Intersect(instruction.labels).Any())
			{
				return false;
			}
			if (blocks.Count > 0 && !blocks.Intersect(instruction.blocks).Any())
			{
				return false;
			}
			if (jumpsFrom.Count > 0 && !jumpsFrom.Select((int index) => codes[index].operand).OfType<Label>().Intersect(instruction.labels)
				.Any())
			{
				return false;
			}
			if (jumpsTo.Count > 0)
			{
				object obj = instruction.operand;
				if (obj == null || obj.GetType() != typeof(Label))
				{
					return false;
				}
				Label label = (Label)obj;
				IEnumerable<int> second = from idx in Enumerable.Range(0, codes.Count)
					where codes[idx].labels.Contains(label)
					select idx;
				if (!jumpsTo.Intersect(second).Any())
				{
					return false;
				}
			}
			return true;
		}

		public static CodeMatch IsLdarg(int? n = null)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.IsLdarg(n));
		}

		public static CodeMatch IsLdarga(int? n = null)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.IsLdarga(n));
		}

		public static CodeMatch IsStarg(int? n = null)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.IsStarg(n));
		}

		public static CodeMatch IsLdloc(LocalBuilder variable = null)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.IsLdloc(variable));
		}

		public static CodeMatch IsStloc(LocalBuilder variable = null)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.IsStloc(variable));
		}

		public static CodeMatch Calls(MethodInfo method)
		{
			return WithOpcodes(CodeInstructionExtensions.opcodesCalling, method);
		}

		public static CodeMatch LoadsConstant()
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.LoadsConstant());
		}

		public static CodeMatch LoadsConstant(long number)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.LoadsConstant(number));
		}

		public static CodeMatch LoadsConstant(double number)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.LoadsConstant(number));
		}

		public static CodeMatch LoadsConstant(Enum e)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.LoadsConstant(e));
		}

		public static CodeMatch LoadsConstant(string str)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.LoadsConstant(str));
		}

		public static CodeMatch LoadsField(FieldInfo field, bool byAddress = false)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.LoadsField(field, byAddress));
		}

		public static CodeMatch StoresField(FieldInfo field)
		{
			return new CodeMatch((CodeInstruction instruction) => instruction.StoresField(field));
		}

		public static CodeMatch Calls(Expression<Action> expression)
		{
			return new CodeMatch(expression);
		}

		public static CodeMatch Calls(LambdaExpression expression)
		{
			return new CodeMatch(expression);
		}

		public static CodeMatch LoadsLocal(bool useAddress = false, string name = null)
		{
			return WithOpcodes(useAddress ? CodeInstructionExtensions.opcodesLoadingLocalByAddress : CodeInstructionExtensions.opcodesLoadingLocalNormal, null, name);
		}

		public static CodeMatch StoresLocal(string name = null)
		{
			return WithOpcodes(CodeInstructionExtensions.opcodesStoringLocal, null, name);
		}

		public static CodeMatch LoadsArgument(bool useAddress = false, string name = null)
		{
			return WithOpcodes(useAddress ? CodeInstructionExtensions.opcodesLoadingArgumentByAddress : CodeInstructionExtensions.opcodesLoadingArgumentNormal, null, name);
		}

		public static CodeMatch StoresArgument(string name = null)
		{
			return WithOpcodes(CodeInstructionExtensions.opcodesStoringArgument, null, name);
		}

		public static CodeMatch Branches(string name = null)
		{
			return WithOpcodes(CodeInstructionExtensions.opcodesBranching, null, name);
		}

		public override string ToString()
		{
			string text = "[";
			if (name != null)
			{
				text = text + name + ": ";
			}
			if (opcodeSet.Count > 0)
			{
				text = text + "opcodes=" + opcodeSet.Join() + " ";
			}
			if (operands.Count > 0)
			{
				text = text + "operands=" + operands.Join() + " ";
			}
			if (labels.Count > 0)
			{
				text = text + "labels=" + labels.Join() + " ";
			}
			if (blocks.Count > 0)
			{
				text = text + "blocks=" + blocks.Join() + " ";
			}
			if (jumpsFrom.Count > 0)
			{
				text = text + "jumpsFrom=" + jumpsFrom.Join() + " ";
			}
			if (jumpsTo.Count > 0)
			{
				text = text + "jumpsTo=" + jumpsTo.Join() + " ";
			}
			if (predicate != null)
			{
				text += "predicate=yes ";
			}
			return text.TrimEnd() + "]";
		}
	}
	public class CodeMatcher
	{
		public delegate bool ErrorHandler(CodeMatcher matcher, string error);

		private enum MatchPosition
		{
			Start,
			End
		}

		private delegate CodeMatcher MatchDelegate();

		private readonly ILGenerator generator;

		private readonly List<CodeInstruction> codes = new List<CodeInstruction>();

		private Dictionary<string, CodeInstruction> lastMatches = new Dictionary<string, CodeInstruction>();

		private string lastError;

		private MatchDelegate lastMatchCall;

		private ErrorHandler errorHandler;

		public int Pos { get; private set; } = -1;

		public int Length => codes.Count;

		public bool IsValid
		{
			get
			{
				if (Pos >= 0)
				{
					return Pos < Length;
				}
				return false;
			}
		}

		public bool IsInvalid
		{
			get
			{
				if (Pos >= 0)
				{
					return Pos >= Length;
				}
				return true;
			}
		}

		public int Remaining => Length - Math.Max(0, Pos);

		public ref System.Reflection.Emit.OpCode Opcode => ref codes[Pos].opcode;

		public ref object Operand => ref codes[Pos].operand;

		public ref List<Label> Labels => ref codes[Pos].labels;

		public ref List<ExceptionBlock> Blocks => ref codes[Pos].blocks;

		public CodeInstruction Instruction => codes[Pos];

		private void FixStart()
		{
			Pos = Math.Max(0, Pos);
		}

		private T HandleException<T>(string error, T defaultValue)
		{
			if (errorHandler != null && errorHandler(this, error))
			{
				return defaultValue;
			}
			lastError = error;
			throw new InvalidOperationException(error);
		}

		private void HandleException(string error)
		{
			lastError = error;
			if (errorHandler != null)
			{
				errorHandler(this, error);
				return;
			}
			throw new InvalidOperationException(error);
		}

		private void SetOutOfBounds(int direction)
		{
			Pos = ((direction > 0) ? Length : (-1));
		}

		public CodeMatcher()
		{
		}

		public CodeMatcher(IEnumerable<CodeInstruction> instructions, ILGenerator generator = null)
		{
			this.generator = generator;
			codes = instructions.Select((CodeInstruction c) => new CodeInstruction(c)).ToList();
		}

		public CodeMatcher Clone()
		{
			return new CodeMatcher(codes, generator)
			{
				Pos = Pos,
				lastMatches = new Dictionary<string, CodeInstruction>(lastMatches),
				lastError = lastError,
				lastMatchCall = lastMatchCall,
				errorHandler = errorHandler
			};
		}

		public CodeMatcher Reset(bool atFirstInstruction = true)
		{
			Pos = ((!atFirstInstruction) ? (-1) : 0);
			lastMatches.Clear();
			lastError = null;
			lastMatchCall = null;
			return this;
		}

		public CodeInstruction InstructionAt(int offset)
		{
			return codes[Pos + offset];
		}

		public List<CodeInstruction> Instructions()
		{
			return codes;
		}

		public IEnumerable<CodeInstruction> InstructionEnumeration()
		{
			return codes.AsEnumerable();
		}

		public List<CodeInstruction> Instructions(int count)
		{
			if (Pos < 0 || Pos + count > Length)
			{
				return HandleException("Cannot retrieve instructions: range is out-of-bounds.", new List<CodeInstruction>());
			}
			return (from c in codes.GetRange(Pos, count)
				select new CodeInstruction(c)).ToList();
		}

		public List<CodeInstruction> InstructionsInRange(int start, int end)
		{
			List<CodeInstruction> list = codes;
			if (start > end)
			{
				int num = start;
				start = end;
				end = num;
			}
			if (start < 0 || end >= Length)
			{
				return HandleException("Cannot retrieve instructions: range is out-of-bounds.", new List<CodeInstruction>());
			}
			list = list.GetRange(start, end - start + 1);
			return list.Select((CodeInstruction c) => new CodeInstruction(c)).ToList();
		}

		public List<CodeInstruction> InstructionsWithOffsets(int startOffset, int endOffset)
		{
			return InstructionsInRange(Pos + startOffset, Pos + endOffset);
		}

		public List<Label> DistinctLabels(IEnumerable<CodeInstruction> instructions)
		{
			return instructions.SelectMany((CodeInstruction instruction) => instruction.labels).Distinct().ToList();
		}

		public bool ReportFailure(MethodBase method, Action<string> logger)
		{
			if (IsValid)
			{
				return false;
			}
			string value = lastError ?? "Unexpected code";
			logger($"{value} in {method}");
			return true;
		}

		public CodeMatcher ThrowIfInvalid(string explanation)
		{
			if (explanation == null)
			{
				throw new ArgumentNullException("explanation");
			}
			if (IsInvalid)
			{
				return HandleException(explanation + " - Current state is invalid", this);
			}
			return this;
		}

		public CodeMatcher ThrowIfNotMatch(string explanation, params CodeMatch[] matches)
		{
			ThrowIfInvalid(explanation);
			if (!MatchSequence(Pos, matches))
			{
				return HandleException(explanation + " - Match failed", this);
			}
			return this;
		}

		private void ThrowIfNotMatch(string explanation, int direction, CodeMatch[] matches)
		{
			ThrowIfInvalid(explanation);
			int pos = Pos;
			try
			{
				if (Match(matches, direction, MatchPosition.Start, prepareOnly: false).IsInvalid)
				{
					HandleException(explanation + " - Match failed");
				}
			}
			finally
			{
				Pos = pos;
			}
		}

		public CodeMatcher ThrowIfNotMatchForward(string explanation, params CodeMatch[] matches)
		{
			ThrowIfNotMatch(explanation, 1, matches);
			return this;
		}

		public CodeMatcher ThrowIfNotMatchBack(string explanation, params CodeMatch[] matches)
		{
			ThrowIfNotMatch(explanation, -1, matches);
			return this;
		}

		public CodeMatcher ThrowIfFalse(string explanation, Func<CodeMatcher, bool> stateCheckFunc)
		{
			if (stateCheckFunc == null)
			{
				throw new ArgumentNullException("stateCheckFunc");
			}
			ThrowIfInvalid(explanation);
			if (!stateCheckFunc(this))
			{
				return HandleException(explanation + " - Check function returned false", this);
			}
			return this;
		}

		public CodeMatcher Do(Action<CodeMatcher> action)
		{
			if (action == null)
			{
				throw new ArgumentNullException("action");
			}
			action(this);
			return this;
		}

		public CodeMatcher OnError(ErrorHandler errorHandler)
		{
			this.errorHandler = errorHandler;
			return this;
		}

		public CodeMatcher SetInstruction(CodeInstruction instruction)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot set instruction/opcode at invalid position.", this);
			}
			codes[Pos] = instruction;
			return this;
		}

		public CodeMatcher SetInstructionAndAdvance(CodeInstruction instruction)
		{
			SetInstruction(instruction);
			Pos++;
			return this;
		}

		public CodeMatcher Set(System.Reflection.Emit.OpCode opcode, object operand)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot set values at invalid position.", this);
			}
			Opcode = opcode;
			Operand = operand;
			return this;
		}

		public CodeMatcher SetAndAdvance(System.Reflection.Emit.OpCode opcode, object operand)
		{
			Set(opcode, operand);
			Pos++;
			return this;
		}

		public CodeMatcher SetOpcodeAndAdvance(System.Reflection.Emit.OpCode opcode)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot set opcode at invalid position.", this);
			}
			Opcode = opcode;
			Pos++;
			return this;
		}

		public CodeMatcher SetOperandAndAdvance(object operand)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot set operand at invalid position.", this);
			}
			Operand = operand;
			Pos++;
			return this;
		}

		public CodeMatcher DeclareLocal(Type variableType, out LocalBuilder localVariable)
		{
			if (generator == null)
			{
				localVariable = null;
				return HandleException("Generator must be provided to use this method", this);
			}
			localVariable = generator.DeclareLocal(variableType);
			return this;
		}

		public CodeMatcher DefineLabel(out Label label)
		{
			if (generator == null)
			{
				label = default(Label);
				return HandleException("Generator must be provided to use this method", this);
			}
			label = generator.DefineLabel();
			return this;
		}

		public CodeMatcher CreateLabel(out Label label)
		{
			if (generator == null)
			{
				label = default(Label);
				return HandleException("Generator must be provided to use this method", this);
			}
			label = generator.DefineLabel();
			Labels.Add(label);
			return this;
		}

		public CodeMatcher CreateLabelAt(int position, out Label label)
		{
			if (generator == null)
			{
				label = default(Label);
				return HandleException("Generator must be provided to use this method", this);
			}
			label = generator.DefineLabel();
			AddLabelsAt(position, new <>z__ReadOnlySingleElementList<Label>(label));
			return this;
		}

		public CodeMatcher CreateLabelWithOffsets(int offset, out Label label)
		{
			if (generator == null)
			{
				label = default(Label);
				return HandleException("Generator must be provided to use this method", this);
			}
			label = generator.DefineLabel();
			return AddLabelsAt(Pos + offset, new <>z__ReadOnlySingleElementList<Label>(label));
		}

		public CodeMatcher AddLabels(IEnumerable<Label> labels)
		{
			Labels.AddRange(labels);
			return this;
		}

		public CodeMatcher AddLabelsAt(int position, IEnumerable<Label> labels)
		{
			if (position < 0 || position >= Length)
			{
				return HandleException("Cannot add labels at invalid position.", this);
			}
			codes[position].labels.AddRange(labels);
			return this;
		}

		public CodeMatcher SetJumpTo(System.Reflection.Emit.OpCode opcode, int destination, out Label label)
		{
			CreateLabelAt(destination, out label);
			return Set(opcode, label);
		}

		public CodeMatcher Insert(params CodeInstruction[] instructions)
		{
			if (instructions == null || instructions.Any((CodeInstruction i) => i == null))
			{
				throw new ArgumentNullException("instructions");
			}
			if (IsInvalid)
			{
				return HandleException("Cannot insert instructions at invalid position.", this);
			}
			codes.InsertRange(Pos, instructions);
			return this;
		}

		public CodeMatcher Insert(IEnumerable<CodeInstruction> instructions)
		{
			if (instructions == null || instructions.Any((CodeInstruction i) => i == null))
			{
				throw new ArgumentNullException("instructions");
			}
			if (IsInvalid)
			{
				return HandleException("Cannot insert instructions at invalid position.", this);
			}
			codes.InsertRange(Pos, instructions);
			return this;
		}

		public CodeMatcher InsertBranch(System.Reflection.Emit.OpCode opcode, int destination)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot insert instructions at invalid position.", this);
			}
			CreateLabelAt(destination, out var label);
			codes.Insert(Pos, new CodeInstruction(opcode, label));
			return this;
		}

		public CodeMatcher InsertAndAdvance(params CodeInstruction[] instructions)
		{
			if (instructions == null || instructions.Any((CodeInstruction i) => i == null))
			{
				throw new ArgumentNullException("instructions");
			}
			foreach (CodeInstruction codeInstruction in instructions)
			{
				Insert(codeInstruction);
				Pos++;
			}
			return this;
		}

		public CodeMatcher InsertAndAdvance(IEnumerable<CodeInstruction> instructions)
		{
			if (instructions == null || instructions.Any((CodeInstruction i) => i == null))
			{
				throw new ArgumentNullException("instructions");
			}
			foreach (CodeInstruction instruction in instructions)
			{
				InsertAndAdvance(instruction);
			}
			return this;
		}

		public CodeMatcher InsertBranchAndAdvance(System.Reflection.Emit.OpCode opcode, int destination)
		{
			InsertBranch(opcode, destination);
			Pos++;
			return this;
		}

		public CodeMatcher InsertAfter(params CodeInstruction[] instructions)
		{
			if (instructions == null || instructions.Any((CodeInstruction i) => i == null))
			{
				throw new ArgumentNullException("instructions");
			}
			if (IsInvalid)
			{
				return HandleException("Cannot insert instructions at invalid position.", this);
			}
			codes.InsertRange(Pos + 1, instructions);
			return this;
		}

		public CodeMatcher InsertAfter(IEnumerable<CodeInstruction> instructions)
		{
			if (instructions == null || instructions.Any((CodeInstruction i) => i == null))
			{
				return HandleException("Cannot insert null instructions.", this);
			}
			if (IsInvalid)
			{
				return HandleException("Cannot insert instructions at invalid position.", this);
			}
			codes.InsertRange(Pos + 1, instructions);
			return this;
		}

		public CodeMatcher InsertBranchAfter(System.Reflection.Emit.OpCode opcode, int destination)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot insert instructions at invalid position.", this);
			}
			CreateLabelAt(destination, out var label);
			codes.Insert(Pos + 1, new CodeInstruction(opcode, label));
			return this;
		}

		public CodeMatcher InsertAfterAndAdvance(params CodeInstruction[] instructions)
		{
			InsertAfter(instructions);
			Pos += instructions.Length;
			return this;
		}

		public CodeMatcher InsertAfterAndAdvance(IEnumerable<CodeInstruction> instructions)
		{
			if (instructions == null || instructions.Any((CodeInstruction i) => i == null))
			{
				return HandleException("Cannot insert null instructions.", this);
			}
			List<CodeInstruction> list = instructions.ToList();
			InsertAfter(list);
			Pos += list.Count;
			return this;
		}

		public CodeMatcher InsertBranchAfterAndAdvance(System.Reflection.Emit.OpCode opcode, int destination)
		{
			InsertBranchAfter(opcode, destination);
			Pos++;
			return this;
		}

		public CodeMatcher RemoveInstruction()
		{
			if (IsInvalid)
			{
				return HandleException("Cannot remove instructions from an invalid position.", this);
			}
			codes.RemoveAt(Pos);
			return this;
		}

		public CodeMatcher RemoveInstructions(int count)
		{
			if (IsInvalid || Pos + count > Length)
			{
				return HandleException("Cannot remove instructions from an invalid or out-of-range position.", this);
			}
			codes.RemoveRange(Pos, count);
			return this;
		}

		public CodeMatcher RemoveInstructionsInRange(int start, int end)
		{
			if (start > end)
			{
				int num = start;
				start = end;
				end = num;
			}
			if (start < 0 || end >= Length)
			{
				return HandleException("Cannot remove instructions: range is out-of-bounds.", this);
			}
			codes.RemoveRange(start, end - start + 1);
			return this;
		}

		public CodeMatcher RemoveInstructionsWithOffsets(int startOffset, int endOffset)
		{
			return RemoveInstructionsInRange(Pos + startOffset, Pos + endOffset);
		}

		public CodeMatcher Advance(int offset = 1)
		{
			Pos += offset;
			if (!IsValid)
			{
				SetOutOfBounds(offset);
			}
			return this;
		}

		public CodeMatcher Start()
		{
			Pos = 0;
			return this;
		}

		public CodeMatcher End()
		{
			Pos = Length - 1;
			return this;
		}

		public CodeMatcher SearchForward(Func<CodeInstruction, bool> predicate)
		{
			return Search(predicate, 1);
		}

		public CodeMatcher SearchBackwards(Func<CodeInstruction, bool> predicate)
		{
			return Search(predicate, -1);
		}

		private CodeMatcher Search(Func<CodeInstruction, bool> predicate, int direction)
		{
			FixStart();
			while (IsValid && !predicate(Instruction))
			{
				Pos += direction;
			}
			lastError = (IsInvalid ? $"Cannot find {predicate}" : null);
			return this;
		}

		public CodeMatcher MatchStartForward(params CodeMatch[] matches)
		{
			return Match(matches, 1, MatchPosition.Start, prepareOnly: false);
		}

		public CodeMatcher PrepareMatchStartForward(params CodeMatch[] matches)
		{
			return Match(matches, 1, MatchPosition.Start, prepareOnly: true);
		}

		public CodeMatcher MatchEndForward(params CodeMatch[] matches)
		{
			return Match(matches, 1, MatchPosition.End, prepareOnly: false);
		}

		public CodeMatcher PrepareMatchEndForward(params CodeMatch[] matches)
		{
			return Match(matches, 1, MatchPosition.End, prepareOnly: true);
		}

		public CodeMatcher MatchStartBackwards(params CodeMatch[] matches)
		{
			return Match(matches, -1, MatchPosition.Start, prepareOnly: false);
		}

		public CodeMatcher PrepareMatchStartBackwards(params CodeMatch[] matches)
		{
			return Match(matches, -1, MatchPosition.Start, prepareOnly: true);
		}

		public CodeMatcher MatchEndBackwards(params CodeMatch[] matches)
		{
			return Match(matches, -1, MatchPosition.End, prepareOnly: false);
		}

		public CodeMatcher PrepareMatchEndBackwards(params CodeMatch[] matches)
		{
			return Match(matches, -1, MatchPosition.End, prepareOnly: true);
		}

		public CodeMatcher RemoveSearchForward(Func<CodeInstruction, bool> predicate)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot remove instructions from an invalid position.", this);
			}
			int pos = Pos;
			CodeMatcher codeMatcher = Clone().SearchForward(predicate);
			if (codeMatcher.IsInvalid)
			{
				lastError = codeMatcher.lastError;
				SetOutOfBounds(1);
				return this;
			}
			int num = codeMatcher.Pos - 1;
			if (num >= pos)
			{
				RemoveInstructionsInRange(pos, num);
			}
			return this;
		}

		public CodeMatcher RemoveSearchBackward(Func<CodeInstruction, bool> predicate)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot remove instructions from an invalid position.", this);
			}
			int pos = Pos;
			CodeMatcher codeMatcher = Clone().SearchBackwards(predicate);
			if (codeMatcher.IsInvalid)
			{
				lastError = codeMatcher.lastError;
				SetOutOfBounds(-1);
				return this;
			}
			int pos2 = codeMatcher.Pos;
			int num = pos2 + 1;
			if (pos >= num)
			{
				RemoveInstructionsInRange(num, pos);
			}
			Pos = pos2;
			return this;
		}

		public CodeMatcher RemoveUntilForward(params CodeMatch[] matches)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot remove instructions from an invalid position.", this);
			}
			int pos = Pos;
			CodeMatcher codeMatcher = Clone().MatchStartForward(matches);
			if (codeMatcher.IsInvalid)
			{
				lastError = codeMatcher.lastError;
				SetOutOfBounds(1);
				return this;
			}
			int num = codeMatcher.Pos - 1;
			if (num >= pos)
			{
				RemoveInstructionsInRange(pos, num);
			}
			return this;
		}

		public CodeMatcher RemoveUntilBackward(params CodeMatch[] matches)
		{
			if (IsInvalid)
			{
				return HandleException("Cannot remove instructions from an invalid position.", this);
			}
			int pos = Pos;
			CodeMatcher codeMatcher = Clone().MatchEndBackwards(matches);
			if (codeMatcher.IsInvalid)
			{
				lastError = codeMatcher.lastError;
				SetOutOfBounds(-1);
				return this;
			}
			int pos2 = codeMatcher.Pos;
			if (pos > pos2)
			{
				RemoveInstructionsInRange(pos2 + 1, pos);
			}
			Pos = pos2;
			return this;
		}

		private CodeMatcher Match(CodeMatch[] matches, int direction, MatchPosition mode, bool prepareOnly)
		{
			lastMatchCall = delegate
			{
				while (IsValid)
				{
					if (MatchSequence(Pos, matches))
					{
						if (mode == MatchPosition.End)
						{
							Pos += matches.Length - 1;
						}
						break;
					}
					Pos += direction;
				}
				lastError = (IsInvalid ? ("Cannot find " + matches.Join()) : null);
				return this;
			};
			if (prepareOnly)
			{
				return this;
			}
			FixStart();
			return lastMatchCall();
		}

		public CodeMatcher Repeat(Action<CodeMatcher> matchAction, Action<string> notFoundAction = null)
		{
			int num = 0;
			if (lastMatchCall == null)
			{
				return HandleException("No previous Match operation - cannot repeat", this);
			}
			while (IsValid)
			{
				matchAction(this);
				lastMatchCall();
				num++;
			}
			lastMatchCall = null;
			if (num == 0)
			{
				notFoundAction?.Invoke(lastError);
			}
			return this;
		}

		public CodeInstruction NamedMatch(string name)
		{
			return lastMatches[name];
		}

		private bool MatchSequence(int start, CodeMatch[] matches)
		{
			if (start < 0)
			{
				return false;
			}
			lastMatches = new Dictionary<string, CodeInstruction>();
			foreach (CodeMatch codeMatch in matches)
			{
				if (start >= Length || !codeMatch.Matches(codes, codes[start]))
				{
					return false;
				}
				if (codeMatch.name != null)
				{
					lastMatches.Add(codeMatch.name, codes[start]);
				}
				start++;
			}
			return true;
		}
	}
	public static class GeneralExtensions
	{
		public static string Join<T>(this IEnumerable<T> enumeration, Func<T, string> converter = null, string delimiter = ", ")
		{
			if (converter == null)
			{
				converter = (T t) => t.ToString();
			}
			return enumeration.Aggregate("", (string prev, T curr) => prev + ((prev.Length > 0) ? delimiter : "") + converter(curr));
		}

		public static string Description(this Type[] parameters)
		{
			if (parameters == null)
			{
				return "NULL";
			}
			return "(" + parameters.Join((Type p) => p.FullDescription()) + ")";
		}

		public static string FullDescription(this Type type)
		{
			if ((object)type == null)
			{
				return "null";
			}
			string text = type.Namespace;
			if (!string.IsNullOrEmpty(text))
			{
				text += ".";
			}
			string text2 = text + type.Name;
			if (type.IsGenericType)
			{
				text2 += "<";
				Type[] genericArguments = type.GetGenericArguments();
				for (int i = 0; i < genericArguments.Length; i++)
				{
					if (!text2.EndsWith("<", StringComparison.Ordinal))
					{
						text2 += ", ";
					}
					text2 += genericArguments[i].FullDescription();
				}
				text2 += ">";
			}
			return text2;
		}

		public static string FullDescription(this MethodBase member)
		{
			if ((object)member == null)
			{
				return "null";
			}
			Type returnedType = AccessTools.GetReturnedType(member);
			StringBuilder stringBuilder = new StringBuilder();
			if (member.IsStatic)
			{
				stringBuilder.Append("static ");
			}
			if (member.IsAbstract)
			{
				stringBuilder.Append("abstract ");
			}
			if (member.IsVirtual)
			{
				stringBuilder.Append("virtual ");
			}
			stringBuilder.Append(returnedType.FullDescription() + " ");
			if ((object)member.DeclaringType != null)
			{
				stringBuilder.Append(member.DeclaringType.FullDescription() + "::");
			}
			string text = member.GetParameters().Join((ParameterInfo p) => p.ParameterType.FullDescription() + " " + p.Name);
			stringBuilder.Append(member.Name + "(" + text + ")");
			return stringBuilder.ToString();
		}

		public static Type[] Types(this ParameterInfo[] pinfo)
		{
			return pinfo.Select((ParameterInfo pi) => pi.ParameterType).ToArray();
		}

		public static bool HasHarmonyAttribute(this Type type)
		{
			if ((object)type == null)
			{
				throw new ArgumentNullException("type");
			}
			return HarmonyMethodExtensions.GetFromType(type).Count > 0;
		}

		public static T GetValueSafe<S, T>(this Dictionary<S, T> dictionary, S key)
		{
			if (dictionary.TryGetValue(key, out var value))
			{
				return value;
			}
			return default(T);
		}

		public static T GetTypedValue<T>(this Dictionary<string, object> dictionary, string key)
		{
			if (dictionary.TryGetValue(key, out var value) && value is T)
			{
				return (T)value;
			}
			return default(T);
		}

		public static string ToLiteral(this string input, string quoteChar = "\"")
		{
			StringBuilder stringBuilder = new StringBuilder(input.Length + 2);
			stringBuilder.Append(quoteChar);
			foreach (char c in input)
			{
				switch (c)
				{
				case '\'':
					stringBuilder.Append("\\'");
					continue;
				case '"':
					stringBuilder.Append("\\\"");
					continue;
				case '\\':
					stringBuilder.Append("\\\\");
					continue;
				case '\0':
					stringBuilder.Append("\\0");
					continue;
				case '\a':
					stringBuilder.Append("\\a");
					continue;
				case '\b':
					stringBuilder.Append("\\b");
					continue;
				case '\f':
					stringBuilder.Append("\\f");
					continue;
				case '\n':
					stringBuilder.Append("\\n");
					continue;
				case '\r':
					stringBuilder.Append("\\r");
					continue;
				case '\t':
					stringBuilder.Append("\\t");
					continue;
				case '\v':
					stringBuilder.Append("\\v");
					continue;
				}
				if (c >= ' ' && c <= '~')
				{
					stringBuilder.Append(c);
					continue;
				}
				stringBuilder.Append("\\u");
				int num = c;
				stringBuilder.Append(num.ToString("x4"));
			}
			stringBuilder.Append(quoteChar);
			return stringBuilder.ToString();
		}
	}
	public static class CodeInstructionExtensions
	{
		internal static readonly HashSet<System.Reflection.Emit.OpCode> opcodesCalling = new HashSet<System.Reflection.Emit.OpCode>
		{
			System.Reflection.Emit.OpCodes.Call,
			System.Reflection.Emit.OpCodes.Callvirt
		};

		internal static readonly HashSet<System.Reflection.Emit.OpCode> opcodesLoadingLocalByAddress = new HashSet<System.Reflection.Emit.OpCode>
		{
			System.Reflection.Emit.OpCodes.Ldloca_S,
			System.Reflection.Emit.OpCodes.Ldloca
		};

		internal static readonly HashSet<System.Reflection.Emit.OpCode> opcodesLoadingLocalNormal = new HashSet<System.Reflection.Emit.OpCode>
		{
			System.Reflection.Emit.OpCodes.Ldloc_0,
			System.Reflection.Emit.OpCodes.Ldloc_1,
			System.Reflection.Emit.OpCodes.Ldloc_2,
			System.Reflection.Emit.OpCodes.Ldloc_3,
			System.Reflection.Emit.OpCodes.Ldloc_S,
			System.Reflection.Emit.OpCodes.Ldloc
		};

		internal static readonly HashSet<System.Reflection.Emit.OpCode> opcodesStoringLocal = new HashSet<System.Reflection.Emit.OpCode>
		{
			System.Reflection.Emit.OpCodes.Stloc_0,
			System.Reflection.Emit.OpCodes.Stloc_1,
			System.Reflection.Emit.OpCodes.Stloc_2,
			System.Reflection.Emit.OpCodes.Stloc_3,
			System.Reflection.Emit.OpCodes.Stloc_S,
			System.Reflection.Emit.OpCodes.Stloc
		};

		internal static readonly HashSet<System.Reflection.Emit.OpCode> opcodesLoadingArgumentByAddress = new HashSet<System.Reflection.Emit.OpCode>
		{
			System.Reflection.Emit.OpCodes.Ldarga_S,
			System.Reflection.Emit.OpCodes.Ldarga
		};

		internal static readonly HashSet<System.Reflection.Emit.OpCode> opcodesLoadingArgumentNormal = new HashSet<System.Reflection.Emit.OpCode>
		{
			System.Reflection.Emit.OpCodes.Ldarg_0,
			System.Reflection.Emit.OpCodes.Ldarg_1,
			System.Reflection.Emit.OpCodes.Ldarg_2,
			System.Reflection.Emit.OpCodes.Ldarg_3,
			System.Reflection.Emit.OpCodes.Ldarg_S,
			System.Reflection.Emit.OpCodes.Ldarg
		};

		internal static readonly HashSet<System.Reflection.Emit.OpCode> opcodesStoringArgument = new HashSet<System.Reflection.Emit.OpCode>
		{
			System.Reflection.Emit.OpCodes.Starg_S,
			System.Reflection.Emit.OpCodes.Starg
		};

		internal static readonly HashSet<System.Reflection.Emit.OpCode> opcodesBranching = new HashSet<System.Reflection.Emit.OpCode>
		{
			System.Reflection.Emit.OpCodes.Br_S,
			System.Reflection.Emit.OpCodes.Brfalse_S,
			System.Reflection.Emit.OpCodes.Brtrue_S,
			System.Reflection.Emit.OpCodes.Beq_S,
			System.Reflection.Emit.OpCodes.Bge_S,
			System.Reflection.Emit.OpCodes.Bgt_S,
			System.Reflection.Emit.OpCodes.Ble_S,
			System.Reflection.Emit.OpCodes.Blt_S,
			System.Reflection.Emit.OpCodes.Bne_Un_S,
			System.Reflection.Emit.OpCodes.Bge_Un_S,
			System.Reflection.Emit.OpCodes.Bgt_Un_S,
			System.Reflection.Emit.OpCodes.Ble_Un_S,
			System.Reflection.Emit.OpCodes.Blt_Un_S,
			System.Reflection.Emit.OpCodes.Br,
			System.Reflection.Emit.OpCodes.Brfalse,
			System.Reflection.Emit.OpCodes.Brtrue,
			System.Reflection.Emit.OpCodes.Beq,
			System.Reflection.Emit.OpCodes.Bge,
			System.Reflection.Emit.OpCodes.Bgt,
			System.Reflection.Emit.OpCodes.Ble,
			System.Reflection.Emit.OpCodes.Blt,
			System.Reflection.Emit.OpCodes.Bne_Un,
			System.Reflection.Emit.OpCodes.Bge_Un,
			System.Reflection.Emit.OpCodes.Bgt_Un,
			System.Reflection.Emit.OpCodes.Ble_Un,
			System.Reflection.Emit.OpCodes.Blt_Un
		};

		private static readonly HashSet<System.Reflection.Emit.OpCode> constantLoadingCodes = new HashSet<System.Reflection.Emit.OpCode>
		{
			System.Reflection.Emit.OpCodes.Ldc_I4_M1,
			System.Reflection.Emit.OpCodes.Ldc_I4_0,
			System.Reflection.Emit.OpCodes.Ldc_I4_1,
			System.Reflection.Emit.OpCodes.Ldc_I4_2,
			System.Reflection.Emit.OpCodes.Ldc_I4_3,
			System.Reflection.Emit.OpCodes.Ldc_I4_4,
			System.Reflection.Emit.OpCodes.Ldc_I4_5,
			System.Reflection.Emit.OpCodes.Ldc_I4_6,
			System.Reflection.Emit.OpCodes.Ldc_I4_7,
			System.Reflection.Emit.OpCodes.Ldc_I4_8,
			System.Reflection.Emit.OpCodes.Ldc_I4,
			System.Reflection.Emit.OpCodes.Ldc_I4_S,
			System.Reflection.Emit.OpCodes.Ldc_I8,
			System.Reflection.Emit.OpCodes.Ldc_R4,
			System.Reflection.Emit.OpCodes.Ldc_R8,
			System.Reflection.Emit.OpCodes.Ldstr
		};

		internal static int GetSize(this CodeInstruction instruction)
		{
			int num = instruction.opcode.Size;
			switch (instruction.opcode.OperandType)
			{
			case System.Reflection.Emit.OperandType.InlineSwitch:
				num += (1 + ((Array)instruction.operand).Length) * 4;
				break;
			case System.Reflection.Emit.OperandType.InlineI8:
			case System.Reflection.Emit.OperandType.InlineR:
				num += 8;
				break;
			case System.Reflection.Emit.OperandType.InlineBrTarget:
			case System.Reflection.Emit.OperandType.InlineField:
			case System.Reflection.Emit.OperandType.InlineI:
			case System.Reflection.Emit.OperandType.InlineMethod:
			case System.Reflection.Emit.OperandType.InlineSig:
			case System.Reflection.Emit.OperandType.InlineString:
			case System.Reflection.Emit.OperandType.InlineTok:
			case System.Reflection.Emit.OperandType.InlineType:
			case System.Reflection.Emit.OperandType.ShortInlineR:
				num += 4;
				break;
			case System.Reflection.Emit.OperandType.InlineVar:
				num += 2;
				break;
			case System.Reflection.Emit.OperandType.ShortInlineBrTarget:
			case System.Reflection.Emit.OperandType.ShortInlineI:
			case System.Reflection.Emit.OperandType.ShortInlineVar:
				num++;
				break;
			}
			return num;
		}

		public static bool IsValid(this System.Reflection.Emit.OpCode code)
		{
			return code.Size > 0;
		}

		public static bool OperandIs(this CodeInstruction code, object value)
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			if (code.operand == null)
			{
				return false;
			}
			Type type = value.GetType();
			Type type2 = code.operand.GetType();
			if (AccessTools.IsInteger(type) && AccessTools.IsNumber(type2))
			{
				return Convert.ToInt64(code.operand) == Convert.ToInt64(value);
			}
			if (AccessTools.IsFloatingPoint(type) && AccessTools.IsNumber(type2))
			{
				return Convert.ToDouble(code.operand) == Convert.ToDouble(value);
			}
			return object.Equals(code.operand, value);
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool OperandIs(this CodeInstruction code, MemberInfo value)
		{
			if ((object)value == null)
			{
				throw new ArgumentNullException("value");
			}
			return object.Equals(code.operand, value);
		}

		public static bool Is(this CodeInstruction code, System.Reflection.Emit.OpCode opcode, object operand)
		{
			if (code.opcode == opcode)
			{
				return code.OperandIs(operand);
			}
			return false;
		}

		[EditorBrowsable(EditorBrowsableState.Never)]
		public static bool Is(this CodeInstruction code, System.Reflection.Emit.OpCode opcode, MemberInfo operand)
		{
			if (code.opcode == opcode)
			{
				return code.OperandIs(operand);
			}
			return false;
		}

		public static bool IsLdarg(this CodeInstruction code, int? n = null)
		{
			if ((!n.HasValue || n.Value == 0) && code.opcode == System.Reflection.Emit.OpCodes.Ldarg_0)
			{
				return true;
			}
			if ((!n.HasValue || n.Value == 1) && code.opcode == System.Reflection.Emit.OpCodes.Ldarg_1)
			{
				return true;
			}
			if ((!n.HasValue || n.Value == 2) && code.opcode == System.Reflection.Emit.OpCodes.Ldarg_2)
			{
				return true;
			}
			if ((!n.HasValue || n.Value == 3) && code.opcode == System.Reflection.Emit.OpCodes.Ldarg_3)
			{
				return true;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldarg && (!n.HasValue || n.Value == Convert.ToInt32(code.operand)))
			{
				return true;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldarg_S && (!n.HasValue || n.Value == Convert.ToInt32(code.operand)))
			{
				return true;
			}
			return false;
		}

		public static bool IsLdarga(this CodeInstruction code, int? n = null)
		{
			if (code.opcode != System.Reflection.Emit.OpCodes.Ldarga && code.opcode != System.Reflection.Emit.OpCodes.Ldarga_S)
			{
				return false;
			}
			if (n.HasValue)
			{
				return n.Value == Convert.ToInt32(code.operand);
			}
			return true;
		}

		public static bool IsStarg(this CodeInstruction code, int? n = null)
		{
			if (code.opcode != System.Reflection.Emit.OpCodes.Starg && code.opcode != System.Reflection.Emit.OpCodes.Starg_S)
			{
				return false;
			}
			if (n.HasValue)
			{
				return n.Value == Convert.ToInt32(code.operand);
			}
			return true;
		}

		public static bool IsLdloc(this CodeInstruction code, LocalBuilder variable = null)
		{
			if (!opcodesLoadingLocalNormal.Contains(code.opcode) && !opcodesLoadingLocalByAddress.Contains(code.opcode))
			{
				return false;
			}
			if (variable != null)
			{
				return object.Equals(variable, code.operand);
			}
			return true;
		}

		public static bool IsStloc(this CodeInstruction code, LocalBuilder variable = null)
		{
			if (!opcodesStoringLocal.Contains(code.opcode))
			{
				return false;
			}
			if (variable != null)
			{
				return object.Equals(variable, code.operand);
			}
			return true;
		}

		public static bool Branches(this CodeInstruction code, out Label? label)
		{
			if (opcodesBranching.Contains(code.opcode))
			{
				label = (Label)code.operand;
				return true;
			}
			label = null;
			return false;
		}

		public static bool Calls(this CodeInstruction code, MethodInfo method)
		{
			if ((object)method == null)
			{
				throw new ArgumentNullException("method");
			}
			if (code.opcode != System.Reflection.Emit.OpCodes.Call && code.opcode != System.Reflection.Emit.OpCodes.Callvirt)
			{
				return false;
			}
			return object.Equals(code.operand, method);
		}

		public static bool LoadsConstant(this CodeInstruction code)
		{
			return constantLoadingCodes.Contains(code.opcode);
		}

		public static bool LoadsConstant(this CodeInstruction code, long number)
		{
			System.Reflection.Emit.OpCode opcode = code.opcode;
			if (number == -1 && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_M1)
			{
				return true;
			}
			if (number == 0L && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_0)
			{
				return true;
			}
			if (number == 1 && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_1)
			{
				return true;
			}
			if (number == 2 && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_2)
			{
				return true;
			}
			if (number == 3 && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_3)
			{
				return true;
			}
			if (number == 4 && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_4)
			{
				return true;
			}
			if (number == 5 && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_5)
			{
				return true;
			}
			if (number == 6 && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_6)
			{
				return true;
			}
			if (number == 7 && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_7)
			{
				return true;
			}
			if (number == 8 && opcode == System.Reflection.Emit.OpCodes.Ldc_I4_8)
			{
				return true;
			}
			if (opcode != System.Reflection.Emit.OpCodes.Ldc_I4 && opcode != System.Reflection.Emit.OpCodes.Ldc_I4_S && opcode != System.Reflection.Emit.OpCodes.Ldc_I8)
			{
				return false;
			}
			return Convert.ToInt64(code.operand) == number;
		}

		public static bool LoadsConstant(this CodeInstruction code, double number)
		{
			if (code.opcode != System.Reflection.Emit.OpCodes.Ldc_R4 && code.opcode != System.Reflection.Emit.OpCodes.Ldc_R8)
			{
				return false;
			}
			double num = Convert.ToDouble(code.operand);
			return num == number;
		}

		public static bool LoadsConstant(this CodeInstruction code, Enum e)
		{
			return code.LoadsConstant(Convert.ToInt64(e));
		}

		public static bool LoadsConstant(this CodeInstruction code, string str)
		{
			if (code.opcode != System.Reflection.Emit.OpCodes.Ldstr)
			{
				return false;
			}
			string text = Convert.ToString(code.operand);
			return text == str;
		}

		public static bool LoadsField(this CodeInstruction code, FieldInfo field, bool byAddress = false)
		{
			if ((object)field == null)
			{
				throw new ArgumentNullException("field");
			}
			System.Reflection.Emit.OpCode opCode = (field.IsStatic ? System.Reflection.Emit.OpCodes.Ldsfld : System.Reflection.Emit.OpCodes.Ldfld);
			if (!byAddress && code.opcode == opCode && object.Equals(code.operand, field))
			{
				return true;
			}
			System.Reflection.Emit.OpCode opCode2 = (field.IsStatic ? System.Reflection.Emit.OpCodes.Ldsflda : System.Reflection.Emit.OpCodes.Ldflda);
			if (byAddress && code.opcode == opCode2 && object.Equals(code.operand, field))
			{
				return true;
			}
			return false;
		}

		public static bool StoresField(this CodeInstruction code, FieldInfo field)
		{
			if ((object)field == null)
			{
				throw new ArgumentNullException("field");
			}
			System.Reflection.Emit.OpCode opCode = (field.IsStatic ? System.Reflection.Emit.OpCodes.Stsfld : System.Reflection.Emit.OpCodes.Stfld);
			if (code.opcode == opCode)
			{
				return object.Equals(code.operand, field);
			}
			return false;
		}

		public static int LocalIndex(this CodeInstruction code)
		{
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldloc_0 || code.opcode == System.Reflection.Emit.OpCodes.Stloc_0)
			{
				return 0;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldloc_1 || code.opcode == System.Reflection.Emit.OpCodes.Stloc_1)
			{
				return 1;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldloc_2 || code.opcode == System.Reflection.Emit.OpCodes.Stloc_2)
			{
				return 2;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldloc_3 || code.opcode == System.Reflection.Emit.OpCodes.Stloc_3)
			{
				return 3;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldloc_S || code.opcode == System.Reflection.Emit.OpCodes.Ldloc)
			{
				if (code.operand is LocalBuilder localBuilder)
				{
					return localBuilder.LocalIndex;
				}
				return Convert.ToInt32(code.operand);
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Stloc_S || code.opcode == System.Reflection.Emit.OpCodes.Stloc)
			{
				if (code.operand is LocalBuilder localBuilder2)
				{
					return localBuilder2.LocalIndex;
				}
				return Convert.ToInt32(code.operand);
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldloca_S || code.opcode == System.Reflection.Emit.OpCodes.Ldloca)
			{
				if (code.operand is LocalBuilder localBuilder3)
				{
					return localBuilder3.LocalIndex;
				}
				return Convert.ToInt32(code.operand);
			}
			throw new ArgumentException("Instruction is not a load or store", "code");
		}

		public static int ArgumentIndex(this CodeInstruction code)
		{
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldarg_0)
			{
				return 0;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldarg_1)
			{
				return 1;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldarg_2)
			{
				return 2;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldarg_3)
			{
				return 3;
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldarg_S || code.opcode == System.Reflection.Emit.OpCodes.Ldarg)
			{
				return Convert.ToInt32(code.operand);
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Starg_S || code.opcode == System.Reflection.Emit.OpCodes.Starg)
			{
				return Convert.ToInt32(code.operand);
			}
			if (code.opcode == System.Reflection.Emit.OpCodes.Ldarga_S || code.opcode == System.Reflection.Emit.OpCodes.Ldarga)
			{
				return Convert.ToInt32(code.operand);
			}
			throw new ArgumentException("Instruction is not a load or store", "code");
		}

		public static CodeInstruction WithLabels(this CodeInstruction code, params Label[] labels)
		{
			code.labels.AddRange(labels);
			return code;
		}

		public static CodeInstruction WithLabels(this CodeInstruction code, IEnumerable<Label> labels)
		{
			code.labels.AddRange(labels);
			return code;
		}

		public static List<Label> ExtractLabels(this CodeInstruction code)
		{
			List<Label> result = new List<Label>(code.labels);
			code.labels.Clear();
			return result;
		}

		public static CodeInstruction MoveLabelsTo(this CodeInstruction code, CodeInstruction other)
		{
			other.WithLabels(code.ExtractLabels());
			return code;
		}

		public static CodeInstruction MoveLabelsFrom(this CodeInstruction code, CodeInstruction other)
		{
			return code.WithLabels(other.ExtractLabels());
		}

		public static CodeInstruction WithBlocks(this CodeInstruction code, params ExceptionBlock[] blocks)
		{
			code.blocks.AddRange(blocks);
			return code;
		}

		public static CodeInstruction WithBlocks(this CodeInstruction code, IEnumerable<ExceptionBlock> blocks)
		{
			code.blocks.AddRange(blocks);
			return code;
		}

		public static List<ExceptionBlock> ExtractBlocks(this CodeInstruction code)
		{
			List<ExceptionBlock> result = new List<ExceptionBlock>(code.blocks);
			code.blocks.Clear();
			return result;
		}

		public static CodeInstruction MoveBlocksTo(this CodeInstruction code, CodeInstruction other)
		{
			other.WithBlocks(code.ExtractBlocks());
			return code;
		}

		public static CodeInstruction MoveBlocksFrom(this CodeInstruction code, CodeInstruction other)
		{
			return code.WithBlocks(other.ExtractBlocks());
		}
	}
	public static class CodeInstructionsExtensions
	{
		public static bool Matches(this IEnumerable<CodeInstruction> instructions, CodeMatch[] matches)
		{
			return new CodeMatcher(instructions).MatchStartForward(matches).IsValid;
		}
	}
	public static class CollectionExtensions
	{
		public static void Do<T>(this IEnumerable<T> sequence, Action<T> action)
		{
			if (sequence != null)
			{
				IEnumerator<T> enumerator = sequence.GetEnumerator();
				while (enumerator.MoveNext())
				{
					action(enumerator.Current);
				}
			}
		}

		public static void DoIf<T>(this IEnumerable<T> sequence, Func<T, bool> condition, Action<T> action)
		{
			sequence.Where(condition).Do(action);
		}

		public static IEnumerable<T> AddItem<T>(this IEnumerable<T> sequence, T item)
		{
			return (sequence ?? Array.Empty<T>()).Concat(new T[1] { item });
		}

		public static T[] AddToArray<T>(this T[] sequence, T item)
		{
			return sequence.AddItem(item).ToArray();
		}

		public static T[] AddRangeToArray<T>(this T[] sequence, T[] items)
		{
			List<T> list = new List<T>();
			list.AddRange(sequence ?? Enumerable.Empty<T>());
			list.AddRange(items);
			return list.ToArray();
		}

		internal static Dictionary<K, V> Merge<K, V>(this IEnumerable<KeyValuePair<K, V>> firstDict, params IEnumerable<KeyValuePair<K, V>>[] otherDicts)
		{
			Dictionary<K, V> dictionary = new Dictionary<K, V>();
			foreach (KeyValuePair<K, V> item in firstDict)
			{
				dictionary[item.Key] = item.Value;
			}
			foreach (IEnumerable<KeyValuePair<K, V>> enumerable in otherDicts)
			{
				foreach (KeyValuePair<K, V> item2 in enumerable)
				{
					dictionary[item2.Key] = item2.Value;
				}
			}
			return dictionary;
		}

		internal static Dictionary<K, V> TransformKeys<K, V>(this Dictionary<K, V> origDict, Func<K, K> transform)
		{
			Dictionary<K, V> dictionary = new Dictionary<K, V>();
			foreach (KeyValuePair<K, V> item in origDict)
			{
				dictionary.Add(transform(item.Key), item.Value);
			}
			return dictionary;
		}
	}
	public static class MethodBaseExtensions
	{
		public static bool HasMethodBody(this MethodBase member)
		{
			return (member.GetMethodBody()?.GetILAsByteArray()?.Length).GetValueOrDefault() > 0;
		}
	}
	public static class FileLog
	{
		private static readonly object fileLock = new object();

		private static bool _logPathInited;

		private static string _logPath;

		public static char indentChar = '\t';

		public static int indentLevel = 0;

		private static List<string> buffer = new List<string>();

		public static StreamWriter LogWriter { get; set; }

		public static string LogPath
		{
			get
			{
				lock (fileLock)
				{
					if (!_logPathInited)
					{
						_logPathInited = true;
						string environmentVariable = Environment.GetEnvironmentVariable("HARMONY_NO_LOG");
						if (!string.IsNullOrEmpty(environmentVariable))
						{
							return null;
						}
						_logPath = Environment.GetEnvironmentVariable("HARMONY_LOG_FILE");
						if (string.IsNullOrEmpty(_logPath))
						{
							string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
							Directory.CreateDirectory(folderPath);
							_logPath = Path.Combine(folderPath, "harmony.log.txt");
						}
					}
					return _logPath;
				}
			}
		}

		private static string IndentString()
		{
			return new string(indentChar, indentLevel);
		}

		private static string CodePos(int offset)
		{
			return $"IL_{offset:X4}: ";
		}

		public static void ChangeIndent(int delta)
		{
			lock (fileLock)
			{
				indentLevel = Math.Max(0, indentLevel + delta);
			}
		}

		public static void LogBuffered(string str)
		{
			lock (fileLock)
			{
				buffer.Add(IndentString() + str);
			}
		}

		public static void LogBuffered(List<string> strings)
		{
			lock (fileLock)
			{
				buffer.AddRange(strings);
			}
		}

		public static List<string> GetBuffer(bool clear)
		{
			lock (fileLock)
			{
				List<string> result = buffer;
				if (clear)
				{
					buffer = new List<string>();
				}
				return result;
			}
		}

		public static void SetBuffer(List<string> buffer)
		{
			lock (fileLock)
			{
				FileLog.buffer = buffer;
			}
		}

		public static void FlushBuffer()
		{
			lock (fileLock)
			{
				if (LogWriter != null)
				{
					foreach (string item in buffer)
					{
						LogWriter.WriteLine(item);
					}
					buffer.Clear();
				}
				else
				{
					if (LogPath == null || buffer.Count <= 0)
					{
						return;
					}
					using FileStream stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
					using StreamWriter streamWriter = new StreamWriter(stream);
					foreach (string item2 in buffer)
					{
						streamWriter.WriteLine(item2);
					}
					buffer.Clear();
					return;
				}
			}
		}

		public static void Log(string str)
		{
			lock (fileLock)
			{
				if (LogWriter != null)
				{
					LogWriter.WriteLine(IndentString() + str);
				}
				else
				{
					if (LogPath == null)
					{
						return;
					}
					using FileStream stream = new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
					using StreamWriter streamWriter = new StreamWriter(stream);
					streamWriter.WriteLine(IndentString() + str);
					return;
				}
			}
		}

		public static void LogILComment(int codePos, string comment)
		{
			LogBuffered($"{CodePos(codePos)}// {comment}");
		}

		public static void LogIL(int codePos, System.Reflection.Emit.OpCode opcode)
		{
			LogBuffered($"{CodePos(codePos)}{opcode}");
		}

		public static void LogIL(int codePos, System.Reflection.Emit.OpCode opcode, object arg)
		{
			string text = Emitter.FormatOperand(arg);
			string text2 = ((text.Length > 0) ? " " : "");
			string text3 = opcode.ToString();
			if (opcode.FlowControl == System.Reflection.Emit.FlowControl.Branch || opcode.FlowControl == System.Reflection.Emit.FlowControl.Cond_Branch)
			{
				text3 += " =>";
			}
			text3 = text3.PadRight(10);
			LogBuffered($"{CodePos(codePos)}{text3}{text2}{text}");
		}

		internal static void LogIL(VariableDefinition variable)
		{
			LogBuffered(string.Format("{0}Local var {1}: {2}{3}", CodePos(0), variable.Index, variable.VariableType.FullName, variable.IsPinned ? "(pinned)" : ""));
		}

		public static void LogIL(int codePos, Label label)
		{
			LogBuffered(CodePos(codePos) + Emitter.FormatOperand(label));
		}

		public static void LogILBlockBegin(int codePos, ExceptionBlock block)
		{
			switch (block.blockType)
			{
			case ExceptionBlockType.BeginExceptionBlock:
				LogBuffered(".try");
				LogBuffered("{");
				ChangeIndent(1);
				break;
			case ExceptionBlockType.BeginCatchBlock:
				LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
				ChangeIndent(-1);
				LogBuffered("} // end try");
				LogBuffered($".catch {block.catchType}");
				LogBuffered("{");
				ChangeIndent(1);
				break;
			case ExceptionBlockType.BeginExceptFilterBlock:
				LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
				ChangeIndent(-1);
				LogBuffered("} // end try");
				LogBuffered(".filter");
				LogBuffered("{");
				ChangeIndent(1);
				break;
			case ExceptionBlockType.BeginFaultBlock:
				LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
				ChangeIndent(-1);
				LogBuffered("} // end try");
				LogBuffered(".fault");
				LogBuffered("{");
				ChangeIndent(1);
				break;
			case ExceptionBlockType.BeginFinallyBlock:
				LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
				ChangeIndent(-1);
				LogBuffered("} // end try");
				LogBuffered(".finally");
				LogBuffered("{");
				ChangeIndent(1);
				break;
			}
		}

		public static void LogILBlockEnd(int codePos, ExceptionBlock block)
		{
			ExceptionBlockType blockType = block.blockType;
			if (blockType == ExceptionBlockType.EndExceptionBlock)
			{
				LogIL(codePos, System.Reflection.Emit.OpCodes.Leave, new LeaveTry());
				ChangeIndent(-1);
				LogBuffered("} // end handler");
			}
		}

		public static void Debug(string str)
		{
			if (Harmony.DEBUG)
			{
				Log(str);
			}
		}

		public static void Reset()
		{
			lock (fileLock)
			{
				string path = $"{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}{Path.DirectorySeparatorChar}harmony.log.txt";
				File.Delete(path);
			}
		}

		public unsafe static void LogBytes(long ptr, int len)
		{
			lock (fileLock)
			{
				byte* ptr2 = (byte*)ptr;
				string text = "";
				for (int i = 1; i <= len; i++)
				{
					if (text.Length == 0)
					{
						text = "#  ";
					}
					text += $"{*ptr2:X2} ";
					if (i > 1 || len == 1)
					{
						if (i % 8 == 0 || i == len)
						{
							Log(text);
							text = "";
						}
						else if (i % 4 == 0)
						{
							text += " ";
						}
					}
					ptr2++;
				}
				byte[] destination = new byte[len];
				Marshal.Copy((IntPtr)ptr, destination, 0, len);
				MD5 mD = MD5.Create();
				byte[] array = mD.ComputeHash(destination);
				StringBuilder stringBuilder = new StringBuilder();
				for (int j = 0; j < array.Length; j++)
				{
					stringBuilder.Append(array[j].ToString("X2"));
				}
				Log($"HASH: {stringBuilder}");
			}
		}
	}
	public static class SymbolExtensions
	{
		public static MethodInfo GetMethodInfo(Expression<Action> expression)
		{
			return GetMethodInfo((LambdaExpression)expression);
		}

		public static MethodInfo GetMethodInfo<T>(Expression<Action<T>> expression)
		{
			return GetMethodInfo((LambdaExpression)expression);
		}

		public static MethodInfo GetMethodInfo<T, TResult>(Expression<Func<T, TResult>> expression)
		{
			return GetMethodInfo((LambdaExpression)expression);
		}

		public static MethodInfo GetMethodInfo(LambdaExpression expression)
		{
			if (!(expression.Body is MethodCallExpression { Method: var method }))
			{
				if (expression.Body is UnaryExpression { Operand: MethodCallExpression { Object: ConstantExpression { Value: MethodInfo value } } })
				{
					return value;
				}
				throw new ArgumentException("Invalid Expression. Expression should consist of a Method call only.");
			}
			if ((object)method == null)
			{
				throw new Exception($"Cannot find method for expression {expression}");
			}
			return method;
		}
	}
	internal class Tools
	{
		internal struct TypeAndName
		{
			internal Type type;

			internal string name;
		}

		internal static readonly bool isWindows = Environment.OSVersion.Platform.Equals(PlatformID.Win32NT);

		internal static TypeAndName TypColonName(string typeColonName)
		{
			if (typeColonName == null)
			{
				throw new ArgumentNullException("typeColonName");
			}
			string[] array = typeColonName.Split(':');
			if (array.Length != 2)
			{
				throw new ArgumentException(" must be specified as 'Namespace.Type1.Type2:MemberName", "typeColonName");
			}
			return new TypeAndName
			{
				type = AccessTools.TypeByName(array[0]),
				name = array[1]
			};
		}

		internal static void ValidateFieldType<F>(FieldInfo fieldInfo)
		{
			Type typeFromHandle = typeof(F);
			Type fieldType = fieldInfo.FieldType;
			if (typeFromHandle == fieldType)
			{
				return;
			}
			if (fieldType.IsEnum)
			{
				Type underlyingType = Enum.GetUnderlyingType(fieldType);
				if (typeFromHandle != underlyingType)
				{
					throw new ArgumentException("FieldRefAccess return type must be the same as FieldType or " + $"FieldType's underlying integral type ({underlyingType}) for enum types");
				}
			}
			else
			{
				if (fieldType.IsValueType)
				{
					throw new ArgumentException("FieldRefAccess return type must be the same as FieldType for value types");
				}
				if (!typeFromHandle.IsAssignableFrom(fieldType))
				{
					throw new ArgumentException("FieldRefAccess return type must be assignable from FieldType for reference types");
				}
			}
		}

		internal static AccessTools.FieldRef<T, F> FieldRefAccess<T, F>(FieldInfo fieldInfo, bool needCastclass)
		{
			ValidateFieldType<F>(fieldInfo);
			Type typeFromHandle = typeof(T);
			Type declaringType = fieldInfo.DeclaringType;
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("__refget_" + typeFromHandle.Name + "_fi_" + fieldInfo.Name, typeof(F).MakeByRefType(), new Type[1] { typeFromHandle });
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			if (fieldInfo.IsStatic)
			{
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldsflda, fieldInfo);
			}
			else
			{
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
				if (needCastclass)
				{
					iLGenerator.Emit(System.Reflection.Emit.OpCodes.Castclass, declaringType);
				}
				iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldflda, fieldInfo);
			}
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
			return dynamicMethodDefinition.Generate().CreateDelegate<AccessTools.FieldRef<T, F>>();
		}

		internal static AccessTools.StructFieldRef<T, F> StructFieldRefAccess<T, F>(FieldInfo fieldInfo) where T : struct
		{
			ValidateFieldType<F>(fieldInfo);
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("__refget_" + typeof(T).Name + "_struct_fi_" + fieldInfo.Name, typeof(F).MakeByRefType(), new Type[1] { typeof(T).MakeByRefType() });
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldflda, fieldInfo);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
			return dynamicMethodDefinition.Generate().CreateDelegate<AccessTools.StructFieldRef<T, F>>();
		}

		internal static AccessTools.FieldRef<F> StaticFieldRefAccess<F>(FieldInfo fieldInfo)
		{
			if (!fieldInfo.IsStatic)
			{
				throw new ArgumentException("Field must be static");
			}
			ValidateFieldType<F>(fieldInfo);
			DynamicMethodDefinition dynamicMethodDefinition = new DynamicMethodDefinition("__refget_" + (fieldInfo.DeclaringType?.Name ?? "null") + "_static_fi_" + fieldInfo.Name, typeof(F).MakeByRefType(), Array.Empty<Type>());
			ILGenerator iLGenerator = dynamicMethodDefinition.GetILGenerator();
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ldsflda, fieldInfo);
			iLGenerator.Emit(System.Reflection.Emit.OpCodes.Ret);
			return dynamicMethodDefinition.Generate().CreateDelegate<AccessTools.FieldRef<F>>();
		}

		internal static FieldInfo GetInstanceField(Type type, string fieldName)
		{
			FieldInfo fieldInfo = AccessTools.Field(type, fieldName);
			if ((object)fieldInfo == null)
			{
				throw new MissingFieldException(type.Name, fieldName);
			}
			if (fieldInfo.IsStatic)
			{
				throw new ArgumentException("Field must not be static");
			}
			return fieldInfo;
		}

		internal static bool FieldRefNeedsClasscast(Type delegateInstanceType, Type declaringType)
		{
			bool flag = false;
			if (delegateInstanceType != declaringType)
			{
				flag = delegateInstanceType.IsAssignableFrom(declaringType);
				if (!flag && !declaringType.IsAssignableFrom(delegateInstanceType))
				{
					throw new ArgumentException("FieldDeclaringType must be assignable from or to T (FieldRefAccess instance type) - \"instanceOfT is FieldDeclaringType\" must be possible");
				}
			}
			return flag;
		}

		internal static void ValidateStructField<T, F>(FieldInfo fieldInfo) where T : struct
		{
			if (fieldInfo.IsStatic)
			{
				throw new ArgumentException("Field must not be static");
			}
			if (fieldInfo.DeclaringType != typeof(T))
			{
				throw new ArgumentException("FieldDeclaringType must be T (StructFieldRefAccess instance type)");
			}
		}
	}
	public class Traverse<T>
	{
		private readonly Traverse traverse;

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

		public Traverse(Traverse traverse)
		{
			this.traverse = traverse;
		}
	}
	public class Traverse
	{
		private static readonly AccessCache Cache;

		private readonly Type _type;

		private readonly object _root;

		private readonly MemberInfo _info;

		private readonly MethodBase _method;

		private readonly object[] _params;

		public static Action<Traverse, Traverse> CopyFields;

		public bool IsField => _info is FieldInfo;

		public bool IsProperty => _info is PropertyInfo;

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

		public static Traverse Create(Type type)
		{
			return new Traverse(type);
		}

		public static Traverse Create<T>()
		{
			return Create(typeof(T));
		}

		public static Traverse Create(object root)
		{
			return new Traverse(root);
		}

		public static Traverse CreateWithType(string name)
		{
			return new Traverse(AccessTools.TypeByName(name));
		}

		private Traverse()
		{
		}

		public Traverse(Type type)
		{
			_type = type;
		}

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

		public T GetValue<T>()
		{
			object value = GetValue();
			if (value == null)
			{
				return default(T);
			}
			return (T)value;
		}

		public object GetValue(params object[] arguments)
		{
			if ((object)_method == null)
			{
				throw new Exception("cannot get method value without method");
			}
			return _method.Invoke(_root, arguments);
		}

		public T GetValue<T>(params object[] arguments)
		{
			if ((object)_method == null)
			{
				throw new Exception("cannot get method value without method");
			}
			return (T)_method.Invoke(_root, arguments);
		}

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

		public Traverse<T> Field<T>(string name)
		{
			return new Traverse<T>(Field(name));
		}

		public List<string> Fields()
		{
			Traverse traverse = Resolve();
			return AccessTools.GetFieldNames(traverse._type);
		}

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

		public Traverse<T> Property<T>(string name, object[] index = null)
		{
			return new Traverse<T>(Property(name, index));
		}

		public List<string> Properties()
		{
			Traverse traverse = Resolve();
			return AccessTools.GetPropertyNames(traverse._type);
		}

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

		public List<string> Methods()
		{
			Traverse traverse = Resolve();
			return AccessTools.GetMethodNames(traverse._type);
		}

		public bool FieldExists()
		{
			if ((object)_info != null)
			{
				return _info is FieldInfo;
			}
			return false;
		}

		public bool PropertyExists()
		{
			if ((object)_info != null)
			{
				return _info is PropertyInfo;
			}
			return false;
		}

		public bool MethodExists()
		{
			return (object)_method != null;
		}

		public bool TypeExists()
		{
			return (object)_type != null;
		}

		public static void IterateFields(object source, Action<Traverse> action)
		{
			Traverse sourceTrv = Create(source);
			AccessTools.GetFieldNames(source).ForEach(delegate(string f)
			{
				action(sourceTrv.Field(f));
			});
		}

		public static void IterateFields(object source, object target, Action<Traverse, Traverse> action)
		{
			Traverse sourceTrv = Create(source);
			Traverse targetTrv = Create(target);
			AccessTools.GetFieldNames(source).ForEach(delegate(string f)
			{
				action(sourceTrv.Field(f), targetTrv.Field(f));
			});
		}

		public static void IterateFields(object source, object target, Action<string, Traverse, Traverse> action)
		{
			Traverse sourceTrv = Create(source);
			Traverse targetTrv = Create(target);
			AccessTools.GetFieldNames(source).ForEach(delegate(string f)
			{
				action(f, sourceTrv.Field(f), targetTrv.Field(f));
			});
		}

		public static void IterateProperties(object source, Action<Traverse> action)
		{
			Traverse sourceTrv = Create(source);
			AccessTools.GetPropertyNames(source).ForEach(delegate(string f)
			{
				action(sourceTrv.Property(f));
			});
		}

		public static void IterateProperties(object source, object target, Action<Traverse, Traverse> action)
		{
			Traverse sourceTrv = Create(source);
			Traverse targetTrv = Create(target);
			AccessTools.GetPropertyNames(source).ForEach(delegate(string f)
			{
				action(sourceTrv.Property(f), targetTrv.Property(f));
			});
		}

		public static void IterateProperties(object source, object target, Action<string, Traverse, Traverse> action)
		{
			Traverse sourceTrv = Create(source);
			Traverse targetTrv = Create(target);
			AccessTools.GetPropertyNames(source).ForEach(delegate(string f)
			{
				action(f, sourceTrv.Property(f), targetTrv.Property(f));
			});
		}

		public override string ToString()
		{
			return (_method ?? GetValue())?.ToString();
		}
	}
}
[CompilerGenerated]
internal sealed class <>z__ReadOnlyArray<T> : IEnumerable, ICollection, IList, IEnumerable<T>, IReadOnlyCollection<T>, IReadOnlyList<T>, ICollection<T>, IList<T>
{
	int ICollection.Count => _items.Length;

	bool ICollection.IsSynchronized => false;

	object ICollection.SyncRoot => this;

	object IList.this[int index]
	{
		get
		{
			return _items[index];
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	bool IList.IsFixedSize => true;

	bool IList.IsReadOnly => true;

	int IReadOnlyCollection<T>.Count => _items.Length;

	T IReadOnlyList<T>.this[int index] => _items[index];

	int ICollection<T>.Count => _items.Length;

	bool ICollection<T>.IsReadOnly => true;

	T IList<T>.this[int index]
	{
		get
		{
			return _items[index];
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public <>z__ReadOnlyArray(T[] items)
	{
		_items = items;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return ((IEnumerable)_items).GetEnumerator();
	}

	void ICollection.CopyTo(Array array, int index)
	{
		((ICollection)_items).CopyTo(array, index);
	}

	int IList.Add(object value)
	{
		throw new NotSupportedException();
	}

	void IList.Clear()
	{
		throw new NotSupportedException();
	}

	bool IList.Contains(object value)
	{
		return ((IList)_items).Contains(value);
	}

	int IList.IndexOf(object value)
	{
		return ((IList)_items).IndexOf(value);
	}

	void IList.Insert(int index, object value)
	{
		throw new NotSupportedException();
	}

	void IList.Remove(object value)
	{
		throw new NotSupportedException();
	}

	void IList.RemoveAt(int index)
	{
		throw new NotSupportedException();
	}

	IEnumerator<T> IEnumerable<T>.GetEnumerator()
	{
		return ((IEnumerable<T>)_items).GetEnumerator();
	}

	void ICollection<T>.Add(T item)
	{
		throw new NotSupportedException();
	}

	void ICollection<T>.Clear()
	{
		throw new NotSupportedException();
	}

	bool ICollection<T>.Contains(T item)
	{
		return ((ICollection<T>)_items).Contains(item);
	}

	void ICollection<T>.CopyTo(T[] array, int arrayIndex)
	{
		((ICollection<T>)_items).CopyTo(array, arrayIndex);
	}

	bool ICollection<T>.Remove(T item)
	{
		throw new NotSupportedException();
	}

	int IList<T>.IndexOf(T item)
	{
		return ((IList<T>)_items).IndexOf(item);
	}

	void IList<T>.Insert(int index, T item)
	{
		throw new NotSupportedException();
	}

	void IList<T>.RemoveAt(int index)
	{
		throw new NotSupportedException();
	}
}
[CompilerGenerated]
internal sealed class <>z__ReadOnlySingleElementList<T> : IEnumerable, ICollection, IList, IEnumerable<T>, IReadOnlyCollection<T>, IReadOnlyList<T>, ICollection<T>, IList<T>
{
	private sealed class Enumerator : IDisposable, IEnumerator, IEnumerator<T>
	{
		object IEnumerator.Current => _item;

		T IEnumerator<T>.Current => _item;

		public Enumerator(T item)
		{
			_item = item;
		}

		bool IEnumerator.MoveNext()
		{
			if (!_moveNextCalled)
			{
				return _moveNextCalled = true;
			}
			return false;
		}

		void IEnumerator.Reset()
		{
			_moveNextCalled = false;
		}

		void IDisposable.Dispose()
		{
		}
	}

	int ICollection.Count => 1;

	bool ICollection.IsSynchronized => false;

	object ICollection.SyncRoot => this;

	object IList.this[int index]
	{
		get
		{
			if (index != 0)
			{
				throw new IndexOutOfRangeException();
			}
			return _item;
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	bool IList.IsFixedSize => true;

	bool IList.IsReadOnly => true;

	int IReadOnlyCollection<T>.Count => 1;

	T IReadOnlyList<T>.this[int index]
	{
		get
		{
			if (index != 0)
			{
				throw new IndexOutOfRangeException();
			}
			return _item;
		}
	}

	int ICollection<T>.Count => 1;

	bool ICollection<T>.IsReadOnly => true;

	T IList<T>.this[int index]
	{
		get
		{
			if (index != 0)
			{
				throw new IndexOutOfRangeException();
			}
			return _item;
		}
		set
		{
			throw new NotSupportedException();
		}
	}

	public <>z__ReadOnlySingleElementList(T item)
	{
		_item = item;
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return new Enumerator(_item);
	}

	void ICollection.CopyTo(Array array, int index)
	{
		array.SetValue(_item, index);
	}

	int IList.Add(object value)
	{
		throw new NotSupportedException();
	}

	void IList.Clear()
	{
		throw new NotSupportedException();
	}

	bool IList.Contains(object value)
	{
		return EqualityComparer<T>.Default.Equals(_item, (T)value);
	}

	int IList.IndexOf(object value)
	{
		if (!EqualityComparer<T>.Default.Equals(_item, (T)value))
		{
			return -1;
		}
		return 0;
	}

	void IList.Insert(int index, object value)
	{
		throw new NotSupportedException();
	}

	void IList.Remove(object value)
	{
		throw new NotSupportedException();
	}

	void IList.RemoveAt(int index)
	{
		throw new NotSupportedException();
	}

	IEnumerator<T> IEnumerable<T>.GetEnumerator()
	{
		return new Enumerator(_item);
	}

	void ICollection<T>.Add(T item)
	{
		throw new NotSupportedException();
	}

	void ICollection<T>.Clear()
	{
		throw new NotSupportedException();
	}

	bool ICollection<T>.Contains(T item)
	{
		return EqualityComparer<T>.Default.Equals(_item, item);
	}

	void ICollection<T>.CopyTo(T[] array, int arrayIndex)
	{
		array[arrayIndex] = _item;
	}

	bool ICollection<T>.Remove(T item)
	{
		throw new NotSupportedException();
	}

	int IList<T>.IndexOf(T item)
	{
		if (!EqualityComparer<T>.Default.Equals(_item, item))
		{
			return -1;
		}
		return 0;
	}

	void IList<T>.Insert(int index, T item)
	{
		throw new NotSupportedException();
	}

	void IList<T>.RemoveAt(int index)
	{
		throw new NotSupportedException();
	}
}
