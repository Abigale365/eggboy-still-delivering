using System;
using System.Collections;

public interface ILux3DGateway
{
	string Name { get; }

	bool CanGenerate { get; }

	IEnumerator Generate(Lux3DRequest req, Lux3DResult result, Func<bool> cancelled);
}
