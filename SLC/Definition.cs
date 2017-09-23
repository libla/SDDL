using System;
using System.IO;
using System.Text;

namespace SDDL
{
	public enum Type
	{
		Bool,
		Int,
		Float,
		String,
		Other,
	}

	internal static class Consts
	{
		public const double EPSILON = 2.2204460492503131e-16;
	}

	public abstract class Value
	{
		public virtual bool TryToBoolean(out bool b)
		{
			b = default(bool);
			return false;
		}

		public virtual bool TryToInteger(out int i)
		{
			i = default(int);
			return false;
		}

		public virtual bool TryToFloat(out double d)
		{
			int i;
			if (TryToInteger(out i))
			{
				d = i;
				return true;
			}
			d = default(double);
			return false;
		}

		public virtual bool TryToString(out string s)
		{
			s = default(string);
			return false;
		}

		public bool ToBoolean()
		{
			bool b;
			if (TryToBoolean(out b))
				return b;
			throw new InvalidCastException();
		}

		public int ToInteger()
		{
			int i;
			if (TryToInteger(out i))
				return i;
			throw new InvalidCastException();
		}

		public double ToFloat()
		{
			double d;
			if (TryToFloat(out d))
				return d;
			throw new InvalidCastException();
		}

		public new string ToString()
		{
			string s;
			if (TryToString(out s))
				return s;
			throw new InvalidCastException();
		}

		public abstract Type Typeof();

		public static implicit operator Value(bool b)
		{
			return new BoolValue(b);
		}

		public static implicit operator Value(int i)
		{
			return new IntValue(i);
		}

		public static implicit operator Value(double d)
		{
			return new FloatValue(d);
		}

		public static implicit operator Value(string s)
		{
			return new StringValue(s);
		}

		public static explicit operator bool(Value value)
		{
			return value.ToBoolean();
		}

		public static explicit operator int(Value value)
		{
			return value.ToInteger();
		}

		public static explicit operator double(Value value)
		{
			return value.ToFloat();
		}

		public static explicit operator string(Value value)
		{
			return value.ToString();
		}

		#region 具体实现类型
		private class BoolValue : Value
		{
			private readonly bool value;

			public BoolValue(bool b)
			{
				value = b;
			}

			public override bool TryToBoolean(out bool b)
			{
				b = value;
				return true;
			}

			public override Type Typeof()
			{
				return Type.Bool;
			}
		}

		private class IntValue : Value
		{
			private readonly int value;

			public IntValue(int i)
			{
				value = i;
			}

			public override bool TryToInteger(out int i)
			{
				i = value;
				return true;
			}

			public override Type Typeof()
			{
				return Type.Int;
			}
		}

		private class FloatValue : Value
		{
			private readonly double value;

			public FloatValue(double d)
			{
				value = d;
			}

			public override bool TryToInteger(out int i)
			{
				i = (int)(value + 0.5);
				return Math.Abs(value - i) <= Consts.EPSILON;
			}

			public override bool TryToFloat(out double d)
			{
				d = value;
				return true;
			}

			public override Type Typeof()
			{
				return Type.Float;
			}
		}

		private class StringValue : Value
		{
			private readonly string value;

			public StringValue(string s)
			{
				value = s;
			}

			public override bool TryToString(out string s)
			{
				s = value;
				return true;
			}

			public override Type Typeof()
			{
				return Type.String;
			}
		}
		#endregion
	}

	public enum EntryOption
	{
		Require,
		Option,
		Array,
		Table,
	}

	public struct Entry
	{
		public string Name;
		public Type Type;
		public string TypeEx;
		public EntryOption Option;
		public int Place;
		public Value Value;
	}

	public struct Alias
	{
		public string Name;
		public int Place;
		public Type? Type;
		public string TypeEx;
	}

	public struct Call
	{
		public string Name;
		public int Place;
		public Type? RequestType;
		public string RequestTypeEx;
		public Type? ResponseType;
		public string ResponseTypeEx;
	}

	public interface Target
	{
		Encoding Encoding { get; }
		string NewLine { get; }
		void Prepare(StreamWriter writer);
		void Flush(StreamWriter writer);
		void Value(StreamWriter writer, string name, bool b);
		void Value(StreamWriter writer, string name, int i);
		void Value(StreamWriter writer, string name, double d);
		void Value(StreamWriter writer, string name, string s);

		void Message(StreamWriter writer, string name, params Entry[] entries);
		void Typedef(StreamWriter writer, string name, params Alias[] aliases);
		void RPC(StreamWriter writer, string name, params Call[] calls);
	}

	internal abstract class Expr
	{
		public virtual bool TryToBoolean(out bool b)
		{
			b = default(bool);
			return false;
		}

		public virtual bool TryToInteger(out int i)
		{
			i = default(int);
			return false;
		}

		public virtual bool TryToFloat(out double d)
		{
			int i;
			if (TryToInteger(out i))
			{
				d = i;
				return true;
			}
			d = default(double);
			return false;
		}

		public virtual bool TryToString(out string s)
		{
			s = default(string);
			return false;
		}

		public bool ToBoolean()
		{
			bool b;
			if (TryToBoolean(out b))
				return b;
			throw new InvalidCastException();
		}

		public int ToInteger()
		{
			int i;
			if (TryToInteger(out i))
				return i;
			throw new InvalidCastException();
		}

		public double ToFloat()
		{
			double d;
			if (TryToFloat(out d))
				return d;
			throw new InvalidCastException();
		}

		public new string ToString()
		{
			string s;
			if (TryToString(out s))
				return s;
			throw new InvalidCastException();
		}

		public abstract Type Typeof();

		public static implicit operator Expr(bool b)
		{
			return new ValueExpr(b);
		}

		public static implicit operator Expr(int i)
		{
			return new ValueExpr(i);
		}

		public static implicit operator Expr(double d)
		{
			return new ValueExpr(d);
		}

		public static implicit operator Expr(string s)
		{
			return new ValueExpr(s);
		}

		public static implicit operator Expr(Value v)
		{
			return new ValueExpr(v);
		}

		public static explicit operator bool(Expr value)
		{
			return value.ToBoolean();
		}

		public static explicit operator int(Expr value)
		{
			return value.ToInteger();
		}

		public static explicit operator double(Expr value)
		{
			return value.ToFloat();
		}

		public static explicit operator string(Expr value)
		{
			return value.ToString();
		}

		public Expr Concat(Expr other)
		{
			return new ConcatExpr(this, other);
		}

		public Expr Equal(Expr other)
		{
			return new EquExpr(this, other);
		}

		public Expr NotEqual(Expr other)
		{
			return new NeqExpr(this, other);
		}

		public static Expr operator +(Expr left, Expr right)
		{
			return new AddExpr(left, right);
		}

		public static Expr operator -(Expr left, Expr right)
		{
			return new SubExpr(left, right);
		}

		public static Expr operator *(Expr left, Expr right)
		{
			return new MulExpr(left, right);
		}

		public static Expr operator /(Expr left, Expr right)
		{
			return new DivExpr(left, right);
		}

		public static Expr operator %(Expr left, Expr right)
		{
			return new ModExpr(left, right);
		}

		public static Expr operator ^(Expr left, Expr right)
		{
			return new PowExpr(left, right);
		}

		public static Expr operator &(Expr left, Expr right)
		{
			return new AndExpr(left, right);
		}

		public static Expr operator |(Expr left, Expr right)
		{
			return new OrExpr(left, right);
		}

		public static Expr operator !(Expr expr)
		{
			return new NotExpr(expr);
		}

		public static Expr operator <(Expr left, Expr right)
		{
			return new LtExpr(left, right);
		}

		public static Expr operator >(Expr left, Expr right)
		{
			return new GtExpr(left, right);
		}

		public static Expr operator <=(Expr left, Expr right)
		{
			return new LeExpr(left, right);
		}

		public static Expr operator >=(Expr left, Expr right)
		{
			return new GeExpr(left, right);
		}

		private class ValueExpr : Expr
		{
			private readonly Value value;

			public ValueExpr(Value value)
			{
				this.value = value;
			}

			public override bool TryToBoolean(out bool b)
			{
				return value.TryToBoolean(out b);
			}

			public override bool TryToInteger(out int i)
			{
				return value.TryToInteger(out i);
			}

			public override bool TryToFloat(out double d)
			{
				return value.TryToFloat(out d);
			}

			public override bool TryToString(out string s)
			{
				return value.TryToString(out s);
			}

			public override Type Typeof()
			{
				return value.Typeof();
			}
		}

		private abstract class OpExpr : Expr
		{
			private readonly Expr left;
			private readonly Expr right;

			protected OpExpr(Expr left, Expr right)
			{
				this.left = left;
				this.right = right;
			}

			public override bool TryToInteger(out int i)
			{
				int li, ri;
				if (left.TryToInteger(out li) && right.TryToInteger(out ri))
				{
					i = Op(li, ri);
					return true;
				}
				i = default(int);
				return false;
			}

			public override bool TryToFloat(out double d)
			{
				double ld, rd;
				if (left.TryToFloat(out ld) && right.TryToFloat(out rd))
				{
					d = Op(ld, rd);
					return true;
				}
				d = default(double);
				return false;
			}

			public override Type Typeof()
			{
				switch (left.Typeof())
				{
				case Type.Int:
					switch (right.Typeof())
					{
					case Type.Int:
						return Type.Int;
					case Type.Float:
						return Type.Float;
					}
					break;
				case Type.Float:
					switch (right.Typeof())
					{
					case Type.Int:
					case Type.Float:
						return Type.Float;
					}
					break;
				}
				throw new InvalidCastException();
			}

			protected abstract int Op(int left, int right);
			protected abstract double Op(double left, double right);
		}

		private abstract class EqExpr : Expr
		{
			private readonly Expr left;
			private readonly Expr right;

			protected EqExpr(Expr left, Expr right)
			{
				this.left = left;
				this.right = right;
			}

			public override bool TryToBoolean(out bool b)
			{
				bool lb, rb;
				if (left.TryToBoolean(out lb) && right.TryToBoolean(out rb))
				{
					b = Eq(lb, rb);
					return true;
				}
				int li, ri;
				if (left.TryToInteger(out li) && right.TryToInteger(out ri))
				{
					b = Eq(li, ri);
					return true;
				}
				double ld, rd;
				if (left.TryToFloat(out ld) && right.TryToFloat(out rd))
				{
					b = Eq(ld, rd);
					return true;
				}
				string ls, rs;
				if (left.TryToString(out ls) && right.TryToString(out rs))
				{
					b = Eq(ls, rs);
					return true;
				}
				b = default(bool);
				return false;
			}

			public override Type Typeof()
			{
				Type type = left.Typeof();
				switch (type)
				{
				case Type.Int:
					switch (right.Typeof())
					{
					case Type.Int:
					case Type.Float:
						return Type.Bool;
					}
					break;
				case Type.Float:
					switch (right.Typeof())
					{
					case Type.Int:
					case Type.Float:
						return Type.Bool;
					}
					break;
				case Type.Bool:
				case Type.String:
					if (right.Typeof() == type)
						return Type.Bool;
					break;
				}
				throw new InvalidCastException();
			}

			protected abstract bool Eq(bool left, bool right);
			protected abstract bool Eq(int left, int right);
			protected abstract bool Eq(double left, double right);
			protected abstract bool Eq(string left, string right);
		}

		private abstract class CmpExpr : Expr
		{
			private readonly Expr left;
			private readonly Expr right;

			protected CmpExpr(Expr left, Expr right)
			{
				this.left = left;
				this.right = right;
			}

			public override bool TryToBoolean(out bool b)
			{
				int li, ri;
				if (left.TryToInteger(out li) && right.TryToInteger(out ri))
				{
					b = Cmp(li, ri);
					return true;
				}
				double ld, rd;
				if (left.TryToFloat(out ld) && right.TryToFloat(out rd))
				{
					b = Cmp(ld, rd);
					return true;
				}
				b = default(bool);
				return false;
			}

			public override Type Typeof()
			{
				Type type = left.Typeof();
				switch (type)
				{
				case Type.Int:
					switch (right.Typeof())
					{
					case Type.Int:
					case Type.Float:
						return Type.Bool;
					}
					break;
				case Type.Float:
					switch (right.Typeof())
					{
					case Type.Int:
					case Type.Float:
						return Type.Bool;
					}
					break;
				}
				throw new InvalidCastException();
			}

			protected abstract bool Cmp(int left, int right);
			protected abstract bool Cmp(double left, double right);
		}

		private class AddExpr : OpExpr
		{
			public AddExpr(Expr left, Expr right) : base(left, right) { }
			protected override int Op(int left, int right)
			{
				return left + right;
			}

			protected override double Op(double left, double right)
			{
				return left + right;
			}
		}

		private class SubExpr : OpExpr
		{
			public SubExpr(Expr left, Expr right) : base(left, right) { }
			protected override int Op(int left, int right)
			{
				return left - right;
			}

			protected override double Op(double left, double right)
			{
				return left - right;
			}
		}

		private class MulExpr : OpExpr
		{
			public MulExpr(Expr left, Expr right) : base(left, right) { }
			protected override int Op(int left, int right)
			{
				return left * right;
			}

			protected override double Op(double left, double right)
			{
				return left * right;
			}
		}

		private class DivExpr : OpExpr
		{
			public DivExpr(Expr left, Expr right) : base(left, right) { }
			protected override int Op(int left, int right)
			{
				return left / right;
			}

			protected override double Op(double left, double right)
			{
				return left / right;
			}
		}

		private class ModExpr : OpExpr
		{
			public ModExpr(Expr left, Expr right) : base(left, right) { }
			protected override int Op(int left, int right)
			{
				return left % right;
			}

			protected override double Op(double left, double right)
			{
				return left % right;
			}
		}

		private class PowExpr : OpExpr
		{
			public PowExpr(Expr left, Expr right) : base(left, right) { }
			protected override int Op(int left, int right)
			{
				return (int)Math.Pow(left, right);
			}

			protected override double Op(double left, double right)
			{
				return Math.Pow(left, right);
			}
		}

		private class ConcatExpr : Expr
		{
			private readonly Expr left;
			private readonly Expr right;

			public ConcatExpr(Expr left, Expr right)
			{
				this.left = left;
				this.right = right;
			}

			public override bool TryToString(out string s)
			{
				string ls, rs;
				if (left.TryToString(out ls) && right.TryToString(out rs))
				{
					s = ls + rs;
					return true;
				}
				s = default(string);
				return false;
			}

			public override Type Typeof()
			{
				if (left.Typeof() == Type.String && right.Typeof() == Type.String)
					return Type.String;
				throw new InvalidCastException();
			}
		}

		private class AndExpr : Expr
		{
			private readonly Expr left;
			private readonly Expr right;

			public AndExpr(Expr left, Expr right)
			{
				this.left = left;
				this.right = right;
			}

			public override bool TryToBoolean(out bool b)
			{
				bool ls, rs;
				if (left.TryToBoolean(out ls) && right.TryToBoolean(out rs))
				{
					b = ls && rs;
					return true;
				}
				b = default(bool);
				return false;
			}

			public override Type Typeof()
			{
				if (left.Typeof() == Type.Bool && right.Typeof() == Type.Bool)
					return Type.Bool;
				throw new InvalidCastException();
			}
		}

		private class OrExpr : Expr
		{
			private readonly Expr left;
			private readonly Expr right;

			public OrExpr(Expr left, Expr right)
			{
				this.left = left;
				this.right = right;
			}

			public override bool TryToBoolean(out bool b)
			{
				bool ls, rs;
				if (left.TryToBoolean(out ls) && right.TryToBoolean(out rs))
				{
					b = ls || rs;
					return true;
				}
				b = default(bool);
				return false;
			}

			public override Type Typeof()
			{
				if (left.Typeof() == Type.Bool && right.Typeof() == Type.Bool)
					return Type.Bool;
				throw new InvalidCastException();
			}
		}

		private class NotExpr : Expr
		{
			private readonly Expr expr;

			public NotExpr(Expr expr)
			{
				this.expr = expr;
			}

			public override bool TryToBoolean(out bool b)
			{
				if (expr.TryToBoolean(out b))
				{
					b = !b;
					return true;
				}
				b = default(bool);
				return false;
			}

			public override Type Typeof()
			{
				if (expr.Typeof() == Type.Bool)
					return Type.Bool;
				throw new InvalidCastException();
			}
		}

		private class EquExpr : EqExpr
		{
			public EquExpr(Expr left, Expr right) : base(left, right) { }

			protected override bool Eq(bool left, bool right)
			{
				return left == right;
			}

			protected override bool Eq(int left, int right)
			{
				return left == right;
			}

			protected override bool Eq(double left, double right)
			{
				return Math.Abs(left - right) < Consts.EPSILON;
			}

			protected override bool Eq(string left, string right)
			{
				return left == right;
			}
		}

		private class NeqExpr : EqExpr
		{
			public NeqExpr(Expr left, Expr right) : base(left, right) { }

			protected override bool Eq(bool left, bool right)
			{
				return left != right;
			}

			protected override bool Eq(int left, int right)
			{
				return left != right;
			}

			protected override bool Eq(double left, double right)
			{
				return Math.Abs(left - right) >= Consts.EPSILON;
			}

			protected override bool Eq(string left, string right)
			{
				return left != right;
			}
		}

		private class LtExpr : CmpExpr
		{
			public LtExpr(Expr left, Expr right) : base(left, right) { }

			protected override bool Cmp(int left, int right)
			{
				return left < right;
			}

			protected override bool Cmp(double left, double right)
			{
				return right - left >= Consts.EPSILON;
			}
		}

		private class LeExpr : CmpExpr
		{
			public LeExpr(Expr left, Expr right) : base(left, right) { }

			protected override bool Cmp(int left, int right)
			{
				return left <= right;
			}

			protected override bool Cmp(double left, double right)
			{
				return left - right < Consts.EPSILON;
			}
		}

		private class GtExpr : CmpExpr
		{
			public GtExpr(Expr left, Expr right) : base(left, right) { }

			protected override bool Cmp(int left, int right)
			{
				return left > right;
			}

			protected override bool Cmp(double left, double right)
			{
				return left - right >= Consts.EPSILON;
			}
		}

		private class GeExpr : CmpExpr
		{
			public GeExpr(Expr left, Expr right) : base(left, right) { }

			protected override bool Cmp(int left, int right)
			{
				return left >= right;
			}

			protected override bool Cmp(double left, double right)
			{
				return right - left < Consts.EPSILON;
			}
		}
	}
}
