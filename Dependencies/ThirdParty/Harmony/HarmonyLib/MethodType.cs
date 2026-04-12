namespace HarmonyLib;

/// <summary>Specifies the type of method</summary>
public enum MethodType
{
	/// <summary>This is a normal method</summary>
	Normal,
	/// <summary>This is a getter</summary>
	Getter,
	/// <summary>This is a setter</summary>
	Setter,
	/// <summary>This is a constructor</summary>
	Constructor,
	/// <summary>This is a static constructor</summary>
	StaticConstructor,
	/// <summary>This targets the MoveNext method of the enumerator result, that actually contains the method's implementation</summary>
	Enumerator,
	/// <summary>This targets the MoveNext method of the async state machine, that actually contains the method's implementation</summary>
	Async,
	/// <summary>Finalize</summary>
	Finalizer,
	/// <summary>This is a add event method</summary>
	EventAdd,
	/// <summary>This is a remove event method</summary>
	EventRemove,
	/// <summary>This is a op_Implicit</summary>
	OperatorImplicit,
	/// <summary>This is a op_Explicit</summary>
	OperatorExplicit,
	/// <summary>This is a op_UnaryPlus</summary>
	OperatorUnaryPlus,
	/// <summary>This is a op_UnaryNegation</summary>
	OperatorUnaryNegation,
	/// <summary>This is a op_LogicalNot</summary>
	OperatorLogicalNot,
	/// <summary>This is a op_OnesComplement</summary>
	OperatorOnesComplement,
	/// <summary>This is a op_Increment</summary>
	OperatorIncrement,
	/// <summary>This is a op_Decrement</summary>
	OperatorDecrement,
	/// <summary>This is a op_True</summary>
	OperatorTrue,
	/// <summary>This is a op_False</summary>
	OperatorFalse,
	/// <summary>This is a op_Addition</summary>
	OperatorAddition,
	/// <summary>This is a op_Subtraction</summary>
	OperatorSubtraction,
	/// <summary>This is a op_Multiply</summary>
	OperatorMultiply,
	/// <summary>This is a op_Division</summary>
	OperatorDivision,
	/// <summary>This is a op_Modulus</summary>
	OperatorModulus,
	/// <summary>This is a op_BitwiseAnd</summary>
	OperatorBitwiseAnd,
	/// <summary>This is a op_BitwiseOr</summary>
	OperatorBitwiseOr,
	/// <summary>This is a op_ExclusiveOr</summary>
	OperatorExclusiveOr,
	/// <summary>This is a op_LeftShift</summary>
	OperatorLeftShift,
	/// <summary>This is a op_RightShift</summary>
	OperatorRightShift,
	/// <summary>This is a op_Equality</summary>
	OperatorEquality,
	/// <summary>This is a op_Inequality</summary>
	OperatorInequality,
	/// <summary>This is a op_GreaterThan</summary>
	OperatorGreaterThan,
	/// <summary>This is a op_LessThan</summary>
	OperatorLessThan,
	/// <summary>This is a op_GreaterThanOrEqual</summary>
	OperatorGreaterThanOrEqual,
	/// <summary>This is a op_LessThanOrEqual</summary>
	OperatorLessThanOrEqual,
	/// <summary>This is a op_Comma</summary>
	OperatorComma
}
