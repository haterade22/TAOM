using System.Threading;
using Mono.Collections.Generic;

namespace Mono.Cecil.Cil;

internal abstract class DebugInformation : ICustomDebugInformationProvider, IMetadataTokenProvider
{
	internal MetadataToken token;

	internal Collection<CustomDebugInformation> custom_infos;

	public MetadataToken MetadataToken
	{
		get
		{
			return token;
		}
		set
		{
			token = value;
		}
	}

	public bool HasCustomDebugInformations => !custom_infos.IsNullOrEmpty();

	public Collection<CustomDebugInformation> CustomDebugInformations
	{
		get
		{
			if (custom_infos == null)
			{
				Interlocked.CompareExchange(ref custom_infos, new Collection<CustomDebugInformation>(), null);
			}
			return custom_infos;
		}
	}

	internal DebugInformation()
	{
	}
}
