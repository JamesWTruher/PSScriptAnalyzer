using System;
using System.Diagnostics;
// using System.Runtime.CompilerServices;
// using System.Reflection.Internal;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer
{

    public sealed class TaskDuration
    {
        public string RuleName;
        public TimeSpan Elapsed;
        public DateTime TimeCreated;
        public TaskDuration(string name, TimeSpan elapsed)
        {
            TimeCreated = DateTime.Now;
            RuleName = name;
            Elapsed = elapsed;
        }
    }

    /// <summary>
    /// Simple timer to be used with `using` to time executions.
    /// </summary>
    /// <example>
    /// An example showing how ExecutionTimer is intended to be used
    /// <code>
    /// using (ExecutionTimer.Start(logger, "Execution of MyMethod completed."))
    /// {
    ///     MyMethod(various, arguments);
    /// }
    /// </code>
    /// This will print a message like "Execution of MyMethod completed. [50ms]" to the logs.
    /// </example>
    public struct ExecutionTimer : IDisposable
    {
        // private static readonly ObjectPool<Stopwatch> s_stopwatchPool = new ObjectPool<Stopwatch>();

        private static ConcurrentBag<TaskDuration> taskDurations = new ConcurrentBag<TaskDuration>();

        private Stopwatch _stopwatch;

        private readonly string _name;

        public static IEnumerable<TaskDuration> GetTiming()
        {
            return taskDurations.ToArray();
        }

        /// <summary>
        /// Create a new execution timer and start it.
        /// </summary>
        /// <param name="message">The message to prefix the execution time with.</param>
        /// <param name="callerMemberName">The name of the calling method or property.</param>
        /// <param name="callerFilePath">The path to the source file of the caller.</param>
        /// <param name="callerLineNumber">The line where the timer is called.</param>
        /// <returns>A new, started execution timer.</returns>
        public static ExecutionTimer Start(string name)
        {
            var timer = new ExecutionTimer(name);
            timer._stopwatch.Start();
            return timer;
        }

        internal ExecutionTimer(string name)
        {
            _name = name;
            _stopwatch = new Stopwatch(); // s_stopwatchPool.Rent();
        }

        /// <summary>
        /// Dispose of the execution timer by stopping the stopwatch and then add the
        /// object to our collection.
        /// </summary>
        public void Dispose()
        {
            _stopwatch.Stop();
            // _stopwatch.Reset();
            TaskDuration duration = new TaskDuration(_name, _stopwatch.Elapsed);
            // s_stopwatchPool.Return(_stopwatch);
            taskDurations.Add(duration);
        }
    }
}
