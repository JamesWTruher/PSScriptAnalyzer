// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Management.Automation;
using System.Linq;

namespace Microsoft.Windows.PowerShell.ScriptAnalyzer
{
    /// <summary>
    /// Provides threadsafe caching around CommandInfo lookups with `Get-Command -Name ...`.
    /// </summary>
    internal class CommandInfoCache
    {
        private static bool _clearCache = true;
        private static bool _initializeCache = true;
        private static int hitCount = 0;
        private static int missCount = 0;
        private static ConcurrentDictionary<CommandLookupKey, DateTime> cacheMisses = new ConcurrentDictionary<CommandLookupKey, DateTime>();

        private readonly ConcurrentDictionary<CommandLookupKey, Lazy<CommandInfo>> _commandInfoCache;

        private readonly Helper _helperInstance;
        /// <summary>
        /// Create a fresh command info cache instance.
        /// </summary>
        public CommandInfoCache(Helper pssaHelperInstance)
        {
            _commandInfoCache = new ConcurrentDictionary<CommandLookupKey, Lazy<CommandInfo>>();
            _helperInstance = pssaHelperInstance;
        }

        /// <summary>
        /// Retrieve a command info object about a command.
        /// </summary>
        /// <param name="commandName">Name of the command to get a commandinfo object for.</param>
        /// <param name="commandTypes">What types of command are needed. If omitted, all types are retrieved.</param>
        /// <returns></returns>
        public CommandInfo GetCommandInfo(string commandName, CommandTypes? commandTypes = null)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                return null;
            }

            if ( _clearCache ) {
                _commandInfoCache.Clear();
                _clearCache = false;
            }

            if ( _initializeCache ) {
                InitializeCache();
            }

            CommandLookupKey key;
            if ( commandTypes == null ) {
                key = new CommandLookupKey(commandName, CommandTypes.All);
            }
            else {
                key = new CommandLookupKey(commandName, commandTypes);
            }
            // Atomically either use PowerShell to query a command info object, or fetch it from the cache
            Lazy<CommandInfo> ci;
            if ( _commandInfoCache.TryGetValue(key, out ci) ) {
                hitCount++;
            }
            else {
                missCount++;
                CommandInfo pci = GetCommandInfoInternal(commandName, commandTypes);
                cacheMisses.TryAdd(key, DateTime.Now);
                ci =  _commandInfoCache.GetOrAdd(key, new Lazy<CommandInfo>(() => pci));
            }
            return ci.Value;
        }

        /// <summary>
        /// Retrieve a command info object about a command.
        /// </summary>
        /// <param name="commandName">Name of the command to get a commandinfo object for.</param>
        /// <param name="commandTypes">What types of command are needed. If omitted, all types are retrieved.</param>
        /// <returns></returns>
        [Obsolete("Alias lookup is expensive and should not be relied upon for command lookup")]
        public CommandInfo GetCommandInfoLegacy(string commandOrAliasName, CommandTypes? commandTypes = null)
        {
            string commandName = _helperInstance.GetCmdletNameFromAlias(commandOrAliasName);

            return string.IsNullOrEmpty(commandName)
                ? GetCommandInfo(commandOrAliasName, commandTypes: commandTypes)
                : GetCommandInfo(commandName, commandTypes: commandTypes);
        }

        /// <summary>
        /// Get a CommandInfo object of the given command name
        /// </summary>
        /// <returns>Returns null if command does not exists</returns>
        private static CommandInfo GetCommandInfoInternal(string cmdName, CommandTypes? commandType)
        {
            // 'Get-Command ?' would return % for example due to PowerShell interpreting is a single-character-wildcard search and not just the ? alias.
            // For more details see https://github.com/PowerShell/PowerShell/issues/9308
            cmdName = WildcardPattern.Escape(cmdName);

            using (var ps = System.Management.Automation.PowerShell.Create())
            {
                ps.AddCommand("Get-Command")
                    .AddParameter("Name", cmdName)
                    .AddParameter("ErrorAction", "SilentlyContinue");

                if (commandType != null)
                {
                    ps.AddParameter("CommandType", commandType);
                }

                return ps.Invoke<CommandInfo>().FirstOrDefault();
            }
        }

        /// <summary>
        /// initialize the CommandInfoCache based on the standard runspacec
        /// This will initialize the cache before an attempt to run Get-Command (which is expensive)
        /// </summary>
        private void InitializeCache()
        {
            using ( var ps = System.Management.Automation.PowerShell.Create()) {
                foreach(CommandInfo ci in ps.Runspace.SessionStateProxy.InvokeCommand.GetCommands("*", CommandTypes.All, true)) {
                    var key = new CommandLookupKey(ci.Name, ci.CommandType);
                    _commandInfoCache.TryAdd(key, new Lazy<CommandInfo>(() => ci));
                    key = new CommandLookupKey(ci.Name, null);
                    _commandInfoCache.TryAdd(key, new Lazy<CommandInfo>(() => ci));
                    key = new CommandLookupKey(ci.Name, CommandTypes.All);
                    _commandInfoCache.TryAdd(key, new Lazy<CommandInfo>(() => ci));
                }
            }
            _initializeCache = false;
        }

        private struct CommandLookupKey : IEquatable<CommandLookupKey>
        {
            private readonly string Name;

            private readonly CommandTypes CommandTypes;

            internal CommandLookupKey(string name, CommandTypes? commandTypes)
            {
                Name = name;
                CommandTypes = commandTypes ?? CommandTypes.All;
            }

            public bool Equals(CommandLookupKey other)
            {
                return CommandTypes == other.CommandTypes
                    && Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase);
            }

            public override int GetHashCode()
            {
                // Algorithm from https://stackoverflow.com/questions/1646807/quick-and-simple-hash-code-combinations
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + Name.ToUpperInvariant().GetHashCode();
                    hash = hash * 31 + CommandTypes.GetHashCode();
                    return hash;
                }
            }

            public override string ToString()
            {
                return String.Format("{0}:{1}", Name, CommandTypes);
            }
        }
    }
}
