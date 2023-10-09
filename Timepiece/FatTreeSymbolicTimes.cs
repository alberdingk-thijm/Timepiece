using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ZenLib;

namespace Timepiece;

public static class FatTreeSymbolicTimes
{
  /// <summary>
  /// Return a list of <c>numTimes</c> many symbolic witness times,
  /// where <c>time[i] &lt; time[i+1]</c> for all <c>i &lt; numTimes</c>.
  /// </summary>
  /// <returns></returns>
  public static IReadOnlyList<SymbolicValue<BigInteger>> AscendingSymbolicTimes(int numTimes)
  {
    var startTime = new SymbolicTime($"tau-0");
    var times = new List<SymbolicTime> {startTime};
    for (var i = 1; i < numTimes; i++)
    {
      // each time needs to be bigger than the last
      var nextTime =
        new SymbolicTime($"tau-{i}", times.Last());
      times.Add(nextTime);
    }

    return times;
  }

  /// <summary>
  /// Return a mapping over the nodes in <paramref name="g"/> where each node has a Finally annotation
  /// with a witness time chosen from <paramref name="symbolicTimes"/> according to the node's distance
  /// from the <paramref name="destination"/> node, and with the predicate <paramref name="afterPredicate"/>.
  /// </summary>
  /// <param name="g"></param>
  /// <param name="destination"></param>
  /// <param name="afterPredicate"></param>
  /// <param name="symbolicTimes"></param>
  /// <typeparam name="RouteType"></typeparam>
  /// <returns></returns>
  public static Dictionary<string, Func<Zen<RouteType>, Zen<BigInteger>, Zen<bool>>>
    FinallyAnnotations<RouteType>(NodeLabelledDigraph<string, int> g, string destination,
      Func<Zen<RouteType>, Zen<bool>> afterPredicate, IReadOnlyList<Zen<BigInteger>> symbolicTimes) =>
    g.MapNodes(n =>
    {
      var dist = n.DistanceFromDestinationEdge(g.L(n), destination, g.L(destination));
      return Lang.Finally(symbolicTimes[dist], afterPredicate);
    });

  public static Dictionary<string, Func<Zen<RouteType>, Zen<BigInteger>, Zen<bool>>>
    FinallyAnnotations<RouteType>(NodeLabelledDigraph<string, int> g, SymbolicDestination destination,
      Func<Zen<RouteType>, Zen<bool>> afterPredicate, IReadOnlyList<Zen<BigInteger>> symbolicTimes) =>
    g.MapNodes(n => Lang.Finally(destination.SymbolicDistanceCases(n, g.L(n), symbolicTimes), afterPredicate));
}