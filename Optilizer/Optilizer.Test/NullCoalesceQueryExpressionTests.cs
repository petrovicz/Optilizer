using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = Optilizer.Test.CSharpCodeFixVerifier<
	Optilizer.NullCoalesceQueryExpressionAnalyzer,
	Optilizer.NullCoalesceQueryExpressionCodeFixProvider>;

namespace Optilizer.Test
{
    [TestClass]
    public class NullCoalesceQueryExpressionTests
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
                where x.HasValue && list.Contains(x.Value)
                select x;
    }
}";

            var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task MultipleContainsInQueryWhere()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list1, List<int> list2, IQueryable<int?> values)
    {
        var q = from x in values
                where list1.Contains({|#0:x ?? 0|}) && list2.Contains({|#1:x ?? 0|})
                select x;
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
        var q = from x in values
                where (x.HasValue && list1.Contains(x.Value)) && (x.HasValue && list2.Contains(x.Value))
                select x;
    }
}";

            var expected0 = VerifyCS.Diagnostic("NC002").WithLocation(0);
            var expected1 = VerifyCS.Diagnostic("NC002").WithLocation(1);
            await VerifyCS.VerifyCodeFixAsync(test, new[] { expected0, expected1 }, fixedTest);
        }

		[TestMethod]
		public async Task MultipleContainsInQueryWhereWithComment()
		{
			var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list1, List<int> list2, IQueryable<int?> values)
    {
        var q = from x in values
                where list1.Contains({|#0:x ?? 0|}) && list2.Contains({|#1:x ?? 0|}) // This is a comment
                select x;
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
        var q = from x in values
                where (x.HasValue && list1.Contains(x.Value)) && (x.HasValue && list2.Contains(x.Value)) // This is a comment
                select x;
    }
}";

			var expected0 = VerifyCS.Diagnostic("NC002").WithLocation(0);
			var expected1 = VerifyCS.Diagnostic("NC002").WithLocation(1);
			await VerifyCS.VerifyCodeFixAsync(test, new[] { expected0, expected1 }, fixedTest);
		}

		[TestMethod]
		public async Task ContainsBeforeOrOperator_Query()
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
                where list.Contains({|#0:x ?? 0|}) || x == null
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
                where (x.HasValue && list.Contains(x.Value)) || x == null
                select x;
    }
}";

			var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
			await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
		}

		[TestMethod]
        public async Task ContainsNextToOrOperator_Query()
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
                where x == null || list.Contains({|#0:x ?? 0|})
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
                where x == null || (x.HasValue && list.Contains(x.Value))
                select x;
    }
}";

            var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

		[TestMethod]
		public async Task ContainsNextToOrOperatorWithComment_Query()
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
                where x == null || list.Contains({|#0:x ?? 0|}) // This is a comment
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
                where x == null || (x.HasValue && list.Contains(x.Value)) // This is a comment
                select x;
    }
}";

			var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
			await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
		}

		[TestMethod]
        public async Task MultipleContainsAndOrOperator_Query()
        {
            var test = @"
using System;
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int> list1, List<int> list2, IQueryable<int?> values)
    {
        var q = from x in values
                where list1.Contains({|#0:x ?? 0|}) || list2.Contains({|#1:x ?? 0|})
                select x;
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
        var q = from x in values
                where (x.HasValue && list1.Contains(x.Value)) || (x.HasValue && list2.Contains(x.Value))
                select x;
    }
}";

            var expected0 = VerifyCS.Diagnostic("NC002").WithLocation(0);
            var expected1 = VerifyCS.Diagnostic("NC002").WithLocation(1);
            await VerifyCS.VerifyCodeFixAsync(test, new[] { expected0, expected1 }, fixedTest);
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
                where x != null && list.Contains(x)
                select x;
    }
}";

            var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

        [TestMethod]
        public async Task NegatedContains_Query_CodeFixWorks()
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
                where !list.Contains({|#0:x ?? 0|})
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
                where !(x.HasValue && list.Contains(x.Value))
                select x;
    }
}";

            var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }

		[TestMethod]
		public async Task NegatedContainsWithOr_Query_CodeFixWorks()
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
                where x == null || !list.Contains({|#0:x ?? 0|})
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
                where x == null || !(x.HasValue && list.Contains(x.Value))
                select x;
    }
}";

			var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
			await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
		}

		[TestMethod]
        public async Task NegatedContains_ReferenceType_Query_CodeFixWorks()
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
                where !list.Contains({|#0:x ?? ""default""|})
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
                where !(x != null && list.Contains(x))
                select x;
    }
}";

            var expected = VerifyCS.Diagnostic("NC002").WithLocation(0);
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixedTest);
        }
    }
}
