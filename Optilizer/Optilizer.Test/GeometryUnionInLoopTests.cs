using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS = Optilizer.Test.CSharpCodeFixVerifier<
    Optilizer.GeometryUnionInLoopAnalyzer,
    Optilizer.GeometryUnionInLoopCodeFixProvider>;

namespace Optilizer.Test
{
    [TestClass]
    public class GeometryUnionInLoopTests
    {
        [TestMethod]
        public async Task ForEachLoop_WithGeometryUnion_ReportsWarning()
        {
            var test = @"
using System.Collections.Generic;

namespace NetTopologySuite.Geometries
{
    public abstract class Geometry
    {
        public abstract Geometry Union(Geometry other);
    }
}

namespace TestNamespace
{
    using NetTopologySuite.Geometries;
    
    class C
    {
        void M(List<Geometry> geometries)
        {
            Geometry result = null;
            foreach (var geom in geometries)
            {
                result = {|#0:result.Union(geom)|};
            }
        }
    }
}";

            var expected = VerifyCS.Diagnostic("GIS001").WithLocation(0);
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [TestMethod]
        public async Task GeometryUnion_OutsideLoop_NoWarning()
        {
            var test = @"
namespace NetTopologySuite.Geometries
{
    public abstract class Geometry
    {
        public abstract Geometry Union(Geometry other);
    }
}

namespace TestNamespace
{
    using NetTopologySuite.Geometries;
    
    class C
    {
        void M(Geometry geom1, Geometry geom2)
        {
            var result = geom1.Union(geom2);
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [TestMethod]
        public async Task NonGeometryUnion_InLoop_NoWarning()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;

class C
{
    void M(List<int[]> arrays)
    {
        int[] result = null;
        foreach (var array in arrays)
        {
            result = result.Union(array).ToArray();
        }
    }
}";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }
    }
}