using System.Runtime.CompilerServices;

namespace Iced.Intel;

internal static class MnemonicUtils
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Mnemonic Mnemonic(this Code code)
	{
		return (Mnemonic)MnemonicUtilsData.toMnemonic[(int)code];
	}
}
