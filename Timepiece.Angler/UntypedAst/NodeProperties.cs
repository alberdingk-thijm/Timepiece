using System.Collections.Immutable;
using Timepiece.Angler.UntypedAst.AstExpr;
using Timepiece.Angler.UntypedAst.AstFunction;
using ZenLib;

namespace Timepiece.Angler.UntypedAst;

/// <summary>
///   Representation of the properties of a node as parsed from JSON.
///   Tracks the node's prefixes and its routing policies.
/// </summary>
/// <typeparam name="T">The type of routes for the node.</typeparam>
public class NodeProperties<T>
{
  public NodeProperties(Dictionary<string, RoutingPolicies> policies,
    string? stable, AstTemporalOperator<T>? temporal, Dictionary<string, AstFunction<T>> declarations,
    Constants constants, Expr initial)
  {
    Policies = policies;
    Stable = stable;
    Temporal = temporal;
    Initial = initial;
    Declarations = declarations;
    Constants = constants;
    DisambiguateVariableNames();
  }

  public Expr Initial { get; set; }

  /// <summary>
  ///   Additional function declarations.
  /// </summary>
  public Dictionary<string, AstFunction<T>> Declarations { get; set; }

  /// <summary>
  ///   Additional constant declarations.
  /// </summary>
  public Constants Constants { get; set; }

  public AstTemporalOperator<T>? Temporal { get; set; }

  public Dictionary<string, RoutingPolicies> Policies { get; }

  public string? Stable { get; }

  /// <summary>
  ///   Make the arguments to all AstFunctions unique.
  /// </summary>
  private void DisambiguateVariableNames()
  {
    foreach (var function in Declarations.Values)
    {
      function.Rename(function.Arg, $"${function.Arg}~{VarCounter.Request()}");
      Console.WriteLine($"New function arg: {function.Arg}");
    }
  }

  /// <summary>
  ///   Construct a node storing all the relevant information for creating a network.
  /// </summary>
  /// <param name="predicateLookupFunction"></param>
  /// <param name="defaultExport"></param>
  /// <param name="defaultImport"></param>
  /// <returns></returns>
  public NetworkNode<T> CreateNode(
    Func<string, AstPredicate<T>> predicateLookupFunction, AstFunction<T> defaultExport, AstFunction<T> defaultImport)
  {
    var env = new AstEnvironment();
    foreach (var (key, val) in Constants.stringConstants)
    {
      env = env.Update(key, val);
    }

    foreach (var (key, val) in Constants.prefixConstants)
    {
      Console.WriteLine($"Adding prefix constant key {key} to the environment...");
      env = env.Update(key, val);
    }

    var init = env.EvaluateExpr(Initial);

    var safetyProperty = Stable is null
      ? _ => true
      : predicateLookupFunction(Stable).Evaluate(env);

    var invariant = Temporal is null
      ? (_, _) => true
      : Temporal.Evaluate(predicateLookupFunction);

    var imports = new Dictionary<string, Func<Zen<T>, Zen<T>>>();
    var exports = new Dictionary<string, Func<Zen<T>, Zen<T>>>();
    foreach (var (neighbor, policies) in Policies)
    {
      var exportAstFunctions = policies.Export.Select(policyName => Declarations[policyName]);
      var importAstFunctions = policies.Import.Select(policyName => Declarations[policyName]);
      exports[neighbor] = defaultExport.Evaluate(env);
      foreach (var function in exportAstFunctions)
      {
        exports[neighbor] = t => exports[neighbor](function.Evaluate(env)(t));
      }

      imports[neighbor] = defaultImport.Evaluate(new AstEnvironment());
      foreach (var function in importAstFunctions)
      {
        imports[neighbor] = t => imports[neighbor](function.Evaluate(env)(t));
      }
    }

    return new NetworkNode<T>(init, safetyProperty, invariant, imports.ToImmutableDictionary(),
      exports.ToImmutableDictionary());
  }
}
