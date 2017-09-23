using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace SDDL
{
	internal class Compiler
	{
		private static readonly string RootDirectory = Environment.CurrentDirectory;

		private readonly List<string> files = new List<string>();
		private readonly HashSet<string> fileset = new HashSet<string>();
		private readonly Dictionary<string, Value> consts = new Dictionary<string, Value>();
		private readonly Dictionary<string, Message> messages = new Dictionary<string, Message>();
		private readonly Dictionary<string, Typedef> typedefs = new Dictionary<string, Typedef>();
		private readonly Dictionary<string, RPC> rpcs = new Dictionary<string, RPC>();

		private static string GetRelativePath(string path)
		{
			Uri uri = new Uri(path);
			Uri root = new Uri(RootDirectory + Path.DirectorySeparatorChar);
			return root.MakeRelativeUri(uri).ToString().Replace('/', Path.DirectorySeparatorChar);
		}

		public void AddInput(string filename)
		{
			filename = filename.Replace('/', Path.DirectorySeparatorChar);
			filename = Path.IsPathRooted(filename) ? filename : Path.Combine(RootDirectory, filename);
			if (!fileset.Contains(filename))
			{
				fileset.Add(filename);
				files.Add(filename);
			}
		}

		private void Parse(string filename)
		{
			string old = Environment.CurrentDirectory;
			Environment.CurrentDirectory = Path.GetDirectoryName(filename);
			Dictionary<string, Value> localconsts = new Dictionary<string, Value>();
			using (StreamReader file = new StreamReader(new FileStream(filename, FileMode.Open, FileAccess.Read), Encoding.UTF8))
			{
				AntlrInputStream stream = new AntlrInputStream(file);
				ITokenSource lexer = new SDDLLexer(stream);
				ITokenStream tokens = new CommonTokenStream(lexer);
				SDDLParser parser = new SDDLParser(tokens) {BuildParseTree = true, ErrorHandler = new ErrorStrategy()};
				SDDLParser.DefinesContext context = parser.defines();
				RequireListener require = new RequireListener(this);
				ParseTreeWalker.Default.Walk(require, context);
				ConstantListener constant = new ConstantListener(filename) {preload = consts, local = localconsts};
				ParseTreeWalker.Default.Walk(constant, context);
				constant.Collect();
				MessageListener message = new MessageListener(filename, consts, localconsts) {preload = messages};
				ParseTreeWalker.Default.Walk(message, context);
				message.Collect();
				AliasListener alias = new AliasListener(filename) {preload = typedefs};
				ParseTreeWalker.Default.Walk(alias, context);
				RPCListener rpc = new RPCListener(filename) {preload = rpcs};
				ParseTreeWalker.Default.Walk(rpc, context);
			}
			Environment.CurrentDirectory = old;
		}

		public bool Output(Target target, StreamWriter output, ref string errmsg)
		{
			fileset.Clear();
			foreach (string file in files)
			{
				if (fileset.Contains(file))
					continue;
				fileset.Add(file);
				try
				{
					Parse(file);
				}
				catch (ParseException e)
				{
					List<string> builder = new List<string>();
					foreach (var token in e.exception.GetExpectedTokens().ToList())
					{
						string name = e.exception.Recognizer.Vocabulary.GetDisplayName(token);
						if (name == "DONE")
							name = "'<EOF>'";
						builder.Add(name);
					}
					errmsg = string.Format("{0}({1}): expected {2}, got {3}", GetRelativePath(file), e.recognizer.CurrentToken.Line,
											string.Join(" or ", builder), string.Format("'{0}'", e.recognizer.CurrentToken.Text));
					return false;
				}
				catch (Exception e)
				{
					errmsg = e.Message + Environment.NewLine + e.StackTrace;
					return false;
				}
			}
			target.Prepare(output);
			string[] constnames = new string[consts.Keys.Count];
			consts.Keys.CopyTo(constnames, 0);
			Array.Sort(constnames);
			foreach (string name in constnames)
			{
				Value value = consts[name];
				switch (value.Typeof())
				{
				case Type.Bool:
					target.Value(output, name, value.ToBoolean());
					break;
				case Type.Int:
					target.Value(output, name, value.ToInteger());
					break;
				case Type.Float:
					target.Value(output, name, value.ToFloat());
					break;
				case Type.String:
					target.Value(output, name, value.ToString());
					break;
				}
			}

			string[] messagenames = new string[messages.Keys.Count];
			messages.Keys.CopyTo(messagenames, 0);
			Array.Sort(messagenames);
			foreach (string name in messagenames)
			{
				Message message = messages[name];
				Entry[] entries = message.entries.ToArray();
				target.Message(output, name, entries);
			}

			string[] aliasnames = new string[typedefs.Keys.Count];
			typedefs.Keys.CopyTo(aliasnames, 0);
			Array.Sort(aliasnames);
			foreach (string name in aliasnames)
			{
				Typedef typedef = typedefs[name];
				Alias[] aliases = typedef.aliases.ToArray();
				Array.Sort(aliases, (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
				target.Typedef(output, name, aliases);
			}

			string[] rpcnames = new string[rpcs.Keys.Count];
			rpcs.Keys.CopyTo(rpcnames, 0);
			Array.Sort(rpcnames);
			foreach (string name in rpcnames)
			{
				RPC rpc = rpcs[name];
				Call[] calls = rpc.calls.ToArray();
				Array.Sort(calls, (left, right) => string.Compare(left.Name, right.Name, StringComparison.Ordinal));
				target.RPC(output, name, calls);
			}

			target.Flush(output);
			return true;
		}

		#region require块处理流程
		private class RequireListener : SDDLBaseListener
		{
			private readonly Compiler compiler;

			public RequireListener(Compiler compiler)
			{
				this.compiler = compiler;
			}

			public override void EnterRequire(SDDLParser.RequireContext context)
			{
				foreach (var node in context.STRING())
				{
					string path = Path.Combine(Environment.CurrentDirectory, Escape(node.Symbol.Text).Replace('/', Path.DirectorySeparatorChar));
					if (!compiler.fileset.Contains(path))
					{
						compiler.fileset.Add(path);
						compiler.Parse(path);
					}
				}
			}
		}
		#endregion

		#region 常量表达式
		private class ConstantExpr : Expr
		{
			public readonly string name;
			public bool hide;
			public Type? type;
			public Expr expr;
			public IToken token;
			public readonly HashSet<ConstantExpr> refers;

			public ConstantExpr(string name)
			{
				this.name = name;
				this.type = null;
				this.hide = false;
				this.refers = new HashSet<ConstantExpr>();
			}

			public override bool TryToBoolean(out bool b)
			{
				if (type.HasValue && type.Value != Type.Bool)
				{
					b = default(bool);
					return false;
				}
				return expr.TryToBoolean(out b);
			}

			public override bool TryToInteger(out int i)
			{
				if (type.HasValue && type.Value != Type.Int)
				{
					i = default(int);
					return false;
				}
				return expr.TryToInteger(out i);
			}

			public override bool TryToFloat(out double d)
			{
				if (type.HasValue && type.Value != Type.Int && type.Value != Type.Float)
				{
					d = default(double);
					return false;
				}
				return expr.TryToFloat(out d);
			}

			public override bool TryToString(out string s)
			{
				if (type.HasValue && type.Value != Type.String)
				{
					s = default(string);
					return false;
				}
				return expr.TryToString(out s);
			}

			public override Type Typeof()
			{
				return type ?? expr.Typeof();
			}
		}
		#endregion

		#region 字符串转义
		private static string Escape(string text)
		{
			StringBuilder builder = new StringBuilder(text.Length);
			for (int i = 1; i < text.Length - 1; ++i)
			{
				char c = text[i];
				if (c == '\\')
				{
					switch (text[++i])
					{
					case 'f':
						builder.Append('\f');
						break;
					case 'n':
						builder.Append('\n');
						break;
					case 'r':
						builder.Append('\r');
						break;
					case 't':
						builder.Append('\t');
						break;
					case '"':
						builder.Append('"');
						break;
					case '\'':
						builder.Append('"');
						break;
					case '\\':
						builder.Append('\\');
						break;
					case 'u':
						{
							int unicode = 0;
							for (int j = 1; j <= 4; ++j)
							{
								byte b = (byte)text[i + j];
								if (b >= 'a')
									b -= (byte)'a' - 10;
								else if (c >= 'A')
									b -= (byte)'A' - 10;
								else
									b -= (byte)'0';
								unicode <<= 4;
								unicode |= b;
							}
							i += 4;
							builder.Append((char)unicode);
						}
						break;
					}
				}
				else
				{
					builder.Append(c);
				}
			}
			return builder.ToString();
		}
		#endregion

		#region 解析常量表达式
		private static Expr ParseBuiltinExpr(SDDLParser.BuiltinContext context, Func<ITerminalNode, ConstantExpr> constants,
											Dictionary<string, Value> preload)
		{
			var name = context.NAME();
			if (name != null)
			{
				Value value;
				if (preload.TryGetValue(name.Symbol.Text, out value))
					return value;
				return constants(name);
			}
			var numberic = context.numeric();
			if (numberic != null)
				return ParseBuiltinExpr(numberic, constants, preload);
			var @string = context.@string();
			if (@string != null)
				return ParseBuiltinExpr(@string, constants, preload);
			var or = context.or();
			if (or != null)
				return ParseBuiltinExpr(or, constants, preload);
			return null;
		}

		private static Expr ParseBuiltinExpr(SDDLParser.BooleanContext context, Func<ITerminalNode, ConstantExpr> constants,
											Dictionary<string, Value> preload)
		{
			bool not = context.NOT() != null;
			ITerminalNode node;
			node = context.K_TRUE();
			if (node != null)
				return !not;
			node = context.K_FALSE();
			if (node != null)
				return not;
			node = context.NAME();
			if (node != null)
			{
				Value value;
				if (preload.TryGetValue(node.Symbol.Text, out value))
					return not ? !(Expr)value : value;
				return not ? !constants(node) : constants(node);
			}
			Expr expr = null;
			SDDLParser.NumericContext[] numbers = context.numeric();
			if (numbers != null && numbers.Length > 0)
			{
				ITerminalNode op = context.EQ() ?? context.CMP();
				switch (op.Symbol.Text)
				{
				case "<":
					expr = ParseBuiltinExpr(numbers[0], constants, preload) < ParseBuiltinExpr(numbers[1], constants, preload);
					break;
				case ">":
					expr = ParseBuiltinExpr(numbers[0], constants, preload) > ParseBuiltinExpr(numbers[1], constants, preload);
					break;
				case "<=":
					expr = ParseBuiltinExpr(numbers[0], constants, preload) <= ParseBuiltinExpr(numbers[1], constants, preload);
					break;
				case ">=":
					expr = ParseBuiltinExpr(numbers[0], constants, preload) >= ParseBuiltinExpr(numbers[1], constants, preload);
					break;
				case "==":
					expr = ParseBuiltinExpr(numbers[0], constants, preload).Equal(ParseBuiltinExpr(numbers[1], constants, preload));
					break;
				default:
					expr = ParseBuiltinExpr(numbers[0], constants, preload).NotEqual(ParseBuiltinExpr(numbers[1], constants, preload));
					break;
				}
			}
			else
			{
				SDDLParser.StringContext[] strings = context.@string();
				if (strings != null && strings.Length > 0)
				{
					expr = context.EQ().Symbol.Text == "=="
						? ParseBuiltinExpr(strings[0], constants, preload).Equal(ParseBuiltinExpr(strings[1], constants, preload))
						: ParseBuiltinExpr(strings[0], constants, preload).NotEqual(ParseBuiltinExpr(strings[1], constants, preload));
				}
				else
				{
					expr = ParseBuiltinExpr(context.or(), constants, preload);
				}
			}
			return not ? !expr : expr;
		}

		private static Expr ParseBuiltinExpr(SDDLParser.AndContext context, Func<ITerminalNode, ConstantExpr> constants,
											Dictionary<string, Value> preload)
		{
			SDDLParser.BooleanContext[] booleans = context.boolean();
			Expr expr = ParseBuiltinExpr(booleans[0], constants, preload);
			for (int i = 1; i < booleans.Length; ++i)
				expr = expr & ParseBuiltinExpr(booleans[i], constants, preload);
			return expr;
		}

		private static Expr ParseBuiltinExpr(SDDLParser.OrContext context, Func<ITerminalNode, ConstantExpr> constants,
											Dictionary<string, Value> preload)
		{
			SDDLParser.AndContext[] ands = context.and();
			Expr expr = ParseBuiltinExpr(ands[0], constants, preload);
			for (int i = 1; i < ands.Length; ++i)
				expr = expr | ParseBuiltinExpr(ands[i], constants, preload);
			return expr;
		}

		private static Expr ParseBuiltinExpr(SDDLParser.AtomContext context, Func<ITerminalNode, ConstantExpr> constants,
											Dictionary<string, Value> preload)
		{
			ITerminalNode node;
			node = context.HEX();
			if (node != null)
				return Convert.ToInt32(node.Symbol.Text.Substring(2), 16);
			node = context.INTEGER();
			if (node != null)
				return Convert.ToInt32(node.Symbol.Text);
			node = context.FLOAT();
			if (node != null)
				return Convert.ToDouble(node.Symbol.Text);
			node = context.NAME();
			if (node != null)
			{
				Value value;
				if (preload.TryGetValue(node.Symbol.Text, out value))
					return value;
				return constants(node);
			}
			return ParseBuiltinExpr(context.numeric(), constants, preload);
		}

		private static Expr ParseBuiltinExpr(SDDLParser.PowexpContext context, Func<ITerminalNode, ConstantExpr> constants,
											Dictionary<string, Value> preload)
		{
			SDDLParser.AtomContext[] atoms = context.atom();
			Expr expr = ParseBuiltinExpr(atoms[atoms.Length - 1], constants, preload);
			for (int i = atoms.Length - 2; i >= 0; --i)
			{
				expr = ParseBuiltinExpr(atoms[i], constants, preload) ^ expr;
			}
			return expr;
		}

		private static Expr ParseBuiltinExpr(SDDLParser.MulexpContext context, Func<ITerminalNode, ConstantExpr> constants,
											Dictionary<string, Value> preload)
		{
			SDDLParser.PowexpContext[] powers = context.powexp();
			Expr expr = ParseBuiltinExpr(powers[0], constants, preload);
			for (int i = 1; i < powers.Length; ++i)
			{
				ITerminalNode mul = context.MUL(i - 1);
				if (mul.Symbol.Text == "*")
				{
					expr = expr * ParseBuiltinExpr(powers[i], constants, preload);
				}
				else if (mul.Symbol.Text == "/")
				{
					expr = expr / ParseBuiltinExpr(powers[i], constants, preload);
				}
				else
				{
					expr = expr % ParseBuiltinExpr(powers[i], constants, preload);
				}
			}
			return expr;
		}

		private static Expr ParseBuiltinExpr(SDDLParser.NumericContext context, Func<ITerminalNode, ConstantExpr> constants,
											Dictionary<string, Value> preload)
		{
			SDDLParser.MulexpContext[] muls = context.mulexp();
			Expr expr = ParseBuiltinExpr(muls[0], constants, preload);
			for (int i = 1; i < muls.Length; ++i)
			{
				ITerminalNode add = context.ADD(i - 1);
				if (add.Symbol.Text == "+")
				{
					expr = expr + ParseBuiltinExpr(muls[i], constants, preload);
				}
				else
				{
					expr = expr - ParseBuiltinExpr(muls[i], constants, preload);
				}
			}
			return expr;
		}

		private static Expr ParseBuiltinExpr(SDDLParser.StringContext context, Func<ITerminalNode, ConstantExpr> constants,
											Dictionary<string, Value> preload)
		{
			Expr expr;
			ITerminalNode node = context.STRING();
			if (node != null)
			{
				expr = Escape(node.Symbol.Text);
			}
			else
			{
				node = context.NAME();
				Value value;
				if (preload.TryGetValue(node.Symbol.Text, out value))
					expr = value;
				else
					expr = constants(node);
			}
			SDDLParser.StringContext right = context.@string();
			if (right == null)
				return expr;
			return expr.Concat(ParseBuiltinExpr(right, constants, preload));
		}
		#endregion

		#region 常量处理流程
		private class ConstantListener : SDDLBaseListener
		{
			public Dictionary<string, Value> preload;
			public Dictionary<string, Value> local;
			private readonly Dictionary<string, ConstantExpr> constants = new Dictionary<string, ConstantExpr>();
			private readonly string file;

			public ConstantListener(string file)
			{
				this.file = file;
			}

			public override void EnterConstant(SDDLParser.ConstantContext context)
			{
				IToken nametoken = context.NAME().Symbol;
				string name = nametoken.Text;
				if (preload.ContainsKey(name))
					throw new Exception(string.Format("{0}({1}): name conflict with '{2}'", GetRelativePath(file), nametoken.Line, nametoken.Text));
				ConstantExpr constant;
				if (constants.TryGetValue(name, out constant) && constant.expr != null)
					throw new Exception(string.Format("{0}({1}): name conflict with '{2}'", GetRelativePath(file), nametoken.Line, nametoken.Text));
				if (constant == null)
				{
					constant = new ConstantExpr(name);
					constants.Add(constant.name, constant);
				}
				constant.token = nametoken;
				if (context.K_LOCAL() != null)
				{
					constant.hide = true;
				}
				else
				{
					constant.hide = false;
					if (context.K_AUTO() == null)
					{
						if (context.K_BOOLEAN() != null)
							constant.type = Type.Bool;
						else if (context.K_INTEGER() != null)
							constant.type = Type.Int;
						else if (context.K_NUMBER() != null)
							constant.type = Type.Float;
						else if (context.K_STRING() != null)
							constant.type = Type.String;
					}
				}
				constant.expr = ParseBuiltinExpr(context.builtin(), node =>
				{
					ConstantExpr expr;
					string text = node.Symbol.Text;
					if (!constants.TryGetValue(text, out expr))
					{
						expr = new ConstantExpr(text) {token = node.Symbol};
						constants.Add(expr.name, expr);
					}
					constant.refers.Add(expr);
					return expr;
				}, preload);
			}

			private void AddConstant(ConstantExpr expr, LinkedList<ConstantExpr> list, HashSet<string> collecting,
									HashSet<string> already)
			{
				if (!already.Contains(expr.name))
				{
					if (collecting.Contains(expr.name))
						throw new Exception(string.Format("{0}({1}): unable to evaluate expression due to circular reference", GetRelativePath(file), expr.token.Line));
					collecting.Add(expr.name);
					foreach (var refer in expr.refers)
					{
						AddConstant(refer, list, collecting, already);
					}
					collecting.Remove(expr.name);
					list.AddLast(expr);
					already.Add(expr.name);
				}
			}

			public void Collect()
			{
				foreach (var constant in constants.Values)
				{
					if (constant.expr == null)
						throw new Exception(string.Format("{0}({1}): variable '{2}' could not be found", GetRelativePath(file), constant.token.Line, constant.token.Text));
				}
				var values = constants.Values;
				ConstantExpr[] exprs = new ConstantExpr[values.Count];
				values.CopyTo(exprs, 0);
				Array.Sort(exprs, (lhs, rhs) =>
				{
					return String.Compare(lhs.name, rhs.name, StringComparison.Ordinal);
				});
				LinkedList<ConstantExpr> list = new LinkedList<ConstantExpr>();
				HashSet<string> already = new HashSet<string>();
				HashSet<string> collecting = new HashSet<string>();
				foreach (var constant in exprs)
				{
					AddConstant(constant, list, collecting, already);
				}
				foreach (var constant in list)
				{
					if (constant.type.HasValue)
					{
						bool match = false;
						string typename = "unknown";
						switch (constant.type.Value)
						{
						case Type.Bool:
							{
								typename = "boolean";
								bool b;
								match = constant.expr.TryToBoolean(out b);
								if (constant.hide)
									local.Add(constant.name, b);
								else
									preload.Add(constant.name, b);
							}
							break;
						case Type.Int:
							{
								typename = "integer";
								int i;
								match = constant.expr.TryToInteger(out i);
								if (constant.hide)
									local.Add(constant.name, i);
								else
									preload.Add(constant.name, i);
							}
							break;
						case Type.Float:
							{
								typename = "real";
								double d;
								match = constant.expr.TryToFloat(out d);
								if (constant.hide)
									local.Add(constant.name, d);
								else
									preload.Add(constant.name, d);
							}
							break;
						case Type.String:
							{
								typename = "string";
								string s;
								match = constant.expr.TryToString(out s);
								if (constant.hide)
									local.Add(constant.name, s);
								else
									preload.Add(constant.name, s);
							}
							break;
						}
						if (!match)
							throw new Exception(string.Format("{0}({1}): value cannot convert to '{2}'", GetRelativePath(file), constant.token.Line, typename));
					}
					else
					{
						try
						{
							constant.type = constant.expr.Typeof();
							switch (constant.type.Value)
							{
							case Type.Bool:
								if (constant.hide)
									local.Add(constant.name, constant.expr.ToBoolean());
								else
									preload.Add(constant.name, constant.expr.ToBoolean());
								break;
							case Type.Int:
								if (constant.hide)
									local.Add(constant.name, constant.expr.ToInteger());
								else
									preload.Add(constant.name, constant.expr.ToInteger());
								break;
							case Type.Float:
								if (constant.hide)
									local.Add(constant.name, constant.expr.ToFloat());
								else
									preload.Add(constant.name, constant.expr.ToFloat());
								break;
							case Type.String:
								if (constant.hide)
									local.Add(constant.name, constant.expr.ToString());
								else
									preload.Add(constant.name, constant.expr.ToString());
								break;
							}
						}
						catch (InvalidCastException)
						{
							throw new Exception(string.Format("{0}({1}): type mismatch in the expression", GetRelativePath(file), constant.token.Line));
						}
					}
				}
			}
		}
		#endregion

		#region 消息定义结构
		private class Message
		{
			public readonly string name;
			public List<Entry> entries;
			public IToken token;
			public readonly HashSet<Message> refers;

			public Message(string name)
			{
				this.name = name;
				this.entries = null;
				this.refers = new HashSet<Message>();
			}
		}
		#endregion

		#region 消息定义处理流程
		private class MessageListener : SDDLBaseListener
		{
			public Dictionary<string, Message> preload;
			private readonly string file;
			public readonly Dictionary<string, Value> consts;
			private readonly Dictionary<string, Message> messages = new Dictionary<string, Message>();

			public MessageListener(string file, Dictionary<string, Value> preload, Dictionary<string, Value> local)
			{
				this.file = file;
				consts = new Dictionary<string, Value>();
				foreach (var kv in preload)
				{
					consts[kv.Key] = kv.Value;
				}
				foreach (var kv in local)
				{
					consts[kv.Key] = kv.Value;
				}
			}

			public override void EnterMessage(SDDLParser.MessageContext context)
			{
				IToken nametoken = context.NAME().Symbol;
				string name = nametoken.Text;
				if (preload.ContainsKey(name))
					throw new Exception(string.Format("{0}({1}): name conflict with '{2}'", GetRelativePath(file), nametoken.Line, nametoken.Text));
				Message message;
				if (messages.TryGetValue(name, out message) && message.entries != null)
					throw new Exception(string.Format("{0}({1}): name conflict with '{2}'", GetRelativePath(file), nametoken.Line, nametoken.Text));
				if (message == null)
				{
					message = new Message(name);
					messages.Add(message.name, message);
				}
				message.entries = new List<Entry>();
				message.token = nametoken;
				HashSet<int> entryplaces = new HashSet<int>();
				HashSet<string> entrynames = new HashSet<string>();
				SDDLParser.EntryContext[] entries = context.entry();
				foreach (var entry in entries)
				{
					IToken entryplacetoken = entry.PLACE().Symbol;
					int entryplace = int.Parse(entryplacetoken.Text.Substring(1));
					if (entryplaces.Contains(entryplace))
						throw new Exception(string.Format("{0}({1}): place conflict with '{2}'", GetRelativePath(file), entryplacetoken.Line, entryplacetoken.Text));
					entryplaces.Add(entryplace);
					SDDLParser.AssignContext assign = entry.assign();
					if (assign != null && assign.K_DELETE() != null)
						continue;
					IToken entrynametoken = entry.NAME(entry.NAME().Length - 1).Symbol;
					string entryname = entrynametoken.Text;
					if (entrynames.Contains(entryname))
						throw new Exception(string.Format("{0}({1}): name conflict with '{2}'", GetRelativePath(file), entrynametoken.Line, entryname));
					entrynames.Add(entryname);
					Entry e = new Entry {Name = entryname, Place = entryplace};
					if (entry.K_BOOLEAN() != null)
					{
						e.Type = Type.Bool;
					}
					else if (entry.K_INTEGER() != null)
					{
						e.Type = Type.Int;
					}
					else if (entry.K_NUMBER() != null)
					{
						e.Type = Type.Float;
					}
					else if (entry.K_STRING() != null)
					{
						e.Type = Type.String;
					}
					else
					{
						ITerminalNode typenode = entry.NAME(0);
						e.Type = Type.Other;
						e.TypeEx = typenode.Symbol.Text;
						if (!preload.ContainsKey(e.TypeEx) && !messages.ContainsKey(e.TypeEx))
							messages.Add(e.TypeEx, new Message(e.TypeEx) {token = typenode.Symbol});
					}
					if (assign != null && assign.K_ARRAY() != null)
					{
						e.Option = EntryOption.Array;
					}
					else if (assign != null && assign.K_TABLE() != null)
					{
						e.Option = EntryOption.Table;
					}
					else if (assign != null && assign.K_OPTION() != null)
					{
						e.Option = EntryOption.Option;
					}
					else
					{
						if (e.Type == Type.Other)
						{
							if (!preload.ContainsKey(e.TypeEx))
								message.refers.Add(messages[e.TypeEx]);
						}
						e.Option = EntryOption.Require;
						switch (e.Type)
						{
						case Type.Bool:
							e.Value = false;
							break;
						case Type.Int:
							e.Value = 0;
							break;
						case Type.Float:
							e.Value = 0.0;
							break;
						case Type.String:
							e.Value = "";
							break;
						}
						if (assign != null)
						{
							SDDLParser.BuiltinContext builtin = assign.builtin();
							if (builtin != null)
							{
								Expr expr = ParseBuiltinExpr(builtin, node =>
								{
									throw new Exception(string.Format("{0}({1}): variable '{2}' could not be found", GetRelativePath(file),
																	node.Symbol.Line, node.Symbol.Text));
								}, consts);
								Type type;
								try
								{
									type = expr.Typeof();
								}
								catch (Exception)
								{
									throw new Exception(string.Format("{0}({1}): type mismatch in the expression", GetRelativePath(file), builtin.Start.Line));
								}
								switch (type)
								{
								case Type.Bool:
									e.Value = expr.ToBoolean();
									break;
								case Type.Int:
									e.Value = expr.ToInteger();
									break;
								case Type.Float:
									e.Value = expr.ToFloat();
									break;
								case Type.String:
									e.Value = expr.ToString();
									break;
								}
							}
						}
					}
					message.entries.Add(e);
				}
				message.entries.Sort((left, right) => left.Place.CompareTo(right.Place));
			}

			private void AddMessage(Message message, LinkedList<Message> list, HashSet<string> collecting,
									HashSet<string> already)
			{
				if (!already.Contains(message.name))
				{
					if (collecting.Contains(message.name))
						throw new Exception(string.Format("{0}({1}): circular reference", GetRelativePath(file), message.token.Line));
					collecting.Add(message.name);
					foreach (var refer in message.refers)
					{
						AddMessage(refer, list, collecting, already);
					}
					collecting.Remove(message.name);
					list.AddLast(message);
					already.Add(message.name);
				}
			}

			private bool VerifyDefault(Type type, Value value)
			{
				switch (type)
				{
				case Type.Bool:
					bool b;
					if (value == null || !value.TryToBoolean(out b))
						return false;
					break;
				case Type.Int:
					int i;
					if (value == null || !value.TryToInteger(out i))
						return false;
					break;
				case Type.Float:
					double d;
					if (value == null || !value.TryToFloat(out d))
						return false;
					break;
				case Type.String:
					string s;
					if (value == null || !value.TryToString(out s))
						return false;
					break;
				default:
					if (value != null)
						return false;
					break;
				}
				return true;
			}

			public void Collect()
			{
				foreach (var message in messages.Values)
				{
					if (message.entries == null)
						throw new Exception(string.Format("{0}({1}): type '{2}' could not be found", GetRelativePath(file), message.token.Line, message.token.Text));
				}
				var values = messages.Values;
				Message[] msgs = new Message[values.Count];
				values.CopyTo(msgs, 0);
				Array.Sort(msgs, (lhs, rhs) =>
				{
					return String.Compare(lhs.name, rhs.name, StringComparison.Ordinal);
				});
				LinkedList<Message> list = new LinkedList<Message>();
				HashSet<string> already = new HashSet<string>();
				HashSet<string> collecting = new HashSet<string>();
				foreach (var message in msgs)
				{
					AddMessage(message, list, collecting, already);
				}
				foreach (var message in list)
				{
					foreach (var entry in message.entries)
					{
						if (entry.Option == EntryOption.Require)
						{
							if (!VerifyDefault(entry.Type, entry.Value))
								throw new Exception(string.Format("{0}({1}): type mismatch in default value", GetRelativePath(file), message.token.Line));
						}
					}
					
					preload.Add(message.name, message);
				}
			}
		}
		#endregion

		#region 类别名定义结构
		private class Typedef
		{
			public readonly string name;
			public readonly List<Alias> aliases;
			public readonly HashSet<int> places;
			public readonly HashSet<string> names;

			public Typedef(string name)
			{
				this.name = name;
				this.aliases = new List<Alias>();
				this.places = new HashSet<int>();
				this.names = new HashSet<string>();
			}
		}
		#endregion

		#region 类别名定义处理流程
		private class AliasListener : SDDLBaseListener
		{
			public Dictionary<string, Typedef> preload;
			private readonly string file;

			public AliasListener(string file)
			{
				this.file = file;
			}

			public override void EnterTypedef(SDDLParser.TypedefContext context)
			{
				IToken nametoken = context.NAME().Symbol;
				string name = nametoken.Text;
				if (preload.ContainsKey(name))
					throw new Exception(string.Format("{0}({1}): name conflict with '{2}'", GetRelativePath(file), nametoken.Line, nametoken.Text));
				Typedef typedef;
				if (!preload.TryGetValue(name, out typedef))
				{
					typedef = new Typedef(name);
					preload.Add(name, typedef);
				}
				SDDLParser.AliasContext[] aliases = context.alias();
				foreach (var alias in aliases)
				{
					IToken entryplacetoken = alias.PLACE().Symbol;
					int entryplace = int.Parse(entryplacetoken.Text.Substring(1));
					if (typedef.places.Contains(entryplace))
						throw new Exception(string.Format("{0}({1}): place conflict with '{2}'", GetRelativePath(file), entryplacetoken.Line, entryplacetoken.Text));
					typedef.places.Add(entryplace);
					IToken entrynametoken = alias.NAME(0).Symbol;
					string entryname = entrynametoken.Text;
					Alias a = new Alias {Name = entryname, Place = entryplace};
					bool deleted = false;
					if (alias.K_DELETE() != null)
					{
						deleted = true;
					}
					else if (alias.K_NULL() != null)
					{
						a.Type = null;
					}
					else if (alias.K_INTEGER() != null)
					{
						a.Type = Type.Int;
					}
					else if (alias.K_NUMBER() != null)
					{
						a.Type = Type.Float;
					}
					else if (alias.K_STRING() != null)
					{
						a.Type = Type.String;
					}
					else if (alias.K_BOOLEAN() != null)
					{
						a.Type = Type.Bool;
					}
					else
					{
						ITerminalNode typenode = alias.NAME(1);
						a.Type = Type.Other;
						a.TypeEx = typenode.Symbol.Text;
					}
					if (!deleted)
					{
						if (typedef.names.Contains(entryname))
							throw new Exception(string.Format("{0}({1}): name conflict with '{2}'", GetRelativePath(file), entrynametoken.Line, entryname));
						typedef.names.Add(entryname);
						typedef.aliases.Add(a);
					}
				}
			}
		}
		#endregion

		#region RPC定义结构
		private class RPC
		{
			public readonly string name;
			public readonly List<Call> calls;
			public readonly HashSet<int> places;
			public readonly HashSet<string> names;

			public RPC(string name)
			{
				this.name = name;
				this.calls = new List<Call>();
				this.places = new HashSet<int>();
				this.names = new HashSet<string>();
			}
		}
		#endregion

		#region RPC定义处理流程
		private class RPCListener : SDDLBaseListener
		{
			public Dictionary<string, RPC> preload;
			private readonly string file;

			public RPCListener(string file)
			{
				this.file = file;
			}

			public override void EnterRpc(SDDLParser.RpcContext context)
			{
				IToken nametoken = context.NAME().Symbol;
				string name = nametoken.Text;
				if (preload.ContainsKey(name))
					throw new Exception(string.Format("{0}({1}): name conflict with '{2}'", GetRelativePath(file), nametoken.Line, nametoken.Text));
				RPC rpc;
				if (!preload.TryGetValue(name, out rpc))
				{
					rpc = new RPC(name);
					preload.Add(name, rpc);
				}
				SDDLParser.CallContext[] calls = context.call();
				foreach (var call in calls)
				{
					IToken entryplacetoken = call.PLACE().Symbol;
					int entryplace = int.Parse(entryplacetoken.Text.Substring(1));
					if (rpc.places.Contains(entryplace))
						throw new Exception(string.Format("{0}({1}): place conflict with '{2}'", GetRelativePath(file), entryplacetoken.Line, entryplacetoken.Text));
					rpc.places.Add(entryplace);
					IToken entrynametoken = call.NAME(0).Symbol;
					string entryname = entrynametoken.Text;
					Call c = new Call {Name = entryname, Place = entryplace};
					bool deleted = false;
					bool returned = false;
					for (int i = 3, j = call.ChildCount; i < j; ++i)
					{
						IParseTree tree = call.GetChild(i);
						ITerminalNode node = tree as ITerminalNode;
						if (node != null)
						{
							IToken token = node.Symbol;
							switch (token.Type)
							{
							case SDDLLexer.K_DELETE:
								deleted = true;
								continue;
							case SDDLLexer.K_INTEGER:
								if (!returned)
									c.RequestType = Type.Int;
								else
									c.ResponseType = Type.Int;
								break;
							case SDDLLexer.K_NUMBER:
								if (!returned)
									c.RequestType = Type.Float;
								else
									c.ResponseType = Type.Float;
								break;
							case SDDLLexer.K_STRING:
								if (!returned)
									c.RequestType = Type.String;
								else
									c.ResponseType = Type.String;
								break;
							case SDDLLexer.K_BOOLEAN:
								if (!returned)
									c.RequestType = Type.Bool;
								else
									c.ResponseType = Type.Bool;
								break;
							case SDDLLexer.NAME:
								if (!returned)
								{
									c.RequestType = Type.Other;
									c.RequestTypeEx = token.Text;
								}
								else
								{
									c.ResponseType = Type.Other;
									c.ResponseTypeEx = token.Text;
								}
								break;
							case SDDLLexer.RETURN:
								returned = true;
								break;
							}
						}
					}
					if (!deleted)
					{
						if (rpc.names.Contains(entryname))
							throw new Exception(string.Format("{0}({1}): name conflict with '{2}'", GetRelativePath(file), entrynametoken.Line, entryname));
						rpc.names.Add(entryname);
						rpc.calls.Add(c);
					}
				}
			}
		}
		#endregion

		#region 语法错误处理
		private class ParseException : Exception
		{
			public readonly Parser recognizer;
			public readonly RecognitionException exception;

			public ParseException(Parser recognizer, RecognitionException exception)
			{
				this.recognizer = recognizer;
				this.exception = exception;
			}
		}

		private class ErrorStrategy : DefaultErrorStrategy
		{
			public override void Recover(Parser recognizer, RecognitionException e)
			{
				throw new ParseException(recognizer, e);
			}

			public override void ReportError(Parser recognizer, RecognitionException e)
			{
				throw new ParseException(recognizer, e);
			}

			public override bool InErrorRecoveryMode(Parser recognizer)
			{
				return true;
			}

			public override IToken RecoverInline(Parser recognizer)
			{
				if (recognizer.GetExpectedTokens().Contains(recognizer.CurrentToken.Type))
					return recognizer.CurrentToken;
				throw new ParseException(recognizer, new InputMismatchException(recognizer));
			}
		}
		#endregion
	}
}
