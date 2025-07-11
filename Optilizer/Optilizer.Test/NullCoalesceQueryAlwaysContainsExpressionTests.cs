using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = Optilizer.Test.CSharpCodeFixVerifier<
    Optilizer.NullCoalesceQueryExpressionAnalyzer,
    Optilizer.NullCoalesceQueryAlwaysContainsCodeFixProvider>;

namespace Optilizer.Test
{
    [TestClass]
    public class NullCoalesceQueryAlwaysContainsExpressionTests
    {
        [TestMethod]
        public async Task CodeFixAppearsAndWorks_Query()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list, IQueryable<int?> values)
    {
        var q = from x in values
                where list.Contains({|#0:x ?? 0|})
                select x;
    }
}";

            var fixedTest = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list, IQueryable<int?> values)
    {
        var q = from x in values
                where ((x.HasValue && list.Contains(x.Value)) || (!x.HasValue && list.Contains(0)))
                select x;
    }
}";

            var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixWorksWithReferenceType_Query()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<string> list, IQueryable<string> values)
    {
        var q = from x in values
                where list.Contains({|#0:x ?? ""default""|})
                select x;
    }
}";

            var fixedTest = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<string> list, IQueryable<string> values)
    {
        var q = from x in values
                where ((x != null && list.Contains(x)) || (x == null && list.Contains(""default"")))
                select x;
    }
}";

            var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixWorksWithAndConditionBeforeContains_Query()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list, IQueryable<int?> values)
    {
        var q = from x in values
                where
                    x != 5 &&
                    list.Contains({|#0:x ?? 0|})
                select x;
    }
}";

            var fixedTest = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list, IQueryable<int?> values)
    {
        var q = from x in values
                where
                    x != 5 &&
                    ((x.HasValue && list.Contains(x.Value)) || (!x.HasValue && list.Contains(0)))
                select x;
    }
}";

            var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixWorksWithAndConditionBeforeContains_ReferenceType_Query()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<string> list, IQueryable<string> values)
    {
        var q = from x in values
                where
                    x != ""foo"" &&
                    list.Contains({|#0:x ?? ""default""|})
                select x;
    }
}";

            var fixedTest = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<string> list, IQueryable<string> values)
    {
        var q = from x in values
                where
                    x != ""foo"" &&
                    ((x != null && list.Contains(x)) || (x == null && list.Contains(""default"")))
                select x;
    }
}";

            var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }
    }
}
