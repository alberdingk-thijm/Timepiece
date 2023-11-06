using Newtonsoft.Json;
using Timepiece.Angler.Ast;
using Timepiece.Angler.DataTypes;
using Timepiece.Angler.Queries;
using Timepiece.DataTypes;
using Timepiece.Tests;
using Xunit.Abstractions;
using ZenLib;

namespace Timepiece.Angler.Tests;

public class Internet2Tests
{
  private readonly ITestOutputHelper _testOutputHelper;
  private const string Internet2FileName = "INTERNET2.angler.json";

  // TODO: change this to instead track the file down by going up the directories
  private static readonly string Internet2Path =
    Path.Join(Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.ToString(),
      Internet2FileName);

  private static readonly AnglerNetwork Internet2Ast =
    AstSerializationBinder.JsonSerializer()
      .Deserialize<AnglerNetwork>(new JsonTextReader(new StreamReader(Internet2Path)))!;

  private static readonly NodeProperties WashProperties = Internet2Ast.Nodes["wash"];

  /// <summary>
  /// Nodes whose incoming routes (to Internet2 nodes) are always rejected.
  /// </summary>
  private static readonly IEnumerable<string> RejectIncoming = Internet2.OtherGroup
    .Concat(Internet2.OtherInternalGroup)
    .Concat(Internet2.AdvancedLayer2ServiceManagementGroup);

  public Internet2Tests(ITestOutputHelper testOutputHelper)
  {
    _testOutputHelper = testOutputHelper;
  }

  [Fact]
  public void Internet2JsonFileExists()
  {
    _testOutputHelper.WriteLine(Internet2Path);
    Assert.True(File.Exists(Internet2Path));
  }

  /// <summary>
  /// Verify that the given route is "good":
  /// <list type="bullet">
  ///   <item>its result value is true and other result features are false</item>
  ///   <item>its AS set is empty</item>
  ///   <item>its prefix is 35.0.0.0/8 (UMichigan's prefix)</item>
  /// </list>
  /// </summary>
  /// <param name="route"></param>
  /// <returns></returns>
  private static Zen<bool> IsGoodUMichRoute(Zen<RouteEnvironment> route) => Zen.And(
    route.GetResultValue(),
    route.GetLocalDefaultAction(),
    route.NonTerminated(),
    Zen.Not(route.GetAsSet().Contains(Internet2.NlrAs)),
    Zen.Not(route.GetAsSet().Contains(Internet2.PrivateAs)),
    Zen.Not(route.GetAsSet().Contains(Internet2.CommercialAs)),
    route.GetPrefix() == new Ipv4Prefix("35.0.0.0", "35.255.255.255"));

  [Fact]
  public void WashSanityInAcceptsGoodRoute()
  {
    var sanityIn = WashProperties.Declarations["SANITY-IN"];
    // the call expression context must be true in order for SANITY-IN not to Return when it completes
    var import = new AstState(WashProperties.Declarations, callExprContext: true).EvaluateFunction(sanityIn);
    var transfer = new TransferCheck<RouteEnvironment>(import);
    var result = transfer.Verify(Zen.Symbolic<RouteEnvironment>("r"), IsGoodUMichRoute, r => r.GetResultValue());
    Assert.Null(result);
  }

  [Fact]
  public static void WashNeighborImportAcceptsGoodRoute()
  {
    var importPolicy = WashProperties.Policies["192.122.183.13"].Import!;
    var importFunction = WashProperties.Declarations[importPolicy];
    var import = new AstState(WashProperties.Declarations).EvaluateFunction(importFunction);
    // This import function does the following:
    // 1. Set the default policy to ~DEFAULT_BGP_IMPORT_POLICY~
    // 2. Set LocalDefaultAction to true.
    // 3. Perform a FirstMatchChain on policies SANITY-IN, SET-PREF, MERIT-IN, CONNECTOR-IN
    // 4. If the FirstMatchChain returns true, assign the result to Exit=true,Value=true
    //    Otherwise if it returns false, assign the result to Exit=true,Value=false
    var transfer = new TransferCheck<RouteEnvironment>(import);
    var result = transfer.Verify(Zen.Symbolic<RouteEnvironment>("r"), IsGoodUMichRoute,
      imported => Zen.And(imported.GetResultValue(), imported.GetResultExit()));
    Assert.Null(result);
  }

  /// <summary>
  /// Verify that every edge that goes into an Internet2 node accepts some route.
  /// </summary>
  [Fact]
  public void Internet2TransferAccepts()
  {
    var (topology, transfer) = Internet2Ast.TopologyAndTransfer();
    var route = Zen.Symbolic<RouteEnvironment>("r");
    // iterate over the edges from non-rejected neighbors to Internet2 nodes
    var acceptedEdges = topology.Edges(e =>
      !RejectIncoming.Contains(e.Item1) && Internet2.Internet2Nodes.Contains(e.Item2));
    // check all the accepted edges
    Assert.All(acceptedEdges, edge =>
    {
      var transferCheck = new TransferCheck<RouteEnvironment>(transfer[edge]);
      var result = transferCheck.Solve(route, r => r.GetPrefix().IsValidPrefixLength(), r => r.GetResultValue());
      _testOutputHelper.WriteLine($"Edge {edge}: {result}");
      Assert.NotNull(result);
    });
  }

  [Fact]
  public void WashNeighborTransferAcceptsGoodRoute()
  {
    var (_, transfer) = Internet2Ast.TopologyAndTransfer();
    // export + import
    var transferCheck = new TransferCheck<RouteEnvironment>(transfer[("192.122.183.13", "wash")]);
    var result = transferCheck.Verify(Zen.Symbolic<RouteEnvironment>("r"), IsGoodUMichRoute, r => r.GetResultValue());
    Assert.Null(result);
  }

  [Fact]
  public void WashNeighborTransferRejectsAsSetRoute()
  {
    var (_, transfer) = Internet2Ast.TopologyAndTransfer();
    var transferCheck = new TransferCheck<RouteEnvironment>(transfer[("192.122.183.13", "wash")]);
    // _testOutputHelper.WriteLine(transferCheck.Transfer(Zen.Symbolic<RouteEnvironment>("route")).Format());
    var result = transferCheck.Verify(Zen.Symbolic<RouteEnvironment>("r"),
      // constrain the route to have an AsSet element that forces it to be filtered
      route =>
        Zen.Or(route.GetAsSet().Contains(Internet2.NlrAs), route.GetAsSet().Contains(Internet2.PrivateAs),
          route.GetAsSet().Contains(Internet2.CommercialAs)),
      r => Zen.Not(r.GetResultValue()));
    Assert.Null(result);
  }

  [Fact]
  public void HousNeighborRejectsPrivateRoute()
  {
    var (_, transfer) = Internet2Ast.TopologyAndTransfer();
    // 64.57.28.149 is the [NETPLUS] Level(3) IP SIP Commodity | I2-S08834 neighbor
    var transferCheck = new TransferCheck<RouteEnvironment>(transfer[("64.57.28.149", "hous")]);
    var result = transferCheck.Verify(Zen.Symbolic<RouteEnvironment>("r"),
      // constrain the route to be from a private AS
      r => r.GetAsSet().Contains(Internet2.PrivateAs),
      r => Zen.Not(r.GetResultValue()));
    Assert.Null(result);
  }

  [Fact]
  public void SomeNeighborAcceptsAsSetRoute()
  {
    var (topology, transfer) = Internet2Ast.TopologyAndTransfer(trackTerms: true);
    // iterate over the edges from non-rejected neighbors to Internet2 nodes
    var acceptedEdges = topology.Edges(e =>
      !RejectIncoming.Contains(e.Item1) && Internet2.Internet2Nodes.Contains(e.Item2));
    Assert.Contains(acceptedEdges, edge =>
    {
      var transferCheck = new TransferCheck<RouteEnvironment>(transfer[edge]);
      var result = transferCheck.Solve(Zen.Symbolic<RouteEnvironment>("r"),
        r => Zen.And(r.GetResultValue(),
          r.GetLocalDefaultAction(),
          r.NonTerminated(),
          // has one of the filtered AsSet elements
          Zen.Or(r.GetAsSet().Contains(Internet2.NlrAs), r.GetAsSet().Contains(Internet2.PrivateAs),
            r.GetAsSet().Contains(Internet2.CommercialAs)),
          r.GetPrefix().IsValidPrefixLength()),
        r => r.GetResultValue());
      return result is not null;
    });
  }

  [Fact]
  public void WashReachableInductiveCheckPasses()
  {
    var (topology, transfer) = Internet2Ast.TopologyAndTransfer();
    var externalNodes = Internet2Ast.Externals.Select(i => i.Name);
    var net = Internet2.Reachable(topology, externalNodes)
      .ToNetwork(topology, transfer, RouteEnvironmentExtensions.MinOptional);
    NetworkAsserts.Sound(net, SmtCheck.Inductive, "wash");
  }

  [Fact]
  public void Internet2ReachableMonolithic()
  {
    var (topology, transfer) = Internet2Ast.TopologyAndTransfer();
    var externalNodes = Internet2Ast.Externals.Select(i => i.Name);
    var net = Internet2.Reachable(topology, externalNodes)
      .ToNetwork(topology, transfer, RouteEnvironmentExtensions.MinOptional);
    NetworkAsserts.Sound(net, SmtCheck.Monolithic);
  }

  [Theory]
  [InlineData(SmtCheck.Monolithic)]
  [InlineData(SmtCheck.Modular)]
  public void Internet2BadPropertyFails(SmtCheck check)
  {
    var (topology, transfer) = Internet2Ast.TopologyAndTransfer();
    var routes = SymbolicValue.SymbolicDictionary<RouteEnvironment>("route", topology.Nodes);
    var initialRoutes = topology.MapNodes(n => routes[n].Value);
    var monolithicProperties = topology.MapNodes(_ => Lang.False<RouteEnvironment>());
    var modularProperties = topology.MapNodes(n => Lang.Globally(monolithicProperties[n]));
    var query = new NetworkQuery<RouteEnvironment, string>(initialRoutes, routes.Values.Cast<ISymbolic>().ToArray(),
      monolithicProperties, modularProperties, modularProperties);
    var net = query.ToNetwork(topology, transfer, RouteEnvironmentExtensions.MinOptional);
    NetworkAsserts.Unsound(net, check);
  }
}
