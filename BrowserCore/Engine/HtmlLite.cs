using System;
using System.Collections.Generic;
using System.Text;

namespace BrowserCore.Engine
{
    // Extremely small HTML node model for experimental renderer
    internal class LiteElement
    {
        public string Tag { get; set; }
        public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<LiteElement> Children { get; } = new List<LiteElement>();
        public StringBuilder Text { get; } = new StringBuilder();
        public bool SelfClose { get; set; }
        public override string ToString() => "<" + Tag + ">";
    }
}
