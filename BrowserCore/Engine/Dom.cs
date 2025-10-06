using System;
using System.Collections.Generic;
using System.Linq;

namespace BrowserCore.Engine
{
    // Minimal DOM model used by the lightweight renderer and JS shims.
    public class Document
    {
        public string Title { get; set; }
        public Element Body { get; private set; }

        private readonly Dictionary<string, Element> _byId = new Dictionary<string, Element>(StringComparer.OrdinalIgnoreCase);

        public Document()
        {
            Body = new Element("body");
        }

        public void Register(Element e)
        {
            if (!string.IsNullOrWhiteSpace(e.Id)) _byId[e.Id] = e;
        }

        public Element GetElementById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            Element e = null;
            _byId.TryGetValue(id, out e);
            return e;
        }

        public Element QuerySelector(string selector)
        {
            if (string.IsNullOrWhiteSpace(selector)) return null;
            selector = selector.Trim();
            if (selector.StartsWith("#")) return GetElementById(selector.Substring(1));
            if (selector.StartsWith("."))
            {
                var cls = selector.Substring(1);
                return Body.Descendants().FirstOrDefault(x => x.ClassList.Contains(cls));
            }
            // tag selector
            return Body.Descendants().FirstOrDefault(x => string.Equals(x.TagName, selector, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class Element
    {
        public string TagName { get; private set; }
        public string Id { get; set; }
        public List<string> ClassList { get; } = new List<string>();
        public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> ComputedStyle { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string InnerText { get; set; }
        public Element Parent { get; private set; }
        private readonly List<Element> _children = new List<Element>();
        public IReadOnlyList<Element> Children => _children;

        public Element(string tagName)
        {
            TagName = tagName ?? "div";
        }

        public void AppendChild(Element c)
        {
            if (c == null) return;
            c.Parent = this;
            _children.Add(c);
        }

        public IEnumerable<Element> Descendants()
        {
            foreach (var c in _children)
            {
                yield return c;
                foreach (var g in c.Descendants()) yield return g;
            }
        }
    }

    public static class DomManager
    {
        // Current navigation document (set by CustomHtmlEngine)
        public static Document Current { get; set; }
    }

    public static class CssApplier
    {
        // cssRules: selector -> prop->value
        public static void ApplyRulesToElement(Element e, Dictionary<string, Dictionary<string, string>> cssRules)
        {
            if (e == null || cssRules == null) return;
            // simple matching: id selector, class selector, tag selector
            foreach (var kv in cssRules)
            {
                var selector = kv.Key.Trim();
                bool match = false;
                if (selector.StartsWith("#") && !string.IsNullOrWhiteSpace(e.Id)) match = string.Equals(selector.Substring(1), e.Id, StringComparison.OrdinalIgnoreCase);
                else if (selector.StartsWith(".")) match = e.ClassList.Contains(selector.Substring(1));
                else match = string.Equals(selector, e.TagName, StringComparison.OrdinalIgnoreCase);

                if (match)
                {
                    foreach (var p in kv.Value)
                    {
                        e.ComputedStyle[p.Key] = p.Value;
                    }
                }
            }
            // propagate to children
            foreach (var c in e.Children) ApplyRulesToElement(c, cssRules);
        }
    }
}
