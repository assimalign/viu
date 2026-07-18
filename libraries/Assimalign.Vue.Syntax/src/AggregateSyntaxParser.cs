using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax;

public abstract class AggregateSyntaxParser
{

    public abstract object Parse(AggregateSyntaxParserContext context);
}
