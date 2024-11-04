using System.Text;

if (args.Length > 0)
{
	switch (args[0].ToLowerInvariant())
	{
		case "-x":
            if (args.Length > 1)
            {
				return ExtractPXM(args[1], args[2..]);
			}
			else
			{
				return 0;
			}
		case "-c":
			if (args.Length > 1)
			{
				return WritePXM(args[1], args[2..]);
			}
			else
			{
				return 0;
			}
		case "-d":
			if (args.Length > 1)
			{
				foreach (string d in Directory.GetFiles(args[1], "*.pxm", SearchOption.AllDirectories))
				{
					Console.WriteLine("{0}: Converting...", d);
					ConvertSinglePXM([d]);
				}
			}
			else
			{
				return 0;
			}
			return 0;
		case "":
			return 0;
		default:
			return ConvertSinglePXM(args);
			//break;
	}
}
else
{
	Console.WriteLine("PXM Conversion Tool 1.0");
	Console.WriteLine();
	Console.WriteLine("Default usage - Convert old PXM (QLP) files to new format: input [output]");
	Console.WriteLine("Extract files from PXM: -x input [output] [output]...");
	Console.WriteLine("Create PXM from files: -c output [input] [input]...");
	Console.WriteLine("Convert old PXM directory to new format: -d dir");
}
return 0;

static int ConvertSinglePXM(string[] args)
{
	if (args.Length > 1)
	{
		Dictionary<string, byte[]> keys = LoadQLP(args[0]);
		return WritePXMFromQLP(keys, args[1]);
	}
	else
	{
		try
		{
			File.Move(args[0], args[0] + ".BAK");
			Dictionary<string, byte[]> keys = LoadQLP(args[0] + ".BAK");
			return WritePXMFromQLP(keys, args[0]);
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine("File Error: {0}", ex.Message);
			return 1;
		}
	}
}

static int WritePXM(string path, string[] paths)
{
	try
	{
		int[] sizes = new int[paths.Length];
		int[] pads = new int[paths.Length];
		int addr = 8 + (paths.Length * 8);
		BinaryWriter writer = new(new FileStream(path, FileMode.Create));
		writer.Write(0x004D5850); //PXM
		writer.Write(paths.Length);
		for (int i = 0; i < paths.Length; i++)
		{
			//addrs[i] = cur_addr;

			sizes[i] = (int)new FileInfo(paths[i]).Length;
			writer.Write(addr);
			writer.Write(sizes[i]);
			addr += sizes[i];
			pads[i] = 4 - (sizes[i] % 4);
			if (pads[i] == 4)
			{
				pads[i] = 0;
			}
			addr += pads[i];
		}
		for (int i = 0; i < pads.Length; i++)
		{
			BinaryReader reader = new(new FileStream(paths[i], FileMode.Open));
			byte[] buffer = reader.ReadBytes(sizes[i]);
			writer.Write(buffer);
			writer.Write(new byte[pads[i]]);
			reader.Dispose();
		}
		writer.Flush();
		writer.Dispose();
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine("Write Error: {0}", ex.Message);
		return 1;
	}
	return 0;
}
static int ExtractPXM(string path, string[] paths)
{
	try
	{
		BinaryReader reader = new(new FileStream(path, FileMode.Open));
		if (reader.ReadUInt32() != 0x004D5850)
		{
			Console.Error.WriteLine("Warning: Wrong PXM Header!");
		}
		int filenum = reader.ReadInt32();
		int size, addr;
		byte[] data;
		BinaryWriter writer;

		for (int i = 0; i < filenum; ++i)
		{
			reader.BaseStream.Position = 8 + (i * 8);
			addr = reader.ReadInt32();
			size = reader.ReadInt32();
			reader.BaseStream.Position = addr;
			data = reader.ReadBytes(size);
			if (i < paths.Length)
			{
				writer = new(new FileStream(paths[i], FileMode.Create));
			}
			else
			{
				writer = new(new FileStream(path + "_" + i, FileMode.Create));
			}
			writer.Write(data);
			writer.Flush();
			writer.Dispose();
		}
	}
	catch (Exception ex)
	{
		Console.Error.WriteLine("Extract Exception: {0}", ex.Message);
		return 1;
	}
	return 0;
}

static Dictionary<string, byte[]> LoadQLP(string path)
{
	Dictionary<string, byte[]> pairs = [];

	try
	{
		BinaryReader reader = new(new FileStream(path, FileMode.Open));
		if (reader.ReadUInt32() != 0x00504C51)
		{
			Console.Error.WriteLine("Warning: Wrong QLP Header!");
		}
		int filenum = reader.ReadInt32();

		string fname;
		byte[] bytes;
		int addr, size;

		for (int i = 0; i < filenum; ++i)
		{
			reader.BaseStream.Position = 8 + (i * 24);
			bytes = reader.ReadBytes(16);
			fname = Encoding.UTF8.GetString(bytes).TrimEnd((char)0x00);
			size = reader.ReadInt32();
			addr = reader.ReadInt32();
			reader.BaseStream.Position = addr * 4;
			pairs.Add(fname, reader.ReadBytes(size));
		}

		reader.Dispose();
	}
	catch (Exception e)
	{
		Console.Error.WriteLine("Read Exception: {0}", e.Message);
	}
	return pairs;
}

static int WritePXMFromQLP(Dictionary<string, byte[]> pairs, string path)
{
	if (pairs.Count != 3)
	{
		Console.Error.WriteLine("QLP has wrong number of files, not a PXM!");
		return 1;
	}
	try
	{
		BinaryWriter writer = new(new FileStream(path, FileMode.Create));
		writer.Write(0x004D5850); //PXM
		writer.Write(2);
		int vab_size = pairs.ElementAt(1).Value.Length + pairs.ElementAt(2).Value.Length;
		writer.Write(8 + (2 * 8));
		writer.Write(vab_size);
		int padding = 4 - (vab_size % 4);
		if (padding == 4)
		{
			padding = 0;
		}
		writer.Write(8 + (2 * 8) + vab_size + padding);
		writer.Write(pairs.ElementAt(0).Value.Length);
		writer.Write(pairs.ElementAt(1).Value);
		writer.Write(pairs.ElementAt(2).Value);
		if (padding > 0)
		{
			writer.Write(new byte[padding]);
		}
		writer.Write(pairs.ElementAt(0).Value);
		padding = 4 - (pairs.ElementAt(0).Value.Length % 4);
		if (padding == 4)
		{
			padding = 0;
		}
		else
		{
			writer.Write(new byte[padding]);
		}
		writer.Flush();
		writer.Dispose();
	}
	catch (Exception e)
	{
		Console.Error.WriteLine("Write Exception: {0}", e.Message);
		return 1;
	}

	return 0;
}