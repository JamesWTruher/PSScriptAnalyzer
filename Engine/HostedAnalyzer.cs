using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Management.Automation;
using System.Management.Automation.Language;
using System.Management.Automation.Runspaces;
using Microsoft.PowerShell.ScriptAnalyzer;
using Microsoft.PowerShell.ScriptAnalyzer.Generic;

namespace Microsoft.PowerShell.ScriptAnalyzer
{
    public class AnalyzerConfiguration
    {
        public List<string> CustomRulePath;
        public List<string> IncludedRuleNames;
        public List<string> ExcludedRuleNames;
        public List<string> Severity;
        public bool IncludeDefaultRules;
        public bool SuppressedOnly;
        public string Profile;
        public AnalyzerConfiguration()
        {
            CustomRulePath = new List<string>();
            IncludedRuleNames = new List<string>();
            ExcludedRuleNames = new List<string>();
            Severity = new List<string>();
            IncludeDefaultRules = true;
            SuppressedOnly = false;
            Profile = String.Empty;
        }
    }

    public class AnalyzerResult
    {
        public List<DiagnosticRecord> Result;
        public List<ErrorRecord> TerminatingErrors;
        public List<ErrorRecord> Errors;
        public List<string> Verbose;
        public List<string> Warning;
        public List<string> Debug;

        private HostedAnalyzer _hostedAnalyzer;

        public AnalyzerResult()
        {
            _hostedAnalyzer = new HostedAnalyzer();
            Result = new List<DiagnosticRecord>();
            TerminatingErrors = new List<ErrorRecord>();
            Errors = new List<ErrorRecord>();
            Verbose = new List<string>();
            Warning = new List<string>();
            Debug = new List<string>();
        }

        public AnalyzerResult(HostedAnalyzer analyzer)
        {
            _hostedAnalyzer = analyzer;
            Result = new List<DiagnosticRecord>();
            TerminatingErrors = new List<ErrorRecord>();
            Errors = new List<ErrorRecord>();
            Verbose = new List<string>();
            Warning = new List<string>();
            Debug = new List<string>();
        }

        public AnalyzerResult AnalyzeFile(string path, bool clearStreams = false)
        {
            AnalyzerResult result = _hostedAnalyzer.AnalyzeFile(path, clearStreams);
            return result;
        }
        public AnalyzerResult AnalyzeScript(string script, bool clearStreams = false)
        {
            AnalyzerResult result = _hostedAnalyzer.AnalyzeScript(script, clearStreams);
            return result;
        }
        public AnalyzerResult AnalyzeAst(Ast ast, bool clearStreams = false)
        {
            AnalyzerResult result = _hostedAnalyzer.AnalyzeScript(ast.Extent.Text, clearStreams);
            return result;
        }

    }

    public class HostedAnalyzer
    {
        bool initialized;
        internal System.Management.Automation.PowerShell ps;
        internal ScriptAnalyzer analyzer;
        internal outputWriter writer;
        internal AnalyzerConfiguration Configuration = new AnalyzerConfiguration();
        public List<double> OperationDuration;

        /// <summary>
        /// Set the configuration of the analyzer
        /// 
        /// <paramref name="config"/>
        /// </summary>
        /// <param name="config"></param>
        public void SetConfiguration(AnalyzerConfiguration config)
        {
            Configuration = config;
            InitializeAnalyzer();
        }

        /// <summary>
        /// Reset the analyzer, recreate the PowerShell instance
        /// and analyzer. This may be used when the session is faulted
        /// or failed for any reason.
        /// </summary>
        public void ResetAnalyzer()
        {
            initialized = false;
            InitializePowerShell();
            InitializeAnalyzer();
            initialized = true;
            OperationDuration.Clear();
        }

        void InitializeAnalyzer()
        {
            analyzer = new ScriptAnalyzer();
            analyzer.Initialize(
                ps.Runspace,
                writer,
                Configuration.CustomRulePath.ToArray(),
                Configuration.IncludedRuleNames.ToArray(),
                Configuration.ExcludedRuleNames.ToArray(),
                Configuration.Severity.ToArray(),
                Configuration.IncludeDefaultRules,
                Configuration.SuppressedOnly,
                Configuration.Profile
                );
            // analyzer.Initialize(ps.Runspace, writer, null, null, null, null, true, false, null);
        }

        void InitializePowerShell()
        {
            if ( ps != null )
            {
                ps.Dispose();
            }
            InitialSessionState iss = InitialSessionState.CreateDefault2();
            // iss.ExecutionPolicy = ExecutionPolicy.Bypass;
            ps = System.Management.Automation.PowerShell.Create(iss);
            ResetStreams();
            OperationDuration.Clear();
        }

        public HostedAnalyzer()
        {
            if ( initialized ) { return; }
            writer = new outputWriter();
            OperationDuration = new List<double>();
            ResetAnalyzer();
        }

        internal void ResetStreams()
        {
            if ( initialized )
            {
                ps.Streams.ClearStreams();
            }
        }

        public AnalyzerResult AnalyzeFile(string path, bool ClearStreams = false)
        {
            if ( ClearStreams )
            {
                ResetStreams();     
            }
            DateTime start = DateTime.Now;
            AnalyzerResult ar = new AnalyzerResult(this);
            OperationDuration.Add ( (DateTime.Now - start).TotalMilliseconds);

            var result = analyzer.AnalyzePath(path, delegate { return true; }, false);
            ar.Result.AddRange(result);
            ar.TerminatingErrors.AddRange(writer.TerminatingErrors);
            ar.Errors.AddRange(writer.Errors);
            ar.Verbose.AddRange(writer.Verbose);
            ar.Warning.AddRange(writer.Warning);
            ar.Debug.AddRange(writer.Debug);
            
            return ar;
        }

        public AnalyzerResult AnalyzeScript(string ScriptDefinition, bool ClearStreams = false)
        {
            if ( ClearStreams )
            {
                ResetStreams();     
            }
            AnalyzerResult ar = new AnalyzerResult(this);
            DateTime start = DateTime.Now;
            var result = analyzer.AnalyzeScriptDefinition(ScriptDefinition);
            OperationDuration.Add ( (DateTime.Now - start).TotalMilliseconds);
            ar.Result.AddRange(result);
            ar.TerminatingErrors.AddRange(writer.TerminatingErrors);
            ar.Errors.AddRange(writer.Errors);
            ar.Verbose.AddRange(writer.Verbose);
            ar.Warning.AddRange(writer.Warning);
            ar.Debug.AddRange(writer.Debug);

            return ar;
        }

        internal class outputWriter : IOutputWriter
        {
            public IList<ErrorRecord> TerminatingErrors = new List<ErrorRecord>();
            public IList<ErrorRecord> Errors = new List<ErrorRecord>();
            public IList<string> Verbose = new List<string>();
            public IList<string> Debug = new List<string>();
            public IList<string> Warning = new List<string>();
            public void ThrowTerminatingError(ErrorRecord er) { TerminatingErrors.Add(er); }
            public void WriteError(ErrorRecord er) { Errors.Add(er); }
            public void WriteVerbose(string m) { Verbose.Add(m); }
            public void WriteDebug(string m) { Debug.Add(m); }
            public void WriteWarning(string m) { Warning.Add(m); }
        }
    }
}
