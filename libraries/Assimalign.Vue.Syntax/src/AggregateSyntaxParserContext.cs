using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax;

public class AggregateSyntaxParserContext
{
    public List<SyntaxParser> Parsers { get; } = new List<SyntaxParser>();
}
