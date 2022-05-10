using System.Numerics;
using Newtonsoft.Json.Serialization;
using Timepiece.Angler.UntypedAst;
using Timepiece.Angler.UntypedAst.AstExpr;
using Timepiece.Angler.UntypedAst.AstStmt;
using Timepiece.Angler.UntypedAst.AstFunction;
// using Timepiece.Angler.TypedAst;
// using Timepiece.Angler.TypedAst.AstExpr;
// using Timepiece.Angler.TypedAst.AstFunction;
// using Timepiece.Angler.TypedAst.AstStmt;
using Timepiece.Datatypes;
using Timepiece.Networks;
using ZenLib;

namespace Timepiece.Angler;

using Route = Pair<bool, BatfishBgpRoute>;

public class PairRouteAst : Ast<Route, Unit>
{
  /// <summary>
  /// Default predicates to test for this AST.
  /// </summary>
  public static readonly AstPredicate<Route> IsValid = new("route",
    new First(new Var("route")));

  /// <summary>
  /// Default import behavior for a route.
  /// </summary>
  private static readonly AstFunction<Route> DefaultImport = AstFunction<Route>.Identity();

  /// <summary>
  /// Default export behavior for a route.
  /// </summary>
  private static readonly AstFunction<Route> DefaultExport = new("arg", new[]
  {
    new Return(
      new PairExpr(
        new First(new Var("arg")),
        new WithField(typeof(BatfishBgpRoute), typeof(BigInteger), new Second(new Var("arg")),
          "AsPathLength",
          new Plus(
            new GetField(typeof(BatfishBgpRoute), typeof(BigInteger),
              new Second(new Var("arg")),
              "AsPathLength"), new ConstantExpr(BigInteger.One)))))
  });

  public PairRouteAst(Dictionary<string, NodeProperties<Route>> nodes, Ipv4Prefix? destination,
    Dictionary<string, AstPredicate<Route>> predicates, Dictionary<string, AstPredicate<Unit>> symbolics,
    BigInteger? convergeTime) : base(nodes,
    symbolics, predicates, destination, convergeTime)
  {
  }

  private static Zen<Route> InitFunction(bool isDestination)
  {
    return Pair.Create<bool, BatfishBgpRoute>(isDestination, new BatfishBgpRoute());
  }

  public Network<Route, Unit> ToNetwork()
  {
    return ToNetwork(InitFunction, BatfishBgpRouteExtensions.MinPair, DefaultExport, DefaultImport);
  }

  public static ISerializationBinder Binder()
  {
    return new AstSerializationBinder<Route>();
  }
}
