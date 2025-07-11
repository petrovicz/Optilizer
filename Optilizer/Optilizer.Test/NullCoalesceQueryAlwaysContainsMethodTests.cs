using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = Optilizer.Test.CSharpCodeFixVerifier<
    Optilizer.NullCoalesceQueryMethodAnalyzer,
    Optilizer.NullCoalesceQueryAlwaysContainsCodeFixProvider>;

namespace Optilizer.Test
{
    [TestClass]
    public class NullCoalesceQueryAlwaysContainsMethodTests
    {
        [TestMethod]
        public async Task CodeFixAppearsAndWorks()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list, IQueryable<int?> values)
    {
        var q = values.Where(x => list.Contains({|#0:x ?? 0|}));
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
        var q = values.Where(x => ((x.HasValue && list.Contains(x.Value)) || (!x.HasValue && list.Contains(0))));
    }
}";

            var expected = VerifyCS.Diagnostic("NC001").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixWorksWithReferenceType_Method()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<string> list, IQueryable<string> values)
    {
        var q = values.Where(x => list.Contains({|#0:x ?? ""default""|}));
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
        var q = values.Where(x => ((x != null && list.Contains(x)) || (x == null && list.Contains(""default""))));
    }
}";

            var expected = VerifyCS.Diagnostic("NC001").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixWorksWithAndConditionBeforeContains_Method()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list, IQueryable<int?> values)
    {
        var q = values.Where(x =>
            x != 5 &&
            list.Contains({|#0:x ?? 0|})
        );
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
        var q = values.Where(x =>
            x != 5 &&
            ((x.HasValue && list.Contains(x.Value)) || (!x.HasValue && list.Contains(0)))
        );
    }
}";

            var expected = VerifyCS.Diagnostic("NC001").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task CodeFixWorksWithAndConditionBeforeContains_ReferenceType_Method()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<string> list, IQueryable<string> values)
    {
        var q = values.Where(x =>
            x != ""foo"" &&
            list.Contains({|#0:x ?? ""default""|})
        );
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
        var q = values.Where(x =>
            x != ""foo"" &&
            ((x != null && list.Contains(x)) || (x == null && list.Contains(""default"")))
        );
    }
}";

            var expected = VerifyCS.Diagnostic("NC001").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }
    }
}
