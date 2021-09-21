using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Fluid.Utils
{
    internal static class IQueryableExtensions
    {
        public static IQueryable CreateQuery(this IQueryable q, Expression exp, Type resultType = null)
        {
            MethodInfo createQueryMethod = null;
            if (!(q.Provider is EnumerableQuery))
            {
                var providerType = q.Provider.GetType();
                createQueryMethod = providerType.GetMethods()
                    .FirstOrDefault(o => o.Name == "CreateQuery" && o.IsGenericMethodDefinition);
            }

            if (createQueryMethod != null)
            {
                if (resultType == null)
                {
                    resultType = q.ElementType;
                }

                return createQueryMethod.MakeGenericMethod(resultType)
                    .Invoke(q.Provider, new object[] { exp }) as IQueryable;
            }
            else
            {
                return q.Provider.CreateQuery(exp);
            }
        }
        public static object Execute(this IQueryable q, Expression exp, Type resultType)
        {
            MethodInfo createQueryMethod = null;
            if (!(q.Provider is EnumerableQuery) && !(q.Provider is EnumerableQuery))
            {
                var providerType = q.Provider.GetType();
                createQueryMethod = providerType.GetMethods()
                    .FirstOrDefault(o => o.Name == "Execute" && o.IsGenericMethodDefinition);
            }

            if (createQueryMethod != null)
            {
                return createQueryMethod.MakeGenericMethod(resultType)
                    .Invoke(q.Provider, new object[] { exp });
            }
            else
            {
                return q.Provider.Execute(exp);
            }
        }
    }
}