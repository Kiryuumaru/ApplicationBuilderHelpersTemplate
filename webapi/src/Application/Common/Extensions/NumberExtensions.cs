using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Extensions;

public static class NumberExtensions
{
    public static long ToNonNegative(this long value)
    {
        return value < 0 ? 0 : value;
    }
}
