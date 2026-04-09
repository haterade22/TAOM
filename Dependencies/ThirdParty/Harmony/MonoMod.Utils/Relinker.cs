using Mono.Cecil;

namespace MonoMod.Utils;

internal delegate IMetadataTokenProvider Relinker(IMetadataTokenProvider mtp, IGenericParameterProvider? context);
