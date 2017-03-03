using System.Linq;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace MrBench.TaskResultExtraction
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<MethodExtraction>();
        }
    }

    public class Message1 { }
    public class Message2 { }
    public class Message3 { }
    public class Message4 { }
    public class Message5 { }

    public class MyThing1
    {
        public void Apply(Message1 msg) { }
        public void Apply(Message2 msg) { }
        public void Apply(Message3 msg) { }
        public void Apply(Message4 msg) { }
        public void Apply(Message5 msg) { }
    }

    public class MyThing2
    {
        public void Apply(Message1 msg) { }
        public void Apply(Message2 msg) { }
        public void Apply(Message3 msg) { }
        public void Apply(Message4 msg) { }
        public void Apply(Message5 msg) { }
    }

    public class MyThing3
    {
        public void Apply(Message1 msg) { }
        public void Apply(Message2 msg) { }
        public void Apply(Message3 msg) { }
        public void Apply(Message4 msg) { }
        public void Apply(Message5 msg) { }
    }

    internal static class GenericMethodCacheFor<T> where T : class
    {
        internal static readonly MethodInfo[] Methods = typeof(T)
            .GetMethods()
            .Where(x => x.Name == "Apply" && x.GetParameters().Length == 1)
            .ToArray();
    }

    public class MethodExtraction
    {
        [Params(1, 10, 100)]
        public int Count { get; set; }

        [Benchmark]
        public void UsingReflection()
        {
            for (var c = 0; c < Count; c++)
            {
                var m1 = typeof(MyThing1)
                    .GetMethods()
                    .Where(x => x.Name == "Apply" && x.GetParameters().Length == 1)
                    .ToArray();

                var m2 = typeof(MyThing2)
                    .GetMethods()
                    .Where(x => x.Name == "Apply" && x.GetParameters().Length == 1)
                    .ToArray();

                var m3 = typeof(MyThing3)
                    .GetMethods()
                    .Where(x => x.Name == "Apply" && x.GetParameters().Length == 1)
                    .ToArray();
            }
        }

        [Benchmark]
        public void UsingGenericMethodCache()
        {
            for (var c = 0; c < Count; c++)
            {
                var m1 = GenericMethodCacheFor<MyThing1>.Methods;
                var m2 = GenericMethodCacheFor<MyThing2>.Methods;
                var m3 = GenericMethodCacheFor<MyThing3>.Methods;
            }
        }
    }
}