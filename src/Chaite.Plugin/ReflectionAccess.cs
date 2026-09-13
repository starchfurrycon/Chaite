using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Chaite.Plugin
{
    internal static class ReflectionAccess
    {
        public static FieldInfo Field(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
            }
            throw new MissingFieldException(type.FullName, name);
        }

        public static Func<object, T> Getter<T>(Type type, string name)
        {
            var field = Field(type, name);
            var source = Expression.Parameter(typeof(object), "source");
            var body = Expression.Field(Expression.Convert(source, field.DeclaringType), field);
            return Expression.Lambda<Func<object, T>>(Expression.Convert(body, typeof(T)), source).Compile();
        }

        public static Action<object, T> Setter<T>(Type type, string name)
        {
            var field = Field(type, name);
            var source = Expression.Parameter(typeof(object), "source");
            var value = Expression.Parameter(typeof(T), "value");
            var target = Expression.Field(Expression.Convert(source, field.DeclaringType), field);
            return Expression.Lambda<Action<object, T>>(Expression.Assign(target,
                Expression.Convert(value, field.FieldType)), source, value).Compile();
        }

        public static Action<T> StaticSetter<T>(Type type, string name)
        {
            var field = Field(type, name);
            var value = Expression.Parameter(typeof(T), "value");
            return Expression.Lambda<Action<T>>(Expression.Assign(Expression.Field(null, field),
                Expression.Convert(value, field.FieldType)), value).Compile();
        }

        public static Action<object, T> PropertySetter<T>(Type type, string name)
        {
            var property = Property(type, name);
            var source = Expression.Parameter(typeof(object), "source");
            var value = Expression.Parameter(typeof(T), "value");
            return Expression.Lambda<Action<object, T>>(Expression.Assign(
                Expression.Property(Expression.Convert(source, property.DeclaringType), property),
                Expression.Convert(value, property.PropertyType)), source, value).Compile();
        }

        // Call on the actual value-type field by address. Boxing with GetValue
        // would silently mutate a copy and leave vanilla's selection unchanged.
        public static Action<object, T> StructMethodSetter<T>(Type type, string fieldName, string methodName)
        {
            var field = Field(type, fieldName);
            if (!field.FieldType.IsValueType || field.IsStatic || field.IsInitOnly)
                throw new ArgumentException("Expected a writable instance value-type field.", nameof(fieldName));
            var method = field.FieldType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(T) }, null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(field.FieldType.FullName, methodName);
            var source = Expression.Parameter(typeof(object), "source");
            var value = Expression.Parameter(typeof(T), "value");
            var target = Expression.Field(Expression.Convert(source, field.DeclaringType), field);
            return Expression.Lambda<Action<object, T>>(Expression.Call(target, method, value), source, value).Compile();
        }

        public static Func<float> StaticVectorComponentGetter(Type type, string vectorFieldName, string component)
        {
            var vector = Field(type, vectorFieldName);
            var member = Field(vector.FieldType, component);
            return Expression.Lambda<Func<float>>(Expression.Field(Expression.Field(null, vector), member)).Compile();
        }

        public static Func<Array, int, int, object> ArrayElementGetter2D(Type arrayType)
        {
            if (!arrayType.IsArray || arrayType.GetArrayRank() != 2)
                throw new ArgumentException("Expected a rectangular two-dimensional tile array.", nameof(arrayType));
            var source = Expression.Parameter(typeof(Array), "source");
            var x = Expression.Parameter(typeof(int), "x");
            var y = Expression.Parameter(typeof(int), "y");
            return Expression.Lambda<Func<Array, int, int, object>>(Expression.Convert(
                Expression.ArrayAccess(Expression.Convert(source, arrayType), x, y), typeof(object)), source, x, y).Compile();
        }

        public static Func<object, float> VectorComponentGetter(Type type, string vectorFieldName, string component)
        {
            var vectorField = Field(type, vectorFieldName);
            var componentField = vectorField.FieldType.GetField(component, BindingFlags.Public | BindingFlags.Instance);
            if (componentField == null)
                throw new MissingFieldException(vectorField.FieldType.FullName, component);
            var source = Expression.Parameter(typeof(object), "source");
            var vector = Expression.Field(Expression.Convert(source, vectorField.DeclaringType), vectorField);
            var body = Expression.Field(vector, componentField);
            return Expression.Lambda<Func<object, float>>(body, source).Compile();
        }

        /// <summary>
        /// Compiled component access for an instance vector array (for example
        /// Projectile.oldPos). Unlike Array.GetValue this performs no boxing or
        /// per-read reflection work on the hot capture path.
        /// </summary>
        public static Func<object, int, float> VectorArrayComponentGetter(
            Type type, string arrayFieldName, string component)
        {
            var arrayField = Field(type, arrayFieldName);
            if (!arrayField.FieldType.IsArray)
                throw new ArgumentException("Expected a vector array.", nameof(arrayFieldName));
            var elementType = arrayField.FieldType.GetElementType();
            var componentField = elementType.GetField(component,
                BindingFlags.Public | BindingFlags.Instance);
            if (componentField == null)
                throw new MissingFieldException(elementType.FullName, component);
            var source = Expression.Parameter(typeof(object), "source");
            var index = Expression.Parameter(typeof(int), "index");
            var array = Expression.Field(Expression.Convert(source,
                arrayField.DeclaringType), arrayField);
            var element = Expression.ArrayIndex(array, index);
            var body = Expression.Field(element, componentField);
            return Expression.Lambda<Func<object, int, float>>(body, source,
                index).Compile();
        }

        public static Func<object, int> ArrayLengthGetter(Type type,
            string arrayFieldName)
        {
            var arrayField = Field(type, arrayFieldName);
            if (!arrayField.FieldType.IsArray)
                throw new ArgumentException("Expected an array field.", nameof(arrayFieldName));
            var source = Expression.Parameter(typeof(object), "source");
            var array = Expression.Field(Expression.Convert(source,
                arrayField.DeclaringType), arrayField);
            var body = Expression.ArrayLength(array);
            return Expression.Lambda<Func<object, int>>(body, source).Compile();
        }

        public static Func<T> StaticGetter<T>(Type type, string name)
        {
            var field = Field(type, name);
            var body = Expression.Field(null, field);
            return Expression.Lambda<Func<T>>(Expression.Convert(body, typeof(T))).Compile();
        }

        public static PropertyInfo Property(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var property = current.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property;
            }
            throw new MissingMemberException(type.FullName, name);
        }

        public static Func<object, T> PropertyGetter<T>(Type type, string name)
        {
            var property = Property(type, name);
            var source = Expression.Parameter(typeof(object), "source");
            var body = Expression.Property(Expression.Convert(source, property.DeclaringType), property);
            return Expression.Lambda<Func<object, T>>(Expression.Convert(body, typeof(T)), source).Compile();
        }

        public static Func<T> StaticPropertyGetter<T>(Type type, string name)
        {
            var property = Property(type, name);
            var body = Expression.Property(null, property);
            return Expression.Lambda<Func<T>>(Expression.Convert(body, typeof(T))).Compile();
        }

        public static Func<object, T> MethodGetter<T>(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, Type.EmptyTypes, null);
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            var source = Expression.Parameter(typeof(object), "source");
            var body = Expression.Call(Expression.Convert(source, method.DeclaringType), method);
            return Expression.Lambda<Func<object, T>>(Expression.Convert(body, typeof(T)), source).Compile();
        }

        public static Func<object, object, T> MethodGetterWithArgument<T>(Type type, string name, Type argumentType)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { argumentType }, null);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            var source = Expression.Parameter(typeof(object), "source");
            var argument = Expression.Parameter(typeof(object), "argument");
            var call = Expression.Call(Expression.Convert(source, method.DeclaringType), method, Expression.Convert(argument, argumentType));
            return Expression.Lambda<Func<object, object, T>>(Expression.Convert(call, typeof(T)), source, argument).Compile();
        }

        public static Func<object, int, T> MethodGetterWithIntArgument<T>(Type type, string name)
        {
            var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                null, new[] { typeof(int) }, null);
            if (method == null) throw new MissingMethodException(type.FullName, name);
            var source = Expression.Parameter(typeof(object), "source");
            var argument = Expression.Parameter(typeof(int), "argument");
            var call = Expression.Call(Expression.Convert(source, method.DeclaringType), method, argument);
            return Expression.Lambda<Func<object, int, T>>(Expression.Convert(call, typeof(T)), source, argument).Compile();
        }

        public static Func<int, object> StaticIntDictionaryValueGetter(Type type, string name)
        {
            var field = Field(type, name);
            var args = field.FieldType.GetGenericArguments();
            if (!field.IsStatic || args.Length != 2 || args[0] != typeof(int)) throw new ArgumentException("Expected a static int-key dictionary.");
            var key = Expression.Parameter(typeof(int), "key");
            var value = Expression.Variable(args[1], "value");
            var dictionary = Expression.Field(null, field);
            var lookup = field.FieldType.GetMethod("TryGetValue", new[] { typeof(int), args[1].MakeByRefType() });
            var found = Expression.AndAlso(Expression.NotEqual(dictionary, Expression.Constant(null, field.FieldType)),
                Expression.Call(dictionary, lookup, key, value));
            return Expression.Lambda<Func<int, object>>(Expression.Block(new[] { value },
                Expression.Condition(found, Expression.Convert(value, typeof(object)), Expression.Constant(null, typeof(object)))), key).Compile();
        }
    }
}
