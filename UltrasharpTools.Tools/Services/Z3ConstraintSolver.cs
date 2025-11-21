using Microsoft.Z3;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Wrapper around Microsoft Z3 SMT solver for constraint solving.
/// </summary>
public sealed class Z3ConstraintSolver : IDisposable
{
    private readonly ILogger<Z3ConstraintSolver> _logger;
    private readonly Context _context;
    private bool _disposed;
    private static readonly char[] separator = new[] { ' ' };

    public Z3ConstraintSolver(ILogger<Z3ConstraintSolver> logger)
    {
        _logger = logger;

        // Create Z3 context with reasonable timeout
        var config = new Dictionary<string, string>
        {
            { "timeout", "5000" }, // 5 seconds
            { "model", "true" }, // Generate models for satisfiable constraints
        };

        _context = new Context(config);
    }

    /// <summary>
    /// Checks if a set of constraints is satisfiable.
    /// </summary>
    public SolverResult IsSatisfiable(List<SymbolicConstraint> constraints)
    {
        try
        {
            var solver = _context.MkSolver();

            // Convert constraints to Z3 expressions
            foreach (var constraint in constraints)
            {
                var z3Expr = ParseZ3Expression(constraint.ToZ3Expression());
                if (z3Expr != null)
                {
                    solver.Assert((BoolExpr)z3Expr);
                }
            }

            // Check satisfiability
            var status = solver.Check();

            if (status == Status.SATISFIABLE)
            {
                // Extract model (example values)
                var model = solver.Model;
                var exampleInputs = ExtractModelValues(model);

                _logger.LogDebug(
                    "Constraints satisfiable. Example: {Example}",
                    string.Join(", ", exampleInputs.Select(kv => $"{kv.Key}={kv.Value}"))
                );

                return new SolverResult { IsSatisfiable = true, ExampleInputs = exampleInputs };
            }
            else if (status == Status.UNSATISFIABLE)
            {
                _logger.LogDebug("Constraints unsatisfiable");

                return new SolverResult
                {
                    IsSatisfiable = false,
                    Reason = "Constraints are contradictory",
                };
            }
            else
            {
                _logger.LogWarning("Z3 solver returned UNKNOWN");

                return new SolverResult
                {
                    IsSatisfiable = false,
                    Reason = "Solver timeout or unknown",
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during constraint solving");

            return new SolverResult
            {
                IsSatisfiable = false,
                Reason = $"Solver error: {ex.Message}",
            };
        }
    }

    /// <summary>
    /// Parses a Z3 S-expression string into a Z3 expression.
    /// Simplified parser for basic constraints.
    /// </summary>
    private Expr? ParseZ3Expression(string sexpr)
    {
        // This is a simplified parser. In production, would use a proper S-expression parser.
        // For now, handle basic patterns.

        try
        {
            sexpr = sexpr.Trim();

            // Handle comparison operators
            if (
                sexpr.StartsWith("(=")
                || sexpr.StartsWith("(>")
                || sexpr.StartsWith("(<")
                || sexpr.StartsWith("(>=")
                || sexpr.StartsWith("(<=")
            )
            {
                // Extract operator and operands
                var parts = sexpr
                    .TrimStart('(')
                    .TrimEnd(')')
                    .Split(separator, StringSplitOptions.RemoveEmptyEntries);
                var op = parts[0];
                var left = parts[1];
                var right = parts[2];

                var leftExpr = MakeExpr(left);
                var rightExpr = MakeExpr(right);

                if (leftExpr is ArithExpr leftArith && rightExpr is ArithExpr rightArith)
                {
                    return op switch
                    {
                        "=" => _context.MkEq(leftArith, rightArith),
                        ">" => _context.MkGt(leftArith, rightArith),
                        "<" => _context.MkLt(leftArith, rightArith),
                        ">=" => _context.MkGe(leftArith, rightArith),
                        "<=" => _context.MkLe(leftArith, rightArith),
                        _ => null,
                    };
                }
            }

            // Handle logical operators
            if (sexpr.StartsWith("(and") || sexpr.StartsWith("(or"))
            {
                // Simplified: assume two operands
                var isAnd = sexpr.StartsWith("(and");
                var inner = sexpr.Substring(isAnd ? 5 : 4).TrimEnd(')').Trim();

                // Split by space (simplified)
                var operands = SplitSExpression(inner);
                var exprs = operands
                    .Select(ParseZ3Expression)
                    .Where(e => e != null)
                    .Cast<BoolExpr>()
                    .ToArray();

                if (exprs.Length > 0)
                {
                    return isAnd ? _context.MkAnd(exprs) : _context.MkOr(exprs);
                }
            }

            if (sexpr.StartsWith("(not"))
            {
                var inner = sexpr.Substring(5).TrimEnd(')').Trim();
                var expr = ParseZ3Expression(inner);
                if (expr is BoolExpr boolExpr)
                {
                    return _context.MkNot(boolExpr);
                }
            }

            // Handle arithmetic
            if (
                sexpr.StartsWith("(+")
                || sexpr.StartsWith("(-")
                || sexpr.StartsWith("(*")
                || sexpr.StartsWith("(div")
            )
            {
                // Similar pattern
            }

            _logger.LogWarning("Could not parse Z3 expression: {Expr}", sexpr);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing Z3 expression: {Expr}", sexpr);
            return null;
        }
    }

    private Expr MakeExpr(string token)
    {
        // Try to parse as integer
        if (int.TryParse(token, out var intValue))
        {
            return _context.MkInt(intValue);
        }

        // Otherwise, it's a variable
        return _context.MkIntConst(token);
    }

    private List<string> SplitSExpression(string expr)
    {
        // Simplified S-expression splitter
        // In production, use proper parser
        var result = new List<string>();
        var depth = 0;
        var current = "";

        foreach (var ch in expr)
        {
            if (ch == '(')
                depth++;
            if (ch == ')')
                depth--;

            if (ch == ' ' && depth == 0 && !string.IsNullOrWhiteSpace(current))
            {
                result.Add(current.Trim());
                current = "";
            }
            else
            {
                current += ch;
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            result.Add(current.Trim());
        }

        return result;
    }

    private Dictionary<string, object> ExtractModelValues(Model model)
    {
        var result = new Dictionary<string, object>();

        foreach (var decl in model.ConstDecls)
        {
            var name = decl.Name.ToString();
            var value = model.ConstInterp(decl);

            if (value != null)
            {
                // Extract concrete value
                if (value.IsIntNum)
                {
                    result[name] = int.Parse(value.ToString());
                }
                else if (value.IsBool)
                {
                    result[name] = value.IsTrue;
                }
                else
                {
                    result[name] = value.ToString();
                }
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _context?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Result of constraint solving.
/// </summary>
public sealed class SolverResult
{
    public required bool IsSatisfiable { get; init; }
    public Dictionary<string, object>? ExampleInputs { get; init; }
    public string? Reason { get; init; }
}
