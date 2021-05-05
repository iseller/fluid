using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class EnumerableValue : FluidValue
    {
        private readonly IEnumerable _value;
        private IList<object> _valueList;
        private readonly TemplateOptions _option;

        public IEnumerable<object> Values => this._valueList != null ? this._valueList : this._value.Cast<object>();
        public IList<object> ValueLists
        {
            get
            {
                if (this._valueList == null)
                {
                    this._valueList = this.Values.ToList();
                }

                return this._valueList;
            }
        }

        public override FluidValues Type => FluidValues.Array;

        public EnumerableValue(IEnumerable value, TemplateOptions option)
        {
            _value = value;
            _option = option;
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
                    var first = Values.FirstOrDefault();
                    if (first != null)
                    {
                        return FluidValue.Create(first, context.Options);
                    }
                    break;

                case "last":
                    var last = ValueLists.LastOrDefault();
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

            var val = this.ValueLists[i];
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
            return this.ValueLists.LongCount();
        }

        public override void WriteTo(TextWriter writer, TextEncoder encoder, CultureInfo cultureInfo)
        {
            AssertWriteToParameters(writer, encoder, cultureInfo);

            foreach (var v in this.ValueLists)
            {
                writer.Write(FluidValue.Create(v, _option).ToStringValue());
            }
        }

        public override string ToStringValue()
        {
            return String.Join("", this.Enumerate().Select(x => x.ToStringValue()));
        }

        public override object ToObjectValue()
        {
            return this._value;
        }

        public override bool Contains(FluidValue value)
        {
            var val = value.ToObjectValue();
            return this.ValueLists.Contains(val);
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
            return new(this.Enumerate());
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
            if (other is EnumerableValue otherValue)
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
