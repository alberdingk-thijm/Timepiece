using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Karesansui;

/// <summary>
///     Represents the topology of an NV network.
/// </summary>
public class Topology
{
  /// <summary>
  ///     The number of edges in the network.
  /// </summary>
  public int NEdges { get; }

  /// <summary>
  ///     The edges for each node in the network.
  /// </summary>
  private readonly Dictionary<string, List<string>> _neighbors;

  /// <summary>
  ///     The nodes in the network and their names.
  /// </summary>
  public string[] Nodes { get; }

  /// <summary>
  ///     Construct a Topology given a mapping from nodes to their predecessors.
  /// </summary>
  public Topology(Dictionary<string, List<string>> edges)
  {
    _neighbors = edges;
    NEdges = _neighbors.Sum(p => p.Value.Count);
    Nodes = _neighbors.Keys.ToArray();
  }

  public string this[uint id] => Nodes[id];

  /// <summary>
  ///     Return the predecessors of a given node.
  /// </summary>
  public List<string> this[string node] => _neighbors[node];

  /// <summary>
  /// Return true if the topology contains the given node.
  /// </summary>
  /// <param name="node">A node.</param>
  /// <returns>True if the node is present, false otherwise.</returns>
  public bool HasNode(string node)
  {
    return _neighbors.ContainsKey(node);
  }

  /// <summary>
  ///     Return a new Topology generated from the given JSON string
  ///     representing an adjacency list.
  /// </summary>
  /// <param name="json">A string in JSON format representing an adjacency list.</param>
  /// <returns></returns>
  public static Topology FromJson(string json)
  {
    return new Topology(JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json));
  }

  /// <summary>
  ///     Return a dictionary mapping each node in the topology with a given function.
  /// </summary>
  /// <param name="nodeFunc">The function over every node.</param>
  /// <typeparam name="T">The return type of the function.</typeparam>
  /// <returns>A dictionary representing the result of the function for every node.</returns>
  public Dictionary<string, T> ForAllNodes<T>(Func<string, T> nodeFunc)
  {
    return new Dictionary<string, T>(
      Nodes.Select(node => new KeyValuePair<string, T>(node, nodeFunc(node))));
  }

  public TAcc FoldNodes<TAcc>(TAcc initial, Func<TAcc, string, TAcc> f)
  {
    return Nodes.Aggregate(initial, f);
  }

  public Dictionary<(string, string), T> ForAllEdges<T>(Func<(string, string), T> edgeFunc)
  {
    var edges = _neighbors
      .SelectMany(nodeNeighbors => nodeNeighbors.Value, (node, nbr) => (node.Key, nbr))
      .Select(e => new KeyValuePair<(string, string), T>(e, edgeFunc(e)));
    return new Dictionary<(string, string), T>(edges);
  }

  public TAcc FoldEdges<TAcc>(TAcc initial, Func<TAcc, (string, string), TAcc> f)
  {
    var edges = _neighbors
      .SelectMany(nodeNeighbors => nodeNeighbors.Value, (node, nbr) => (node.Key, nbr));
    return edges.Aggregate(initial, f);
  }
}

public static class Default
{
  // helper method to generate node names ala Excel columns
  // adapted from https://stackoverflow.com/a/5384627
  private static string ToBase26(long i)
  {
    // the recursion adds the prefix
    if (i == 0) return "";
    i--;
    // the modulo is used to get the next char, looping back from 'Z' to 'A'
    return ToBase26(i / 26) + (char) ('A' + i % 26);
  }

  /// <summary>
  ///     Create a path digraph topology.
  /// </summary>
  /// <param name="numNodes">Number of nodes in topology.</param>
  /// <returns></returns>
  public static Topology Path(int numNodes)
  {
    var neighbors = new Dictionary<string, List<string>>();
    for (var i = 0; i < numNodes; i++) neighbors.Add(ToBase26(i + 1), new List<string>());

    var nodes = neighbors.Keys.ToArray();
    for (var i = 1; i < numNodes; i++)
    {
      // add a pair of edges in sequence
      neighbors[nodes[i - 1]].Add(nodes[i]);
      neighbors[nodes[i]].Add(nodes[i - 1]);
    }

    return new Topology(neighbors);
  }

  /// <summary>
  ///     Create a complete digraph topology.
  /// </summary>
  /// <param name="numNodes">Number of nodes in topology.</param>
  /// <returns></returns>
  public static Topology Complete(int numNodes)
  {
    var neighbors = new Dictionary<string, List<string>>();
    for (var i = 0; i < numNodes; i++) neighbors.Add(ToBase26(i + 1), new List<string>());

    var nodes = neighbors.Keys;
    foreach (var (node, adj) in neighbors)
      // add all other nodes except the current one
      adj.AddRange(nodes.Where(n => n != node));

    return new Topology(neighbors);
  }
}
