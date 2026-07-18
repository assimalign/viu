using System;
using System.Collections.Generic;
using System.Text;

namespace Assimalign.Vue.Syntax;

public enum SyntaxNodeKind
{
    Template,
    Css,
    /// <summary>
    /// Syntax for .viu components
    /// </summary>
    Component,
    Custom
}
