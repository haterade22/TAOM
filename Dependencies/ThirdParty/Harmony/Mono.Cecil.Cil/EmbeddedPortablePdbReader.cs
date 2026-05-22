using System;
using Mono.Collections.Generic;

namespace Mono.Cecil.Cil;

internal sealed class EmbeddedPortablePdbReader : ISymbolReader, IDisposable
{
	private readonly PortablePdbReader reader;

	internal EmbeddedPortablePdbReader(PortablePdbReader reader)
	{
		if (reader == null)
		{
			throw new ArgumentNullException();
		}
		this.reader = reader;
	}

	public ISymbolWriterProvider GetWriterProvider()
	{
		return new EmbeddedPortablePdbWriterProvider();
	}

	public bool ProcessDebugHeader(ImageDebugHeader header)
	{
		return reader.ProcessDebugHeader(header);
	}

	public MethodDebugInformation Read(MethodDefinition method)
	{
		return reader.Read(method);
	}

	public Collection<CustomDebugInformation> Read(ICustomDebugInformationProvider provider)
	{
		return reader.Read(provider);
	}

	public void Dispose()
	{
		reader.Dispose();
	}
}
