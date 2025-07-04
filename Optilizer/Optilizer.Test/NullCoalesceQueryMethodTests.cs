using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = Optilizer.Test.CSharpCodeFixVerifier<
	Optilizer.NullCoalesceQueryMethodAnalyzer,
	Optilizer.NullCoalesceQueryMethodCodeFixProvider>;

namespace Optilizer.Test
{
    [TestClass]
    public class NullCoalesceQueryMethodTests
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
        var q = values.Where(x => x.HasValue && list.Contains(x.Value));
    }
}";

            var expected = VerifyCS.Diagnostic("NC001").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task MultipleContainsInPredicate()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list1, List<int> list2, IQueryable<int?> values)
    {
        var q = values.Where(x => list1.Contains({|#0:x ?? 0|}) && list2.Contains({|#1:x ?? 0|}));
    }
}";

            var fixedTest = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list1, List<int> list2, IQueryable<int?> values)
    {
        var q = values.Where(x => (x.HasValue && list1.Contains(x.Value)) && (x.HasValue && list2.Contains(x.Value)));
    }
}";

            var expected0 = VerifyCS.Diagnostic("NC001").WithLocation(0);
            var expected1 = VerifyCS.Diagnostic("NC001").WithLocation(1);
            await VerifyCS.VerifyCodeFixAsync(test, new[] { expected0, expected1 }, fixedTest);
        }

		[TestMethod]
        public async Task ContainsNextToOrOperator()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list, IQueryable<int?> values)
    {
        var q = values.Where(x => list.Contains({|#0:x ?? 0|}) || x == null);
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
        var q = values.Where(x => (x.HasValue && list.Contains(x.Value)) || x == null);
    }
}";

            var expected = VerifyCS.Diagnostic("NC001").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task MultipleContainsAndOrOperator()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list1, List<int> list2, IQueryable<int?> values)
    {
        var q = values.Where(x => list1.Contains({|#0:x ?? 0|}) || list2.Contains({|#1:x ?? 0|}));
    }
}";

            var fixedTest = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list1, List<int> list2, IQueryable<int?> values)
    {
        var q = values.Where(x => (x.HasValue && list1.Contains(x.Value)) || (x.HasValue && list2.Contains(x.Value)));
    }
}";

            var expected0 = VerifyCS.Diagnostic("NC001").WithLocation(0);
            var expected1 = VerifyCS.Diagnostic("NC001").WithLocation(1);
            await VerifyCS.VerifyCodeFixAsync(test, new[] { expected0, expected1 }, fixedTest);
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
        var q = values.Where(x => x != null && list.Contains(x));
    }
}";

            var expected = VerifyCS.Diagnostic("NC001").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }
    }
}
