using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Fluid.Values;

namespace Fluid.Filters
{
    public static class ArrayFilters
    {
        public static FilterCollection WithArrayFilters(this FilterCollection filters)
        {
            filters.AddFilter("join", Join);
            filters.AddFilter("first", First);
            filters.AddFilter("last", Last);
            filters.AddFilter("concat", Concat);
            filters.AddFilter("map", Map);
            filters.AddFilter("reverse", Reverse);
            filters.AddFilter("size", Size);
            filters.AddFilter("sort", Sort);
            filters.AddFilter("sort_natural", SortNatural);
            filters.AddFilter("uniq", Uniq);
            filters.AddFilter("where", Where);
            filters.AddFilter("skip", Skip);
            filters.AddFilter("take", Take);
            filters.AddFilter("any", Any);
            filters.AddFilter("all", All);
            return filters;
        }

        public static ValueTask<FluidValue> Join(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var separator = arguments.At(0).ToStringValue();
            var values = input.ToStringArray();
            var joined = string.Join(separator, values);
            return new StringValue(joined);
        }

        public static ValueTask<FluidValue> First(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return input.FirstOrDefault() ?? NilValue.Instance;
        }

        public static ValueTask<FluidValue> Last(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            return input.LastOrDefault() ?? NilValue.Instance;
        }

        public static ValueTask<FluidValue> Concat(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            if (arguments.At(0).Type != FluidValues.Array)
            {
                return input;
            }

            var input1 = input.ToObjectValue() as IEnumerable<object>;
            var arr1 = arguments.At(0).ToObjectValue() as IEnumerable<object>;

            return FluidValue.Create(input1.AsQueryable().Concat(arr1.AsQueryable()), context.Options);
        }

        public static ValueTask<FluidValue> Map(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            try
            {

                var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
                var member = arguments.At(0).ToStringValue();

                var selector = ArrayFilters.GetPropertySelector(source, member);

                var d = source.Provider.CreateQuery(
                    System.Linq.Expressions.Expression.Call(typeof(Queryable), "Select",
                        new Type[] { source.ElementType, selector.Body.Type },
                        source.Expression, System.Linq.Expressions.Expression.Quote(selector)));
                return FluidValue.Create(d, context.Options);
            }
            catch (Exception ep)
            {
                System.Diagnostics.Debug.WriteLine(ep);
            }

            return NilValue.Instance;
        }

        // https://github.com/Shopify/liquid/commit/842986a9721de11e71387732be51951285225977
        public static ValueTask<FluidValue> Where(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            // First argument is the property name to match
            var member = arguments.At(0).ToStringValue();

            // Second argument is the value to match, or 'true' if none is defined
            var targetValue = arguments.At(1).Or(BooleanValue.True);

            var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
            var predicate = GetPropertyPredicate(source, member, targetValue.ToObjectValue());
            var d = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Where",
                    new Type[] { source.ElementType },
                    source.Expression, System.Linq.Expressions.Expression.Quote(predicate)));

            return FluidValue.Create(d, context.Options);
        }

        public static ValueTask<FluidValue> Reverse(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
            return FluidValue.Create(source.Reverse(), context.Options);
        }

        public static async ValueTask<FluidValue> Size(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type == FluidValues.Array)
            {
                return await ((ArrayValue)input).GetValueAsync("size", context);
            }

            if (input.Type == FluidValues.String)
            {
                return await ((StringValue)input).GetValueAsync("size", context);
            }

            return NilValue.Instance;
        }

        public static ValueTask<FluidValue> Sort(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
            if (arguments.Count > 0)
            {
                var member = arguments.At(0).ToStringValue();
                var selector = GetPropertySelector(source, member);
                var d = source.Provider.CreateQuery(
                    System.Linq.Expressions.Expression.Call(typeof(Queryable), "OrderBy",
                        new Type[] { source.ElementType, selector.Body.Type },
                        source.Expression, System.Linq.Expressions.Expression.Quote(selector)));
                return FluidValue.Create(d, context.Options);
            }
            else
            {
                return FluidValue.Create(source.OrderBy(o => o), context.Options);
            }
        }

        public static ValueTask<FluidValue> SortNatural(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
            if (arguments.Count > 0)
            {
                var member = arguments.At(0).ToStringValue();
                var selector = GetPropertySelector(source, member);
                var d = source.Provider.CreateQuery(
                    System.Linq.Expressions.Expression.Call(typeof(Queryable), "OrderBy",
                        new Type[] { source.ElementType, selector.Body.Type },
                        source.Expression, System.Linq.Expressions.Expression.Quote(selector)));
                return FluidValue.Create(d, context.Options);
            }
            else
            {
                return FluidValue.Create(source.OrderBy(o => o), context.Options);
            }
        }

        public static ValueTask<FluidValue> Uniq(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
            return FluidValue.Create(source.Distinct(), context.Options);
        }

        public static ValueTask<FluidValue> Skip(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var skip = arguments.At(0).Or(NumberValue.Zero);
            var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
            return FluidValue.Create(source.Skip((int)skip.ToNumberValue()), context.Options);
        }
        public static ValueTask<FluidValue> Take(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var take = arguments.At(0).Or(NumberValue.Zero);
            var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
            return FluidValue.Create(source.Take((int)take.ToNumberValue()), context.Options);
        }
        public static ValueTask<FluidValue> Any(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
            return FluidValue.Create(source.Any(), context.Options);
        }
        public static ValueTask<FluidValue> All(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var member = arguments.At(0).ToStringValue();
            var targetValue = arguments.At(1).Or(BooleanValue.True);

            var source = (input.ToObjectValue() as IEnumerable<object>).AsQueryable();
            var predicate = GetPropertyPredicate(source, member, targetValue.ToObjectValue());
            var d = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "All",
                    new Type[] { source.ElementType },
                    source.Expression, System.Linq.Expressions.Expression.Quote(predicate)));
            return FluidValue.Create(d, context.Options);
        }

        public static System.Linq.Expressions.LambdaExpression GetPropertySelector(this IQueryable list, string property)
        {
            var parameterExp = System.Linq.Expressions.Expression.Parameter(list.ElementType, "type");
            var fullExp = System.Linq.Expressions.Expression.Property(parameterExp, property);
            return System.Linq.Expressions.Expression.Lambda(fullExp, parameterExp);
        }
        public static System.Linq.Expressions.LambdaExpression GetPropertyPredicate(this IQueryable list, string property, object value)
        {
            var parameterExp = System.Linq.Expressions.Expression.Parameter(list.ElementType, "type");
            var fullExp = System.Linq.Expressions.Expression.Equal(System.Linq.Expressions.Expression.Property(parameterExp, property), System.Linq.Expressions.Expression.Constant(value));
            return System.Linq.Expressions.Expression.Lambda(fullExp, parameterExp);
        }
    }
}
