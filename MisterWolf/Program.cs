﻿// See https://aka.ms/new-console-template for more information

using MisterWolf;
using Timepiece;
using ZenLib;

Infer<bool> Reachability(Topology topology, Dictionary<string, Zen<bool>> initialValues)
{
  // initially, the route can be anything
  var beforeInvariants = topology.MapNodes(_ => Lang.True<bool>());
  // eventually, it must be true
  var afterInvariants = topology.MapNodes(_ => Lang.Identity<bool>());

  return new Infer<bool>(topology, topology.MapEdges(_ => Lang.Identity<bool>()), Zen.Or, initialValues,
    beforeInvariants, afterInvariants);
}

Infer<bool> ReachabilityStrong(Topology topology, Dictionary<string, Zen<bool>> initialValues)
{
  // initially, the route is false
  var beforeInvariants = topology.MapNodes(_ => Lang.Not(Lang.Identity<bool>()));
  // eventually, it must be true
  var afterInvariants = topology.MapNodes(_ => Lang.Identity<bool>());

  return new Infer<bool>(topology, topology.MapEdges(_ => Lang.Identity<bool>()), Zen.Or, initialValues,
    beforeInvariants, afterInvariants);
}

Infer<Option<uint>> PathLength(Topology topology, Dictionary<string, Zen<Option<uint>>> initialValues,
  Dictionary<string, uint> upperBounds)
{
  var beforeInvariants = topology.MapNodes(_ => Lang.True<Option<uint>>());
  // eventually, the route must be less than the specified max
  var afterInvariants = new Dictionary<string, Func<Zen<Option<uint>>, Zen<bool>>>(upperBounds.Select(b =>
    new KeyValuePair<string, Func<Zen<Option<uint>>, Zen<bool>>>(b.Key, Lang.IfSome<uint>(x => x <= b.Value))));
  return new Infer<Option<uint>>(topology, topology.MapEdges(_ => Lang.Omap<uint, uint>(x => x + 1)),
    Lang.Omap2<uint>(Zen.Min), initialValues, beforeInvariants, afterInvariants);
}

var topology = Topologies.Path(3);
var initialValues = topology.MapNodes(n => n == "A" ? Zen.True() : Zen.False());

if (args.Length == 0)
{
  Console.WriteLine("Please specify a benchmark to run!");
  return;
}

foreach (var arg in args)
{
  dynamic infer;
  switch (arg)
  {
    case "reach":
      infer = Reachability(topology, initialValues);
      break;
    case "reach2":
      infer = ReachabilityStrong(topology, initialValues);
      break;
    case "len":
      infer = PathLength(topology, topology.MapNodes(n => n == "A" ? Option.Some(0U) : Option.Null<uint>()),
        new Dictionary<string, uint>(topology.Nodes.Select((node, index) =>
          new KeyValuePair<string, uint>(node, (uint) index))));
      break;
    default:
      throw new ArgumentOutOfRangeException(arg);
  }

  var net = infer.ToNetwork<Unit>();
  Profile.RunAnnotated(net);
}
