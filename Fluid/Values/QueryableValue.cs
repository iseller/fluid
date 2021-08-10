using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Fluid.Utils;

namespace Fluid.Values
{
    public sealed class QueryableValue : FluidValue
    {
        public static QueryableValue Create(IQueryable value, TemplateOptions option, int maxItem = 50)
        {
            return new QueryableValue(value, option, maxItem);
        }

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
                    var q = this.Values.CreateQuery(Expression.Call(
                            typeof(Queryable), "Take",
                            new Type[] { this.Values.ElementType },
                            this.Values.Expression, Expression.Constant(_maxItem)));

                    this._valueList = Activator.CreateInstance(typeof(List<>).MakeGenericType(q.ElementType), q) as IList;

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

        public override async ValueTask<FluidValue> GetValueAsync(string name, TemplateContext context)
        {
            switch (name)
            {
                case "size":
                    return NumberValue.Create(this.ToNumberValue());

                case "first":
                    return this.FirstOrDefault();

                case "last":
                    return this.LastOrDefault();
                default:
                    {
                        var accessor = context.Options.MemberAccessStrategy.GetAccessor(_value.GetType(), name);
                        if (accessor != null)
                        {
                            if (accessor is IAsyncMemberAccessor asyncAccessor)
                            {
                                return FluidValue.Create(await asyncAccessor.GetAsync(_value, name, context), context.Options);
                            }

                            return FluidValue.Create(accessor.Get(_value, name, context), context.Options);
                        }
                    }
                    break;
            }

            return NilValue.Instance;
        }

        public override async ValueTask<FluidValue> GetIndexAsync(FluidValue index, TemplateContext context)
        {
            if (index is NumberValue)
            {
                var i = (int)index.ToNumberValue();

                object val;
                IQueryable q;
                if (i < _maxItem)
                {
                    q = this.Values as IQueryable;
                }
                else
                {
                    q = _value as IQueryable;
                }

                if (i > 0)
                {
                    q = this.Values.CreateQuery(Expression.Call(
                        typeof(Queryable), "Skip",
                        new Type[] { this.Values.ElementType }, this.Values.Expression, Expression.Constant(i)));
                }

                val = q.Execute(Expression.Call(
                    typeof(Queryable), "FirstOrDefault",
                    new Type[] { q.ElementType }, q.Expression), q.ElementType);

                if (val != null)
                {
                    return FluidValue.Create(val, context.Options);
                }
            }

            var name = index.ToStringValue();
            if (!string.IsNullOrEmpty(name))
            {
                var accessor = context.Options.MemberAccessStrategy.GetAccessor(_value.GetType(), name);
                if (accessor != null)
                {
                    if (accessor is IAsyncMemberAccessor asyncAccessor)
                    {
                        return FluidValue.Create(await asyncAccessor.GetAsync(_value, name, context), context.Options);
                    }

                    return FluidValue.Create(accessor.Get(_value, name, context), context.Options);
                }
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
                this._count = _value.Provider.Execute<long>(Expression.Call(
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
            if (this._valueList != null && this._valueList.Count < this._maxItem)
            {
                return this.Values;
            }

            return this._value;
        }

        public override bool Contains(FluidValue value)
        {
            var val = value.ToObjectValue();
            if (val.GetType() == this._value.ElementType)
            {
                if (this._count.HasValue && this._count.Value < this._maxItem)
                {
                    return this.Values.Provider.Execute<bool>(Expression.Call(
                        typeof(Queryable), "Contains",
                        new Type[] { this.Values.ElementType }, this.Values.Expression, Expression.Constant(val)));
                }

                return this._value.Provider.Execute<bool>(Expression.Call(
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
            var first = this.Values.Execute(Expression.Call(
                typeof(Queryable), "FirstOrDefault",
                new Type[] { this.Values.ElementType }, this.Values.Expression), this.Values.ElementType);

            if (first != null)
            {
                return FluidValue.Create(first, _option);
            }

            return NilValue.Instance;
        }

        internal override FluidValue LastOrDefault()
        {
            object last;
            if (this._count.HasValue && this._count <= this._maxItem)
            {
                last = this.Values.Execute(Expression.Call(
                    typeof(Queryable), "LastOrDefault",
                    new Type[] { this.Values.ElementType }, this.Values.Expression), this.Values.ElementType);
            }
            else
            {
                last = this._value.Execute(Expression.Call(
                    typeof(Queryable), "LastOrDefault",
                    new Type[] { this._value.ElementType }, this._value.Expression), this._value.ElementType);
            }

            if (last != null)
            {
                return FluidValue.Create(last, _option);
            }

            return NilValue.Instance;
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
