using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelScratchPad
{
    class Program
    {
        private static void Main(string[] args)
        {
            Runner1();
            //Runner2();
        }

        private static void Runner1()
        {
            LoopThroughTasksIncorrect();
            //LoopThroughTasksCorrect();
            Finish();
        }

        private static void Runner2()
        {
            for (int i = 0; i < 10; i++)
            {
                LoopThroughTasksSharedLock();
                Finish();
            }
        }

        private static void Finish()
        {
            Console.WriteLine();
            Console.WriteLine("Done");
            Console.WriteLine();
        }

        /// <summary>
        /// Loops through tasks identifying them (incorrectly).
        /// </summary>
        /// <remarks>
        /// This fails because loop finishes before task runs, so only last value of i is repeatedly printed.
        /// </remarks>
        private static void LoopThroughTasksIncorrect()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                Task t = Task.Factory.StartNew(() =>
                {
                    int taskId = i;
                    Console.WriteLine(taskId); // prints ten 10s
                });
                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());
        }

        /// <summary>
        /// Loops through tasks identifying them.
        /// </summary>
        /// <remarks>
        /// If value may change, e.g., loop counter, don't use closure, pass as a task parameter.
        /// </remarks>
        private static void LoopThroughTasksCorrect()
        {
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                Task t = Task.Factory.StartNew((arg) =>
                {
                    int taskId = (int)arg;
                    Console.WriteLine(taskId);
                },
                i); // i is captured when task is created, not when it starts - without this loop runs to end before task starts, so prints out last value of i

                tasks.Add(t);
            }
        }

        /// <summary>
        /// Loops through tasks with unsafe access to shared resource.
        /// </summary>
        /// <remarks>
        /// The number of hits should always add up to 10 x 10000 = 100000 but this not always the case due to shared access to hits.
        /// </remarks>
        private static void LoopThroughTasksSharedNotThreadSafe()
        {
            int hits = 0; // shared state
            var tasks = new List<Task>();

            for (int i = 0; i < 10; i++)
            {
                Task t = Task.Factory.StartNew((arg) =>
                {
                    int taskId = (int)arg;
                    Console.WriteLine("Task Id = " + taskId);

                    for (int j = 0; j < 10000; j++)
                    {
                        hits++;
                    }
                },
                i);

                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            Console.WriteLine("Hits = " + hits);
        }

        /// <summary>
        /// Loops through tasks with safe access to shared resource using lock.
        /// </summary>
        /// <remarks>
        /// Correct. But prefer a lock-free solution where possible.
        /// </remarks>
        private static void LoopThroughTasksSharedLock()
        {
            int hits = 0; // shared state
            var tasks = new List<Task>();
            var l = new object();

            for (int i = 0; i < 10; i++)
            {
                Task t = Task.Factory.StartNew((arg) =>
                {
                    int taskId = (int)arg;
                    Console.WriteLine("Task Id = " + taskId);

                    for (int j = 0; j < 10000; j++)
                    {
                        lock (l) hits++;
                    }
                },
                i);

                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            Console.WriteLine("Hits = " + hits);
        }

        /// <summary>
        /// Loops through tasks with safe access to shared resource using interlocking.
        /// </summary>
        /// <remarks>
        /// More efficient. Reads, increments and writes in one atomic operation.
        /// </remarks>
        private static void LoopThroughTasksSharedInterlocked()
        {
            var tasks = new List<Task>();
            int hits = 0;

            for (int i = 0; i < 10; i++)
            {
                Task t = Task.Factory.StartNew((arg) =>
                {
                    int taskId = (int)arg;
                    Console.WriteLine("Task Id = " + taskId);

                    for (int j = 0; j < 10000; j++)
                    {
                        Interlocked.Increment(ref hits); // hardware-based
                    }
                },
                i);

                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            Console.WriteLine("Hits = " + hits);
        }

        /// <summary>
        /// Loops through tasks with safe access to shared resource without locking.
        /// </summary>
        /// <remarks>
        /// A local hits variable is defined for each task and returned at the end. Then all are summed.
        /// </remarks>
        private static void LoopThroughTasksSharedLockFree()
        {
            var tasks = new List<Task<int>>();

            for (int i = 0; i < 10; i++)
            {
                Task<int> t = Task.Factory.StartNew<int>((arg) =>
                {
                    int localHits = 0;
                    int taskId = (int)arg;
                    Console.WriteLine("Task Id = " + taskId);

                    for (int j = 0; j < 10000; j++)
                    {
                        localHits++;
                    }

                    return localHits;
                },
                i);

                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());

            int hits = 0;

            foreach (var t in tasks)
            {
                hits += t.Result;
            }

            Console.WriteLine("Hits = " + hits);
        }

        /// <summary>
        /// Loops through tasks with safe access to shared resource without locking (optimised).
        /// </summary>
        /// <remarks>
        /// A local hits variable is defined for each task and returned at the end. Then all are summed.
        /// Optimised using the "Wait All One By One" pattern.
        /// </remarks>
        private static void LoopThroughTasksSharedLockFreeWaitAllOneByOne()
        {
            var tasks = new List<Task<int>>();

            for (int i = 0; i < 10; i++)
            {
                Task<int> t = Task.Factory.StartNew<int>((arg) =>
                {
                    int localHits = 0;
                    int taskId = (int)arg;
                    Console.WriteLine("Task Id = " + taskId);

                    for (int j = 0; j < 10000; j++)
                    {
                        localHits++;
                    }

                    return localHits;
                },
                i);

                tasks.Add(t);
            }

            List<int> results = WaitAllOneByOne(tasks);

            int hits = results.AsParallel().Sum();

            Console.WriteLine("Hits = " + hits);
        }

        /// <summary>
        ///  Waits until all tasks finish, but processes results as each one completes.
        /// </summary>
        /// <remarks>
        /// This is based on:
        /// 
        /// <para>
        /// Introduction to Async and Parallel Programming in .NET 4 by Dr Joe Hummel
        /// <seealso href="http://app.pluralsight.com/courses/intro-async-parallel-dotnet4" />
        /// </para>
        /// </remarks>
        /// <param name="tasks">The tasks.</param>
        /// <returns>A collection of results from each task.</returns>
        private static List<int> WaitAllOneByOne(List<Task<int>> tasks)
        {
            var results = new List<int>();

            while (tasks.Count > 0)
            {
                int i = Task.WaitAny(tasks.ToArray());
                results.Add(tasks[i].Result);
                tasks.RemoveAt(i);
            }

            return results;
        }
    }
}