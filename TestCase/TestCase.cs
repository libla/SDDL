using System.IO;
using System.Text;
using SDDL;

public class TestCase : Target
{
	public TestCase(string ns) {}

	public Encoding Encoding
	{
		get { return new UTF8Encoding(false); }
	}

	public string NewLine
	{
		get { return "\n"; }
	}

	public void Prepare(StreamWriter writer) {}
	public void Flush(StreamWriter writer) {}
	public void Value(StreamWriter writer, string name, bool b) {}
	public void Value(StreamWriter writer, string name, int i) {}
	public void Value(StreamWriter writer, string name, double d) {}
	public void Value(StreamWriter writer, string name, string s) {}
	public void Message(StreamWriter writer, string name, params Entry[] entries) {}
	public void RPC(StreamWriter writer, string name, params Call[] calls) {}
}