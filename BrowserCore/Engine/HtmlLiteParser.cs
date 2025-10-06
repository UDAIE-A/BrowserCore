using System;
using System.Collections.Generic;
using System.Text;

namespace BrowserCore.Engine
{
    internal static class HtmlLiteParser
    {
        private static readonly HashSet<string> VoidTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "br","img","hr","meta","link","input","area","base","col","embed","param","source","track","wbr"
        };

        public static LiteElement Parse(string html)
        {
            if (string.IsNullOrEmpty(html)) return new LiteElement { Tag = "root" };
            int i = 0, n = html.Length;
            var root = new LiteElement { Tag = "root" };
            var stack = new Stack<LiteElement>();
            stack.Push(root);
            while (i < n)
            {
                char c = html[i];
                if (c == '<')
                {
                    if (StartsWith(html, i + 1, "!--"))
                    {
                        int endCom = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                        if (endCom < 0) break; i = endCom + 3; continue;
                    }
                    if (i + 1 < n && html[i + 1] == '/')
                    {
                        // closing tag
                        int close = html.IndexOf('>', i + 2);
                        if (close < 0) break;
                        string tname = html.Substring(i + 2, close - (i + 2)).Trim().Split(' ')[0];
                        // pop until matching
                        while (stack.Count > 1)
                        {
                            var top = stack.Pop();
                            if (string.Equals(top.Tag, tname, StringComparison.OrdinalIgnoreCase)) break;
                        }
                        i = close + 1; continue;
                    }
                    // opening tag
                    int gt = html.IndexOf('>', i + 1);
                    if (gt < 0) break;
                    bool selfClose = html[gt - 1] == '/';
                    string inside = html.Substring(i + 1, gt - (i + 1));
                    var el = ParseTag(inside, ref selfClose);
                    if (el == null) { i = gt + 1; continue; }
                    el.SelfClose = selfClose || VoidTags.Contains(el.Tag);
                    stack.Peek().Children.Add(el);
                    if (!el.SelfClose)
                    {
                        if (!string.Equals(el.Tag, "script", StringComparison.OrdinalIgnoreCase) &&
                            !string.Equals(el.Tag, "style", StringComparison.OrdinalIgnoreCase))
                        {
                            stack.Push(el);
                        }
                        else
                        {
                            // capture raw text until closing tag
                            string endTag = "</" + el.Tag + ">";
                            int end = IndexOfIgnoreCase(html, endTag, gt + 1);
                            if (end < 0) { i = gt + 1; continue; }
                            string raw = html.Substring(gt + 1, end - (gt + 1));
                            if (string.Equals(el.Tag, "style", StringComparison.OrdinalIgnoreCase))
                                el.Text.Append(raw);
                            // skip over end tag
                            i = end + endTag.Length; continue;
                        }
                    }
                    i = gt + 1;
                }
                else
                {
                    // text node: accumulate trimmed but preserve spaces
                    int next = html.IndexOf('<', i);
                    if (next < 0) next = n;
                    var text = html.Substring(i, next - i);
                    if (text.Length > 0) stack.Peek().Text.Append(text);
                    i = next;
                }
            }
            return root;
        }

        private static LiteElement ParseTag(string inside, ref bool selfClose)
        {
            try
            {
                int i = 0; int n = inside.Length;
                // trim
                while (i < n && char.IsWhiteSpace(inside[i])) i++;
                if (i >= n) return null;
                // tag
                int start = i; while (i < n && !char.IsWhiteSpace(inside[i]) && inside[i] != '/' ) i++;
                string tag = inside.Substring(start, i - start).Trim();
                var el = new LiteElement { Tag = tag };
                // attrs
                while (i < n)
                {
                    while (i < n && char.IsWhiteSpace(inside[i])) i++;
                    if (i >= n) break;
                    if (inside[i] == '/') { selfClose = true; break; }
                    int anStart = i; while (i < n && !char.IsWhiteSpace(inside[i]) && inside[i] != '=' && inside[i] != '/') i++;
                    string an = inside.Substring(anStart, i - anStart);
                    string av = string.Empty;
                    while (i < n && char.IsWhiteSpace(inside[i])) i++;
                    if (i < n && inside[i] == '=')
                    {
                        i++; while (i < n && char.IsWhiteSpace(inside[i])) i++;
                        if (i < n && (inside[i] == '"' || inside[i] == '\''))
                        {
                            char q = inside[i++]; int valStart = i; while (i < n && inside[i] != q) i++;
                            av = inside.Substring(valStart, Math.Max(0, i - valStart)); if (i < n) i++;
                        }
                        else
                        {
                            int vs = i; while (i < n && !char.IsWhiteSpace(inside[i]) && inside[i] != '/') i++;
                            av = inside.Substring(vs, i - vs);
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(an))
                    {
                        if (!el.Attributes.ContainsKey(an)) el.Attributes.Add(an, av);
                        else el.Attributes[an] = av;
                    }
                }
                return el;
            }
            catch { return null; }
        }

        private static bool StartsWith(string s, int idx, string token)
        {
            if (idx < 0 || idx + token.Length > s.Length) return false;
            for (int j = 0; j < token.Length; j++) if (s[idx + j] != token[j]) return false; return true;
        }
        private static int IndexOfIgnoreCase(string s, string token, int start)
        {
            return s.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
        }
    }
}
