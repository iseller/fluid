using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class QueryableValue<T> : FluidValue
    {
        private readonly IQueryable<T> _value;
        private readonly TemplateOptions _option;
        private readonly int _maxItem;
        private double? _count;
        private IList<T> _valueList;

        public IQueryable<T> Values => this._valueList != null ? this._valueList.AsQueryable() : this._value.Take(_maxItem);
        public IList<T> ValueLists
        {
            get
            {
                if (this._valueList == null)
                {
                    this._valueList = this.Values.ToList();
                    if (this._valueList.Count < this._maxItem)
                    {
                        this._count = this._valueList.Count;
                    }
                }

                return this._valueList;
            }
        }

        public override FluidValues Type => FluidValues.Array;

        public QueryableValue(IQueryable<T> value, TemplateOptions option, int maxItem = 50)
        {
            _value = value;
            _option = option;
            _maxItem = maxItem;
        }

        public override bool Equals(FluidValue other)
        {
            return false;
        }

        protected override FluidValue GetValue(string name, TemplateContext context)
        {
            switch (name)
            {
                case "size":
                    return NumberValue.Create(this.ToNumberValue());

                case "first":
                    var first = this.Values.FirstOrDefault();
                    if (first != null)
                    {
                        return FluidValue.Create(first, context.Options);
                    }
                    break;

                case "last":
                    T last;
                    if (this._count.HasValue && this._count <= this._maxItem)
                    {
                        last = this.Values.LastOrDefault();
                    }
                    else
                    {
                        last = this._value.LastOrDefault();
                    }

                    if (last != null)
                    {
                        return FluidValue.Create(last, context.Options);
                    }
                    break;

            }

            return NilValue.Instance;
        }

        protected override FluidValue GetIndex(FluidValue index, TemplateContext context)
        {
            var i = (int)index.ToNumberValue();

            T val;
            if (i < _maxItem)
            {
                val = this.Values.Skip(i).FirstOrDefault();
            }
            else
            {
                val = _value.Skip(1).FirstOrDefault();
            }

            if (val != null)
            {
                return FluidValue.Create(val, context.Options);
            }

            return NilValue.Instance;
        }

        public override bool ToBooleanValue()
        {
            return true;
        }

        public override decimal ToNumberValue()
        {
            if (!this._count.HasValue)
            {
                this._count = this._value.LongCount();
            }
            return (decimal)this._count.GetValueOrDefault(0);
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);

            foreach (var v in this.Enumerate())
            {
                writer.Write(v.ToStringValue());
            }
        }

        public override string ToStringValue()
        {
            return String.Join("", this.Enumerate().Select(x => x.ToStringValue()));
        }

        public override object ToObjectValue()
        {
            return this.ValueLists;
        }

        public override bool Contains(FluidValue value)
        {
            var val = value.ToObjectValue();
            if (val is T)
            {
                if (this._count.HasValue && this._count.Value < this._maxItem)
                {
                    return this.Values.Contains((T)val);
                }

                return _value.Contains((T)val);
            }
            return false;
        }

        public override IEnumerable<FluidValue> Enumerate()
        {
            return this.ValueLists.Select(o => FluidValue.Create(o, _option));
        }

        internal override string[] ToStringArray()
        {
            return this.Enumerate().Select(x => x.ToStringValue()).ToArray();
        }

        internal override List<FluidValue> ToList()
        {
            return this.Enumerate().ToList();
        }

        internal override FluidValue FirstOrDefault()
        {
            return this.GetValue("first", new TemplateContext(_option));
        }

        internal override FluidValue LastOrDefault()
        {
            return this.GetValue("last", new TemplateContext(_option));
        }

        public override bool Equals(object other)
        {
            // The is operator will return false if null
            if (other is QueryableValue<T> otherValue)
            {
                return _value.Equals(otherValue._value);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }
    }
}
