using System;
using System.Collections.Generic;
using System.Numerics;
using Timepiece.Networks;
using Xunit;
using ZenLib;

namespace Timepiece.Tests;

public static class FaultToleranceTests
{
  private static FaultTolerance<Unit> UnitFtNet(
    Func<SymbolicValue<(string, string)>[], Dictionary<string, Func<Zen<Option<Unit>>, Zen<BigInteger>, Zen<bool>>>>
      annotations)
  {
    var topology = Topologies.Complete(3);

    var initialValues = new Dictionary<string, Zen<Option<Unit>>>
    {
      {"A", Option.Some(new Unit())},
      {"B", Option.None<Unit>()},
      {"C", Option.None<Unit>()}
    };

    var modularProperties = topology.MapNodes(_ => Lang.Finally(new BigInteger(2), Lang.IsSome<Unit>()));
    var monolithicProperties = topology.MapNodes(_ => Lang.IsSome<Unit>());

    var failedEdges = Zen.Symbolic<FSeq<(string, string)>>(depth: topology.NEdges);

    var unitNetwork = new UnitNetwork<Unit>(topology, new SymbolicValue<Unit>[] { });
    return new FaultTolerance<Unit>(unitNetwork, initialValues, annotations, modularProperties, monolithicProperties,
      failedEdges, 1);
  }

  [Fact]
  public static void SoundAnnotationsPassChecks()
  {
    var annotations =
      new Func<SymbolicValue<(string, string)>[],
        Dictionary<string, Func<Zen<Option<Unit>>, Zen<BigInteger>, Zen<bool>>>>(edges =>
        new Dictionary<string, Func<Zen<Option<Unit>>, Zen<BigInteger>, Zen<bool>>>
        {
          {"A", Lang.Globally(Lang.IsSome<Unit>())},
          {
            "B",
            Lang.Finally(
              Zen.If<BigInteger>(FaultTolerance<Unit>.IsFailed(edges, ("A", "B")), new BigInteger(2),
                new BigInteger(1)),
              Lang.IsSome<Unit>())
          },
          {
            "C", Lang.Finally(
              Zen.If<BigInteger>(FaultTolerance<Unit>.IsFailed(edges, ("A", "C")), new BigInteger(2),
                new BigInteger(1)),
              Lang.IsSome<Unit>())
          }
        });

    var net = UnitFtNet(annotations);

    NetworkAssert.CheckSound(net);
  }

  [Fact]
  public static void UnsoundAnnotationsFailChecks()
  {
    var annotations =
      new Func<SymbolicValue<(string, string)>[],
        Dictionary<string, Func<Zen<Option<Unit>>, Zen<BigInteger>, Zen<bool>>>>(_ =>
        new Dictionary<string, Func<Zen<Option<Unit>>, Zen<BigInteger>, Zen<bool>>>
        {
          {"A", Lang.Globally(Lang.IsSome<Unit>())},
          {"B", Lang.Finally(new BigInteger(2), Lang.IsSome<Unit>())},
          {"C", Lang.Finally(new BigInteger(2), Lang.IsSome<Unit>())}
        });

    var net = UnitFtNet(annotations);

    // inductive check fails
    NetworkAssert.CheckUnsound(net);
  }
}
