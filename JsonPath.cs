//
// C# implementation of JSONPath[1]
//
// Copyright (c) 2007 Atif Aziz (http://www.raboof.com/)
// Licensed under The MIT License
//
// Supported targets:
//
//  - Mono 1.1 or later
//  - Microsoft .NET Framework 1.0 or later
//
// [1]  JSONPath - XPath for JSON
//      http://code.google.com/p/jsonpath/
//      Copyright (c) 2007 Stefan Goessner (goessner.net)
//      Licensed under The MIT License
//

#region The MIT License
//
// The MIT License
//
// Copyright (c) 2007 Atif Aziz (http://www.raboof.com/)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
#endregion

namespace JsonPath
{
    #region Imports

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    #endregion

    public delegate object JsonPathScriptEvaluator(string script, object value, string context);
    public delegate void JsonPathResultAccumulator(object value, string[] indicies);

    public interface IJsonPathValueSystem
    {
        bool HasMember(object value, string member);
        object GetMemberValue(object value, string member);
        IEnumerable<string> GetMembers(object value);
        bool IsObject(object value);
        bool IsArray(object value);
        bool IsPrimitive(object value);
    }

    [Serializable]
    public sealed class JsonPathNode
    {
        public JsonPathNode(object value, string path)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (path.Length == 0) throw new ArgumentException("path");

            Value = value;
            Path = path;
        }

        public object Value { get; private set; }
        public string Path { get; private set; }

        public override string ToString() { return Path + " = " + Value; }
    }

    public sealed class JsonPathContext
    {
        public static readonly JsonPathContext Default = new JsonPathContext();

        public JsonPathScriptEvaluator ScriptEvaluator { get; set; }
        public IJsonPathValueSystem ValueSystem { get; set; }

        public void SelectTo(object obj, string expr, JsonPathResultAccumulator output)
        {
            if (obj == null) throw new ArgumentNullException("obj");
            if (output == null) throw new ArgumentNullException("output");

            var i = new Interpreter(output, ValueSystem, ScriptEvaluator);

            expr = Normalize(expr);

            if (expr.Length >= 1 && expr[0] == '$') // ^\$:?
                expr = expr.Substring(expr.Length >= 2 && expr[1] == ';' ? 2 : 1);

            i.Trace(expr, obj, "$");
        }

        public JsonPathNode[] SelectNodes(object obj, string expr)
        {
            var list = new ArrayList();
            SelectNodesTo(obj, expr, list);
            return (JsonPathNode[]) list.ToArray(typeof(JsonPathNode));
        }

        public IList SelectNodesTo(object obj, string expr, IList output)
        {
            var nodes = output ?? new ArrayList();
            SelectTo(obj, expr, (value, indicies) => nodes.Add(new JsonPathNode(value, AsBracketNotation(indicies))));
            return nodes;
        }

        static Regex RegExp(string pattern)
        {
            return new Regex(pattern, RegexOptions.ECMAScript);
        }

        static string Normalize(string expr)
        {
            var subx = new List<string>();
            expr = RegExp(@"[\['](\??\(.*?\))[\]']").Replace(expr, m =>
            {
                subx.Add(m.Groups[1].Value);
                return "[#" + subx.Count.ToString(CultureInfo.InvariantCulture) + "]";
            });
            expr = RegExp(@"'?\.'?|\['?").Replace(expr, ";");
            expr = RegExp(@";;;|;;").Replace(expr, ";..;");
            expr = RegExp(@";$|'?\]|'$").Replace(expr, string.Empty);
            expr = RegExp(@"#([0-9]+)").Replace(expr, m =>
            {
                var index = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                return subx[index];
            });
            return expr;
        }

        public static string AsBracketNotation(string[] indicies)
        {
            if (indicies == null)
                throw new ArgumentNullException("indicies");

            var sb = new StringBuilder();

            foreach (var index in indicies)
            {
                if (sb.Length == 0)
                {
                    sb.Append('$');
                }
                else
                {
                    sb.Append('[');
                    if (RegExp(@"^[0-9*]+$").IsMatch(index))
                        sb.Append(index);
                    else
                        sb.Append('\'').Append(index).Append('\'');
                    sb.Append(']');
                }
            }

            return sb.ToString();
        }

        static int ParseInt(string str, int defaultValue = 0)
        {
            if (string.IsNullOrEmpty(str))
                return defaultValue;

            try
            {
                return int.Parse(str, NumberStyles.None, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return defaultValue;
            }
        }

        sealed class Interpreter
        {
            readonly JsonPathResultAccumulator output;
            readonly JsonPathScriptEvaluator eval;
            readonly IJsonPathValueSystem system;

            static readonly IJsonPathValueSystem defaultValueSystem = new BasicValueSystem();

            static readonly char[] colon = { ':' };
            static readonly char[] semicolon = { ';' };

            delegate void WalkCallback(object member, string loc, string expr, object value, string path);

            public Interpreter(JsonPathResultAccumulator output, IJsonPathValueSystem valueSystem, JsonPathScriptEvaluator eval)
            {
                Debug.Assert(output != null);

                this.output = output;
                this.eval = eval ?? NullEval;
                system = valueSystem ?? defaultValueSystem;
            }

            public void Trace(string expr, object value, string path)
            {
                if (string.IsNullOrEmpty(expr))
                {
                    Store(path, value);
                    return;
                }

                var i = expr.IndexOf(';');
                var atom = i >= 0 ? expr.Substring(0, i) : expr;
                var tail = i >= 0 ? expr.Substring(i + 1) : string.Empty;

                if (value != null && system.HasMember(value, atom))
                {
                    Trace(tail, Index(value, atom), path + ";" + atom);
                }
                else if (atom == "*")
                {
                    Walk(atom, tail, value, path, WalkWild);
                }
                else if (atom == "..")
                {
                    Trace(tail, value, path);
                    Walk(atom, tail, value, path, WalkTree);
                }
                else if (atom.Length > 2 && atom[0] == '(' && atom[atom.Length - 1] == ')') // [(exp)]
                {
                    Trace(eval(atom, value, path.Substring(path.LastIndexOf(';') + 1)) + ";" + tail, value, path);
                }
                else if (atom.Length > 3 && atom[0] == '?' && atom[1] == '(' && atom[atom.Length - 1] == ')') // [?(exp)]
                {
                    Walk(atom, tail, value, path, WalkFiltered);
                }
                else if (RegExp(@"^(-?[0-9]*):(-?[0-9]*):?([0-9]*)$").IsMatch(atom)) // [start:end:step] Phyton slice syntax
                {
                    Slice(atom, tail, value, path);
                }
                else if (atom.IndexOf(',') >= 0) // [name1,name2,...]
                {
                    foreach (var part in RegExp(@"'?,'?").Split(atom))
                        Trace(part + ";" + tail, value, path);
                }
            }

            void Store(string path, object value)
            {
                if (path != null)
                    output(value, path.Split(semicolon));
            }

            void Walk(string loc, string expr, object value, string path, WalkCallback callback)
            {
                if (system.IsPrimitive(value))
                    return;

                if (system.IsArray(value))
                {
                    var list = (IList) value;
                    for (var i = 0; i < list.Count; i++)
                        callback(i, loc, expr, value, path);
                }
                else if (system.IsObject(value))
                {
                    foreach (var key in system.GetMembers(value))
                        callback(key, loc, expr, value, path);
                }
            }

            void WalkWild(object member, string loc, string expr, object value, string path)
            {
                Trace(member + ";" + expr, value, path);
            }

            void WalkTree(object member, string loc, string expr, object value, string path)
            {
                var result = Index(value, member.ToString());
                if (result != null && !system.IsPrimitive(result))
                    Trace("..;" + expr, result, path + ";" + member);
            }

            void WalkFiltered(object member, string loc, string expr, object value, string path)
            {
                var result = eval(RegExp(@"^\?\((.*?)\)$").Replace(loc, "$1"),
                    Index(value, member.ToString()), member.ToString());

                if (Convert.ToBoolean(result, CultureInfo.InvariantCulture))
                    Trace(member + ";" + expr, value, path);
            }

            void Slice(string loc, string expr, object value, string path)
            {
                var list = value as IList;

                if (list == null)
                    return;

                var length = list.Count;
                var parts = loc.Split(colon);
                var start = ParseInt(parts[0]);
                var end = ParseInt(parts[1], list.Count);
                var step = parts.Length > 2 ? ParseInt(parts[2], 1) : 1;
                start = (start < 0) ? Math.Max(0, start + length) : Math.Min(length, start);
                end = (end < 0) ? Math.Max(0, end + length) : Math.Min(length, end);
                for (var i = start; i < end; i += step)
                    Trace(i + ";" + expr, value, path);
            }

            object Index(object obj, string member)
            {
                return system.GetMemberValue(obj, member);
            }

            static object NullEval(string expr, object value, string context)
            {
                //
                // @ symbol in expr must be interpreted specially to resolve
                // to value. In JavaScript, the implementation would look
                // like:
                //
                // return obj && value && eval(expr.replace(/@/g, "value"));
                //

                return null;
            }
        }

        sealed class BasicValueSystem : IJsonPathValueSystem
        {
            public bool HasMember(object value, string member)
            {
                if (IsPrimitive(value))
                    return false;

                var dict = value as IDictionary;
                if (dict != null)
                    return dict.Contains(member);

                var list = value as IList;
                if (list != null)
                {
                    var index = ParseInt(member, -1);
                    return index >= 0 && index < list.Count;
                }

                return false;
            }

            public object GetMemberValue(object value, string member)
            {
                if (IsPrimitive(value))
                    throw new ArgumentException("value");

                var dict = value as IDictionary;
                if (dict != null)
                    return dict[member];

                var list = (IList) value;
                var index = ParseInt(member, -1);
                if (index >= 0 && index < list.Count)
                    return list[index];

                return null;
            }

            public IEnumerable<string> GetMembers(object value)
            {
                return ((IDictionary) value).Keys.Cast<string>();
            }

            public bool IsObject(object value)
            {
                return value is IDictionary;
            }

            public bool IsArray(object value)
            {
                return value is IList;
            }

            public bool IsPrimitive(object value)
            {
                if (value == null)
                    throw new ArgumentNullException("value");

                return Type.GetTypeCode(value.GetType()) != TypeCode.Object;
            }
        }
    }
}