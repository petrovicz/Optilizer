# Optilizer
This is a Roslyn analyzer library focusing on finding and fixing code that has a risk of sub-optimal performance.

Usually these syntaxes have a good performance with smaller datasets (like in development environments) and start under performing when deployed to production servers where used on large datasets.

### List of analyzers
| Code | Description |
| -------- | ------- |
| NC001 | Avoid ?? inside Contains within IQueryable.Where |
| NC002 | Avoid ?? inside Contains in LINQ query where clause |
