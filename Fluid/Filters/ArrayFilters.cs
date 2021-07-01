using System;
using System.Collections;
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
            filters.AddFilter("intersect", Intersect);
            filters.AddFilter("union", Union);
            filters.AddFilter("concat", Concat);
            filters.AddFilter("to_array", ToArray);
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

            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            var second = (arguments.At(0).ToObjectValue() as IEnumerable).AsQueryable();

            source = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Concat", new Type[] { source.ElementType },
                source.Expression, System.Linq.Expressions.Expression.Constant(second))
            );
            return FluidValue.Create(source, context.Options);
        }

        public static ValueTask<FluidValue> Map(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            try
            {
                var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
                var member = arguments.At(0).ToStringValue();

                var selector = ArrayFilters.GetPropertySelector(source.ElementType, member);

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

            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            var predicate = GetPropertyPredicate(source.ElementType, member, targetValue.ToObjectValue());
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

            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();

            source = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Reverse", new Type[] { source.ElementType },
                source.Expression)
            );
            return FluidValue.Create(source, context.Options);
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
            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            var d = source;
            if (arguments.Count > 0)
            {
                var isBegin = true;
                foreach (var argument in arguments.Values)
                {
                    var member = arguments.At(0).ToStringValue();
                    var direction = "asc";

                    if (member.Contains(":"))
                    {
                        var ms = member.Split(':');
                        member = ms[1];
                        direction = ms[0];
                    }

                    var method = isBegin ? "OrderBy" : "ThenBy";
                    if (direction == "desc")
                    {
                        method += "Descending";
                    }

                    var selector = GetPropertySelector(source.ElementType, member);
                    d = source.Provider.CreateQuery(
                        System.Linq.Expressions.Expression.Call(typeof(Queryable), method,
                        new Type[] { source.ElementType, selector.Body.Type },
                        source.Expression, System.Linq.Expressions.Expression.Quote(selector)));
                    isBegin = false;
                }
            }
            else
            {
                var selector = GetPropertySelector(source.ElementType, "");
                d = source.Provider.CreateQuery(
                    System.Linq.Expressions.Expression.Call(typeof(Queryable), "OrderBy",
                    new Type[] { source.ElementType, selector.Body.Type },
                    source.Expression, System.Linq.Expressions.Expression.Quote(selector)));
            }

            return FluidValue.Create(d, context.Options);
        }

        public static ValueTask<FluidValue> SortNatural(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            return Sort(input, arguments, context);
        }

        public static ValueTask<FluidValue> Uniq(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            source = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Distinct", new Type[] { source.ElementType }, source.Expression)
            );
            return FluidValue.Create(source, context.Options);
        }

        public static ValueTask<FluidValue> Skip(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var skip = arguments.At(0).Or(NumberValue.Zero);
            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            source = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Skip", new Type[] { source.ElementType },
                source.Expression, System.Linq.Expressions.Expression.Constant((int)skip.ToNumberValue()))
            );
            return FluidValue.Create(source, context.Options);
        }
        public static ValueTask<FluidValue> Take(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var take = arguments.At(0).Or(NumberValue.Zero);
            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();

            source = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Take", new Type[] { source.ElementType },
                source.Expression, System.Linq.Expressions.Expression.Constant((int)take.ToNumberValue()))
            );
            return FluidValue.Create(source, context.Options);
        }
        public static ValueTask<FluidValue> Any(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            source = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Any", new Type[] { source.ElementType },
                source.Expression)
            );
            return FluidValue.Create(source, context.Options);
        }
        public static ValueTask<FluidValue> All(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var member = arguments.At(0).ToStringValue();
            var targetValue = arguments.At(1).Or(BooleanValue.True);

            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            var predicate = GetPropertyPredicate(source.ElementType, member, targetValue.ToObjectValue());
            var d = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "All",
                    new Type[] { source.ElementType },
                    source.Expression, System.Linq.Expressions.Expression.Quote(predicate)));
            return FluidValue.Create(d, context.Options);
        }
        public static ValueTask<FluidValue> Intersect(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var secondInput = arguments.At(0);
            if (secondInput.Type != FluidValues.Array)
            {
                return input;
            }


            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            var second = (secondInput.ToObjectValue() as IEnumerable).AsQueryable();

            source = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Intersect", new Type[] { source.ElementType },
                source.Expression, System.Linq.Expressions.Expression.Constant(second))
            );
            return FluidValue.Create(source, context.Options);
        }
        public static ValueTask<FluidValue> Union(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var secondInput = arguments.At(0);
            if (secondInput.Type != FluidValues.Array)
            {
                return input;
            }


            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            var second = (secondInput.ToObjectValue() as IEnumerable).AsQueryable();

            source = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Union", new Type[] { source.ElementType },
                source.Expression, System.Linq.Expressions.Expression.Constant(second))
            );
            return FluidValue.Create(source, context.Options);
        }
        public static ValueTask<FluidValue> Except(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            var secondInput = arguments.At(0);
            if (secondInput.Type != FluidValues.Array)
            {
                return input;
            }


            var source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            var second = (secondInput.ToObjectValue() as IEnumerable).AsQueryable();

            source = source.Provider.CreateQuery(
                System.Linq.Expressions.Expression.Call(typeof(Queryable), "Except", new Type[] { source.ElementType },
                source.Expression, System.Linq.Expressions.Expression.Constant(second))
            );
            return FluidValue.Create(source, context.Options);
        }
        public static ValueTask<FluidValue> ToArray(FluidValue input, FilterArguments arguments, TemplateContext context)
        {
            if (input.Type != FluidValues.Array)
            {
                return input;
            }

            if (input is QueryableValue)
            {
                return FluidValue.Create((input as QueryableValue).ValueLists, context.Options);
            }

            IQueryable source = (input.ToObjectValue() as IEnumerable).AsQueryable();
            var value = Activator.CreateInstance(typeof(List<>).MakeGenericType(source.ElementType), source);
            return FluidValue.Create(value, context.Options);
        }

        public static System.Linq.Expressions.LambdaExpression GetPropertySelector(Type type, string property)
        {
            var parameterExp = System.Linq.Expressions.Expression.Parameter(type, "type");
            System.Linq.Expressions.Expression fullExp = parameterExp;

            do
            {
                var index = property.IndexOf(".");
                string prop;
                if (index < 0)
                {
                    prop = property;
                    property = null;
                }
                else
                {
                    prop = property.Substring(0, index);
                    property = property.Substring(index + 1);
                }

                if (!string.IsNullOrEmpty(prop))
                {
                    var propertyInfo = type.GetProperty($"z_{prop}") ?? type.GetProperty(prop);
                    fullExp = System.Linq.Expressions.Expression.Property(fullExp, propertyInfo.Name);
                }
            } while (!string.IsNullOrEmpty(property));

            return System.Linq.Expressions.Expression.Lambda(fullExp, parameterExp);
        }
        public static System.Linq.Expressions.LambdaExpression GetPropertyPredicate(Type type, string property, object value)
        {
            var parameterExp = System.Linq.Expressions.Expression.Parameter(type, "type");
            System.Linq.Expressions.Expression fullExp = parameterExp;

            do
            {
                var index = property.IndexOf(".");
                string prop;
                if (index < 0)
                {
                    prop = property;
                    property = null;
                }
                else
                {
                    prop = property.Substring(0, index);
                    property = property.Substring(index + 1);
                }

                System.Reflection.PropertyInfo propertyInfo = null;
                if (!string.IsNullOrEmpty(prop))
                {
                    propertyInfo = type.GetProperty($"z_{prop}") ?? type.GetProperty(prop);
                    fullExp = System.Linq.Expressions.Expression.Property(fullExp, propertyInfo.Name);
                }
                else
                {
                    propertyInfo = type.GetProperty("value");
                    if (propertyInfo != null)
                    {
                        prop = propertyInfo.Name;
                        fullExp = System.Linq.Expressions.Expression.Property(fullExp, propertyInfo.Name);
                    }
                }

                if (propertyInfo != null && propertyInfo.PropertyType != typeof(string) && typeof(System.Collections.IEnumerable).IsAssignableFrom(propertyInfo.PropertyType))
                {
                    var itemType = propertyInfo.PropertyType.GenericTypeArguments.FirstOrDefault();
                    var predicate = GetPropertyPredicate(itemType, property, value);
                    fullExp = System.Linq.Expressions.Expression.Call(typeof(Enumerable), "Any", new[] { itemType }, fullExp, predicate);
                    break;
                }
                else if (string.IsNullOrEmpty(property))
                {
                    if (value is System.Collections.IEnumerable && !(value is string))
                    {
                        var itemType = propertyInfo.PropertyType;
                        if (value is IQueryable)
                        {
                            var vq = value as IQueryable;
                            vq = vq.Provider.CreateQuery(
                                System.Linq.Expressions.Expression.Call(
                                    typeof(Queryable), "Take",
                                    new Type[] { vq.ElementType },
                                    vq.Expression, System.Linq.Expressions.Expression.Constant(50)));

                            value = Activator.CreateInstance(typeof(List<>).MakeGenericType(itemType), vq);
                        }
                        System.Linq.Expressions.Expression valueExp = System.Linq.Expressions.Expression.Constant(value);
                        fullExp = System.Linq.Expressions.Expression.Call(typeof(Enumerable), "Contains", new[] { itemType }, valueExp, fullExp);
                    }
                    else
                    {
                        fullExp = System.Linq.Expressions.Expression.Equal(fullExp, System.Linq.Expressions.Expression.Constant(value));
                    }
                }
            } while (!string.IsNullOrEmpty(property));

            return System.Linq.Expressions.Expression.Lambda(fullExp, parameterExp);
        }
    }
}
