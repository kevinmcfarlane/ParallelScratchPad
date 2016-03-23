using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ParallelScratchPad
{
    /// <summary>
    /// Illustrates various concepts in .NET 4.5 Task Parallel Library.
    /// </summary>
    class Program
    {
        private static void Main(string[] args)
        {
            Runner1();
            //Runner2();
        }

        private static void Runner1()
        {
            CreateLongRunningTasks();
            //LoopThroughTasksIncorrect();
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

        private static void Welcome(int numberOfTasks, int numberOfCores, int durationInMinutes, int durationInSeconds)
        {
            Console.WriteLine("**Long-running tasks App**");
            Console.WriteLine("Number of tasks:\t" + numberOfTasks);
            Console.WriteLine("Number of cores:\t" + numberOfCores);
            Console.WriteLine(string.Format("Task duration: {0} mins, {1} secs", durationInMinutes, durationInSeconds));
        }

        /// <summary>
        /// Creates long running tasks with no optimisation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Initially creates one task per core and then keeps injecting worker threads every few seconds and grabs the next task.
        /// This is because each task is long-running, i.e., longer than a few seconds.
        /// </para>
        /// <para>
        /// If you make the tasks run for, say, 5 minutes you will see the thread count in task manager creeping higher and higher.
        /// Eventually the threads will climb to 100.
        /// This is called over-subscription, with too many threads doing CPU-intensive work competing for cores.
        /// </para>
        /// </remarks>
        private static void CreateLongRunningTasks()
        {
            int numberOfTasks = 100;
            var tasks = new List<Task>();

            int numberOfCores = Environment.ProcessorCount;
            int durationInMinutes = 5;
            int durationInSeconds = 0;

            Welcome(numberOfTasks, numberOfCores, durationInMinutes, durationInSeconds);

            TaskCreationOptions taskCreationOptions = TaskCreationOptions.None;

            for (int i = 0; i < numberOfTasks; i++)
            {
                Task t = CreateLongRunningTask(durationInMinutes, durationInSeconds, taskCreationOptions);
                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());
        }

        /// <summary>
        /// Creates long running tasks with long-running option.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Creates a dedicated worker thread for each task, so 100 are created straight away instead of one by one.
        /// This is because each task is long-running, i.e., longer than a few seconds.
        /// </para>
        /// <para>
        /// However, this is still over-subscribed.
        /// </para>
        /// </remarks>
        private static void CreateLongRunningTasksWithLongRunningOption()
        {
            int numberOfTasks = 100;
            var tasks = new List<Task>();

            int numberOfCores = Environment.ProcessorCount;
            int durationInMinutes = 5;
            int durationInSeconds = 0;

            Welcome(numberOfTasks, numberOfCores, durationInMinutes, durationInSeconds);

            TaskCreationOptions taskCreationOptions = TaskCreationOptions.LongRunning;

            for (int i = 0; i < numberOfTasks; i++)
            {
                Task t = CreateLongRunningTask(durationInMinutes, durationInSeconds, taskCreationOptions);
                tasks.Add(t);
            }

            Task.WaitAll(tasks.ToArray());
        }

        /// <summary>
        /// Creates long-running tasks (optimised).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Creates 100 tasks all at once and then waits for them to finish.
        /// This is a technique to avoid too many threads competing for cores.
        /// </para>
        /// <para>
        /// Initially we create as many tasks as the number of cores.
        /// Then we start a new task as one finishes until there is no more work to do.
        /// </para>
        /// </remarks>
        private static void CreateLongRunningTasksOptimised()
        {
            int numberOfTasks = 100;
            var tasks = new List<Task>();

            int numberOfCores = Environment.ProcessorCount;
            int durationInMinutes = 5;
            int durationInSeconds = 0;

            Welcome(numberOfTasks, numberOfCores, durationInMinutes, durationInSeconds);

            TaskCreationOptions taskCreationOptions = TaskCreationOptions.None;

            for (int i = 0; i < numberOfCores; i++)
            {
                Task t = CreateLongRunningTask(durationInMinutes, durationInSeconds, taskCreationOptions);
                tasks.Add(t);
            }

            // Variation of "Wait All One By One" pattern
            while (tasks.Any())
            {
                int index = Task.WaitAny(tasks.ToArray());
                tasks.RemoveAt(index);

                numberOfTasks--;

                bool moreWorkToDo = numberOfTasks > 0;

                if (moreWorkToDo)
                {
                    // Create another task
                    Task t = CreateLongRunningTask(durationInMinutes, durationInSeconds, taskCreationOptions);
                    tasks.Add(t);
                }
            }
        }

        /// <summary>
        /// Creates a long running task of the specified duration.
        /// </summary>
        /// <param name="durationInMinutes">The duration in minutes.</param>
        /// <param name="durationInSeconds">The duration in seconds.</param>
        /// <param name="taskCreationOptions">The task creation options.</param>
        /// <returns></returns>
        private static Task CreateLongRunningTask(int durationInMinutes, int durationInSeconds, TaskCreationOptions taskCreationOptions)
        {
            long durationInMilliseconds = durationInMinutes * 60 * 1000 + durationInSeconds * 1000;

            Task t = Task.Factory.StartNew(() =>
            {
                Console.WriteLine("Starting task...");

                var stopwatch = Stopwatch.StartNew();
                long count = 0;

                while (stopwatch.ElapsedMilliseconds < durationInMilliseconds)
                {
                    count++;

                    if (count == 1000000000)
                    {
                        count = 0;
                    }
                }

                Console.WriteLine("Task finished.");
            },
            taskCreationOptions);

            return t;
        }
    }
}