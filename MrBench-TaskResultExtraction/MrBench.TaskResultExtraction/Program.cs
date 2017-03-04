using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace MrBench.TaskResultExtraction
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<ExtractFromTask>();
        }
    }

    public class ExtractFromTask
    {
        private List<ICase> _cases;

        [Params(1, 10, 100)]
        public int Count { get; set; }

        [Setup]
        public void Setup()
        {
            _cases = new List<ICase>
            {
                new Case<Thing>(new Thing("Some value")),
                new Case<string>("Some other value"),
                new Case<int>(42),
                new Case<decimal>(3.14M),
                new Case<bool>(true),
                new Case<DateTime>(DateTime.Now),
                new Case<List<int>>(new List<int> { 1, 2, 3, 4, 5 }),
                new Case<string[]>(new []{ ".Net", "C#", "Benchmark" })
            };
        }

        [Benchmark]
        public void UsingReflection()
        {
            for (var c = 0; c < Count; c++)
            {
                for (var i = 0; i < _cases.Count; i++)
                    _cases[i].ExtractUsingReflection();
            }
        }

        [Benchmark]
        public void UsingReflectionWithSameProp()
        {
            for (var c = 0; c < Count; c++)
            {
                for (var i = 0; i < _cases.Count; i++)
                    _cases[i].ExtractUsingReflectionWithSameProp();
            }
        }

        [Benchmark]
        public void UsingReflectionWithCache()
        {
            for (var c = 0; c < Count; c++)
            {
                for (var i = 0; i < _cases.Count; i++)
                    _cases[i].ExtractUsingReflectionWithCache();
            }
        }

        [Benchmark]
        public void UsingIlWithSameGetter()
        {
            for (var c = 0; c < Count; c++)
            {
                for (var i = 0; i < _cases.Count; i++)
                    _cases[i].ExtractUsingIlWithSameGetter();
            }
        }

        [Benchmark]
        public void UsingIlWithCache()
        {
            for (var c = 0; c < Count; c++)
            {
                for (var i = 0; i < _cases.Count; i++)
                    _cases[i].ExtractUsingIlWithCache();
            }
        }

        [Benchmark]
        public void UsingCompiledLambdasWithSameGetter()
        {
            for (var c = 0; c < Count; c++)
            {
                for (var i = 0; i < _cases.Count; i++)
                    _cases[i].ExtractUsingLambdaWithSameGetter();
            }
        }

        [Benchmark]
        public void UsingCompiledLambdasWithCache()
        {
            for (var c = 0; c < Count; c++)
            {
                for (var i = 0; i < _cases.Count; i++)
                    _cases[i].ExtractUsingLambdaWithCache();
            }
        }

        [Benchmark]
        public void UsingDynamic()
        {
            for (var c = 0; c < Count; c++)
            {
                for (var i = 0; i < _cases.Count; i++)
                    _cases[i].ExtractUsingDynamic();
            }
        }
    }

    public interface ICase
    {
        void ExtractUsingReflection();
        void ExtractUsingReflectionWithSameProp();
        void ExtractUsingReflectionWithCache();

        void ExtractUsingIlWithSameGetter();
        void ExtractUsingIlWithCache();

        void ExtractUsingLambdaWithSameGetter();
        void ExtractUsingLambdaWithCache();

        void ExtractUsingDynamic();
    }

    public class Case<T> : ICase
    {
        public Task<T> TaskItem { get; }
        public Type TaskType { get; }
        public PropertyInfo Prop { get; }
        public DynamicGetter IlGetter { get; }
        public DynamicGetter LambdaGetter { get; }

        public Case(T value)
        {
            TaskItem = Task.FromResult(value);
            TaskType = typeof(Task<T>);
            Prop = TaskType.GetProperty("Result");
            IlGetter = DynamicGetterFactory.CreateUsingIl(Prop);
            LambdaGetter = DynamicGetterFactory.CreateUsingCompiledLambda(Prop);
        }

        public void ExtractUsingReflection()
        {
            var value = (T)TaskItem
                    .GetType()
                    .GetProperty("Result")
                    .GetValue(TaskItem);

            EnsureExtractionSucceeded(value);
        }

        public void ExtractUsingReflectionWithSameProp()
        {
            var value = (T)Prop.GetValue(TaskItem);

            EnsureExtractionSucceeded(value);
        }

        public void ExtractUsingReflectionWithCache()
        {
            var value = (T)Cache.PropCache
                .GetFor(TaskType)
                .GetValue(TaskItem);

            EnsureExtractionSucceeded(value);
        }

        public void ExtractUsingIlWithSameGetter()
        {
            var value = (T)IlGetter(TaskItem);

            EnsureExtractionSucceeded(value);
        }

        public void ExtractUsingIlWithCache()
        {
            var value = (T)Cache.IlGetterCache
                .GetFor(TaskType)(TaskItem);

            EnsureExtractionSucceeded(value);
        }

        public void ExtractUsingLambdaWithSameGetter()
        {
            var value = (T)LambdaGetter(TaskItem);

            EnsureExtractionSucceeded(value);
        }

        public void ExtractUsingLambdaWithCache()
        {
            var value = (T)Cache.LambdaGetterCache
                .GetFor(TaskType)(TaskItem);

            EnsureExtractionSucceeded(value);
        }

        public void ExtractUsingDynamic()
        {
            dynamic d = TaskItem;
            var value = (T)d.Result;

            EnsureExtractionSucceeded(value);
        }

        private void EnsureExtractionSucceeded(T value)
        {
            if (!TaskItem.Result.Equals(value))
                throw new Exception("Extraction failed");
        }
    }

    public static class Cache
    {
        public static readonly PropInfoCache PropCache = new PropInfoCache();
        public static readonly DynamicIlGetterCache IlGetterCache = new DynamicIlGetterCache();
        public static readonly DynamicCompiledLambdaGetterCache LambdaGetterCache = new DynamicCompiledLambdaGetterCache();
    }

    public class Thing
    {
        public string Value { get; }

        public Thing(string value)
        {
            Value = value;
        }
    }

    public class PropInfoCache
    {
        private readonly ConcurrentDictionary<Type, PropertyInfo> _state =
            new ConcurrentDictionary<Type, PropertyInfo>();

        public PropertyInfo GetFor(Type type) => _state.GetOrAdd(
            type, t => t.GetProperty("Result"));
    }

    public class DynamicIlGetterCache
    {
        private readonly ConcurrentDictionary<Type, DynamicGetter> _state
            = new ConcurrentDictionary<Type, DynamicGetter>();

        public DynamicGetter GetFor(Type type) => _state.GetOrAdd(
            type, t => DynamicGetterFactory.CreateUsingIl(t.GetProperty("Result")));
    }

    public class DynamicCompiledLambdaGetterCache
    {
        private readonly ConcurrentDictionary<Type, DynamicGetter> _state
            = new ConcurrentDictionary<Type, DynamicGetter>();

        public DynamicGetter GetFor(Type type) => _state.GetOrAdd(
            type, t => DynamicGetterFactory.CreateUsingCompiledLambda(t.GetProperty("Result")));
    }

    public delegate object DynamicGetter(object obj);

    public static class DynamicGetterFactory
    {
        private static readonly Type ObjectType = typeof(object);
        private static readonly Type IlGetterType = typeof(Func<object, object>);

        public static DynamicGetter CreateUsingIl(PropertyInfo p)
            => new DynamicGetter(CreateIlGetter(p));

        public static DynamicGetter CreateUsingCompiledLambda(PropertyInfo p)
            => new DynamicGetter(CreateLambdaGetter(p.DeclaringType, p));

        private static Func<object, object> CreateLambdaGetter(
            Type type,
            PropertyInfo property)
        {
            var objExpr = Expression.Parameter(ObjectType, "theItem");
            var castedObjExpr = Expression.Convert(objExpr, type);

            var p = Expression.Property(castedObjExpr, property);
            var castedProp = Expression.Convert(p, ObjectType);

            var lambda = Expression.Lambda<Func<object, object>>(castedProp, objExpr);

            return lambda.Compile();
        }

        private static Func<object, object> CreateIlGetter(PropertyInfo propertyInfo)
        {
            var propGetMethod = propertyInfo.GetGetMethod(true);
            if (propGetMethod == null)
                return null;

            var getter = CreateDynamicGetMethod(propertyInfo);
            var generator = getter.GetILGenerator();

            var x = generator.DeclareLocal(propertyInfo.DeclaringType);//Arg
            var y = generator.DeclareLocal(propertyInfo.PropertyType); //Prop val
            var z = generator.DeclareLocal(ObjectType); //Prop val as obj

            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Castclass, propertyInfo.DeclaringType);
            generator.Emit(OpCodes.Stloc, x);

            generator.Emit(OpCodes.Ldloc, x);
            generator.EmitCall(OpCodes.Callvirt, propGetMethod, null);
            generator.Emit(OpCodes.Stloc, y);

            generator.Emit(OpCodes.Ldloc, y);

            if (!propertyInfo.PropertyType.IsClass)
            {
                generator.Emit(OpCodes.Box, propertyInfo.PropertyType);
                generator.Emit(OpCodes.Stloc, z);
                generator.Emit(OpCodes.Ldloc, z);
            }

            generator.Emit(OpCodes.Ret);

            return (Func<object, object>)getter.CreateDelegate(IlGetterType);
        }

        private static DynamicMethod CreateDynamicGetMethod(PropertyInfo propertyInfo)
        {
            var args = new[] { ObjectType };
            var name = $"_{propertyInfo.DeclaringType.Name}_Get{propertyInfo.Name}_";
            var returnType = ObjectType;

            return !propertyInfo.DeclaringType.IsInterface
                       ? new DynamicMethod(
                             name,
                             returnType,
                             args,
                             propertyInfo.DeclaringType,
                             true)
                       : new DynamicMethod(
                             name,
                             returnType,
                             args,
                             propertyInfo.Module,
                             true);
        }
    }
}