using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax;

/// <summary>
/// 
/// </summary>
public abstract class SyntaxParser
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="source"></param>
    /// <returns></returns>
    public abstract SyntaxParserResult Parse(string source);
}
