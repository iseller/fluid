using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Encodings.Web;

namespace Fluid.Values
{
    public sealed class QueryableValue : FluidValue
    {
        private readonly IQueryable _value;
        private readonly TemplateOptions _option;
        private readonly int _maxItem;
        private double? _count;
        private IList _valueList;

        public IQueryable Values => this._valueList != null ? this._valueList.AsQueryable() : this._value;
        public IList ValueLists
        {
            get
            {
                if (this._valueList == null)
                {
                    var q = this.Values.Provider.CreateQuery(
                        Expression.Call(
                            typeof(Queryable), "Take",
                            new Type[] { this.Values.ElementType },
                            this.Values.Expression, Expression.Constant(_maxItem)));

                    var r = new List<object>();
                    foreach (var t in q)
                    {
                        r.Add(t);
                    }
                    this._valueList = r;

                    if (this._valueList.Count < this._maxItem)
                    {
                        this._count = this._valueList.Count;
                    }
                }

                return this._valueList;
            }
        }

        public override FluidValues Type => FluidValues.Array;

        public QueryableValue(IQueryable value, TemplateOptions option, int maxItem = 50)
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
                    var first = this.Values.Provider.Execute(Expression.Call(
                        typeof(Queryable), "FirstOrDefault",
                        new Type[] { this.Values.ElementType }, this.Values.Expression));

                    if (first != null)
                    {
                        return FluidValue.Create(first, context.Options);
                    }
                    break;

                case "last":
                    object last;
                    if (this._count.HasValue && this._count <= this._maxItem)
                    {
                        last = this.Values.Provider.Execute(Expression.Call(
                            typeof(Queryable), "LastOrDefault",
                            new Type[] { this.Values.ElementType }, this.Values.Expression));
                    }
                    else
                    {
                        last = this._value.Provider.Execute(Expression.Call(
                            typeof(Queryable), "LastOrDefault",
                            new Type[] { this._value.ElementType }, this._value.Expression));
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

            object val;
            if (i < _maxItem)
            {
                var q = this.Values.Provider.CreateQuery(Expression.Call(
                    typeof(Queryable), "Skip",
                    new Type[] { this.Values.ElementType }, this.Values.Expression, Expression.Constant(i)));
                val = q.Provider.Execute(Expression.Call(
                    typeof(Queryable), "FirstOrDefault",
                    new Type[] { q.ElementType }, q.Expression));
            }
            else
            {
                var q = _value.Provider.CreateQuery(Expression.Call(
                    typeof(Queryable), "Skip",
                    new Type[] { _value.ElementType }, _value.Expression, Expression.Constant(i)));
                val = q.Provider.Execute(Expression.Call(
                    typeof(Queryable), "FirstOrDefault",
                    new Type[] { q.ElementType }, q.Expression));
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
                this._count = (long)_value.Provider.Execute(Expression.Call(
                    typeof(Queryable), "LongCount",
                    new Type[] { _value.ElementType }, _value.Expression));
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
            return this._value;
        }

        public override bool Contains(FluidValue value)
        {
            var val = value.ToObjectValue();
            if (val.GetType() == this._value.ElementType)
            {
                if (this._count.HasValue && this._count.Value < this._maxItem)
                {
                    return (bool)this.Values.Provider.Execute(Expression.Call(
                        typeof(Queryable), "Contains",
                        new Type[] { this.Values.ElementType }, this.Values.Expression, Expression.Constant(val)));
                }

                return (bool)this._value.Provider.Execute(Expression.Call(
                    typeof(Queryable), "Contains",
                    new Type[] { this._value.ElementType }, this._value.Expression, Expression.Constant(val)));
            }

            return false;
        }

        public override IEnumerable<FluidValue> Enumerate()
        {
            return this.ValueLists.OfType<object>().Select(o => FluidValue.Create(o, _option));
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
            if (other is QueryableValue otherValue)
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
