﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;

namespace Inedo.Extensions.Clients.CommandLine
{
    internal sealed class GitArgumentsBuilder
    {
        private List<GitArg> arguments = new List<GitArg>(16);

        public GitArgumentsBuilder()
        {
        }

        public GitArgumentsBuilder(string initialArguments)
        {
            this.Append(initialArguments);
        }

        public void Append(string arg) => this.arguments.Add(new GitArg(arg, false, false));
        public void AppendQuoted(string arg) => this.arguments.Add(new GitArg(arg, true, false));
        public void AppendSensitive(string arg) => this.arguments.Add(new GitArg(arg, true, true));

        public override string ToString() => string.Join(" ", this.arguments);
        public string ToSensitiveString() => string.Join(" ", this.arguments.Select(a => a.ToSensitiveString()));

        private sealed class GitArg
        {
            private bool quoted;
            private bool sensitive;
            private string arg;

            public GitArg(string arg, bool quoted, bool sensitive)
            {
                this.arg = arg ?? "";
                this.quoted = quoted;
                this.sensitive = sensitive;
            }

            public override string ToString()
            {
                if (this.quoted)
                    return '"' + this.arg.Replace("\"", @"\""") + '"';
                else
                    return this.arg;
            }

            public string ToSensitiveString()
            {
                if (this.sensitive)
                    return "(hidden)";
                else if (this.quoted)
                    return '"' + this.arg.Replace("\"", @"\""") + '"';
                else
                    return this.arg;
            }
        }
    }
}
