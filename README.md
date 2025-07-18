# Optilizer
This is a Roslyn analyzer library focusing on finding and fixing code that has a risk of sub-optimal performance.

Usually these syntaxes have a good performance with smaller datasets (like in development environments) and start under performing when deployed to production servers where used on large datasets.

## GIS001 - Avoid calling Geometry.Union inside loops

**Problem:** Calling `Geometry.Union()` repeatedly in loops (for, foreach, while) has poor performance characteristics, especially with large geometry collections. Each union operation can be expensive and doing them individually creates O(n²) complexity in many cases.

**Solution:** Use `NetTopologySuite.Operation.Union.CascadedPolygonUnion.Union()` for batch operations, which provides much better performance through optimized algorithms.

**Example:**
```csharp
// Bad - Poor performance
Geometry result = null;
foreach (var geom in geometries)
{
    result = result.Union(geom);
}

// Good - Much better performance  
Geometry result = CascadedPolygonUnion.Union(geometries);
```

### List of analyzers
| Code | Description |
| -------- | ------- |
| NC001 | Avoid ?? inside Contains within IQueryable.Where |
| NC002 | Avoid ?? inside Contains in LINQ query where clause |
| GIS001 | Avoid calling Geometry.Union inside loops |
